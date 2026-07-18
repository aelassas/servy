using Servy.Service.Timers;
using System;
using System.Timers;
using Xunit;

namespace Servy.Service.UnitTests.Timers
{
    public class TimerAdapterTests
    {
        private const double Interval = 100.0;

        #region Initialization & Properties

        [Fact]
        public void Constructor_InitializesCorrectly()
        {
            using (var timer = new TimerAdapter(Interval))
            {
                Assert.NotNull(timer);
            }
        }

        [Fact]
        public void AutoReset_GetSet_WorksCorrectly()
        {
            using (var timer = new TimerAdapter(Interval))
            {
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
            var timer = new TimerAdapter(Interval);

            // First dispose
            timer.Dispose();

            // Second dispose should not throw
            var exception = Record.Exception(() => timer.Dispose());
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
            var timer = new TimerAdapter(Interval);
            timer.Dispose();

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
            using (var timer = new TimerAdapter(Interval))
            {
                // Simple functional smoke test
                var exceptionStart = Record.Exception(() => timer.Start());
                Assert.Null(exceptionStart);

                var exceptionStop = Record.Exception(() => timer.Stop());
                Assert.Null(exceptionStop);
            }
        }

        [Fact]
        public void Elapsed_EventSubscription_Works()
        {
            using (var timer = new TimerAdapter(Interval))
            {
                bool eventRaised = false;
                ElapsedEventHandler handler = (s, e) => eventRaised = true;

                // Subscribe
                timer.Elapsed += handler;

                // Unsubscribe
                timer.Elapsed -= handler;

                // If we reached here without ObjectDisposedException or NullReferenceException,
                // the subscription logic is correctly delegated.
                Assert.False(eventRaised);
            }
        }

        #endregion
    }
}