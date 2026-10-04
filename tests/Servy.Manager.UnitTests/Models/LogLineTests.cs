using Servy.Manager.Models;

namespace Servy.Manager.UnitTests.Models
{
    public class LogLineTests
    {
        /// <summary>
        /// A timestamp whose kind is <see cref="DateTimeKind.Unspecified"/> is relabelled as UTC with its
        /// ticks untouched (#5998). The tick assertion separates that from <c>ToUniversalTime()</c> only on
        /// a host whose local offset is not zero; the kind assertion holds on every host and catches a
        /// constructor that stores the value as given.
        /// </summary>
        [Fact]
        public void Constructor_UnspecifiedKindTimestamp_IsRelabelledUtcWithoutShift()
        {
            // Arrange
            var unspecified = new DateTime(2026, 3, 30, 10, 0, 0, DateTimeKind.Unspecified);

            // Act
            var line = new LogLine("text", LogType.StdOut, unspecified);

            // Assert
            Assert.Equal(DateTimeKind.Utc, line.Timestamp.Kind);
            Assert.Equal(unspecified.Ticks, line.Timestamp.Ticks);
        }

        [Fact]
        public void Constructor_LocalKindTimestamp_IsConvertedToUtc()
        {
            // Arrange
            var local = new DateTime(2026, 3, 30, 10, 0, 0, DateTimeKind.Local);

            // Act
            var line = new LogLine("text", LogType.StdErr, local);

            // Assert
            Assert.Equal(DateTimeKind.Utc, line.Timestamp.Kind);
            Assert.Equal(local.ToUniversalTime(), line.Timestamp);
        }

        [Fact]
        public void Constructor_NoTimestamp_UsesCurrentUtcTime()
        {
            // Arrange
            var before = DateTime.UtcNow;

            // Act
            var line = new LogLine("text", LogType.StdOut);

            // Assert
            var after = DateTime.UtcNow;
            Assert.Equal(DateTimeKind.Utc, line.Timestamp.Kind);
            Assert.InRange(line.Timestamp, before, after);
        }
    }
}
