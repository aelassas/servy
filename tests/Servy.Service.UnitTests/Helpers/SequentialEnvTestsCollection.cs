using Xunit;

namespace Servy.Service.UnitTests.Helpers
{
    /// <summary>
    /// Collection definition serializing the suites that mutate process-wide state:
    /// the OS environment variables set by <see cref="EnvironmentVariableHelperTests"/>
    /// and the static <c>ProcessHelper.EnvVarRegex</c> swapped by <see cref="ProcessHelperTests"/>.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class SequentialEnvTestsCollection
    {
        /// <summary>Collection name; reference this instead of repeating the string literal.</summary>
        public const string Name = "SequentialEnvTests";
    }
}
