using Servy.Infrastructure.Data;
using System;
using System.Linq;
using Xunit;

namespace Servy.Infrastructure.UnitTests.Data
{
    /// <summary>
    /// Pins the legacy counter-file naming the v10 migration must reproduce to find the files the wrapper wrote in
    /// <c>recovery\</c>, and the way it parses their content. These cases were the wrapper's own
    /// <c>MakeFilenameSafe</c> tests, moved with the function.
    /// </summary>
    public class LegacyRecoveryImporterTests
    {
        #region Null, Empty, and Standard Sanitization Tests

        [Theory]
        [InlineData(null, "_")]
        [InlineData("", "_")]
        public void MakeFilenameSafe_NullOrEmptyInput_ReturnsSafeFallback(string input, string expectedBase)
        {
            // Arrange & Act
            string result = LegacyRecoveryImporter.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith(expectedBase, result);

            // The result length minus the expected base prefix length must equal
            // exactly 6 characters (the length of our deterministic hex short hash).
            Assert.Equal(6, result.Length - expectedBase.Length);
        }

        [Fact]
        public void MakeFilenameSafe_ValidStandardName_AppendsHashSuffix()
        {
            // Arrange
            string input = "service_runtime_log.txt";

            // Act
            string result = LegacyRecoveryImporter.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith("service_runtime_log.txt_", result);
            // Verify hash part length is exactly 6 hex characters
            string hashPart = result.Substring("service_runtime_log.txt_".Length);
            Assert.Equal(6, hashPart.Length);
        }

