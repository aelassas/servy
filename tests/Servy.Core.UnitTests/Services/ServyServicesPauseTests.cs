using Moq;
using Servy.Core.Helpers;
using Servy.Core.Services;

namespace Servy.Core.UnitTests.Services
{
    /// <summary>
    /// Unit tests for <see cref="ServyServicesPause"/>: one stop of the running Servy services and the host before an
    /// update, one start after it, and never a stop of a service that only borrows the host's name.
    /// </summary>
    public class ServyServicesPauseTests
    {
        private readonly Mock<IServiceHelper> _serviceHelper = new Mock<IServiceHelper>();
        private readonly Mock<IServyHostInstaller> _host = new Mock<IServyHostInstaller>();
        private readonly List<string> _calls = new List<string>();

        public ServyServicesPauseTests()
        {
            _host.Setup(h => h.GetRunningServyServices(_serviceHelper.Object)).Returns(() => new List<string> { "A", "B" });
            _host.Setup(h => h.GetState()).Returns(ServyHostServiceState.ServyHost);
            _host.Setup(h => h.StopAsync(It.IsAny<CancellationToken>())).Callback(() => _calls.Add("stop host")).Returns(Task.CompletedTask);
            _host.Setup(h => h.StartAsync(It.IsAny<CancellationToken>())).Callback(() => _calls.Add("start host")).Returns(Task.CompletedTask);
            _serviceHelper.Setup(s => s.StopServicesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<string>, CancellationToken>((names, ct) => _calls.Add("stop " + string.Join("+", names)))
                .Returns(Task.CompletedTask);
            _serviceHelper.Setup(s => s.StartServicesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<string>, CancellationToken>((names, ct) => _calls.Add("start " + string.Join("+", names)))
                .Returns(Task.CompletedTask);
        }

        private ServyServicesPause Create() => new ServyServicesPause(_serviceHelper.Object, _host.Object);

        [Fact]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ServyServicesPause(null!, _host.Object));
            Assert.Throws<ArgumentNullException>(() => new ServyServicesPause(_serviceHelper.Object, null!));
        }

        [Fact]
        public async Task PauseThenResume_StopsTheServicesThenTheHostOnceAndStartsThemOnce()
        {
            // Arrange
            var pause = Create();

            // Act: several updates ask for the pause; only the first one stops anything
            await pause.PauseAsync(TestContext.Current.CancellationToken);
            await pause.PauseAsync(TestContext.Current.CancellationToken);
            Assert.True(pause.IsPaused);
            await pause.ResumeAsync();
            await pause.ResumeAsync();

            // Assert
            Assert.Equal(new[] { "stop A+B", "stop host", "start host", "start A+B" }, _calls);
            Assert.False(pause.IsPaused);
            Assert.Equal(new[] { "A", "B" }, pause.StoppedServices);
        }

        [Fact]
        public async Task Resume_WithoutPause_DoesNothing()
        {
            await Create().ResumeAsync();

            Assert.Empty(_calls);
        }

        [Theory]
        [InlineData(ServyHostServiceState.Foreign)]
        [InlineData(ServyHostServiceState.Unknown)]
        [InlineData(ServyHostServiceState.NotInstalled)]
        public async Task Pause_NameNotHeldByTheServyHost_NeverStopsOrStartsThatService(ServyHostServiceState state)
        {
            // Arrange: a service that only borrows the name is not Servy's to stop (#7294)
            _host.Setup(h => h.GetState()).Returns(state);
            var pause = Create();

            // Act
            await pause.PauseAsync(TestContext.Current.CancellationToken);
            await pause.ResumeAsync();

            // Assert
            Assert.Equal(new[] { "stop A+B", "start A+B" }, _calls);
        }

        [Fact]
        public async Task Pause_NoServiceRunning_StopsOnlyTheHost()
        {
            // Arrange
            _host.Setup(h => h.GetRunningServyServices(_serviceHelper.Object)).Returns(new List<string>());
            var pause = Create();

            // Act
            await pause.PauseAsync(TestContext.Current.CancellationToken);
            await pause.ResumeAsync();

            // Assert
            Assert.Equal(new[] { "stop host", "start host" }, _calls);
        }

        [Fact]
        public async Task Resume_HostOrServicesFailToStart_IsLoggedAndDoesNotThrow()
        {
            // Arrange
            _host.Setup(h => h.StartAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("host down"));
            _serviceHelper.Setup(s => s.StartServicesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new AggregateException(new InvalidOperationException("A did not start")));
            var pause = Create();
            await pause.PauseAsync(TestContext.Current.CancellationToken);

            // Act
            var ex = await Record.ExceptionAsync(() => pause.ResumeAsync());

            // Assert: the services are still asked to start after the host failed
            Assert.Null(ex);
            _serviceHelper.Verify(s => s.StartServicesAsync(It.Is<IEnumerable<string>>(n => n.SequenceEqual(new[] { "A", "B" })), CancellationToken.None), Times.Once);
        }
    }
}
