using Servy.Manager.Models;
using Servy.Manager.Utils;
using System;
using System.Collections.Generic;
using Xunit;

namespace Servy.Manager.UnitTests.Utils
{
    /// <summary>
    /// Unit tests for the <see cref="HistoryResult"/> data transfer object.
    /// Ensures properties are correctly initialized and null constraints are handled defensively.
    /// </summary>
    public class HistoryResultTests
    {
        private static readonly DateTime ExpectedCreationTime = new DateTime(2026, 3, 30, 10, 0, 0);

        [Fact]
        public void Constructor_ValidArguments_PopulatesPropertiesCorrectly()
        {
            // Arrange
            var sampleLines = new List<LogLine>
            {
                new LogLine ("Line 1", LogType.StdOut,  DateTime.Now ),
                new LogLine ("Line 2", LogType.StdOut, DateTime.Now ),
            };
            long expectedPosition = 1024;

            // Act
            var result = new HistoryResult(sampleLines, expectedPosition, ExpectedCreationTime);

            // Assert
            Assert.NotNull(result.Lines);
            Assert.Equal(2, result.Lines.Count);
            Assert.Equal("Line 1", result.Lines[0].Text);
            Assert.Equal(expectedPosition, result.Position);
            Assert.Equal(ExpectedCreationTime, result.CreationTime);
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
            // Verifies the null-coalescing branch (lines ?? new List<LogLine>()) evaluated successfully
            Assert.NotNull(result.Lines);
            Assert.Empty(result.Lines);
            Assert.Equal(expectedPosition, result.Position);
            Assert.Equal(ExpectedCreationTime, result.CreationTime);
        }
    }
}