        [Fact]
        public void MakeFilenameSafe_WithInvalidCharacters_ReplacesThemAndAppendsHash()
        {
            // Arrange
            string input = "log:service/v1*production?.txt";
            string expectedPrefix = "log_service_v1_production_.txt_";

            // Act
            string result = LegacyRecoveryImporter.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        #endregion

        #region DOS Reserved Device Names & Multi-Extension Edge Cases

        [Theory]
        [InlineData("CON")]
        [InlineData("PRN")]
        [InlineData("AUX")]
        [InlineData("NUL")]
        [InlineData("COM1")]
        [InlineData("LPT5")]
        public void MakeFilenameSafe_ExactReservedDeviceName_PrependsUnderscore(string reservedName)
        {
            // Arrange
            string expectedPrefix = "_" + reservedName + "_";

            // Act
            string result = LegacyRecoveryImporter.MakeFilenameSafe(reservedName);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        [Theory]
        [InlineData("CON.log", "_CON.log_")]
        [InlineData("NUL.txt", "_NUL.txt_")]
        [InlineData("LPT1.dat", "_LPT1.dat_")]
        public void MakeFilenameSafe_SingleExtensionReservedDeviceName_PrependsUnderscore(string input, string expectedPrefix)
        {
            // Arrange & Act
            string result = LegacyRecoveryImporter.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        [Theory]
        [InlineData("CON.log.gz", "_CON.log.gz_")]
        [InlineData("NUL.bak.tmp", "_NUL.bak.tmp_")]
        [InlineData("LPT1.foo.bar", "_LPT1.foo.bar_")]
        [InlineData("AUX.spec.json.zip", "_AUX.spec.json.zip_")]
        public void MakeFilenameSafe_MultiExtensionReservedDeviceName_SuccessfullyCatchesAndPrependsUnderscore(string input, string expectedPrefix)
        {
            // Arrange & Act
            string result = LegacyRecoveryImporter.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        [Theory]
        [InlineData("CONSTANT.log", "CONSTANT.log_")]
        [InlineData("NULLED.bak", "NULLED.bak_")]
        [InlineData("COMPASS.json", "COMPASS.json_")]
        [InlineData("A.CON.log", "A.CON.log_")]
        public void MakeFilenameSafe_NamesContainingReservedWordsAsSubstrings(string safeName, string expectedPrefix)
        {
            // Arrange & Act
            string result = LegacyRecoveryImporter.MakeFilenameSafe(safeName);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        #endregion

        #region Disambiguation & Namespace Collision Resolution

        [Theory]
        [InlineData("CON", "_CON_")]
        [InlineData("_CON", "__CON_")]
        [InlineData("__CON", "___CON_")]
        [InlineData("CON.log.gz", "_CON.log.gz_")]
        [InlineData("_CON.log.gz", "__CON.log.gz_")]
        public void MakeFilenameSafe_CollidingNamespaceInputs_ResolvesToUniqueFilenames(string input, string expectedPrefix)
        {
            // Arrange & Act
            string result = LegacyRecoveryImporter.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        [Theory]
        [InlineData("CON  ", "_CON_")]
        [InlineData("CON...", "_CON_")]
        [InlineData("CON.log.gz  ", "_CON.log.gz_")]
        [InlineData("正常_service_name.log.  ", "正常_service_name.log_")]
        public void MakeFilenameSafe_WithTrailingSpacesOrPeriods_NormalizesAndEscapesCorrectly(string input, string expectedPrefix)
        {
            // Arrange & Act
            string result = LegacyRecoveryImporter.MakeFilenameSafe(input);

            // Assert
            Assert.StartsWith(expectedPrefix, result);
        }

        [Fact]
        public void MakeFilenameSafe_TrailingVariationsProduceUniqueOutputs()
        {
            // Arrange: Inputs that would natively collide on Win32 filesystems due to trailing strip behaviors
            string nameBase = "MyService";
            string nameWithSpace = "MyService ";
            string nameWithDot = "MyService.";
            string nameWithSpaces = "MyService   ";

            // Act
            string outBase = LegacyRecoveryImporter.MakeFilenameSafe(nameBase);
            string outSpace = LegacyRecoveryImporter.MakeFilenameSafe(nameWithSpace);
            string outDot = LegacyRecoveryImporter.MakeFilenameSafe(nameWithDot);
            string outSpaces = LegacyRecoveryImporter.MakeFilenameSafe(nameWithSpaces);

            // Assert: Verify that despite trimming, appending original hashes isolates filenames completely.
            // Asserting over the whole set covers all six pairs, including outBase/outSpaces and
            // outDot/outSpaces, and keeps the comparison count correct if a fifth variant is added.
            var all = new[] { outBase, outSpace, outDot, outSpaces };
            Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());

            // All must preserve base readability prefixing
            Assert.All(all, o => Assert.StartsWith("MyService_", o));
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData("...")]
        [InlineData(" \t. ")]
        public void MakeFilenameSafe_PathTraversalAndEmptyTrimsAreNeutralized(string input)
        {
            // Arrange & Act
            string result = LegacyRecoveryImporter.MakeFilenameSafe(input);

            // Assert: Directory traversal markers or blank nodes reduce to safe baseline anchors plus hash codes
            Assert.StartsWith("__", result);
            Assert.False(result.Contains(".."), "Output must not contain directory traversal paths.");
        }

        #endregion

        #region Counter File Name and Content

        [Fact]
        public void GetCounterFileName_AppendsTheLegacySuffixToTheSafeName()
        {
            // Act
            string result = LegacyRecoveryImporter.GetCounterFileName("MyService");

            // Assert: exactly the name the wrapper used, <safe name>_restartAttempts.dat
            Assert.Equal(LegacyRecoveryImporter.MakeFilenameSafe("MyService") + "_restartAttempts.dat", result);
            Assert.EndsWith(LegacyRecoveryImporter.CounterFileSuffix, result);
        }

        [Fact]
        public void MakeFilenameSafe_KnownName_ProducesTheSameNameAsTheWrapperDid()
        {
            // The hash is the first three bytes of the UTF-8 SHA-256 of the raw name, as lowercase hex: a change here
            // would make the migration miss every counter file on disk.
            Assert.Equal("MyService_6fe428", LegacyRecoveryImporter.MakeFilenameSafe("MyService"));
        }

        [Theory]
        [InlineData("0", 0)]
        [InlineData("7", 7)]
        [InlineData(" 12\r\n", 12)]
        [InlineData("", 0)]
        [InlineData(null, 0)]
        [InlineData("abc", 0)]
        [InlineData("-1", 0)]
        [InlineData("99999999999", 0)]
        public void ParseCounter_ReadsANonNegativeIntegerAndTreatsAnythingElseAsZero(string content, int expected)
        {
            Assert.Equal(expected, LegacyRecoveryImporter.ParseCounter(content));
        }

        #endregion
    }
}
