using System;

namespace Servy.Testing
{
    public static class TestTimeouts
    {
        public static readonly TimeSpan CiGenerous = TimeSpan.FromSeconds(20);
        public const int ChildSleepSeconds = 15;
        public const int ChildTimeoutSeconds = 15;
        public const int ProcessTreeTimeoutSeconds = 20;
        public const int ProcessLauncherTimeoutMs = 30_000;
        public const int ProcessLauncherSynchronousTimeoutSeconds = 15;
        public const int ProcessLauncherEventQueueTimeoutMs = 250;
        public const int ProcessWrapperProcessTimeoutMs = 5000;
        public const int ProcessWrapperProcessTimeoutMsGenerous = 10_000;
        public static readonly TimeSpan ProcessWrapperCancellationDelay = TimeSpan.FromMilliseconds(500);
        public const int ProcessWrapperStopTimeoutMs = 50;
        public const int ProcessWrapperStopDescendantsTimeoutMs = 500;
        public static readonly TimeSpan ServiceRestarterRestartTimeout = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan ServiceRestarterStuckInPendingStateTimeout = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan ServiceRestarterHandleTransitionalErrorTimeout = TimeSpan.FromSeconds(10);
    }
}
