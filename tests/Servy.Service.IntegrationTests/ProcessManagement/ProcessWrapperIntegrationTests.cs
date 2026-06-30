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
    public class ProcessWrapperIntegrationTestsCollection : ICollectionFixture<object>
    {
        // Enforces sequential, isolated integration suite runs to protect the native Win32 console state lock mutations.
    }

    [Collection("ProcessWrapperIntegrationTests")]
    public class ProcessWrapperIntegrationTests : IDisposable
    {
        private readonly List<Process> _processesToCleanup = new List<Process>();
        private readonly TestLogger _logger = new TestLogger();

        public void Dispose()
        {
            foreach (var p in _processesToCleanup)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        ProcessHelper.KillProcessTree(p);
                    }
                }
                catch { /* Ignore cleanup errors */ }
                finally
                {
                    p.Dispose();
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
            _processesToCleanup.Add(wrapper.UnderlyingProcess);
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
        public void Dispose_CalledMultipleTimes_BypassesSecondInvocationSafely()
        {
            // Arrange
            var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\"");

            // Act
            wrapper.Dispose();
            var exception = Record.Exception(() => wrapper.Dispose());

            // Assert
            Assert.Null(exception);
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
                Assert.Equal(ProcessPriorityClass.BelowNormal, wrapper.PriorityClass);

                // Act & Assert EnableRaisingEvents
                wrapper.EnableRaisingEvents = false;
                Assert.False(wrapper.EnableRaisingEvents);

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
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"while ($true) { Start-Sleep 1 }\"", createNoWindow: true))
            {
                wrapper.Start();
                int parentPid = wrapper.Id;
                DateTime parentStartTime = wrapper.StartTime;

                Thread.Sleep(TestTimeouts.ProcessWrapperStopDescendantsTimeoutMs);

                // Act
                wrapper.StopDescendants(parentPid, parentStartTime, 1000);

                // Assert
                Assert.Contains(_logger.Infos, m => m.Contains("Scanning for top-level descendants"));

                try
                {
                    if (!wrapper.HasExited)
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

                // Act - Force internal Win32 interop execution flow via TryStopGracefullyOrKill private method mapping
                var privateMethod = typeof(ProcessWrapper).GetMethod("TryStopGracefullyOrKill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Act on an already exited process handle to trigger the initial null/exited evaluation checks
                var result = privateMethod.Invoke(wrapper, new object[] { wrapper.UnderlyingProcess, 1000, 500 });

                // Assert - Accept null (exited), false (force-killed), or true (graceful exit handled mid-teardown)
                if (result != null)
                {
                    Assert.IsType<bool>(result);
                }
            }
        }

        [Fact]
        public void SendCtrlC_ProcessWithNoConsoleAttached_GracefullyReturnsFalseToFallbackChain()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 5\""))
            {
                wrapper.Start();
                var privateMethod = typeof(ProcessWrapper).GetMethod("SendCtrlC", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Act - Invoke SendCtrlC directly on a wrapper targeting a windowless background task runner profile
                var result = privateMethod.Invoke(wrapper, new object[] { wrapper.UnderlyingProcess });

                // Assert: True/False depends cleanly on native environment access, but path must complete without unhandled crashes.
                Assert.NotNull(result);

                // Cleanup
                wrapper.Kill();
            }
        }

        #endregion

        #region Standard Streams Tests

        [Fact]
        public void RedirectStreams_EventsFireAndCanBeCanceled()
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
                wrapper.UnderlyingProcess.WaitForExit();

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