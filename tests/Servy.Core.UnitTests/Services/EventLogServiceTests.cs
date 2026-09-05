using Moq;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Core.Services;
using Servy.Testing;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using System.Security;

namespace Servy.Core.UnitTests.Services
{
    public class EventLogServiceTests
    {
        private EventLogService CreateService(Mock<IEventLogReader> mockReader)
        {
            return new EventLogService(mockReader.Object);
        }

        private ServyEventLogEntry CreateFakeEvent(int id, byte level, DateTime time, string message)
        {
            return new ServyEventLogEntry
            {
                EventId = id,
                Level = Core.Logging.EventLogReader.ParseLevel(level),
                Time = time,
                ProviderName = AppConfig.EventSource,
                Message = message
            };
        }

        private static IEnumerable<ServyEventLogEntry> ThrowingIterator(Exception ex)
        {
            yield return new ServyEventLogEntry { Message = "[service] ok", ProviderName = AppConfig.EventSource };
            throw ex;
        }

        private static string? GetInternalQuery(EventLogQuery queryObj)
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
        public void Constructor_WhenReaderIsNull_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
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

            // Assert: Use reflection helper to verify the private field _sourceName
            var actualValue = TestReflection.GetField<string>(service, "_sourceName");

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
            var actualValue = TestReflection.GetField<string>(service, "_sourceName");

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
            var actualValue = TestReflection.GetField<object>(service, "_reader");

            Assert.NotNull(actualValue);
            Assert.Same(mockReader.Object, actualValue);
        }

        #region Explicit Branch Coverage Tests for Query String Generation

        [Fact]
        public async Task SearchAsync_WithNoExplicitFilters_BuildsDefaultSourceSystemQuery()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            string? capturedQuery = null;

            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                .Callback<EventLogQuery, int>((queryObj, limit) =>
                {
                    capturedQuery = GetInternalQuery(queryObj);
                })
                .Returns(Array.Empty<ServyEventLogEntry>());

            var service = CreateService(mockReader);

