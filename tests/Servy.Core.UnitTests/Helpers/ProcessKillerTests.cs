using Moq;
using Servy.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    /// <summary>
    /// Unit and Integration tests for the ProcessKiller utility.
    /// Note: Integration tests require an environment where 'timeout.exe' and 'cmd.exe' are available.
    /// </summary>
    public class ProcessKillerTests : IDisposable
    {
        private const string SacrificialProcessName = "timeout";
        private readonly IProcessKiller _processKiller;

        public ProcessKillerTests()
        {
            _processKiller = new ProcessKiller();
        }

        public void Dispose()
        {
            try
            {
                // Broad cleanup for 'timeout.exe' processes
                foreach (var p in Process.GetProcessesByName(SacrificialProcessName))
                {
                    using (p) // Ensures disposal of the process handle
                    {
                        try
                        {
                            if (!p.HasExited) p.Kill();
                        }
                        catch
                        {
                            /* Ignore: Process might have exited or access denied */
                        }
                    }
                }

                // Targeted cleanup for 'cmd.exe' processes
                foreach (var p in Process.GetProcessesByName("cmd"))
                {
                    using (p) // Ensures disposal of the process handle
                    {
                        // Only kill cmd processes that are windowless and in the current session
                        // to avoid terminating the developer's active command prompts.
                        if (string.IsNullOrEmpty(p.MainWindowTitle) && p.SessionId == Process.GetCurrentProcess().SessionId)
                        {
                            try
                            {
                                if (!p.HasExited) p.Kill();
                            }
                            catch
                            {
                                /* Ignore: Usually safe in CI environments */
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // In a test environment, failing to clean up shouldn't crash the test runner,
                // but we log it for observability.
                Debug.WriteLine($"Process cleanup failed: {ex.Message}");
            }
        }

        #region Unit Tests (Validation & Logic)

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void KillProcessTreeAndParents_InvalidInput_ReturnsFalse(string name)
        {
            // Act
            var result = _processKiller.KillProcessTreeAndParents(name);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void KillProcessTreeAndParents_ProcessNotFound_ReturnsTrue()
        {
            // Act
            var result = _processKiller.KillProcessTreeAndParents("NonExistentProcess_Unique_999");

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("app.exe")]
        [InlineData("APP.EXE")]
        [InlineData("App.Exe")]
        public void KillProcessTreeAndParents_ShouldHandleExeExtensionCaseInsensitively(string input)
        {
            // Act
            // This verifies the string manipulation logic doesn't throw when suffix is present
            var result = _processKiller.KillProcessTreeAndParents(input);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void KillProcessesUsingFile_MissingFile_ReturnsTrue()
        {
            // Arrange
            string fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");

            var mockProcessHelper = new Mock<IProcessHelper>();

            // Act
            var result = _processKiller.KillProcessesUsingFile(mockProcessHelper.Object, fakePath);

            // Assert
            // The method returns true and logs an error if the target file is missing
            Assert.True(result);
        }

        #endregion

    }
}