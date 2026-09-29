using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Service.CommandLine;
using Servy.Service.Native;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Xunit;
using IServiceHelper = Servy.Service.Helpers.IServiceHelper;
using ITimer = Servy.Service.Timers.ITimer;

namespace Servy.Service.UnitTests.Native
{
    /// <summary>
    /// Covers the paths of <see cref="Service"/> that reach the Service Control Manager through
    /// <see cref="IScmNative"/> and through <c>GetNativeServiceHandle</c>: the console detach in
    /// <c>OnStart</c>, the service-handle acquisition, the delayed PRESHUTDOWN registration and the
    /// body of <c>UpdateServiceStatus</c>, including its exit-code mapping.
    /// </summary>
    public class ScmNativeSeamTests : IDisposable
    {
        private const int SetServiceStatusPollTimeoutMs = 10_000;

        private readonly ServiceTestContext _ctx;
        private readonly FakeScmNative _scm;
        private readonly Mock<IStreamWriter> _mockStdoutWriter;
        private readonly Mock<IStreamWriter> _mockStderrWriter;
        private readonly Mock<ITimer> _mockTimer;
        private readonly Mock<IProcessWrapper> _mockProcess;
        private readonly List<IDisposable> _built = new List<IDisposable>();

        /// <summary>Wires the shared mocks the SCM paths need to reach their native calls.</summary>
        public ScmNativeSeamTests()
        {
            _ctx = new ServiceTestContext();
            _scm = new FakeScmNative();

            _mockStdoutWriter = new Mock<IStreamWriter>();
            _mockStderrWriter = new Mock<IStreamWriter>();
            _mockTimer = new Mock<ITimer>();
            _mockProcess = new Mock<IProcessWrapper>();

            _mockProcess.Setup(p => p.StartInfo).Returns(new ProcessStartInfo());
            _mockProcess.Setup(p => p.Start()).Returns(true);

            _ctx.StreamWriterFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<DateRotationType>(), It.IsAny<int>(), It.IsAny<bool>()))
                .Returns((string path, bool enableSizeRotation, long size, bool enableDateRotation, DateRotationType dateRotationType, int maxRotations, bool useLocalTimeForRotation) =>
                    path.Contains("stderr") ? _mockStderrWriter.Object : _mockStdoutWriter.Object);

