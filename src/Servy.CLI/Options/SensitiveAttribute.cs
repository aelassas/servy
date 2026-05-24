namespace Servy.CLI.Options
{
    /// <summary>
    /// Indicates that a command-line option contains sensitive information
    /// (such as passwords, environment variables, or arbitrary parameters)
    /// and must be scrubbed from logs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class SensitiveAttribute : Attribute
    {
    }
}