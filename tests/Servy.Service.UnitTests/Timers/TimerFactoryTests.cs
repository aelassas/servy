using Servy.Service.Timers;
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

            // Act
            var result = factory.Create(interval);

            // Assert
            // Ensure the result is not null and is specifically the Adapter type
            Assert.NotNull(result);
            Assert.IsType<TimerAdapter>(result);
        }
    }
}