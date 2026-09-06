using Servy.Service.Timers;
using Servy.Testing;
using System;
using Xunit;

namespace Servy.Service.UnitTests.Timers
{
    public class TimerFactoryTests
    {
        [Fact]
        public void Create_ReturnsNewTimerAdapterInstance()
        {
            // Arrange
            var factory = new TimerFactory();
            double interval = 1000.0;

            // Act & Assert
            using (var result = factory.Create(interval))
            using (var second = factory.Create(interval))
            {
                var adapter = Assert.IsType<TimerAdapter>(result);
                var inner = TestReflection.GetField<System.Timers.Timer>(adapter, "_timer");
                Assert.Equal(interval, inner.Interval);

                // The "New" the name leads with: Service.SetupHealthMonitoring creates a timer per
                // start and disposes it on stop, so a factory that cached one shared adapter would
                // let one service's stop tear down another's heartbeat.
                Assert.IsType<TimerAdapter>(second);
                Assert.NotSame(result, second);
            }
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.MaxValue)] // beyond int.MaxValue milliseconds
        public void Create_InvalidInterval_Throws(double interval)
        {
            // Arrange
            var factory = new TimerFactory();

            // Act & Assert
            // The rejection lives in System.Timers.Timer's constructor, which TimerAdapter passes
            // the value straight to; pinning it here documents where an invalid interval is caught.
            Assert.ThrowsAny<ArgumentException>(() => factory.Create(interval));
        }
    }
}
