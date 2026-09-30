using Servy.Core.Config;
using Servy.Core.Native;
using Servy.Service.ProcessManagement;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Service.IntegrationTests.ProcessManagement
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class ProcessWrapperIntegrationTestsCollection
    {
        /// <summary>Collection name; reference this instead of repeating the string literal.</summary>
        public const string Name = "ProcessWrapperIntegrationTests";

        // Enforces sequential, isolated integration suite runs to protect the native Win32 console state lock mutations.
    }

    [Collection(ProcessWrapperIntegrationTestsCollection.Name)]
    public class ProcessWrapperIntegrationTests : IDisposable
    {
        // Best-effort second pass: wrappers already disposed by their test's using block are skipped (HasExited throws ObjectDisposedException, which KillAndDispose swallows)
        private readonly List<ProcessWrapper> _wrappersToCleanup = new List<ProcessWrapper>();
        private readonly TestLogger _logger = new TestLogger();

        public void Dispose()
        {
            // Iterate over all tracked wrappers and clean up their associated OS processes
            foreach (var wrapper in _wrappersToCleanup)
            {
                TestProcessCleanup.KillAndDispose(wrapper);
            }
        }

        private ProcessWrapper CreateWrapper(string fileName, string arguments, bool redirectOutput = false, bool createNoWindow = true)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = createNoWindow,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput,
                WorkingDirectory = Path.GetTempPath(),
            };

            var wrapper = new ProcessWrapper(psi, _logger);

            // Add the wrapper to our safety tracking list
            _wrappersToCleanup.Add(wrapper);

            return wrapper;
        }

        #region Disposal & Precondition Tests

        [Fact]
        public async Task ObjectDisposed_AccessingProperties_ThrowsObjectDisposedException()
        {
            // Arrange
            var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\"");
            wrapper.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => wrapper.Id);
            Assert.Throws<ObjectDisposedException>(() => wrapper.HasExited);
            Assert.Throws<ObjectDisposedException>(() => wrapper.ExitCode);
            Assert.Throws<ObjectDisposedException>(() => wrapper.EnableRaisingEvents);
            Assert.Throws<ObjectDisposedException>(() => wrapper.EnableRaisingEvents = true);
            Assert.Throws<ObjectDisposedException>(() => wrapper.StartTime);
            Assert.Throws<ObjectDisposedException>(() => wrapper.PriorityClass);
            Assert.Throws<ObjectDisposedException>(() => wrapper.PriorityClass = ProcessPriorityClass.Normal);
            Assert.Throws<ObjectDisposedException>(() => wrapper.ProcessorAffinity);
            Assert.Throws<ObjectDisposedException>(() => wrapper.ProcessorAffinity = new IntPtr(21L));
            Assert.Throws<ObjectDisposedException>(() => wrapper.StartInfo);
            Assert.Throws<ObjectDisposedException>(() => wrapper.UnderlyingProcess);

            Assert.Throws<ObjectDisposedException>(() => wrapper.Start());
            Assert.Throws<ObjectDisposedException>(() => wrapper.Stop(TestTimeouts.ProcessWrapperGracefulStopMs));
            Assert.Throws<ObjectDisposedException>(() => wrapper.StopDescendants(1, DateTime.Now, TestTimeouts.CleanupWaitMs));
            Assert.Throws<ObjectDisposedException>(() => wrapper.Format());
            Assert.Throws<ObjectDisposedException>(() => wrapper.Kill());
            Assert.Throws<ObjectDisposedException>(() => wrapper.WaitForExit(TestTimeouts.CleanupWaitMs));
            Assert.Throws<ObjectDisposedException>(() => wrapper.BeginOutputReadLine());
            Assert.Throws<ObjectDisposedException>(() => wrapper.BeginErrorReadLine());
            Assert.Throws<ObjectDisposedException>(() => wrapper.CancelOutputRead());
            Assert.Throws<ObjectDisposedException>(() => wrapper.CancelErrorRead());

            // The blocking members: without their guard a disposed wrapper would block or fault
            // inside a released Process instead of throwing.
            Assert.Throws<ObjectDisposedException>(() => wrapper.WaitForExit());

            // WaitAndCheckStillRunningAsync is an async method, so its ThrowIfDisposed lands on the
            // returned task rather than on the call: the awaiting overload is the one that observes it.
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                wrapper.WaitAndCheckStillRunningAsync(TestTimeouts.NegativeObservationWindow, CancellationToken.None));

            // The six event accessors each carry their own guard, so dropping one of them is
            // invisible to every other test in the suite.
            DataReceivedEventHandler dataHandler = (s, e) => { };
            EventHandler exitHandler = (s, e) => { };

            Assert.Throws<ObjectDisposedException>(() => wrapper.OutputDataReceived += dataHandler);
            Assert.Throws<ObjectDisposedException>(() => wrapper.OutputDataReceived -= dataHandler);
            Assert.Throws<ObjectDisposedException>(() => wrapper.ErrorDataReceived += dataHandler);
            Assert.Throws<ObjectDisposedException>(() => wrapper.ErrorDataReceived -= dataHandler);
            Assert.Throws<ObjectDisposedException>(() => wrapper.Exited += exitHandler);
            Assert.Throws<ObjectDisposedException>(() => wrapper.Exited -= exitHandler);
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ExecutesIdempotentlyAndIdlesSafe()
        {
            // Arrange
            var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\"");

            // Act 1: Initial Disposal execution window path
            wrapper.Dispose();
            bool isDisposedAfterFirstCall = TestReflection.GetField<bool>(wrapper, "_disposed");

            // Act 2: Submitting a secondary disposal invoke track
            var secondaryException = Record.Exception(() => wrapper.Dispose());

            // Assert
            Assert.True(isDisposedAfterFirstCall, "The underlying tracking field '_disposed' was not set to true during the first execution pass.");
            Assert.Null(secondaryException); // Re-entry remains stable and does not throw framework exceptions
        }

        #endregion

        #region Basic Lifecycle Tests

        [Fact]
        public void Start_And_WaitForExit_PopulatesPropertiesCorrectly()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 42\""))
            {
                // Act
                bool started = wrapper.Start();

                // Assert: Immediately verify active-handle properties while the process lifecycle is valid
                Assert.True(started);
                Assert.Equal("powershell.exe", Path.GetFileName(wrapper.StartInfo.FileName));
                Assert.True(wrapper.EnableRaisingEvents); // Constructor default
                Assert.InRange(wrapper.StartTime, DateTime.Now.AddMinutes(-1), DateTime.Now.AddMinutes(1));

                string formatString = wrapper.Format();
                Assert.Contains(wrapper.Id.ToString(), formatString);

                // Act: Await terminal completion
                bool exited = wrapper.WaitForExit(TestTimeouts.ProcessWrapperProcessTimeoutMs);

                // Assert: Verify post-execution properties
                Assert.True(exited);
                Assert.True(wrapper.HasExited);
                Assert.Equal(42, wrapper.ExitCode);
            }
        }

        [Fact]
        public void PropertySetters_UpdateUnderlyingProcess()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 2\""))
            {
                wrapper.Start();

                // Act & Assert PriorityClass
                wrapper.PriorityClass = ProcessPriorityClass.BelowNormal;
                Assert.Equal(ProcessPriorityClass.BelowNormal, wrapper.UnderlyingProcess.PriorityClass);

                // Act & Assert ProcessorAffinity (Use CPU 0 / bitmask 1 to guarantee validity across all CI core counts)
                IntPtr cpuAffinity = new IntPtr(1L);
                wrapper.ProcessorAffinity = cpuAffinity;
                Assert.Equal(cpuAffinity, wrapper.UnderlyingProcess.ProcessorAffinity);

                // Act & Assert EnableRaisingEvents
                wrapper.EnableRaisingEvents = false;
                Assert.False(wrapper.UnderlyingProcess.EnableRaisingEvents);

                // Cleanup
                TestProcessCleanup.KillNow(wrapper);
            }
        }

        [Fact]
        public void PriorityClass_Get_ReturnsPriorityAssignedToUnderlyingProcess()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 2\""))
            {
                wrapper.Start();

                // Act
                wrapper.UnderlyingProcess.PriorityClass = ProcessPriorityClass.BelowNormal;

                // Assert
                Assert.Equal(ProcessPriorityClass.BelowNormal, wrapper.PriorityClass);
            }
        }

        [Fact]
        public void ProcessorAffinity_Get_ReturnsAffinityAssignedToUnderlyingProcess()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 2\""))
            {
                wrapper.Start();

                // Act
                var expected = new IntPtr(1L);              // CPU 0 - valid on every core count
                wrapper.UnderlyingProcess.ProcessorAffinity = expected;

                // Assert
                Assert.Equal(expected, wrapper.ProcessorAffinity);
            }
        }

        #endregion

        #region Event Modification Tracking Tests

        [Fact]
        public void DataAndExitEvents_AddThenRemoveHandlers_DoesNotThrowOnStart()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\"", redirectOutput: true))
            {
                DataReceivedEventHandler outputHandler = (s, e) => { };
                DataReceivedEventHandler errorHandler = (s, e) => { };
                EventHandler exitHandler = (s, e) => { };

                // Act & Assert branch coverage for explicit add/remove event routing primitives
                wrapper.OutputDataReceived += outputHandler;
                wrapper.OutputDataReceived -= outputHandler;

                wrapper.ErrorDataReceived += errorHandler;
                wrapper.ErrorDataReceived -= errorHandler;

                wrapper.Exited += exitHandler;
                wrapper.Exited -= exitHandler;

                var exception = Record.Exception(() => wrapper.Start());
                Assert.Null(exception);
            }
        }

        #endregion

        #region Async Wait Tests

        [Fact]
        public async Task WaitAndCheckStillRunningAsync_StaysAlive_ReturnsTrue()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            {
                wrapper.Start();

                // Act
                bool isHealthy = await wrapper.WaitAndCheckStillRunningAsync(TestTimeouts.NegativeObservationWindow, CancellationToken.None);

                // Assert
                Assert.True(isHealthy);
                TestProcessCleanup.KillNow(wrapper);
            }
        }

        [Fact]
        public async Task WaitAndCheckStillRunningAsync_ExitsEarly_ReturnsFalse()
        {
            // Arrange
            using (var wrapper = CreateWrapper("cmd.exe", "/c exit 0"))
            {
                // cmd.exe is launched by bare name with UseShellExecute = false, so CreateProcess
                // searches the working directory before the system directory. Every cmd.exe launch
                // in this file pins System32 so the writable temp default cannot decide which
                // cmd.exe runs.
                wrapper.StartInfo.WorkingDirectory = Environment.SystemDirectory;
                wrapper.Start();

                // Act
                bool isHealthy = await wrapper.WaitAndCheckStillRunningAsync(TimeSpan.FromSeconds(TestTimeouts.CiGenerousSeconds), CancellationToken.None);

                // Assert
                Assert.False(isHealthy);
            }
        }

        [Fact]
        public async Task WaitAndCheckStillRunningAsync_Cancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            using (var cts = new CancellationTokenSource(TestTimeouts.ProcessWrapperCancellationDelay))
            {
                wrapper.Start();

                // Act & Assert
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    wrapper.WaitAndCheckStillRunningAsync(TestTimeouts.CiGenerous, cts.Token));

                // Cleanup
                TestProcessCleanup.KillNow(wrapper);
            }
        }

        [Fact]
        public void WaitForExit_InfiniteBlock_ExecutesSuccessfullyOnTerminatedProcess()
        {
            // Arrange
            using (var wrapper = CreateWrapper("cmd.exe", "/c exit 0"))
            {
                // Same bare-name resolution pin as the other two cmd.exe launches in this file.
                wrapper.StartInfo.WorkingDirectory = Environment.SystemDirectory;
                wrapper.Start();

                // Act
                wrapper.WaitForExit();

                // Assert
                Assert.True(wrapper.HasExited);
            }
        }

        #endregion

        #region Stop & Kill Tests

        [Fact]
        public void Stop_RunningHeadlessProcess_ReturnsNonNullAndExits()
        {
            // The name states what the assertion backs. Stop's graceful-success branch - true for a
            // windowed process whose CloseMainWindow() causes a clean exit within the timeout - is
            // still covered by no test: this arrangement is headless, so it reaches the force-kill
            // fallback and Assert.NotNull accepts that false just as readily.

            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\"", createNoWindow: true))
            {
                wrapper.Start();

                // Act: Stop triggers standard stop sequence; on headless CI, unattached console calls fall back to force kill safely
                bool? result = wrapper.Stop(TestTimeouts.ProcessWrapperGracefulStopMs);

                // Assert: Verify process was terminated safely without tearing down testhost
                Assert.NotNull(result);
                Assert.True(wrapper.HasExited);
            }
        }

        [Fact]
        public void Stop_AlreadyExited_ReturnsNull()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.Start();
                wrapper.WaitForExit(TestTimeouts.ProcessWrapperProcessTimeoutMs);

                // Act
                bool? result = wrapper.Stop(TestTimeouts.ProcessWrapperGracefulStopMs);

                // Assert
                Assert.Null(result);
            }
        }

        [Fact]
        public void Stop_ForceKillFallback_ReturnsFalse_AndLogs()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"[Console]::TreatControlCAsInput = $true; while($true) { Start-Sleep 1 }\"", createNoWindow: true))
            {
                wrapper.Start();

                // Act: Force graceful timeout expiration to trigger process.Kill fallback loop branch
                bool? result = wrapper.Stop(TestTimeouts.ProcessWrapperStopTimeoutMs);

                // Assert
                Assert.False(result);
                Assert.True(wrapper.HasExited);
                Assert.Contains(_logger.Infos, m => m.Contains("Graceful shutdown not supported or timed out"));
            }
        }

        [Fact]
        public void StopDescendants_KillsEntireTree_AndHandlesRecursion()
        {
            // Arrange
            string commandArgs = "-NoProfile -WindowStyle Hidden -Command \"$p = Start-Process cmd.exe -ArgumentList '/c timeout /t 100 /nobreak' -WindowStyle Hidden -PassThru; while ($true) { Start-Sleep 1 }\"";

            using (var wrapper = CreateWrapper("powershell.exe", commandArgs, createNoWindow: true))
            {
                wrapper.Start();

                var underlyingProcess = TestReflection.GetField<Process>(wrapper, "_process");
                int parentPid = underlyingProcess?.Id ?? wrapper.Id;
                DateTime parentStartTime = underlyingProcess?.StartTime ?? wrapper.StartTime;

                // Dynamically poll until the child process infrastructure has fully completed initialization (5 second budget)
                int childPid = 0;
                bool childSpawned = SpinWait.SpinUntil(() =>
                {
                    try
                    {
                        var verifiedChildren = ProcessExtensions.GetChildren(parentPid, parentStartTime);
                        foreach (var child in verifiedChildren)
                        {
                            using (child)
                            {
                                if (child.ProcessName.Equals("cmd", StringComparison.OrdinalIgnoreCase))
                                {
                                    childPid = child.Id;
                                    return true;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Suppress intermittent access deviations while process is initializing
                    }
                    return false;
                }, TimeSpan.FromSeconds(TestTimeouts.CiGenerousSeconds));

                // Act
                // Pass the actual parent process identity to execute tree termination via the SUT
                wrapper.StopDescendants(parentPid, parentStartTime, TestTimeouts.CleanupWaitMs);

                // Assert
                Assert.True(childSpawned && childPid > 0, "Child cmd.exe never spawned; the test cannot verify descendant termination.");

                bool childCleanedUp = SpinWait.SpinUntil(() =>
                {
                    try
                    {
                        using (var targetChild = Process.GetProcessById(childPid))
                        {
                            targetChild.Refresh();
                            return targetChild.HasExited;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process identifier has been completely cleared out by the OS kernel
                        return true;
                    }
                    catch (InvalidOperationException)
                    {
                        // Process state tracking references are dead/gone
                        return true;
                    }
                }, TestTimeouts.DescendantExitWait);

                Assert.True(childCleanedUp, $"Descendant process with PID {childPid} survived StopDescendants.");

                // Cleanup after assertions: KillAndDispose kills the tree and disposes the
                // Process, and the wrapper's own using block disposes the wrapper. A null
                // reflection read is covered by the class-level teardown, which tracks the wrapper.
                Testing.TestProcessCleanup.KillAndDispose(underlyingProcess);
            }
        }

        [Fact]
        public void StopDescendants_NoActiveDescendantsFound_LogsAndExitsEarly()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 2\""))
            {
                wrapper.Start();

                // Act - Trigger scan on a dummy lookup range that contains no cascading process children
                wrapper.StopDescendants(wrapper.Id, DateTime.Now.AddDays(1), TestTimeouts.CleanupWaitMs);

                // Assert
                Assert.Contains(_logger.Infos, m => m.Contains("No active descendants found for PID"));
                TestProcessCleanup.KillNow(wrapper);
            }
        }

        [Fact]
        public void StopDescendants_WithActiveChildren_ExecutesForeachBranchAndStopsTree()
        {
            // Arrange
            string commandArgs = "-NoProfile -Command \"$p = Start-Process cmd.exe -ArgumentList '/c timeout /t 100 /nobreak' -WindowStyle Hidden -PassThru; while ($true) { Start-Sleep 1 }\"";

            using (var wrapper = CreateWrapper("powershell.exe", commandArgs, createNoWindow: true))
            {
                wrapper.Start();

                int parentPid = wrapper.Id;
                DateTime parentStartTime = wrapper.StartTime;

                // Wait for the child process to spawn
                bool childSpawned = SpinWait.SpinUntil(() =>
                {
                    try
                    {
                        var children = ProcessExtensions.GetChildren(parentPid, parentStartTime);
                        bool spawned = children.Count > 0;
                        foreach (var c in children) c.Dispose();
                        return spawned;
                    }
                    catch
                    {
                        return false;
                    }
                }, TimeSpan.FromSeconds(TestTimeouts.CiGenerousSeconds));

                Assert.True(childSpawned, "Child process never spawned; the foreach branch cannot be verified.");

                // Act - Call StopDescendants on the active parent process
                wrapper.StopDescendants(parentPid, parentStartTime, TestTimeouts.CleanupWaitMs);

                // Assert - Verify that descendant scanning log messages were produced
                Assert.Contains(_logger.Infos, m => m.Contains($"Scanning for top-level descendants of PID {parentPid}"));
                Assert.Contains(_logger.Infos, m => m.Contains("Found descendant:"));
                Assert.Contains(_logger.Infos, m => m.Contains("Initiating cascaded kill..."));

                // Cleanup
                TestProcessCleanup.KillNow(wrapper);
            }
        }

        /// <summary>
        /// The constructor declares the logger nullable and every log call in the stop path is written
        /// <c>_logger?.</c> to honour that, but <see cref="CreateWrapper"/> always supplies one, so the
        /// logger-absent side of those null-conditional calls has no other cover. Replacing any of them with
        /// <c>_logger.</c> throws for a caller that passes the documented <see langword="null"/>.
        /// </summary>
        [Fact]
        public void StopPath_WithNullLogger_SweepsDescendantsAndStopsTheTreeWithoutThrowing()
        {
            // Arrange - same real process tree as StopDescendants_WithActiveChildren_..., but the wrapper is
            // built directly so that it gets no logger, which CreateWrapper cannot do.
            string commandArgs = "-NoProfile -Command \"$p = Start-Process cmd.exe -ArgumentList '/c timeout /t 100 /nobreak' -WindowStyle Hidden -PassThru; while ($true) { Start-Sleep 1 }\"";
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = commandArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(),
            };

            using (var wrapper = new ProcessWrapper(psi, null))
            {
                // Add the wrapper to the safety tracking list, as CreateWrapper would have
                _wrappersToCleanup.Add(wrapper);

                wrapper.Start();

                int parentPid = wrapper.Id;
                DateTime parentStartTime = wrapper.StartTime;

                // Wait for the child process to spawn, so the descendant sweep has something to log about
                bool childSpawned = SpinWait.SpinUntil(() =>
                {
                    try
                    {
                        var children = ProcessExtensions.GetChildren(parentPid, parentStartTime);
                        bool spawned = children.Count > 0;
                        foreach (var c in children) c.Dispose();
                        return spawned;
                    }
                    catch
                    {
                        return false;
                    }
                }, TimeSpan.FromSeconds(TestTimeouts.CiGenerousSeconds));

                Assert.True(childSpawned, "Child process never spawned; the logger-absent stop path cannot be verified.");

                // Act - drive the descendant sweep and then the graceful-stop-or-kill path, both of which log
                var descendantSweep = Record.Exception(() =>
                    wrapper.StopDescendants(parentPid, parentStartTime, TestTimeouts.CleanupWaitMs));
                var stop = Record.Exception(() => wrapper.Stop(TestTimeouts.ProcessWrapperGracefulStopMs));

                // Assert
                Assert.Null(descendantSweep);
                Assert.Null(stop);
                Assert.True(
                    SpinWait.SpinUntil(() => wrapper.HasExited, TimeSpan.FromSeconds(TestTimeouts.CiGenerousSeconds)),
                    "The parent process is still running, so the stop path did not run to completion.");

                // Cleanup
                TestProcessCleanup.KillNow(wrapper);
            }
        }

        #region StopTree Internal Branch Tests

        [Fact]
        public void StopTree_PIDReadException_LogsWarningAndContinues()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c exit 0",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    // Same bare-name resolution pin as the other two cmd.exe launches in this file.
                    WorkingDirectory = Environment.SystemDirectory,
                };

                // Act
                var exitedProcess = Process.Start(psi);
                exitedProcess.WaitForExit();
                exitedProcess.Close(); // Id/StartTime access inside StopTree throws InvalidOperationException

                var exception = Record.Exception(() =>
                    TestReflection.InvokeNonPublic(wrapper, "StopTree", exitedProcess, TestTimeouts.ProcessWrapperGracefulStopMs));

                // Assert
                Assert.Null(exception);
                Assert.Contains(_logger.Warnings, m => m.Contains("StopTree could not read process PID/StartTime"));
            }
        }

        [Fact]
        public void StopTree_ProcessAlreadyExited_LogsAlreadyExitedInfo()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.Start();
                wrapper.WaitForExit(TestTimeouts.ProcessWrapperProcessTimeoutMs);

                // Act - Pass an exited process to StopTree (TryStopGracefullyOrKill returns null)
                TestReflection.InvokeNonPublic(wrapper, "StopTree", wrapper.UnderlyingProcess, TestTimeouts.ProcessWrapperGracefulStopMs);

                // Assert
                Assert.Contains(_logger.Infos, m => m.Contains("has already exited."));
            }
        }

        [Fact]
        public void TryStopGracefullyOrKill_HeadlessProcess_ForceKillsAndLogsFallback()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\"", createNoWindow: true))
            {
                wrapper.Start();

                // Act - Execute TryStopGracefullyOrKill cleanly without broadcasting Ctrl+C to test host
                var result = TestReflection.InvokeNonPublic(
                    wrapper,
                    "TryStopGracefullyOrKill",
                    wrapper.UnderlyingProcess,
                    100,
                    100);

                // Assert
                Assert.False((bool?)result);   // graceful path not available headless -> force-kill fallback
                Assert.True(wrapper.HasExited);
                Assert.Contains(_logger.Infos, m => m.Contains("Graceful shutdown not supported or timed out"));
            }
        }

        [Fact]
        public void StopTree_ProcessForceKilled_LogsTerminatedInfo()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"[Console]::TreatControlCAsInput = $true; while($true) { Start-Sleep 1 }\"", createNoWindow: true))
            {
                wrapper.Start();

                // Act - Pass active process ignoring Ctrl+C to force TryStopGracefullyOrKill to return false
                TestReflection.InvokeNonPublic(wrapper, "StopTree", wrapper.UnderlyingProcess, TestTimeouts.ProcessWrapperStopTimeoutMs);

                // Assert
                Assert.Contains(_logger.Infos, m => m.Contains("terminated."));
            }
        }

        [Fact]
        public void StopTree_ProcessGracefulExit_LogsCanceledWithCodeInfo()
        {
            // Arrange
            // Dynamically compile a Win32 GUI executable (WindowsApplication).
            // Explicitly set ShowInTaskbar = true and Application.DoEvents() to ensure USER32
            // registers a top-level MainWindowHandle on headless CI hosts (ARM64/x64).
            string tempExe = Path.Combine(Path.GetTempPath(), $"ServyTestWin_{Guid.NewGuid():N}.exe");

            try
            {
                var compilePsi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"Add-Type -TypeDefinition 'using System; using System.Windows.Forms; class Program {{ [STAThread] static void Main() {{ var f = new Form {{ ShowInTaskbar = true }}; var h = f.Handle; f.Show(); Application.Run(f); }} }}' -OutputAssembly '{tempExe}' -OutputType WindowsApplication -ReferencedAssemblies System.Windows.Forms\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (var compileProc = Process.Start(compilePsi))
                {
                    compileProc?.WaitForExit();
                }

                Assert.True(File.Exists(tempExe), "Failed to compile temporary test executable.");

                var psi = new ProcessStartInfo
                {
                    FileName = tempExe,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetTempPath(),
                };

                using (var wrapper = new ProcessWrapper(psi, _logger))
                {
                    _wrappersToCleanup.Add(wrapper);
                    wrapper.Start();
                    var process = wrapper.UnderlyingProcess;

                    // Wait until USER32 allocates the MainWindowHandle.
                    // Extend spin interval or check process status defensively for ARM64 runner latency.
                    bool windowReady = SpinWait.SpinUntil(() =>
                    {
                        try
                        {
                            process.Refresh();
                            return !process.HasExited && process.MainWindowHandle != IntPtr.Zero;
                        }
                        catch
                        {
                            return false;
                        }
                    }, TimeSpan.FromSeconds(TestTimeouts.CiGenerousSeconds * 2));

                    Assert.True(windowReady, "Test process window handle was not initialized within the timeout.");

                    // Act
                    TestReflection.InvokeNonPublic(
                        wrapper,
                        "StopTree",
                        process,
                        TestTimeouts.CiGenerousMs);

                    // Assert
                    Assert.True(wrapper.HasExited);
                    Assert.Contains(_logger.Infos, m => m.Contains("canceled with code") || m.Contains("canceled gracefully"));
                }
            }
            finally
            {
                if (File.Exists(tempExe))
                {
                    // The temp file is under %TEMP%; leaving it behind is harmless
                    try { File.Delete(tempExe); } catch { }
                }
            }
        }

        #endregion

        #region Enumerated Child Disposal Tests

        [Fact]
        public void StopDescendants_EnumeratedChild_IsDisposedExactlyOnce()
        {
            // Arrange
            const int parentPid = 424241;
            var child = new DisposeCountingProcess();

            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                // The child is deliberately never started: the nested StopTree call then reads no PID, so it
                // asks for the children of PID 0 and the cascade takes its already-exited path without
                // touching a real OS process. The child is left unwrapped by a using on purpose - the sweep
                // under test is what disposes it.
                wrapper.ChildEnumerator = (pid, startTime) =>
                    pid == parentPid ? new List<Process> { child } : new List<Process>();

                // Act
                wrapper.StopDescendants(parentPid, DateTime.Now, TestTimeouts.ProcessWrapperGracefulStopMs);

                // Assert
                Assert.Equal(1, child.DisposeCount);
            }
        }

        [Fact]
        public void StopTree_EnumeratedChild_IsDisposedExactlyOnce()
        {
            // Arrange
            var parent = new DisposeCountingProcess();
            var child = new DisposeCountingProcess();
            var handedOut = false;

            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                // Neither process is started, so both levels of the cascade ask for the children of PID 0.
                // Handing the set out once is what terminates the recursion.
                wrapper.ChildEnumerator = (pid, startTime) =>
                {
                    if (handedOut) return new List<Process>();
                    handedOut = true;
                    return new List<Process> { child };
                };

                // Act
                TestReflection.InvokeNonPublic(wrapper, "StopTree", parent, TestTimeouts.ProcessWrapperGracefulStopMs);

                // Assert
                Assert.Equal(1, child.DisposeCount);
            }

            parent.Dispose();
        }

        [Fact]
        public void StopDescendants_EnumeratedChildDisposeThrows_LogsDebugAndDoesNotPropagate()
        {
            // Arrange
            const int parentPid = 424242;
            var child = new DisposeCountingProcess { ThrowOnDispose = true };

            try
            {
                using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
                {
                    wrapper.ChildEnumerator = (pid, startTime) =>
                        pid == parentPid ? new List<Process> { child } : new List<Process>();

                    // Act
                    var exception = Record.Exception(() =>
                        wrapper.StopDescendants(parentPid, DateTime.Now, TestTimeouts.ProcessWrapperGracefulStopMs));

                    // Assert
                    Assert.Null(exception);
                    Assert.Contains(_logger.Debugs, m =>
                        m.Contains("Failed to dispose enumerated child handle")
                        && m.Contains(DisposeCountingProcess.DisposeFailureMessage));
                }
            }
            finally
            {
                // Teardown must not throw, and the sweep under test has already disposed it once.
                child.ThrowOnDispose = false;
                child.Dispose();
            }
        }

        [Theory]
        [InlineData(0, false)]        // the parent PID could not be read
        [InlineData(424245, true)]    // the parent StartTime could not be read
        public void StopDescendants_LineageNotCaptured_WarnsAndNeverEnumerates(int parentPid, bool startTimeMissing)
        {
            // Arrange
            // Either half of the lineage being unreadable makes a PID/StartTime pair unsafe to match on,
            // so the sweep must warn and return BEFORE enumerating rather than scan on a partial identity.
            var enumeratorCalls = 0;

            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.ChildEnumerator = (pid, startTime) => { enumeratorCalls++; return new List<Process>(); };
                var parentStartTime = startTimeMissing ? DateTime.MinValue : DateTime.Now;

                // Act
                wrapper.StopDescendants(parentPid, parentStartTime, TestTimeouts.ProcessWrapperGracefulStopMs);

                // Assert
                Assert.Contains(_logger.Warnings, m => m.Contains("Descendant sweep skipped"));
                Assert.Equal(0, enumeratorCalls);
                // The early return is what is under test: reaching the enumeration would log the
                // empty-result line instead.
                Assert.DoesNotContain(_logger.Infos, m => m.Contains("No active descendants found"));
            }
        }

        [Fact]
        public void StopDescendants_EnumeratorThrows_WarnsAndDoesNotPropagate()
        {
            // Arrange
            // A failed snapshot leaves the descendant set unknown, so the sweep abandons the cascade and
            // reports it rather than letting the failure reach the caller that is stopping the service.
            const int parentPid = 424246;

            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.ChildEnumerator = (pid, startTime) => throw new InvalidOperationException("snapshot failed");

                // Act
                var exception = Record.Exception(() =>
                    wrapper.StopDescendants(parentPid, DateTime.Now, TestTimeouts.ProcessWrapperGracefulStopMs));

                // Assert
                Assert.Null(exception);
                Assert.Contains(_logger.Warnings, m =>
                    m.Contains($"Descendant enumeration for PID {parentPid} failed; skipping cascaded stop: snapshot failed"));
            }
        }

        [Fact]
        public void StopTree_EnumeratorThrows_WarnsAndStillTerminatesTheNode()
        {
            // Arrange
            // StopTree's own enumeration failure is handled differently from StopDescendants': it falls back
            // to an empty child set and carries on, so the node itself is still terminated.
            var parent = new DisposeCountingProcess();

            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.ChildEnumerator = (pid, startTime) => throw new InvalidOperationException("snapshot failed");

                // Act
                var exception = Record.Exception(() =>
                    TestReflection.InvokeNonPublic(wrapper, "StopTree", parent, TestTimeouts.ProcessWrapperGracefulStopMs));

                // Assert
                Assert.Null(exception);
                Assert.Contains(_logger.Warnings, m => m.Contains("skipping sub-tree stop: snapshot failed"));
                Assert.Contains(_logger.Infos, m => m.StartsWith("Terminating node:"));
            }

            parent.Dispose();
        }

        [Fact]
        public void StopTree_EnumeratedChildDisposeThrows_LogsDebugAndDoesNotPropagate()
        {
            // Arrange
            // The StopDescendants twin of this test covers that method's own disposal loop; StopTree owns a
            // second one, and a handle that fails to close must not abort a shutdown in progress there either.
            var parent = new DisposeCountingProcess();
            var child = new DisposeCountingProcess { ThrowOnDispose = true };
            var handedOut = false;

            try
            {
                using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
                {
                    // Neither process is started, so both levels of the cascade ask for the children of PID 0.
                    // Handing the set out once is what terminates the recursion.
                    wrapper.ChildEnumerator = (pid, startTime) =>
                    {
                        if (handedOut) return new List<Process>();
                        handedOut = true;
                        return new List<Process> { child };
                    };

                    // Act
                    var exception = Record.Exception(() =>
                        TestReflection.InvokeNonPublic(wrapper, "StopTree", parent, TestTimeouts.ProcessWrapperGracefulStopMs));

                    // Assert
                    Assert.Null(exception);
                    Assert.Equal(1, child.DisposeCount);
                    Assert.Contains(_logger.Debugs, m =>
                        m.Contains("Failed to dispose enumerated child handle")
                        && m.Contains(DisposeCountingProcess.DisposeFailureMessage));
                }
            }
            finally
            {
                // Teardown must not throw, and the sweep under test has already disposed the child once.
                child.ThrowOnDispose = false;
                child.Dispose();
                parent.Dispose();
            }
        }

        /// <summary>
        /// A <see cref="Process"/> that records how often it is explicitly disposed, and can be made to
        /// throw from disposal. Handed to the cascade through <c>ProcessWrapper.ChildEnumerator</c>, which is
        /// what makes the disposal of an enumerated child observable at all.
        /// </summary>
        private sealed class DisposeCountingProcess : Process
        {
            /// <summary>Message the simulated disposal failure carries, so a test can match on it.</summary>
            public const string DisposeFailureMessage = "Simulated enumerated-child disposal failure.";

            private int _disposeCount;

            /// <summary>Gets the number of explicit <see cref="Process.Dispose()"/> calls this instance has seen.</summary>
            public int DisposeCount => _disposeCount;

            /// <summary>Gets or sets whether explicit disposal throws.</summary>
            public bool ThrowOnDispose { get; set; }

            protected override void Dispose(bool disposing)
            {
                // Only explicit disposal is counted, and only explicit disposal throws: raising from a
                // finalizer would tear down the test host rather than fail a test.
                if (!disposing)
                {
                    base.Dispose(false);
                    return;
                }

                _disposeCount++;
                base.Dispose(true);

                if (ThrowOnDispose)
                {
                    throw new InvalidOperationException(DisposeFailureMessage);
                }
            }
        }

        #endregion

        #region Stop Failure Arm Seam Tests

        [Fact]
        public void Stop_CloseMainWindowThrowsOnLiveProcess_FallsThroughToKill()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            {
                Assert.True(wrapper.Start());

                // The process stays alive, so the 'when (process.HasExited)' filter on the first catch does
                // not match and the catch-all arm #2129 added is the one that runs.
                wrapper.MainWindowCloser = _ => throw new InvalidOperationException("window call failed");

                // Act
                var result = wrapper.Stop(TestTimeouts.ProcessWrapperGracefulStopMs);

                // Assert
                Assert.False(result);   // force-killed, not "already exited" (the #2129 regression)
                Assert.Contains(_logger.Warnings, m =>
                    m.StartsWith("CloseMainWindow failed for") && m.Contains("Falling through to force-kill."));
                Assert.True(wrapper.HasExited);
            }
        }

        [Fact]
        public void Stop_CloseMainWindowThrowsAfterProcessExited_ReturnsNullAndLogsDebug()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            {
                Assert.True(wrapper.Start());

                // The seam kills the process first, so 'when (process.HasExited)' matches and the Debug arm
                // #1852 added is the one that runs: a process that truly died between Ctrl+C and the window
                // request is reported as already gone rather than force-killed.
                wrapper.MainWindowCloser = p =>
                {
                    p.Kill();
                    p.WaitForExit(TestTimeouts.CleanupWaitMs);
                    throw new InvalidOperationException("no process is associated with this object");
                };

                // Act
                var result = wrapper.Stop(TestTimeouts.ProcessWrapperGracefulStopMs);

                // Assert
                Assert.Null(result);
                Assert.Contains(_logger.Debugs, m => m.StartsWith("CloseMainWindow noted process exit for"));
            }
        }

        [Fact]
        public void Stop_KillThrowsAndProcessDoesNotExit_LogsBothWarningsAndReturnsFalse()
        {
            // Arrange
            var pid = 0;

            try
            {
                using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
                {
                    Assert.True(wrapper.Start());
                    pid = wrapper.Id;

                    // No window request succeeds, the kill itself fails, and the post-kill wait never observes
                    // an exit - so both Warn arms of the force-kill block run on the one call.
                    wrapper.MainWindowCloser = _ => false;
                    wrapper.ProcessKiller = _ => throw new Win32Exception(Errors.ERROR_ACCESS_DENIED);
                    wrapper.ExitWaiter = (_, __) => false;

                    // Act
                    var result = wrapper.Stop(TestTimeouts.ProcessWrapperGracefulStopMs);

                    // Assert
                    Assert.False(result);
                    Assert.Contains(_logger.Warnings, m => m.StartsWith("Kill failed for"));
                    Assert.Contains(_logger.Warnings, m =>
                        m.Contains("killed, but did not exit within")
                        && m.Contains($"{AppConfig.DefaultPostKillWaitMs / (double)AppConfig.MillisecondsPerSecond}s."));
                }
            }
            finally
            {
                // The seams suppressed the real kill, so the child outlives its wrapper: reap it by PID
                // rather than through the disposed wrapper (the #7067 lesson).
                KillByIdIfRunning(pid);
            }
        }

        [Fact]
        public void Kill_PostKillWaitTimesOut_ReturnsFalseAndLogsWarn()
        {
            // Arrange
            var waits = new List<int>();

            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            {
                Assert.True(wrapper.Start());

                // The real Process.Kill still runs; only the wait is driven, which is the arm #6043 added so
                // ProcessLauncher's orphan-cleanup catch can fire on a kill that did not take. The argument is
                // recorded as well, so the budget Kill(bool) passes is pinned and not only the warning text.
                wrapper.ExitWaiter = (_, ms) =>
                {
                    waits.Add(ms);
                    return false;
                };

                // Act
                var result = wrapper.Kill();

                // Assert
                Assert.False(result);
                Assert.Contains(_logger.Warnings, m => m.Contains("killed, but did not exit within"));
                Assert.Equal(new[] { AppConfig.DefaultPostKillWaitMs }, waits);
            }
        }

        [Fact]
        public void StopTree_DescendantStopsGracefully_LogsCanceledWithExitCode()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            using (var childWrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            {
                Assert.True(childWrapper.Start());
                var child = childWrapper.UnderlyingProcess;

                // No grandchildren, so the cascade terminates at this node; the window request succeeds and
                // the wait reports a graceful exit, which is the only way to reach the result == true branch.
                wrapper.ChildEnumerator = (pid, startTime) => new List<Process>();
                wrapper.MainWindowCloser = _ => true;
                wrapper.ExitWaiter = (p, ms) =>
                {
                    p.Kill();
                    return p.WaitForExit(ms);
                };

                // Act
                TestReflection.InvokeNonPublic(wrapper, "StopTree", child, TestTimeouts.CiGenerousMs);

                // Assert
                // The child was started by its own Process instance, so ExitCode is readable and the
                // exitCode.HasValue arm is the one that logs.
                Assert.Contains(_logger.Infos, m => m.Contains($"canceled with code {child.ExitCode}."));
                Assert.DoesNotContain(_logger.Infos, m => m.Contains("canceled gracefully."));
            }
        }

        [Fact]
        public void StopTree_DescendantObtainedByPidStopsGracefully_LogsCanceledGracefully()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            using (var childWrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            {
                Assert.True(childWrapper.Start());

                // The instance shape GetChildren produces (ProcessExtensions.cs builds each descendant with
                // Process.GetProcessById), so ExitCode is not readable and the exitCode.HasValue arm is skipped.
                // This is the instance every descendant StopTree stops in production arrives as.
                using (var enumerated = Process.GetProcessById(childWrapper.Id))
                {
                    wrapper.ChildEnumerator = (pid, startTime) => new List<Process>();
                    wrapper.MainWindowCloser = _ => true;
                    wrapper.ExitWaiter = (p, ms) =>
                    {
                        p.Kill();
                        return p.WaitForExit(ms);
                    };

                    // Act
                    TestReflection.InvokeNonPublic(wrapper, "StopTree", enumerated, TestTimeouts.CiGenerousMs);

                    // Assert
                    Assert.Contains(_logger.Infos, m => m.Contains("canceled gracefully."));
                    Assert.DoesNotContain(_logger.Infos, m => m.Contains("canceled with code"));
                }
            }
        }

        [Fact]
        public void Stop_ForwardsTheStopTimeoutToTheGracefulWaitAndThePostKillBudgetToTheSecond()
        {
            // Arrange
            var waits = new List<int>();

            // The two budgets must differ, or a swap between the waits would pass by accident. They do:
            // ProcessWrapperGracefulStopMs is 1000 and AppConfig.DefaultPostKillWaitMs is 3000.
            Assert.NotEqual(TestTimeouts.ProcessWrapperGracefulStopMs, AppConfig.DefaultPostKillWaitMs);

            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            {
                Assert.True(wrapper.Start());

                // Whether or not SendCtrlC attaches, 'sent' ends up true, so the graceful wait runs first; it
                // reports no exit, so the force-kill block and its own wait follow. The real Process.Kill still
                // runs through the default ProcessKiller, so no child outlives the test.
                wrapper.MainWindowCloser = _ => true;
                wrapper.ExitWaiter = (_, ms) =>
                {
                    waits.Add(ms);
                    return false;
                };

                // Act
                var result = wrapper.Stop(TestTimeouts.ProcessWrapperGracefulStopMs);

                // Assert
                Assert.False(result);

                // The sequence, not the set: the caller's stop timeout reaches the graceful wait and the
                // post-kill budget reaches the second one. This is what #1971 and #1227 each had to fix.
                Assert.Equal(
                    new[] { TestTimeouts.ProcessWrapperGracefulStopMs, AppConfig.DefaultPostKillWaitMs },
                    waits);
            }
        }

        /// <summary>
        /// Kills a process by identifier when it is still running, swallowing the races a best-effort reap
        /// runs into: the process exiting between the lookup and the kill, or the identifier being reused.
        /// </summary>
        /// <param name="processId">The identifier of the process to reap; 0 and negatives are ignored.</param>
        private static void KillByIdIfRunning(int processId)
        {
            if (processId <= 0)
            {
                return;
            }

            try
            {
                // Fully qualified: this assembly declares its own wrapper-shaped TestProcessCleanup, which
                // the enclosing namespace would otherwise resolve to.
                Servy.Testing.TestProcessCleanup.KillAndDispose(Process.GetProcessById(processId));
            }
            catch (ArgumentException)
            {
                // Already gone; nothing to reap.
            }
            catch (InvalidOperationException)
            {
                // The handle went stale between the lookup and the kill.
            }
        }

        #endregion

        [Fact]
        public void Kill_AlreadyExited_DoesNotThrow()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.Start();
                wrapper.WaitForExit(TestTimeouts.ProcessWrapperProcessTimeoutMs);

                // Act
                var exception = Record.Exception(() => wrapper.Kill());

                // Assert
                Assert.Null(exception);
            }
        }

        [Fact]
        public void Kill_CatchBranch_AccessViolationOrInvalidTargetState_LogsWarningSafely()
        {
            // Arrange
            // Closing the handle detaches the Process object while the OS process keeps running, so Kill's
            // HasExited read throws InvalidOperationException into the catch before the kill call is reached.
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            {
                wrapper.Start();

                // Read the PID while the wrapper can still answer for it: the Close() below detaches the
                // Process object, after which neither Id, nor Kill, nor the class-level teardown can reach
                // the OS process any more.
                int pid = wrapper.Id;

                try
                {
                    // Detach the Process object so that Kill's HasExited read throws
                    wrapper.UnderlyingProcess.Close();

                    // Act
                    bool? result = null;
                    var exception = Record.Exception(() => result = wrapper.Kill());

                    // Assert
                    Assert.Null(exception); // Exception should be caught internally by the Try/Catch block
                    Assert.False(result); // The catch branch publishes its failure as false, like the other Kill_* tests assert
                    Assert.Contains(_logger.Warnings, m => m.Contains("Kill failed for"));
                }
                finally
                {
                    // The detached process is out of reach of both Kill and the class-level teardown, so end
                    // it by PID instead of leaving it to sleep out its 10 seconds as an orphan alongside the
                    // rest of this non-parallel collection. KillAndDispose kills the tree and disposes the
                    // handle; ArgumentException means the process had already gone on its own.
                    try
                    {
                        Testing.TestProcessCleanup.KillAndDispose(Process.GetProcessById(pid));
                    }
                    catch (ArgumentException)
                    {
                        // Already gone.
                    }
                }
            }
        }

        [Fact]
        public void Kill_WhenProcessHasExited_ReturnsTrueWithoutAttemptingKill()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.Start();
                wrapper.WaitForExit(TestTimeouts.ProcessWrapperProcessTimeoutMs);

                // Act
                bool result = wrapper.Kill();

                // Assert
                Assert.True(result);
                Assert.Empty(_logger.Warnings);
            }
        }

        [Fact]
        public void Kill_WhenDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\"");
            wrapper.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => wrapper.Kill());
        }

        [Fact]
        public void Kill_ActiveProcess_SuccessfullyKillsAndReturnsTrue()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 30\""))
            {
                wrapper.Start();
                Assert.False(wrapper.UnderlyingProcess.HasExited);

                // Act
                bool result = wrapper.Kill(entireProcessTree: true);

                // Assert
                Assert.True(result);
                Assert.True(wrapper.UnderlyingProcess.HasExited);
            }
        }

        /// <summary>
        /// Verifies that <see cref="ProcessWrapper.Kill(bool)"/> forwards its flag, by killing a parent
        /// that owns a detached child and asserting the child ends too.
        /// </summary>
        /// <remarks>
        /// The sibling test above kills a childless process, so it stays green if the argument is dropped.
        /// The parent/child shape here is the one
        /// <c>StopDescendants_KillsEntireTree_AndHandlesRecursion</c> already uses: a PowerShell parent that
        /// starts a detached cmd.exe and then stays alive, so the child is reachable only through the
        /// process tree and a single-process kill leaves it running.
        /// </remarks>
        [Fact]
        public void Kill_EntireProcessTree_AlsoEndsTheChild()
        {
            // Arrange
            string commandArgs = "-NoProfile -WindowStyle Hidden -Command \"$p = Start-Process cmd.exe -ArgumentList '/c timeout /t 100 /nobreak' -WindowStyle Hidden -PassThru; while ($true) { Start-Sleep 1 }\"";
            int childPid = 0;

            try
            {
                using (var wrapper = CreateWrapper("powershell.exe", commandArgs, createNoWindow: true))
                {
                    Assert.True(wrapper.Start());

                    int parentPid = wrapper.UnderlyingProcess.Id;
                    DateTime parentStartTime = wrapper.UnderlyingProcess.StartTime;

                    // Poll until the child has finished spawning, on the same budget the tree test uses.
                    bool childSpawned = SpinWait.SpinUntil(() =>
                    {
                        try
                        {
                            foreach (var child in ProcessExtensions.GetChildren(parentPid, parentStartTime))
                            {
                                using (child)
                                {
                                    if (child.ProcessName.Equals("cmd", StringComparison.OrdinalIgnoreCase))
                                    {
                                        childPid = child.Id;
                                        return true;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Suppress intermittent access deviations while the child is initializing
                        }
                        return false;
                    }, TimeSpan.FromSeconds(TestTimeouts.CiGenerousSeconds));

                    Assert.True(childSpawned && childPid > 0,
                        "Child cmd.exe never spawned; the test cannot tell a tree kill from a single-process kill.");

                    // Act
                    bool result = wrapper.Kill(entireProcessTree: true);

                    // Assert
                    Assert.True(result);

                    bool childEnded = SpinWait.SpinUntil(() =>
                    {
                        try
                        {
                            using (var child = Process.GetProcessById(childPid))
                            {
                                child.Refresh();
                                return child.HasExited;
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Process identifier has been completely cleared out by the OS kernel
                            return true;
                        }
                        catch (InvalidOperationException)
                        {
                            // Process state tracking references are dead/gone
                            return true;
                        }
                    }, TestTimeouts.DescendantExitWait);

                    Assert.True(childEnded,
                        $"Kill(entireProcessTree: true) left the child cmd.exe with PID {childPid} running.");
                }
            }
            finally
            {
                // A dropped flag leaves the child behind, so reap it by identifier rather than
                // letting a failing run leak a 100 second timeout onto the runner.
                KillByIdIfRunning(childPid);
            }
        }

        #endregion

        #region Win32 Interop & SendCtrlC Signal Exception Tests

        [Fact]
        public void TryStopGracefullyOrKill_ExitedOrInvalidProcess_HandlesStateWithoutCrashing()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.Start();
                wrapper.WaitForExit(TestTimeouts.ProcessWrapperProcessTimeoutMs);

                // Act
                // Act on an already exited process handle to trigger the initial null/exited evaluation checks
                var result = TestReflection.InvokeNonPublic(wrapper, "TryStopGracefullyOrKill", wrapper.UnderlyingProcess, TestTimeouts.ProcessWrapperGracefulStopMs, TestTimeouts.ProcessWrapperPostKillWaitMs);

                // Assert
                Assert.Null(result);
            }
        }

        [Fact]
        public void SendCtrlC_LiveWindowlessChild_ReturnsFalseToFallbackChain()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", $"-NoProfile -Command \"Start-Sleep -Seconds {TestTimeouts.ChildSleepSeconds}\""))
            {
                wrapper.Start();

                // Act - Trigger SendCtrlC directly on a wrapper targeting a windowless background task runner profile
                var result = TestReflection.InvokeNonPublic(wrapper, "SendCtrlC", wrapper.UnderlyingProcess);

                // Assert
                Assert.False((bool)result);

                // Cleanup
                TestProcessCleanup.KillNow(wrapper);
            }
        }

        [Fact]
        public void SendCtrlC_ProcessHasExited_ReturnsNull()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.Start();
                wrapper.WaitForExit(TestTimeouts.ProcessWrapperProcessTimeoutMs);

                // Act - Pass an exited process instance directly to SendCtrlC
                var result = TestReflection.InvokeNonPublic(wrapper, "SendCtrlC", wrapper.UnderlyingProcess);

                // Assert
                Assert.Null(result);
            }
        }

        [Fact(Skip = "Skipping SendCtrlC test to prevent console IPC pipe crash.")]
        public void SendCtrlC_ProcessWithAttachedConsole_SendsSignalSuccessfully()
        {
            // The attribute-level Skip above is unconditional, so this body never runs on any
            // architecture; conhost/GenerateConsoleCtrlEvent severs testhost.exe's stdio IPC pipe
            // and crashes the test host, first observed on ARM64 (native and emulated).

            // Arrange
            // Launch cmd.exe with CreateNoWindow = false so Windows allocates a console buffer.
            using (var wrapper = CreateWrapper("cmd.exe", "/c pause", createNoWindow: false))
            {
                wrapper.Start();
                var process = wrapper.UnderlyingProcess;

                // Act & Assert
                // Poll with retries to account for conhost.exe setup latency on headless CI runners.
                bool result = false;

                for (int attempt = 1; attempt <= TestTimeouts.MaxPollAttempts; attempt++)
                {
                    if (process != null && !process.HasExited)
                    {
                        result = (bool)TestReflection.InvokeNonPublic(wrapper, "SendCtrlC", process);
                        if (result)
                        {
                            break;
                        }
                    }

                    Thread.Sleep(TestTimeouts.PollIntervalMs);
                }

                Assert.True(result, "SendCtrlC failed to attach to console or signal the process within the retry window.");
                Assert.Contains(_logger.Infos, m => m.Contains("Sent Ctrl+C to process"));

                // Cleanup
                TestProcessCleanup.KillNow(wrapper);
            }
        }

        [Theory]
        [InlineData(Errors.ERROR_PIPE_NOT_CONNECTED, true)]
        [InlineData(Errors.ERROR_INVALID_HANDLE, false)]
        [InlineData(Errors.ERROR_GEN_FAILURE, false)]
        [InlineData(Errors.ERROR_INVALID_PARAMETER, null)]
        [InlineData(1234, false)]   // default arm
        public void ClassifyAttachFailure_MapsWin32ErrorToSignalOutcome(int error, bool? expected)
            => Assert.Equal(expected, ProcessWrapper.ClassifyAttachFailure(error));

        #endregion
    }
}
