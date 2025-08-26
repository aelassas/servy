using Servy.Core.EnvironmentVariables;
using Servy.Service.Helpers;
using System;
using System.Collections.Generic;
using Xunit;

namespace Servy.Service.UnitTests.Helpers
{
    public class EnvironmentVariableHelperTests
    {
        [Fact]
        public void ExpandEnvironmentVariables_ShouldExpandSystemVariable()
        {
            // Arrange
            var vars = new List<EnvironmentVariable>();

            // Act
            var expanded = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);
            string systemRoot = expanded["SystemRoot"]; // should always exist
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
            string expected = System.IO.Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "MyApp");

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
    }
}
