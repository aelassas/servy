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
            var clone = original.Clone() as ServiceConsoleStateDto;

            // Assert
            Assert.NotNull(clone);
            Assert.NotSame(original, clone); // Verify it's a different instance
            Assert.Equal(original.Pid, clone!.Pid);
            Assert.Equal(original.ActiveStdoutPath, clone.ActiveStdoutPath);
            Assert.Equal(original.ActiveStderrPath, clone.ActiveStderrPath);
        }

        [Fact]
        public void Clone_ShouldHandleNullValues()
        {
            // Arrange
            var original = new ServiceConsoleStateDto
            {
                Pid = null,
                ActiveStdoutPath = null,
                ActiveStderrPath = null
            };

            // Act
            var clone = original.Clone() as ServiceConsoleStateDto;

            // Assert
            Assert.NotNull(clone);
            Assert.Null(clone!.Pid);
            Assert.Null(clone.ActiveStdoutPath);
            Assert.Null(clone.ActiveStderrPath);
        }
    }
}