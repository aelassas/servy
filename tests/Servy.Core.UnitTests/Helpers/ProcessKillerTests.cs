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
            var result = ProcessKiller.KillProcessTreeAndParents(name);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void KillProcessTreeAndParents_ProcessNotFound_ReturnsTrue()
        {
            // Act
            var result = ProcessKiller.KillProcessTreeAndParents("NonExistentProcess_Unique_999");

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
            var result = ProcessKiller.KillProcessTreeAndParents(input);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void KillProcessesUsingFile_MissingFile_ReturnsTrue()
        {
            // Arrange
            string fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");

            var mockProcessHelper = new Mock<IProcessHelper>(); // Assuming a mock implementation exists

            // Act
            var result = ProcessKiller.KillProcessesUsingFile(mockProcessHelper.Object, fakePath);

            // Assert
            // The method returns true and logs an error if the target file is missing
            Assert.True(result);
        }

        #endregion

        #region Integration Tests (Multiple Processes)

        //[Fact]
        [Fact(Skip = "Requires elevated permissions not available in standard CI container")]
        [Trait("Category", "Integration")]
        public async Task KillProcessTreeAndParents_MultipleInstances_SuccessfullyTerminatesAll()
        {
            // Arrange: Spawn 3 separate instances of the sacrificial process
            int count = 3;
            var processes = new List<Process>();

            try
            {
                for (int i = 0; i < count; i++)
                {
                    var p = Process.Start(new ProcessStartInfo("timeout.exe", "120")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    processes.Add(p);
                }

                await Task.Delay(500); // Wait for OS to register processes

                // Act
                bool result = ProcessKiller.KillProcessTreeAndParents(SacrificialProcessName, killParents: false);

                // Assert
                Assert.True(result);
                foreach (var p in processes)
                {
                    Assert.True(p.WaitForExit(5000), $"Process {p.Id} failed to terminate.");
                }
            }
            finally
            {
                foreach (var p in processes)
                {
                    if (!p.HasExited) p.Kill();
                    p.Dispose();
                }
            }
        }

        //[Fact]
        [Fact(Skip = "Requires elevated permissions not available in standard CI container")]
        [Trait("Category", "Integration")]
        public async Task KillChildren_DeepAndWideTree_TerminatesAllDescendants()
        {
            // Arrange: Create a tree: Parent (cmd) -> [Child (cmd) -> Grandchild (timeout)], [Child (timeout)]
            // We use cmd.exe to bridge and spawn background children
            var startInfo = new ProcessStartInfo("cmd.exe", "/c \"start /b cmd /c timeout 120 & start /b timeout 120\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (var parent = Process.Start(startInfo))
            {
                try
                {
                    Assert.NotNull(parent);
                    // Give the shell time to spawn the nested tree
                    await Task.Delay(2000);

                    // Capture a snapshot of all timeout processes currently running
                    var timeoutsBefore = Process.GetProcessesByName(SacrificialProcessName).Select(p => p.Id).ToList();
                    Assert.NotEmpty(timeoutsBefore);

                    // Act: Kill only the children of the root cmd process
                    ProcessKiller.KillChildren(parent.Id);

                    // Assert
                    // Verify that the timeout processes spawned as descendants are gone
                    await Task.Delay(1000); // Allow termination to ripple
                    var timeoutsAfter = Process.GetProcessesByName(SacrificialProcessName).Select(p => p.Id).ToList();

                    // None of the specific PIDs we found earlier should still be running
                    foreach (int pid in timeoutsBefore)
                    {
                        Assert.DoesNotContain(pid, timeoutsAfter);
                    }
                }
                finally
                {
                    if (parent != null && !parent.HasExited)
                        parent.Kill();
                }
            }
        }

        //[Fact]
        [Fact(Skip = "Requires elevated permissions not available in standard CI container")]
        [Trait("Category", "Integration")]
        public async Task KillProcessTreeAndParents_WithParents_TerminatesUpwardChain()
        {
            // Arrange: Start a chain where the leaf is 'timeout.exe'
            // We want to see if KillProcessTreeAndParents(timeout, true) kills the cmd.exe parent
            var startInfo = new ProcessStartInfo("cmd.exe", "/c timeout 120")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (var parent = Process.Start(startInfo))
            {
                try
                {
                    await Task.Delay(1000); // Wait for timeout.exe to spawn

                    // Act
                    bool result = ProcessKiller.KillProcessTreeAndParents(SacrificialProcessName, killParents: true);

                    // Assert
                    Assert.True(result);

                    // The parent (cmd.exe) should also have been killed because killParents was true
                    bool parentExited = parent.WaitForExit(5000);
                    Assert.True(parentExited, "Parent cmd.exe should have been terminated by the upward recursive call.");
                }
                finally
                {
                    if (parent != null && !parent.HasExited)
                        parent.Kill();
                }
            }
        }

        #endregion

    }
}