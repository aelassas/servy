using Moq;
using System.ServiceProcess;

namespace Servy.Restarter.UnitTests
{
    public class ServiceRestarterTests
    {
        private static readonly TimeSpan RestartTimeout = TimeSpan.FromSeconds(5);

        private readonly Mock<IServiceController> _mockController;
        private readonly ServiceRestarter _restarter;

        public ServiceRestarterTests()
        {
            _mockController = new Mock<IServiceController>();
            // Inject factory returning the mock controller
            _restarter = new ServiceRestarter(name => _mockController.Object);
        }

        #region Factory Initialization Tests

        [Fact]
        public void Constructor_NullFactory_FallbackToDefaultInstantiation()
        {
            // Act
            var defaultRestarter = new ServiceRestarter(null);

            // Assert
            Assert.NotNull(defaultRestarter);
            // Since it uses a real concrete ServiceController internally on invocation,
            // we verify it instantiates cleanly without throwing a NullReferenceException.
        }

        #endregion

        #region Phase 1: Settle / Initial Pending Loops

        [Fact]
        public void RestartService_InitialStateIsPending_LoopsAndSettles()
        {
            // Arrange
            // We simulate a real state machine tracking variable
            var currentStatus = ServiceControllerStatus.StopPending;

            _mockController.Setup(c => c.Status).Returns(() => currentStatus);

            // When Refresh is called, we simulate the SCM advancing the state from StopPending to Stopped
            _mockController.Setup(c => c.Refresh()).Callback(() =>
            {
                if (currentStatus == ServiceControllerStatus.StopPending)
                {
                    currentStatus = ServiceControllerStatus.Stopped;
                }
            });

            _mockController.Setup(c => c.Start()).Callback(() =>
            {
                currentStatus = ServiceControllerStatus.Running;
            });

            // Act
            _restarter.RestartService("MyService", RestartTimeout);

            // Assert
            // 1. First Refresh happens inside the while loop to move from StopPending -> Stopped
            // 2. Second Refresh happens explicitly right before the Start phase check
            _mockController.Verify(c => c.Refresh(), Times.Exactly(2));

            // Ensure it skipped the stop command because the settle loop left it in a 'Stopped' state
            _mockController.Verify(c => c.Stop(), Times.Never);

            // Ensure it successfully proceeded to issue the start command
            _mockController.Verify(c => c.Start(), Times.Once);
        }

        [Theory]
        [InlineData(ServiceControllerStatus.StartPending)]
        [InlineData(ServiceControllerStatus.StopPending)]
        [InlineData(ServiceControllerStatus.ContinuePending)]
        [InlineData(ServiceControllerStatus.PausePending)]
        public void RestartService_StuckInPendingState_ThrowsTimeoutException(ServiceControllerStatus pendingState)
        {
            // Arrange
            _mockController.Setup(c => c.Status).Returns(pendingState);

            // Act & Assert
            var ex = Assert.Throws<System.TimeoutException>(() =>
                _restarter.RestartService("MyService", TimeSpan.FromMilliseconds(1)));

            Assert.Contains($"stuck in {pendingState} state", ex.Message);
        }

        #endregion

        #region Phase 2: Stop Step Boundaries

        [Fact]
        public void RestartService_NoTimeRemainingToStop_ThrowsTimeoutException()
        {
            // Arrange
            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Running)  // Step 1: Passes pending check
                .Returns(ServiceControllerStatus.Running); // Step 2: Enters Stop block

            // Act & Assert
            // Force remaining time to evaluate to <= 0 immediately inside the phase execution by using zero timeout
            var ex = Assert.Throws<System.TimeoutException>(() =>
                _restarter.RestartService("MyService", TimeSpan.Zero));

            Assert.Contains("No time remaining to stop service", ex.Message);
        }

        [Fact]
        public void RestartService_StopThrowsInvalidOperationException_HandlesTransitionalErrorToStopped()
        {
            // Arrange
            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Running)       // Step 1: Passes pending check
                .Returns(ServiceControllerStatus.Running)       // Step 2: Enters Stop block
                .Returns(ServiceControllerStatus.StopPending)   // HandleTransitionalError: First Refresh check
                .Returns(ServiceControllerStatus.Stopped);      // HandleTransitionalError: Consequent Refresh loop exit

            _mockController.Setup(c => c.Stop()).Throws<InvalidOperationException>();

            // Act
            _restarter.RestartService("MyService", RestartTimeout);

