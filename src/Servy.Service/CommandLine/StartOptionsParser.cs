using Servy.Core.Enums;
using Servy.Core.EnvironmentVariables;
using System.Diagnostics;

namespace Servy.Service.CommandLine
{
    /// <summary>
    /// Provides functionality to parse command-line arguments into a <see cref="StartOptions"/> object.
    /// </summary>
    public static class StartOptionsParser
    {
        /// <summary>
        /// Parses the specified array of command-line arguments into a <see cref="StartOptions"/> instance.
        /// </summary>
        /// <param name="fullArgs">An array of strings representing the command-line arguments.</param>
        /// <returns>
        /// A <see cref="StartOptions"/> object populated with values parsed from the input arguments.
        /// Missing or invalid values will be set to default values.
        /// </returns>
        public static StartOptions Parse(string[] fullArgs)
        {
            fullArgs = fullArgs.Select(a => a.Trim(' ', '"')).ToArray();

            return new StartOptions
            {
                ExecutablePath = fullArgs.Length > 1 ? fullArgs[1] : string.Empty,
                ExecutableArgs = fullArgs.Length > 2 ? fullArgs[2] : string.Empty,
                WorkingDirectory = fullArgs.Length > 3 ? fullArgs[3] : string.Empty,
                Priority = fullArgs.Length > 4 && Enum.TryParse(fullArgs[4], true, out ProcessPriorityClass p) ? p : ProcessPriorityClass.Normal,
                StdOutPath = fullArgs.Length > 5 ? fullArgs[5] : string.Empty,
                StdErrPath = fullArgs.Length > 6 ? fullArgs[6] : string.Empty,
                RotationSizeInBytes = fullArgs.Length > 7 && int.TryParse(fullArgs[7], out int rsb) ? rsb : 0,
                HeartbeatInterval = fullArgs.Length > 8 && int.TryParse(fullArgs[8], out int hbi) ? hbi : 0,
                MaxFailedChecks = fullArgs.Length > 9 && int.TryParse(fullArgs[9], out int mfc) ? mfc : 0,
                RecoveryAction = fullArgs.Length > 10 && Enum.TryParse(fullArgs[10], true, out RecoveryAction ra) ? ra : RecoveryAction.None,
                ServiceName = fullArgs.Length > 11 ? fullArgs[11] : string.Empty,
                MaxRestartAttempts = fullArgs.Length > 12 && int.TryParse(fullArgs[12], out int mra) ? mra : 3,
                EnvironmentVariables = EnvironmentVariableParser.Parse(fullArgs.Length > 13 ? fullArgs[13] : string.Empty),
            };
        }
    }
}
