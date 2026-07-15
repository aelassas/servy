using Servy.Service.Timers;

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
                Assert.IsType<TimerAdapter>(result);
            }
        }
    }
}