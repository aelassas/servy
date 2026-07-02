using Moq;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;

namespace Servy.Service.UnitTests.Utilities
{
    /// <summary>
    /// Establishes an isolated mock profile and instantiation context for configuring <see cref="TestableService"/> topologies.
    /// </summary>
    public class ServiceTestContext
    {
        public Mock<IServyLogger> Logger { get; } = new Mock<IServyLogger>();
        public Mock<IServiceHelper> Helper { get; } = new Mock<IServiceHelper>();
        public Mock<IStreamWriterFactory> StreamWriterFactory { get; } = new Mock<IStreamWriterFactory>();
        public Mock<ITimerFactory> TimerFactory { get; } = new Mock<ITimerFactory>();
        public Mock<IProcessFactory> ProcessFactory { get; } = new Mock<IProcessFactory>();
        public Mock<IPathValidator> PathValidator { get; } = new Mock<IPathValidator>();
        public Mock<IServiceRepository> ServiceRepository { get; } = new Mock<IServiceRepository>();

        public ServiceTestContext()
        {
            // Set up universally accurate base paths to prevent standard configuration failures
            PathValidator.Setup(p => p.IsValidPath(It.IsAny<string>())).Returns(true);
        }

        /// <summary>
        /// Builds a new <see cref="TestableService"/> bounded context utilizing the internal mock signatures.
        /// </summary>
        public TestableService Build(Core.Helpers.IProcessKiller processKiller)
        {
            return new TestableService(
                Helper.Object,
                Logger.Object,
                StreamWriterFactory.Object,
                TimerFactory.Object,
                ProcessFactory.Object,
                PathValidator.Object,
                ServiceRepository.Object,
                processKiller
            );
        }
    }
}