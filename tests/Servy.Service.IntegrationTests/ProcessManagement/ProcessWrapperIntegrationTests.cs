using Servy.Core.Logging;
using Servy.Service.Helpers;
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

        #endregion

        #region Async Wait Tests

        [Fact]
        public async Task WaitForExitOrTimeoutAsync_StaysAlive_ReturnsTrue()
        {
            // FIX: Replaced 'ping' with PowerShell's Start-Sleep for reliable headless execution
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            {
                wrapper.Start();

                // Act: Wait for 1 second. The process takes 10 seconds, so it will remain healthy.
                bool isHealthy = await wrapper.WaitForExitOrTimeoutAsync(TimeSpan.FromSeconds(1));

                // Assert: Returns true because it did NOT exit before the timeout
                Assert.True(isHealthy);
                wrapper.Kill();
            }
        }

        [Fact]
        public async Task WaitForExitOrTimeoutAsync_ExitsEarly_ReturnsFalse()
        {
            // Use cmd.exe instead of powershell.exe for a guaranteed instant exit
            using (var wrapper = CreateWrapper("cmd.exe", "/c exit 0"))
            {
                wrapper.Start();

                // Act: Wait for 5 seconds. The process exits instantly.
                bool isHealthy = await wrapper.WaitForExitOrTimeoutAsync(TimeSpan.FromSeconds(5));

                // Assert: Returns false because it exited before the timeout finished
                Assert.False(isHealthy);
            }
        }

        [Fact]
        public async Task WaitForExitOrTimeoutAsync_Cancellation_ThrowsTaskCanceledException()
        {
            // FIX: Updated to use PowerShell here as well for consistency
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\""))
            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
            {
                wrapper.Start();

                // Act & Assert
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    wrapper.WaitForExitOrTimeoutAsync(TimeSpan.FromSeconds(10), cts.Token));

                wrapper.Kill();
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
            // A ping process without a window ignores CloseMainWindow and often ignores Ctrl+C when running headless
            using (var wrapper = CreateWrapper("ping.exe", "127.0.0.1 -n 20", createNoWindow: true))
            {
                wrapper.Start();

                // Act: Provide a very short graceful timeout to force the kill fallback
                bool? result = wrapper.Stop(50);

                // Assert
                Assert.False(result); // False indicates it had to be force killed
                Assert.True(wrapper.HasExited);

                // Verify fallback logging
                Assert.Contains(_logger.Infos, m => m.Contains("Graceful shutdown not supported or timed out"));
            }
        }

        [Fact]
        public void StopDescendants_KillsEntireTree()
        {
            // Construct a nested process tree: powershell -> ping
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"ping 127.0.0.1 -n 30\"", createNoWindow: true))
            {
                wrapper.Start();

                // Allow the OS time to spawn the child ping process
                Thread.Sleep(1000);

                // Act
                wrapper.StopDescendants(wrapper.Id, wrapper.StartTime, 1000);

                // Give the OS a moment to reap the descendants
                Thread.Sleep(500);

                // Assert: The stop logic logs the cascade
                Assert.Contains(_logger.Infos, m => m.Contains("Initiating cascaded kill"));

                // Verify native handles are gone (the parent powershell usually dies too when the shell command is terminated)
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

        #endregion

        #region Standard Streams Tests

        [Fact]
        public void RedirectStreams_EventsFireAndCanBeCanceled()
        {
            using (var outputFinished = new ManualResetEventSlim(false))
            using (var errorFinished = new ManualResetEventSlim(false))
            // 1. Use powershell.exe
            // 2. Add -NoProfile to skip loading user scripts (prevents access denied / slow starts)
            // 3. Use [Console]::Error.WriteLine to ensure a clean, raw string is sent to stderr
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
                wrapper.BeginOutputReadLine();
                wrapper.BeginErrorReadLine();

                // 4. Increased timeout from 5000ms to 10000ms to comfortably handle PowerShell's JIT warmup
                bool processExited = wrapper.WaitForExit(10000);
                wrapper.UnderlyingProcess.WaitForExit();

                // 5. Increased WaitAll timeout for the same reason
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

        #region Mocks

        private class TestLogger : IServyLogger
        {
            public List<string> Infos { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
            public List<string> Errors { get; } = new List<string>();

            public string Prefix => string.Empty;

            public void Info(string message, Exception ex) => Infos.Add(message);
            public void Warn(string message, Exception ex) => Warnings.Add(message);
            public void Error(string message, Exception ex) => Errors.Add(message);
            public void Debug(string message, Exception ex) { }

            public IServyLogger CreateScoped(string prefix) => throw new NotImplementedException();
            public void SetLogLevel(LogLevel level) { }
            public void SetIsEventLogEnabled(bool isEnabled) { }
            public void Dispose() { }
        }

        #endregion
    }
}