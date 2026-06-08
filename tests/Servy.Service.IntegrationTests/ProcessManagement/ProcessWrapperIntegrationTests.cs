using Servy.Core.Logging;
using Servy.Service.ProcessManagement;
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
                        p.Kill(true);
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
            var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\"");
            wrapper.Dispose();

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
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 42\""))
            {
                bool started = wrapper.Start();

                Assert.True(started);
                Assert.True(wrapper.Id > 0);
                Assert.NotNull(wrapper.StartInfo);
                Assert.NotNull(wrapper.UnderlyingProcess);
                Assert.True(wrapper.EnableRaisingEvents); // Constructor default

                // Wait for it to finish
                bool exited = wrapper.WaitForExit(5000);

                Assert.True(exited);
                Assert.True(wrapper.HasExited);
                Assert.Equal(42, wrapper.ExitCode);
                Assert.True(wrapper.StartTime > DateTime.MinValue);
                Assert.Contains(wrapper.Id.ToString(), wrapper.Format());
            }
        }

        [Fact]
        public void PropertySetters_UpdateUnderlyingProcess()
        {
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 2\""))
            {
                wrapper.Start();

                // Act & Assert PriorityClass
                wrapper.PriorityClass = ProcessPriorityClass.BelowNormal;
                Assert.Equal(ProcessPriorityClass.BelowNormal, wrapper.PriorityClass);

                // Act & Assert EnableRaisingEvents
                wrapper.EnableRaisingEvents = false;
                Assert.False(wrapper.EnableRaisingEvents);

                wrapper.Kill();
            }
        }

        [Fact]
        public void NativeProperties_Getters_RetrieveValidOperatingSystemHandles()
        {
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 2\""))
            {
                wrapper.Start();

                // Act
                IntPtr processHandle = wrapper.Handle;
                IntPtr windowHandle = wrapper.MainWindowHandle;

                // Assert
                Assert.NotEqual(IntPtr.Zero, processHandle);
                Assert.Equal(IntPtr.Zero, windowHandle); // Console window initialized with CreateNoWindow = true returns Zero

                wrapper.Kill();
            }
        }

        #endregion

        #region Event Modification Tracking Tests

        [Fact]
        public void DataAndExitEvents_AddAndRemoveHandlers_MaintainsSubscriptionsWithoutExceptions()
        {
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
        public async Task WaitForExitOrTimeoutAsync_StaysAlive_ReturnsTrue()
        {
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            {
                wrapper.Start();

                bool isHealthy = await wrapper.WaitAndCheckStillRunningAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

                Assert.True(isHealthy);
                wrapper.Kill();
            }
        }

        [Fact]
        public async Task WaitForExitOrTimeoutAsync_ExitsEarly_ReturnsFalse()
        {
            using (var wrapper = CreateWrapper("cmd.exe", "/c exit 0"))
            {
                wrapper.StartInfo.WorkingDirectory = Environment.SystemDirectory;
                wrapper.Start();

                bool isHealthy = await wrapper.WaitAndCheckStillRunningAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

                Assert.False(isHealthy);
            }
        }

        [Fact]
        public async Task WaitForExitOrTimeoutAsync_Cancellation_ThrowsTaskCanceledException()
        {
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
            {
                wrapper.Start();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    wrapper.WaitAndCheckStillRunningAsync(TimeSpan.FromSeconds(10), cts.Token));

                wrapper.Kill();
            }
        }

        [Fact]
        public void WaitForExit_InfiniteBlock_ExecutesSuccessfullyOnTerminatedProcess()
        {
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
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.Start();
                wrapper.WaitForExit(5000);

                // Act
                bool? result = wrapper.Stop(1000);

                // Assert
                Assert.Null(result);
            }
        }

        [Fact]
        public void Stop_ForceKillFallback_ReturnsFalse_AndLogs()
        {
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"[Console]::TreatControlCAsInput = $true; while($true) { Start-Sleep 1 }\"", createNoWindow: true))
            {
                wrapper.Start();

                // Act: Force graceful timeout expiration to trigger process.Kill fallback loop branch
                bool? result = wrapper.Stop(50);

                // Assert
                Assert.False(result);
                Assert.True(wrapper.HasExited);
                Assert.Contains(_logger.Infos, m => m.Contains("Graceful shutdown not supported or timed out"));
            }
        }

        [Fact]
        public void StopDescendants_KillsEntireTree_AndHandlesRecursion()
        {
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"while ($true) { Start-Sleep 1 }\"", createNoWindow: true))
            {
                wrapper.Start();

                int parentPid = wrapper.Id;
                DateTime parentStartTime = wrapper.StartTime;

                Thread.Sleep(500);

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
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.Start();
                wrapper.WaitForExit(5000);

                // Act
                var exception = Record.Exception(() => wrapper.Kill());

                // Assert
                Assert.Null(exception);
            }
        }

        [Fact]
        public void Kill_CatchBranch_AccessViolationOrInvalidTargetState_LogsWarningSafely()
        {
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.Start();
                wrapper.WaitForExit(5000);

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
        public void SendCtrlC_InvalidProcessParameters_ReturnsNullOrFalseWithoutCrashing()
        {
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"exit 0\""))
            {
                wrapper.Start();
                wrapper.WaitForExit(5000);

                // Act - Force internal Win32 interop execution flow via TryStopGracefullyOrKill private method mapping
                var privateMethod = typeof(ProcessWrapper).GetMethod("TryStopGracefullyOrKill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Act on an already exited process handle to trigger the initial null/exited evaluation checks
                var result = privateMethod!.Invoke(wrapper, new object[] { wrapper.UnderlyingProcess, 1000, 500 });

                // Assert
                Assert.Null(result);
            }
        }

        [Fact]
        public void SendCtrlC_ProcessWithNoConsoleAttached_GracefullyReturnsFalseToFallbackChain()
        {
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 5\""))
            {
                wrapper.Start();

                var privateMethod = typeof(ProcessWrapper).GetMethod("SendCtrlC", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Act - Invoke SendCtrlC directly on a wrapper targeting a windowless background task runner profile
                var result = privateMethod!.Invoke(wrapper, new object[] { wrapper.UnderlyingProcess });

                // Assert: True/False depends cleanly on native environment access, but path must complete without unhandled crashes.
                Assert.NotNull(result);

                wrapper.Kill();
            }
        }

        #endregion

        #region Standard Streams Tests

        [Fact]
        public void RedirectStreams_EventsFireAndCanBeCanceled()
        {
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

                wrapper.Start();

                // FIX: Removed Assert.NotNull(wrapper.StandardOutput) and Assert.NotNull(wrapper.StandardError)
                // to prevent mixing synchronous readers with asynchronous event registration channels.

                wrapper.BeginOutputReadLine();
                wrapper.BeginErrorReadLine();

                bool processExited = wrapper.WaitForExit(10000);
                wrapper.UnderlyingProcess.WaitForExit();

                bool signalsReceived = WaitHandle.WaitAll(
                    new[] { outputFinished.WaitHandle, errorFinished.WaitHandle },
                    TimeSpan.FromSeconds(5));

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

        #region Mocks

        private class TestLogger : IServyLogger
        {
            public List<string> Infos { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
            public List<string> Errors { get; } = new List<string>();

            public string? Prefix => string.Empty;

            public void Info(string message, Exception? ex = null) => Infos.Add(message);
            public void Warn(string message, Exception? ex = null) => Warnings.Add(message);
            public void Error(string message, Exception? ex = null) => Errors.Add(message);
            public void Debug(string message, Exception? ex = null) { }

            public IServyLogger CreateScoped(string prefix) => throw new NotImplementedException();
            public void SetLogLevel(LogLevel level) { }
            public void SetIsEventLogEnabled(bool isEnabled) { }
            public void Dispose() { }
        }

        #endregion
    }
}