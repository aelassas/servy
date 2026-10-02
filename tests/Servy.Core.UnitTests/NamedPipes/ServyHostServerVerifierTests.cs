using Moq;
using Servy.Core.Config;
using Servy.Core.NamedPipes;
using Servy.Core.Services;
using System.IO.Pipes;

namespace Servy.Core.UnitTests.NamedPipes
{
    /// <summary>
    /// Unit tests for <see cref="ServyHostServerVerifier"/>, the client-side guard against pipe squatting.
    /// </summary>
    public class ServyHostServerVerifierTests : IDisposable
    {
        private readonly Mock<IWindowsServiceApi> _api = new Mock<IWindowsServiceApi>();
        private readonly AnonymousPipeServerStream _pipe = new AnonymousPipeServerStream();

        public void Dispose() => _pipe.Dispose();

        private ServyHostServerVerifier Create(int serverProcessId)
            => new ServyHostServerVerifier(_api.Object, AppConfig.ServyHostServiceName, _ => serverProcessId);

        [Fact]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ServyHostServerVerifier(null!));
            Assert.Throws<ArgumentNullException>(() => new ServyHostServerVerifier(_api.Object, null!, _ => 1));
            Assert.Throws<ArgumentNullException>(() => new ServyHostServerVerifier(_api.Object, "Servy", null!));
        }

        [Fact]
        public void Verify_NullPipe_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Create(1).Verify(null!));
        }

        [Fact]
        public void Verify_ServerIsTheHostServiceProcess_Accepts()
        {
            // Arrange
            _api.Setup(a => a.GetServiceProcessId("Servy")).Returns(4242);

            // Act
            var ex = Record.Exception(() => Create(4242).Verify(_pipe));

            // Assert
            Assert.Null(ex);
            _api.Verify(a => a.GetServiceProcessId("Servy"), Times.Once);
        }

        [Fact]
        public void Verify_ServerIsAnotherProcess_Refuses()
        {
            // Arrange: something else created the pipe name (pipe squatting)
            _api.Setup(a => a.GetServiceProcessId("Servy")).Returns(4242);

            // Act
            var ex = Assert.Throws<UnauthorizedAccessException>(() => Create(666).Verify(_pipe));

            // Assert
            Assert.Contains("process 666", ex.Message);
            Assert.Contains("process 4242", ex.Message);
        }

        [Fact]
        public void Verify_HostServiceNotRunning_Refuses()
        {
            // Arrange: whatever serves the pipe, it cannot be the host
            _api.Setup(a => a.GetServiceProcessId("Servy")).Returns(0);

            // Act & Assert
            var ex = Assert.Throws<UnauthorizedAccessException>(() => Create(4242).Verify(_pipe));
            Assert.Contains("is not running", ex.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Verify_ServerProcessUnknown_RefusesWithoutAskingTheScm(int serverProcessId)
        {
            // Act & Assert
            Assert.Throws<UnauthorizedAccessException>(() => Create(serverProcessId).Verify(_pipe));
            _api.Verify(a => a.GetServiceProcessId(It.IsAny<string>()), Times.Never);
        }
    }
}
