using Servy.Core.Enums;
using System.Diagnostics.Eventing.Reader;
using System.Security.Principal;
using EventLogReader = Servy.Core.Logging.EventLogReader;

namespace Servy.Core.UnitTests.Logging
{
    public class EventLogReaderTests
    {
        #region ParseLevel Tests

        [Theory]
        [InlineData(0, EventLogLevel.Information)] // LogAlways
        [InlineData(1, EventLogLevel.Error)]       // Critical folded to Error
        [InlineData(2, EventLogLevel.Error)]       // Error
        [InlineData(3, EventLogLevel.Warning)]     // Warning
        [InlineData(4, EventLogLevel.Information)] // Information
        [InlineData(5, EventLogLevel.Information)] // Verbose folded to Information
        [InlineData(6, EventLogLevel.Information)] // Unknown level boundary fallback
        [InlineData(255, EventLogLevel.Information)]
        public void ParseLevel_AllBranches_ReturnExpectedStronglyTypedEnum(byte rawLevel, EventLogLevel expected)
        {
            // Act
            var result = EventLogReader.ParseLevel(rawLevel);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region SafeToOffset Tests

        [Fact]
        public void SafeToOffset_WhenNull_ReturnsDateTimeOffsetMinValue()
        {
            // Act
            var result = EventLogReader.SafeToOffset(null);

            // Assert
            Assert.Equal(DateTimeOffset.MinValue, result);
        }

        [Fact]
        public void SafeToOffset_WhenValidUtcDateTime_ReturnsCorrectUtcOffset()
        {
            // Arrange
            var testTime = new DateTime(2026, 6, 24, 12, 0, 0, DateTimeKind.Utc);

            // Act
            var result = EventLogReader.SafeToOffset(testTime);

            // Assert
            Assert.Equal(TimeSpan.Zero, result.Offset);
            Assert.Equal(2026, result.Year);
            Assert.Equal(12, result.Hour);
        }

        #endregion

        #region MapToDto Tests

        [Fact]
        public void MapToDto_AllPropertiesValid_MapsCorrectly()
        {
            // Arrange
            var mockEvent = new TestableEventRecord
            {
                IdValue = 42,
                TimeCreatedValue = new DateTime(2026, 6, 24, 15, 0, 0, DateTimeKind.Utc),
                LevelValue = 3,
                ProviderNameValue = "ServyEngine",
                FormatDescriptionValue = "Service started successfully."
            };

            // Act
            var result = EventLogReader.MapToDto(mockEvent);

            // Assert
            Assert.Equal(42, result.EventId);
            Assert.Equal(EventLogLevel.Warning, result.Level);
            Assert.Equal("ServyEngine", result.ProviderName);
            Assert.Equal("Service started successfully.", result.Message);
            Assert.Equal(TimeSpan.Zero, result.Time.Offset);
        }

        [Fact]
        public void MapToDto_NullLevelAndNullMessage_UsesDefaultsSafely()
        {
            // Arrange
            var mockEvent = new TestableEventRecord
            {
                IdValue = 101,
                TimeCreatedValue = null,
                LevelValue = null,
                ProviderNameValue = "Servy",
                FormatDescriptionValue = null
            };

            // Act
            var result = EventLogReader.MapToDto(mockEvent);

            // Assert
            Assert.Equal(101, result.EventId);
            Assert.Equal(DateTimeOffset.MinValue, result.Time);
            Assert.Equal(EventLogLevel.Information, result.Level); // Level null triggers 0 inside MapToDto -> ParseLevel(0) is Information
            Assert.Equal("Servy", result.ProviderName);
            Assert.NotNull(result.Message);
            Assert.Empty(result.Message);
        }

        [Fact]
        public void MapToDto_WhenEventLogExceptionsThrownOnProperties_CatchesAndAssignsDefaults()
        {
            // Arrange
            var mockEvent = new TestableEventRecord
            {
                ThrowExceptionOnProperties = true,
                FormatDescriptionValue = "Will be ignored due to properties exception layout"
            };

            // Act
            var result = EventLogReader.MapToDto(mockEvent);

            // Assert
            Assert.Equal(0, result.EventId);
            Assert.Equal(DateTimeOffset.MinValue, result.Time);
            Assert.Equal(EventLogLevel.Information, result.Level);
            Assert.Equal("<unavailable>", result.ProviderName);
        }

        [Theory]
        [InlineData(typeof(EventLogException))]
        [InlineData(typeof(InvalidOperationException))]
        public void MapToDto_WhenFormatDescriptionThrowsExpectedExceptions_WrapsExceptionMessage(Type exceptionType)
        {
            // Arrange
            var exception = (Exception)Activator.CreateInstance(exceptionType, "Native description handle missing")!;
            var mockEvent = new TestableEventRecord
            {
                IdValue = 500,
                ProviderNameValue = "System",
                ExceptionToThrowOnFormat = exception
            };

            // Act
            var result = EventLogReader.MapToDto(mockEvent);

            // Assert
            Assert.Equal(500, result.EventId);
            Assert.StartsWith("<unavailable:", result.Message);
            Assert.EndsWith(">", result.Message);
        }

        #endregion

        #region Helper Mock Stub Layout for EventRecord

        /// <summary>
        /// A testable variant overriding property backing blocks via reflection mappings or mock layout emulation.
        /// Since fields on EventRecord are protected/internal, reflection allows instantiation without external API dependencies.
        /// </summary>
        private class TestableEventRecord : EventRecord
        {
            public int IdValue { get; set; }
            public DateTime? TimeCreatedValue { get; set; }
            public byte? LevelValue { get; set; }
            public string? ProviderNameValue { get; set; }
            public string? FormatDescriptionValue { get; set; }
            public bool ThrowExceptionOnProperties { get; set; }
            public Exception? ExceptionToThrowOnFormat { get; set; }

            // Instantiates via the internal constructor structure or creates a blank layout handle
            public TestableEventRecord()
            {
                // Force base initialization to prevent native validation exceptions if framework code runs
            }

            public override int Id => ThrowExceptionOnProperties
                ? throw new EventLogNotFoundException("Simulated missing property exception context")
                : IdValue;

            public override DateTime? TimeCreated => ThrowExceptionOnProperties
                ? throw new EventLogReadingException("Simulated missing time offset")
                : TimeCreatedValue;

            public override byte? Level => ThrowExceptionOnProperties
                ? throw new EventLogException("Simulated exception")
                : LevelValue;

            public override string ProviderName => ThrowExceptionOnProperties
                ? throw new EventLogException("Access Denied")
                : ProviderNameValue ?? string.Empty;

            public override Guid? ActivityId => null;

            public override EventBookmark? Bookmark => null;

            public override long? Keywords => null;

            public override IEnumerable<string> KeywordsDisplayNames => Array.Empty<string>();

            public override string? LevelDisplayName => null;

            public override string? LogName => null;

            public override string? MachineName => null;

            public override short? Opcode => null;

            public override string? OpcodeDisplayName => null;

            public override int? ProcessId => null;

            public override IList<EventProperty> Properties => Array.Empty<EventProperty>();

            public override Guid? ProviderId => null;

            public override int? Qualifiers => null;

            public override long? RecordId => null;

            public override Guid? RelatedActivityId => null;

            public override int? Task => null;

            public override string? TaskDisplayName => null;

            public override int? ThreadId => null;

            public override SecurityIdentifier? UserId => null;

            public override byte? Version => null;

            public override string FormatDescription()
            {
                if (ExceptionToThrowOnFormat != null)
                {
                    throw ExceptionToThrowOnFormat;
                }
                return FormatDescriptionValue!;
            }

            public override string FormatDescription(IEnumerable<object> values)
            {
                return FormatDescriptionValue ?? string.Empty;
            }

            public override string ToXml()
            {
                return string.Empty;
            }
        }

        private class ExceptionAttribute : Attribute
        {
            public Type ExceptionType { get; }
            public ExceptionAttribute(Type exceptionType) => ExceptionType = exceptionType;
        }

        #endregion
    }
}