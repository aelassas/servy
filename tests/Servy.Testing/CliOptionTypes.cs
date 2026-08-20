using CommandLine;
using Servy.CLI.Options;
using System.Reflection;

namespace Servy.Testing
{
    /// <summary>
    /// Provides shared reflection-based discovery of CLI option types across unit and integration test suites.
    /// </summary>
    public static class CliOptionTypes
    {
        /// <summary>
        /// Discovers every CLI option-carrying type in the <c>Servy.CLI</c> assembly: verbs,
        /// and any other type declaring <see cref="OptionAttribute"/> properties, such as <c>GlobalOptionsBase</c>.
        /// Dynamic reflection is used to prevent newly added properties from escaping sensitive field leak guards.
        /// </summary>
        public static readonly Type[] All = typeof(InstallServiceOptions).Assembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<VerbAttribute>() != null
                     || t.GetProperties().Any(p => p.GetCustomAttribute<OptionAttribute>() != null))
            .ToArray();
    }
}
