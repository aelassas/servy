using Servy.Manager.Utils;
using Xunit;

namespace Servy.Manager.UnitTests.Utils
{
    /// <summary>
    /// Tests for <see cref="WindowPlacement"/>, the persisted position, size and state of the main window.
    /// </summary>
    public class WindowPlacementTests
    {
        [Fact]
        public void TryParse_NormalPlacement_ReadsEveryField()
        {
            // Act
            var parsed = WindowPlacement.TryParse("120.5,-80,1200,700.25,Normal", out var placement);

            // Assert
            Assert.True(parsed);
            Assert.NotNull(placement);
            Assert.Equal(120.5, placement.Left);
            Assert.Equal(-80, placement.Top);
            Assert.Equal(1200, placement.Width);
            Assert.Equal(700.25, placement.Height);
            Assert.False(placement.IsMaximized);
        }

        [Theory]
        [InlineData("10,20,1000,600,Maximized")]
        [InlineData("10,20,1000,600,maximized")]
        [InlineData(" 10 , 20 , 1000 , 600 , Maximized ")]
        public void TryParse_MaximizedState_IsReadWithoutRegardToCaseOrSpacing(string value)
        {
            // Act
            var parsed = WindowPlacement.TryParse(value, out var placement);

            // Assert
            Assert.True(parsed);
            Assert.NotNull(placement);
            Assert.True(placement.IsMaximized);
            Assert.Equal(10, placement.Left);
            Assert.Equal(20, placement.Top);
            Assert.Equal(1000, placement.Width);
            Assert.Equal(600, placement.Height);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("10,20,1000,600")]
        [InlineData("10,20,1000,600,Normal,extra")]
        [InlineData("abc,20,1000,600,Normal")]
        [InlineData("10,abc,1000,600,Normal")]
        [InlineData("10,20,abc,600,Normal")]
        [InlineData("10,20,1000,abc,Normal")]
        [InlineData("NaN,20,1000,600,Normal")]
        [InlineData("10,Infinity,1000,600,Normal")]
        [InlineData("10,20,0,600,Normal")]
        [InlineData("10,20,-1000,600,Normal")]
        [InlineData("10,20,1000,0,Normal")]
        [InlineData("10,20,1000,-600,Normal")]
        [InlineData("10,20,1000,600,Minimized")]
        [InlineData("10,20,1000,600,")]
        public void TryParse_UnusableValue_ReturnsFalseAndNoPlacement(string value)
        {
            // Act
            var parsed = WindowPlacement.TryParse(value, out var placement);

            // Assert
            Assert.False(parsed);
            Assert.Null(placement);
        }

        [Fact]
        public void TryParse_DecimalComma_IsNotReadAsADecimalSeparator()
        {
            // "10,5" is two fields, never the number 10.5: the persisted form is culture-invariant.
            var parsed = WindowPlacement.TryParse("10,5,20,1000,600,Normal", out var placement);

            Assert.False(parsed);
            Assert.Null(placement);
        }

        [Fact]
        public void Serialize_NormalPlacement_WritesInvariantNumbersRoundedToTwoDecimals()
        {
            // Arrange
            var placement = new WindowPlacement(120.456, -80, 1200, 700.5, false);

            // Act
            var text = placement.Serialize();

            // Assert
            Assert.Equal("120.46,-80,1200,700.5,Normal", text);
        }

        [Fact]
        public void Serialize_MaximizedPlacement_WritesTheMaximizedState()
        {
            // Arrange
            var placement = new WindowPlacement(0, 0, 1000, 600, true);

            // Act
            var text = placement.Serialize();

            // Assert
            Assert.Equal("0,0,1000,600,Maximized", text);
        }

        [Fact]
        public void Serialize_ThenTryParse_RoundTrips()
        {
            // Arrange
            var placement = new WindowPlacement(-1920, 35.5, 1280, 720, true);

            // Act
            var parsed = WindowPlacement.TryParse(placement.Serialize(), out var roundTripped);

            // Assert
            Assert.True(parsed);
            Assert.NotNull(roundTripped);
            Assert.Equal(-1920, roundTripped.Left);
            Assert.Equal(35.5, roundTripped.Top);
            Assert.Equal(1280, roundTripped.Width);
            Assert.Equal(720, roundTripped.Height);
            Assert.True(roundTripped.IsMaximized);
        }