            _ctx.TimerFactory.Setup(f => f.Create(It.IsAny<double>())).Returns(_mockTimer.Object);
            _ctx.ProcessFactory.Setup(f => f.Create(It.IsAny<ProcessStartInfo>(), It.IsAny<IServyLogger>())).Returns(_mockProcess.Object);
        }

        #region UpdateServiceStatus

        [Fact]
        public void UpdateServiceStatus_ZeroHandle_LogsAndMakesNoNativeCall()
        {
            // Arrange
            var service = BuildService();
            TestReflection.SetField(service, "_serviceHandle", IntPtr.Zero);

            // Act
            TestReflection.InvokeNonPublic(service, "UpdateServiceStatus", NativeMethods.SERVICE_STOPPED, 0);

            // Assert
            Assert.Empty(_scm.StatusCalls);
            _ctx.Logger.Verify(l => l.Error("Service handle is null, cannot update status", It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void UpdateServiceStatus_StoppedWithNonZeroExitCode_MapsToServiceSpecificError()
        {
            // Arrange
            var service = BuildService();
            TestReflection.SetField(service, "_serviceHandle", new IntPtr(1));
            service.ExitCode = 42;

            // Act
            TestReflection.InvokeNonPublic(service, "UpdateServiceStatus", NativeMethods.SERVICE_STOPPED, 0);

            // Assert
            var status = Assert.Single(_scm.StatusCalls);
            Assert.Equal(AppConfig.ServiceSpecificErrorCode, status.dwWin32ExitCode);
            Assert.Equal(42, status.dwServiceSpecificExitCode);
            Assert.Equal(0, status.dwControlsAccepted);
            Assert.Equal(0, status.dwCheckPoint);
        }

        [Fact]
        public void UpdateServiceStatus_StoppedWithZeroExitCode_ReportsNoError()
        {
            // Arrange
            var service = BuildService();
            TestReflection.SetField(service, "_serviceHandle", new IntPtr(1));

            // Act
            TestReflection.InvokeNonPublic(service, "UpdateServiceStatus", NativeMethods.SERVICE_STOPPED, 0);

            // Assert
            var status = Assert.Single(_scm.StatusCalls);
            Assert.Equal(0, status.dwWin32ExitCode);
            Assert.Equal(0, status.dwServiceSpecificExitCode);
            Assert.Equal(0, status.dwControlsAccepted);
        }

        [Fact]
        public void UpdateServiceStatus_Running_AcceptsStopAndPreShutdownAndZeroesTheCheckPoint()
        {
            // Arrange
            var service = BuildService();
            TestReflection.SetField(service, "_serviceHandle", new IntPtr(1));
            TestReflection.SetField(service, "_checkPoint", 7u);

            // Act
            TestReflection.InvokeNonPublic(service, "UpdateServiceStatus", NativeMethods.SERVICE_RUNNING, 0);

            // Assert
            var status = Assert.Single(_scm.StatusCalls);
            Assert.Equal(NativeMethods.SERVICE_WIN32_OWN_PROCESS, status.dwServiceType);
            Assert.Equal(NativeMethods.SERVICE_ACCEPT_STOP | NativeMethods.SERVICE_ACCEPT_PRESHUTDOWN, status.dwControlsAccepted);
            Assert.Equal(0, status.dwCheckPoint);
        }

        [Fact]
        public void UpdateServiceStatus_StopPending_PublishesTheCurrentCheckPointAndWaitHint()
        {
            // Arrange
            var service = BuildService();
            TestReflection.SetField(service, "_serviceHandle", new IntPtr(1));
            TestReflection.SetField(service, "_checkPoint", 7u);

            // Act
            TestReflection.InvokeNonPublic(service, "UpdateServiceStatus", NativeMethods.SERVICE_STOP_PENDING, 12345);

            // Assert
            var status = Assert.Single(_scm.StatusCalls);
            Assert.Equal(NativeMethods.SERVICE_ACCEPT_STOP | NativeMethods.SERVICE_ACCEPT_PRESHUTDOWN, status.dwControlsAccepted);
            Assert.Equal(7, status.dwCheckPoint);
            Assert.Equal(12345, status.dwWaitHint);
        }

        [Fact]
        public void UpdateServiceStatus_NativeCallFails_LogsTheWin32ErrorTheSeamReports()
        {
            // Arrange
            var service = BuildService();
            TestReflection.SetField(service, "_serviceHandle", new IntPtr(1));
            _scm.SetServiceStatusResult = false;
            _scm.LastWin32Error = 1314;

            // Act
            TestReflection.InvokeNonPublic(service, "UpdateServiceStatus", NativeMethods.SERVICE_RUNNING, 0);

            // Assert
            _ctx.Logger.Verify(l => l.Error("SetServiceStatus failed with Win32 error code: 1314", It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void UpdateServiceStatus_NativeCallThrows_LogsTheExceptionMessage()
        {
            // Arrange
            var service = BuildService();
            TestReflection.SetField(service, "_serviceHandle", new IntPtr(1));
            _scm.SetServiceStatusThrows = new InvalidOperationException("boom");

            // Act
            TestReflection.InvokeNonPublic(service, "UpdateServiceStatus", NativeMethods.SERVICE_RUNNING, 0);

            // Assert
            _ctx.Logger.Verify(l => l.Error("Exception in UpdateServiceStatus: boom", It.IsAny<Exception>()), Times.Once);
        }

        #endregion

        #region OnStart

        [Fact]
        public void OnStart_DetachesTheConsoleThroughTheSeam()
        {
            // Arrange
            _ = ArrangeSuccessfulStart(out var service, handle: IntPtr.Zero, testMode: true);

            // Act
            service.StartForTest();

            // Assert
            Assert.Equal(1, _scm.DetachConsoleCalls);
            Assert.Empty(_scm.StatusCalls);
        }

        [Fact]
        public void OnStart_NotInTestMode_ZeroServiceHandle_LogsAndSetsTheServiceSpecificExitCode()
        {
            // Arrange
            var scopedLogger = ArrangeSuccessfulStart(out var service, handle: IntPtr.Zero, testMode: false);

            // Act
            TestReflection.InvokeNonPublic(service, "OnStart", new object[] { new string[0] });

            // Assert
            Assert.Equal(AppConfig.ServiceSpecificErrorCode, service.ExitCode);
            scopedLogger.Verify(l => l.Error("Exception in OnStart.", It.IsAny<Exception>()), Times.Once);
            Assert.Empty(_scm.StatusCalls);
        }

        [Fact]
        public void OnStart_NotInTestMode_NonZeroServiceHandle_RegistersPreShutdownSupportThroughTheSeam()
        {
            // Arrange
            var scopedLogger = ArrangeSuccessfulStart(out var service, handle: new IntPtr(1234), testMode: false);

            // Act
            TestReflection.InvokeNonPublic(service, "OnStart", new object[] { new string[0] });

            // Assert
            Assert.True(WaitForStatusCall(), "The PRESHUTDOWN registration never reached the native seam.");
            var status = Assert.Single(_scm.StatusCalls);
            Assert.Equal(NativeMethods.SERVICE_RUNNING, status.dwCurrentState);
            Assert.Equal(NativeMethods.SERVICE_ACCEPT_STOP | NativeMethods.SERVICE_ACCEPT_PRESHUTDOWN, status.dwControlsAccepted);
            Assert.Equal(new IntPtr(1234), _scm.LastHandle);
            scopedLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Service handle obtained natively")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void OnStart_NotInTestMode_PreShutdownRegistrationFails_LogsTheSeamsWin32Error()
        {
            // Arrange
            var scopedLogger = ArrangeSuccessfulStart(out var service, handle: new IntPtr(1234), testMode: false);
            _scm.SetServiceStatusResult = false;
            _scm.LastWin32Error = 5;
            var failureLogged = ArrangeLogSignal(scopedLogger, l => l.Error("Failed to register PRESHUTDOWN support via native Win32. Error: 5", It.IsAny<Exception>()));

            // Act
            TestReflection.InvokeNonPublic(service, "OnStart", new object[] { new string[0] });

            // Assert
            Assert.True(WaitForStatusCall(), "The PRESHUTDOWN registration never reached the native seam.");
            Assert.True(SpinWait.SpinUntil(failureLogged, SetServiceStatusPollTimeoutMs), "The Win32 error the seam reported was never logged.");
            scopedLogger.Verify(l => l.Info("Service signaled RUNNING to SCM with PRESHUTDOWN support natively enabled.", It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void OnStart_NotInTestMode_TheNativeSeamReportsCancellation_LogsTheAbortedRegistration()
        {
            // Arrange
            var scopedLogger = ArrangeSuccessfulStart(out var service, handle: new IntPtr(1234), testMode: false);
            _scm.SetServiceStatusThrows = new OperationCanceledException();
            var abortLogged = ArrangeLogSignal(scopedLogger, l => l.Info("PRESHUTDOWN registration aborted due to service shutdown.", It.IsAny<Exception>()));

            // Act
            TestReflection.InvokeNonPublic(service, "OnStart", new object[] { new string[0] });

            // Assert
            Assert.True(WaitForStatusCall(), "The PRESHUTDOWN registration never reached the native seam.");
            Assert.True(SpinWait.SpinUntil(abortLogged, SetServiceStatusPollTimeoutMs), "The cancelled registration was never logged as aborted.");
        }

        [Fact]
        public void OnStart_NotInTestMode_TheNativeSeamThrows_LogsTheUnexpectedRegistrationError()
        {
            // Arrange
            var scopedLogger = ArrangeSuccessfulStart(out var service, handle: new IntPtr(1234), testMode: false);
            var failure = new InvalidOperationException("boom");
            _scm.SetServiceStatusThrows = failure;
            var errorLogged = ArrangeLogSignal(scopedLogger, l => l.Error("Unexpected error during PRESHUTDOWN registration.", failure));

            // Act
            TestReflection.InvokeNonPublic(service, "OnStart", new object[] { new string[0] });

            // Assert
            Assert.True(WaitForStatusCall(), "The PRESHUTDOWN registration never reached the native seam.");
            Assert.True(SpinWait.SpinUntil(errorLogged, SetServiceStatusPollTimeoutMs), "The unexpected registration failure was never logged with its exception.");
        }

        [Fact]
        public void OnStart_NotInTestMode_TearingDownDuringTheDelay_SkipsTheRegistration()
        {
            // Arrange
            var scopedLogger = ArrangeSuccessfulStart(out var service, handle: new IntPtr(1234), testMode: false);
            var skipLogged = ArrangeLogSignal(scopedLogger, l => l.Info("Skipping PRESHUTDOWN registration: Service is already tearing down.", It.IsAny<Exception>()));

            // Act
            TestReflection.InvokeNonPublic(service, "OnStart", new object[] { new string[0] });
            TestReflection.SetField(service, "_isTearingDown", true);

            // Assert
            Assert.True(SpinWait.SpinUntil(skipLogged, SetServiceStatusPollTimeoutMs), "The tearing-down service never logged the skipped registration.");
            Assert.Empty(_scm.StatusCalls);
        }

        #endregion

        #region Helpers

        /// <summary>Builds a <see cref="Service"/> wired to the fake seam and the shared mocks.</summary>
        /// <param name="handle">The handle the overridden accessor returns; ignored by the status tests.</param>
        /// <returns>The service, registered for disposal.</returns>
        private HandleStubService BuildService(IntPtr handle = default)
        {
            var service = new HandleStubService(
                _ctx.Helper.Object,
                _ctx.Logger.Object,
                _ctx.StreamWriterFactory.Object,
                _ctx.TimerFactory.Object,
                _ctx.ProcessFactory.Object,
                _ctx.PathValidator.Object,
                _ctx.ServiceRepository.Object,
                _scm,
                handle);

            _built.Add(service);
            return service;
        }

        /// <summary>
        /// Arranges the helper, logger and validation mocks so <c>OnStart</c> reaches its SCM block.
        /// </summary>
        /// <param name="service">Receives the service under test.</param>
        /// <param name="handle">The handle the overridden accessor returns.</param>
        /// <param name="testMode">Unused by the arrangement; documents the caller's intent.</param>
        /// <returns>The scoped logger mock the service promotes to.</returns>
        private Mock<IServyLogger> ArrangeSuccessfulStart(out HandleStubService service, IntPtr handle, bool testMode)
        {
            _ = testMode;

            var options = new StartOptions
            {
                ServiceName = "ScmSeamService",
                ExecutablePath = "C:\\Windows\\notepad.exe",
                StartupDirectory = "C:\\Windows",
                EnableHealthMonitoring = false,
                MaxFailedChecks = 3,
                RecoveryAction = RecoveryAction.None,
                StdoutPath = "C:\\Logs\\stdout.log",
                StderrPath = "C:\\Logs\\stderr.log"
            };

            var scopedLogger = new Mock<IServyLogger>();
            _ctx.Helper.Setup(h => h.GetArgs()).Returns(new[] { "servy.exe" });
            _ctx.Helper.Setup(h => h.ParseOptions(_ctx.ServiceRepository.Object, It.IsAny<string[]>())).Returns(options);
            _ctx.Logger.Setup(l => l.CreateScoped(options.ServiceName)).Returns(scopedLogger.Object);
            _ctx.Helper.Setup(h => h.ValidateAndLog(options, scopedLogger.Object)).Returns(true);
            _ctx.PathValidator.Setup(v => v.IsValidPath(It.IsAny<string>())).Returns(true);

            service = BuildService(handle);
            return scopedLogger;
        }

        /// <summary>
        /// Arranges a thread-safe signal reporting whether the given logging call has arrived. The
        /// delayed PRESHUTDOWN registration writes its log from a background task, so a test cannot
        /// verify the call the moment <c>OnStart</c> returns.
        /// </summary>
        /// <param name="logger">The logger mock the background task writes to.</param>
        /// <param name="call">The logging call to wait for.</param>
        /// <returns>A predicate reporting whether the call has been made.</returns>
        private static Func<bool> ArrangeLogSignal(Mock<IServyLogger> logger, Expression<Action<IServyLogger>> call)
        {
            var seen = 0;
            logger.Setup(call).Callback(() => Interlocked.Exchange(ref seen, 1));
            return () => Volatile.Read(ref seen) == 1;
        }

        /// <summary>Waits for the delayed PRESHUTDOWN registration to reach the fake seam.</summary>
        /// <returns><see langword="true"/> when a status call arrived within the timeout.</returns>
        private bool WaitForStatusCall()
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(SetServiceStatusPollTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (_scm.StatusCalls.Count > 0)
                {
                    return true;
                }

                Thread.Sleep(25);
            }

            return _scm.StatusCalls.Count > 0;
        }

        /// <summary>Disposes every service this fixture built.</summary>
        public void Dispose()
        {
            foreach (var service in _built)
            {
                service.Dispose();
            }

            _built.Clear();
            _ctx.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Test doubles

        /// <summary>
        /// A <see cref="Service"/> whose native service handle is supplied by the test instead of by
        /// the Service Control Manager.
        /// </summary>
        private sealed class HandleStubService : Service
        {
            private readonly IntPtr _handle;

            /// <summary>Initializes a new instance wired to the fake seam.</summary>
            /// <param name="serviceHelper">The service helper.</param>
            /// <param name="logger">The logger wrapper.</param>
            /// <param name="streamWriterFactory">The stream writer factory.</param>
            /// <param name="timerFactory">The timer factory.</param>
            /// <param name="processFactory">The process factory.</param>
            /// <param name="pathValidator">The path validator.</param>
            /// <param name="serviceRepository">The service repository.</param>
            /// <param name="scmNative">The native seam to observe.</param>
            /// <param name="handle">The handle <see cref="GetNativeServiceHandle"/> returns.</param>
            public HandleStubService(
                IServiceHelper serviceHelper,
                IServyLogger logger,
                IStreamWriterFactory streamWriterFactory,
                ITimerFactory timerFactory,
                IProcessFactory processFactory,
                IPathValidator pathValidator,
                IServiceRepository serviceRepository,
                IScmNative scmNative,
                IntPtr handle)
                : base(serviceHelper, logger, streamWriterFactory, timerFactory, processFactory, pathValidator, serviceRepository, scmNative)
            {
                _handle = handle;
            }

            /// <summary>Returns the handle the test supplied.</summary>
            /// <returns>The configured handle.</returns>
            protected override IntPtr GetNativeServiceHandle() => _handle;
        }

        /// <summary>
        /// Records what <see cref="Service"/> sends to the Service Control Manager, and lets a test
        /// steer the outcome of the native status call.
        /// </summary>
        private sealed class FakeScmNative : IScmNative
        {
            private readonly object _sync = new object();
            private readonly List<NativeMethods.SERVICE_STATUS> _statusCalls = new List<NativeMethods.SERVICE_STATUS>();

            /// <summary>Gets the number of times <see cref="DetachConsole"/> was called.</summary>
            public int DetachConsoleCalls { get; private set; }

            /// <summary>Gets or sets the value <see cref="SetServiceStatus"/> returns.</summary>
            public bool SetServiceStatusResult { get; set; } = true;

            /// <summary>Gets or sets the value <see cref="GetLastWin32Error"/> returns.</summary>
            public int LastWin32Error { get; set; }

            /// <summary>
            /// Gets or sets the exception <see cref="SetServiceStatus"/> throws once it has recorded
            /// the call. <see langword="null"/> leaves the call returning
            /// <see cref="SetServiceStatusResult"/>.
            /// </summary>
            public Exception SetServiceStatusThrows { get; set; }

            /// <summary>Gets the handle of the most recent status call.</summary>
            public IntPtr LastHandle { get; private set; }

            /// <summary>Gets the status structures published so far, in call order.</summary>
            public IReadOnlyList<NativeMethods.SERVICE_STATUS> StatusCalls
            {
                get
                {
                    lock (_sync)
                    {
                        return _statusCalls.ToList();
                    }
                }
            }

            /// <summary>Counts the call instead of detaching the test host's console.</summary>
            public void DetachConsole() => DetachConsoleCalls++;

            /// <summary>Records the status instead of calling the SCM.</summary>
            /// <param name="handle">The handle the service reported against.</param>
            /// <param name="status">The status structure the service published.</param>
            /// <returns><see cref="SetServiceStatusResult"/>.</returns>
            /// <exception cref="Exception">The exception <see cref="SetServiceStatusThrows"/> carries, when one is set.</exception>
            public bool SetServiceStatus(IntPtr handle, ref NativeMethods.SERVICE_STATUS status)
            {
                lock (_sync)
                {
                    LastHandle = handle;
                    _statusCalls.Add(status);
                }

                if (SetServiceStatusThrows != null)
                {
                    throw SetServiceStatusThrows;
                }

                return SetServiceStatusResult;
            }

            /// <summary>Returns the error code the test configured.</summary>
            /// <returns><see cref="LastWin32Error"/>.</returns>
            public int GetLastWin32Error() => LastWin32Error;
        }

        #endregion
    }
}
