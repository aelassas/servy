using Servy.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Servy.Service
{
    /// <inheritdoc />
    public class ServiceHelper : IServiceHelper
    {
        private readonly ICommandLineProvider _commandLineProvider;

        public ServiceHelper(ICommandLineProvider commandLineProvider)
        {
            _commandLineProvider = commandLineProvider;
        }

        /// <inheritdoc />
        public string[] GetSanitizedArgs()
        {
            var args = _commandLineProvider.GetArgs();
            return args.Select(a => a.Trim(' ', '"')).ToArray();
        }

        /// <inheritdoc />
        public void LogStartupArguments(ILogger logger, string[] args, StartOptions options)
        {
            if (options == null)
            {
                logger?.Error("StartOptions is null.");
                return;
            }

            logger?.Info($"[Args] {string.Join(" ", args)}");
            logger?.Info($"[Args] fullArgs Length: {args.Length}");

            logger?.Info(
              $"[Startup Parameters]\n" +
              $"- serviceName: {options.ServiceName}\n" +
              $"- realExePath: {options.ExecutablePath}\n" +
              $"- realArgs: {options.ExecutableArgs}\n" +
              $"- workingDir: {options.WorkingDirectory}\n" +
              $"- priority: {options.Priority}\n" +
              $"- stdoutFilePath: {options.StdOutPath}\n" +
              $"- stderrFilePath: {options.StdErrPath}\n" +
              $"- rotationSizeInBytes: {options.RotationSizeInBytes}\n" +
              $"- heartbeatInterval: {options.HeartbeatInterval}\n" +
              $"- maxFailedChecks: {options.MaxFailedChecks}\n" +
              $"- recoveryAction: {options.RecoveryAction}\n" +
              $"- maxRestartAttempts: {options.MaxRestartAttempts}"
          );
        }

        /// <inheritdoc />
        public void EnsureValidWorkingDirectory(StartOptions options, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(options.WorkingDirectory) ||
                !Helper.IsValidPath(options.WorkingDirectory) ||
                !Directory.Exists(options.WorkingDirectory))
            {
                var system32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
                options.WorkingDirectory = Path.GetDirectoryName(options.ExecutablePath) ?? system32;
                logger?.Warning($"Working directory fallback applied: {options.WorkingDirectory}");
            }
        }

        /// <inheritdoc />
        public bool ValidateStartupOptions(ILogger logger, StartOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.ExecutablePath))
            {
                logger?.Error("Executable path not provided.");
                return false;
            }

            if (string.IsNullOrEmpty(options.ServiceName))
            {
                logger?.Error("Service name empty");
                return false;
            }

            if (!Helper.IsValidPath(options.ExecutablePath) || !File.Exists(options.ExecutablePath))
            {
                logger?.Error($"Executable not found: {options.ExecutablePath}");
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public StartOptions InitializeStartup(ILogger logger)
        {
            var fullArgs = GetSanitizedArgs();
            var options = StartOptionsParser.Parse(fullArgs);

            LogStartupArguments(logger, fullArgs, options);

            if (!ValidateStartupOptions(logger, options))
            {
                return null;
            }

            return options;
        }
    }
}
