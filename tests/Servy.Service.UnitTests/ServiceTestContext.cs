using Moq;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Service.Helpers;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;

namespace Servy.Service.UnitTests
{
    /// <summary>
    /// Holds the mocked dependencies of <see cref="TestableService"/> and builds instances wired to them.
    /// </summary>
    public class ServiceTestContext
    {
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
            return new TestableService(
                Helper.Object,
                Logger.Object,
                StreamWriterFactory.Object,
                TimerFactory.Object,
                ProcessFactory.Object,
                PathValidator.Object,
                ServiceRepository.Object,
                ProcessKiller.Object
            );
        }

        /// <summary>
        /// Creates a standard <see cref="Service"/> wired to this context's mocks.
        /// </summary>
        public Service BuildService(IServiceRepository serviceRepository = null, IServyLogger logger = null)
        {
            return new Service(
                Helper.Object,
                logger ?? Logger.Object,
                StreamWriterFactory.Object,
                TimerFactory.Object,
                ProcessFactory.Object,
                PathValidator.Object,
                serviceRepository ?? ServiceRepository.Object,
                ProcessKiller.Object
            );
        }
    }
}
