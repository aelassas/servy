using Servy.Service.Timers;
using Servy.Testing;
using System;
using System.Threading;
using System.Timers;
using Xunit;

namespace Servy.Service.UnitTests.Timers
{
    public class TimerAdapterTests
    {
        private const double Interval = 100.0;

        #region Initialization & Properties

        [Fact]
        public void Constructor_InitializesInternalTimerWithCorrectInterval()
        {
            // Arrange & Act
            using (var timer = new TimerAdapter(Interval))
            {
                // Assert
                // Verify that the adapter's backing field dependency is configured with the expected runtime interval
                var internalTimer = TestReflection.GetField<System.Timers.Timer>(timer, "_timer");

                Assert.NotNull(internalTimer);
                Assert.Equal(Interval, internalTimer.Interval);
            }
        }

        [Fact]
        public void AutoReset_GetSet_WorksCorrectly()
        {
            // Arrange
            using (var timer = new TimerAdapter(Interval))
            {
                // Act & Assert
                timer.AutoReset = true;
                Assert.True(timer.AutoReset);

                timer.AutoReset = false;
                Assert.False(timer.AutoReset);
            }
        }

        #endregion

        #region Disposal Logic & Guard Clauses

        [Fact]
        public void Dispose_IsIdempotent()
        {
            // Arrange
            var timer = new TimerAdapter(Interval);

            // Act
            // First dispose
            timer.Dispose();

            // Second dispose should not throw
            var exception = Record.Exception(() => timer.Dispose());

            // Assert
            Assert.Null(exception);
        }

        [Theory]
        [InlineData("Start")]
        [InlineData("Stop")]
        [InlineData("AutoResetGetter")]
        [InlineData("AutoResetSetter")]
        [InlineData("ElapsedAdder")]
        [InlineData("ElapsedRemover")]
        public void Member_WhenDisposed_ThrowsObjectDisposedException(string member)
        {
            // Arrange
            var timer = new TimerAdapter(Interval);
            timer.Dispose();

            // Act & Assert
            switch (member)
            {
                case "Start":
                    Assert.Throws<ObjectDisposedException>(() => timer.Start());
                    break;
                case "Stop":
                    Assert.Throws<ObjectDisposedException>(() => timer.Stop());
                    break;
                case "AutoResetGetter":
                    Assert.Throws<ObjectDisposedException>(() => { var x = timer.AutoReset; });
                    break;
                case "AutoResetSetter":
                    Assert.Throws<ObjectDisposedException>(() => timer.AutoReset = true);
                    break;
                case "ElapsedAdder":
                    Assert.Throws<ObjectDisposedException>(() => timer.Elapsed += (s, e) => { });
                    break;
                case "ElapsedRemover":
                    Assert.Throws<ObjectDisposedException>(() => timer.Elapsed -= (s, e) => { });
                    break;
                default:
                    Assert.Fail($"Unhandled member name '{member}' in test case. Please ensure the test data is up to date with the TimerAdapter methods.");
                    break;
            }
        }

        #endregion

        #region Operational Tests

        [Fact]
        public void Start_And_Stop_DoNotThrowExceptions()
        {
            // Arrange
            using (var timer = new TimerAdapter(Interval))
            {
                // Act
                // Simple functional smoke test
                var exceptionStart = Record.Exception(() => timer.Start());
                var exceptionStop = Record.Exception(() => timer.Stop());

                // Assert
                Assert.Null(exceptionStart);
                Assert.Null(exceptionStop);
            }
        }

        [Fact]
        public void Elapsed_EventAddition_DelegatesToUnderlyingTimer()
        {
            // Arrange
            const double TestInterval = 10.0;
            using (var timer = new TimerAdapter(TestInterval))
            using (var resetEvent = new ManualResetEventSlim(false))
            {
                bool eventRaised = false;
                ElapsedEventHandler handler = (s, e) =>
                {
                    eventRaised = true;
                    resetEvent.Set();
                };

                // Act
                timer.Elapsed += handler;
                timer.AutoReset = false;
                timer.Start();

                // Wait up to a generous 1 second for the background thread pool dispatch signal
                bool signaled = resetEvent.Wait(1000, CancellationToken.None);
                timer.Stop();

                // Assert
                Assert.True(signaled, "The underlying timer event failed to fire within the allocated timeout.");
                Assert.True(eventRaised);
            }
        }

        [Fact]
        public void Elapsed_EventRemoval_UnsubscribesFromUnderlyingTimer()
        {
            // Arrange
            const double TestInterval = 10.0;
            using (var timer = new TimerAdapter(TestInterval))
            using (var resetEvent = new ManualResetEventSlim(false))
            {
                bool eventRaised = false;
                ElapsedEventHandler handler = (s, e) =>
                {
                    eventRaised = true;
                };

                // Act
                timer.Elapsed += handler;
                timer.Elapsed -= handler; // Immediately remove delegation path

                // Set up a second canary handler so we know exactly when the underlying timer ticked
                timer.Elapsed += (s, e) => resetEvent.Set();
                timer.AutoReset = false;
                timer.Start();

                bool signaled = resetEvent.Wait(1000, CancellationToken.None);
                timer.Stop();

                // Assert
                Assert.True(signaled, "The canary event tracking loop failed to fire.");
                Assert.False(eventRaised, "The handler was executed despite having been explicitly unsubscribed.");
            }
        }

        #endregion
    }
}