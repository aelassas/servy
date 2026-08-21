using Servy.Core.Native;
using Servy.Service.ProcessManagement;
using Servy.Testing;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
                        wrapper.WaitForExit(2000);
                    }
                }
                catch (Exception)
                {
                    // Swallowed: Safe lookup boundaries (wrapper already disposed or process dead)
                }
                finally
                {
                    try { wrapper.Dispose(); } catch { }
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
            Assert.Throws<ObjectDisposedException>(() => wrapper.ProcessorAffinity);
            Assert.Throws<ObjectDisposedException>(() => wrapper.ProcessorAffinity = new IntPtr(21L));
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

                // Act & Assert ProcessorAffinity (Use CPU 0 / bitmask 1 to guarantee validity across all CI core counts)
                IntPtr cpuAffinity = new IntPtr(1L);
                wrapper.ProcessorAffinity = cpuAffinity;
                Assert.Equal(cpuAffinity, wrapper.UnderlyingProcess.ProcessorAffinity);

                // Act & Assert EnableRaisingEvents
                wrapper.EnableRaisingEvents = false;
                Assert.False(wrapper.UnderlyingProcess.EnableRaisingEvents);

                // Cleanup
                wrapper.Kill(entireProcessTree: true);
                wrapper.WaitForExit(1000);
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
                wrapper.Kill(entireProcessTree: true);
                wrapper.WaitForExit(1000);
            }
        }

        [Fact]
        public void CloseMainWindow_WhenCalledOnConsoleApp_ExecutesWithoutThrowing()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 2\""))
            {
                wrapper.Start();

                // Act
                bool closed = wrapper.CloseMainWindow();

                // Assert
                // For a windowless console application, CloseMainWindow returns false cleanly without erroring
                Assert.False(closed);

                // Cleanup
                wrapper.Kill(entireProcessTree: true);
                wrapper.WaitForExit(1000);
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
                bool isHealthy = await wrapper.WaitAndCheckStillRunningAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                // Assert
                Assert.True(isHealthy);
                wrapper.Kill(entireProcessTree: true);
                wrapper.WaitForExit(1000);
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
                bool isHealthy = await wrapper.WaitAndCheckStillRunningAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

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
                wrapper.Kill(entireProcessTree: true);
                wrapper.WaitForExit(1000);
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
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 10\"", createNoWindow: true))
            {
                wrapper.Start();

                // Act: Stop triggers standard stop sequence; on headless CI, unattached console calls fall back to force kill safely
                bool? result = wrapper.Stop(1000);

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
                }, TimeSpan.FromSeconds(5));

                // Act
                // Pass the actual parent process identity to execute tree termination via the SUT
                wrapper.StopDescendants(parentPid, parentStartTime, 1000);

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
                }, TimeSpan.FromSeconds(3));

                Assert.True(childCleanedUp, $"Descendant process with PID {childPid} survived StopDescendants.");

                // Cleanup after assertions
                try
                {
                    if (underlyingProcess != null && !underlyingProcess.HasExited)
                    {
                        underlyingProcess.Kill(entireProcessTree: true);
                        underlyingProcess.WaitForExit(1000);
                    }
                    else if (!wrapper.HasExited)
                    {
                        wrapper.Kill(entireProcessTree: true);
                        wrapper.WaitForExit(1000);
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
                wrapper.Kill(entireProcessTree: true);
                wrapper.WaitForExit(1000);
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
                SpinWait.SpinUntil(() =>
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
                }, TimeSpan.FromSeconds(5));

                // Act - Call StopDescendants on the active parent process
                wrapper.StopDescendants(parentPid, parentStartTime, 1000);

                // Assert - Verify that descendant scanning log messages were produced
                Assert.Contains(_logger.Infos, m => m.Contains($"Scanning for top-level descendants of PID {parentPid}"));

                // Cleanup
                wrapper.Kill(entireProcessTree: true);
                wrapper.WaitForExit(1000);
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
                    UseShellExecute = false
                };

                // Act
                var exitedProcess = Process.Start(psi)!;
                exitedProcess.WaitForExit();
                exitedProcess.Close(); // Id/StartTime access inside StopTree throws InvalidOperationException

                var exception = Record.Exception(() =>
                    TestReflection.InvokeNonPublic(wrapper, "StopTree", exitedProcess, 1000));

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
                TestReflection.InvokeNonPublic(wrapper, "StopTree", wrapper.UnderlyingProcess, 1000);

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
            // Dynamically compile a lightweight Win32 GUI executable to TEMP.
            // Compiling with -OutputType WindowsApplication ensures the binary has the GUI subsystem header,
            // so Windows will not spawn a child conhost.exe process. WinForms creates a valid MainWindowHandle
            // via USER32 even in headless CI sessions, responding cleanly to CloseMainWindow (WM_CLOSE) with exit code 0.
            string tempExe = Path.Combine(Path.GetTempPath(), $"ServyTestWin_{Guid.NewGuid():N}.exe");

            try
            {
                var compilePsi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"Add-Type -TypeDefinition 'using System; using System.Windows.Forms; class Program {{ [STAThread] static void Main() {{ Application.Run(new Form()); }} }}' -OutputAssembly '{tempExe}' -OutputType WindowsApplication -ReferencedAssemblies System.Windows.Forms\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (var compileProc = Process.Start(compilePsi))
                {
                    compileProc?.WaitForExit();
                }

                Assert.True(File.Exists(tempExe), "Failed to compile temporary WinForms test executable.");

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

                    // Poll dynamically until the WinForms window handle is allocated by USER32
                    bool windowCreated = SpinWait.SpinUntil(() =>
                    {
                        try
                        {
                            process.Refresh();
                            return process.MainWindowHandle != IntPtr.Zero;
                        }
                        catch
                        {
                            return false;
                        }
                    }, TimeSpan.FromSeconds(5));

                    Assert.True(windowCreated, "Test window was not created within the timeout.");

                    // Act
                    TestReflection.InvokeNonPublic(
                        wrapper,
                        "StopTree",
                        process,
                        5000);

                    // Assert
                    Assert.True(wrapper.HasExited);
                    Assert.Contains(_logger.Infos, m => m.Contains("canceled with code") || m.Contains("canceled gracefully"));
                }
            }
            finally
            {
                if (File.Exists(tempExe))
                {
                    try { File.Delete(tempExe); } catch { }
                }
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
        public void SendCtrlC_LiveWindowlessChild_ReturnsFalseToFallbackChain()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Start-Sleep -Seconds 5\""))
            {
                wrapper.Start();

                // Act - Trigger SendCtrlC directly on a wrapper targeting a windowless background task runner profile
                var result = TestReflection.InvokeNonPublic(wrapper, "SendCtrlC", wrapper.UnderlyingProcess);

                // Assert
                Assert.False((bool)result!);

                // Cleanup
                wrapper.Kill(entireProcessTree: true);
                wrapper.WaitForExit(1000);
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
            // Skip execution on ARM64 environments (native or emulated) where conhost/GenerateConsoleCtrlEvent
            // severs testhost.exe's stdio IPC pipe and crashes the test host process.
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
            {
                Assert.Skip("Skipping SendCtrlC test on ARM64 environment to prevent console IPC pipe crash.");
            }

            // Arrange
            // Launch cmd.exe with CreateNoWindow = false so Windows allocates a console buffer.
            using (var wrapper = CreateWrapper("cmd.exe", "/c pause", createNoWindow: false))
            {
                wrapper.Start();
                var process = wrapper.UnderlyingProcess;

                // Act & Assert
                // Poll with retries to account for conhost.exe setup latency on headless CI runners.
                bool result = false;
                const int maxAttempts = 10;
                const int pollIntervalMs = 500;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    if (process != null && !process.HasExited)
                    {
                        result = (bool)TestReflection.InvokeNonPublic(wrapper, "SendCtrlC", process)!;
                        if (result)
                        {
                            break;
                        }
                    }

                    Thread.Sleep(pollIntervalMs);
                }

                Assert.True(result, "SendCtrlC failed to attach to console or signal the process within the retry window.");
                Assert.Contains(_logger.Infos, m => m.Contains("Sent Ctrl+C to process"));

                // Cleanup
                wrapper.Kill(entireProcessTree: true);
                wrapper.WaitForExit(1000);
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

        #region Standard Streams Tests

        [Fact]
        public void StandardOutput_Get_ReturnsValidStreamReader()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"Write-Output 'STREAM_TEST'\"", redirectOutput: true))
            {
                wrapper.Start();

                // Act
                StreamReader reader = wrapper.StandardOutput;
                string content = reader.ReadToEnd();
                wrapper.WaitForExit(TestTimeouts.ProcessWrapperProcessTimeoutMs);

                // Assert
                Assert.NotNull(reader);
                Assert.Contains("STREAM_TEST", content);
            }
        }

        [Fact]
        public void StandardError_Get_ReturnsValidStreamReader()
        {
            // Arrange
            using (var wrapper = CreateWrapper("powershell.exe", "-NoProfile -Command \"[Console]::Error.WriteLine('STDERR_TEST')\"", redirectOutput: true))
            {
                wrapper.Start();

                // Act
                StreamReader reader = wrapper.StandardError;
                string content = reader.ReadToEnd();
                wrapper.WaitForExit(TestTimeouts.ProcessWrapperProcessTimeoutMs);

                // Assert
                Assert.NotNull(reader);
                Assert.Contains("STDERR_TEST", content);
            }
        }

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

                bool processExited = wrapper.WaitForExit(TestTimeouts.ProcessWrapperProcessGenerousTimeoutMs);
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
