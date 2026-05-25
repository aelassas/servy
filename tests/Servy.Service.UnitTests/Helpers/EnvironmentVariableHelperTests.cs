using Servy.Core.Config;
using Servy.Core.EnvironmentVariables;
using Servy.Service.Helpers;
using System;
using System.Collections.Generic;
using Xunit;

namespace Servy.Service.UnitTests.Helpers
{
    // Running sequentially to prevent Environment.SetEnvironmentVariable calls 
    // from causing race conditions across different tests.
    [CollectionDefinition("SequentialEnvTests", DisableParallelization = true)]
    public class SequentialEnvTestsCollection { }

    [Collection("SequentialEnvTests")]
    public class EnvironmentVariableHelperTests : IDisposable
    {
        // Tracks temporarily modified OS environment variables to restore them after each test
        private readonly List<string> _modifiedOsVars = new List<string>();

        public void Dispose()
        {
            // Cleanup OS environment state to ensure pristine runs for subsequent tests
            foreach (var varName in _modifiedOsVars)
            {
                Environment.SetEnvironmentVariable(varName, null);
            }
        }

        private void SetTempOsVariable(string name, string value)
        {
            Environment.SetEnvironmentVariable(name, value);
            if (!_modifiedOsVars.Contains(name))
            {
                _modifiedOsVars.Add(name);
            }
        }

        #region Input Guards & Null Handling

        [Fact]
        public void ExpandEnvironmentVariables_NullList_ReturnsSystemVariables()
        {
            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(null!);

            // Assert
            Assert.NotNull(expanded);
            Assert.True(expanded.ContainsKey("PATH") || expanded.ContainsKey("Path"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ExpandEnvironmentVariables_String_NullOrEmptyInput_ReturnsAsIs(string? input)
        {
            // Arrange
            var dict = new Dictionary<string, string?>();

            // Act
            var result = EnvironmentVariableHelper.ExpandEnvironmentVariables(input!, dict);

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void ExpandEnvironmentVariables_EmptyOrWhitespaceCustomNames_AreIgnored()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "", Value = "Empty" },
                new EnvironmentVariable { Name = "   ", Value = "Whitespace" },
                new EnvironmentVariable { Name = null!, Value = "Null" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            Assert.False(expanded.ContainsKey(""));
            Assert.False(expanded.ContainsKey("   "));
        }

        #endregion

        #region Standard Expansion Paths

        [Fact]
        public void ExpandEnvironmentVariables_ShouldExpandSystemVariable()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>();

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);
            string systemRoot = expanded["SystemRoot"]!; // should always exist
            string result = EnvironmentVariableHelper.ExpandEnvironmentVariables("%SystemRoot%", expanded);

            // Assert
            Assert.Equal(systemRoot, result, ignoreCase: true);
        }

        [Fact]
        public void ExpandEnvironmentVariables_ShouldIncludeCustomVariable()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "MY_VAR", Value = "HelloWorld" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            Assert.Equal("HelloWorld", expanded["MY_VAR"]);
        }

        [Fact]
        public void ExpandEnvironmentVariables_ShouldExpandCustomVariableReference()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "LOG_DIR", Value = "C:\\Logs" },
                new EnvironmentVariable { Name = "APP_HOME", Value = "%LOG_DIR%\\bin" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            Assert.Equal("C:\\Logs", expanded["LOG_DIR"]);
            Assert.Equal("C:\\Logs\\bin", expanded["APP_HOME"]);
        }

