using Servy.Core.Config;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
        /// A deterministic, reserved token used to temporarily protect '%%' escape sequences 
        /// from being parsed during the fixed-point expansion loop. Uses the Unicode Replacement 
        /// Character (\uFFFD) to guarantee no collisions with legitimate user input.
        /// </summary>
        private const string PercentEscapeToken = "\uFFFD_SERVY_ESC_PERCENT_\uFFFD";

        /// <summary>
        /// Protected system variables that should never be overridden by user configuration
        /// to prevent privilege escalation, process hijacking, and system instability.
        /// </summary>
        private static readonly HashSet<string> ProtectedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // --- System Integrity ---
            "PATH", "COMSPEC", "SYSTEMROOT", "WINDIR", "SYSTEMDRIVE", "TEMP", "TMP", "PATHEXT",
            "PROGRAMFILES", "PROGRAMFILES(X86)", "PROGRAMW6432",
            "COMMONPROGRAMFILES", "COMMONPROGRAMFILES(X86)", "COMMONPROGRAMW6432",

            // --- Profile / Identity Redirection ---
            "APPDATA", "LOCALAPPDATA", "PUBLIC",
            "HOMEDRIVE", "HOMEPATH", "HOME",
            "USERDOMAIN", "USERDOMAIN_ROAMINGPROFILE", "LOGONSERVER",

            // --- User & Profile Integrity ---
            "USERNAME", "USERPROFILE", "ALLUSERSPROFILE", "PROGRAMDATA", "PSMODULEPATH",

            // --- Runtime Injection & Hijack Vectors ---
            
            // .NET Injection (Legacy & Modern CoreCLR)
            "COR_ENABLE_PROFILING", "COR_PROFILER", "COR_PROFILER_PATH",
            "CORECLR_ENABLE_PROFILING", "CORECLR_PROFILER", "CORECLR_PROFILER_PATH",
            "DOTNET_STARTUP_HOOKS", "DOTNET_ROOT", "DOTNET_ROOT(x86)", "DOTNET_HOST_PATH",
            "DOTNET_BUNDLE_EXTRACT_BASE_DIR", "DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE",
  
            // Java Injection - Covers direct java.exe (TOOL_OPTIONS) and common shell-wrapper launchers (OPTS)
            "JAVA_TOOL_OPTIONS", "_JAVA_OPTIONS", "JAVA_OPTS", "JAVA_OPTIONS",
            "CATALINA_OPTS", "CATALINA_JAVA_OPTS",
            "MAVEN_OPTS", "M2_OPTS",
            "GRADLE_OPTS",
            "ANT_OPTS",
            "JBOSS_JAVA_OPTS", "WILDFLY_OPTS",
            "CLASSPATH", "JAVA_HOME", "JRE_HOME", "JDK_HOME",
  
            // Node.js & NPM Injection - Covers direct runtime, npm config wrappers, and rogue CA injection
            "NODE_OPTIONS", "NODE_PATH", "NODE_EXTRA_CA_CERTS",
            "NPM_CONFIG_PREFIX", "NPM_CONFIG_USERCONFIG", "NPM_CONFIG_GLOBALCONFIG",
  
            // Python Injection - Covers interpreters and package manager wrappers
            "PYTHONSTARTUP", "PYTHONPATH", "PYTHONHOME",
            "PYTHONIOENCODING", "PYTHONFAULTHANDLER", "PYTHONUSERBASE", "PYTHONEXECUTABLE",
            
            // Ruby & Perl Injection
            "RUBYOPT", "RUBYLIB", "PERL5OPT", "PERL5LIB",

            // PHP Injection - Prevents loading malicious extensions or rogue php.ini files
            "PHPRC", "PHP_INI_SCAN_DIR",
  
            // Global/Unix-like fallback (for MinGW/WSL/Cygwin contexts)
            "LD_PRELOAD", "LD_LIBRARY_PATH",

            // --- Windows AppCompat / Debugger Injection Vectors ---
            "__COMPAT_LAYER", "SHIM_FILE_LOG", "SHIM_DEBUG_LEVEL",
            "_NT_SYMBOL_PATH", "_NT_ALT_SYMBOL_PATH", "_NT_SOURCE_PATH",
            "MICROSOFT_TELEMETRY_ENV_OVERRIDE",

            // PowerShell Injection / Hardening Bypass
            "__PSLockDownPolicy",          // Forces a weaker LanguageMode at PS startup
            "PSExecutionPolicyPreference", // Overrides ExecutionPolicy at PS startup
        };

        /// <summary>
        /// Builds a dictionary of environment variables by merging the current system environment
        /// with the provided custom environment variables. All values are expanded so that system
        /// and custom variables can reference each other (e.g. %ProgramData%, %MY_CUSTOM_VAR%).
        /// Supports escaping '%' via '%%' (e.g. '100%%' resolves to '100%').
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

            // 2. Merge Custom Variables with SECURITY CHECK & ESCAPE PROTECTION
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

                    // Encode '%%' into a temporary token to prevent the expansion engine 
                    // from treating escaped percent signs as variable boundaries.
                    result[envVar.Name] = envVar.Value?.Replace("%%", PercentEscapeToken);
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

                    string expanded = ExpandWithDictionary(original, snapshot, key);

                    // Exponential growth guard
                    if (expanded != null && expanded.Length > AppConfig.MaxEnvVarExpandedLength)
                    {
                        Logger.Warn($"Expansion of '{key}' exceeded {AppConfig.MaxEnvVarExpandedLength} characters. Truncating to prevent memory exhaustion.");
                        expanded = expanded.Substring(0, AppConfig.MaxEnvVarExpandedLength);
                    }

                    if (!string.Equals(original, expanded, StringComparison.Ordinal))
                    {
                        result[key] = expanded;
                        changed = true;
                    }
                }

                pass++;
            }
            while (changed && pass < AppConfig.MaxEnvVarExpansionPasses);

            if (pass >= AppConfig.MaxEnvVarExpansionPasses && changed)
            {
                Logger.Warn("Environment variable expansion reached maximum pass limit. Indirect circular reference detected (e.g., A=%B%, B=%A%).");
            }

            // 4. Final layer: Apply OS-level expansion for any remaining unmapped system placeholders
            foreach (var key in result.Keys.Where(k => !string.IsNullOrEmpty(result[k])).ToList())
            {
                result[key] = Environment.ExpandEnvironmentVariables(result[key]);
            }

            // 5. Decode escaped percentages: Collapse the protective token into a literal '%'
            foreach (var key in result.Keys.Where(k => !string.IsNullOrEmpty(result[k])).ToList())
            {
                result[key] = result[key].Replace(PercentEscapeToken, "%");
            }

            return result;
        }

        /// <summary>
        /// Expands environment variables in the given input string using both system and custom variables.
        /// Supports escaping '%' via '%%'.
        /// </summary>
        /// <param name="input">The string containing environment variable references (e.g. "%ProgramFiles%\\MyApp").</param>
        /// <param name="expandedEnv">
        /// A dictionary of environment variables previously built by <see cref="ExpandEnvironmentVariables(List{EnvironmentVariable})"/>.
        /// </param>
        /// <returns>
        /// The input string with all environment variable references expanded, and escaped '%%' collapsed to '%'.
        /// </returns>
        public static string ExpandEnvironmentVariables(string input, IDictionary<string, string> expandedEnv)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Encode '%%' to protect it during dictionary and OS expansion
            string encodedInput = input.Replace("%%", PercentEscapeToken);

            // Since 'expandedEnv' is already resolved to a fixed point by the dictionary builder,
            // only one pass is needed here.
            string result = ExpandWithDictionary(encodedInput, expandedEnv, null);
            result = Environment.ExpandEnvironmentVariables(result);

            // Decode '%%' protection token back to a single literal '%'
            return result.Replace(PercentEscapeToken, "%");
        }

        /// <summary>
        /// Expands environment variables in a string using the provided dictionary of variables.
        /// Custom variables override system variables. Windows built-in expansion is also applied
        /// to handle system-defined placeholders such as %SystemRoot%.
        /// </summary>
        /// <param name="value">The string to expand.</param>
        /// <param name="variables">The dictionary of environment variables to use during expansion.</param>
        /// <param name="currentKey">The specific variable key currently being expanded, if any.</param>
        /// <returns>The expanded string.</returns>
        private static string ExpandWithDictionary(string value, IDictionary<string, string> variables, string currentKey = null)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            string expanded = value;

            foreach (var kvp in variables)
            {
                if (string.IsNullOrEmpty(kvp.Key)) continue;
                if (kvp.Value == null) continue;   // null means "no override" - skip

                string token = "%" + kvp.Key + "%";
                string replacement = kvp.Value; // empty string is a valid replacement

                // SELF-REFERENCE GUARD:
                // If the replacement value contains the token itself (e.g., MY_PATH=%MY_PATH%;bin), 
                // we must resolve it to prevent exponential string growth during multi-pass expansion.
                // We substitute the current process's OS-level value of that variable for the token,
                // mimicking standard Windows "PATH-append" semantics.
                if (replacement.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string inheritedValue = Environment.GetEnvironmentVariable(kvp.Key);

                    // If there is no inherited OS value to append to, leave the placeholder 
                    // intact so the user understands why the append operation did not occur.
                    if (string.IsNullOrEmpty(inheritedValue))
                    {
                        Logger.Warn($"Direct cycle detected for variable '{kvp.Key}'; leaving literal placeholder.");
                        continue;
                    }

                    // If we are currently expanding the self-referential variable's OWN definition, 
                    // we substitute only the inherited OS value to avoid double-appending the suffix.
                    // If we are expanding a DIFFERENT variable or a raw string, we substitute the 
                    // fully resolved replacement. Note: MatchEvaluator prevents parser issues with '$' signs.
                    if (string.Equals(currentKey, kvp.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        replacement = inheritedValue;
                    }
                    else
                    {
                        replacement = Regex.Replace(replacement, Regex.Escape(token), m => inheritedValue, RegexOptions.IgnoreCase);
                    }
                }

                int index = 0;
                while ((index = expanded.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    expanded = expanded.Substring(0, index) + replacement + expanded.Substring(index + token.Length);
                    index += replacement.Length; // move past the inserted value

                    // Inline length guard to prevent memory exhaustion
                    if (expanded.Length > AppConfig.MaxEnvVarExpandedLength)
                    {
                        Logger.Warn($"Inline expansion exceeded {AppConfig.MaxEnvVarExpandedLength} characters during token replacement. Truncating to prevent memory exhaustion.");
                        return expanded.Substring(0, AppConfig.MaxEnvVarExpandedLength);
                    }
                }
            }

            return expanded;
        }
    }
}