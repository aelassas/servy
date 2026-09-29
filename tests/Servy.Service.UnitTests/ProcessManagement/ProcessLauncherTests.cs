using Microsoft.Win32.SafeHandles;
using Moq;
using Servy.Core.Logging;
using Servy.Core.Validation;
using Servy.Service.ProcessManagement;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Servy.Service.UnitTests.ProcessManagement
{
    /// <summary>
    /// Unit tests for <see cref="ProcessLauncher"/>'s language-fix detection fallbacks and for the
    /// security refusals of <see cref="ProcessLauncher.TryOpenAppendWriter(string, Encoding, string, string, IServyLogger)"/>.
    /// </summary>
    /// <remarks>
    /// The two detection patterns are anchored, compiled and private, so no input can make them time out.
    /// These tests drive the internal seam overload with matchers that throw
    /// <see cref="RegexMatchTimeoutException"/> directly, which is the only way to reach either catch arm.
    /// The writer refusals are driven the same way: the ancestor reparse-point check and the handle-path
    /// resolver are supplied by the test, because a real junction swap between two lines and a mismatching
    /// final path cannot be arranged from a test.
    /// </remarks>
    public class ProcessLauncherTests : IDisposable
    {
        private readonly string _tempDir =
            Path.Combine(Path.GetTempPath(), "servy-pl-" + Guid.NewGuid().ToString("N"));

        private readonly Mock<IServyLogger> _logger = new Mock<IServyLogger>();

        /// <summary>
        /// Removes the per-test temporary directory.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch (IOException)
            {
                // A leftover temp directory must never fail a test.
            }
            catch (UnauthorizedAccessException)
            {
                // Same: teardown is best effort.
            }
        }

        /// <summary>
        /// A handle resolver that always fails to resolve, which is the fail-closed branch.
        /// </summary>
        /// <param name="handle">The open file handle, ignored.</param>
        /// <param name="finalPath">Always set to an empty string.</param>
        /// <returns>Always <c>false</c>.</returns>
        private static bool Unresolvable(SafeFileHandle handle, out string finalPath)
        {
            finalPath = string.Empty;
            return false;
        }

        /// <summary>
        /// The path <see cref="ResolvesElsewhere"/> reports, standing in for a symlink swap won by an attacker.
        /// </summary>
        private string _swapTarget = string.Empty;

        /// <summary>
        /// A handle resolver that reports <see cref="_swapTarget"/> rather than the requested path.
        /// </summary>
        /// <param name="handle">The open file handle, ignored.</param>
        /// <param name="finalPath">Always set to <see cref="_swapTarget"/>.</param>
        /// <returns>Always <c>true</c>.</returns>
        private bool ResolvesElsewhere(SafeFileHandle handle, out string finalPath)
        {
            finalPath = _swapTarget;
            return true;
        }

        private static RegexMatchTimeoutException Timeout(string input)
        {
            return new RegexMatchTimeoutException(input, "(pattern)", TimeSpan.FromMilliseconds(1));
        }

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
            Func<string, bool> pythonTimesOut = _ => { throw Timeout("python"); };
            Func<string, bool> noJavaEncoding = _ => false;

            // Act
            ProcessLauncher.ApplyLanguageFixes(psi, logger.Object, pythonTimesOut, noJavaEncoding);

            // Assert
            Assert.False(psi.Environment.ContainsKey("PYTHONUTF8"));
            Assert.False(psi.Environment.ContainsKey("PYTHONIOENCODING"));
            Assert.False(psi.Environment.ContainsKey("PYTHONLEGACYWINDOWSSTDIO"));
            Assert.False(psi.Environment.ContainsKey("PYTHONUNBUFFERED"));
            logger.Verify(
                l => l.Warn(It.Is<string>(m => m.Contains("Python detection regex timed out")), It.IsAny<Exception>()),
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
            Func<string, bool> notPython = _ => false;
            Func<string, bool> javaEncodingTimesOut = _ => { throw Timeout("-jar app.jar"); };

            // Act
            ProcessLauncher.ApplyLanguageFixes(psi, logger.Object, notPython, javaEncodingTimesOut);

            // Assert
            Assert.Equal(expectedArgs, psi.Arguments);
            logger.Verify(
                l => l.Warn(It.Is<string>(m => m.Contains("-Dfile.encoding detection regex timed out")), It.IsAny<Exception>()),
                Times.Once);
        }

        [Fact]
        public void TryOpenAppendWriter_AncestorIsReparsePoint_RefusesBeforeCreatingTheDirectory()
        {
            // Arrange
            var subDir = Path.Combine(_tempDir, "sub");
            var path = Path.Combine(subDir, "out.log");
            var calls = 0;

            // Act
            var writer = ProcessLauncher.TryOpenAppendWriter(
                path, Encoding.UTF8, "app.exe", "stdout", _logger.Object,
                _ => { calls++; return true; }, PathSecurityGuard.TryGetFinalPathByHandle);

            // Assert
            Assert.Null(writer);
            Assert.Equal(1, calls);
            Assert.False(Directory.Exists(subDir));
            _logger.Verify(
                l => l.Error(
                    It.Is<string>(m => m.StartsWith("Refusing to write stdout") && m.Contains("junction")),
                    It.IsAny<Exception>()),
                Times.Once);
        }

        [Fact]
        public void TryOpenAppendWriter_AncestorBecomesReparsePointAfterCreate_RefusesAndLogs()
        {
            // Arrange
            var path = Path.Combine(_tempDir, "sub", "out.log");
            var calls = 0;

            // Act
            var writer = ProcessLauncher.TryOpenAppendWriter(
                path, Encoding.UTF8, "app.exe", "stdout", _logger.Object,
                _ => ++calls == 2, PathSecurityGuard.TryGetFinalPathByHandle);

            // Assert
            Assert.Null(writer);
            Assert.Equal(2, calls);
            Assert.False(File.Exists(path));
            _logger.Verify(
                l => l.Error(
                    It.Is<string>(m => m.StartsWith("Refusing to write stdout") && m.Contains("junction")),
                    It.IsAny<Exception>()),
                Times.Once);
        }

        [Fact]
        public void TryOpenAppendWriter_HandleCannotBeResolved_FailsClosedAndReleasesTheFile()
        {
            // Arrange
            var path = Path.Combine(_tempDir, "out.log");

            // Act
            var writer = ProcessLauncher.TryOpenAppendWriter(
                path, Encoding.UTF8, "app.exe", "stderr", _logger.Object, _ => false, Unresolvable);

            // Assert
            Assert.Null(writer);
            Assert.True(File.Exists(path));
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // An exclusive open throws IOException while the refused writer's handle is still open,
                // so it witnesses the fs.Dispose() in this arm. File.Delete does not: the writer is
                // opened with FileShare.Delete, so a delete succeeds with the handle still open.
            }
            _logger.Verify(
                l => l.Error(
                    It.Is<string>(m => m.Contains("could not resolve the opened handle")),
                    It.IsAny<Exception>()),
                Times.Once);
        }

        [Fact]
        public void TryOpenAppendWriter_HandleResolvesElsewhere_RefusesAndNamesBothPaths()
        {
            // Arrange
            var path = Path.Combine(_tempDir, "out.log");
            _swapTarget = Path.Combine(_tempDir, "elsewhere.log");

            // Act
            var writer = ProcessLauncher.TryOpenAppendWriter(
                path, Encoding.UTF8, "app.exe", "stdout", _logger.Object, _ => false, ResolvesElsewhere);

            // Assert
            Assert.Null(writer);
            Assert.True(File.Exists(path));
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // An exclusive open throws IOException while the refused writer's handle is still open,
                // so it witnesses the fs.Dispose() in this arm. File.Delete does not: the writer is
                // opened with FileShare.Delete, so a delete succeeds with the handle still open.
            }
            _logger.Verify(
                l => l.Error(
                    It.Is<string>(m => m.Contains(_swapTarget) && m.Contains(Path.GetFullPath(path))),
                    It.IsAny<Exception>()),
                Times.Once);
        }
    }
}
