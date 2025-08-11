namespace Servy.Core.EnvironmentVariables
{
    /// <summary>
    /// Represents a single environment variable with a name and value.
    /// </summary>
    public class EnvironmentVariable
    {
        /// <summary>
        /// Gets or sets the environment variable name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the environment variable value.
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }
}
