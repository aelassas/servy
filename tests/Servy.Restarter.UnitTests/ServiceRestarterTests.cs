using Moq;
using Servy.Testing;
using System.ComponentModel;
using System.ServiceProcess;

namespace Servy.Restarter.UnitTests
{
    public class ServiceRestarterTests
    {
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
            _restarter.RestartService("MyService", TestTimeouts.ServiceRestarterRestartTimeout);

            // Assert
            // 1. First Refresh happens inside the while loop to move from StopPending -> Stopped
            // 2. Second Refresh happens explicitly right before the Start phase check
            _mockController.Verify(c => c.Refresh(), Times.Exactly(2));

            // Ensure it skipped the stop command because the settle loop left it in a 'Stopped' state
            _mockController.Verify(c => c.Stop(), Times.Never);

            // Ensure it successfully proceeded to issue the start command
            _mockController.Verify(c => c.Start(), Times.Once);
            _mockController.Verify(c => c.Dispose(), Times.Once);
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
                _restarter.RestartService("MyService", TestTimeouts.ServiceRestarterStuckInPendingStateTimeout));

            Assert.Contains($"stuck in {pendingState} state", ex.Message);

            // Ensure controller is cleanly disposed of even if the service is stuck in a pending block lifecycle phase
            _mockController.Verify(c => c.Dispose(), Times.Once);
        }

        #endregion

        #region Phase 2: Stop Step Boundaries

        [Fact]
        public void RestartService_StopIssuedButNoTimeToAwaitStopped_ThrowsTimeoutException()
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
            _mockController.Verify(c => c.Stop(), Times.Once); // Stop is issued before the time check

            // Verify context handle cleanup rules execute on immediate stop timeouts
            _mockController.Verify(c => c.Dispose(), Times.Once);
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
            _restarter.RestartService("MyService", TestTimeouts.ServiceRestarterRestartTimeout);

            // Assert
            _mockController.Verify(c => c.Stop(), Times.Exactly(2)); // Primary check call + Fallback step execution call
            _mockController.Verify(c => c.Start(), Times.Once); // Continues cleanly to start phase
            _mockController.Verify(c => c.Dispose(), Times.Once);
        }

        #endregion

        #region Phase 3: Start Step Boundaries

        [Fact]
        public void RestartService_TimeoutExpiresAfterStartIssued_ThrowsTimeoutException()
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

            // Ensure start timeouts pass the object disposal verification check
            _mockController.Verify(c => c.Dispose(), Times.Once);
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
            _restarter.RestartService("MyService", TestTimeouts.ServiceRestarterRestartTimeout);

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
                .Returns(ServiceControllerStatus.Stopped)  // 1. Settle loop sanity check
                .Returns(ServiceControllerStatus.Stopped)  // 2. Stop-phase completion check
                .Returns(ServiceControllerStatus.Stopped)  // 3. Pre-Start execution entry check
                .Returns(ServiceControllerStatus.Running); // 4. HandleTransitionalError: First dynamic loop refresh evaluates true

            // Trigger an initial transitional error state to bounce execution into the handler
            _mockController.Setup(c => c.Start()).Throws<InvalidOperationException>();

            // Act
            _restarter.RestartService("MyService", TestTimeouts.ServiceRestarterRestartTimeout);

            // Assert
            // Verify that only the single, outer lifecycle start command was issued.
            // If an off-by-one status check failure happens, the handler loop will execute a secondary retry pass.
            _mockController.Verify(c => c.Start(), Times.Once,
                "The transitional error loop executed a secondary retry loop pass instead of exiting early on the first successful refresh status validation.");

            // Verify the sequence consumes exactly 2 structural refresh calls (1 during the start attempt phase, 1 inside the handler)
            _mockController.Verify(c => c.Refresh(), Times.Exactly(2),
                "The internal refresh orchestration layout does not align with the single-iteration early-return pattern profile.");

            _mockController.Verify(c => c.WaitForStatus(It.IsAny<ServiceControllerStatus>(), It.IsAny<TimeSpan>()), Times.Never);
            _mockController.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public void HandleTransitionalError_RemainingTimeExpiresInsideLoop_ThrowsTimeoutException()
        {
            // Arrange
            bool hasSlept = false;

            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Running)      // Step 1: Initial sanity check
                .Returns(ServiceControllerStatus.Running)      // Step 2: Pre-Stop execution check
                .Returns(ServiceControllerStatus.StopPending)  // Step 3: Loop entry evaluation pass
                .Returns(() =>
                {
                    if (!hasSlept)
                    {
                        // Deliberately burn the remaining timeout budget inside the loop body
                        // right after Refresh() is called, forcing the next evaluation loop check to fail.
                        Thread.Sleep(60);
                        hasSlept = true;
                    }
                    return ServiceControllerStatus.StopPending;
                });

            // Throw the transitional exception immediately to force entry into HandleTransitionalError
            _mockController.Setup(c => c.Stop()).Throws<InvalidOperationException>();

            // Act & Assert
            // We pass a 50ms budget; the initial actions happen instantly, then the loop body burns 60ms
            var ex = Assert.Throws<System.TimeoutException>(() =>
                _restarter.RestartService("MyService", TimeSpan.FromMilliseconds(50)));

            // Assert
            Assert.Contains("failed to reach Stopped within the timeout period", ex.Message);

            // This will now pass cleanly because code execution is forced to traverse the loop body 
            // to hit the sleeping Status lambda sequence.
            _mockController.Verify(c => c.Refresh(), Times.AtLeastOnce(),
                "The transitional error loop condition was short-circuited; code execution failed to traverse internal mid-loop monitoring steps.");

            // Ensure deep mid-loop timeout evaluation passes don't drop handles
            _mockController.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public void HandleTransitionalError_StopAndRefreshKeepThrowing_LoopsAndTimesOut()
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
                _restarter.RestartService("MyService", TestTimeouts.ServiceRestarterHandleTransitionalErrorTimeout));

            Assert.Contains("failed to reach Stopped within the timeout period", ex.Message);

            // Verify teardown routines hit even under cascading exception loop conditions
            _mockController.Verify(c => c.Dispose(), Times.Once);
        }

        #endregion

        #region Disappearance Guard Tests

        [Fact]
        public void RestartService_StatusThrowsInSettleLoop_ReturnsCleanlyWithoutStopOrStart()
        {
            // Arrange
            _mockController.Setup(c => c.Status).Throws<InvalidOperationException>();

            // Act
            var ex = Record.Exception(() => _restarter.RestartService("MyService", TestTimeouts.ServiceRestarterRestartTimeout));

            // Assert
            Assert.Null(ex);
            _mockController.Verify(c => c.Stop(), Times.Never);
            _mockController.Verify(c => c.Start(), Times.Never);
            _mockController.Verify(c => c.Dispose(), Times.Once);
        }

        [Theory]
        [InlineData(true)]  // Test InvalidOperationException path
        [InlineData(false)] // Test Win32Exception path
        public void RestartService_RefreshThrowsInSettleLoop_ReturnsCleanlyWithoutStopOrStart(bool throwInvalidOperation)
        {
            // Arrange
            _mockController.Setup(c => c.Status).Returns(ServiceControllerStatus.StopPending);

            if (throwInvalidOperation)
            {
                _mockController.Setup(c => c.Refresh()).Throws<InvalidOperationException>();
            }
            else
            {
                _mockController.Setup(c => c.Refresh()).Throws<Win32Exception>();
            }

            // Act
            var ex = Record.Exception(() => _restarter.RestartService("MyService", TestTimeouts.ServiceRestarterRestartTimeout));

            // Assert
            Assert.Null(ex);
            _mockController.Verify(c => c.Stop(), Times.Never);
            _mockController.Verify(c => c.Start(), Times.Never);
            _mockController.Verify(c => c.Dispose(), Times.Once);
        }

        [Theory]
        [InlineData(true)]  // Test InvalidOperationException path
        [InlineData(false)] // Test Win32Exception path
        public void RestartService_RefreshThrowsInStartPhase_ReturnsCleanlyWithoutStartCommand(bool throwInvalidOperation)
        {
            // Arrange
            // We bypass the Settle loop by returning Stopped instantly, which also skips the Stop phase.
            _mockController.SetupSequence(c => c.Status)
                .Returns(ServiceControllerStatus.Stopped)  // Settle loop entry
                .Returns(ServiceControllerStatus.Stopped); // Stop phase skip-check

            if (throwInvalidOperation)
            {
                _mockController.Setup(c => c.Refresh()).Throws<InvalidOperationException>();
            }
            else
            {
                _mockController.Setup(c => c.Refresh()).Throws<Win32Exception>();
            }

            // Act
            var ex = Record.Exception(() => _restarter.RestartService("MyService", TestTimeouts.ServiceRestarterRestartTimeout));

            // Assert
            Assert.Null(ex);
            _mockController.Verify(c => c.Start(), Times.Never);
            _mockController.Verify(c => c.Dispose(), Times.Once);
        }

        #endregion
    }
}