        [Fact]
        public void ExpandEnvironmentVariables_ShouldExpandMixedSystemAndCustomVariables()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "MY_TEMP", Value = "%TEMP%\\MyApp" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);
            string expected = Path.Combine(Environment.GetEnvironmentVariable("TEMP")!, "MyApp");

            // Assert
            Assert.Equal(expected, expanded["MY_TEMP"]);
        }

        [Fact]
        public void ExpandEnvironmentVariables_ShouldLeaveUnresolvedVariablesAsIs()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "BROKEN", Value = "%DOES_NOT_EXIST%\\foo" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            Assert.Contains("%DOES_NOT_EXIST%", expanded["BROKEN"]);
        }

        [Fact]
        public void ExpandEnvironmentVariables_InputString_ShouldExpandCorrectly()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "APP_HOME", Value = "C:\\MyApp" }
            };

            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Act
            string input = "%APP_HOME%\\data";
            string result = EnvironmentVariableHelper.ExpandEnvironmentVariables(input, expanded);

            // Assert
            Assert.Equal("C:\\MyApp\\data", result);
        }

        #endregion

        #region Self-Referencing and Cycle Paths

        [Fact]
        public void ExpandEnvironmentVariables_DirectCircularReference_SafelyLeavesPlaceholders()
        {
            // Arrange: Tests Issue #2024
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "CYCLE_A", Value = "%CYCLE_B%" },
                new EnvironmentVariable { Name = "CYCLE_B", Value = "%CYCLE_A%" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            Assert.Equal("%CYCLE_A%", expanded["CYCLE_A"]);
            Assert.Equal("%CYCLE_A%", expanded["CYCLE_B"]);
        }

        [Fact]
        public void ExpandEnvironmentVariables_ShouldHandleSelfReferencingVariableGracefully_WhenNoOsValueExists()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "NO_OS_VAR", Value = "%NO_OS_VAR%;\\bin" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            // The self-referencing token should be skipped and remain safely intact in the string, 
            // since there is no inherited OS value to append it to.
            Assert.Equal("%NO_OS_VAR%;\\bin", expanded["NO_OS_VAR"]);
        }

        [Fact]
        public void ExpandEnvironmentVariables_SelfReference_AppendsToInheritedOsValue()
        {
            // Arrange
            string osVarName = "TEST_OS_APPEND_PATH";
            SetTempOsVariable(osVarName, "C:\\BaseOSPath");

            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = osVarName, Value = $"%{osVarName}%;\\MyTools" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            Assert.Equal("C:\\BaseOSPath;\\MyTools", expanded[osVarName]);
        }

        [Fact]
        public void ExpandEnvironmentVariables_SelfReference_DependentVariablesResolveCorrectly()
        {
            // Arrange
            // This tests the `else` block in the double-append prevention logic,
            // ensuring that if variable B depends on a self-referencing variable A,
            // variable B gets the fully resolved replacement string.
            string osVarName = "TEST_CORE_VAR";
            SetTempOsVariable(osVarName, "C:\\Inherited");

            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = osVarName, Value = $"%{osVarName}%\\Child" },
                new EnvironmentVariable { Name = "DEPENDENT_VAR", Value = $"%{osVarName}%\\Grandchild" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            Assert.Equal("C:\\Inherited\\Child", expanded[osVarName]);
            Assert.Equal("C:\\Inherited\\Child\\Grandchild", expanded["DEPENDENT_VAR"]);
        }

        [Fact]
        public void ExpandEnvironmentVariables_ShouldHandleIndirectCircularReferencesGracefully()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "A", Value = "%B%_suffix" },
                new EnvironmentVariable { Name = "B", Value = "prefix_%C%" },
                new EnvironmentVariable { Name = "C", Value = "foo_%A%" },
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            // The expansion should halt at MaxEnvVarExpansionPasses without blowing up memory.
            Assert.Contains("%", expanded["A"]);
            Assert.Contains("%", expanded["B"]);
            Assert.Contains("%", expanded["C"]);
        }

        #endregion

        #region Size Limitations and Guard Paths

        [Fact]
        public void ExpandEnvironmentVariables_ExceedingMaxExpandedLength_TruncatesString()
        {
            // Arrange
            int maxLen = AppConfig.MaxEnvVarExpandedLength;

            // We create a base variable that is just under half the max length.
            // Referencing it twice in OVERFLOW will cause the inline expansion to breach the limit.
            string largePayload = new string('A', (maxLen / 2) + 100);

            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "LARGE_BASE", Value = largePayload },
                new EnvironmentVariable { Name = "OVERFLOW_VAR", Value = "%LARGE_BASE%_%LARGE_BASE%" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            Assert.Equal(maxLen, expanded["OVERFLOW_VAR"]?.Length);
        }

        #endregion

        #region Security and Protected Variables

        [Fact]
        public void ExpandEnvironmentVariables_ShouldBlockProtectedVariableOverride()
        {
            // Arrange
            string systemPath = Environment.GetEnvironmentVariable("PATH")!;
            var vars = new List<EnvironmentVariable>
            {
                // Attempting to hijack the PATH
                new EnvironmentVariable { Name = "PATH", Value = "C:\\Attacker\\Bin" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            // The custom value should be ignored; the system value must remain intact.
            Assert.NotEqual("C:\\Attacker\\Bin", expanded["PATH"]);
            Assert.Equal(systemPath, expanded["PATH"]);
        }

        [Fact]
        public void ExpandEnvironmentVariables_ShouldBeCaseInsensitiveForProtectedVariables()
        {
            // Arrange
            string systemComSpec = Environment.GetEnvironmentVariable("COMSPEC")!;
            var vars = new List<EnvironmentVariable>
            {
                // Attempting to bypass using casing
                new EnvironmentVariable { Name = "pAtH", Value = "C:\\Malicious" },
                new EnvironmentVariable { Name = "comspec", Value = "C:\\Malicious\\cmd.exe" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            Assert.NotEqual("C:\\Malicious", expanded["PATH"]);
            Assert.NotEqual("C:\\Malicious\\cmd.exe", expanded["COMSPEC"]);
            Assert.Equal(systemComSpec, expanded["COMSPEC"], ignoreCase: true);
        }

        [Fact]
        public void ExpandEnvironmentVariables_ShouldBlockAllVariablesInProtectedSet()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "SYSTEMROOT", Value = "C:\\Fake" },
                new EnvironmentVariable { Name = "TEMP", Value = "C:\\FakeTemp" },
                new EnvironmentVariable { Name = "USERNAME", Value = "Administrator" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            Assert.NotEqual("C:\\Fake", expanded["SYSTEMROOT"]);
            Assert.NotEqual("C:\\FakeTemp", expanded["TEMP"]);
            Assert.NotEqual("Administrator", expanded["USERNAME"]);
        }

        [Fact]
        public void ExpandEnvironmentVariables_ShouldAllowOverridingNonProtectedVariables()
        {
            // Arrange
            // Create a custom variable that isn't in the blocklist
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "CUSTOM_APP_SETTING", Value = "SafeValue" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            Assert.Equal("SafeValue", expanded["CUSTOM_APP_SETTING"]);
        }

        #endregion
    }
}