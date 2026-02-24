using System.Security.Cryptography;
using System.Text;

namespace Servy.Core.Security
{
    /// <summary>
    /// Provides thread-safe, authenticated encryption (Encrypt-then-MAC) for service credentials and large configuration files.
    /// Implements salted HKDF for key separation and follows strict memory-zeroing protocols for all sensitive buffers.
    /// Designed for a Singleton lifetime; internal keys are immutable after construction.
    /// </summary>
    public class SecureData : ISecureData
    {
        private readonly IProtectedKeyProvider _protectedKeyProvider;
        private readonly byte[] _v1MasterKey;
        private readonly byte[] _v1StaticIv;
        private readonly byte[] _v2EncryptionKey;
        private readonly byte[] _v2HmacKey;

        private const int BufferSize = 4096;
        private const string EncryptMarker = "SERVY_ENC:";
        private const int HmacSize = 32;
        private const int IvSize = 16;

        /// <summary>
        /// Salt used for HKDF key derivation to provide domain separation.
        /// </summary>
        private static readonly byte[] HkdfSalt = Encoding.UTF8.GetBytes("Servy.Core.Security.v2.Salt");

        /// <summary>
        /// Initializes a new instance of the <see cref="SecureData"/> class.
        /// Performs HKDF key derivation to generate independent keys for encryption and authentication.
        /// </summary>
        /// <remarks>
        /// This constructor follows a "Secure Retrieval and Purge" pattern:
        /// <list type="number">
        /// <item><description>Retrieves the raw master keying material from the <paramref name="protectedKeyProvider"/>.</description></item>
        /// <item><description>Uses HKDF (RFC 5869) to derive independent sub-keys for AES encryption and HMAC authentication.</description></item>
        /// <item><description>Clones the master key for legacy v1 support.</description></item>
        /// <item><description>Security Critical: Immediately clears the temporary <c>masterKey</c> buffer using <see cref="Array.Clear(Array, int, int)"/> to ensure the raw secret does not linger in memory.</description></item>
        /// </list>
        /// </remarks>
        /// <param name="protectedKeyProvider">The provider used to retrieve the master keying material and legacy IV.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="protectedKeyProvider"/> is null.</exception>
        public SecureData(IProtectedKeyProvider protectedKeyProvider)
        {
            _protectedKeyProvider = protectedKeyProvider ?? throw new ArgumentNullException(nameof(protectedKeyProvider));

            byte[]? masterKey = null;
            try
            {
                masterKey = _protectedKeyProvider.GetKey();
                _v1StaticIv = _protectedKeyProvider.GetIV();

                _v2EncryptionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 32, HkdfSalt, Encoding.UTF8.GetBytes("V2_AES_ENCRYPTION"));
                _v2HmacKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 32, HkdfSalt, Encoding.UTF8.GetBytes("V2_HMAC_AUTHENTICATION"));

