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
    /// Holds the eight mocked dependencies of <see cref="TestableService"/> and builds instances wired to them.
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
        public Mock<Core.Helpers.IProcessKiller> ProcessKiller { get; } = new Mock<Core.Helpers.IProcessKiller>();

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
    }
}
