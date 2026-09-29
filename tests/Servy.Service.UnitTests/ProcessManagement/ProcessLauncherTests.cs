using Moq;
using Servy.Core.Logging;
using Servy.Service.ProcessManagement;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace Servy.Service.UnitTests.ProcessManagement
{
    /// <summary>
    /// Unit tests for <see cref="ProcessLauncher"/>'s language-fix detection fallbacks.
    /// </summary>
    /// <remarks>
    /// The two detection patterns are anchored, compiled and private, so no input can make them time out.
    /// These tests drive the internal seam overload with matchers that throw
    /// <see cref="RegexMatchTimeoutException"/> directly, which is the only way to reach either catch arm.
    /// </remarks>
    public class ProcessLauncherTests
    {
        private static RegexMatchTimeoutException Timeout(string input) =>
            new RegexMatchTimeoutException(input, "(pattern)", System.TimeSpan.FromMilliseconds(1));

        [Fact]
        public void ApplyLanguageFixes_PythonRegexTimesOut_WarnsAndSetsNoPythonVariables()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var psi = new ProcessStartInfo { FileName = @"C:\py\python.exe", UseShellExecute = false };
            foreach (var key in new[] { "PYTHONUTF8", "PYTHONIOENCODING", "PYTHONLEGACYWINDOWSSTDIO", "PYTHONUNBUFFERED" })
            {
                psi.Environment.Remove(key);
            }

            // Act
            ProcessLauncher.ApplyLanguageFixes(psi, logger.Object, _ => throw Timeout("python"), _ => false);

            // Assert
            Assert.False(psi.Environment.ContainsKey("PYTHONUTF8"));
            Assert.False(psi.Environment.ContainsKey("PYTHONIOENCODING"));
            Assert.False(psi.Environment.ContainsKey("PYTHONLEGACYWINDOWSSTDIO"));
            Assert.False(psi.Environment.ContainsKey("PYTHONUNBUFFERED"));
            logger.Verify(
                l => l.Warn(It.Is<string>(m => m.Contains("Python detection regex timed out")), It.IsAny<System.Exception?>()),
                Times.Once);
        }

        [Theory]
        [InlineData("java.exe", "-Dfile.encoding=UTF-8 -jar app.jar")]
        [InlineData("javaw.exe", "-Dfile.encoding=UTF-8 -jar app.jar")]
        [InlineData("javac.exe", "-J-Dfile.encoding=UTF-8 -jar app.jar")]
        public void ApplyLanguageFixes_JavaEncodingRegexTimesOut_WarnsAndPrependsTheFlag(string exe, string expectedArgs)
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var psi = new ProcessStartInfo { FileName = exe, Arguments = "-jar app.jar", UseShellExecute = false };

            // Act
            ProcessLauncher.ApplyLanguageFixes(psi, logger.Object, _ => false, _ => throw Timeout("-jar app.jar"));

            // Assert
            Assert.Equal(expectedArgs, psi.Arguments);
            logger.Verify(
                l => l.Warn(It.Is<string>(m => m.Contains("-Dfile.encoding detection regex timed out")), It.IsAny<System.Exception?>()),
                Times.Once);
        }
    }
}
