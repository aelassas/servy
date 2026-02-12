using Servy.Core.Security;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides secure encryption and decryption for service credentials and sensitive data.
    /// Implements V2 Authenticated Encryption (Encrypt-then-MAC) while maintaining backward compatibility.
    /// </summary>
    public class SecurePassword : ISecurePassword
    {
        private readonly IProtectedKeyProvider _protectedKeyProvider;
        private readonly byte[] _key;
        private readonly byte[] _iv;
        private readonly byte[] _hmacKey; // cached HMAC key for efficiency

        private const int BufferSize = 4096; // 4 KB
        private const string EncryptMarker = "SERVY_ENC:";

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurePassword"/> class.
        /// </summary>
        /// <param name="protectedKeyProvider">The provider used to retrieve encryption keys and legacy IVs.</param>
        /// <exception cref="ArgumentNullException">Thrown if protectedKeyProvider is null.</exception>
        public SecurePassword(IProtectedKeyProvider protectedKeyProvider)
        {
            _protectedKeyProvider = protectedKeyProvider ?? throw new ArgumentNullException(nameof(protectedKeyProvider));
            _key = _protectedKeyProvider.GetKey();
            _iv = _protectedKeyProvider.GetIV();
            _hmacKey = HmacKeyFromAesKey(_key);
        }

        /// <summary>
        /// Encrypts plain text using V2 strategy: AES-CBC with a random IV and HMAC-SHA256 for integrity.
        /// </summary>
        /// <param name="plainText">The sensitive string to encrypt.</param>
        /// <returns>A versioned, Base64-encoded string prefixed with the encryption marker.</returns>
        /// <exception cref="ArgumentNullException">Thrown if plainText is null or empty.</exception>
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                throw new ArgumentNullException(nameof(plainText));

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            // Determine AES block size
            int aesBlockSizeBytes;
            using (var aesTemp = Aes.Create())
                aesBlockSizeBytes = aesTemp.BlockSize / 8;

            byte[] iv = new byte[aesBlockSizeBytes];
            byte[] buffer = new byte[BufferSize];
            byte[] payloadBytesWithHmac = null;

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = _key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    // Calculate the exact size: IV (16) + Ciphertext (Padded) + HMAC (32)
                    // AES CBC padding always adds at least 1 byte and up to 1 block.
                    int maxCiphertextLength = ((plainBytes.Length / aesBlockSizeBytes) + 1) * aesBlockSizeBytes;
                    payloadBytesWithHmac = new byte[iv.Length + maxCiphertextLength + 32];

                    // 1. Copy IV to the start of the combined array
                    Buffer.BlockCopy(iv, 0, payloadBytesWithHmac, 0, iv.Length);

                    int actualCiphertextLength = 0;

                    using (var encryptor = aes.CreateEncryptor())
                    // Create a MemoryStream that writes directly into the 'combined' array
                    using (var ms = new MemoryStream(payloadBytesWithHmac, iv.Length, maxCiphertextLength))
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        // Write plaintext in chunks
                        int offset = 0;
                        while (offset < plainBytes.Length)
                        {
                            int chunkSize = Math.Min(buffer.Length, plainBytes.Length - offset);
                            cs.Write(plainBytes, offset, chunkSize);
                            offset += chunkSize;
                        }

                        cs.FlushFinalBlock();
                        // The actual length might be slightly less than max if padding behaves differently, 
                        // though for CBC/PKCS7 it is deterministic.
                        actualCiphertextLength = (int)ms.Position;
                    }

                    // 2. Compute HMAC over [IV + Actual Ciphertext]
                    using (var hmacSha = new HMACSHA256(_hmacKey))
                    {
                        int bytesToHash = iv.Length + actualCiphertextLength;
                        int hmacOffset = 0;

                        // Process the combined array in chunks to update HMAC
                        while (hmacOffset < bytesToHash)
                        {
                            int chunkSize = Math.Min(buffer.Length, bytesToHash - hmacOffset);
                            hmacSha.TransformBlock(payloadBytesWithHmac, hmacOffset, chunkSize, null, 0);
                            hmacOffset += chunkSize;
                        }

                        hmacSha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        byte[] hmac = hmacSha.Hash;

                        // 3. Place HMAC at the end of the actual ciphertext
                        Buffer.BlockCopy(hmac, 0, payloadBytesWithHmac, bytesToHash, hmac.Length);

                        // Final length is IV + Ciphertext + HMAC
                        string result = EncryptMarker + "v2:" + Convert.ToBase64String(payloadBytesWithHmac, 0, bytesToHash + hmac.Length);
                        return result;
                    }
                }
            }
            finally
            {
                // Strict Cleanup
                Array.Clear(plainBytes, 0, plainBytes.Length);
                Array.Clear(buffer, 0, buffer.Length);
                Array.Clear(iv, 0, iv.Length);
                if (payloadBytesWithHmac != null) Array.Clear(payloadBytesWithHmac, 0, payloadBytesWithHmac.Length);
            }
        }

        /// <summary>
        /// Decrypts a string by detecting its version (v1 or v2). 
        /// Supports legacy unencrypted data by returning the input if no marker is found.
        /// </summary>
        /// <param name="cipherText">The encrypted string or legacy raw text.</param>
        /// <returns>The decrypted plain text.</returns>
        /// <exception cref="ArgumentNullException">Thrown if cipherText is null or empty.</exception>
        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                throw new ArgumentNullException(nameof(cipherText));

            bool hasMarker = cipherText.StartsWith(EncryptMarker, StringComparison.Ordinal);
            var payload = hasMarker ? cipherText.Substring(EncryptMarker.Length) : cipherText;


            try
            {
                if (payload.StartsWith("v2:"))
                {
                    return DecryptV2(payload.Substring(3));
                }
                else if (payload.StartsWith("v1:"))
                {
                    return DecryptV1(payload.Substring(3));
                }
                else
                {
                    // If the payload isn't Base64, it's likely legacy raw data from a previous system version
                    if (!IsBase64(payload))
                    {
                        return payload;
                    }

                    // Fallback for early encrypted data that lacked a version prefix
                    return DecryptV1(payload);
                }
            }
            catch
            {
                // Otherwise, return raw payload for unencrypted legacy fields
                return payload;
            }
        }

        #region V1 AES-CBC static IV

        /// <summary>
        /// Performs legacy decryption using a static IV.
        /// </summary>
        /// <param name="payload">Base64 encoded ciphertext.</param>
        /// <returns>Decrypted UTF8 string.</returns>
        private string DecryptV1(string payload)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = _iv;

                var cipherBytes = Convert.FromBase64String(payload);

                try
                {
                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(cipherBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs, Encoding.UTF8))
                    { 
                        return sr.ReadToEnd();
                    }
                }
                finally
                {
                    Array.Clear(cipherBytes, 0, cipherBytes.Length);
                }
            }
        }

        #endregion

        #region V2 AES-CBC + random IV + HMAC

        /// <summary>
        /// Performs V2 decryption including HMAC integrity verification before decryption.
        /// </summary>
        /// <param name="payload">Base64 encoded string containing IV, Ciphertext, and HMAC.</param>
        /// <returns>Decrypted UTF8 string.</returns>
        /// <exception cref="CryptographicException">Thrown if HMAC validation fails or payload is malformed.</exception>
        private string DecryptV2(string payload)
        {
            // Determine AES block size
            int aesBlockSizeBytes;
            using (var aesTemp = Aes.Create())
                aesBlockSizeBytes = aesTemp.BlockSize / 8;

            byte[] combined = null;
            byte[] iv = new byte[aesBlockSizeBytes];
            byte[] buffer = new byte[BufferSize];
            byte[] expectedHmac = new byte[32];

            try
            {
                combined = Convert.FromBase64String(payload);
                if (combined.Length < aesBlockSizeBytes + 32)
                    throw new CryptographicException("Invalid v2 payload: length is too short.");

                int ciphertextLength = combined.Length - iv.Length - expectedHmac.Length;

                // 1. Extract IV and Expected HMAC
                Buffer.BlockCopy(combined, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(combined, combined.Length - expectedHmac.Length, expectedHmac, 0, expectedHmac.Length);

                // 2. VERIFY HMAC (Chunked)
                using (var hmacSha = new HMACSHA256(_hmacKey))
                {
                    int bytesProcessed = 0;
                    int totalToHash = iv.Length + ciphertextLength;

                    while (bytesProcessed < totalToHash)
                    {
                        int chunkSize = Math.Min(buffer.Length, totalToHash - bytesProcessed);
                        hmacSha.TransformBlock(combined, bytesProcessed, chunkSize, null, 0);
                        bytesProcessed += chunkSize;
                    }

                    hmacSha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                    if (!CryptographicEquals(expectedHmac, hmacSha.Hash))
                        throw new CryptographicException("HMAC validation failed. Data may have been tampered with.");
                }

                // 3. DECRYPT (Stream-based for character safety)
                using (var aes = Aes.Create())
                {
                    aes.Key = _key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var decryptor = aes.CreateDecryptor())
                    // MemoryStream here is just a wrapper around the existing 'combined' array (zero-copy)
                    using (var msCipher = new MemoryStream(combined, iv.Length, ciphertextLength))
                    using (var csDecrypt = new CryptoStream(msCipher, decryptor, CryptoStreamMode.Read))
                    // StreamReader handles UTF-8 multi-byte character boundaries correctly
                    using (var sr = new StreamReader(csDecrypt, Encoding.UTF8))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            finally
            {
                if (combined != null) Array.Clear(combined, 0, combined.Length);
                Array.Clear(iv, 0, iv.Length);
                Array.Clear(buffer, 0, buffer.Length);
                Array.Clear(expectedHmac, 0, expectedHmac.Length);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Checks if a string is a valid Base64 encoded string.
        /// </summary>
        private static bool IsBase64(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            // Base64 must be a multiple of 4
            if (value.Length % 4 != 0) return false;

            // Use Span to check characters without allocating a new array
            ReadOnlySpan<char> span = value.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                char c = span[i];
                if (!(char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '='))
                    return false;
            }

            // Additional check: '=' can only be at the end
            int paddingIndex = value.IndexOf('=');
            if (paddingIndex != -1)
            {
                // If '=' is found before the last two positions, it's invalid
                if (paddingIndex < value.Length - 2)
                    return false;

                // If '=' is at the second to last position, the last char must also be '='
                if (paddingIndex == value.Length - 2 && value[value.Length - 1] != '=')
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Derives an HMAC key from the primary AES key using SHA256.
        /// </summary>
        private static byte[] HmacKeyFromAesKey(byte[] aesKey)
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(aesKey);
        }

        /// <summary>
        /// Compares two byte arrays in constant time to prevent side-channel timing attacks.
        /// </summary>
        private static bool CryptographicEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        #endregion
    }
}
