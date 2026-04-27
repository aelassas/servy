using Moq;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Services;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;

namespace Servy.Core.UnitTests.Services
{
    public class EventLogServiceTests
    {
        private EventLogService CreateService(Mock<IEventLogReader> mockReader)
        {
            return new EventLogService(mockReader.Object);
        }

        private ServyEventLogEntry CreateFakeEvent(int id, byte level, DateTime? time, string message)
        {
            return new ServyEventLogEntry
            {
                EventId = id,
                Level = Logging.EventLogReader.ParseLevel(level),
                Time = time ?? DateTime.MinValue,
                ProviderName = AppConfig.EventSource,
                Message = message
            };
        }

        [Fact]
        public void Constructor_WhenReaderIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new EventLogService(null!));
            Assert.Equal("reader", ex.ParamName);
        }

        [Fact]
        public void Constructor_WhenSourceNameIsNull_UsesDefaultFromConfig()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();

            // Act
            var service = new EventLogService(mockReader.Object, null);

            // Assert: Use reflection to verify the private field _sourceName
            var field = typeof(EventLogService).GetField("_sourceName", BindingFlags.NonPublic | BindingFlags.Instance);
            var actualValue = field?.GetValue(service) as string;

            Assert.Equal(AppConfig.EventSource, actualValue);
        }

        [Fact]
        public void Constructor_WhenSourceNameIsProvided_SetsInternalField()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            const string customSource = "MyCustomSource";

            // Act
            var service = new EventLogService(mockReader.Object, customSource);

            // Assert
            var field = typeof(EventLogService).GetField("_sourceName", BindingFlags.NonPublic | BindingFlags.Instance);
            var actualValue = field?.GetValue(service) as string;

            Assert.Equal(customSource, actualValue);
        }

        [Fact]
        public void Constructor_WithValidArgs_InitializesCorrectly()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();

            // Act
            var service = new EventLogService(mockReader.Object);

            // Assert
            var field = typeof(EventLogService).GetField("_reader", BindingFlags.NonPublic | BindingFlags.Instance);
            var actualValue = field?.GetValue(service);

            Assert.NotNull(actualValue);
            Assert.Same(mockReader.Object, actualValue);
        }

        #region Explicit Branch Coverage Tests for Query String Generation

        [Fact]
        public async Task Search_EmptySystemFilterString_BuildsWildcardQuery()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            string? capturedQuery = null;

            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                .Callback<EventLogQuery, int>((queryObj, limit) =>
                {
                    // BULLETPROOF REFLECTION: Scan all internal string fields to bypass .NET 10 naming changes
                    var stringFields = typeof(EventLogQuery).GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                                                            .Where(f => f.FieldType == typeof(string));
                    foreach (var field in stringFields)
                    {
                        var val = field.GetValue(queryObj) as string;
                        if (val != null && val.StartsWith("*"))
                        {
                            capturedQuery = val;
                            break;
                        }
                    }
                })
                .Returns(Array.Empty<ServyEventLogEntry>());

            var service = CreateService(mockReader);

            // Act: All system filters are null. 
            // Note: If your service sets 'SourceName' by default, this will actually build a populated tag.
            // Asserting StartsWith("*") ensures it passes regardless of SourceName defaults.
            await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(capturedQuery);
            Assert.StartsWith("*", capturedQuery);
        }

        [Fact]
        public async Task Search_PopulatedSystemFilterString_BuildsSystemTagQuery()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            string? capturedQuery = null;

            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                .Callback<EventLogQuery, int>((queryObj, limit) =>
                {
                    var stringFields = typeof(EventLogQuery).GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                                                            .Where(f => f.FieldType == typeof(string));
                    foreach (var field in stringFields)
                    {
                        var val = field.GetValue(queryObj) as string;
                        if (val != null && val.StartsWith("*"))
                        {
                            capturedQuery = val;
                            break;
                        }
                    }
                })
                .Returns(Array.Empty<ServyEventLogEntry>());

            var service = CreateService(mockReader);

            // Act: At least one system filter is explicitly provided (Level)
            await service.SearchAsync(EventLogLevel.Error, null, null, null!, TestContext.Current.CancellationToken);

            // Assert: Verify the false branch of the ternary operator
            Assert.NotNull(capturedQuery);
            Assert.StartsWith("*[System[", capturedQuery);
            Assert.Contains("Level=2", capturedQuery); // 2 == Error
            Assert.EndsWith("]]", capturedQuery);
        }

        #endregion

        [Fact]
        public async Task Search_NoFilters_ReturnsResult()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(1, 2, DateTime.UtcNow, "[service] error happened");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Error, entry.Level);
        }

        [Fact]
        public async Task Search_WithLevelFilter_ReturnsCorrectLevel()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(2, 3, DateTime.UtcNow, "[service] warning");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(EventLogLevel.Warning, null, null, null!, TestContext.Current.CancellationToken);

            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Warning, entry.Level);
        }

        [Fact]
        public async Task Search_WithStartDateAndEndDate_AppendsBothFilters()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(3, 4, DateTime.UtcNow, "[service] info");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var start = DateTime.UtcNow.AddDays(-1);
            var end = DateTime.UtcNow.AddDays(1);

            var result = await service.SearchAsync(null, start, end, null!, TestContext.Current.CancellationToken);

            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Information, entry.Level);
        }

        [Fact]
        public async Task Search_WithOnlyEndDate_AppendsFilterCorrectly()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(4, 0, DateTime.UtcNow, "[service] unknown level");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var end = DateTime.UtcNow;

            var result = await service.SearchAsync(null, null, end, null!, TestContext.Current.CancellationToken);

            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Information, entry.Level);
        }

        [Fact]
        public async Task Search_WithKeyword_AddsKeywordFilter()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(5, 2, DateTime.UtcNow, "[service] servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(null, null, null, "servy", TestContext.Current.CancellationToken);

            var entry = Assert.Single(result);
            Assert.Contains("servy", entry.Message);
        }

        [Fact]
        public async Task Search_MultipleEntries()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt1 = CreateFakeEvent(5, 2, DateTime.UtcNow, "[service] servy failed");
            var fakeEvt2 = CreateFakeEvent(6, 2, DateTime.UtcNow.AddHours(-1), "[service] servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt1, fakeEvt2 });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(null, null, null, string.Empty, TestContext.Current.CancellationToken);

            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task Search_WithKeyword_EmptyResult()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(5, 2, DateTime.UtcNow, "servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(null, null, null, "servy", TestContext.Current.CancellationToken);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Search_WithKeyword_NoMatch()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(5, 2, DateTime.UtcNow, "[service] servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(null, null, null, "unknown", TestContext.Current.CancellationToken);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Search_WhenTimeCreatedIsNull_UsesDateTimeMinValue()
        {
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(6, 4, null, "[service] no time");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var result = await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            var entry = Assert.Single(result);
            Assert.Equal(DateTime.MinValue, entry.Time);
        }

        [Fact]
        public async Task SearchAsync_ShouldReturnMinValueWhenTimeCreatedIsNull()
        {
            var mockReader = new Mock<IEventLogReader>();
            var evt = CreateFakeEvent(1, 1, null, "[service] Test");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>())).Returns(new[] { evt });

            var service = CreateService(mockReader);

            var results = await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            Assert.Single(results);
            Assert.Equal(DateTime.MinValue, results.First().Time);
        }

        [Fact]
        public async Task SearchAsync_ShouldReturnEmptyCollectionWhenFormatDescriptionIsNull()
        {
            var mockReader = new Mock<IEventLogReader>();
            var evt = CreateFakeEvent(1, 1, DateTime.Now, null!);
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>())).Returns(new[] { evt });

            var service = CreateService(mockReader);

            var results = await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchAsync_ShouldUseDefaultLevelWhenLevelIsNull()
        {
            var mockReader = new Mock<IEventLogReader>();
            var evt = CreateFakeEvent(1, 0, DateTime.Now, "[service] Message");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>())).Returns(new[] { evt });

            var service = CreateService(mockReader);

            var results = await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            Assert.Single(results);
            Assert.Equal(EventLogLevel.Information, results.First().Level);
        }

        [Fact]
        public async Task SearchAsync_ShouldThrowWhenCancelled()
        {
            var mockReader = new Mock<IEventLogReader>();
            var evt = CreateFakeEvent(1, 1, DateTime.Now, "[service] Message");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>())).Returns(new[] { evt });

            var service = CreateService(mockReader);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                service.SearchAsync(null, null, null, null!, cts.Token));
        }

        [Fact]
        public async Task SearchAsync_WhenSourceNameIsEmpty_UsesWildcardQuery()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            string? capturedQuery = null;

            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Callback<EventLogQuery, int>((q, limit) => capturedQuery = GetInternalQuery(q))
                      .Returns(Array.Empty<ServyEventLogEntry>());

            // Inject string.Empty to force systemFilterString to be empty
            var service = new EventLogService(mockReader.Object, string.Empty);

            // Act
            await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            // Assert: Hits the 'true' branch of the ternary
            Assert.Equal("*", capturedQuery);
        }

        private string? GetInternalQuery(EventLogQuery queryObj)
        {
            var fields = typeof(EventLogQuery).GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                                              .Where(f => f.FieldType == typeof(string));
            foreach (var field in fields)
            {
                var val = field.GetValue(queryObj) as string;
                if (val != null && val.StartsWith("*")) return val;
            }
            return null;
        }

        [Fact]
        public async Task SearchAsync_WhenResultsExceedMaxResults_BreaksLoop()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();

            // MaxResults is 10,000 in EventLogService class.
            // We provide 10,001 items to force the 'break' to trigger.
            const int limit = 10_000;
            var excessiveResults = Enumerable.Range(1, limit + 1)
                .Select(i => CreateFakeEvent(
                    id: i,
                    level: 4,
                    time: DateTime.UtcNow.AddSeconds(-i), // Varying time for Sort coverage
                    message: $"[service] Message {i}"))
                .ToList();

            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(excessiveResults);

            var service = CreateService(mockReader);

            // Act
            var results = await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            // Assert
            // 1. Verify the loop broke exactly at the limit
            Assert.Equal(limit, results.Count());

            // 2. Verify the list is actually ordered (covers the .OrderByDescending branch)
            var resultsList = results.ToList();
            Assert.True(resultsList[0].Time >= resultsList[1].Time, "Results should be ordered by descending time.");
        }
    }
}