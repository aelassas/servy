using Servy.Service.Timers;
using Servy.Testing;

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
            {
                var adapter = Assert.IsType<TimerAdapter>(result);
                var inner = TestReflection.GetField<System.Timers.Timer>(adapter, "_timer");
                Assert.Equal(interval, inner.Interval);
            }
        }
    }
}