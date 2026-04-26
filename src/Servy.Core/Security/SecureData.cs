using Servy.Core.Logging;
using System;
using System.IO;
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
        private readonly byte[] _v1MasterKey;
        private readonly byte[] _v1StaticIv;
        private readonly byte[] _v2EncryptionKey;
        private readonly byte[] _v2HmacKey;
        private bool _disposed;

        private const int BufferSize = 4096;
        private const string EncryptMarker = "SERVY_ENC:";
        private const string V2Marker = EncryptMarker + "v2:";
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
            if (protectedKeyProvider == null)
                throw new ArgumentNullException(nameof(protectedKeyProvider));

            byte[] masterKey = null;
            byte[] v1StaticIv = null;
            try
            {
                masterKey = protectedKeyProvider.GetKey();
                v1StaticIv = protectedKeyProvider.GetIV();

                _v2EncryptionKey = DeriveHkdf(masterKey, HkdfSalt, "V2_AES_ENCRYPTION");
                _v2HmacKey = DeriveHkdf(masterKey, HkdfSalt, "V2_HMAC_AUTHENTICATION");

                _v1MasterKey = (byte[])masterKey.Clone();
                _v1StaticIv = (byte[])v1StaticIv.Clone();
            }
            finally
            {
                if (masterKey != null) Array.Clear(masterKey, 0, masterKey.Length);
                if (v1StaticIv != null) Array.Clear(v1StaticIv, 0, v1StaticIv.Length);
            }
        }

        /// <summary>
        /// Encrypts the specified plain text using AES-256-CBC and secures it with an HMAC-SHA256 signature.
        /// </summary>
        /// <remarks>
        /// This method implements the <b>v2 (Authenticated Encryption)</b> format using an "Encrypt-then-MAC" (EtM) approach:
        /// <list type="number">
        /// <item><description>Generates a cryptographically strong random Initialization Vector (IV).</description></item>
        /// <item><description>Encrypts the UTF-8 encoded <paramref name="plainText"/> using AES-256 in CBC mode with PKCS7 padding.</description></item>
        /// <item><description>Calculates an HMAC-SHA256 signature over both the IV and the resulting ciphertext to ensure integrity and authenticity.</description></item>
        /// <item><description>Packages the result into a versioned, Base64-encoded string prefixed with the service marker.</description></item>
        /// </list>
        /// <para><b>Memory Management:</b> Uses chunked stream processing to avoid Large Object Heap (LOH) fragmentation and 
        /// performs explicit <see cref="Array.Clear(Array, int, int)"/> on all sensitive buffers before the method returns.</para>
        /// </remarks>
        /// <param name="plainText">The sensitive string to be encrypted.</param>
        /// <returns>A string formatted as: {Marker}v2:{Base64(IV + Ciphertext + HMAC)}</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="plainText"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="plainText"/> is empty.</exception>
        public string Encrypt(string plainText)
        {
            ThrowIfDisposed();

            if (plainText == null) throw new ArgumentNullException(nameof(plainText));
            if (plainText.Length == 0) throw new ArgumentException("Cannot encrypt empty string.", nameof(plainText));

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] iv = new byte[IvSize];
            byte[] payload = null;

            using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(iv); }

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = _v2EncryptionKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    int maxCipherLen = ((plainBytes.Length / IvSize) + 1) * IvSize;
                    payload = new byte[IvSize + maxCipherLen + HmacSize];
                    Buffer.BlockCopy(iv, 0, payload, 0, IvSize);

                    int actualCipherLen;
                    using (var encryptor = aes.CreateEncryptor())
                    using (var ms = new MemoryStream(payload, IvSize, maxCipherLen))
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        // Chunked write to prevent Large Object Heap (LOH) fragmentation for large strings
                        int offset = 0;
                        while (offset < plainBytes.Length)
                        {
                            int count = Math.Min(BufferSize, plainBytes.Length - offset);
                            cs.Write(plainBytes, offset, count);
                            offset += count;
                        }
                        cs.FlushFinalBlock();
                        actualCipherLen = (int)ms.Position;
                    }

                    using (var hmacSha = new HMACSHA256(_v2HmacKey))
                    {
                        int totalToHash = IvSize + actualCipherLen;
                        hmacSha.TransformBlock(payload, 0, totalToHash, null, 0);
                        hmacSha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        Buffer.BlockCopy(hmacSha.Hash, 0, payload, totalToHash, HmacSize);

                        return $"{V2Marker}{Convert.ToBase64String(payload, 0, totalToHash + HmacSize)}";
                    }
                }
            }
            finally
            {
                Array.Clear(plainBytes, 0, plainBytes.Length);
                Array.Clear(iv, 0, iv.Length);
                if (payload != null) Array.Clear(payload, 0, payload.Length);
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
            ThrowIfDisposed();

            if (cipherText == null) throw new ArgumentNullException(nameof(cipherText));
            if (cipherText.Length == 0) throw new ArgumentException("Cannot decrypt empty string.", nameof(cipherText));

            bool hasMarker = cipherText.StartsWith(EncryptMarker, StringComparison.Ordinal);
            string payload = hasMarker ? cipherText.Substring(EncryptMarker.Length) : cipherText;

            try
            {
                if (payload.StartsWith("v2:")) return DecryptV2(payload.Substring(3));

                if (payload.StartsWith("v1:")) return DecryptV1(payload.Substring(3));

                // Fallback for legacy payloads that were encrypted but not version-tagged
                string rawPayload = payload.ToString();

                // Version 1 Legacy Detection
                if (IsStrictBase64(rawPayload))
                {
                    return DecryptV1(rawPayload);
                }

                // Explicitly log when data is processed as plaintext.
                // This allows admins to find unencrypted passwords or config values in the DB (#568).
                Logger.Warn("Decryption bypassed: Input does not match any known encryption format. Returning as plaintext.");
                return rawPayload;
            }
            catch (Exception ex) when (ex is FormatException || ex is CryptographicException)
            {
                // Elevated from Debug to Warn. 
                // If a string HAS an encryption marker but fails to decrypt, it's a security event or data corruption.
                Logger.Warn($"Decryption failed for marked payload ({ex.GetType().Name}): {ex.Message} Returning original input.");
                return cipherText;
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
            finally { Array.Clear(cipherBytes, 0, cipherBytes.Length); }
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
            byte[] combined = Convert.FromBase64String(payload);
            if (combined.Length < (IvSize + HmacSize))
                throw new CryptographicException("V2 payload length is insufficient.");

            byte[] iv = new byte[IvSize];
            byte[] expectedHmac = new byte[HmacSize];

            try
            {
                int ciphertextLen = combined.Length - IvSize - HmacSize;
                Buffer.BlockCopy(combined, 0, iv, 0, IvSize);
                Buffer.BlockCopy(combined, combined.Length - HmacSize, expectedHmac, 0, HmacSize);

                // Constant-time HMAC verification
                using (var hmacSha = new HMACSHA256(_v2HmacKey))
                {
                    int totalToHash = IvSize + ciphertextLen;
                    hmacSha.TransformBlock(combined, 0, totalToHash, null, 0);
                    hmacSha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                    if (!CryptographicEquals(expectedHmac, hmacSha.Hash))
                        throw new CryptographicException("HMAC integrity check failed.");
                }

                using (var aes = Aes.Create())
                {
                    aes.Key = _v2EncryptionKey;
                    aes.IV = iv;
                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(combined, IvSize, ciphertextLen))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs, Encoding.UTF8))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            finally
            {
                Array.Clear(combined, 0, combined.Length);
                Array.Clear(iv, 0, iv.Length);
                Array.Clear(expectedHmac, 0, expectedHmac.Length);
            }
        }

        /// <summary>
        /// Implements HKDF-Extract-and-Expand (RFC 5869) to derive cryptographically strong sub-keys from a master key.
        /// </summary>
        /// <remarks>
        /// This implementation provides key separation by ensuring that even if one derived key is compromised, 
        /// the others remain secure. 
        /// <para>
        /// 1. <b>Extract:</b> Mixes the Input Keying Material (IKM) with a salt to produce a fixed-length Pseudorandom Key (PRK).
        /// </para>
        /// <para>
        /// 2. <b>Expand:</b> Uses the PRK and a context-specific 'info' string to generate the final output key. 
        /// This specific implementation is simplified for a single-iteration expansion (suitable for outputs ≤ 32 bytes).
        /// </para>
        /// </remarks>
        /// <param name="ikm">The Input Keying Material (the master secret).</param>
        /// <param name="salt">A non-secret random value (salt) to improve the extraction quality.</param>
        /// <param name="info">Context-specific string (e.g., "V2_AES_ENCRYPTION") to ensure unique keys per use case.</param>
        /// <returns>A 32-byte derived key.</returns>
        private static byte[] DeriveHkdf(byte[] ikm, byte[] salt, string info)
        {
            byte[] prk = null;
            byte[] infoBytes = null;
            byte[] buffer = null;

            try
            {
                infoBytes = Encoding.UTF8.GetBytes(info);
                buffer = new byte[infoBytes.Length + 1];

                // HKDF-Extract
                using (var hmacExtract = new HMACSHA256(salt))
                {
                    prk = hmacExtract.ComputeHash(ikm);
                }

                // HKDF-Expand
                using (var hmacExpand = new HMACSHA256(prk))
                {
                    Buffer.BlockCopy(infoBytes, 0, buffer, 0, infoBytes.Length);
                    buffer[buffer.Length - 1] = 0x01; // First iteration
                    return hmacExpand.ComputeHash(buffer);
                }
            }
            finally
            {
                if (prk != null) Array.Clear(prk, 0, prk.Length);
                if (infoBytes != null) Array.Clear(infoBytes, 0, infoBytes.Length);
                if (buffer != null) Array.Clear(buffer, 0, buffer.Length);
            }
        }

        /// <summary>
        /// Performs a bitwise comparison of two byte arrays in constant time to prevent timing attacks.
        /// </summary>
        /// <param name="a">The first byte array to compare (e.g., the expected HMAC).</param>
        /// <param name="b">The second byte array to compare (e.g., the provided HMAC).</param>
        /// <returns>
        /// True if the arrays are non-null and identical in content; otherwise, false. 
        /// Note: Returns false if both are null to prevent null-bypass vulnerabilities.
        /// </returns>
        private static bool CryptographicEquals(byte[] a, byte[] b)
        {
            // Fail closed: If either array is missing, or if lengths differ, they are not 'equal' 
            // in a cryptographic sense. We return false for dual-nulls to ensure an explicit 
            // signature is always required for authentication.
            if (a == null || b == null || a.Length != b.Length) return false;

            int diff = 0;

            // Constant-time loop: We iterate through the entire length regardless of where 
            // a difference is found to prevent side-channel timing attacks.
            for (int i = 0; i < a.Length; i++) { diff |= a[i] ^ b[i]; }

            return diff == 0;
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

        /// <summary>
        /// Performs strict memory-zeroing of all sensitive cryptographic key material.
        /// </summary>
        public void Dispose()
        {
            // Pass 'true' because we are being called explicitly by the user's code.
            Dispose(true);

            // Tell the GC that this object no longer needs its Finalizer called.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs strict memory-zeroing of all sensitive cryptographic key material.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Zero-out sensitive managed byte arrays.
                // Even though these are managed objects, we treat them as critical 
                // resources that must be wiped before the memory is reclaimed.
                if (_v1MasterKey != null) Array.Clear(_v1MasterKey, 0, _v1MasterKey.Length);
                if (_v1StaticIv != null) Array.Clear(_v1StaticIv, 0, _v1StaticIv.Length);
                if (_v2EncryptionKey != null) Array.Clear(_v2EncryptionKey, 0, _v2EncryptionKey.Length);
                if (_v2HmacKey != null) Array.Clear(_v2HmacKey, 0, _v2HmacKey.Length);
            }

            _disposed = true;
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if this instance has already been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }
}