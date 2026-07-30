using Servy.Core.Helpers;
using Servy.Core.Resources;

namespace Servy.Core.UnitTests.Helpers
{
    public class AffinityHelperTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseAffinity_NullOrWhiteSpace_ReturnsIntPtrZero(string? input)
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
        [InlineData("0,2,4", 21L, 5)]     // 1 + 4 + 16 = 21
        [InlineData("0-3,8", 271L, 9)]    // 15 + 256 = 271
        [InlineData("0-1, 2-3", 15L, 4)]  // 1 + 2 + 4 + 8 = 15
        [InlineData(" 0-2 , 4 ", 23L, 5)] // 7 + 16 = 23
        public void ParseAffinity_Valid(string input, long expectedMask, int requiredCores)
        {
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
        [InlineData("0x123456789ABCDEF0123")] // Exceeds long.MaxValue limits
        public void ParseAffinity_InvalidHex_ThrowsArgumentException(string input)
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => AffinityHelper.ParseAffinity(input));

            // Extract the static format prefix from the localized template
            string expectedPrefix = Strings.Msg_InvalidHexAffinityFormat.Split('{')[0];
            Assert.Contains(expectedPrefix, ex.Message);
        }

        [Fact]
        public void ParseAffinity_ValidSingleCoresAndRanges_ReturnsExpectedBitmask()
        {
            int maxCores = Math.Min(Environment.ProcessorCount, 64);
            Assert.SkipWhen(maxCores < 2, "Guard for single-core execution environments.");

            // Act: Core 0 and Core 1 -> (1 << 0) | (1 << 1) = 3
            IntPtr result = AffinityHelper.ParseAffinity("0, 1");

            // Assert
            Assert.Equal(new IntPtr(3L), result);

            // Act: Range "0-1" -> 3
            IntPtr rangeResult = AffinityHelper.ParseAffinity("0-1");

            // Assert
            Assert.Equal(new IntPtr(3L), rangeResult);
        }

        [Fact]
        public void ParseAffinity_ComplexRangesAndLists_ReturnsExpectedBitmask()
        {
            int maxCores = Math.Min(Environment.ProcessorCount, 64);

            // Test case: "0,2,4" -> (1<<0) | (1<<2) | (1<<4) = 1 + 4 + 16 = 21
            Assert.SkipWhen(maxCores < 5, $"Host has {maxCores} usable cores; complex range tests require at least 5 cores.");

            IntPtr result1 = AffinityHelper.ParseAffinity("0,2,4");
            Assert.Equal(new IntPtr(21L), result1);

            // Test case: " 0-2 , 4 " with leading/trailing spaces -> 7 + 16 = 23
            IntPtr result2 = AffinityHelper.ParseAffinity(" 0-2 , 4 ");
            Assert.Equal(new IntPtr(23L), result2);

            // Test case: "0-1, 2-3" with spaces -> 15
            IntPtr result3 = AffinityHelper.ParseAffinity("0-1, 2-3");
            Assert.Equal(new IntPtr(15L), result3);

            // Test case: "0-3,8" -> (1<<0 | 1<<1 | 1<<2 | 1<<3) | (1<<8) = 15 + 256 = 271
            Assert.SkipWhen(maxCores < 9, $"Host has {maxCores} usable cores; high core index case (8) requires at least 9 cores.");

            IntPtr result4 = AffinityHelper.ParseAffinity("0-3,8");
            Assert.Equal(new IntPtr(271L), result4);
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
            int maxAllowedCores = Math.Min(Environment.ProcessorCount, 64);

            // Single core out of bounds
            string singleOutOfBounds = maxAllowedCores.ToString();
            var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(singleOutOfBounds));
            string singleExpectedPrefix = Strings.Msg_CoreIndexOutOfBounds.Split('{')[0];
            Assert.Contains(singleExpectedPrefix, ex1.Message);

            // Range start out of bounds
            string rangeOutOfBounds = $"{maxAllowedCores}-{maxAllowedCores + 1}";
            var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity(rangeOutOfBounds));
            string rangeExpectedPrefix = Strings.Msg_CoreIndexRangeOutOfBounds.Split('{')[0];
            Assert.Contains(rangeExpectedPrefix, ex2.Message);

            // Inverted range (start > end)
            Assert.SkipWhen(maxAllowedCores < 2, $"Host has {maxAllowedCores} usable cores; inverted range validation requires at least 2 cores.");
            var ex3 = Assert.Throws<ArgumentOutOfRangeException>(() => AffinityHelper.ParseAffinity("1-0"));
            Assert.Contains(rangeExpectedPrefix, ex3.Message);
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("   ", 0)]
        [InlineData("0", 1)]
        [InlineData("0x1", 1)]
        [InlineData("0,2,4", 5)]
        [InlineData("0-3,8", 9)]
        public void ValidateAffinity_ValidInput_ReturnsTrueAndNullErrorMessage(string? input, int requiredCores)
        {
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
        [InlineData("0xINVALID")]
        [InlineData("abc")]
        [InlineData("9999")]
        public void ValidateAffinity_InvalidInput_ReturnsFalseAndPopulatesErrorMessage(string input)
        {
            // Act
            bool isValid = AffinityHelper.ValidateAffinity(input, out string? errorMessage);

            // Assert
            Assert.False(isValid);
            Assert.NotNull(errorMessage);
            Assert.NotEmpty(errorMessage);
        }
    }
}