using Servy.Core.Helpers;
using Servy.Testing;

namespace Servy.Core.UnitTests.Helpers
{
    /// <summary>
    /// Unit tests for the ProcessKiller utility (input validation, the critical-process safelist, and not-found logic).
    /// Integration tests that spawn real processes live in ProcessKillerIntegrationTests.
    /// </summary>
    public class ProcessKillerTests
    {
        private readonly IProcessKiller _processKiller;

        public ProcessKillerTests()
        {
            _processKiller = new ProcessKiller();
        }

        #region Unit Tests (Validation & Logic)

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void KillProcessTreeAndParents_InvalidInput_ReturnsFalse(string? name)
        {
            // Act
            var result = _processKiller.KillProcessTreeAndParents(name!);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Supplies every entry of the ProcessKiller safelist, so the coverage of the guard cannot drift from the list it guards.
        /// </summary>
        /// <returns>One theory row per entry of the CriticalSystemProcesses safelist.</returns>
        public static TheoryData<string> AllCriticalProcessNames()
        {
            // GetFieldStatic throws when the field is renamed or removed, so the theory can never
            // degrade silently into an empty (and therefore vacuously green) data set.
            var safelist = TestReflection.GetFieldStatic<HashSet<string>>(
                typeof(ProcessKiller), "CriticalSystemProcesses");

            var data = new TheoryData<string>();
            foreach (var name in safelist)
            {
                data.Add(name);
            }

            return data;
        }

        /// <summary>
        /// Verifies that attempting to terminate a critical Windows system process by name is actively blocked by the internal guardrails.
        /// </summary>
        /// <param name="protectedName">The name of the critical system process, taken from the safelist itself.</param>
        [Theory]
        [MemberData(nameof(AllCriticalProcessNames))]
        public void KillProcessTreeAndParents_ProtectedProcessName_ReturnsFalse(string protectedName)
        {
            // Act
            // Every entry must be refused with and without the .exe suffix the guard normalizes away,
            // whether or not the process happens to be running on this host.
            var result = _processKiller.KillProcessTreeAndParents(protectedName, killParents: true);
            var resultWithExtension = _processKiller.KillProcessTreeAndParents(protectedName + ".exe", killParents: true);

            // Assert
            // Names on the CriticalSystemProcesses safelist are never killed
            Assert.False(result);
            Assert.False(resultWithExtension);
        }

        /// <summary>
        /// Verifies that providing an invalid or non-positive process identifier results in a safe bypass returning false.
        /// </summary>
        /// <param name="invalidPid">The numerical identifier simulating an invalid process ID.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-999)]
        public void KillProcessTreeAndParents_InvalidPid_ReturnsFalse(int invalidPid)
        {
            // Act
            var result = _processKiller.KillProcessTreeAndParents(invalidPid, killParents: true);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void KillProcessTreeAndParents_ProcessNotFound_ReturnsTrue()
        {
            // Act
            var result = _processKiller.KillProcessTreeAndParents("NonExistentProcess_Unique_999");

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("app.exe", "app")]
        [InlineData("APP.EXE", "APP")]
        [InlineData("App.Exe", "App")]
        [InlineData("app", "app")]
        public void StripExe_ShouldNormalizeExtensionsCaseInsensitively(string input, string expected)
        {
            // Arrange & Act
            // Invoke the private 'StripExe' static method directly using the TestReflection engine.
            // This bypasses live process-table dependencies and targets the text manipulation algorithm directly.
            string? actual = TestReflection.InvokeNonPublicStatic(typeof(ProcessKiller), "StripExe", input) as string;

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void KillProcessesUsingFile_InvalidInput_ReturnsTrue(string? path)
            => Assert.True(_processKiller.KillProcessesUsingFile(path!));

        [Fact]
        public void KillProcessesUsingFile_MissingFile_ReturnsTrue()
        {
            // Arrange
            string fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");

            // Act
            var result = _processKiller.KillProcessesUsingFile(fakePath);

            // Assert
            // Missing target file means nothing to kill: the method logs at Info and reports success.
            Assert.True(result);
        }

        #endregion

    }
}
