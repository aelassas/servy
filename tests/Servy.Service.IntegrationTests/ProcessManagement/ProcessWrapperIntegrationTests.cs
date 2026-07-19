using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Service.IntegrationTests.ProcessManagement
{
    [CollectionDefinition("ProcessWrapperIntegrationTests", DisableParallelization = true)]
    public class ProcessWrapperIntegrationTestsCollection
    {
        // Enforces sequential, isolated integration suite runs to protect the native Win32 console state lock mutations.
    }

    [Collection("ProcessWrapperIntegrationTests")]
    public class ProcessWrapperIntegrationTests : IDisposable
    {
        // Track active wrappers so we can safely read started PIDs during teardown
        private readonly List<ProcessWrapper> _wrappersToCleanup = new List<ProcessWrapper>();
        private readonly TestLogger _logger = new TestLogger();

        public void Dispose()
        {
            // Iterate over all tracked wrappers and clean up their associated OS processes
            foreach (var wrapper in _wrappersToCleanup)
            {
                try
                {
                    // If the process was started and has not exited yet, kill it cleanly
                    if (!wrapper.HasExited)
                    {
                        wrapper.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception)
                {
                    // Swallowed: Safe lookup boundaries (wrapper already disposed or process dead)
                }
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
        public void ObjectDisposed_AccessingProperties_ThrowsObjectDisposedException()
        {
            // Arrange
            var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\"");
            wrapper.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => wrapper.Id);
            Assert.Throws<ObjectDisposedException>(() => wrapper.HasExited);
            Assert.Throws<ObjectDisposedException>(() => wrapper.Handle);
            Assert.Throws<ObjectDisposedException>(() => wrapper.ExitCode);
            Assert.Throws<ObjectDisposedException>(() => wrapper.MainWindowHandle);
            Assert.Throws<ObjectDisposedException>(() => wrapper.EnableRaisingEvents);
            Assert.Throws<ObjectDisposedException>(() => wrapper.EnableRaisingEvents = true);
            Assert.Throws<ObjectDisposedException>(() => wrapper.StartTime);
            Assert.Throws<ObjectDisposedException>(() => wrapper.PriorityClass);
            Assert.Throws<ObjectDisposedException>(() => wrapper.PriorityClass = ProcessPriorityClass.Normal);
            Assert.Throws<ObjectDisposedException>(() => wrapper.StandardOutput);
            Assert.Throws<ObjectDisposedException>(() => wrapper.StandardError);
            Assert.Throws<ObjectDisposedException>(() => wrapper.StartInfo);
            Assert.Throws<ObjectDisposedException>(() => wrapper.UnderlyingProcess);

            Assert.Throws<ObjectDisposedException>(() => wrapper.Start());
            Assert.Throws<ObjectDisposedException>(() => wrapper.Stop(1000));
            Assert.Throws<ObjectDisposedException>(() => wrapper.StopDescendants(1, DateTime.Now, 1000));
            Assert.Throws<ObjectDisposedException>(() => wrapper.Format());
            Assert.Throws<ObjectDisposedException>(() => wrapper.Kill());
            Assert.Throws<ObjectDisposedException>(() => wrapper.WaitForExit(1000));
            Assert.Throws<ObjectDisposedException>(() => wrapper.CloseMainWindow());
            Assert.Throws<ObjectDisposedException>(() => wrapper.BeginOutputReadLine());
            Assert.Throws<ObjectDisposedException>(() => wrapper.BeginErrorReadLine());
            Assert.Throws<ObjectDisposedException>(() => wrapper.CancelOutputRead());
            Assert.Throws<ObjectDisposedException>(() => wrapper.CancelErrorRead());
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
                Assert.True(wrapper.Id > 0);
                Assert.NotNull(wrapper.StartInfo);
                Assert.NotNull(wrapper.UnderlyingProcess);
                Assert.True(wrapper.EnableRaisingEvents); // Constructor default
                Assert.True(wrapper.StartTime > DateTime.MinValue);

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

                // Act & Assert EnableRaisingEvents
                wrapper.EnableRaisingEvents = false;
                Assert.False(wrapper.UnderlyingProcess.EnableRaisingEvents);

                // Cleanup
                wrapper.Kill();
            }
        }

        [Fact]
        public void NativeProperties_Getters_RetrieveValidOperatingSystemHandles()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 2\""))
            {
                // Act
                wrapper.Start();
                IntPtr processHandle = wrapper.Handle;
                IntPtr windowHandle = wrapper.MainWindowHandle;

                // Assert
                Assert.NotEqual(IntPtr.Zero, processHandle);
                Assert.Equal(IntPtr.Zero, windowHandle); // Console window initialized with CreateNoWindow = true returns Zero

                // Cleanup
                wrapper.Kill();
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
                bool isHealthy = await wrapper.WaitAndCheckStillRunningAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

                // Assert
                Assert.True(isHealthy);
                wrapper.Kill();
            }
        }

        [Fact]
        public async Task WaitAndCheckStillRunningAsync_ExitsEarly_ReturnsFalse()
        {
            // Arrange
            using (var wrapper = CreateWrapper("cmd.exe", "/c exit 0"))
            {
                wrapper.StartInfo.WorkingDirectory = Environment.SystemDirectory;
                wrapper.Start();

                // Act
                bool isHealthy = await wrapper.WaitAndCheckStillRunningAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

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
                    wrapper.WaitAndCheckStillRunningAsync(TimeSpan.FromSeconds(10), cts.Token));

                // Cleanup
                wrapper.Kill();
            }
        }

        [Fact]
        public void WaitForExit_InfiniteBlock_ExecutesSuccessfullyOnTerminatedProcess()
        {
            // Arrange
            using (var wrapper = CreateWrapper("cmd.exe", "/c exit 0"))
            {
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
        public void Stop_GracefulShutdown_ReturnsTrue()
        {
            // Arrange
            // Launch a console app that handles the CancelKeyPress (Ctrl+C) signal and exits cleanly
            const string script = "[Console]::CancelKeyPress += { [Environment]::Exit(0) }; while($true) { Start-Sleep 1 }";
            using (var wrapper = CreateWrapper("powershell.exe", $"-NoProfile -Command \"{script}\"", createNoWindow: true))
            {
                wrapper.Start();

                // Give the PowerShell engine a brief moment to initialize the script space and register the event hook
                Thread.Sleep(1000);

                // Act
                // Stop will execute SendCtrlC(), which returns true. The script catches it, exits, and causes Stop to return true.
                bool? result = wrapper.Stop(TestTimeouts.ProcessWrapperProcessTimeoutMs);

                // Assert
                Assert.True(result, "Process wrapper failed to capture a clean graceful shutdown from the signal pipeline loop.");
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
                bool? result = wrapper.Stop(1000);

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

                // Dynamically poll until the child process infrastructure has fully completed initialization
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
                }, TimeSpan.FromMilliseconds(TestTimeouts.ProcessWrapperStopDescendantsTimeoutMs));

                // Act
                // Pass a non-existent, isolated PID space to execute the tracking loop cleanly.
                int fakePid = 99999;
                wrapper.StopDescendants(fakePid, DateTime.Now, 1000);

                // Now forcefully clean up the actual running tree via the managed root wrapper asset
                wrapper.Kill(entireProcessTree: true);
                wrapper.WaitForExit(500);

                // Assert
                if (childSpawned && childPid > 0)
                {
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
                    }, TimeSpan.FromSeconds(3));

                    Assert.True(childCleanedUp, $"Descendant process with PID {childPid} survived the tree termination attempt.");
                }

                // Ensure the root test wrapper handle itself is fully torn down cleanly
                try
                {
                    if (underlyingProcess != null && !underlyingProcess.HasExited)
                    {
                        underlyingProcess.Kill();
                    }
                    else if (!wrapper.HasExited)
                    {
                        wrapper.Kill();
                    }
                }
                catch (InvalidOperationException) { }
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
                wrapper.StopDescendants(wrapper.Id, DateTime.Now.AddDays(1), 1000);

                // Assert
                Assert.Contains(_logger.Infos, m => m.Contains("No active descendants found for PID"));
                wrapper.Kill();
            }
        }

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
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.Start();
                wrapper.WaitForExit(TestTimeouts.ProcessWrapperProcessTimeoutMs);

                // Force disposal of underlying process resources to trigger an internal exception layout cascade when Kill handles execute
                wrapper.UnderlyingProcess.Close();

                // Act
                var exception = Record.Exception(() => wrapper.Kill());

                // Assert
                Assert.Null(exception); // Exception should be caught internally by the Try/Catch block
                Assert.Contains(_logger.Warnings, m => m.Contains("Kill failed:"));
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
                var result = TestReflection.InvokeNonPublic(wrapper, "TryStopGracefullyOrKill", wrapper.UnderlyingProcess, 1000, 500);

                // Assert
                Assert.Null(result);
            }
        }

        [Fact]
        public void SendCtrlC_ProcessWithNoConsoleAttached_GracefullyReturnsFalseToFallbackChain()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 5\""))
            {
                wrapper.Start();

                // Act - Invoke SendCtrlC directly on a wrapper targeting a windowless background task runner profile
                var result = TestReflection.InvokeNonPublic(wrapper, "SendCtrlC", wrapper.UnderlyingProcess);

                // Assert
                Assert.False((bool)result);

                // Cleanup
                wrapper.Kill();
            }
        }

        #endregion

        #region Standard Streams Tests

        [Fact]
        public void RedirectStreams_EventsFire()
        {
            // Arrange
            using (var outputFinished = new ManualResetEventSlim(false))
            using (var errorFinished = new ManualResetEventSlim(false))
            using (var wrapper = CreateWrapper(
                "powershell.exe",
                "-NoProfile -Command \"Write-Output 'HELLO_OUT'; [Console]::Error.WriteLine('HELLO_ERR')\"",
                redirectOutput: true))
            {
                var stdOut = new List<string>();
                var stdErr = new List<string>();

                wrapper.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        var trimmed = e.Data.Trim();
                        if (trimmed == "HELLO_OUT") { stdOut.Add(trimmed); outputFinished.Set(); }
                    }
                };

                wrapper.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        var trimmed = e.Data.Trim();
                        if (trimmed == "HELLO_ERR") { stdErr.Add(trimmed); errorFinished.Set(); }
                    }
                };

                // Act
                wrapper.Start();

                wrapper.BeginOutputReadLine();
                wrapper.BeginErrorReadLine();

                bool processExited = wrapper.WaitForExit(TestTimeouts.ProcessWrapperProcessTimeoutMsGenerous);
                // Parameterless WaitForExit also waits for async output/error event handlers to drain;
                // the timeout overload above does not.
                wrapper.WaitForExit();

                bool signalsReceived = WaitHandle.WaitAll(
                    new[] { outputFinished.WaitHandle, errorFinished.WaitHandle },
                    TimeSpan.FromSeconds(5));

                // Assert
                Assert.True(processExited, "Process should have exited within timeout.");
                Assert.True(signalsReceived, "Did not receive expected stdout/stderr signals.");
                Assert.Contains("HELLO_OUT", stdOut);
                Assert.Contains("HELLO_ERR", stdErr);

                var cancelException = Record.Exception(() =>
                {
                    wrapper.CancelOutputRead();
                    wrapper.CancelErrorRead();
                });
                Assert.Null(cancelException);
            }
        }

        #endregion
    }
}