        [Fact]
        public void FitTo_PlacementInsideTheScreen_IsUnchanged()
        {
            // Arrange
            var placement = new WindowPlacement(100, 50, 1200, 700, true);

            // Act
            var fitted = placement.FitTo(0, 0, 1920, 1080, 1000, 500);

            // Assert
            Assert.Equal(100, fitted.Left);
            Assert.Equal(50, fitted.Top);
            Assert.Equal(1200, fitted.Width);
            Assert.Equal(700, fitted.Height);
            Assert.True(fitted.IsMaximized);
        }

        [Fact]
        public void FitTo_PlacementOnAMonitorThatIsGone_IsMovedBackInsideTheScreen()
        {
            // Arrange: saved on a second monitor to the right of a 1920x1080 primary, below its bottom edge
            var placement = new WindowPlacement(2500, 1500, 1200, 700, false);

            // Act
            var fitted = placement.FitTo(0, 0, 1920, 1080, 1000, 500);

            // Assert: right and bottom edges are aligned with the screen
            Assert.Equal(720, fitted.Left);
            Assert.Equal(380, fitted.Top);
            Assert.Equal(1200, fitted.Width);
            Assert.Equal(700, fitted.Height);
        }

        [Fact]
        public void FitTo_PlacementLeftOfAndAboveTheScreen_IsMovedToTheScreenOrigin()
        {
            // Arrange: the virtual screen starts at a negative origin (a monitor to the left of the primary)
            var placement = new WindowPlacement(-5000, -3000, 1200, 700, false);

            // Act
            var fitted = placement.FitTo(-1920, -200, 3840, 1280, 1000, 500);

            // Assert
            Assert.Equal(-1920, fitted.Left);
            Assert.Equal(-200, fitted.Top);
        }

        [Fact]
        public void FitTo_SizeLargerThanTheScreen_IsLimitedToTheScreen()
        {
            // Arrange
            var placement = new WindowPlacement(100, 100, 3000, 2000, false);

            // Act
            var fitted = placement.FitTo(0, 0, 1920, 1080, 1000, 500);

            // Assert
            Assert.Equal(1920, fitted.Width);
            Assert.Equal(1080, fitted.Height);
            Assert.Equal(0, fitted.Left);
            Assert.Equal(0, fitted.Top);
        }

        [Fact]
        public void FitTo_SizeBelowTheMinimum_IsRaisedToTheMinimum()
        {
            // Arrange
            var placement = new WindowPlacement(100, 100, 200, 100, false);

            // Act
            var fitted = placement.FitTo(0, 0, 1920, 1080, 1000, 500);

            // Assert
            Assert.Equal(1000, fitted.Width);
            Assert.Equal(500, fitted.Height);
            Assert.Equal(100, fitted.Left);
            Assert.Equal(100, fitted.Top);
        }

        [Fact]
        public void FitTo_ScreenSmallerThanTheMinimum_KeepsTheMinimumAndAlignsWithTheScreenOrigin()
        {
            // Arrange
            var placement = new WindowPlacement(300, 200, 1200, 700, false);

            // Act
            var fitted = placement.FitTo(0, 0, 800, 480, 1000, 500);

            // Assert
            Assert.Equal(1000, fitted.Width);
            Assert.Equal(500, fitted.Height);
            Assert.Equal(0, fitted.Left);
            Assert.Equal(0, fitted.Top);
        }

        [Theory]
        [InlineData(0, 0, 0, 1080)]
        [InlineData(0, 0, 1920, 0)]
        [InlineData(0, 0, -1920, 1080)]
        [InlineData(0, 0, double.NaN, 1080)]
        [InlineData(0, 0, 1920, double.PositiveInfinity)]
        [InlineData(double.NaN, 0, 1920, 1080)]
        [InlineData(0, double.NegativeInfinity, 1920, 1080)]
        public void FitTo_ScreenAreaIsNotUsable_ReturnsThePlacementUnchanged(double left, double top, double width, double height)
        {
            // Arrange
            var placement = new WindowPlacement(5000, 4000, 200, 100, false);

            // Act
            var fitted = placement.FitTo(left, top, width, height, 1000, 500);

            // Assert
            Assert.Same(placement, fitted);
        }

        [Fact]
        public void FitTo_MinimumIsNotUsable_IsTreatedAsNoMinimum()
        {
            // Arrange
            var placement = new WindowPlacement(10, 10, 200, 100, false);

            // Act
            var fitted = placement.FitTo(0, 0, 1920, 1080, double.NaN, -5);

            // Assert
            Assert.Equal(200, fitted.Width);
            Assert.Equal(100, fitted.Height);
        }
    }
}
