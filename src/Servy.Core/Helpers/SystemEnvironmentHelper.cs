using Microsoft.Win32;
using Servy.Core.Config;
using Servy.Core.Logging;
using System.Text;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Expands environment variable placeholders against the current process environment and, for placeholders
    /// the process environment does not know, against the System environment variables stored in the registry.
    /// </summary>
    /// <remarks>
    /// A process receives its environment block when it starts and never sees a System variable added later.
    /// A Windows service is the worst case: it inherits the Service Control Manager's environment, which is not
    /// refreshed until the machine restarts. Reading the registry lets a newly added System variable resolve
    /// without restarting the desktop app, Servy Manager or the service (#7393).
    /// </remarks>
    public static class SystemEnvironmentHelper
    {
        /// <summary>
        /// The registry key, under <see cref="Registry.LocalMachine"/>, that holds the System environment variables.
        /// </summary>
        internal const string SystemEnvironmentSubKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";

        /// <summary>
        /// Expands environment variable placeholders in the specified string.
        /// </summary>
        /// <param name="input">The path or string containing percent-enclosed placeholders (e.g. <c>%PYTHON_EXE%</c>).</param>
        /// <returns>
        /// The string with its placeholders replaced by their current values. A <see langword="null"/> or empty
        /// <paramref name="input"/> is returned unchanged, and so is a placeholder that no variable defines.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The process environment is tried first, through <see cref="Environment.ExpandEnvironmentVariables(string)"/>.
        /// Only when a <c>%...%</c> placeholder is left does it read the System variables from
        /// <c>HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment</c>, read-only, so it works in a
        /// non-elevated process and in a service running under any account.
        /// </para>
        /// <para>
        /// A System variable resolved this way is also set in the process environment
        /// (<see cref="EnvironmentVariableTarget.Process"/>), with its value already expanded, so later lookups
        /// and child processes started afterwards see it. A variable the process environment already has is never
        /// replaced: an updated value of an existing variable still needs a restart, as before.
        /// </para>
        /// </remarks>
        public static string ExpandSystemEnvironmentVariables(string input)
            => ExpandSystemEnvironmentVariables(input, ReadSystemVariables, SetProcessVariable);

        /// <summary>
        /// Expands environment variable placeholders using the supplied System-variable reader and process-variable
        /// writer; the seam behind <see cref="ExpandSystemEnvironmentVariables(string)"/>.
        /// </summary>
        /// <param name="input">The path or string containing percent-enclosed placeholders.</param>
        /// <param name="readSystemVariables">
        /// Returns the System variables by name with their raw, unexpanded values, or <see langword="null"/> when they
        /// cannot be read. Called at most once, and only when a placeholder is left after the process expansion.
        /// </param>
        /// <param name="setProcessVariable">Sets a variable in the process environment; an exception it throws is ignored.</param>
        /// <returns>The expanded string; see <see cref="ExpandSystemEnvironmentVariables(string)"/>.</returns>
        internal static string ExpandSystemEnvironmentVariables(
            string input,
            Func<IReadOnlyDictionary<string, string>?> readSystemVariables,
            Action<string, string> setProcessVariable)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // 1. The process environment first
            var expanded = Environment.ExpandEnvironmentVariables(input);
            if (expanded.IndexOf('%') < 0)
                return expanded;

            // 2. The System variables in the registry, for the placeholders the process does not know
            var systemVariables = readSystemVariables();
            if (systemVariables == null || systemVariables.Count == 0)
                return expanded;

            // A System variable may refer to another one added at the same time, so repeat until nothing changes
            for (var pass = 0; pass < AppConfig.MaxEnvVarExpansionPasses; pass++)
            {
                var changed = false;

                foreach (var variable in systemVariables)
                {
                    var placeholder = "%" + variable.Key + "%";
                    if (string.IsNullOrEmpty(variable.Value) || expanded.IndexOf(placeholder, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    // A REG_EXPAND_SZ value such as %SystemRoot%\... is expanded the way Windows expands it
                    var value = Environment.ExpandEnvironmentVariables(variable.Value);

                    try
                    {
                        setProcessVariable(variable.Key, value);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Could not set the System environment variable '{variable.Key}' in the process environment: {ex.Message}");
                    }

                    expanded = ReplaceIgnoreCase(expanded, placeholder, value);
                    changed = true;
                }

                if (!changed || expanded.IndexOf('%') < 0)
                    break;
            }

            return expanded;
        }

        /// <summary>
        /// Gets the System variables that are defined in the registry but missing from the process environment, which
        /// is the case for every System variable added after the process started.
        /// </summary>
        /// <returns>
        /// The missing variables by name (case-insensitive) with their values expanded the way Windows expands them;
        /// empty when there are none or the registry cannot be read.
        /// </returns>
        /// <remarks>
        /// A variable the process environment already has is never returned, so an inherited value, a value set for
        /// this process or a User variable of the same name keeps precedence.
        /// </remarks>
        public static IReadOnlyDictionary<string, string> GetSystemVariablesMissingFromProcess()
            => GetSystemVariablesMissingFromProcess(ReadSystemVariables);

        /// <summary>
        /// Gets the System variables missing from the process environment using the supplied reader; the seam behind
        /// <see cref="GetSystemVariablesMissingFromProcess()"/>.
        /// </summary>
        /// <param name="readSystemVariables">Returns the System variables with their raw values, or <see langword="null"/>.</param>
        /// <returns>The missing variables with their expanded values.</returns>
        internal static IReadOnlyDictionary<string, string> GetSystemVariablesMissingFromProcess(
            Func<IReadOnlyDictionary<string, string>?> readSystemVariables)
        {
            var missing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var systemVariables = readSystemVariables();
            if (systemVariables == null)
                return missing;

            foreach (var variable in systemVariables)
            {
                if (string.IsNullOrEmpty(variable.Value) || Environment.GetEnvironmentVariable(variable.Key) != null)
                    continue;

                missing[variable.Key] = Environment.ExpandEnvironmentVariables(variable.Value);
            }

            return missing;
        }

        /// <summary>
        /// Replaces every occurrence of <paramref name="oldValue"/> in <paramref name="source"/>, ignoring case.
        /// </summary>
        /// <param name="source">The string to search.</param>
        /// <param name="oldValue">The text to replace; environment variable names are case-insensitive on Windows.</param>
        /// <param name="newValue">The replacement text; <see langword="null"/> removes each occurrence.</param>
        /// <returns>
        /// The string with every occurrence replaced, or <paramref name="source"/> unchanged when it or
        /// <paramref name="oldValue"/> is <see langword="null"/> or empty, or when <paramref name="oldValue"/> does not occur.
        /// </returns>
        internal static string ReplaceIgnoreCase(string source, string oldValue, string? newValue)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(oldValue))
                return source;

            var index = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return source;

            var sb = new StringBuilder();
            var previousIndex = 0;

            while (index >= 0)
            {
                sb.Append(source, previousIndex, index - previousIndex);
                sb.Append(newValue ?? string.Empty);
                previousIndex = index + oldValue.Length;
                index = source.IndexOf(oldValue, previousIndex, StringComparison.OrdinalIgnoreCase);
            }

            sb.Append(source, previousIndex, source.Length - previousIndex);
            return sb.ToString();
        }

        /// <summary>
        /// Reads the System environment variables from the registry, read-only, with their raw values.
        /// </summary>
        /// <returns>
        /// The variables by name (case-insensitive) with their unexpanded values, or <see langword="null"/> when the
        /// key is missing or cannot be read.
        /// </returns>
        /// <remarks>
        /// <see cref="RegistryValueOptions.DoNotExpandEnvironmentNames"/> keeps a <c>REG_EXPAND_SZ</c> value raw, so it
        /// is expanded once, by the caller, rather than against a partial environment here. The key is readable by
        /// every account, so no elevation is needed.
        /// </remarks>
        internal static IReadOnlyDictionary<string, string>? ReadSystemVariables()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(SystemEnvironmentSubKey, writable: false))
                {
                    if (key == null)
                        return null;

                    var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var name in key.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(name))
                            continue;

                        var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();
                        if (value != null)
                            variables[name] = value;
                    }

                    return variables;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Could not read the System environment variables from the registry: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sets a variable in the current process environment.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The expanded value.</param>
        private static void SetProcessVariable(string name, string value)
            => Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
    }
}
