using Servy.Core.Helpers;

namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// Covers <see cref="SystemEnvironmentHelper"/> against the real registry, in the non-elevated test process: both
    /// environment keys are readable without elevation, a System variable missing from the process is added, and in
    /// service mode a stale inherited value takes the registry's (#7393).
    /// </summary>
    [Collection(CoreOsIntegrationCollection.Name)]
    public class SystemEnvironmentHelperIntegrationTests : IDisposable
    {
        /// <summary>
        /// A System variable every Windows installation defines with a plain value (<c>Windows_NT</c>) and no user defines.
        /// </summary>
        private const string SystemVariable = "OS";

        private readonly string? _original = Environment.GetEnvironmentVariable(SystemVariable);

        /// <summary>
        /// Restores the variable the test changed in this process.
        /// </summary>
        public void Dispose() => Environment.SetEnvironmentVariable(SystemVariable, _original);

        /// <summary>
        /// Creates a refresher over the real registry, narrowed to <see cref="SystemVariable"/>, that writes the real
        /// process environment.
        /// </summary>
        /// <returns>The refresher.</returns>
        /// <remarks>
        /// Narrowed because a full service-mode refresh rebuilds every System variable of the test process from the
        /// registry, Path included, and drops the entries the CI runner added to Path, which broke the process-spawning
        /// tests running alongside (measured on ARM64: five ProcessKillerIntegrationTests timed out).
        /// </remarks>
        private static SystemEnvironmentRefresher CreateRefresher()
            => new SystemEnvironmentRefresher(
                () => Only(SystemEnvironmentHelper.ReadSystemVariables()),
                () => Only(SystemEnvironmentHelper.ReadUserVariables()),
                (name, value) => Environment.SetEnvironmentVariable(name, value));

        /// <summary>
        /// Keeps only <see cref="SystemVariable"/> from a set of registry variables.
        /// </summary>
        /// <param name="variables">The variables read from the registry, or <see langword="null"/>.</param>
        /// <returns>The variable, if present; <see langword="null"/> when the registry was unreadable.</returns>
        private static IReadOnlyDictionary<string, string>? Only(IReadOnlyDictionary<string, string>? variables)
        {
            if (variables == null)
                return null;

            var only = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? value;
            if (variables.TryGetValue(SystemVariable, out value))
                only[SystemVariable] = value;

            return only;
        }

        [Fact]
        public void ReadVariables_WithoutElevation_ReturnsTheRawSystemAndUserVariables()
        {
            // Arrange & Act
            var system = SystemEnvironmentHelper.ReadSystemVariables();
            var user = SystemEnvironmentHelper.ReadUserVariables();

            // Assert: read-only access works without elevation, and REG_EXPAND_SZ values stay raw
            Assert.NotNull(system);
            Assert.Equal("Windows_NT", system![SystemVariable]);
            Assert.Contains("%SystemRoot%", system["ComSpec"], StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(user);
        }

        [Fact]
        public void Refresh_Application_VariableMissingFromTheProcess_IsAddedFromTheRegistry()
        {
            // Arrange: remove the variable from this process, as if it had been added after the process started
            Environment.SetEnvironmentVariable(SystemVariable, null);
            var refresher = CreateRefresher();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal("Windows_NT", Environment.GetEnvironmentVariable(SystemVariable));
        }

        [Fact]
        public void Refresh_Service_StaleInheritedValue_TakesTheRegistryValue()
        {
            // Arrange: the value the service inherited at boot no longer matches the registry
            Environment.SetEnvironmentVariable(SystemVariable, "Stale_Boot_Value");
            var refresher = CreateRefresher();
            refresher.UseRegistryAsSource();

            // Act
            refresher.Refresh();

            // Assert
            Assert.Equal("Windows_NT", Environment.GetEnvironmentVariable(SystemVariable));
        }

        [Fact]
        public void ResolvePath_SystemVariableMissingFromTheProcess_IsResolvedWithoutARestart()
        {
            // Arrange: ProcessHelper.ResolvePath is what the apps and the service use for every configured path
            Environment.SetEnvironmentVariable(SystemVariable, null);

            // Act
            var resolved = new ProcessHelper().ResolvePath($@"C:\%{SystemVariable}%\bin\..\app.exe");

            // Assert
            Assert.Equal(@"C:\Windows_NT\app.exe", resolved);
        }
    }
}
