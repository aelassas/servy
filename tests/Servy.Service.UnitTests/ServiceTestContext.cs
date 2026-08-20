using Moq;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;

namespace Servy.Service.UnitTests
{
    /// <summary>
    /// Holds the mocked dependencies of <see cref="TestableService"/> and builds instances wired to them.
    /// Manages lifetime disposal of all built services.
    /// </summary>
    public class ServiceTestContext : IDisposable
    {
        private readonly List<IDisposable> _builtServices = new List<IDisposable>();

        public Mock<IServyLogger> Logger { get; set; } = new Mock<IServyLogger>();
        public Mock<IServiceHelper> Helper { get; set; } = new Mock<IServiceHelper>();
        public Mock<IStreamWriterFactory> StreamWriterFactory { get; set; } = new Mock<IStreamWriterFactory>();
        public Mock<ITimerFactory> TimerFactory { get; set; } = new Mock<ITimerFactory>();
        public Mock<IProcessFactory> ProcessFactory { get; set; } = new Mock<IProcessFactory>();
        public Mock<IPathValidator> PathValidator { get; set; } = new Mock<IPathValidator>();
        public Mock<IServiceRepository> ServiceRepository { get; set; } = new Mock<IServiceRepository>();
        public Mock<Core.Helpers.IProcessKiller> ProcessKiller { get; set; } = new Mock<Core.Helpers.IProcessKiller>();

        public ServiceTestContext()
        {
            // Paths validate by default; individual tests override IsValidPath to exercise failure branches.
            PathValidator.Setup(p => p.IsValidPath(It.IsAny<string>())).Returns(true);
        }

        /// <summary>
        /// Creates a <see cref="TestableService"/> wired to this context's mocks.
        /// </summary>
        public TestableService Build()
        {
            var service = new TestableService(
                Helper.Object,
                Logger.Object,
                StreamWriterFactory.Object,
                TimerFactory.Object,
                ProcessFactory.Object,
                PathValidator.Object,
                ServiceRepository.Object,
                ProcessKiller.Object
            );

            _builtServices.Add(service);
            return service;
        }

        /// <summary>
        /// Creates a standard <see cref="Service"/> wired to this context's mocks.
        /// </summary>
        public Service BuildService(IServiceRepository? serviceRepository = null, IServyLogger? logger = null)
        {
            var service = new Service(
                Helper.Object,
                logger ?? Logger.Object,
                StreamWriterFactory.Object,
                TimerFactory.Object,
                ProcessFactory.Object,
                PathValidator.Object,
                serviceRepository ?? ServiceRepository.Object,
                ProcessKiller.Object
            );

            _builtServices.Add(service);
            return service;
        }

        /// <summary>
        /// Creates a default <see cref="StartOptions"/> pre-configured with standard paths and heartbeat flags for unit tests.
        /// </summary>
        public static StartOptions CreateDefaultStartOptions(string heartbeatUrl = "https://127.0.0.1:1/test-uuid") => new StartOptions
        {
            StdoutPath = "valid-path.log",
            StderrPath = "error-path.log",
            RecoveryOnCleanExit = false,
            EnableHealthMonitoring = true,
            EnableHeartbeatUrlFlags = true,
            HeartbeatUrl = heartbeatUrl,
            HeartbeatUrlTimeoutInSeconds = 10,
            HeartbeatIntervalInSeconds = 30
        };

        /// <summary>
        /// Performs best-effort disposal of all SUT instances created via <see cref="Build"/> or <see cref="BuildService"/>.
        /// </summary>
        public void Dispose()
        {
            foreach (var service in _builtServices)
            {
                try
                {
                    service?.Dispose();
                }
                catch
                {
                    // Teardown is best-effort to prevent a failing SUT from blocking remaining disposals
                }
            }

            _builtServices.Clear();
        }
    }
}
