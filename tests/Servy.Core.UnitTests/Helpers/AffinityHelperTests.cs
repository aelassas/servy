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
        [InlineData("   ")]
        public void ParseAffinity_NullOrWhiteSpace_ReturnsIntPtrZero(string input)
        {
            // Act
            IntPtr result = AffinityHelper.ParseAffinity(input);

            // Assert
            Assert.Equal(IntPtr.Zero, result);
        }

        [Theory]
        [InlineData("0x1", 0x1L)]
        [InlineData("0X2", 0x2L)]
        [InlineData("0xFF", 0xFFL)]
        [InlineData(" 0x10 ", 0x10L)]
        public void ParseAffinity_ValidHex_ReturnsExpectedBitmask(string input, long expectedMask)
        {
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
            if (maxCores < requiredCores)
            {
                // Skip execution if host lacks required core count for input validation
                return;
            }

            // Act
            IntPtr result = AffinityHelper.ParseAffinity(input);

            // Assert
            Assert.Equal(new IntPtr(expectedMask), result);
        }

        [Theory]
        [InlineData("0xG12")]
        [InlineData("0xXYZ")]
        [InlineData("0x123456789ABCDEF0123")] // Exceeds long.MaxValue limits
        public void ParseAffinity_InvalidHex_ThrowsArgumentException(string input)
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => AffinityHelper.ParseAffinity(input));

            // Extract the static format prefix from the localized template
            string expectedPrefix = Strings.Msg_InvalidHexAffinityFormat.Split('{')[0];
            Assert.Contains(expectedPrefix, ex.Message);
        }

        [Theory]
        [InlineData("-1")] // Core < 0
        [InlineData("0-")] // Malformed range
        [InlineData("0-1-2")] // Too many dashes
        [InlineData("abc")] // Non-numeric token
        [InlineData("0, abc")] // Mixed invalid token
        [InlineData("0-abc")] // Non-numeric end range
        [InlineData("abc-1")] // Non-numeric start range
        public void ParseAffinity_InvalidTokenOrRangeSyntax_ThrowsArgumentException(string input)
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => AffinityHelper.ParseAffinity(input));

            string expectedPrefix = Strings.Msg_InvalidCoreSpecification.Split('{')[0];
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

            // Act & Assert - Range start out of bounds
            string rangeOutOfBounds = $"{maxAllowedCores}-{maxAllowedCores + 1}";
            var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(rangeOutOfBounds));
            string rangeExpectedPrefix = Strings.Msg_CoreIndexRangeOutOfBounds.Split('{')[0];
            Assert.Contains(rangeExpectedPrefix, ex2.Message);

            // Act & Assert - Inverted range (start > end)
            if (maxAllowedCores >= 2)
            {
                var ex3 = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity("1-0"));
                Assert.Contains(rangeExpectedPrefix, ex3.Message);
            }
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("   ", 0)]
        [InlineData("0", 1)]
        [InlineData("0x1", 1)]
        [InlineData("0,2,4", 5)]
        [InlineData("0-3,8", 9)]
        public void ValidateAffinity_ValidInput_ReturnsTrueAndNullErrorMessage(string input, int requiredCores)
        {
            // Arrange
            int maxCores = Math.Min(Environment.ProcessorCount, 64);

            // Skip test cases if the current system lacks enough cores to evaluate the input
            if (requiredCores > 0 && maxCores < requiredCores)
            {
                return;
            }

            // Act
            bool isValid = AffinityHelper.ValidateAffinity(input, out string errorMessage);

            // Assert
            Assert.True(isValid);
            Assert.Null(errorMessage);
        }

        [Theory]
        [InlineData("0xINVALID")]
        [InlineData("abc")]
        [InlineData("9999")]
        public void ValidateAffinity_InvalidInput_ReturnsFalseAndPopulatesErrorMessage(string input)
        {
            // Act
            bool isValid = AffinityHelper.ValidateAffinity(input, out string errorMessage);

            // Assert
            Assert.False(isValid);
            Assert.NotNull(errorMessage);
            Assert.NotEmpty(errorMessage);
        }
    }
}