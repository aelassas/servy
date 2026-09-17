using Servy.Manager.Models;
using Servy.Manager.Utils;
using System;
using System.Collections.Generic;
using Xunit;

namespace Servy.Manager.UnitTests.Utils
{
    /// <summary>
    /// Unit tests for the <see cref="HistoryResult"/> data transfer object.
    /// Ensures properties are correctly initialized, defensive copies are isolated, and UTC timestamp constraints are enforced.
    /// </summary>
    public class HistoryResultTests
    {
        private static readonly DateTime ExpectedCreationTime = new DateTime(2026, 3, 30, 10, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Constructor_ValidArguments_PopulatesPropertiesCorrectly()
        {
            // Arrange
            var sampleLines = new List<LogLine>
            {
                new LogLine("Line 1", LogType.StdOut, DateTime.UtcNow),
                new LogLine("Line 2", LogType.StdOut, DateTime.UtcNow),
            };
            long expectedPosition = 1024;

            // Act
            var result = new HistoryResult(sampleLines, expectedPosition, ExpectedCreationTime);

            // The constructor stores a defensive copy, so mutating the caller's list afterwards
            // must not reach the snapshot; without the copy these assertions fail.
            sampleLines.Add(new LogLine("Line 3", LogType.StdOut, DateTime.UtcNow));
            sampleLines.Clear();

            // Assert
            Assert.NotNull(result.Lines);
            Assert.Equal(2, result.Lines.Count);
            Assert.Equal("Line 1", result.Lines[0].Text);
            Assert.Equal("Line 2", result.Lines[1].Text);
            Assert.Equal(expectedPosition, result.Position);
            Assert.Equal(ExpectedCreationTime, result.CreationTimeUtc);
        }

        [Fact]
        public void Constructor_NullLinesArgument_FallsBackToEmptyCollectionBranch()
        {
            // Arrange
            List<LogLine> nullLines = null;
            long expectedPosition = 512;

            // Act
            var result = new HistoryResult(nullLines, expectedPosition, ExpectedCreationTime);

            // Assert
            // Verifies the null branch yields an empty collection rather than a null reference
            Assert.NotNull(result.Lines);
            Assert.Empty(result.Lines);
            Assert.Equal(expectedPosition, result.Position);
            Assert.Equal(ExpectedCreationTime, result.CreationTimeUtc);
        }

        [Theory]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void Constructor_NonUtcCreationTime_ThrowsArgumentException(DateTimeKind kind)
        {
            // Arrange
            var invalidTime = new DateTime(2026, 3, 30, 10, 0, 0, kind);
            var sampleLines = new List<LogLine>();

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new HistoryResult(sampleLines, 0, invalidTime));
            Assert.Equal("creationTimeUtc", ex.ParamName);
        }

        [Fact]
        public void Constructor_EmptyLinesList_InitializesEmptyReadOnlyCollection()
        {
            // Arrange
            var emptyLines = new List<LogLine>();
            long expectedPosition = 0;

            // Act
            var result = new HistoryResult(emptyLines, expectedPosition, ExpectedCreationTime);

            // Assert
            Assert.NotNull(result.Lines);
            Assert.Empty(result.Lines);
            Assert.Equal(expectedPosition, result.Position);
            Assert.Equal(ExpectedCreationTime, result.CreationTimeUtc);
        }
    }
}
