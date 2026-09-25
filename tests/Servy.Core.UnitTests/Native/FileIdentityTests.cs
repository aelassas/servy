using Servy.Core.Native;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Servy.Core.UnitTests.Native
{
    /// <summary>
    /// Unit tests for <see cref="FILE_IDENTITY.IsDifferentFrom"/>, a pure in-memory
    /// comparison of four struct fields. The whole truth table of the method lives here, so a
    /// coverage audit of one branch never has to be answered from two projects.
    /// </summary>
    public class FileIdentityTests
    {
        [Fact]
        public void FileIdentity_FileTrackingStructures_ValidatesEqualityAndRotationDifferences()
        {
            // Arrange
            var idA = new FILE_IDENTITY
            {
                FileIndex = 12345,
                VolumeSerialNumber = 98765,
                PrefixDigest = "ABCDE",
                IsValidHandleInfo = true
            };

            var idB = new FILE_IDENTITY
            {
                FileIndex = 12345,
                VolumeSerialNumber = 98765,
                PrefixDigest = "ABCDE",
                IsValidHandleInfo = true
            };

            var idC = new FILE_IDENTITY
            {
                FileIndex = 54321, // Differs by index path
                VolumeSerialNumber = 98765,
                PrefixDigest = "ABCDE",
                IsValidHandleInfo = true
            };

            // Assert behavior when VolumeSerialNumber differs independently
            var idVolumeMismatch = new FILE_IDENTITY
            {
                FileIndex = 12345,
                VolumeSerialNumber = 11111, // Differs by serial path
                PrefixDigest = "ABCDE",
                IsValidHandleInfo = true
            };

            // Act & Assert
            Assert.False(idA.IsDifferentFrom(idB), "Identical primary handles must evaluate as the same file object.");
            Assert.True(idA.IsDifferentFrom(idC), "Varying FileIndex should indicate a rotation event.");
            Assert.True(idA.IsDifferentFrom(idVolumeMismatch), "Varying VolumeSerialNumber should indicate a volume/rotation move event.");
        }

        [Fact]
        public void FileIdentity_HandleValidityMismatch_ReturnsTrue()
        {
            // Arrange
            // Unnumbered Guard: Validation status asymmetry must trigger immediate difference flag
            var idValid = new FILE_IDENTITY { IsValidHandleInfo = true, PrefixDigest = "SAME" };
            var idInvalid = new FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = "SAME" };

            // Act & Assert
            Assert.True(idValid.IsDifferentFrom(idInvalid), "Handle info status inequality must evaluate as structurally different file tracks.");
        }

        [Fact]
        public void FileIdentity_SecondaryProbeFAT32Fallback_ValidatesDigestEquality()
        {
            // Arrange
            // Branch (2) Secondary Probe: Both handle checks fail (e.g. FAT32 volume layers). Compare contents using PrefixDigest
            var baseId = new FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = "MD5_HASH_A" };
            var matchingId = new FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = "MD5_HASH_A" };
            var differingId = new FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = "MD5_HASH_B" };

            // Branch (2) Secondary Probe Asymmetric Null: one side has no digest - the conjunction guard must fail,
            // falling through to Branch (3) Fallback rather than attempting string comparison against null.
            var digestMissing = new FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = null! };

            // Act & Assert
            Assert.False(baseId.IsDifferentFrom(matchingId), "Identical content hashes on invalid handle states must evaluate as unchanged.");
            Assert.True(baseId.IsDifferentFrom(differingId), "Differing content hashes on invalid handle states must trigger a rotation switch signal.");
            Assert.True(baseId.IsDifferentFrom(digestMissing), "Asymmetric null digest must fall through conjunction guard to return true.");
            Assert.True(digestMissing.IsDifferentFrom(baseId), "Symmetric reverse null digest must fall through conjunction guard to return true.");
        }

        [Fact]
        public void FileIdentity_IsDifferentFrom_EmptyFilesWithEmptyStringDigests_ReturnsFalse()
        {
            // Arrange: Two identities where handle info failed (IsValidHandleInfo=false)
            // but content probes succeeded on empty files (PrefixDigest=string.Empty).
            var identity1 = new FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = string.Empty };
            var identity2 = new FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = string.Empty };

            // Act
            bool isDifferent = identity1.IsDifferentFrom(identity2);

            // Assert: Compares equal (returns false) because both content probes succeeded on empty files
            Assert.False(isDifferent);
        }

        [Fact]
        public void FileIdentity_UndeterminableStateFallback_DefaultsToTrue()
        {
            // Arrange
            // Branch (3) Fallback: No robust identifiers available on either side. Should report 'true' as a safe default.
            var blindIdA = new FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = null! };
            var blindIdB = new FILE_IDENTITY { IsValidHandleInfo = false, PrefixDigest = null! };

            // Act & Assert
            Assert.True(blindIdA.IsDifferentFrom(blindIdB), "Undeterminable file identities must fall back to 'true' to safely force metadata loop updates.");
        }

        /// <summary>
        /// Guards what makes every assertion above count: <see cref="FILE_IDENTITY"/> must stay
        /// outside <see cref="NativeMethods"/>, whose class-level
        /// <see cref="ExcludeFromCodeCoverageAttribute"/> covers its nested types as well. Nesting
        /// the struct back, or marking it directly, removes <c>IsDifferentFrom</c> from the coverage
        /// report again and the truth table above silently stops being measured.
        /// </summary>
        [Fact]
        public void FileIdentity_IsDifferentFrom_IsNotExcludedFromCodeCoverage()
        {
            // Arrange
            var method = typeof(FILE_IDENTITY).GetMethod(nameof(FILE_IDENTITY.IsDifferentFrom));
            Assert.NotNull(method);

            // Act
            var exclusions = new List<string>();
            if (method.GetCustomAttribute<ExcludeFromCodeCoverageAttribute>() != null)
            {
                exclusions.Add(nameof(FILE_IDENTITY.IsDifferentFrom));
            }

            for (var declaring = method.DeclaringType; declaring != null; declaring = declaring.DeclaringType)
            {
                if (declaring.GetCustomAttribute<ExcludeFromCodeCoverageAttribute>() != null)
                {
                    exclusions.Add(declaring.FullName ?? declaring.Name);
                }
            }

            // Assert
            Assert.True(
                exclusions.Count == 0,
                "FILE_IDENTITY.IsDifferentFrom is excluded from the coverage report by "
                + $"[ExcludeFromCodeCoverage] on: {string.Join(", ", exclusions)}. "
                + "The tests in this class then assert real behaviour but contribute nothing to the "
                + "published coverage number, which is what issue 6985 reported.");
        }
    }
}
