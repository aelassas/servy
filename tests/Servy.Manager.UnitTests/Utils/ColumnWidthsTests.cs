using Servy.Manager.Utils;
using System.Globalization;
using System.Collections.Generic;
using System;
using Xunit;

namespace Servy.Manager.UnitTests.Utils
{
    /// <summary>
    /// Tests for <see cref="ColumnWidths"/>, the persisted <c>Header=Width</c> list of a grid.
    /// </summary>
    public class ColumnWidthsTests
    {
        private static KeyValuePair<string, double> Entry(string header, double width)
        {
            return new KeyValuePair<string, double>(header, width);
        }

        [Fact]
        public void Parse_ValidList_ReturnsEveryEntryInOrder()
        {
            // Act
            var widths = ColumnWidths.Parse("Name=180,Startup Type=95.5, Log On As = 240 ");

            // Assert
            Assert.Equal(new[] { Entry("Name", 180), Entry("Startup Type", 95.5), Entry("Log On As", 240) }, widths);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Parse_NullOrBlank_ReturnsNoEntries(string value)
        {
            // Act
            var widths = ColumnWidths.Parse(value);

            // Assert
            Assert.Empty(widths);
        }

        [Theory]
        [InlineData("Name")]
        [InlineData("=120")]
        [InlineData("  =120")]
        [InlineData("Name=")]
        [InlineData("Name=abc")]
        [InlineData("Name=NaN")]
        [InlineData("Name=Infinity")]
        [InlineData("Name=0")]
        [InlineData("Name=-120")]
        [InlineData("Name=10000.01")]
        public void Parse_DamagedEntry_IsSkippedAndTheOthersAreKept(string damaged)
        {
            // Act
            var widths = ColumnWidths.Parse("Status=90," + damaged + ",PID=60");

            // Assert
            Assert.Equal(new[] { Entry("Status", 90), Entry("PID", 60) }, widths);
        }

        [Fact]
        public void Parse_WidthAtTheMaximum_IsKept()
        {
            // Act
            var widths = ColumnWidths.Parse("Name=10000");

            // Assert
            Assert.Equal(new[] { Entry("Name", 10000) }, widths);
        }

        [Fact]
        public void Parse_SameHeaderTwice_KeepsTheLastWidthWithoutRegardToCase()
        {
            // Act
            var widths = ColumnWidths.Parse("Name=100,Status=90,NAME=250");

            // Assert
            Assert.Equal(new[] { Entry("Status", 90), Entry("NAME", 250) }, widths);
        }

        [Fact]
        public void Parse_HeaderContainingTheWidthSeparator_SplitsOnTheLastOne()
        {
            // Act
            var widths = ColumnWidths.Parse("A=B=120");

            // Assert
            Assert.Equal(new[] { Entry("A=B", 120) }, widths);
        }

        [Fact]
        public void TryGetWidth_KnownHeader_ReturnsItsWidthWithoutRegardToCase()
        {
            // Arrange
            var widths = ColumnWidths.Parse("Name=180,Status=90");

            // Act
            var found = ColumnWidths.TryGetWidth(widths, "status", out var width);

            // Assert
            Assert.True(found);
            Assert.Equal(90, width);
        }

        [Theory]
        [InlineData("PID")]
        [InlineData("")]
        [InlineData(null)]
        public void TryGetWidth_UnknownOrMissingHeader_ReturnsFalse(string header)
        {
            // Arrange
            var widths = ColumnWidths.Parse("Name=180,Status=90");

            // Act
            var found = ColumnWidths.TryGetWidth(widths, header, out var width);

            // Assert
            Assert.False(found);
            Assert.Equal(0, width);
        }

        [Fact]
        public void Merge_MeasuredColumns_AreWrittenInOrderWithInvariantNumbersRoundedToTwoDecimals()
        {
            // Act
            var text = ColumnWidths.Merge(null, new[] { Entry("Name", 180), Entry("Status", 95.456), Entry("PID", 60.5) });

            // Assert
            Assert.Equal("Name=180,Status=95.46,PID=60.5", text);
        }

        [Fact]
        public void Merge_ColumnNotMeasured_KeepsItsPreviousWidthAfterTheMeasuredOnes()
        {
            // Arrange: "Description" is hidden now, so it is not measured
            const string previous = "Name=100,Description=300,Status=80";

            // Act
            var text = ColumnWidths.Merge(previous, new[] { Entry("Name", 180), Entry("Status", 90) });

            // Assert
            Assert.Equal("Name=180,Status=90,Description=300", text);
        }

        [Fact]
        public void Merge_MeasuredColumn_ReplacesItsPreviousWidthWithoutRegardToCase()
        {
            // Act
            var text = ColumnWidths.Merge("NAME=100", new[] { Entry("Name", 180) });

            // Assert
            Assert.Equal("Name=180", text);
        }

        [Theory]
        [InlineData("", 120)]
        [InlineData("   ", 120)]
        [InlineData("A,B", 120)]
        [InlineData("A=B", 120)]
        [InlineData("Name", 0)]
        [InlineData("Name", -1)]
        [InlineData("Name", double.NaN)]
        [InlineData("Name", double.PositiveInfinity)]
        [InlineData("Name", 10000.01)]
        public void Merge_MeasuredEntryThatCannotBeWrittenBack_IsSkipped(string header, double width)
        {
            // Act
            var text = ColumnWidths.Merge(null, new[] { Entry("Status", 90), Entry(header, width) });

            // Assert
            Assert.Equal("Status=90", text);
        }

        [Fact]
        public void Merge_SameHeaderMeasuredTwice_KeepsTheFirst()
        {
            // Act
            var text = ColumnWidths.Merge(null, new[] { Entry("Name", 180), Entry("name", 250) });

            // Assert
            Assert.Equal("Name=180", text);
        }

        [Fact]
        public void Merge_NothingMeasuredAndNothingPrevious_ReturnsAnEmptyString()
        {
            // Act
            var text = ColumnWidths.Merge(null, Array.Empty<KeyValuePair<string, double>>());

            // Assert
            Assert.Equal(string.Empty, text);
        }

        [Fact]
        public void Merge_ThenParse_RoundTrips()
        {
            // Arrange
            var measured = new[] { Entry("Name", 180), Entry("Startup Type", 95.5) };

            // Act
            var widths = ColumnWidths.Parse(ColumnWidths.Merge(null, measured));

            // Assert
            Assert.Equal(measured, widths);
        }

        [Fact]
        public void MergeAndParse_UnderADecimalCommaCulture_KeepTheInvariantForm()
        {
            // Arrange
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            try
            {
                // Act
                var text = ColumnWidths.Merge(null, new[] { Entry("Status", 95.456) });
                var widths = ColumnWidths.Parse("Status=95.5");

                // Assert
                Assert.Equal("Status=95.46", text);
                Assert.Equal(new[] { Entry("Status", 95.5) }, widths);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }


        [Fact]
        public void TryGetWidth_NullList_ReturnsFalse()
        {
            // Act
            var found = ColumnWidths.TryGetWidth(null, "Name", out var width);

            // Assert
            Assert.False(found);
            Assert.Equal(0, width);
        }

        [Fact]
        public void Merge_NullMeasuredList_KeepsThePreviousWidths()
        {
            // Act
            var text = ColumnWidths.Merge("Name=100", null);

            // Assert
            Assert.Equal("Name=100", text);
        }

        [Fact]
        public void Merge_NullHeader_SkipsThatEntryAndKeepsTheRest()
        {
            // Act
            var text = ColumnWidths.Merge(null, new[] { Entry(null, 120), Entry("Status", 90) });

            // Assert
            Assert.Equal("Status=90", text);
        }
    }
}
