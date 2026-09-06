namespace Servy.Service.UnitTests.Helpers
{
    /// <summary>
    /// Collection definition serializing the suites that mutate process-wide state:
    /// the OS environment variables set by <see cref="EnvironmentVariableHelperTests"/>
    /// and the static <c>ProcessHelper.EnvVarRegex</c> swapped by <see cref="ProcessHelperTests"/>.
    /// </summary>
    [CollectionDefinition("SequentialEnvTests", DisableParallelization = true)]
    public class SequentialEnvTestsCollection
    {
        // Marker class, no code
    }
}
