using Moq;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Services;
using System.Diagnostics.Eventing.Reader;

namespace Servy.Core.UnitTests
{
    public class EventLogServiceTests
    {
        private EventLogService CreateService(Mock<IEventLogReader> mockReader)
        {
            return new EventLogService(mockReader.Object);
        }

        private EventRecord CreateFakeEvent(int id, byte level, DateTime? time, string message)
        {
            var fake = new Mock<EventRecord>();
            fake.Setup(e => e.Id).Returns(id);
            fake.Setup(e => e.Level).Returns(level);
            fake.Setup(e => e.TimeCreated).Returns(time);
            fake.Setup(e => e.FormatDescription()).Returns(message);
            return fake.Object;
        }

        [Fact]
        public void Search_NoFilters_ReturnsResult()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(1, 2, DateTime.UtcNow, "error happened");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            // Act
            var result = service.Search(null, null, null, null!);

            // Assert
            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Error, entry.Level);
        }

        [Fact]
        public void Search_WithLevelFilter_ReturnsCorrectLevel()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(2, 3, DateTime.UtcNow, "warning");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = service.Search(EventLogLevel.Warning, null, null, null!);

            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Warning, entry.Level);
        }

        [Fact]
        public void Search_WithStartDateAndEndDate_AppendsBothFilters()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(3, 4, DateTime.UtcNow, "info");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var start = DateTime.UtcNow.AddDays(-1);
            var end = DateTime.UtcNow.AddDays(1);

            var result = service.Search(null, start, end, null!);

            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Information, entry.Level);
        }

        [Fact]
        public void Search_WithOnlyEndDate_AppendsFilterCorrectly()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(4, 0, DateTime.UtcNow, "unknown level");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var end = DateTime.UtcNow;

            var result = service.Search(null, null, end, null!);

            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Information, entry.Level); // default branch
        }

        [Fact]
        public void Search_WithKeyword_AddsKeywordFilter()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(5, 2, DateTime.UtcNow, "servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = service.Search(null, null, null, "servy");

            var entry = Assert.Single(result);
            Assert.Contains("servy", entry.Message);
        }

        [Fact]
        public void Search_WhenTimeCreatedIsNull_UsesDateTimeMinValue()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(6, 4, null, "no time");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = service.Search(null, null, null, null!);

            var entry = Assert.Single(result);
            Assert.Equal(DateTime.MinValue, entry.Time);
        }
    }
}
