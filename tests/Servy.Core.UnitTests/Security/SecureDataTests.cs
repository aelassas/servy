using Moq;
using Servy.Core.Config;
using Servy.Core.Security;
using Servy.Testing;
using System;
using Xunit;

namespace Servy.Core.UnitTests.Security
{
    public class SecureDataTests
    {
        private readonly byte[] _key = new byte[32]; // AES-256
        private readonly byte[] _iv = new byte[16];  // AES block size
        private readonly Mock<IProtectedKeyProvider> _mockProvider;

        public SecureDataTests()
        {
            for (var i = 0; i < _key.Length; i++) _key[i] = (byte)i;
            for (var i = 0; i < _iv.Length; i++) _iv[i] = (byte)(i + 1);

            _mockProvider = new Mock<IProtectedKeyProvider>();

            // Use .Clone() so the class clearing the array doesn't wipe the test's key
            _mockProvider.Setup(x => x.GetKey()).Returns(() => (byte[])_key.Clone());
            _mockProvider.Setup(x => x.GetIV()).Returns(() => (byte[])_iv.Clone());
        }

        #region Initialization & Constraints

        [Fact]
        public void Constructor_NullProvider_Throws()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SecureData(null));
        }

        [Fact]
        public void Encrypt_Null_Throws()
        {
            // Arrange
            string input = null;
            using (var sp = new SecureData(_mockProvider.Object))
            {
                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sp.Encrypt(input));
            }
        }

        [Fact]
        public void Encrypt_Empty_Throws()
        {
            // Arrange
            string input = string.Empty;
            using (var sp = new SecureData(_mockProvider.Object))
            {
                // Act & Assert
                Assert.Throws<ArgumentException>(() => sp.Encrypt(input));
            }
        }

        #endregion

        #region Core Encryption/Decryption Logic

        [Fact]
        public void EncryptV2_HandlesComplexCharacters_Successfully()
        {
            // Arrange
            using (var sp = new SecureData(_mockProvider.Object))
            {
                // Testing multi-byte character boundary safety (Emoji uses 4 bytes)
                var original = "Security is key! 🛡️🔐";

                // Act
                var encrypted = sp.Encrypt(original);
                var decrypted = sp.Decrypt(encrypted);

                // Assert
                Assert.Equal(original, decrypted);
                Assert.Contains("SERVY_ENC:v2:", encrypted);
            }
        }

        [Theory]
        [InlineData("SERVY_ENC:v1:")]
        [InlineData("SERVY_ENC:")]
        [InlineData("")]
        public void DecryptedV1_WithVariousPrefixes_Works(string prefix)
        {
            // Arrange
            if (!AppConfig.AllowLegacyV1Decryption)
            {
                // Skip this test if legacy decryption is disabled
                return;
            }

            using (var sp = new SecureData(_mockProvider.Object))
            {
                var secret = "LegacySecret";
                var rawV1Base64 = SecureDataHelper.CreateLegacyV1Base64(_key, _iv, secret);
                var fullCipherWithPrefix = prefix + rawV1Base64;

                // Act
                var decrypted = sp.Decrypt(fullCipherWithPrefix);

                // Assert
                Assert.Equal(secret, decrypted);
            }
        }

        #endregion

        #region Branch Coverage: Fallbacks & Tampering

        [Fact]
        public void Decrypt_Null_ThrowsArgumentNullException()
        {
            // Arrange
            string input = null;
            using (var sp = new SecureData(_mockProvider.Object))
            {
                // Act & Assert
                var ex = Assert.Throws<ArgumentNullException>(() => sp.Decrypt(input));

                // Assert
                // Verify the parameter name matches for extra precision
                Assert.Equal("cipherText", ex.ParamName);
            }
        }

        [Fact]
        public void Decrypt_Empty_ThrowsArgumentException()
        {
            // Arrange
            string input = string.Empty;
            using (var sp = new SecureData(_mockProvider.Object))
            {
                // Act & Assert
                var ex = Assert.Throws<ArgumentException>(() => sp.Decrypt(input));

                // Assert
                // Verify the parameter name matches for extra precision
                Assert.Equal("cipherText", ex.ParamName);
            }
        }

        [Theory]
        // Branch 1: !IsStrictBase64(payload) -> returns the raw plaintext payload string directly.
        [InlineData("Plain_Legacy_Password_123!", false)]
        // Branch 2: IsStrictBase64(payload) -> blocked while AllowLegacyV1Decryption is false; throws SecureDataLegacyBlockedException.
        [InlineData(null, true)]
        public void Decrypt_LegacyFormat_HandlesBothBranches(string input, bool isBase64LegacyBranch)
        {
            // Arrange
            var rawKey = _mockProvider.Object.GetKey();
            var rawIv = _mockProvider.Object.GetIV();

            if (isBase64LegacyBranch)
            {
                const string plaintextToEncrypt = "DecryptedValueFromV1";
                input = SecureDataHelper.CreateLegacyV1Base64(rawKey, rawIv, plaintextToEncrypt);
            }

            using (var sp = new SecureData(_mockProvider.Object))
            {
                if (isBase64LegacyBranch && !AppConfig.AllowLegacyV1Decryption)
                {
                    // Act & Assert
                    // Raw legacy ciphertext detected with legacy decryption disabled must throw SecureDataLegacyBlockedException
                    var ex = Assert.Throws<SecureDataLegacyBlockedException>(() => sp.Decrypt(input));
                    Assert.Contains("Raw legacy ciphertext detected", ex.Message);
                }
                else
                {
                    // Act
                    var result = sp.Decrypt(input);

                    // Assert
                    Assert.Equal(input, result);
                }
            }
        }

        [Fact]
        public void Decrypt_UnmarkedStrictBase64_WhenLegacyDisabled_ThrowsSecureDataLegacyBlockedException()
        {
            // Arrange
            if (AppConfig.AllowLegacyV1Decryption)
            {
                // Test targets disabled legacy v1 decryption policy
                return;
            }

            const string plaintextToEncrypt = "UnmarkedLegacyPayload";
            string rawBase64Ciphertext = SecureDataHelper.CreateLegacyV1Base64(_key, _iv, plaintextToEncrypt);

            using (var sp = new SecureData(_mockProvider.Object))
            {
                // Act & Assert
                var ex = Assert.Throws<SecureDataLegacyBlockedException>(() => sp.Decrypt(rawBase64Ciphertext));
                Assert.Contains("Raw legacy ciphertext detected", ex.Message);
            }
        }

        [Fact]
        public void Decrypt_UnmarkedNonBase64_ReturnsRawInput()
        {
            // Arrange
            using (var sp = new SecureData(_mockProvider.Object))
            {
                string input = "NotBase64!";

                // Act
                var result = sp.Decrypt(input);

                // Assert
                // Unmarked strings still return as-is for backward compatibility
                Assert.Equal(input, result);
            }
        }

        [Fact]
        public void Decrypt_MarkedButInvalidFormat_ThrowsIntegrityException()
        {
            // Arrange
            using (var sp = new SecureData(_mockProvider.Object))
            {
                string input = "SERVY_ENC:NotBase64!";

                // Act & Assert
                // Since the string has a marker, it MUST succeed or fail loud.
                // Returning a substring is no longer permitted as it masks integrity issues.
                Assert.Throws<SecureDataIntegrityException>(() => sp.Decrypt(input));
            }
        }

        [Fact]
        public void Decrypt_TamperedV2_ThrowsIntegrityException()
        {
            // Arrange
            using (var sp = new SecureData(_mockProvider.Object))
            {
                var original = "MySecret123";

                // Encrypting creates a valid v2 payload: [IV + Ciphertext + HMAC]
                var encrypted = sp.Encrypt(original);

                // 1. Extract the Base64 payload after the "SERVY_ENC:v2:" marker
                var lastColonIndex = encrypted.LastIndexOf(':');
                var rawBase64 = encrypted.Substring(lastColonIndex + 1);
                byte[] data = Convert.FromBase64String(rawBase64);

                // 2. Tamper with the ciphertext
                // Flipping a bit here ensures the HMAC-SHA256 signature will no longer match.
                data[20] ^= 0x01;

                var tampered = "SERVY_ENC:v2:" + Convert.ToBase64String(data);

                // 3. Act & Assert
                // The Decrypt method now recognizes the v2 marker and enforces integrity.
                // It must throw an exception rather than returning tampered "junk".
                var ex = Assert.Throws<SecureDataIntegrityException>(() => sp.Decrypt(tampered));

                // Assert
                // Verify the error message relates to the integrity check
                Assert.Contains("HMAC integrity check failed", ex.Message);
            }
        }

        [Fact]
        public void Decrypt_MarkedInvalidBase64_ThrowsException()
        {
            // Arrange
            using (var sp = new SecureData(_mockProvider.Object))
            {
                // This string has a marker, so the Decrypt method will attempt to decode it.
                // It is no longer allowed to "swallow" the failure and return the junk string.
                var tampered = "SERVY_ENC:v2:!!!NotBase64!!!";

                // Act & Assert
                // Invalid Base64 strings must always be caught and wrapped by the cryptographic
                // provider to guarantee a deterministic SecureDataIntegrityException surface.
                Assert.Throws<SecureDataIntegrityException>(() => sp.Decrypt(tampered));
            }
        }

        [Fact]
        public void DecryptV2_Internal_InvalidBase64_Throws()
        {
            // Arrange
            using (var sp = new SecureData(_mockProvider.Object))
            {
                var invalidBase64 = "!!!NotBase64!!!";

                // Act & Assert
                // TestReflection natively unwraps TargetInvocationException to restore the original exception frame context
                Assert.Throws<SecureDataIntegrityException>(() =>
                    TestReflection.InvokeNonPublic(sp, "DecryptV2", new object[] { invalidBase64 }));
            }
        }

        [Fact]
        public void DecryptV2_PayloadTooShort_Throws()
        {
            // Arrange
            using (var sp = new SecureData(_mockProvider.Object))
            {
                // A v2 payload must be at least 48 bytes (16-byte IV + 32-byte HMAC); ciphertext is additional.
                // We provide only 10 bytes here.
                var shortPayloadBase64 = Convert.ToBase64String(new byte[10]);

                // Act & Assert
                // TestReflection unwraps TargetInvocationException cleanly to target the inner exception directly
                var ex = Assert.Throws<SecureDataIntegrityException>(() =>
                    TestReflection.InvokeNonPublic(sp, "DecryptV2", new object[] { shortPayloadBase64 }));

                // Assert
                Assert.Contains("V2 payload length is insufficient.", ex.Message);
            }
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Encrypt_ShouldThrowObjectDisposedException_WhenDisposed()
        {
            // Arrange
            var sp = new SecureData(_mockProvider.Object);
            sp.Dispose();

            // Act & Assert
            var exception = Assert.Throws<ObjectDisposedException>(() =>
                sp.Encrypt("Sensitive Data"));

            // Assert
            // Verify the exception mentions the correct class name
            Assert.Contains(nameof(SecureData), exception.ObjectName);
        }

        [Fact]
        public void Decrypt_ShouldThrowObjectDisposedException_WhenDisposed()
        {
            // Arrange
            var sp = new SecureData(_mockProvider.Object);
            sp.Dispose();

            // Act & Assert
            var exception = Assert.Throws<ObjectDisposedException>(() =>
                sp.Decrypt("SERVY_ENC:v2:dummy_payload"));

            // Assert
            Assert.Contains(nameof(SecureData), exception.ObjectName);
        }

        [Fact]
        public void Methods_ShouldWorkBeforeDisposal_ButThrowAfter()
        {
            // Arrange
            var sp = new SecureData(_mockProvider.Object);
            const string plainText = "SecureMessage";

            // Act 1: Should succeed before disposal
            var cipher = sp.Encrypt(plainText);

            // Assert 1
            Assert.NotNull(cipher);

            // Act 2: Dispose
            sp.Dispose();

            // Assert 2: Subsequent calls must fail
            Assert.Throws<ObjectDisposedException>(() => sp.Encrypt(plainText));
            Assert.Throws<ObjectDisposedException>(() => sp.Decrypt(cipher));
        }

        [Fact]
        public void Dispose_ZeroesAllKeyMaterialAndHandlesIdempotency()
        {
            // Arrange: Setup Mock Provider with dummy key data
            var mockProvider = new Mock<IProtectedKeyProvider>();
            byte[] dummyKey = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            byte[] dummyIv = { 10, 20, 30, 40, 50, 60, 70, 80 };

            mockProvider.Setup(p => p.GetKey()).Returns((byte[])dummyKey.Clone());
            mockProvider.Setup(p => p.GetIV()).Returns((byte[])dummyIv.Clone());

            var secureData = new SecureData(mockProvider.Object);

            // To verify zeroing, we use Reflection to grab the internal byte arrays.
            // This is necessary because the fields are private and we need to check
            // the content of the memory after Dispose.
            var v1Key = TestReflection.GetField<byte[]>(secureData, "_v1MasterKey");
            var v1Iv = TestReflection.GetField<byte[]>(secureData, "_v1StaticIv");
            var v2Enc = TestReflection.GetField<byte[]>(secureData, "_v2EncryptionKey");
            var v2Hmac = TestReflection.GetField<byte[]>(secureData, "_v2HmacKey");

            // Pre-condition check: Ensure keys are not zero before Dispose
            if (AppConfig.AllowLegacyV1Decryption)
            {
                Assert.Contains(v1Key, b => b != 0);
            }
            Assert.Contains(v2Enc, b => b != 0);

            // Act 1: First Dispose (Covers all null-checks and zeroing logic)
            secureData.Dispose();

            // Assert 1: Verify all memory is zeroed
            if (AppConfig.AllowLegacyV1Decryption)
            {
                Assert.All(v1Key, b => Assert.Equal(0, b));
                Assert.All(v1Iv, b => Assert.Equal(0, b));
            }
            Assert.All(v2Enc, b => Assert.Equal(0, b));
            Assert.All(v2Hmac, b => Assert.Equal(0, b));

            // Act 2: Second Dispose (Covers the 'if (_disposed) return' branch)
            // This should not throw or cause any side effects
            var record = Record.Exception(() => secureData.Dispose());

            // Assert 2
            Assert.Null(record);

            // Verify state remains disposed
            int disposedField = TestReflection.GetField<int>(secureData, "_disposed");
            Assert.Equal(1, disposedField);
        }

        #endregion

        #region Helpers

        [Theory]
        // Branch 1: Null, Empty, or Whitespace
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("    ", false)]

        // Branch 2: Length not a multiple of 4
        [InlineData("abc", false)]
        [InlineData("abcde", false)]

        // Branch 3: Invalid Characters (hitting the loop and the if logic)
        [InlineData("abc!", false)]      // Special char
        [InlineData("abc?", false)]      // Special char
        [InlineData("abc ", false)]      // Space inside
        [InlineData("ab\n1", false)]     // Newline

        // Branch 4: Misplaced Padding ('=' not at the end)
        [InlineData("=abc", false)]      // Start
        [InlineData("a=bc", false)]      // Middle
        [InlineData("ab=c", false)]      // '=' is second-to-last but the final char isn't '=' (rejected by the
                                         // "trailing padding must be contiguous at the end" rule, not by paddingIndex < length-2)

        // Branch 5: Valid Base64 (The "Success" return)
        [InlineData("SGVsbG8=", true)]   // 1 padding
        [InlineData("SGVsbG82", true)]   // 0 padding
        [InlineData("SGVsbA==", true)]   // 2 padding
        [InlineData("YQ==", true)]       // Short valid string
        public void IsStrictBase64_ShouldCoverAllBranches(string input, bool expected)
        {
            // Arrange & Act
            // IsStrictBase64 is a private static method on SecureData
            var result = (bool)TestReflection.InvokeNonPublicStatic(typeof(SecureData), "IsStrictBase64", input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion
    }
}
