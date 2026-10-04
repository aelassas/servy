using Microsoft.Win32;
using Servy.Core.Config;
using Servy.Core.Logging;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Keeps the process environment in step with the System environment variables in the registry, so a System
    /// variable added, changed or removed in Windows after the process started is seen without a restart (#7393).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A process receives its environment block when it starts and never sees a later change. A Windows service is
    /// the worst case: it inherits the Service Control Manager's environment, which is built at boot.
    /// </para>
    /// <para>
    /// The System variables are read from <c>HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment</c>
    /// and the user's from <c>HKCU\Environment</c>, both read-only, so no elevation is needed. Which process values
    /// may be replaced depends on where the environment came from; see <see cref="InitializeForService"/> and
    /// <see cref="InitializeForApplication"/>.
    /// </para>
    /// </remarks>
    public static class SystemEnvironmentHelper
    {
        /// <summary>
        /// The registry key, under <see cref="Registry.LocalMachine"/>, that holds the System environment variables.
        /// </summary>
        internal const string SystemEnvironmentSubKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";

        /// <summary>
        /// The registry key, under <see cref="Registry.CurrentUser"/>, that holds the user's environment variables.
        /// </summary>
        internal const string UserEnvironmentSubKey = "Environment";

        /// <summary>
        /// The refresher every public member of this class uses.
        /// </summary>
        private static readonly SystemEnvironmentRefresher Default =
            new SystemEnvironmentRefresher(ReadSystemVariables, ReadUserVariables, SetProcessVariable);

        /// <summary>
        /// Declares that this process is the Servy service wrapper, whose environment was built by the Service Control
        /// Manager at boot: from now on every System variable takes its current registry value, unless the user's own
        /// environment defines the same name (<c>Path</c> is composed of both, the System part first, as Windows does).
        /// </summary>
        public static void InitializeForService() => Default.UseRegistryAsSource();

        /// <summary>
        /// Declares that this process is an application whose environment was built when it started, and records the
        /// System variables as they are now. A System variable changed later is updated only while this process still
        /// holds the value it had at that point, so a value overridden by a User variable or by the parent process is kept.
        /// </summary>
        /// <remarks>Call it as early as possible: a change made before the call is not detected.</remarks>
        public static void InitializeForApplication() => Default.CaptureBaseline();

        /// <summary>
        /// Applies the System variables added, changed or removed in the registry since the process started to the
        /// process environment, under the rules set by <see cref="InitializeForService"/> or
        /// <see cref="InitializeForApplication"/>. Without either call, the first refresh records the baseline, so only
        /// variables missing from the process are added.
        /// </summary>
        public static void RefreshProcessEnvironment() => Default.Refresh();

        /// <summary>
        /// Expands environment variable placeholders in the specified string against the process environment, after
        /// bringing it up to date with the System variables in the registry.
        /// </summary>
        /// <param name="input">The path or string containing percent-enclosed placeholders (e.g. <c>%PYTHON_EXE%</c>).</param>
        /// <returns>
        /// The string with its placeholders replaced by their current values. A <see langword="null"/> or empty
        /// <paramref name="input"/>, or one without a <c>%</c>, is returned unchanged without reading the registry; a
        /// placeholder that no variable defines is kept.
        /// </returns>
        public static string ExpandSystemEnvironmentVariables(string input)
            => ExpandSystemEnvironmentVariables(input, Default);

        /// <summary>
        /// Expands environment variable placeholders after refreshing the process environment with the supplied
        /// refresher; the seam behind <see cref="ExpandSystemEnvironmentVariables(string)"/>.
        /// </summary>
        /// <param name="input">The path or string containing percent-enclosed placeholders.</param>
        /// <param name="refresher">The refresher that brings the process environment up to date.</param>
        /// <returns>The expanded string; see <see cref="ExpandSystemEnvironmentVariables(string)"/>.</returns>
        internal static string ExpandSystemEnvironmentVariables(string input, SystemEnvironmentRefresher refresher)
        {
            if (string.IsNullOrEmpty(input) || input.IndexOf('%') < 0)
                return input;

            refresher.Refresh();
            return Environment.ExpandEnvironmentVariables(input);
        }

        /// <summary>
        /// Reads the System environment variables from the registry, read-only, with their raw values.
        /// </summary>
        /// <returns>The variables by name with their unexpanded values, or <see langword="null"/> when unreadable.</returns>
        internal static IReadOnlyDictionary<string, string>? ReadSystemVariables()
            => ReadVariables(Registry.LocalMachine, SystemEnvironmentSubKey);

        /// <summary>
        /// Reads the current user's environment variables from the registry, read-only, with their raw values.
        /// </summary>
        /// <returns>The variables by name with their unexpanded values, or <see langword="null"/> when unreadable.</returns>
        internal static IReadOnlyDictionary<string, string>? ReadUserVariables()
            => ReadVariables(Registry.CurrentUser, UserEnvironmentSubKey);

        /// <summary>
        /// Reads every value of a registry key as raw strings.
        /// </summary>
        /// <param name="hive">The hive the key is under.</param>
        /// <param name="subKey">The key path.</param>
        /// <returns>
        /// The values by name (case-insensitive), or <see langword="null"/> when the key is missing or cannot be read.
        /// </returns>
        /// <remarks>
        /// <see cref="RegistryValueOptions.DoNotExpandEnvironmentNames"/> keeps a <c>REG_EXPAND_SZ</c> value raw, so it
        /// is expanded once, against the refreshed process environment, rather than against a partial one here.
        /// </remarks>
        private static IReadOnlyDictionary<string, string>? ReadVariables(RegistryKey hive, string subKey)
        {
            try
            {
                using (var key = hive.OpenSubKey(subKey, writable: false))
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
                Logger.Debug($"Could not read the environment variables in '{hive.Name}\\{subKey}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sets or removes a variable in the current process environment.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The expanded value, or <see langword="null"/> to remove the variable.</param>
        private static void SetProcessVariable(string name, string? value)
            => Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
    }

    /// <summary>
    /// Applies the System variables in the registry to the process environment; the testable core of
    /// <see cref="SystemEnvironmentHelper"/>, with the registry reads and the process writes behind delegates.
    /// </summary>
    internal sealed class SystemEnvironmentRefresher
    {
        /// <summary>
        /// Variables Windows fills in per account and per session when it builds an environment block, never from the
        /// System key alone. <c>USERNAME=SYSTEM</c> is in the System key, so without this list a service running under a
        /// user account would be told it is SYSTEM.
        /// </summary>
        internal static readonly HashSet<string> PerAccountVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "USERNAME", "USERDOMAIN", "USERDOMAIN_ROAMINGPROFILE", "USERPROFILE", "ALLUSERSPROFILE", "APPDATA",
            "LOCALAPPDATA", "HOMEDRIVE", "HOMEPATH", "HOMESHARE", "LOGONSERVER", "COMPUTERNAME", "PUBLIC",
            "SystemRoot", "SystemDrive", "ProgramData", "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432",
            "CommonProgramFiles", "CommonProgramFiles(x86)", "CommonProgramW6432", "OneDrive",
        };

        /// <summary>A planned update of one variable during a refresh.</summary>
        private sealed class Change
        {
            /// <summary>Gets or sets the variable name.</summary>
            public string Name { get; set; } = string.Empty;

            /// <summary>Gets or sets the raw System value to apply, or <see langword="null"/> to remove the variable.</summary>
            public string? Raw { get; set; }

            /// <summary>Gets or sets the raw user value joined after the System value (<c>Path</c> only).</summary>
            public string? UserRaw { get; set; }

            /// <summary>Gets or sets the text kept after the System part of the process value, e.g. the user part of <c>Path</c>.</summary>
            public string Suffix { get; set; } = string.Empty;
        }

        private readonly Func<IReadOnlyDictionary<string, string>?> _readSystem;
        private readonly Func<IReadOnlyDictionary<string, string>?> _readUser;
        private readonly Action<string, string?> _setProcess;
        private readonly string _pathName;
        private readonly object _gate = new object();
        private Dictionary<string, string>? _baseline;
        private bool _registryIsSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemEnvironmentRefresher"/> class.
        /// </summary>
        /// <param name="readSystem">Returns the System variables with their raw values, or <see langword="null"/>.</param>
        /// <param name="readUser">Returns the current user's variables with their raw values, or <see langword="null"/>.</param>
        /// <param name="setProcess">Sets (or, with <see langword="null"/>, removes) a process variable; an exception it throws is logged and ignored.</param>
        /// <param name="pathName">The variable Windows composes from its System and user values; <c>Path</c> outside tests.</param>
        internal SystemEnvironmentRefresher(
            Func<IReadOnlyDictionary<string, string>?> readSystem,
            Func<IReadOnlyDictionary<string, string>?> readUser,
            Action<string, string?> setProcess,
            string pathName = "Path")
        {
            _readSystem = readSystem;
            _readUser = readUser;
            _setProcess = setProcess;
            _pathName = pathName;
        }

        /// <summary>
        /// Makes the registry the source of every System variable; see <see cref="SystemEnvironmentHelper.InitializeForService"/>.
        /// </summary>
        internal void UseRegistryAsSource()
        {
            lock (_gate)
            {
                _registryIsSource = true;
            }
        }

        /// <summary>
        /// Records the current System variables as the baseline, unless one is already recorded; see
        /// <see cref="SystemEnvironmentHelper.InitializeForApplication"/>.
        /// </summary>
        internal void CaptureBaseline()
        {
            lock (_gate)
            {
                if (_baseline == null)
                {
                    var current = _readSystem();
                    if (current != null)
                        _baseline = Copy(current);
                }
            }
        }

        /// <summary>
        /// Applies the System variables added, changed or removed since the baseline (or, in registry-source mode, every
        /// System variable whose process value differs) to the process environment.
        /// </summary>
        internal void Refresh()
        {
            lock (_gate)
            {
                var current = _readSystem();
                if (current == null)
                    return;

                Apply(_registryIsSource ? PlanFromRegistry(current) : PlanFromBaseline(current));

                if (!_registryIsSource)
                    _baseline = Copy(current);
            }
        }

        /// <summary>
        /// Copies a set of variables into a case-insensitive dictionary.
        /// </summary>
        /// <param name="variables">The variables to copy.</param>
        /// <returns>The copy.</returns>
        private static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string> variables)
        {
            var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var variable in variables)
                copy[variable.Key] = variable.Value;

            return copy;
        }

        /// <summary>
        /// Plans the changes for a service: every System variable takes its registry value unless the user defines it.
        /// </summary>
        /// <param name="current">The System variables now in the registry.</param>
        /// <returns>The variables to set.</returns>
        private List<Change> PlanFromRegistry(IReadOnlyDictionary<string, string> current)
        {
            var user = _readUser() ?? new Dictionary<string, string>();
            var changes = new List<Change>();

            foreach (var variable in current)
            {
                if (string.IsNullOrEmpty(variable.Value) || PerAccountVariables.Contains(variable.Key))
                    continue;

                string? userRaw;
                if (user.TryGetValue(variable.Key, out userRaw) && !string.IsNullOrEmpty(userRaw))
                {
                    // A User variable wins over a System one of the same name, except Path, which joins both
                    if (string.Equals(variable.Key, _pathName, StringComparison.OrdinalIgnoreCase))
                        changes.Add(new Change { Name = variable.Key, Raw = variable.Value, UserRaw = userRaw });

                    continue;
                }

                changes.Add(new Change { Name = variable.Key, Raw = variable.Value });
            }

            return changes;
        }

        /// <summary>
        /// Plans the changes for an application: added variables, and changed or removed ones the process has not overridden.
        /// </summary>
        /// <param name="current">The System variables now in the registry.</param>
        /// <returns>The variables to set or remove.</returns>
        private List<Change> PlanFromBaseline(IReadOnlyDictionary<string, string> current)
        {
            var baseline = _baseline ?? Copy(current);
            var changes = new List<Change>();

            foreach (var variable in current)
            {
                if (string.IsNullOrEmpty(variable.Value) || PerAccountVariables.Contains(variable.Key))
                    continue;

                var process = Environment.GetEnvironmentVariable(variable.Key);
                if (process == null)
                {
                    changes.Add(new Change { Name = variable.Key, Raw = variable.Value });
                    continue;
                }

                string? old;
                if (!baseline.TryGetValue(variable.Key, out old) || string.Equals(old, variable.Value, StringComparison.Ordinal))
                    continue;

                string suffix;
                if (HoldsSystemValue(process, old, out suffix))
                    changes.Add(new Change { Name = variable.Key, Raw = variable.Value, Suffix = suffix });
            }

            foreach (var removed in baseline)
            {
                string? now;
                if ((current.TryGetValue(removed.Key, out now) && !string.IsNullOrEmpty(now)) || PerAccountVariables.Contains(removed.Key))
                    continue;

                var process = Environment.GetEnvironmentVariable(removed.Key);
                string suffix;
                if (process != null && HoldsSystemValue(process, removed.Value, out suffix) && suffix.Length == 0)
                    changes.Add(new Change { Name = removed.Key, Raw = null });
            }

            return changes;
        }

        /// <summary>
        /// Tells whether a process value is still the one the System value <paramref name="oldRaw"/> produced, alone or
        /// as the leading part of a <c>;</c>-separated list (the user part of <c>Path</c>).
        /// </summary>
        /// <param name="process">The process value.</param>
        /// <param name="oldRaw">The raw System value at the baseline.</param>
        /// <param name="suffix">The text after the System part, starting with its <c>;</c>; empty for an exact match.</param>
        /// <returns><see langword="true"/> when the process value derives from <paramref name="oldRaw"/>.</returns>
        private static bool HoldsSystemValue(string process, string oldRaw, out string suffix)
        {
            suffix = string.Empty;
            var old = Environment.ExpandEnvironmentVariables(oldRaw);
            if (string.Equals(process, old, StringComparison.Ordinal))
                return true;

            if (process.StartsWith(old + ";", StringComparison.OrdinalIgnoreCase))
            {
                suffix = process.Substring(old.Length);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Applies the planned changes, expanding each value against the process environment and repeating while a
        /// value still changes, so a variable that refers to another one changed in the same refresh resolves too.
        /// </summary>
        /// <param name="changes">The planned changes.</param>
        private void Apply(List<Change> changes)
        {
            for (var pass = 0; pass < AppConfig.MaxEnvVarExpansionPasses; pass++)
            {
                var changed = false;

                foreach (var change in changes)
                {
                    string? target = null;
                    if (change.Raw != null)
                    {
                        target = Environment.ExpandEnvironmentVariables(change.Raw);
                        if (!string.IsNullOrEmpty(change.UserRaw))
                            target = target.TrimEnd(';') + ";" + Environment.ExpandEnvironmentVariables(change.UserRaw!);
                        target += change.Suffix;
                    }

                    if (string.Equals(Environment.GetEnvironmentVariable(change.Name), target, StringComparison.Ordinal))
                        continue;

                    try
                    {
                        _setProcess(change.Name, target);
                        changed = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Could not update the environment variable '{change.Name}' in the process environment: {ex.Message}");
                    }
                }

                if (!changed)
                    break;
            }
        }
    }
}
