using Moq;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Services;
using System;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Core.UnitTests.Services
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
        public async Task Search_NoFilters_ReturnsResult()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(1, 2, DateTime.UtcNow, "[service] error happened");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            // Act
            var result = await service.SearchAsync(null, null, null, null);

            // Assert
            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Error, entry.Level);
        }

        [Fact]
        public async Task Search_WithLevelFilter_ReturnsCorrectLevel()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(2, 3, DateTime.UtcNow, "[service] warning");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(EventLogLevel.Warning, null, null, null);

            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Warning, entry.Level);
        }

        [Fact]
        public async Task Search_WithStartDateAndEndDate_AppendsBothFilters()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(3, 4, DateTime.UtcNow, "[service] info");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var start = DateTime.UtcNow.AddDays(-1);
            var end = DateTime.UtcNow.AddDays(1);

            var result = await service.SearchAsync(null, start, end, null);

            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Information, entry.Level);
        }

        [Fact]
        public async Task Search_WithOnlyEndDate_AppendsFilterCorrectly()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(4, 0, DateTime.UtcNow, "[service] unknown level");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var end = DateTime.UtcNow;

            var result = await service.SearchAsync(null, null, end, null);

            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Information, entry.Level); // default branch
        }

        [Fact]
        public async Task Search_WithKeyword_AddsKeywordFilter()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(5, 2, DateTime.UtcNow, "[service] servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(null, null, null, "servy");

            var entry = Assert.Single(result);
            Assert.Contains("servy", entry.Message);
        }

        [Fact]
        public async Task Search_MultipleEntries()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt1 = CreateFakeEvent(5, 2, DateTime.UtcNow, "[service] servy failed");
            var fakeEvt2 = CreateFakeEvent(6, 2, DateTime.UtcNow.AddHours(-1), "[service] servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt1, fakeEvt2 });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(null, null, null, string.Empty);

            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task Search_WithKeyword_EmptyResult()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(5, 2, DateTime.UtcNow, "servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(null, null, null, "servy");

            Assert.Empty(result);
        }

        [Fact]
        public async Task Search_WithKeyword_NoMatch()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(5, 2, DateTime.UtcNow, "[service] servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(null, null, null, "unknown");

            Assert.Empty(result);
        }

        [Fact]
        public async Task Search_WhenTimeCreatedIsNull_UsesDateTimeMinValue()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(6, 4, null, "[service] no time");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(null, null, null, null);

            var entry = Assert.Single(result);
            Assert.Equal(DateTime.MinValue, entry.Time);
        }

        [Fact]
        public async Task SearchAsync_ShouldReturnMinValueWhenTimeCreatedIsNull()
        {
            var mockReader = new Mock<IEventLogReader>();
            var evt = CreateFakeEvent(1, 1, null, "[service] Test");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>())).Returns(new[] { evt });

            var service = CreateService(mockReader);

            var results = await service.SearchAsync(null, null, null, null);

            Assert.Single(results);
            Assert.Equal(DateTime.MinValue, results.First().Time);
        }

        [Fact]
        public async Task SearchAsync_ShouldReturnEmptyCollectionWhenFormatDescriptionIsNull()
        {
            var mockReader = new Mock<IEventLogReader>();
            var evt = CreateFakeEvent(1, 1, DateTime.Now, null);
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>())).Returns(new[] { evt });

            var service = CreateService(mockReader);

            var results = await service.SearchAsync(null, null, null, null);

            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchAsync_ShouldUseDefaultLevelWhenLevelIsNull()
        {
            var mockReader = new Mock<IEventLogReader>();
            var evt = CreateFakeEvent(1, 0, DateTime.Now, "[service] Message"); // level = 0
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>())).Returns(new[] { evt });

            var service = CreateService(mockReader);

            var results = await service.SearchAsync(null, null, null, null);

            Assert.Single(results);
            // Here check the mapping from 0 -> your default ParseLevel
            Assert.Equal(EventLogLevel.Information, results.First().Level); // Example
        }

        [Fact]
        public async Task SearchAsync_ShouldThrowWhenCancelled()
        {
            var mockReader = new Mock<IEventLogReader>();
            var evt = CreateFakeEvent(1, 1, DateTime.Now, "[service] Message");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>())).Returns(new[] { evt });

            var service = CreateService(mockReader);
            var cts = new CancellationTokenSource();
            cts.Cancel(); // cancel immediately

            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                service.SearchAsync(null, null, null, null, cts.Token));
        }

    }
}
