using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.ServiceDependencies;
using Servy.Core.Services;
using Servy.Core.UnitTests.Logging;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Core.UnitTests.Services
{
    /// <summary>
    /// Covers the <see cref="ServiceManager"/> arms whose only effect is a log entry: the install
    /// rollback's two failure paths and the filtered catch of the startup-type mapper. Both return
    /// the same value as the paths beside them, so the log line is what distinguishes them and these
    /// tests drive the static <see cref="Logger"/> - hence the sequential logger collection.
    /// </summary>
    [Collection(LoggerCollection.Name)]
    public class ServiceManagerDiagnosticsTests : IDisposable
    {
        private readonly List<IntPtr> _unmanagedAllocations = new List<IntPtr>();

        #region Install rollback logging

        [Fact]
        public async Task InstallService_ShouldLogManualCleanupWarning_WhenRollbackDeleteServiceReturnsFalse()
        {
            // Arrange
            var mocks = new InstallMocks(this);
            mocks.WindowsServiceApi.Setup(x => x.DeleteService(mocks.ServiceHandle)).Returns(false);
            mocks.Win32ErrorProvider.Setup(x => x.GetLastWin32Error()).Returns(1072);

            // Act
            var capture = await RunWithCapturedLogAsync(() => mocks.Manager.InstallServiceAsync(mocks.Options, cancellationToken: CancellationToken.None));

            // Assert
            Assert.False(capture.Result.IsSuccess);
            mocks.WindowsServiceApi.Verify(x => x.DeleteService(mocks.ServiceHandle), Times.Once);

            // A rollback that could not delete the service leaves an SCM orphan, and the only thing
            // standing between that and a silent leak is this line naming the service and the error.
            Assert.Contains($"Rollback failed: DeleteService returned false for '{InstallMocks.ServiceName}'", capture.Log);
            Assert.Contains("Win32 error: 1072", capture.Log);
            Assert.Contains("Manual cleanup may be required", capture.Log);
        }

        [Fact]
        public async Task InstallService_ShouldSwallowAndLog_WhenRollbackDeleteServiceThrows()
        {
            // Arrange
            var mocks = new InstallMocks(this);
            mocks.WindowsServiceApi.Setup(x => x.DeleteService(mocks.ServiceHandle))
                .Throws(new InvalidOperationException("SCM handle already closed"));

            // Act
            var capture = await RunWithCapturedLogAsync(() => mocks.Manager.InstallServiceAsync(mocks.Options, cancellationToken: CancellationToken.None));

            // Assert
            // The rollback runs inside a finally: an exception escaping it would replace the install's
            // own failure result with a throw, so swallowing it and logging is the contract.
            Assert.False(capture.Result.IsSuccess);
            Assert.Contains($"Rollback raised an exception for '{InstallMocks.ServiceName}'", capture.Log);
        }

        #endregion

        #region MapStartupType filtered catch

        [Theory]
        [InlineData(typeof(InvalidOperationException))]
        [InlineData(typeof(Win32Exception))]
        public async Task GetServiceStartupType_ShouldLogAtDebugAndReturnUnknown_WhenStartTypeThrowsProtectedServiceError(Type exceptionType)
        {
            // Arrange
            const string serviceName = "ProtectedService";

            // The two exception types the filter names are what ServiceController.StartType raises for
            // a service this process cannot open; a plain Exception falls to the catch-all below it,
            // which logs at Error level and is what the existing sibling test already covers.
            var thrown = exceptionType == typeof(Win32Exception)
                ? (Exception)new Win32Exception(5)
                : new InvalidOperationException("Access is denied");

            var mockController = new Mock<IServiceControllerWrapper>();
            mockController.Setup(c => c.ServiceName).Returns(serviceName);
            mockController.Setup(c => c.StartType).Throws(thrown);

            var manager = new ServiceManager(
                _ => mockController.Object,
                new Mock<IServiceControllerProvider>().Object,
                new Mock<IWindowsServiceApi>().Object,
                new Mock<IWin32ErrorProvider>().Object,
                new Mock<IServiceRepository>().Object);

            // Act
            var capture = await RunWithCapturedLogAsync(() =>
                Task.FromResult(manager.GetServiceStartupType(serviceName, CancellationToken.None)));

            // Assert
            Assert.Equal(ServiceStartType.Unknown, capture.Result);

            // Both arms return Unknown, so the level and the wording are the only observable
            // difference: a filter narrowed back to Win32Exception would land in the catch-all.
            Assert.Contains($"Access denied or Win32 error reading StartType for '{serviceName}'", capture.Log);
            Assert.DoesNotContain("Unexpected error mapping startup type", capture.Log);
        }

        #endregion

        #region Test Helpers

        /// <summary>The result of an action plus everything the static logger wrote while it ran.</summary>
        private sealed class LogCapture<T>
        {
            public LogCapture(T result, string log)
            {
                Result = result;
                Log = log;
            }

            public T Result { get; }

            public string Log { get; }
        }

        /// <summary>
        /// Routes the static <see cref="Logger"/> into a private temp directory at Debug level, runs
        /// <paramref name="action"/>, and returns its result together with the log text.
        /// </summary>
        private static async Task<LogCapture<T>> RunWithCapturedLogAsync<T>(Func<Task<T>> action)
        {
            var tempLogDir = Path.Combine(Path.GetTempPath(), "ServyTestLogs", Guid.NewGuid().ToString("N"));
            var tempLogFileName = $"Servy_Test_Log_{Guid.NewGuid():N}.log";
            var tempLogFilePath = Path.Combine(tempLogDir, tempLogFileName);

            try
            {
                Logger.Shutdown();
                Logger.Initialize(tempLogFileName, LogLevel.Debug, logDirectory: tempLogDir);

                var result = await action();

                // Flush and release the log file handle before reading it back
                Logger.Shutdown();

                var text = File.Exists(tempLogFilePath) ? File.ReadAllText(tempLogFilePath) : string.Empty;
                return new LogCapture<T>(result, text);
            }
            finally
            {
                Logger.Shutdown();

                // Initialize's logDirectory override is sticky - Shutdown does not clear it - so point
                // the static logger back at its default folder before leaving, or every later test in
                // the assembly writes into the temp directory this one is about to delete.
                Logger.Initialize(null, logDirectory: AppConfig.LogsFolderPath);
                Logger.Shutdown();

                if (Directory.Exists(tempLogDir))
                {
                    try { Directory.Delete(tempLogDir, recursive: true); } catch { /* Ignore cleanup errors */ }
                }
            }
        }

        /// <summary>
        /// The smallest install arrangement that reaches the rollback: the service is created, then
        /// the pre-shutdown configuration fails, so the finally block has an SCM entry to delete.
        /// </summary>
        private sealed class InstallMocks
        {
            public const string ServiceName = "TestService";

            public InstallMocks(ServiceManagerDiagnosticsTests owner)
            {
                var scmHandle = owner.CreateScmHandle();
                ServiceHandle = owner.CreateServiceHandle();

                WindowsServiceApi = new Mock<IWindowsServiceApi>();
                Win32ErrorProvider = new Mock<IWin32ErrorProvider>();

                WindowsServiceApi.Setup(x => x.OpenSCManager(null, null, It.IsAny<uint>())).Returns(scmHandle);
                WindowsServiceApi.Setup(x => x.CreateService(
                        scmHandle,
                        ServiceName,
                        It.IsAny<string>(),
                        It.IsAny<uint>(),
                        It.IsAny<uint>(),
                        It.IsAny<uint>(),
                        It.IsAny<uint>(),
                        It.IsAny<string>(),
                        null,
                        IntPtr.Zero,
                        ServiceDependenciesParser.NoDependencies,
                        ServiceAccounts.LocalSystem,
                        null))
                    .Returns(ServiceHandle);
                WindowsServiceApi.Setup(x => x.OpenService(scmHandle, ServiceName, It.IsAny<uint>())).Returns(ServiceHandle);

                // The pre-shutdown configuration is what fails, which is what sets needsRollback
                WindowsServiceApi.Setup(x => x.ChangeServiceConfig2(It.IsAny<SafeServiceHandle>(), It.IsAny<uint>(), It.IsAny<IntPtr>()))
                    .Returns(false);

                Manager = new ServiceManager(
                    _ => new Mock<IServiceControllerWrapper>().Object,
                    new Mock<IServiceControllerProvider>().Object,
                    WindowsServiceApi.Object,
                    Win32ErrorProvider.Object,
                    new Mock<IServiceRepository>().Object);

                Options = new InstallServiceOptions
                {
                    ServiceName = ServiceName,
                    Description = string.Empty,
                    WrapperExePath = "wrapper.exe",
                    RealExePath = "real.exe",
                    StartType = ServiceStartType.Automatic,
                    ProcessPriority = ProcessPriority.Normal
                };
            }

            public Mock<IWindowsServiceApi> WindowsServiceApi { get; }

            public Mock<IWin32ErrorProvider> Win32ErrorProvider { get; }

            public SafeServiceHandle ServiceHandle { get; }

            public ServiceManager Manager { get; }

            public InstallServiceOptions Options { get; }
        }

        private SafeScmHandle CreateScmHandle()
        {
            var handle = Activator.CreateInstance(typeof(SafeScmHandle), true) as SafeScmHandle;
            Assert.NotNull(handle);

            // TestReflection automatically ascends the inheritance chain to locate and invoke 'SetHandle' on SafeHandle
            TestReflection.InvokeNonPublic(handle, "SetHandle", Allocate());
            return handle;
        }

        private SafeServiceHandle CreateServiceHandle()
        {
            var handle = Activator.CreateInstance(typeof(SafeServiceHandle), true) as SafeServiceHandle;
            Assert.NotNull(handle);

            TestReflection.InvokeNonPublic(handle, "SetHandle", Allocate());
            return handle;
        }

        /// <summary>
        /// Allocates unmanaged space so a valid handle wrapper does not fault the runtime on Dispose.
        /// </summary>
        private IntPtr Allocate()
        {
            var ptr = Marshal.AllocHGlobal(64);
            lock (_unmanagedAllocations)
            {
                _unmanagedAllocations.Add(ptr);
            }

            return ptr;
        }

        public void Dispose()
        {
            lock (_unmanagedAllocations)
            {
                foreach (var ptr in _unmanagedAllocations)
                {
                    Marshal.FreeHGlobal(ptr);
                }

                _unmanagedAllocations.Clear();
            }
        }

        #endregion
    }
}
