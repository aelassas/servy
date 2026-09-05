using Servy.Core.Helpers;
using Servy.Core.Resources;
using System;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class AffinityHelperTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void ParseAffinity_NullOrWhiteSpace_ReturnsIntPtrZero(string input)
        {
            // Act
            IntPtr result = AffinityHelper.ParseAffinity(input);

            // Assert
            Assert.Equal(IntPtr.Zero, result);
        }

        [Theory]
        [InlineData("0x1", 0x1L, 1)]
        [InlineData("0X2", 0x2L, 2)]
        [InlineData("0xFF", 0xFFL, 8)]
        [InlineData(" 0x10 ", 0x10L, 5)]
        public void ParseAffinity_ValidHex_ReturnsExpectedBitmask(string input, long expectedMask, int requiredCores)
        {
            // Arrange
            int maxCores = Math.Min(Environment.ProcessorCount, 64);
            if (maxCores < requiredCores) return; // Skip if host has fewer cores than required

            // Act
            IntPtr result = AffinityHelper.ParseAffinity(input);

            // Assert
            Assert.Equal(new IntPtr(expectedMask), result);
        }

        [Theory]
        [InlineData("0, 1", 3L, 2)]        // 1 + 2 = 3
        [InlineData("0-1", 3L, 2)]         // 1 + 2 = 3
        [InlineData("0,2,4", 21L, 5)]      // 1 + 4 + 16 = 21
        [InlineData("0-3,8", 271L, 9)]     // 15 + 256 = 271
        [InlineData("0-1, 2-3", 15L, 4)]   // 1 + 2 + 4 + 8 = 15
        [InlineData(" 0-2 , 4 ", 23L, 5)]  // 7 + 16 = 23
        public void ParseAffinity_Valid(string input, long expectedMask, int requiredCores)
        {
            // Arrange
            int maxCores = Math.Min(Environment.ProcessorCount, 64);
            if (maxCores < requiredCores) return; // Skip if host has fewer cores than required

            // Act
            IntPtr result = AffinityHelper.ParseAffinity(input);

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
            var ex = Assert.Throws<ArgumentException>(() => AffinityHelper.ParseAffinity(input));

            // Extract the static format prefix from the localized template
            Assert.Contains(expectedPrefix, ex.Message);
        }

        [Fact]
        public void ParseAffinity_ZeroHex_ThrowsArgumentException()
        {
            // Arrange
            string expectedPrefix = Strings.Msg_EmptyAffinityMask.Split('{')[0];

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => AffinityHelper.ParseAffinity("0x0"));

            Assert.Contains(expectedPrefix, ex.Message);
        }

        [Fact]
        public void ParseAffinity_HexOutOfBounds_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int maxCores = Math.Min(Environment.ProcessorCount, 64);
            string input = "0xFFFFFFFFFFFFFFFF";
            if (maxCores >= 64) return; // Skip if host has full 64 cores and input is all bits set

            string expectedPrefix = Strings.Msg_HexMaskOutOfBounds.Split('{')[0];

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(input));

            Assert.Contains(expectedPrefix, ex.Message);
        }

        [Fact]
        public void ParseAffinity_HexExceedingHostCores_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int maxAllowedCores = Math.Min(Environment.ProcessorCount, 64);
            if (maxAllowedCores >= 64) return; // Skip if host has full 64 cores

            // Mask that sets a bit beyond host max cores
            long outOfBoundsMask = 1L << maxAllowedCores;
            string input = $"0x{outOfBoundsMask:X}";
            string expectedPrefix = Strings.Msg_HexMaskOutOfBounds.Split('{')[0];

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(input));

            Assert.Contains(expectedPrefix, ex.Message);
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
            var ex = Assert.Throws<ArgumentException>(() => AffinityHelper.ParseAffinity(input));

            Assert.Contains(expectedPrefix, ex.Message);
        }

        [Fact]
        public void ParseAffinity_InvertedRange_ThrowsArgumentException()
        {
            // Arrange
            string expectedPrefix = Strings.Msg_InvertedCoreRange.Split('{')[0];

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => AffinityHelper.ParseAffinity("1-0"));

            Assert.Contains(expectedPrefix, ex.Message);
        }

        [Fact]
        public void ParseAffinity_CoreIndexOutOfRange_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int maxAllowedCores = Math.Min(Environment.ProcessorCount, 64);

            // Act & Assert - Single core out of bounds
            string singleOutOfBounds = maxAllowedCores.ToString();
            var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(singleOutOfBounds));
            string singleExpectedPrefix = Strings.Msg_CoreIndexOutOfBounds.Split('{')[0];
            Assert.Contains(singleExpectedPrefix, ex1.Message);

            // Act & Assert - Range start/end out of bounds
            string rangeOutOfBounds = $"{maxAllowedCores}-{maxAllowedCores + 1}";
            var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(rangeOutOfBounds));
            string rangeExpectedPrefix = Strings.Msg_CoreIndexRangeOutOfBounds.Split('{')[0];
            Assert.Contains(rangeExpectedPrefix, ex2.Message);
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("    ", 0)]
        [InlineData("0", 1)]
        [InlineData("0x1", 1)]
        [InlineData("0,2,4", 5)]
        [InlineData("0-3,8", 9)]
        public void ValidateAffinity_ValidInput_ReturnsTrueAndNullErrorMessage(string input, int requiredCores)
        {
            // Arrange
            int maxCores = Math.Min(Environment.ProcessorCount, 64);

            // Skip test cases if the current system lacks enough cores to evaluate the input
            if (requiredCores > 0 && maxCores < requiredCores) return; // Skip if host has fewer cores than required

            // Act
            bool isValid = AffinityHelper.ValidateAffinity(input, out string errorMessage);

            // Assert
            Assert.True(isValid);
            Assert.Null(errorMessage);
        }

        [Theory]
        [InlineData("0xINVALID")]
        [InlineData("0x0")]
        [InlineData("abc")]
        [InlineData("9999")]
        [InlineData(",")]
        [InlineData(",,")]
        [InlineData(" , ")]
        [InlineData("1-0")]
        public void ValidateAffinity_InvalidInput_ReturnsFalseAndPopulatesErrorMessage(string input)
        {
            // Arrange (N/A)

            // Act
            bool isValid = AffinityHelper.ValidateAffinity(input, out string errorMessage);

            // Assert
            Assert.False(isValid);
            Assert.NotNull(errorMessage);
            Assert.NotEmpty(errorMessage);
        }
    }
}
