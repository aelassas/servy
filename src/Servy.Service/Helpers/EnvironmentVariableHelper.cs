using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using System.Collections;

namespace Servy.Service.Helpers
{
    /// <summary>
    /// Provides helper methods for expanding system and custom environment variables.
    /// Supports expansion in values, process arguments, executable paths, and working directories,
    /// including cross-references between custom environment variables.
    /// </summary>
    /// <example>
    /// Example usage:
    /// <code>
    /// var customVars = new List&lt;EnvironmentVariable&gt;
    /// {
    ///     new EnvironmentVariable { Name = "LOG_DIR", Value = "%ProgramData%\\Servy\\logs" },
    ///     new EnvironmentVariable { Name = "APP_HOME", Value = "%LOG_DIR%\\bin" }
    /// };
    ///
    /// var expandedEnv = EnvironmentVariableHelper.ExpandEnvironmentVariables(customVars);
    ///
    /// // expandedEnv["LOG_DIR"] = "C:\\ProgramData\\Servy\\logs"
    /// // expandedEnv["APP_HOME"] = "C:\\ProgramData\\Servy\\logs\\bin"
    ///
    /// string path = EnvironmentVariableHelper.ExpandEnvironmentVariables(
    ///     "%APP_HOME%\\myapp.exe",
    ///     expandedEnv);
    ///
    /// // path = "C:\\ProgramData\\Servy\\logs\\bin\\myapp.exe"
    /// </code>
    /// </example>
    public static class EnvironmentVariableHelper
    {
        /// <summary>
        /// Protected system variables that should never be overridden by user configuration
        /// to prevent privilege escalation and system instability.
        /// </summary>
        private static readonly HashSet<string> ProtectedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PATH",           // DLL/Binary hijacking
            "COMSPEC",        // Shell hijacking
            "SYSTEMROOT",     // System API redirection
            "WINDIR",         // System directory redirection
            "SYSTEMDRIVE",    // Core drive redirection
            "TEMP",           // Sandbox escape/hijacking
            "TMP",            // Sandbox escape/hijacking
            "PATHEXT",        // Extension hijacking
            "PSMODULEPATH",   // PowerShell module hijacking
            "USERNAME",       // Identity spoofing
            "USERPROFILE",    // Profile redirection
            "ALLUSERSPROFILE",
            "PROGRAMDATA"
        };

        /// <summary>
        /// Builds a dictionary of environment variables by merging the current system environment
        /// with the provided custom environment variables. All values are expanded so that system
        /// and custom variables can reference each other (e.g. %ProgramData%, %MY_CUSTOM_VAR%).
        /// </summary>
        /// <param name="environmentVariables">
        /// A list of custom environment variables to include. May be <c>null</c>.
        /// </param>
        /// <returns>
        /// A dictionary containing system environment variables combined with the provided custom ones,
        /// with all values fully expanded.
        /// </returns>
        public static Dictionary<string, string?> ExpandEnvironmentVariables(List<EnvironmentVariable> environmentVariables)
        {
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            // 1. Load System Environment
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                result[(string)entry.Key] = (string?)entry.Value;
            }

            // 2. Merge Custom Variables with SECURITY CHECK
            if (environmentVariables != null)
            {
                foreach (var envVar in environmentVariables)
                {
                    if (string.IsNullOrWhiteSpace(envVar.Name)) continue;

                    if (ProtectedVariables.Contains(envVar.Name))
                    {
                        // Log the violation - this is critical for auditing
                        Logger.Warn($"Security: Blocked an attempt to override protected variable '{envVar.Name}'. Custom values for this variable are ignored to prevent privilege escalation.");
                        continue;
                    }

                    result[envVar.Name] = envVar.Value;
                }
            }

            // 3. Recursive Expansion
            // We use ToList() to avoid "Collection was modified" exceptions
            foreach (var key in result.Keys.ToList())
            {
                result[key] = ExpandWithDictionary(result[key]!, result);
            }

            return result;
        }

        /// <summary>
        /// Expands environment variables in the given input string using both system and custom variables.
        /// </summary>
        /// <param name="input">The string containing environment variable references (e.g. "%ProgramFiles%\\MyApp").</param>
        /// <param name="expandedEnv">
        /// A dictionary of environment variables previously built by <see cref="ExpandEnvironmentVariables(List{EnvironmentVariable})"/>.
        /// </param>
        /// <returns>
        /// The input string with all environment variable references expanded.
        /// </returns>
        public static string ExpandEnvironmentVariables(string input, IDictionary<string, string?> expandedEnv)
        {
            return ExpandWithDictionary(input, expandedEnv);
        }

        /// <summary>
        /// Expands environment variables in a string using the provided dictionary of variables.
        /// Custom variables override system variables. Windows built-in expansion is also applied
        /// to handle system-defined placeholders such as %SystemRoot%.
        /// </summary>
        /// <param name="value">The string to expand.</param>
        /// <param name="variables">The dictionary of environment variables to use during expansion.</param>
        /// <returns>The expanded string.</returns>
        private static string ExpandWithDictionary(string value, IDictionary<string, string?> variables)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            string expanded = value;

            foreach (var kvp in variables)
            {
                string token = "%" + kvp.Key + "%";
                string replacement = kvp.Value ?? string.Empty;

                // Detect and skip self-referencing tokens.
                // If the replacement value already contains the token we are trying to resolve, 
                // skip it. This prevents unbounded exponential string growth during dictionary mutation
                // and safely short-circuits both direct and indirect circular references.
                if (replacement.IndexOf(token, StringComparison.OrdinalIgnoreCase) > -1)
                {
                    continue;
                }

                int index = 0;
                while ((index = expanded.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    expanded = expanded.Substring(0, index) + replacement + expanded.Substring(index + token.Length);
                    index += replacement.Length; // move past the inserted value
                }
            }

            // Also apply Windows built-in expansion (covers %SystemRoot% etc.)
            return Environment.ExpandEnvironmentVariables(expanded);
        }

    }
}
