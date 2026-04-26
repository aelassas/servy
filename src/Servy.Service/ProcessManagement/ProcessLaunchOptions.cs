using Servy.Core.Config;
using Servy.Core.EnvironmentVariables;
using System;
using System.Collections.Generic;

namespace Servy.Service.ProcessManagement
{
    /// <summary>
    /// Encapsulates all configuration parameters required to launch and monitor an external process 
    /// within the Servy service infrastructure.
    /// </summary>
    public class ProcessLaunchOptions
    {
        /// <summary>
        /// Gets or sets the absolute filesystem path to the executable to be launched.
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the command-line arguments to pass to the executable. 
        /// Supports environment variable placeholders (e.g., %VAR_NAME%).
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// Gets or sets the directory in which the process will be started. 
        /// If null, the directory containing the executable is used by default.
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the list of environment variables to be injected into the process's environment block.
        /// </summary>
        public List<EnvironmentVariable> EnvironmentVariables { get; set; } = new List<EnvironmentVariable>();

        #region Behavioral Flags

        /// <summary>
        /// Gets or sets a value indicating whether the service should continue execution immediately 
        /// after launching the process without waiting for it to exit.
        /// </summary>
        public bool FireAndForget { get; set; } = false;

        /// <summary>
        /// A positive timeout, in milliseconds, to wait for the process to exit.
        /// Only applicable when <see cref="FireAndForget"/> is false; required to be &gt; 0
        /// in synchronous mode. Use <see cref="FireAndForget"/> = true for unbounded launches.
        /// </summary>
        public int TimeoutMs { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating whether errors should be logged as a Warning instead of an Error.
        /// </summary>
        public bool LogErrorAsWarning { get; set; } = false;

        /// <summary>
        /// Whether to enable the console user interface for the service.
        /// </summary>
        public bool EnableConsoleUI { get; set; }

        #endregion

        #region Timing Configuration

        /// <summary>
        /// Gets or sets the interval, in milliseconds, at which the launcher checks the process status 
        /// and invokes the <see cref="OnScmHeartbeat"/> delegate.
        /// </summary>
        public int WaitChunkMs { get; set; } = AppConfig.DefaultWaitChunkMs;

        /// <summary>
        /// Gets or sets the amount of additional time, in milliseconds, to request from the 
        /// Service Control Manager (SCM) during each heartbeat pulse.
        /// </summary>
        public int ScmAdditionalTimeMs { get; set; } = AppConfig.DefaultScmAdditionalTimeMs;

        #endregion

        #region Redirection

        /// <summary>
        /// Gets or sets a value indicating whether standard output and error should be redirected 
        /// to the service's internal rotating log writers.
        /// </summary>
        public bool RedirectToWriters { get; set; } = false;

        /// <summary>
        /// Gets or sets the optional filesystem path where standard output will be appended.
        /// </summary>
        public string StdOutPath { get; set; }

        /// <summary>
        /// Gets or sets the optional filesystem path where standard error will be appended.
        /// </summary>
        public string StdErrPath { get; set; }

        #endregion

        /// <summary>
        /// Gets or sets the delegate invoked during long-running wait operations to inform the 
        /// Windows Service Control Manager that the service is still responsive.
        /// </summary>
        /// <remarks>
        /// The integer parameter represents the additional time hint (in milliseconds) to request.
        /// </remarks>
        public Action<int> OnScmHeartbeat { get; set; }
    }
}