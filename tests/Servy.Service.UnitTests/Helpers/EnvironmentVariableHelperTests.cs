using Servy.Core.Config;
using Servy.Core.EnvironmentVariables;
using Servy.Service.Helpers;

namespace Servy.Service.UnitTests.Helpers
{
    // Running sequentially to prevent Environment.SetEnvironmentVariable calls 
    // from causing race conditions across different tests.
    [CollectionDefinition("SequentialEnvTests", DisableParallelization = true)]
    public class SequentialEnvTestsCollection
    {
        // Enforces strict sequential isolation across the execution suite
    }

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
            Assert.True(expanded.ContainsKey("PATH"));
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
                new EnvironmentVariable { Name = "    ", Value = "Whitespace" },
                new EnvironmentVariable { Name = null!, Value = "Null" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            Assert.False(expanded.ContainsKey(""));
            Assert.False(expanded.ContainsKey("    "));
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

        #region Percent Escaping (%%) Paths

        [Fact]
        public void ExpandEnvironmentVariables_DoublePercent_CollapsesToSinglePercent()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "PROFIT_MARGIN", Value = "100%%" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            // The literal '%%' should be safely decoded back to '%' after expansion passes.
            Assert.Equal("100%", expanded["PROFIT_MARGIN"]);
        }

        [Fact]
        public void ExpandEnvironmentVariables_DoublePercent_PreventsVariableExpansion()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "LITERAL_TEMP", Value = "%%TEMP%%" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            // The double percent should collapse to a single percent, leaving the literal placeholder intact
            // rather than expanding it to the system TEMP path.
            Assert.Equal("%TEMP%", expanded["LITERAL_TEMP"]);
        }

        [Fact]
        public void ExpandEnvironmentVariables_InputString_HandlesMixedEscapedAndUnescapedPercents()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "MY_CUSTOM", Value = "Value" }
            };
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Act
            // Tests an escaped 100%, an escaped variable marker, and a live variable marker side-by-side
            string input = "100%% of %%MY_CUSTOM%% is %MY_CUSTOM%";
            string result = EnvironmentVariableHelper.ExpandEnvironmentVariables(input, expanded);

            // Assert
            Assert.Equal("100% of %MY_CUSTOM% is Value", result);
        }

        [Fact]
        public void ExpandEnvironmentVariables_InjectedLiteralPercentVar_PreventsOSReExpansion()
        {
            // Arrange
            // User sets a custom variable to literally equal "%PATH%" using the escape sequence
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "CUSTOM_LITERAL", Value = "%%PATH%%" }
            };
            var expandedEnv = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Verify the dictionary baseline is correct: contains literal "%PATH%"
            Assert.Equal("%PATH%", expandedEnv["CUSTOM_LITERAL"]);

            // Act
            // Reference that custom variable inside an input string template.
            // The string overload should substitute "%CUSTOM_LITERAL%" with "%PATH%",
            // but MUST NOT allow the underlying OS layer to expand "%PATH%" into the full system path string.
            string input = "Value=%CUSTOM_LITERAL%";
            string result = EnvironmentVariableHelper.ExpandEnvironmentVariables(input, expandedEnv);

            // Assert
            Assert.Equal("Value=%PATH%", result);
        }

        [Fact]
        public void ExpandEnvironmentVariables_InjectedLiteralPercentVar_MaintainsSystemPlaceholderResolution()
        {
            // Arrange
            // Set up a custom environment variable containing an escaped system variable name 
            // sitting right next to an unescaped, live cross-referenced system placeholder.
            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "MIXED_BAG", Value = "Escaped=%%SystemRoot%%, Real=%SystemRoot%" }
            };
            var expandedEnv = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Act
            string input = "Result->%MIXED_BAG%";
            string result = EnvironmentVariableHelper.ExpandEnvironmentVariables(input, expandedEnv);

            // Assert
            string expectedSystemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows";
            string expectedString = $"Result->Escaped=%SystemRoot%, Real={expectedSystemRoot}";

            Assert.Equal(expectedString, result);
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
            // The fixed-point engine safely halts and leaves the unresolvable macro boundaries intact.
            // Due to isolated snapshot routing, each variable cleanly retains its fallback cycle token state.
            Assert.Equal("%CYCLE_A%", expanded["CYCLE_A"]);
            Assert.Equal("%CYCLE_B%", expanded["CYCLE_B"]);
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
            // Pin down the precise literal values generated by the single-pass 
            // security-masking architecture instead of a loose check. This guarantees any changes 
            // to the core expansion/protection loop behavior will be explicitly tracked.
            Assert.Equal("prefix_%C%_suffix", expanded["A"]);
            Assert.Equal("prefix_foo_%A%", expanded["B"]);
            Assert.Equal("foo_%B%_suffix", expanded["C"]);
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
            string systemPath = Environment.GetEnvironmentVariable("PATH")!;

            var vars = new List<EnvironmentVariable>
            {
                // Attempting to bypass the block framework using scrambled character casings
                new EnvironmentVariable { Name = "pAtH", Value = "C:\\Malicious" },
                new EnvironmentVariable { Name = "comspec", Value = "C:\\Malicious\\cmd.exe" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            // Verify rejection of malicious values
            Assert.NotEqual("C:\\Malicious", expanded["PATH"]);
            Assert.NotEqual("C:\\Malicious\\cmd.exe", expanded["COMSPEC"]);

            // Verify true underlying system configuration value preservation (Symmetric Verification)
            Assert.Equal(systemPath, expanded["PATH"], ignoreCase: true);
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

            Assert.Equal(Environment.GetEnvironmentVariable("SystemRoot"), expanded["SYSTEMROOT"], ignoreCase: true);
            Assert.Equal(Environment.GetEnvironmentVariable("TEMP"), expanded["TEMP"], ignoreCase: true);
            Assert.Equal(Environment.GetEnvironmentVariable("USERNAME"), expanded["USERNAME"], ignoreCase: true);
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

        #region Size Limitations and Truncation Sentinel Guard Paths

        [Fact]
        public void ExpandEnvironmentVariables_TruncationLandsExactlyOnTokenEnd_PreservesIntactToken()
        {
            // Arrange: Tests Issue #2273 (Outer guard validation)
            int maxLen = AppConfig.MaxEnvVarExpandedLength;
            string token = EnvironmentVariableHelper.PercentEscapeToken;

            // Allocate a payload that tracks the dynamic limit using an arithmetic safety offset.
            string largeChunk = new string('A', maxLen - 200);

            // Derive the remainder mathematically to hit exactly (maxLen + 1) during execution.
            // Target equation configuration: chunk length + fineTuningPad length + token length + 1 ("X") = maxLen + 1
            int padLength = (maxLen + 1) - largeChunk.Length - token.Length - 1;
            string fineTuningPad = new string('A', padLength);

            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "LARGE_BLOCK", Value = largeChunk },
                // This composition remains small (under maxLen) during initialization,
                // avoiding the inline guard and forcing the outer loop to manage the truncation point.
                new EnvironmentVariable { Name = "OVERFLOW_VAR", Value = "%LARGE_BLOCK%" + fineTuningPad + token + "X" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            string resultValue = expanded["OVERFLOW_VAR"]!;

            // The outer look-behind filter should acknowledge the token is completely intact,
            // ignore it, and let Step 5 safely collapse it to '%'.
            Assert.True(resultValue.EndsWith("%"), $"Expected string to end with collapsed escape character '%' but got trailing value: {resultValue.Substring(resultValue.Length - 5)}");
            Assert.DoesNotContain("_SERVY_ESC_PERCENT_", resultValue);
            Assert.Equal(maxLen - token.Length + 1, resultValue.Length);
        }

        [Fact]
        public void ExpandEnvironmentVariables_TruncationSplitsToken_RollsBackToCleanBoundary()
        {
            // Arrange: Tests Issue #2267 & #2273 (Token fragmentation outer protection)
            int maxLen = AppConfig.MaxEnvVarExpandedLength;
            string token = EnvironmentVariableHelper.PercentEscapeToken;

            // Align the token so that the truncation line (maxLen) cuts directly through its center
            int prefixLength = maxLen - (token.Length / 2);
            string targetPayload = new string('B', prefixLength) + token + "ExtendedContent";

            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "FRAGMENT_VAR", Value = targetPayload }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            string resultValue = expanded["FRAGMENT_VAR"]!;

            // The rollback should discard the partial token entirely, meaning the string 
            // should safely shorten past maxLen and contain only the baseline padding characters.
            Assert.True(resultValue.Length < maxLen, $"Expected string length ({resultValue.Length}) to be less than MaxLength ({maxLen}) due to fragment rollback.");
            Assert.True(resultValue.All(c => c == 'B'), "The string should only contain the padding prefix characters after rolling back.");
            Assert.DoesNotContain("\uFFFD", resultValue, StringComparison.Ordinal);
            Assert.DoesNotContain("_SERVY_ESC_PERCENT_", resultValue);
        }

        [Fact]
        public void ExpandEnvironmentVariables_StringOverload_SplitTokenAtLengthCap_IsSanitizedInline()
        {
            // Arrange
            int maxLen = AppConfig.MaxEnvVarExpandedLength;
            string token = EnvironmentVariableHelper.PercentEscapeToken;

            // Position the split boundary within the internal dictionary lookup runner
            int prefixLength = maxLen - (token.Length / 2);
            string largePayload = new string('C', prefixLength) + token + "OverflowContent";

            var dictionaryContext = new Dictionary<string, string?>
            {
                { "TRIGGER_VAR", largePayload }
            };

            // Act
            // Force an inline token replacement truncation by referencing our massive variable definition
            var resultValue = EnvironmentVariableHelper.ExpandEnvironmentVariables("%TRIGGER_VAR%", dictionaryContext);

            // Assert
            // The output should be safely sanitized of broken tokens even when bypassing parent macro loops
            Assert.True(resultValue.Length < maxLen, "The internal inline guard within ExpandWithDictionary should trigger and discard the split token.");
            Assert.True(resultValue.All(c => c == 'C'), "The output string must contain only sanitized baseline padding values.");
            Assert.DoesNotContain("\uFFFD", resultValue, StringComparison.Ordinal);
            Assert.DoesNotContain("_SERVY_ESC_PERCENT_", resultValue);
        }

        #endregion

        #region Injected Literal Percent

        [Fact]
        public void ExpandEnvironmentVariables_InjectedLiteralPercentContent_DoesNotTriggerOsReExpansion()
        {
            // Arrange: Tests Issue #2300
            // We set up a custom variable that evaluates to a literal '%'.
            // When referenced inside another variable surrounding a real system variable keyword,
            // the resulting literal token sequence (e.g., %SystemRoot%) must NOT be expanded 
            // by the Step 4 OS-level execution layer.
            string systemRootValue = Environment.GetEnvironmentVariable("SystemRoot")!;
            Assert.False(string.IsNullOrEmpty(systemRootValue), "Precondition failed: SystemRoot OS variable is not set.");

            var vars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "PERCENT", Value = "%" },
                new EnvironmentVariable { Name = "LITERAL_MSG", Value = "%PERCENT%SystemRoot%PERCENT%" }
            };

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // Assert
            string resultValue = expanded["LITERAL_MSG"]!;

            // SSoT Parity Validation: The dictionary-builder must match the string-overload behavior.
            // The literal string text '%SystemRoot%' should remain completely intact as a user literal,
            // instead of aggressively expanding into the machine's actual system directory path (e.g., C:\Windows).
            Assert.Equal("%SystemRoot%", resultValue);
            Assert.NotEqual(systemRootValue, resultValue);
        }

        #endregion
    }
}