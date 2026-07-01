using Servy.Core.Enums;
using Servy.Manager.Models;

namespace Servy.Manager.UnitTests.Models
{
    public class LogEntryModelTests
    {
        [Fact]
        public void Properties_StandardMutations_UpdateCorrectlyAndNotifyUI()
        {
            // Arrange
            var logEntry = new LogEntryModel();
            var expectedTime = DateTime.UtcNow;
            var expectedMessage = "Service stopped successfully.";
            int expectedEventId = 1001;

            bool timeChangedFired = false;
            bool messageChangedFired = false;
            bool eventIdChangedFired = false;

            logEntry.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(logEntry.Time)) timeChangedFired = true;
                if (e.PropertyName == nameof(logEntry.Message)) messageChangedFired = true;
                if (e.PropertyName == nameof(logEntry.EventId)) eventIdChangedFired = true;
            };

            // Act
            logEntry.Time = expectedTime;
            logEntry.Message = expectedMessage;
            logEntry.EventId = expectedEventId;

            // Assert
            Assert.Equal(expectedTime, logEntry.Time);
            Assert.Equal(expectedMessage, logEntry.Message);
            Assert.Equal(expectedEventId, logEntry.EventId);

            Assert.True(timeChangedFired);
            Assert.True(messageChangedFired);
            Assert.True(eventIdChangedFired);
        }

        [Fact]
        public void Level_ValueChanges_FiresDependentPropertyNotifications()
        {
            // Arrange
            var logEntry = new LogEntryModel { Level = EventLogLevel.Information };
            bool levelChangedFired = false;
            bool levelIconChangedFired = false;

            logEntry.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(logEntry.Level)) levelChangedFired = true;
                if (e.PropertyName == nameof(logEntry.LevelIcon)) levelIconChangedFired = true;
            };

            // Act
            logEntry.Level = EventLogLevel.Warning;

            // Assert
            Assert.Equal(EventLogLevel.Warning, logEntry.Level);
            Assert.True(levelChangedFired, "Changing Level should raise PropertyChanged for 'Level'.");
            Assert.True(levelIconChangedFired, "Changing Level should notify the UI that the dependent 'LevelIcon' property changed.");
        }

        [Fact]
        public void Level_SetSameValue_DoesNotFireNotifications()
        {
            // Arrange
            var logEntry = new LogEntryModel { Level = EventLogLevel.Error };
            bool anyPropertyChangedFired = false;

            logEntry.PropertyChanged += (s, e) => anyPropertyChangedFired = true;

            // Act
            logEntry.Level = EventLogLevel.Error;

            // Assert
            Assert.False(anyPropertyChangedFired, "Setting Level to the same value must skip raising notifications.");
        }

        [Theory]
        [InlineData(EventLogLevel.Critical, "Error.png")]
        [InlineData(EventLogLevel.Error, "Error.png")]
        [InlineData(EventLogLevel.Warning, "Warning.png")]
        [InlineData(EventLogLevel.Information, "Info.png")]
        [InlineData(EventLogLevel.Verbose, "Info.png")]
        [InlineData((EventLogLevel)999, "Info.png")] // Undefined/Default boundary fallback branch
        public void LevelIcon_GetsInvoked_ReturnsExpectedAbsolutePackUri(EventLogLevel inputLevel, string expectedIconFile)
        {
            // Arrange
            var logEntry = new LogEntryModel { Level = inputLevel };

            // Act
            string actualUri = logEntry.LevelIcon;

            // Assert
            // Validate that the string suffix resolves to the correct filename payload
            // without hardcoding or binding against the absolute WPF application pack infrastructure schema.
            Assert.NotNull(actualUri);
            Assert.EndsWith(expectedIconFile, actualUri, StringComparison.OrdinalIgnoreCase);
        }
    }
}