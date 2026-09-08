using Servy.Core.Helpers;
using Servy.Core.Resources;

namespace Servy.Core.UnitTests.Helpers
{
    public class AffinityHelperTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void ParseAffinity_NullOrWhiteSpace_ReturnsIntPtrZero(string? input)
        {
            // Act
            IntPtr result = AffinityHelper.ParseAffinity(input, 64);

            // Assert
            Assert.Equal(IntPtr.Zero, result);
        }

        [Fact]
        public void ParseAffinity_PublicOverload_UsesHostProcessorCountBoundedAt64()
        {
            // Arrange
            int expectedMaxCores = Math.Min(Environment.ProcessorCount, 64);

            // Act & Assert: Valid input within host bounds succeeds
            IntPtr validResult = AffinityHelper.ParseAffinity("0");
            Assert.Equal(new IntPtr(1L), validResult);

            // Act & Assert: Input exceeding host bounds throws
            string outOfBoundsInput = expectedMaxCores.ToString();
            Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(outOfBoundsInput));
        }

        [Theory]
        [InlineData("0x1", 0x1L, 16)]
        [InlineData("0X2", 0x2L, 16)]
        [InlineData("0xFF", 0xFFL, 16)]
        [InlineData(" 0x10 ", 0x10L, 16)]
        [InlineData("0xFFFFFFFFFFFFFFFF", -1L, 64)] // Tests 64-core max allowed ternary branch (-1L mask)
        public void ParseAffinity_ValidHex_ReturnsExpectedBitmask(string input, long expectedMask, int maxAllowedCores)
        {
            // Act
            IntPtr result = AffinityHelper.ParseAffinity(input, maxAllowedCores);

            // Assert
            Assert.Equal(new IntPtr(expectedMask), result);
        }

        [Theory]
        [InlineData("0, 1", 3L, 8)]         // 1 + 2 = 3
        [InlineData("0-1", 3L, 8)]          // 1 + 2 = 3
        [InlineData("0,2,4", 21L, 8)]       // 1 + 4 + 16 = 21
        [InlineData("0-3,8", 271L, 16)]      // 15 + 256 = 271
        [InlineData("0-1, 2-3", 15L, 8)]    // 1 + 2 + 4 + 8 = 15
        [InlineData(" 0-2 , 4 ", 23L, 8)]   // 7 + 16 = 23
        public void ParseAffinity_Valid(string input, long expectedMask, int maxAllowedCores)
        {
            // Act
            IntPtr result = AffinityHelper.ParseAffinity(input, maxAllowedCores);

            // Assert
            Assert.Equal(new IntPtr(expectedMask), result);
        }

        [Theory]
        [InlineData("0xG12")]
        [InlineData("0xXYZ")]
        [InlineData("0x123456789ABCDEF0123")] // Exceeds long limits
        public void ParseAffinity_InvalidHex_ThrowsArgumentException(string input)
        {
            // Arrange
            string expectedPrefix = Strings.Msg_InvalidHexAffinityFormat.Split('{')[0];

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => AffinityHelper.ParseAffinity(input, 64));

            // Extract the static format prefix from the localized template
            Assert.Contains(expectedPrefix, ex.Message);
        }

        [Fact]
        public void ParseAffinity_ZeroHex_ThrowsArgumentException()
        {
            // Arrange
            string expected = string.Format(Strings.Msg_EmptyAffinityMask, "0x0");

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => AffinityHelper.ParseAffinity("0x0", 64));

            Assert.Contains(expected, ex.Message);
        }

        [Fact]
        public void ParseAffinity_HexOutOfBounds_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int maxAllowedCores = 8;
            string input = "0xFFFFFFFFFFFFFFFF"; // Requires 64 cores
            string expected = string.Format(Strings.Msg_HexMaskOutOfBounds, input, maxAllowedCores - 1);

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(input, maxAllowedCores));

            Assert.Contains(expected, ex.Message);
        }

        [Fact]
        public void ParseAffinity_HexExceedingHostCores_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int maxAllowedCores = 8;
            long outOfBoundsMask = 1L << maxAllowedCores;
            string input = $"0x{outOfBoundsMask:X}";
            string expected = string.Format(Strings.Msg_HexMaskOutOfBounds, input, maxAllowedCores - 1);

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(input, maxAllowedCores));

            Assert.Contains(expected, ex.Message);
        }

        [Theory]
        [InlineData("-1")] // Leading minus: empty range start, not a negative core index
        [InlineData("0-")] // Malformed range
        [InlineData("0-1-2")] // Too many dashes
        [InlineData("abc")] // Non-numeric token
        [InlineData("0, abc")] // Mixed invalid token
        [InlineData("0-abc")] // Non-numeric end range
        [InlineData("abc-1")] // Non-numeric start range
        [InlineData(",")] // Comma-only input
        [InlineData(",,")] // Multiple commas
        [InlineData(" , ")] // Comma with whitespace
        public void ParseAffinity_InvalidTokenOrRangeSyntax_ThrowsArgumentException(string input)
        {
            // Arrange
            string expectedPrefix = Strings.Msg_InvalidCoreSpecification.Split('{')[0];

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => AffinityHelper.ParseAffinity(input, 64));

            Assert.Contains(expectedPrefix, ex.Message);
        }

        [Fact]
        public void ParseAffinity_InvertedRange_ThrowsArgumentException()
        {
            // Arrange
            string expectedPrefix = Strings.Msg_InvertedCoreRange.Split('{')[0];

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => AffinityHelper.ParseAffinity("1-0", 64));

            Assert.Contains(expectedPrefix, ex.Message);
        }

        [Fact]
        public void ParseAffinity_CoreIndexOutOfRange_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int maxAllowedCores = 8;

            // Act & Assert - Single core out of bounds
            string singleOutOfBounds = maxAllowedCores.ToString();
            var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(singleOutOfBounds, maxAllowedCores));
            string singleExpectedPrefix = Strings.Msg_CoreIndexOutOfBounds.Split('{')[0];
            Assert.Contains(singleExpectedPrefix, ex1.Message);

            // Act & Assert - Range start/end out of bounds
            string rangeOutOfBounds = $"{maxAllowedCores}-{maxAllowedCores + 1}";
            var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(rangeOutOfBounds, maxAllowedCores));
            string rangeExpectedPrefix = Strings.Msg_CoreIndexRangeOutOfBounds.Split('{')[0];
            Assert.Contains(rangeExpectedPrefix, ex2.Message);
        }

        [Theory]
        [InlineData(null, 8)]
        [InlineData("", 8)]
        [InlineData("    ", 8)]
        [InlineData("0", 8)]
        [InlineData("0x1", 8)]
        [InlineData("0,2,4", 8)]
        [InlineData("0-3,8", 16)]
        public void ValidateAffinity_ValidInput_ReturnsTrueAndNullErrorMessage(string? input, int maxAllowedCores)
        {
            // Act
            bool isValid = AffinityHelper.ValidateAffinity(input, maxAllowedCores, out string? errorMessage);

            // Assert
            Assert.True(isValid);
            Assert.Null(errorMessage);
        }

        [Theory]
        [InlineData("0xINVALID", nameof(Strings.Msg_InvalidHexAffinityFormat), "0xINVALID", 64)]
        [InlineData("0x0", nameof(Strings.Msg_EmptyAffinityMask), "0x0", 64)]
        [InlineData("abc", nameof(Strings.Msg_InvalidCoreSpecification), "abc", 64)]
        [InlineData("9999", nameof(Strings.Msg_CoreIndexOutOfBounds), "9999", 64)]
        [InlineData(",", nameof(Strings.Msg_InvalidCoreSpecification), ",", 64)]
        [InlineData(",,", nameof(Strings.Msg_InvalidCoreSpecification), ",,", 64)]
        [InlineData(" , ", nameof(Strings.Msg_InvalidCoreSpecification), ",", 64)] // ParseAffinity trims before formatting
        [InlineData("1-0", nameof(Strings.Msg_InvertedCoreRange), "1-0", 64)]
        public void ValidateAffinity_InvalidInput_ReturnsFalseAndPopulatesErrorMessage(string input, string expectedResourceKey, string expectedToken, int maxAllowedCores)
        {
            // Arrange
            string template = (string)typeof(Strings).GetProperty(expectedResourceKey)!.GetValue(null)!;
            string expected = string.Format(template, expectedToken, maxAllowedCores - 1);

            // Act
            bool isValid = AffinityHelper.ValidateAffinity(input, maxAllowedCores, out string? errorMessage);

            // Assert
            Assert.False(isValid);
            Assert.NotNull(errorMessage);
            Assert.Contains(expected, errorMessage);
            Assert.DoesNotContain("(Parameter 'affinityInput')", errorMessage);
        }
    }
}
