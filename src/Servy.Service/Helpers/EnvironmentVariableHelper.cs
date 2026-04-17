using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
            // --- System Integrity ---
            "PATH", "COMSPEC", "SYSTEMROOT", "WINDIR", "SYSTEMDRIVE", "TEMP", "TMP", "PATHEXT",
    
            // --- User & Profile Integrity ---
            "USERNAME", "USERPROFILE", "ALLUSERSPROFILE", "PROGRAMDATA", "PSMODULEPATH",

            // --- Runtime Injection Vectors (Issue #573) ---
            // .NET / CLR Injection
            "COR_ENABLE_PROFILING", "COR_PROFILER", "COR_PROFILER_PATH", "DOTNET_STARTUP_HOOKS",
    
            // Java Injection
            "JAVA_TOOL_OPTIONS", "_JAVA_OPTIONS", "CLASSPATH",
    
            // Node.js Injection
            "NODE_OPTIONS", "NODE_PATH",
    
            // Python Injection
            "PYTHONSTARTUP", "PYTHONPATH", "PYTHONHOME",
    
            // Global/Unix-like fallback (for MinGW/WSL contexts)
            "LD_PRELOAD", "LD_LIBRARY_PATH"
        };

        // Safety caps to prevent unbounded growth from complex recursive nesting
        private const int MaxExpansionPasses = 5;
        private const int MaxStringLength = 32768;

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
        /// with all values fully expanded using a multi-pass fixed-point resolution to safely handle cross-references.
        /// </returns>
        public static Dictionary<string, string> ExpandEnvironmentVariables(List<EnvironmentVariable> environmentVariables)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 1. Load System Environment
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                result[(string)entry.Key] = (string)entry.Value;
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

            // 3. Recursive Expansion (Multi-pass Fixed-Point Resolution)
            // We use ToList() to avoid "Collection was modified" exceptions
            bool changed;
            int pass = 0;

            do
            {
                changed = false;

                // Snapshot the current state to prevent cascading mutations during a single pass
                var snapshot = result.ToDictionary(k => k.Key, k => k.Value, StringComparer.OrdinalIgnoreCase);

                foreach (var key in result.Keys.ToList())
                {
                    string original = result[key];
                    if (string.IsNullOrEmpty(original)) continue;

                    string expanded = ExpandWithDictionary(original, snapshot);

                    // Exponential growth guard
                    if (expanded != null && expanded.Length > MaxStringLength)
                    {
                        Logger.Warn($"Expansion of '{key}' exceeded {MaxStringLength} characters. Truncating to prevent memory exhaustion.");
                        expanded = expanded.Substring(0, MaxStringLength);
                    }

                    if (!string.Equals(original, expanded, StringComparison.Ordinal))
                    {
                        result[key] = expanded;
                        changed = true;
                    }
                }

                pass++;
            }
            while (changed && pass < MaxExpansionPasses);

            if (pass >= MaxExpansionPasses && changed)
            {
                Logger.Warn("Environment variable expansion reached maximum pass limit. Indirect circular reference detected (e.g., A=%B%, B=%A%).");
            }

            // 4. Final layer: Apply OS-level expansion for any remaining unmapped system placeholders
            foreach (var key in result.Keys.Where(k => !string.IsNullOrEmpty(result[k])).ToList())
            {
                result[key] = Environment.ExpandEnvironmentVariables(result[key]);
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
        public static string ExpandEnvironmentVariables(string input, IDictionary<string, string> expandedEnv)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Since 'expandedEnv' is already resolved to a fixed point by the dictionary builder,
            // only one pass is needed here.
            string result = ExpandWithDictionary(input, expandedEnv);
            return Environment.ExpandEnvironmentVariables(result);
        }

        /// <summary>
        /// Expands environment variables in a string using the provided dictionary of variables.
        /// Custom variables override system variables. Windows built-in expansion is also applied
        /// to handle system-defined placeholders such as %SystemRoot%.
        /// </summary>
        /// <param name="value">The string to expand.</param>
        /// <param name="variables">The dictionary of environment variables to use during expansion.</param>
        /// <returns>The expanded string.</returns>
        private static string ExpandWithDictionary(string value, IDictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            string expanded = value;

            foreach (var kvp in variables)
            {
                if (string.IsNullOrEmpty(kvp.Key) || string.IsNullOrEmpty(kvp.Value)) continue;

                string token = "%" + kvp.Key + "%";
                string replacement = kvp.Value;

                // SELF-REFERENCE GUARD:
                // If the replacement value contains the token itself (e.g., PATH=%PATH%;bin), 
                // we must skip it here. This prevents exponential string growth during 
                // our multi-pass custom expansion. 
                // The final pass of 'Environment.ExpandEnvironmentVariables' will 
                // correctly resolve these using the actual system-level values.
                if (replacement.IndexOf(token, StringComparison.OrdinalIgnoreCase) > -1)
                {
                    continue;
                }

                int index = 0;
                while ((index = expanded.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    expanded = expanded.Substring(0, index) + replacement + expanded.Substring(index + token.Length);
                    index += replacement.Length; // move past the inserted value

                    // Inline length guard to prevent memory exhaustion
                    if (expanded.Length > MaxStringLength)
                    {
                        return expanded.Substring(0, MaxStringLength);
                    }
                }
            }

            return expanded;
        }
    }
}