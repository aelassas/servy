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
            
            // .NET & CLR Runtime Injection / Diagnostics (Legacy & Modern CoreCLR)
            "COR_ENABLE_PROFILING", "COR_PROFILER", "COR_PROFILER_PATH",
            "CORECLR_ENABLE_PROFILING", "CORECLR_PROFILER", "CORECLR_PROFILER_PATH",
            "DOTNET_STARTUP_HOOKS", "DOTNET_ROOT", "DOTNET_ROOT(x86)", "DOTNET_HOST_PATH",
            "DOTNET_BUNDLE_EXTRACT_BASE_DIR", "DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE",
            
            // Modern Diagnostic Attach Surfaces
            "DOTNET_DiagnosticPorts",          "COMPlus_DiagnosticPorts",
            "DOTNET_EnableDiagnostics",        "COMPlus_EnableDiagnostics",
            "DOTNET_EnableDiagnostics_IPC",    "COMPlus_EnableDiagnostics_IPC",
            "DOTNET_EnableDiagnostics_Profiler","COMPlus_EnableDiagnostics_Profiler",
            "DOTNET_EnableEventPipe",          "COMPlus_EnableEventPipe",
            
            // Runtime Custom Component Loading & Assembly Layout Adjustments
            "DOTNET_GCName",                   "COMPlus_GCName",
            "DOTNET_GCPath",                   "COMPlus_GCPath",
            "DOTNET_LegacyHostPolicy",         "COMPlus_LegacyHostPolicy",
            "DOTNET_LegacyTransform",          "COMPlus_LegacyTransform",
            "DOTNET_PerfMapEnabled",           "COMPlus_PerfMapEnabled",
            "DOTNET_ZapDisable",               "COMPlus_ZapDisable",
            
            // MiniDump Storage Layout Targets (Prevents sensitive memory leakage redirection)
            "DOTNET_DbgEnableMiniDump",        "COMPlus_DbgEnableMiniDump",
            "DOTNET_DbgMiniDumpName",          "COMPlus_DbgMiniDumpName",
            "DOTNET_DbgMiniDumpType",          "COMPlus_DbgMiniDumpType",
  
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

                        // Pass to the robust boundary trimmer to ensure no escape tokens are fragmented
                        expanded = TrimToSafeBoundary(expanded, AppConfig.MaxEnvVarExpandedLength);
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
            // only one pass is needed here. We pass 'protectInjectedValues: true' so that any literal 
            // '%' characters injected from the dictionary aren't accidentally re-expanded by the OS.
            string result = ExpandWithDictionary(encodedInput, expandedEnv, null, protectInjectedValues: true);
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
        /// <param name="protectInjectedValues">If true, encodes '%' in the substituted values to prevent later OS expansion.</param>
        /// <returns>The expanded string.</returns>
        private static string ExpandWithDictionary(string value, IDictionary<string, string> variables, string currentKey = null, bool protectInjectedValues = false)
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

                // If requested, protect any '%' in the finalized replacement value so it survives 
                // subsequent OS expansion without being re-evaluated.
                string safeReplacement = protectInjectedValues ? replacement.Replace("%", PercentEscapeToken) : replacement;

                int index = 0;
                while ((index = expanded.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    expanded = expanded.Substring(0, index) + safeReplacement + expanded.Substring(index + token.Length);
                    index += safeReplacement.Length; // move past the inserted value

                    // Inline length guard to prevent memory exhaustion
                    if (expanded.Length > AppConfig.MaxEnvVarExpandedLength)
                    {
                        Logger.Warn($"Inline expansion exceeded {AppConfig.MaxEnvVarExpandedLength} characters during token replacement. Truncating to prevent memory exhaustion.");
                        return TrimToSafeBoundary(expanded, AppConfig.MaxEnvVarExpandedLength);
                    }
                }
            }

            return expanded;
        }

        /// <summary>
        /// Truncates a string value to a specified maximum boundary limit, checking for and removing 
        /// any partial or malformed <see cref="PercentEscapeToken"/> segments split during the cut operation.
        /// </summary>
        /// <param name="value">The expanded string payload currently requiring boundary verification.</param>
        /// <param name="maxLength">The maximum character allowance ceiling permitted for the environment block.</param>
        /// <returns>A clean, validated string guaranteed to contain no corrupt sentinel marker fragments.</returns>
        private static string TrimToSafeBoundary(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length <= maxLength) return value;

            // We must check if the strict truncation boundary (maxLength) cuts directly through 
            // a PercentEscapeToken. Since the token is exactly 21 characters long, we only need 
            // to inspect indices where a token could start and subsequently straddle the cut-line.
            int startBound = Math.Max(0, maxLength - PercentEscapeToken.Length + 1);

            for (int i = startBound; i < maxLength; i++)
            {
                // Ensure the remaining string is long enough to contain the full token for comparison
                if (value.Length - i >= PercentEscapeToken.Length)
                {
                    // If a complete token starts at index 'i' (before maxLength) but requires 
                    // characters beyond 'maxLength' to finish, it straddles the boundary.
                    if (string.Compare(value, i, PercentEscapeToken, 0, PercentEscapeToken.Length, StringComparison.Ordinal) == 0)
                    {
                        Logger.Info($"Detected partial sentinel escape sequence split at boundary position {i}. Rolling back allocation window.");
                        return value.Substring(0, i);
                    }
                }
            }

            // If no token straddles the line, a standard hard truncation is perfectly safe.
            return value.Substring(0, maxLength);
        }
    }
}