                _v1MasterKey = (byte[])masterKey.Clone();
            }
            finally
            {
                if (masterKey != null) Array.Clear(masterKey);
            }
        }

        /// <summary>
        /// Encrypts the specified plain text using AES-256-CBC and secures it with an HMAC-SHA256 signature (v2).
        /// </summary>
        /// <remarks>
        /// This method implements an <b>Encrypt-then-MAC (EtM)</b> pattern with zero-copy optimizations:
        /// <list type="bullet">
        /// <item><description>Uses <see cref="Span{T}"/> to manipulate a single contiguous buffer for IV, Ciphertext, and HMAC.</description></item>
        /// <item><description>Leverages <c>string.Create</c> to materialize the final Base64 string directly into memory without intermediate allocations.</description></item>
        /// <item><description>Calculates exact Base64 length to avoid costly post-processing like <c>.Trim()</c>.</description></item>
        /// </list>
        /// </remarks>
        /// <param name="plainText">The sensitive string to encrypt.</param>
        /// <returns>A versioned string formatted as: <c>SERVY_ENC:v2:{Base64(IV + Ciphertext + HMAC)}</c></returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="plainText"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="plainText"/> is empty.</exception>
        public string Encrypt(string plainText)
        {
            // Validation: Ensure we have data to work with
            if (plainText == null) throw new ArgumentNullException(nameof(plainText));
            if (plainText.Length == 0) throw new ArgumentException("Cannot encrypt empty string.", nameof(plainText));

            // Convert string to UTF-8 bytes for cryptographic processing
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            // Initialize AES-256 with the derived V2 encryption key
            using var aes = Aes.Create();
            aes.Key = _v2EncryptionKey;

            // 1. PRE-CALCULATION: Determine exact buffer requirements
            // Get the exact ciphertext size (including PKCS7 padding)
            int ciphertextLen = aes.GetCiphertextLengthCbc(plainBytes.Length, PaddingMode.PKCS7);

            // Total binary payload: [IV (16 bytes)] + [Ciphertext (Variable)] + [HMAC (32 bytes)]
            int binaryPayloadLen = IvSize + ciphertextLen + HmacSize;
            byte[] binaryPayload = new byte[binaryPayloadLen];

            try
            {
                // 2. CRYPTOGRAPHIC OPERATIONS: Direct buffer manipulation via Spans
                Span<byte> payloadSpan = binaryPayload;

                // A. Generate a random IV directly into the head of the buffer
                RandomNumberGenerator.Fill(payloadSpan.Slice(0, IvSize));

                // B. Encrypt directly into the body of the buffer
                // Slice is [IV size] ... [ciphertext size]
                aes.EncryptCbc(plainBytes, payloadSpan.Slice(0, IvSize), payloadSpan.Slice(IvSize, ciphertextLen), PaddingMode.PKCS7);

                // C. Authenticate the data (IV + Ciphertext)
                // Store the resulting 32-byte HMAC at the tail of the buffer
                HMACSHA256.HashData(_v2HmacKey, payloadSpan.Slice(0, IvSize + ciphertextLen), payloadSpan.Slice(IvSize + ciphertextLen, HmacSize));

                // 3. MATERIALIZATION: Optimized String Construction
                string marker = $"{EncryptMarker}v2:";

                // Exact Base64 formula: every 3 input bytes -> 4 output chars, always padded to multiple of 4
                int exactBase64Len = ((binaryPayloadLen + 2) / 3) * 4;

                // Allocate the final string object once and write marker + base64 data directly into it
                return string.Create(marker.Length + exactBase64Len, (binaryPayload, marker), (chars, state) =>
                {
                    // Copy the "SERVY_ENC:v2:" marker into the start of the string
                    state.marker.AsSpan().CopyTo(chars);

                    // Encode the binary payload into the remaining space
                    // TryToBase64Chars writes exactly exactBase64Len chars (with '=' padding if needed)
                    Convert.TryToBase64Chars(state.binaryPayload, chars.Slice(state.marker.Length), out _);
                });
            }
            finally
            {
                // 4. SECURITY HYGIENE: Wipe sensitive buffers from the heap immediately after use
                // plainBytes contains sensitive text; binaryPayload contains the IV and Ciphertext
                Array.Clear(plainBytes);
                Array.Clear(binaryPayload);
            }
        }

        /// <summary>
        /// Decrypts a ciphertext string by automatically detecting the encryption version (v2, v1, or legacy raw).
        /// </summary>
        /// <remarks>
        /// This method implements a high-performance routing logic using <see cref="ReadOnlySpan{T}"/>:
        /// <list type="bullet">
        /// <item><term>v2:</term><description>Authenticated AES-256-CBC (Encrypt-then-MAC).</description></item>
        /// <item><term>v1:</term><description>Legacy AES-256-CBC with static IV (No authentication).</description></item>
        /// <item><term>Fallback:</term><description>Validates if the input is raw Base64; if so, attempts v1 decryption. Otherwise, returns input as-is.</description></item>
        /// </list>
        /// Defensive posture: All cryptographic or formatting failures are caught, returning the original payload to prevent service disruption.
        /// </remarks>
        /// <param name="cipherText">The versioned ciphertext (with marker) or a raw legacy string.</param>
        /// <returns>The decrypted plain text if successful; otherwise, the original <paramref name="cipherText"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="cipherText"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="cipherText"/> is empty.</exception>
        public string Decrypt(string cipherText)
        {
            // Initial validation
            if (cipherText == null) throw new ArgumentNullException(nameof(cipherText));
            if (cipherText.Length == 0) throw new ArgumentException("Cannot decrypt empty string.", nameof(cipherText));

            // PERF: Create a span of the input string to perform prefix checks and slicing 
            // without allocating new string objects on the heap.
            ReadOnlySpan<char> textSpan = cipherText.AsSpan();
            ReadOnlySpan<char> markerSpan = EncryptMarker.AsSpan();

            // Check for the "SERVY_ENC:" prefix
            bool hasMarker = textSpan.StartsWith(markerSpan, StringComparison.Ordinal);

            // Slice the payload (either stripping the marker or using the whole string)
            ReadOnlySpan<char> payload = hasMarker ? textSpan.Slice(markerSpan.Length) : textSpan;

            try
            {
                // Version 2 Routing: Authenticated Encryption
                if (payload.StartsWith("v2:", StringComparison.Ordinal))
                    return DecryptV2(payload.Slice(3).ToString());

                // Version 1 Routing: Legacy Encryption
                if (payload.StartsWith("v1:", StringComparison.Ordinal))
                    return DecryptV1(payload.Slice(3).ToString());

                // FALLBACK LOGIC: Handle legacy data that lacks markers or version tags.
                // Convert the span to a string once for use in legacy methods.
                string rawPayload = payload.ToString();

                // If it looks like Base64, we treat it as an unversioned v1 encrypted string.
                // Otherwise, it is assumed to be already plaintext.
                return IsStrictBase64(rawPayload) ? DecryptV1(rawPayload) : rawPayload;
            }
            catch (FormatException)
            {
                // Graceful fallback for Base64 decoding errors
                return payload.ToString();
            }
            catch (CryptographicException)
            {
                // Graceful fallback for key mismatches or corrupted ciphertext
                return payload.ToString();
            }
        }

        /// <summary>
        /// Internal logic for v1 (legacy) decryption using a static IV and the master key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Security Warning:</b> This method implements a legacy format that lacks an HMAC integrity check. 
        /// It is vulnerable to ciphertext manipulation and does not provide authentication.
        /// </para>
        /// <para>
        /// This version utilizes a static Initialization Vector (IV), which reduces cryptographic variance 
        /// compared to the random IV used in v2. It is maintained strictly for backward compatibility 
        /// with data encrypted prior to the implementation of the authenticated v2 format.
        /// </para>
        /// </remarks>
        /// <param name="payload">The Base64-encoded v1 ciphertext.</param>
        /// <returns>The decrypted UTF-8 string.</returns>
        /// <exception cref="CryptographicException">Thrown if decryption fails (e.g., due to incorrect keying material or corrupted data).</exception>
        private string DecryptV1(string payload)
        {
            byte[] cipherBytes = Convert.FromBase64String(payload);
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = _v1MasterKey;
                    aes.IV = _v1StaticIv;
                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(cipherBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs, Encoding.UTF8))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            finally { Array.Clear(cipherBytes); }
        }

        /// <summary>
        /// Internal logic for v2 decryption. Validates the HMAC-SHA256 signature before attempting AES decryption.
        /// </summary>
        /// <remarks>
        /// This follows the <b>Encrypt-then-MAC (EtM)</b> security best practice:
        /// <list type="number">
        /// <item><description>The payload is decomposed into IV, Ciphertext, and HMAC using zero-copy <see cref="ReadOnlySpan{T}"/> slices.</description></item>
        /// <item><description>A new HMAC is computed over the [IV + Ciphertext] using <c>stackalloc</c> for zero heap allocation.</description></item>
        /// <item><description>The HMAC is verified in constant-time via <see cref="CryptographicOperations.FixedTimeEquals"/>.</description></item>
        /// <item><description>Only if integrity is verified, the ciphertext is decrypted using AES-256-CBC.</description></item>
        /// </list>
        /// </remarks>
        /// <param name="payload">The Base64-encoded v2 encrypted string (excluding the version prefix).</param>
        /// <returns>The decrypted UTF-8 string.</returns>
        /// <exception cref="CryptographicException">
        /// Thrown if the payload is truncated, the HMAC is invalid, or AES decryption fails.
        /// </exception>
        private string DecryptV2(string payload)
        {
            // 1. DECODING: Convert Base64 back to raw bytes
            // Note: This remains the primary allocation in the decryption path.
            byte[] combined = Convert.FromBase64String(payload);

            try
            {
                // Minimum size check: must contain at least an IV (16) and an HMAC (32)
                if (combined.Length < (IvSize + HmacSize))
                    throw new CryptographicException("V2 payload length is insufficient.");

                // Create Spans: These are lightweight "views" into the 'combined' array (0 copies)
                ReadOnlySpan<byte> combinedSpan = combined;
                ReadOnlySpan<byte> iv = combinedSpan.Slice(0, IvSize);
                ReadOnlySpan<byte> expectedHmac = combinedSpan.Slice(combinedSpan.Length - HmacSize);
                ReadOnlySpan<byte> ciphertext = combinedSpan.Slice(IvSize, combinedSpan.Length - IvSize - HmacSize);
                ReadOnlySpan<byte> dataToHash = combinedSpan.Slice(0, IvSize + ciphertext.Length);

                // 2. AUTHENTICATION: High-speed HMAC Verification
                // stackalloc allocates 32 bytes on the stack, bypassing the Garbage Collector entirely.
                Span<byte> computedHash = stackalloc byte[HmacSize];

                // Compute the hash over [IV + Ciphertext]
                HMACSHA256.TryHashData(_v2HmacKey, dataToHash, computedHash, out _);

                // Security Critical: Constant-time comparison prevents side-channel timing attacks.
                if (!CryptographicOperations.FixedTimeEquals(computedHash, expectedHmac))
                    throw new CryptographicException("HMAC integrity check failed.");

                // 3. DECRYPTION: Direct AES execution
                using (var aes = Aes.Create())
                {
                    aes.Key = _v2EncryptionKey;

                    // Pre-allocate the output buffer for plaintext. 
                    // In PKCS7, plaintext length is always <= ciphertext length.
                    byte[] outputBuffer = new byte[ciphertext.Length];
                    try
                    {
                        // DecryptCbc is a high-performance Span-based method that avoids CryptoStream overhead.
                        int bytesWritten = aes.DecryptCbc(ciphertext, iv, outputBuffer, PaddingMode.PKCS7);

                        // Materialize the final string directly from the buffer.
                        return Encoding.UTF8.GetString(outputBuffer, 0, bytesWritten);
                    }
                    finally
                    {
                        // Wipe the plaintext buffer from memory immediately.
                        Array.Clear(outputBuffer);
                    }
                }
            }
            finally
            {
                // Security hygiene: Wipe the combined buffer (containing IV and Ciphertext) from the heap.
                Array.Clear(combined);
            }
        }

        /// <summary>
        /// Validates if a string is structurally valid Base64.
        /// </summary>
        /// <remarks>
        /// This method performs a strict structural check by verifying:
        /// <list type="bullet">
        /// <item><description>The string length is a multiple of 4.</description></item>
        /// <item><description>Characters belong exclusively to the Base64 alphabet (A-Z, a-z, 0-9, +, /).</description></item>
        /// <item><description>Padding ('=') only appears at the very end of the string.</description></item>
        /// </list>
        /// It is used to quickly distinguish between encrypted markers and raw legacy text without triggering 
        /// expensive <see cref="System.FormatException"/> exceptions during decryption attempts.
        /// </remarks>
        /// <param name="value">The string to validate.</param>
        /// <returns>True if the string follows strict Base64 formatting rules; otherwise, false.</returns>
        private static bool IsStrictBase64(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length % 4 != 0)
                return false;

            int length = value.Length;

            for (int i = 0; i < length; i++)
            {
                char c = value[i];

                // If we hit a padding character
                if (c == '=')
                {
                    // Padding can only occur in the last two positions
                    if (i < length - 2) return false;

                    // If it's the second to last char, the very last char MUST also be '='
                    if (i == length - 2 && value[i + 1] != '=') return false;

                    // Once we validate the remaining padding, we're done
                    break;
                }

                // Standard Alphabet Check
                if (!((c >= 'A' && c <= 'Z') ||
                      (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9') ||
                      c == '+' || c == '/'))
                {
                    return false;
                }
            }

            return true;
        }

    }
}