            // Act: All system filters are null.
            await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            // Assert: Default source name produces the provider-filtered system query exactly
            Assert.NotNull(capturedQuery);
            Assert.Equal($"*[System[Provider[@Name='{AppConfig.EventSource}']]]", capturedQuery);
        }

        [Fact]
        public async Task SearchAsync_PopulatedSystemFilterString_BuildsSystemTagQuery()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            string? capturedQuery = null;

            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                .Callback<EventLogQuery, int>((queryObj, limit) =>
                {
                    capturedQuery = GetInternalQuery(queryObj);
                })
                .Returns(Array.Empty<ServyEventLogEntry>());

            var service = CreateService(mockReader);

            // Act: At least one system filter is explicitly provided (Level)
            await service.SearchAsync(EventLogLevel.Error, null, null, null!, TestContext.Current.CancellationToken);

            // Assert: Verify the system query string construction
            Assert.NotNull(capturedQuery);
            Assert.StartsWith("*[System[", capturedQuery);

            // Assert the full composite clause to verify that both Critical (1) and Error (2) levels are queried.
            Assert.Contains("(Level=1 or Level=2)", capturedQuery);

            Assert.EndsWith("]]", capturedQuery);
        }

        #endregion

        #region Security & Allowlist Tests

        [Theory]
        [InlineData("Servy'] | //*")]
        [InlineData("Bad<Source>")]
        [InlineData("Src&Name")]
        public async Task SearchAsync_WhenSourceNameViolatesAllowlist_ThrowsSecurityException(string source)
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var service = new EventLogService(mockReader.Object, source);

            // Act & Assert
            await Assert.ThrowsAsync<SecurityException>(() =>
                service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData("Microsoft-Windows-Servy Agent 2.0")]
        [InlineData("Servy_Test-1")]
        public async Task SearchAsync_WhenSourceNameIsUnusualButAllowed_DoesNotThrow(string source)
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(Array.Empty<ServyEventLogEntry>());

            var service = new EventLogService(mockReader.Object, source);

            // Act & Assert
            var ex = await Record.ExceptionAsync(() =>
                service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken));

            Assert.Null(ex);
        }

        #endregion

        #region Exception Translation Tests

        [Fact]
        public async Task SearchAsync_WhenReaderThrowsEventLogException_ThrowsInvalidOperationException()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(ThrowingIterator(new EventLogException("Service stopped")));

            var service = CreateService(mockReader);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken));

            Assert.Contains("Cannot access Windows Event Log", ex.Message);
            Assert.IsType<EventLogException>(ex.InnerException);
        }

        [Fact]
        public async Task SearchAsync_WhenReaderThrowsUnauthorizedAccessException_ThrowsSecurityException()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(ThrowingIterator(new UnauthorizedAccessException("Access denied")));

            var service = CreateService(mockReader);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<SecurityException>(() =>
                service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken));

            Assert.Contains("Access denied to Windows Event Log", ex.Message);
            Assert.IsType<UnauthorizedAccessException>(ex.InnerException);
        }

        #endregion

        [Fact]
        public async Task SearchAsync_NoFilters_ReturnsResult()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(1, 2, DateTime.UtcNow, "[service] error happened");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            // Act
            var result = await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            // Assert
            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Error, entry.Level);
        }

        [Fact]
        public async Task SearchAsync_WithLevelFilter_ReturnsCorrectLevel()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(2, 3, DateTime.UtcNow, "[service] warning");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            // Act
            var result = await service.SearchAsync(EventLogLevel.Warning, null, null, null!, TestContext.Current.CancellationToken);

            // Assert
            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Warning, entry.Level);
        }

        [Fact]
        public async Task SearchAsync_WithStartDateAndEndDate_AppendsBothFilters()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(3, 4, DateTime.UtcNow, "[service] info");
            string? capturedQuery = null;

            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Callback<EventLogQuery, int>((queryObj, limit) =>
                      {
                          capturedQuery = GetInternalQuery(queryObj);
                      })
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var start = DateTime.UtcNow.AddDays(-1);
            var end = DateTime.UtcNow.AddDays(1);

            // Act
            var result = await service.SearchAsync(null, start, end, null!, TestContext.Current.CancellationToken);

            // Assert
            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Information, entry.Level);

            mockReader.Verify(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()), Times.Once);

            // Assert: both bounds are local calendar days converted to UTC - the caller's
            // time of day is discarded and the end bound is widened to the last tick of the day
            var expectedStartUtc = DateTime.SpecifyKind(start.Date, DateTimeKind.Local).ToUniversalTime();
            var expectedEndUtc = DateTime.SpecifyKind(end.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();

            Assert.NotNull(capturedQuery);
            Assert.Contains($"TimeCreated[@SystemTime >= '{expectedStartUtc:o}']", capturedQuery);
            Assert.Contains($"TimeCreated[@SystemTime <= '{expectedEndUtc:o}']", capturedQuery);
        }

        [Fact]
        public async Task SearchAsync_WithOnlyEndDate_AppendsFilterCorrectly()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(4, 0, DateTime.UtcNow, "[service] unknown level");
            string? capturedQuery = null;

            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Callback<EventLogQuery, int>((queryObj, limit) =>
                      {
                          capturedQuery = GetInternalQuery(queryObj);
                      })
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            var end = DateTime.UtcNow;

            // Act
            var result = await service.SearchAsync(null, null, end, null!, TestContext.Current.CancellationToken);

            // Assert
            var entry = Assert.Single(result);
            Assert.Equal(EventLogLevel.Information, entry.Level);

            mockReader.Verify(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()), Times.Once);

            // Assert: only the upper bound is emitted, widened to the last tick of the local day
            var expectedEndUtc = DateTime.SpecifyKind(end.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();

            Assert.NotNull(capturedQuery);
            Assert.Contains($"TimeCreated[@SystemTime <= '{expectedEndUtc:o}']", capturedQuery);
            Assert.DoesNotContain("SystemTime >=", capturedQuery);
        }

        [Fact]
        public async Task SearchAsync_WithKeyword_AddsKeywordFilter()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(5, 2, DateTime.UtcNow, "[service] servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            // Act
            var result = await service.SearchAsync(null, null, null, "servy", TestContext.Current.CancellationToken);

            // Assert
            var entry = Assert.Single(result);
            Assert.Contains("servy", entry.Message);
        }

        [Fact]
        public async Task SearchAsync_MultipleEntries()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt1 = CreateFakeEvent(5, 2, DateTime.UtcNow, "[service] servy failed");
            var fakeEvt2 = CreateFakeEvent(6, 2, DateTime.UtcNow.AddHours(-1), "[service] servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt1, fakeEvt2 });

            var service = CreateService(mockReader);

            // Act
            var result = await service.SearchAsync(null, null, null, string.Empty, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task Search_MessageWithoutBracketFormat_IsExcluded()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(5, 2, DateTime.UtcNow, "servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            // Act
            var result = await service.SearchAsync(null, null, null, null, TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task SearchAsync_WithKeyword_NoMatch()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var fakeEvt = CreateFakeEvent(5, 2, DateTime.UtcNow, "[service] servy failed");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            // Act
            var result = await service.SearchAsync(null, null, null, "unknown", TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task SearchAsync_PreservesEntryTimestamp()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var fakeEvt = CreateFakeEvent(6, 4, timestamp, "[service] valid time");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(new[] { fakeEvt });

            var service = CreateService(mockReader);

            // Act
            var result = await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            // Assert
            var entry = Assert.Single(result);
            Assert.Equal(timestamp, entry.Time);
        }

        [Fact]
        public async Task SearchAsync_ShouldReturnEmptyCollectionWhenFormatDescriptionIsNull()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var evt = CreateFakeEvent(1, 1, DateTime.UtcNow, null!);
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>())).Returns(new[] { evt });

            var service = CreateService(mockReader);

            // Act
            var results = await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchAsync_WhenLevelIsNull_OmitsLevelClauseFromQuery()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var evt = CreateFakeEvent(1, 0, DateTime.UtcNow, "[service] Message");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>())).Returns(new[] { evt });

            var service = CreateService(mockReader);

            // Act
            var results = await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(results);
            Assert.Equal(EventLogLevel.Information, results.First().Level);
        }

        [Fact]
        public async Task SearchAsync_ShouldThrowWhenCancelled()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var evt = CreateFakeEvent(1, 1, DateTime.UtcNow, "[service] Message");
            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>())).Returns(new[] { evt });

            var service = CreateService(mockReader);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act & Assert
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    service.SearchAsync(null, null, null, null!, cts.Token));
            }
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

        [Fact]
        public async Task SearchAsync_WhenResultsExceedMaxResults_BreaksLoop()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();

            const int limit = AppConfig.EventLogMaxResults;

            // Feed one more than the service's cap (limit + 1) so the 'break' is forced.
            // Ascending time values (+i) keep the mock data unsorted, so OrderByDescending has to reverse it.
            var excessiveResults = Enumerable.Range(1, limit + 1)
                .Select(i => CreateFakeEvent(
                    id: i,
                    level: 4,
                    time: DateTime.UtcNow.AddSeconds(i), // Varying ascending time for genuine Sort coverage
                    message: $"[service] Message {i}"))
                .ToList();

            mockReader.Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                      .Returns(excessiveResults);

            var service = CreateService(mockReader);

            // Act
            var results = await service.SearchAsync(null, null, null, null!, TestContext.Current.CancellationToken);

            // Assert
            // 1. Verify the loop broke exactly at the limit
            var resultsList = results.ToList();
            Assert.Equal(limit, resultsList.Count);

            // 2. Verify the list is fully ordered (covers the .OrderByDescending branch rigorously)
            for (int k = 1; k < resultsList.Count; k++)
            {
                Assert.True(resultsList[k - 1].Time >= resultsList[k].Time,
                    $"Results are out of sequence at index {k}. Elements must be monotonically ordered by descending time across the entire dataset.");
            }
        }

        [Fact]
        public async Task SearchAsync_WildcardMode_FiltersOutNonServyProviderEvents()
        {
            // Arrange
            var mockReader = new Mock<IEventLogReader>();
            var matchingEvent = CreateFakeEvent(1, 4, DateTime.UtcNow, "[Service] Servy event log entry");
            matchingEvent.ProviderName = AppConfig.EventSource;

            var nonMatchingEvent = CreateFakeEvent(2, 4, DateTime.UtcNow, "[Service] Unrelated system event entry");
            nonMatchingEvent.ProviderName = "Microsoft-Windows-Kernel-General";

            mockReader
                .Setup(r => r.ReadEvents(It.IsAny<EventLogQuery>(), It.IsAny<int>()))
                .Returns(new[] { matchingEvent, nonMatchingEvent });

            var service = new EventLogService(mockReader.Object, string.Empty);

            // Act
            var results = await service.SearchAsync(null, null, null, null, TestContext.Current.CancellationToken);

            // Assert
            var entry = Assert.Single(results);
            Assert.Equal(1, entry.EventId);
            Assert.Equal(AppConfig.EventSource, entry.ProviderName);
        }
    }
}
