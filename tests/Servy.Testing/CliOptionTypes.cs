using CommandLine;
using Servy.CLI.Options;
using System;
using System.Linq;
using System.Reflection;

namespace Servy.Testing
{
    /// <summary>
    /// Provides shared reflection-based discovery of CLI option types across unit and integration test suites.
    /// </summary>
    public static class CliOptionTypes
    {
        /// <summary>
        /// Discovers all option verbs dynamically via reflection
        /// to prevent new properties from escaping sensitive field leak guards.
        /// </summary>
        public static readonly Type[] All = typeof(InstallServiceOptions).Assembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<VerbAttribute>() != null
                     || t.GetProperties().Any(p => p.GetCustomAttribute<OptionAttribute>() != null))
            .ToArray();
    }
}
