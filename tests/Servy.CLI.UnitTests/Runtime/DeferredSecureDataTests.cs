using Moq;
using Servy.CLI.Runtime;
using Servy.Core.Security;
using System;
using Xunit;

namespace Servy.CLI.UnitTests.Runtime
{
    public class DeferredSecureDataTests
    {
        [Fact]
        public void ConstructAndDispose_WithoutEncryptOrDecrypt_NeverBuildsTheSecureData()
        {
            // Arrange
            // Building SecureData reads, and on a fresh machine creates, the AES key file, so composing the CLI
            // commands must not build it (#7531).
            var builds = 0;
            var inner = new Mock<ISecureData>();

            // Act
            var deferred = new DeferredSecureData(() => { builds++; return inner.Object; });
            var createdBeforeDispose = deferred.IsCreated;
            deferred.Dispose();

            // Assert
            Assert.Equal(0, builds);
            Assert.False(createdBeforeDispose);
            inner.Verify(s => s.Dispose(), Times.Never);
        }

        [Fact]
        public void EncryptAndDecrypt_SeveralCalls_BuildOnceAndForwardEveryCall()
        {
            // Arrange
            var builds = 0;
            var inner = new Mock<ISecureData>();
            inner.Setup(s => s.Encrypt("plain")).Returns("cipher");
            inner.Setup(s => s.Decrypt("cipher")).Returns("plain");
            using (var deferred = new DeferredSecureData(() => { builds++; return inner.Object; }))
            {
                // Act
                var first = deferred.Encrypt("plain");
                var second = deferred.Encrypt("plain");
                var decrypted = deferred.Decrypt("cipher");

                // Assert
                Assert.Equal(1, builds);
                Assert.True(deferred.IsCreated);
                Assert.Equal("cipher", first);
                Assert.Equal("cipher", second);
                Assert.Equal("plain", decrypted);
                inner.Verify(s => s.Encrypt("plain"), Times.Exactly(2));
                inner.Verify(s => s.Decrypt("cipher"), Times.Once);
            }
        }

        [Fact]
        public void Dispose_AfterUse_DisposesTheBuiltSecureDataOnceAndRefusesLaterCalls()
        {
            // Arrange
            var inner = new Mock<ISecureData>();
            inner.Setup(s => s.Encrypt("plain")).Returns("cipher");
            var deferred = new DeferredSecureData(() => inner.Object);
            deferred.Encrypt("plain");

            // Act
            deferred.Dispose();
            deferred.Dispose();

            // Assert
            inner.Verify(s => s.Dispose(), Times.Once);
            Assert.Throws<ObjectDisposedException>(() => deferred.Decrypt("cipher"));
        }
    }
}
