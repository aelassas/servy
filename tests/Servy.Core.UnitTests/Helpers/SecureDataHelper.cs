using System.Security.Cryptography;
using System.Text;

namespace Servy.Core.UnitTests.Helpers
{
    public static class SecureDataHelper
    {
        /// <summary>
        /// Simulates the old V1 encryption logic to test the SUT's DecryptV1 method.
        /// </summary>
        /// <param name="key">The AES key to use for encryption.</param>
        /// <param name="iv">The AES initialization vector to use for encryption.</param>
        /// <param name="plainText">The plaintext to encrypt.</param>
        /// <param name="markerPrefix">The prefix to use for the legacy format. Default is "SERVY_ENC:v1:".</param>
        /// <returns>A string in the legacy V1 format: "SERVY_ENC:v1:{Base64}"</returns>
        public static string CreateLegacyV1EncryptedString(byte[] key, byte[] iv, string plainText, string markerPrefix = "SERVY_ENC:v1:")
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    // Legacy V1 format: "SERVY_ENC:v1:{Base64}"
                    return markerPrefix + Convert.ToBase64String(cipherBytes);
                }
            }
        }
    }
}
