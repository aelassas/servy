using Moq;
using Servy.Core.Config;
using Servy.Core.Security;
using Servy.Core.UnitTests.Helpers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Servy.Core.UnitTests.Security
{
    public class SecureDataStressTests : IDisposable
    {
        private readonly Mock<IProtectedKeyProvider> _mockKeyProvider;
        private readonly byte[] _testKey;
        private readonly byte[] _testIvV1;
        private readonly SecureData _sut;
        private readonly ITestOutputHelper _output;

        public SecureDataStressTests(ITestOutputHelper output)
        {
            _output = output;

            // Generate random keys; round-trip is verified within each run
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

        public void Dispose() => _sut.Dispose();

        [Theory]
        [InlineData(1)]  // 1 MB: 1,048,576 chars, ~2 MB of RAM
        [InlineData(10)] // 10 MB: 10,485,760 chars, ~20 MB of RAM
        [InlineData(50)] // 50 MB: 52,428,800 chars, ~100 MB of RAM
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
            if (!AppConfig.AllowLegacyV1Decryption)
            {
                // Skip this test if legacy decryption is disabled
                return;
            }

            // Arrange
            int sizeInMb = 5;
            string largePlainText = GenerateLargeString(sizeInMb);
            var sw = new Stopwatch();

            // Manually create a V1 payload since the new SUT only encrypts in V2
            string v1Payload = SecureDataHelper.CreateLegacyV1EncryptedString(_testKey, _testIvV1, largePlainText);

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

            sb.Length = targetLength;   // truncate the overshoot in place
            return sb.ToString();       // single allocation
        }

        #endregion
    }
}