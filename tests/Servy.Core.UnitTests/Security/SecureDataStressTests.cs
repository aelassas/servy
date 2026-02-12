using Moq;
using Servy.Core.Security;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Servy.Core.UnitTests.Security
{
    public class SecureDataStressTests
    {
        private readonly Mock<IProtectedKeyProvider> _mockKeyProvider;
        private readonly byte[] _testKey;
        private readonly byte[] _testIvV1;
        private readonly SecureData _sut;
        private readonly ITestOutputHelper _output;

        public SecureDataStressTests(ITestOutputHelper output)
        {
            _output = output;

            // Setup static keys for consistent testing
            _testKey = new byte[32]; // AES-256
            _testIvV1 = new byte[16]; // AES Block Size

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(_testKey);
                rng.GetBytes(_testIvV1);
            }

            _mockKeyProvider = new Mock<IProtectedKeyProvider>();

            // Use .Clone() so the class clearing the array doesn't wipe the test's key
            _mockKeyProvider.Setup(x => x.GetKey()).Returns(() => (byte[])_testKey.Clone());
            _mockKeyProvider.Setup(x => x.GetIV()).Returns(() => (byte[])_testIvV1.Clone());

            _sut = new SecureData(_mockKeyProvider.Object);
        }

        [Theory]
        [InlineData(1)]  // 1 MB
        [InlineData(10)] // 10 MB
        public void StressTest_V2_EncryptionDecryption_LargePayload(int sizeInMb)
        {
            // Arrange
            string largePlainText = GenerateLargeString(sizeInMb);
            var sw = new Stopwatch();

            _output.WriteLine($"--- V2 STRESS TEST ({sizeInMb} MB) ---");

            // Act - Encryption
            sw.Start();
            string encrypted = _sut.Encrypt(largePlainText);
            sw.Stop();
            long encryptMs = sw.ElapsedMilliseconds;
            _output.WriteLine($"V2 Encrypt Time: {encryptMs}ms");

            // Act - Decryption
            sw.Restart();
            string decrypted = _sut.Decrypt(encrypted);
            sw.Stop();
            long decryptMs = sw.ElapsedMilliseconds;
            _output.WriteLine($"V2 Decrypt Time: {decryptMs}ms");

            // Assert
            Assert.Equal(largePlainText.Length, decrypted.Length);
            Assert.Equal(largePlainText, decrypted);
            _output.WriteLine($"V2 Integrity Verified. Payload Size: {encrypted.Length / 1024.0 / 1024.0:F2} MB (Base64)");
            _output.WriteLine("--------------------------------------\n");
        }

        [Fact]
        public void StressTest_V1_BackwardCompatibility_LargePayload()
        {
            // Arrange
            int sizeInMb = 5;
            string largePlainText = GenerateLargeString(sizeInMb);
            var sw = new Stopwatch();

            // Manually create a V1 payload since the new SUT only encrypts in V2
            string v1Payload = CreateLegacyV1EncryptedString(largePlainText);

            _output.WriteLine($"--- V1 COMPATIBILITY STRESS TEST ({sizeInMb} MB) ---");

            // Act
            sw.Start();
            string decrypted = _sut.Decrypt(v1Payload);
            sw.Stop();

            // Assert
            Assert.Equal(largePlainText, decrypted);
            _output.WriteLine($"V1 Decrypt Time: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine("V1 Compatibility Verified.");
            _output.WriteLine("--------------------------------------------\n");
        }

        #region Helpers

        private string GenerateLargeString(int sizeInMb)
        {
            int targetLength = sizeInMb * 1024 * 1024;
            var sb = new StringBuilder(targetLength);
            string source = "TheQuickBrownFoxJumpsOverTheLazyDog1234567890!@#$%^&*()";

            while (sb.Length < targetLength)
            {
                sb.Append(source);
            }

            return sb.ToString().Substring(0, targetLength);
        }

        /// <summary>
        /// Simulates the old V1 encryption logic to test the SUT's DecryptV1 method.
        /// </summary>
        private string CreateLegacyV1EncryptedString(string plainText)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = _testKey;
                aes.IV = _testIvV1;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    // Legacy V1 format: "SERVY_ENC:v1:{Base64}"
                    return "SERVY_ENC:v1:" + Convert.ToBase64String(cipherBytes);
                }
            }
        }

        #endregion
    }
}