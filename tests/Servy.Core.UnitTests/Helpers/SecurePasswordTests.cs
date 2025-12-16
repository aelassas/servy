using Moq;
using Servy.Core.Helpers;
using Servy.Core.Security;
using System;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class SecurePasswordTests
    {
        private readonly byte[] _key = new byte[32]; // AES-256
        private readonly byte[] _iv = new byte[16];  // AES block size
        private readonly Mock<IProtectedKeyProvider> _mockProvider;

        public SecurePasswordTests()
        {
            // Fill key and IV with dummy values
            for (var i = 0; i < _key.Length; i++) _key[i] = (byte)i;
            for (var i = 0; i < _iv.Length; i++) _iv[i] = (byte)(i + 1);

            _mockProvider = new Mock<IProtectedKeyProvider>();
            _mockProvider.Setup(p => p.GetKey()).Returns(_key);
            _mockProvider.Setup(p => p.GetIV()).Returns(_iv);
        }

        [Fact]
        public void Constructor_NullProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SecurePassword(null));
        }

        [Fact]
        public void Encrypt_NullOrEmpty_Throws()
        {
            var sp = new SecurePassword(_mockProvider.Object);
            Assert.Throws<ArgumentNullException>(() => sp.Encrypt(null));
            Assert.Throws<ArgumentNullException>(() => sp.Encrypt(string.Empty));
        }

        [Fact]
        public void Decrypt_NullOrEmpty_Throws()
        {
            var sp = new SecurePassword(_mockProvider.Object);
            Assert.Throws<ArgumentNullException>(() => sp.Decrypt(null));
            Assert.Throws<ArgumentNullException>(() => sp.Decrypt(string.Empty));
        }

        [Fact]
        public void EncryptAndDecrypt_ReturnsOriginal()
        {
            var sp = new SecurePassword(_mockProvider.Object);
            var original = "MySecretPassword123!";

            var encrypted = sp.Encrypt(original);
            Assert.NotNull(encrypted);
            Assert.NotEqual(original, encrypted); // Ensure actually encrypted

            var decrypted = sp.Decrypt(encrypted);
            Assert.Equal(original, decrypted);
        }

        [Theory]
        [InlineData("MySecretPassword123!", "MySecretPassword123!")]              // plain text
        [InlineData("SERVY_ENC:MySecretPassword123!", "MySecretPassword123!")]    // marker but not encrypted
        [InlineData("U29tZUJhc2U2NFN0cmluZw==", "U29tZUJhc2U2NFN0cmluZw==")]      // valid Base64, fails AES -> returns original
        public void Decrypt_ReturnsOriginal(string original, string expected)
        {
            var sp = new SecurePassword(_mockProvider.Object);

            var decrypted = sp.Decrypt(original);
            Assert.Equal(expected, decrypted);
        }

        [Fact]
        public void Encrypt_ReturnsDifferentValues_ForDifferentPlainText()
        {
            var sp = new SecurePassword(_mockProvider.Object);

            var encrypted1 = sp.Encrypt("Password1");
            var encrypted2 = sp.Encrypt("Password2");

            Assert.NotEqual(encrypted1, encrypted2);
        }

        [Fact]
        public void EncryptDecrypt_EmptyString_Throws()
        {
            var sp = new SecurePassword(_mockProvider.Object);
            Assert.Throws<ArgumentNullException>(() => sp.Encrypt(""));
            Assert.Throws<ArgumentNullException>(() => sp.Decrypt(""));
        }


        // Use reflection to access private static method
        private static bool CallIsBase64(string value)
        {
            var method = typeof(SecurePassword).GetMethod(
                "IsBase64",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            );
            return (bool)method.Invoke(null, new object[] { value });
        }

        [Theory]
        [InlineData("", false)]                      // empty
        [InlineData("   ", false)]                   // whitespace
        [InlineData("abcd!@", false)]                // invalid Base64 format
        [InlineData("SGVsbG8gd29ybGQ=", true)]       // valid Base64
        [InlineData("U29tZVRleHRXaXRoU3BlY2lhbENoYXJzIQ==", true)] // valid Base64
        public void IsBase64_CoversAllBranches(string input, bool expected)
        {
            var result = CallIsBase64(input);
            Assert.Equal(expected, result);
        }

    }
}
