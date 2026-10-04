using Servy.Core.Helpers;
using System;
using System.Collections.Generic;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    /// <summary>
    /// Covers <see cref="SystemEnvironmentHelper"/>: the process-environment pass, the registry fallback for the
    /// placeholders the process does not know (#7393), and the case-insensitive replacement it relies on. The
    /// registry is replaced by a dictionary, so no test depends on the machine's System variables.
    /// </summary>
    public class SystemEnvironmentHelperTests
    {
        /// <summary>
        /// Returns a variable name no process or registry defines, so a placeholder of it is never resolved by accident.
        /// </summary>
        /// <returns>A unique variable name.</returns>
        private static string UniqueName() => "SERVY_TEST_" + Guid.NewGuid().ToString("N");

        /// <summary>
        /// Expands <paramref name="input"/> against <paramref name="system"/>, counting the reads and recording every
        /// process variable set.
        /// </summary>
        /// <param name="input">The string to expand.</param>
        /// <param name="system">The System variables the fake registry returns, or <see langword="null"/>.</param>
        /// <param name="reads">The number of registry reads.</param>
        /// <param name="sets">The process variables set, in order.</param>
        /// <param name="throwOnSet">Whether setting a process variable throws.</param>
        /// <returns>The expanded string.</returns>
        private static string Expand(
            string input,
            Dictionary<string, string> system,
            out int reads,
            out List<KeyValuePair<string, string>> sets,
            bool throwOnSet = false)
        {
            var readCount = 0;
            var recorded = new List<KeyValuePair<string, string>>();
            var result = SystemEnvironmentHelper.ExpandSystemEnvironmentVariables(
                input,
                () => { readCount++; return system; },
                (name, value) =>
                {
                    if (throwOnSet) throw new InvalidOperationException("set refused");
                    recorded.Add(new KeyValuePair<string, string>(name, value));
                });
            reads = readCount;
            sets = recorded;
            return result;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ExpandSystemEnvironmentVariables_NullOrEmpty_ReturnsInputWithoutReadingTheRegistry(string input)
        {
            // Arrange & Act
            var result = Expand(input, new Dictionary<string, string>(), out var reads, out _);

            // Assert
            Assert.Equal(input, result);
            Assert.Equal(0, reads);
        }

        [Fact]
        public void ExpandSystemEnvironmentVariables_NoPlaceholder_ReturnsInputWithoutReadingTheRegistry()
        {
            // Arrange & Act
            var result = Expand(@"C:\Apps\tool.exe", new Dictionary<string, string>(), out var reads, out _);

            // Assert
            Assert.Equal(@"C:\Apps\tool.exe", result);
            Assert.Equal(0, reads);
        }

        [Fact]
        public void ExpandSystemEnvironmentVariables_ProcessVariable_IsExpandedWithoutReadingTheRegistry()
        {
            // Arrange
            var name = UniqueName();
            Environment.SetEnvironmentVariable(name, @"C:\Process");
            try
            {
                // Act
                var result = Expand($@"%{name}%\tool.exe", new Dictionary<string, string> { [name] = @"C:\Registry" }, out var reads, out _);

                // Assert: the process value wins and the registry is never read
                Assert.Equal(@"C:\Process\tool.exe", result);
                Assert.Equal(0, reads);
            }
            finally
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        [Fact]
        public void ExpandSystemEnvironmentVariables_RegistryUnreadable_ReturnsTheProcessExpansion()
        {
            // Arrange
            var name = UniqueName();

            // Act
            var result = Expand($@"%{name}%\tool.exe", null, out var reads, out var sets);

            // Assert
            Assert.Equal($@"%{name}%\tool.exe", result);
            Assert.Equal(1, reads);
            Assert.Empty(sets);
        }

        [Fact]
        public void ExpandSystemEnvironmentVariables_RegistryEmpty_ReturnsTheProcessExpansion()
        {
            // Arrange
            var name = UniqueName();

            // Act
            var result = Expand($@"%{name}%\tool.exe", new Dictionary<string, string>(), out var reads, out var sets);

            // Assert
            Assert.Equal($@"%{name}%\tool.exe", result);
            Assert.Equal(1, reads);
            Assert.Empty(sets);
        }

        [Fact]
        public void ExpandSystemEnvironmentVariables_NewSystemVariable_IsResolvedCaseInsensitivelyAndSetInTheProcess()
        {
            // Arrange: the placeholder's case differs from the registry name's
            var name = UniqueName();
            var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [name] = @"C:\Python312\python.exe" };

            // Act
            var result = Expand($"\"%{name.ToLowerInvariant()}%\" -m app", system, out var reads, out var sets);

            // Assert
            Assert.Equal("\"C:\\Python312\\python.exe\" -m app", result);
            Assert.Equal(1, reads);
            Assert.Equal(new[] { new KeyValuePair<string, string>(name, @"C:\Python312\python.exe") }, sets);
        }

        [Fact]
        public void ExpandSystemEnvironmentVariables_ExpandableRegistryValue_IsExpandedBeforeUseAndBeforeTheProcessSet()
        {
            // Arrange: a REG_EXPAND_SZ value read raw
            var name = UniqueName();
            var windir = Environment.GetEnvironmentVariable("SystemRoot");
            var system = new Dictionary<string, string> { [name] = @"%SystemRoot%\System32" };

            // Act
            var result = Expand($@"%{name}%\cmd.exe", system, out _, out var sets);

            // Assert: neither the result nor the process variable keeps the raw %SystemRoot%
            Assert.Equal($@"{windir}\System32\cmd.exe", result);
            Assert.Equal($@"{windir}\System32", Assert.Single(sets).Value);
        }

        [Fact]
        public void ExpandSystemEnvironmentVariables_NewVariableReferringToAnotherNewVariable_IsResolvedInALaterPass()
        {
            // Arrange: JAVA_BIN refers to JAVA_HOME, both added after the process started. JAVA_HOME is enumerated
            // first, so its placeholder only appears once JAVA_BIN is replaced and needs a second pass
            var home = UniqueName();
            var bin = UniqueName();
            var system = new Dictionary<string, string> { [home] = @"C:\Java\jdk-21", [bin] = $@"%{home}%\bin" };

            // Act
            var result = Expand($@"%{bin}%\java.exe", system, out var reads, out _);

            // Assert
            Assert.Equal(@"C:\Java\jdk-21\bin\java.exe", result);
            Assert.Equal(1, reads);
        }

        [Fact]
        public void ExpandSystemEnvironmentVariables_EmptyRegistryValueOrUnknownName_LeavesThePlaceholder()
        {
            // Arrange
            var empty = UniqueName();
            var unknown = UniqueName();
            var system = new Dictionary<string, string> { [empty] = string.Empty };

            // Act
            var result = Expand($@"%{empty}%\%{unknown}%\50%", system, out _, out var sets);

            // Assert: nothing is replaced, the literal '%' is kept and no process variable is set
            Assert.Equal($@"%{empty}%\%{unknown}%\50%", result);
            Assert.Empty(sets);
        }

        [Fact]
        public void ExpandSystemEnvironmentVariables_ProcessSetThrows_StillReplacesThePlaceholder()
        {
            // Arrange
            var name = UniqueName();
            var system = new Dictionary<string, string> { [name] = @"D:\Tools" };

            // Act
            var result = Expand($@"%{name}%\tool.exe", system, out _, out _, throwOnSet: true);

            // Assert
            Assert.Equal(@"D:\Tools\tool.exe", result);
        }

        [Fact]
        public void GetSystemVariablesMissingFromProcess_RegistryUnreadable_ReturnsEmpty()
        {
            // Arrange & Act
            var missing = SystemEnvironmentHelper.GetSystemVariablesMissingFromProcess(() => null);

            // Assert
            Assert.Empty(missing);
        }

        [Fact]
        public void GetSystemVariablesMissingFromProcess_ReturnsOnlyNonEmptyVariablesTheProcessLacks_Expanded()
        {
            // Arrange: one variable the process has, one it lacks, one empty
            var inherited = UniqueName();
            var added = UniqueName();
            var empty = UniqueName();
            var windir = Environment.GetEnvironmentVariable("SystemRoot");
            Environment.SetEnvironmentVariable(inherited, "process value");
            try
            {
                var system = new Dictionary<string, string>
                {
                    [inherited] = "registry value",
                    [added] = @"%SystemRoot%\Tools",
                    [empty] = string.Empty,
                };

                // Act
                var missing = SystemEnvironmentHelper.GetSystemVariablesMissingFromProcess(() => system);

                // Assert: the inherited value keeps precedence, the empty one is skipped, the new one is expanded
                var entry = Assert.Single(missing);
                Assert.Equal(added, entry.Key);
                Assert.Equal($@"{windir}\Tools", entry.Value);
            }
            finally
            {
                Environment.SetEnvironmentVariable(inherited, null);
            }
        }

        [Theory]
        [InlineData(null, "%A%", "x", null)]
        [InlineData("", "%A%", "x", "")]
        [InlineData("abc", null, "x", "abc")]
        [InlineData("abc", "", "x", "abc")]
        [InlineData("abc", "%A%", "x", "abc")]
        [InlineData("%a%\\%A%\\%a%", "%A%", "v", "v\\v\\v")]
        [InlineData("pre-%A%-post", "%a%", null, "pre--post")]
        public void ReplaceIgnoreCase_ReplacesEveryOccurrenceIgnoringCase(string source, string oldValue, string newValue, string expected)
        {
            // Arrange & Act
            var result = SystemEnvironmentHelper.ReplaceIgnoreCase(source, oldValue, newValue);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
