using Servy.Core.Helpers;
using System;
using Xunit;

namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// Covers <see cref="SystemEnvironmentHelper"/> against the real registry, in the non-elevated test process: the
    /// System variables are readable without elevation, and a System variable the process environment does not have
    /// is resolved from them (#7393), as for a variable added after the process started.
    /// </summary>
    [Collection(CoreOsIntegrationCollection.Name)]
    public class SystemEnvironmentHelperIntegrationTests
    {
        /// <summary>
        /// A System variable every Windows installation defines with a plain value (<c>Windows_NT</c>).
        /// </summary>
        private const string SystemVariable = "OS";

        [Fact]
        public void ReadSystemVariables_WithoutElevation_ReturnsTheRawSystemVariables()
        {
            // Arrange & Act
            var variables = SystemEnvironmentHelper.ReadSystemVariables();

            // Assert: read-only access works without elevation, and REG_EXPAND_SZ values stay raw
            Assert.NotNull(variables);
            Assert.Equal("Windows_NT", variables[SystemVariable]);
            Assert.Contains("%SYSTEMROOT%", variables["ComSpec"].ToUpperInvariant());
        }

        [Fact]
        public void ExpandSystemEnvironmentVariables_VariableMissingFromTheProcess_IsResolvedFromTheRegistryAndRestoredInTheProcess()
        {
            // Arrange: remove the variable from this process, as if it had been added after the process started
            var original = Environment.GetEnvironmentVariable(SystemVariable);
            Environment.SetEnvironmentVariable(SystemVariable, null);
            try
            {
                // Act
                var result = SystemEnvironmentHelper.ExpandSystemEnvironmentVariables($@"C:\%{SystemVariable}%\app");

                // Assert
                Assert.Equal(@"C:\Windows_NT\app", result);
                Assert.Equal("Windows_NT", Environment.GetEnvironmentVariable(SystemVariable));
            }
            finally
            {
                Environment.SetEnvironmentVariable(SystemVariable, original);
            }
        }

        [Fact]
        public void ResolvePath_SystemVariableMissingFromTheProcess_IsResolvedWithoutARestart()
        {
            // Arrange: ProcessHelper.ResolvePath is what the apps and the service use for every configured path (#7393)
            var original = Environment.GetEnvironmentVariable(SystemVariable);
            Environment.SetEnvironmentVariable(SystemVariable, null);
            try
            {
                // Act
                var resolved = new ProcessHelper().ResolvePath($@"C:\%{SystemVariable}%\bin\..\app.exe");

                // Assert
                Assert.Equal(@"C:\Windows_NT\app.exe", resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable(SystemVariable, original);
            }
        }
    }
}
