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
        /// <param name="protectedKeyProvider">The provider used to retrieve the master keying material.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="protectedKeyProvider"/> is null.</exception>
        public SecureData(IProtectedKeyProvider protectedKeyProvider)
        {
            _protectedKeyProvider = protectedKeyProvider ?? throw new ArgumentNullException(nameof(protectedKeyProvider));

            byte[]? masterKey = null;
            try
            {
                masterKey = _protectedKeyProvider.GetKey();
                _v1StaticIv = _protectedKeyProvider.GetIV();

                _v2EncryptionKey = DeriveHkdf(masterKey, HkdfSalt, "V2_AES_ENCRYPTION");
                _v2HmacKey = DeriveHkdf(masterKey, HkdfSalt, "V2_HMAC_AUTHENTICATION");

                _v1MasterKey = (byte[])masterKey.Clone();
            }
            finally
            {
                if (masterKey != null) Array.Clear(masterKey, 0, masterKey.Length);
            }
        }

        /// <summary>
        /// Encrypts the specified plain text using AES-256-CBC and HMAC-SHA256.
        /// </summary>
        /// <param name="plainText">The text to encrypt.</param>
        /// <returns>A versioned, Base64-encoded ciphertext string with an integrity marker.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="plainText"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="plainText"/> is empty.</exception>
        public string Encrypt(string plainText)
        {
            if (plainText == null) throw new ArgumentNullException(nameof(plainText));
            if (plainText.Length == 0) throw new ArgumentException("Cannot encrypt empty string.", nameof(plainText));

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] iv = new byte[IvSize];
            byte[]? payload = null;

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
                        Buffer.BlockCopy(hmacSha.Hash!, 0, payload, totalToHash, HmacSize);

                        return $"{EncryptMarker}v2:{Convert.ToBase64String(payload, 0, totalToHash + HmacSize)}";
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
        /// Decrypts a ciphertext string. Supports v2 (authenticated) and v1 (legacy) formats.
        /// </summary>
        /// <param name="cipherText">The versioned ciphertext or legacy raw text.</param>
        /// <returns>The decrypted plain text, or the original input if decryption is not applicable.</returns>
        public string Decrypt(string cipherText)
        {
            if (cipherText == null) throw new ArgumentNullException(nameof(cipherText));
            if (cipherText.Length == 0) throw new ArgumentException("Cannot decrypt empty string.", nameof(cipherText));

            bool hasMarker = cipherText.StartsWith(EncryptMarker, StringComparison.Ordinal);
            string payload = hasMarker ? cipherText.Substring(EncryptMarker.Length) : cipherText;

            try
            {
                if (payload.StartsWith("v2:")) return DecryptV2(payload.Substring(3));

                if (payload.StartsWith("v1:")) return DecryptV1(payload.Substring(3));

                // Fallback for legacy payloads that were encrypted but not version-tagged
                return IsStrictBase64(payload) ? DecryptV1(payload) : payload;
            }
            catch (FormatException) { return payload; }
            catch (CryptographicException) { return payload; }
        }

        /// <summary>
        /// Internal logic for v1 (legacy) decryption using static IV and master key.
        /// </summary>
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

                    if (!CryptographicEquals(expectedHmac, hmacSha.Hash!))
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
        /// Implements HKDF-Extract-and-Expand (RFC 5869) to derive sub-keys.
        /// </summary>
        private static byte[] DeriveHkdf(byte[] ikm, byte[] salt, string info)
        {
            byte[]? prk = null;
            byte[]? infoBytes = null;
            byte[]? buffer = null;

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
        private static bool CryptographicEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) { diff |= a[i] ^ b[i]; }
            return diff == 0;
        }

        /// <summary>
        /// Validates if a string is structurally valid Base64.
        /// Enforces character set, length, and correct padding placement.
        /// </summary>
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