            // Assert
            _mockController.Verify(c => c.Stop(), Times.Exactly(2)); // Primary check call + Fallback step execution call
            _mockController.Verify(c => c.Start(), Times.Once); // Continues cleanly to start phase
        }

        #endregion

        #region Phase 3: Start Step Boundaries

        [Fact]
        public void RestartService_NoTimeRemainingToStart_ThrowsTimeoutException()
        {
            // Arrange
            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Stopped)  // Step 1 check
                .Returns(ServiceControllerStatus.Stopped)  // Step 2 check (skips stop block)
                .Returns(ServiceControllerStatus.Stopped)  // Step 3 pre-start refresh state
                .Returns(ServiceControllerStatus.Stopped); // Step 3 check block entry

            // Act & Assert
            var ex = Assert.Throws<System.TimeoutException>(() =>
                _restarter.RestartService("MyService", TimeSpan.Zero));

            Assert.Contains("Timeout expired while waiting for service", ex.Message);
            _mockController.Verify(c => c.Start(), Times.Once());
        }

        [Fact]
        public void RestartService_StartThrowsInvalidOperationException_HandlesTransitionalErrorToRunning()
        {
            // Arrange
            var currentStatus = ServiceControllerStatus.Stopped;
            var startCallCount = 0;

            _mockController.Setup(c => c.Status).Returns(() => currentStatus);

            _mockController.Setup(c => c.Start()).Callback(() =>
            {
                startCallCount++;

                // 1. First call to Start() throws the transitional error block
                if (startCallCount == 1)
                {
                    throw new InvalidOperationException("Service is in a transitional lock state.");
                }

                // 2. Second call to Start() happens inside HandleTransitionalError
                // and transitions the mock to a pending state.
                if (startCallCount == 2)
                {
                    currentStatus = ServiceControllerStatus.StartPending;
                }
            });

            // Act
            _restarter.RestartService("MyService", RestartTimeout);

            // Assert
            // Verifies the structural recovery path:
            // Call 1: Outer block (Throws Exception)
            // Call 2: Inner HandleTransitionalError block (Succeeds)
            _mockController.Verify(c => c.Start(), Times.Exactly(2));

            // Verifies that HandleTransitionalError issues the final wait command 
            // to block until the transition to Running completes fully.
            _mockController.Verify(c => c.WaitForStatus(ServiceControllerStatus.Running, It.IsAny<TimeSpan>()), Times.Once);

            _mockController.Verify(c => c.Dispose(), Times.Once);
        }

        #endregion

        #region Internal: HandleTransitionalError Deep Exceptions Loops

        [Fact]
        public void HandleTransitionalError_TargetReachedOnFirstRefresh_ReturnsEarly()
        {
            // Arrange
            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Stopped)  // Step 1 check
                .Returns(ServiceControllerStatus.Stopped)  // Step 2 check
                .Returns(ServiceControllerStatus.Stopped)  // Step 3 pre-start refresh state
                .Returns(ServiceControllerStatus.Stopped)  // Step 3 check block entry
                .Returns(ServiceControllerStatus.Running); // HandleTransitionalError: First refresh evaluates true, loops return early

            _mockController.Setup(c => c.Start()).Throws<InvalidOperationException>();

            // Act
            _restarter.RestartService("MyService", RestartTimeout);

            // Assert
            _mockController.Verify(c => c.WaitForStatus(It.IsAny<ServiceControllerStatus>(), It.IsAny<TimeSpan>()), Times.Never);
        }

        [Fact]
        public void HandleTransitionalError_RemainingTimeExpiresInsideLoop_ThrowsTimeoutException()
        {
            // Arrange
            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Running)  // Step 1 check
                .Returns(ServiceControllerStatus.Running)  // Step 2 entry check
                .Returns(ServiceControllerStatus.StopPending); // HandleTransitionalError entry refresh state

            _mockController.Setup(c => c.Stop()).Throws<InvalidOperationException>();

            // Act & Assert
            // We force remaining time to drop below 0 immediately inside the fallback calculation loop via zero timeout
            var ex = Assert.Throws<System.TimeoutException>(() =>
                _restarter.RestartService("MyService", TimeSpan.Zero));

            Assert.Contains("failed to reach Stopped within the timeout period", ex.Message);
        }

        [Fact]
        public void HandleTransitionalError_LSAInterrogationContinuouslyThrowsInvalidOperationException_LoopsAndTimeouts()
        {
            // Arrange
            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Running)  // Step 1 check
                .Returns(ServiceControllerStatus.Running); // Step 2 check

            // First call throws to hit handler, then subsequent calls inside the handler also throw
            _mockController.Setup(c => c.Stop()).Throws<InvalidOperationException>();
            _mockController.Setup(c => c.Refresh()).Throws<InvalidOperationException>();

            // Act & Assert
            // The catch (InvalidOperationException) block matches, remaining time runs down, and it exits via loop break
            var ex = Assert.Throws<System.TimeoutException>(() =>
                _restarter.RestartService("MyService", TimeSpan.FromMilliseconds(10)));

            Assert.Contains("failed to reach Stopped within the timeout period", ex.Message);
        }

        #endregion
    }

    /// <summary>
    /// Defensive compiler bridge interface utility mapping
    /// </summary>
    internal static class SafeMatcher
    {
        public static bool IsAny<T>() => true;
    }
}