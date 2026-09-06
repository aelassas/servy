using Servy.Core.DTOs;

namespace Servy.Core.UnitTests.DTOs
{
    public class ServiceConsoleStateDtoTests
    {
        [Fact]
        public void Properties_ShouldStoreAndRetrieveValues()
        {
            // Arrange
            var dto = new ServiceConsoleStateDto();
            var expectedPid = 1234;
            var expectedStdout = @"C:\Logs\stdout.log";
            var expectedStderr = @"C:\Logs\stderr.log";

            // Act
            dto.Pid = expectedPid;
            dto.ActiveStdoutPath = expectedStdout;
            dto.ActiveStderrPath = expectedStderr;

            // Assert
            Assert.Equal(expectedPid, dto.Pid);
            Assert.Equal(expectedStdout, dto.ActiveStdoutPath);
            Assert.Equal(expectedStderr, dto.ActiveStderrPath);
        }

        [Fact]
        public void Clone_ShouldReturnNewInstanceWithSameValues()
        {
            // Arrange
            var original = new ServiceConsoleStateDto
            {
                Pid = 999,
                ActiveStdoutPath = "out.log",
                ActiveStderrPath = "err.log"
            };

            // Act
            var clone = (ServiceConsoleStateDto)original.Clone();

            // Assert
            Assert.NotSame(original, clone); // Verify it's a different instance
            Assert.Equal(original.Pid, clone.Pid);
            Assert.Equal(original.ActiveStdoutPath, clone.ActiveStdoutPath);
            Assert.Equal(original.ActiveStderrPath, clone.ActiveStderrPath);
        }

        [Fact]
        public void Clone_ShouldHandleNullValues()
        {
            // Arrange - only the paths are null; Pid is set, so a clone that returns
            // a fresh instance instead of a copy is distinguishable from a real one.
            var original = new ServiceConsoleStateDto
            {
                Pid = 4321,
                ActiveStdoutPath = null,
                ActiveStderrPath = null
            };

            // Act
            var clone = (ServiceConsoleStateDto)original.Clone();

            // Assert
            Assert.NotSame(original, clone);
            Assert.Equal(4321, clone.Pid);
            Assert.Null(clone.ActiveStdoutPath);
            Assert.Null(clone.ActiveStderrPath);
        }
    }
}
