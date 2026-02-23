using System.Buffers.Text;
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
        /// Encrypts the specified plain text using AES-256-CBC and secures it with an HMAC-SHA256 signature.
        /// </summary>
        /// <remarks>
        /// This method implements high-performance <b>v2 (Authenticated Encryption)</b>:
        /// <list type="number">
        /// <item><description>Pre-calculates required buffer sizes to avoid intermediate allocations.</description></item>
        /// <item><description>Uses <see cref="Span{T}"/> for zero-copy slicing of the payload buffer.</description></item>
        /// <item><description>Encrypts using <c>EncryptCbc</c> for direct OS-level cryptographic execution.</description></item>
        /// <item><description>Computes HMAC-SHA256 over the [IV + Ciphertext] for integrity (Encrypt-then-MAC).</description></item>
        /// </list>
        /// </remarks>
        /// <param name="plainText">The sensitive string to be encrypted.</param>
        /// <returns>A versioned string formatted as: {Marker}v2:{Base64(IV + Ciphertext + HMAC)}</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="plainText"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="plainText"/> is empty.</exception>
        public string Encrypt(string plainText)
        {
            if (plainText == null) throw new ArgumentNullException(nameof(plainText));
            if (plainText.Length == 0) throw new ArgumentException("Cannot decrypt empty string.", nameof(plainText));

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            using var aes = Aes.Create();
            aes.Key = _v2EncryptionKey;

            // Use the .NET helper to get the exact ciphertext size (including PKCS7 padding)
            int ciphertextLen = aes.GetCiphertextLengthCbc(plainBytes.Length, PaddingMode.PKCS7);

            int binaryPayloadLen = IvSize + ciphertextLen + HmacSize;
            byte[] binaryPayload = new byte[binaryPayloadLen];

            try
            {
                // 1. Encrypt and Hash as before
                Span<byte> payloadSpan = binaryPayload;
                RandomNumberGenerator.Fill(payloadSpan.Slice(0, IvSize));
                aes.EncryptCbc(plainBytes, payloadSpan.Slice(0, IvSize), payloadSpan.Slice(IvSize, ciphertextLen), PaddingMode.PKCS7);
                HMACSHA256.HashData(_v2HmacKey, payloadSpan.Slice(0, IvSize + ciphertextLen), payloadSpan.Slice(IvSize + ciphertextLen, HmacSize));

                string marker = $"{EncryptMarker}v2:";

                // Exact Base64 length: every 3 input bytes -> 4 output chars, always padded to multiple of 4
                int exactBase64Len = ((binaryPayloadLen + 2) / 3) * 4;

                return string.Create(marker.Length + exactBase64Len, (binaryPayload, marker), (chars, state) =>
                {
                    state.marker.AsSpan().CopyTo(chars);
                    // TryToBase64Chars writes exactly exactBase64Len chars (with '=' padding) — no nulls, no trim needed
                    Convert.TryToBase64Chars(state.binaryPayload, chars.Slice(state.marker.Length), out _);
                });
            }
            finally
            {
                // Security hygiene: Wipe sensitive buffers from the heap
                Array.Clear(plainBytes);
                Array.Clear(binaryPayload);
            }
        }

        /// <summary>
        /// Decrypts a ciphertext string by automatically detecting the encryption version (v2, v1, or legacy raw).
        /// </summary>
        /// <remarks>
        /// This method acts as a router for different decryption strategies:
        /// <list type="bullet">
        /// <item>
        /// <term>v2:</term>
        /// <description>Identified by the "v2:" prefix. Uses authenticated AES-256-CBC with HMAC-SHA256 verification.</description>
        /// </item>
        /// <item>
        /// <term>v1:</term>
        /// <description>Identified by the "v1:" prefix. Uses legacy AES decryption with a static IV.</description>
        /// </item>
        /// <item>
        /// <term>Fallback:</term>
        /// <description>If no version prefix is present, the method checks if the string is valid Base64. 
        /// If it is, it attempts v1 decryption. If not, it returns the input as-is (assuming it is already plaintext).</description>
        /// </item>
        /// </list>
        /// Defensive programming: Any <see cref="FormatException"/> or <see cref="CryptographicException"/> 
        /// encountered during the process is caught, and the method gracefully returns the original input.
        /// </remarks>
        /// <param name="cipherText">The versioned ciphertext (with marker) or a raw legacy string.</param>
        /// <returns>
        /// The decrypted plain text if successful; otherwise, the original <paramref name="cipherText"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="cipherText"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="cipherText"/> is empty.</exception>
        public string Decrypt(string cipherText)
        {
            if (cipherText == null) throw new ArgumentNullException(nameof(cipherText));
            if (cipherText.Length == 0) throw new ArgumentException("Cannot decrypt empty string.", nameof(cipherText));
            
            // Use Spans for routing to avoid Substring() allocations
            ReadOnlySpan<char> textSpan = cipherText.AsSpan();
            ReadOnlySpan<char> markerSpan = EncryptMarker.AsSpan();

            bool hasMarker = textSpan.StartsWith(markerSpan, StringComparison.Ordinal);
            ReadOnlySpan<char> payload = hasMarker ? textSpan.Slice(markerSpan.Length) : textSpan;

            try
            {
                if (payload.StartsWith("v2:")) return DecryptV2(payload.Slice(3).ToString());

                if (payload.StartsWith("v1:")) return DecryptV1(payload.Slice(3).ToString());

                // Fallback for legacy payloads that were encrypted but not version-tagged
                string rawPayload = payload.ToString();
                return IsStrictBase64(rawPayload) ? DecryptV1(rawPayload) : rawPayload;
            }
            catch (FormatException) { return payload.ToString(); }
            catch (CryptographicException) { return payload.ToString(); }
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
        /// This follows the "Encrypt-then-MAC" (EtM) security best practice. 
        /// 1. The payload is decomposed into the IV, Ciphertext, and HMAC.
        /// 2. A new HMAC is computed over the IV and Ciphertext and compared against the provided HMAC in constant time.
        /// 3. If integrity is verified, the ciphertext is decrypted using AES-256-CBC.
        /// <para>
        /// <b>Security Note:</b> Decryption only occurs if the HMAC is valid. This prevents "Padding Oracle" attacks 
        /// by treating any manipulation of the ciphertext as an integrity failure rather than a decryption error.
        /// </para>
        /// </remarks>
        /// <param name="payload">The Base64-encoded v2 encrypted string (excluding the version prefix).</param>
        /// <returns>The decrypted UTF-8 string.</returns>
        /// <exception cref="CryptographicException">
        /// Thrown if the payload is truncated, the HMAC is invalid, or decryption fails.
        /// </exception>
        private string DecryptV2(string payload)
        {
            // 1. Decode Base64 to a byte array (Still requires one allocation)
            byte[] combined = Convert.FromBase64String(payload);

            try
            {
                if (combined.Length < (IvSize + HmacSize))
                    throw new CryptographicException("V2 payload length is insufficient.");

                // Create ReadOnlySpans - these are "views" into the 'combined' array (0 copies)
                ReadOnlySpan<byte> combinedSpan = combined;
                ReadOnlySpan<byte> iv = combinedSpan.Slice(0, IvSize);
                ReadOnlySpan<byte> expectedHmac = combinedSpan.Slice(combinedSpan.Length - HmacSize);
                ReadOnlySpan<byte> ciphertext = combinedSpan.Slice(IvSize, combinedSpan.Length - IvSize - HmacSize);
                ReadOnlySpan<byte> dataToHash = combinedSpan.Slice(0, IvSize + ciphertext.Length);

                // 2. High-speed HMAC Verification
                Span<byte> computedHash = stackalloc byte[HmacSize];
                HMACSHA256.TryHashData(_v2HmacKey, dataToHash, computedHash, out _);
                if (!CryptographicOperations.FixedTimeEquals(computedHash, expectedHmac))
                    throw new CryptographicException("HMAC integrity check failed.");

                // 3. Direct AES Decryption (No Streams)
                using (var aes = Aes.Create())
                {
                    aes.Key = _v2EncryptionKey;

                    // Pre-allocate the exact output buffer size
                    // In CBC/PKCS7, the plaintext is at most the size of the ciphertext
                    byte[] outputBuffer = new byte[ciphertext.Length];
                    try
                    {
                        // DecryptCbc is a high-performance Span-based method
                        int bytesWritten = aes.DecryptCbc(ciphertext, iv, outputBuffer, PaddingMode.PKCS7);

                        // Convert bytes directly to string without StreamReader overhead
                        return Encoding.UTF8.GetString(outputBuffer, 0, bytesWritten);
                    }
                    finally
                    {
                        Array.Clear(outputBuffer);
                    }
                }
            }
            finally
            {
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