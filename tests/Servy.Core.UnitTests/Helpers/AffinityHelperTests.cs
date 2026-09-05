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
            Assert.SkipWhen(maxCores < requiredCores,
                $"Host has {maxCores} usable cores; this case needs {requiredCores}.");

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
            Assert.SkipWhen(maxCores < requiredCores,
                $"Host has {maxCores} usable cores; this case needs {requiredCores}.");

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
            // Msg_EmptyAffinityMask and Msg_HexMaskOutOfBounds share the head that Split('{')
            // extracts ("Affinity mask '"), so the formatted message is asserted instead:
            // swapping the two branches of ParseAffinity must not leave this test green.
            string expected = string.Format(Strings.Msg_EmptyAffinityMask, "0x0");

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => AffinityHelper.ParseAffinity("0x0"));

            Assert.Contains(expected, ex.Message);
        }

        [Fact]
        public void ParseAffinity_HexOutOfBounds_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int maxCores = Math.Min(Environment.ProcessorCount, 64);
            string input = "0xFFFFFFFFFFFFFFFF";
            Assert.SkipWhen(maxCores >= 64,
                $"Host has {maxCores} usable cores; 0xFFFFFFFFFFFFFFFF is a valid mask on 64-core hosts.");

            // The static head Split('{') extracts is shared with Msg_EmptyAffinityMask, so the
            // formatted message is asserted instead - see ParseAffinity_ZeroHex.
            string expected = string.Format(Strings.Msg_HexMaskOutOfBounds, input, maxCores - 1);

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(input));

            Assert.Contains(expected, ex.Message);
        }

        [Fact]
        public void ParseAffinity_HexExceedingHostCores_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int maxAllowedCores = Math.Min(Environment.ProcessorCount, 64);
            Assert.SkipWhen(maxAllowedCores >= 64,
                $"Host has {maxAllowedCores} usable cores; this case needs fewer than 64.");

            // Mask that sets a bit beyond host max cores
            long outOfBoundsMask = 1L << maxAllowedCores;
            string input = $"0x{outOfBoundsMask:X}";

            // The static head Split('{') extracts is shared with Msg_EmptyAffinityMask, so the
            // formatted message is asserted instead - see ParseAffinity_ZeroHex.
            string expected = string.Format(Strings.Msg_HexMaskOutOfBounds, input, maxAllowedCores - 1);

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(input));

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
        public void ValidateAffinity_ValidInput_ReturnsTrueAndNullErrorMessage(string? input, int requiredCores)
        {
            // Arrange
            int maxCores = Math.Min(Environment.ProcessorCount, 64);

            // Skip test cases if the current system lacks enough cores to evaluate the input
            Assert.SkipWhen(requiredCores > 0 && maxCores < requiredCores,
                $"Host has {maxCores} usable cores; validating '{input}' requires at least {requiredCores} cores.");

            // Act
            bool isValid = AffinityHelper.ValidateAffinity(input, out string? errorMessage);

            // Assert
            Assert.True(isValid);
            Assert.Null(errorMessage);
        }

        [Theory]
        [InlineData("0xINVALID", nameof(Strings.Msg_InvalidHexAffinityFormat), "0xINVALID")]
        [InlineData("0x0", nameof(Strings.Msg_EmptyAffinityMask), "0x0")]
        [InlineData("abc", nameof(Strings.Msg_InvalidCoreSpecification), "abc")]
        [InlineData("9999", nameof(Strings.Msg_CoreIndexOutOfBounds), "9999")]
        [InlineData(",", nameof(Strings.Msg_InvalidCoreSpecification), ",")]
        [InlineData(",,", nameof(Strings.Msg_InvalidCoreSpecification), ",,")]
        [InlineData(" , ", nameof(Strings.Msg_InvalidCoreSpecification), ",")] // ParseAffinity trims before formatting
        [InlineData("1-0", nameof(Strings.Msg_InvertedCoreRange), "1-0")]
        public void ValidateAffinity_InvalidInput_ReturnsFalseAndPopulatesErrorMessage(string input, string expectedResourceKey, string expectedToken)
        {
            // Arrange
            // Name the exact resource each row is expected to surface: the eight rows reach five
            // different throw sites, and asserting only that the message is non-empty cannot tell
            // them apart, so a wrong-resource regression of the #5911 kind would pass.
            string template = (string)typeof(Strings).GetProperty(expectedResourceKey)!.GetValue(null)!;
            string expected = string.Format(template, expectedToken, Math.Min(Environment.ProcessorCount, 64) - 1);

            // Act
            bool isValid = AffinityHelper.ValidateAffinity(input, out string? errorMessage);

            // Assert
            Assert.False(isValid);
            Assert.NotNull(errorMessage);
            Assert.Contains(expected, errorMessage);
        }
    }
}
