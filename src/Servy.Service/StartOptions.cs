using Servy.Core;
using System.Diagnostics;

namespace Servy.Service
{
    public class StartOptions
    {
        public string ExecutablePath { get; set; }
        public string ExecutableArgs { get; set; }
        public string WorkingDirectory { get; set; }
        public ProcessPriorityClass Priority { get; set; } = ProcessPriorityClass.Normal;

        public string StdOutPath { get; set; }
        public string StdErrPath { get; set; }
        public int RotationSizeInBytes { get; set; }

        public int HeartbeatInterval { get; set; }
        public int MaxFailedChecks { get; set; }
        public RecoveryAction RecoveryAction { get; set; } = RecoveryAction.None;

        public string ServiceName { get; set; }
        public int MaxRestartAttempts { get; set; } = 3;
    }

}
