namespace Servy.Testing
{
    /// <summary>
    /// Centralised timing budgets and timeout constraints shared across the test suite.
    /// Ensures CI-sensitive timeouts are tuned predictably in a single location.
    /// </summary>
    /// <remarks>
    /// Members suffixed with <c>Ms</c> are expressed in milliseconds, members suffixed
    /// with <c>Seconds</c> are expressed in seconds and members suffixed with <c>Attempts</c> are
    /// iteration counts; all other members are <see cref="TimeSpan"/> instances.
    /// </remarks>
    public static class TestTimeouts
    {
        /// <summary>
        /// A generous upper bound (20 seconds) for async operations and wait conditions that must
        /// not flake when running on loaded or resource-constrained CI build agents.
        /// </summary>
        /// <remarks>
        /// This is the single source of the <c>CiGenerous</c> budget: <see cref="CiGenerousMs"/> and
        /// <see cref="CiGenerous"/> are derived from it so the three spellings cannot drift apart.
        /// It is a wait budget only; a child process spawned purely to stay alive during a test uses
        /// <see cref="ChildSleepSeconds"/>, which is deliberately shorter so a wait always outlives
        /// the workload it observes.
        /// </remarks>
        public const int CiGenerousSeconds = 20;

        /// <summary>
        /// <see cref="CiGenerousSeconds"/> expressed in milliseconds.
        /// </summary>
        public const int CiGenerousMs = CiGenerousSeconds * 1000;

        /// <summary>
        /// <see cref="CiGenerousSeconds"/> expressed as a <see cref="TimeSpan"/>.
        /// </summary>
        public static readonly TimeSpan CiGenerous = TimeSpan.FromSeconds(CiGenerousSeconds);

        /// <summary>
        /// Observation window (1 second) for negative waits - the "wait, then assert nothing was
        /// observed" shape, where the delay is the whole test and a value that is too short lets the
        /// assertion pass vacuously because the work was never scheduled.
        /// </summary>
        /// <remarks>
        /// One second matches the scheduling ceiling the suite already assumes for a fire-and-forget
        /// body, and is deliberately far below <see cref="CiGenerous"/> so that a handful of these
        /// waits does not add a minute of pure sleep to the run.
        /// </remarks>
        public static readonly TimeSpan NegativeObservationWindow = TimeSpan.FromSeconds(1);

        /// <summary>
        /// How long (15 seconds) a spawned PowerShell process a test keeps alive as a fixture stays
        /// alive - a leaf of a process tree, or a lone child the test needs running while it acts.
        /// </summary>
        /// <remarks>
        /// Must remain greater than or equal to <see cref="ChildTimeoutSeconds"/> to ensure the leaf process
        /// does not exit before process enumeration or tree stabilization checks complete, and shorter than
        /// <see cref="CiGenerousSeconds"/> so a wait budget outlives the workload it observes.
        /// </remarks>
        public const int ChildSleepSeconds = 15;

        /// <summary>
        /// How long (15 seconds) process search polling functions like <c>WaitForProcessName</c> wait
        /// for a named child process to appear before throwing a <see cref="TimeoutException"/>.
        /// </summary>
        /// <seealso cref="ChildSleepSeconds"/>
        public const int ChildTimeoutSeconds = 15;

        /// <summary>
        /// Maximum duration (20 seconds) allocated for a process tree to fully spawn and stabilize
        /// during descendant process enumeration tests.
        /// </summary>
        public const int ProcessTreeTimeoutSeconds = 20;

        /// <summary>
        /// Spin duration (100 ms) that lets a process accumulate measurable processor time between two
        /// consecutive metric samples, so the second sample can report a CPU delta rather than a bare baseline.
        /// </summary>
        public const int CpuSampleSpinMs = 100;

        /// <summary>
        /// Poll interval (50 ms) between successive process-metric samples while waiting for a process tree
        /// to spawn and allocate.
        /// </summary>
        public const int MetricsPollIntervalMs = 50;

        /// <summary>
        /// Default timeout (30,000 ms / 30 seconds) allocated for process launcher execution blocks.
        /// </summary>
        public const int ProcessLauncherTimeoutMs = 30_000;

        /// <summary>
        /// Duration (15 seconds) assigned to child process workloads in synchronous launcher timeout tests.
        /// </summary>
        /// <remarks>
        /// Used in conjunction with shorter execution budgets to intentionally trigger timeout handling logic.
        /// </remarks>
        public const int ProcessLauncherSynchronousTimeoutSeconds = 15;

        /// <summary>
        /// Timeout budget (10 seconds) for the file-lock fixture of the process killer tests: both the poll that
        /// waits for the spawned child to acquire the exclusive lock and that child's LOCKED stdout handshake.
        /// </summary>
        public const int ProcessKillerFileLockTimeoutSeconds = 10;

        /// <summary>
        /// Per-attempt window (3 seconds) allowed for a locking process to actually exit after a single
        /// <c>KillProcessesUsingFile</c> call, before the test retries the kill.
        /// </summary>
        public const int ProcessKillerPerAttemptExitWaitSeconds = 3;

        /// <summary>
        /// Standard timeout budget (5,000 ms / 5 seconds) for waiting for process exit in process wrapper unit tests.
        /// </summary>
        public const int ProcessWrapperProcessTimeoutMs = 5000;

        /// <summary>
        /// Extended timeout budget (10,000 ms / 10 seconds) for waiting for process exit during asynchronous stream redirection tests.
        /// </summary>
        public const int ProcessWrapperProcessGenerousTimeoutMs = 10_000;

        /// <summary>
        /// Delay (500 ms) before triggering cancellation in asynchronous process wrapper execution tests.
        /// </summary>
        public static readonly TimeSpan ProcessWrapperCancellationDelay = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Short timeout budget (50 ms) used to force graceful shutdown timeouts and exercise force-kill fallback branches.
        /// </summary>
        public const int ProcessWrapperStopTimeoutMs = 50;

        /// <summary>
        /// Graceful-stop budget (1,000 ms) handed to the process wrapper stop entry points
        /// (<c>Stop</c>, <c>StopTree</c>, <c>TryStopGracefullyOrKill</c>) when the test wants the
        /// graceful attempt to be given a realistic chance rather than be tripped deliberately.
        /// </summary>
        /// <seealso cref="ProcessWrapperStopTimeoutMs"/>
        public const int ProcessWrapperGracefulStopMs = 1000;

        /// <summary>
        /// Settle window (500 ms) allowed after a force kill before the stop sequence re-checks the
        /// process, matching the production post-kill wait.
        /// </summary>
        /// <seealso cref="ProcessWrapperGracefulStopMs"/>
        public const int ProcessWrapperPostKillWaitMs = 500;

        /// <summary>
        /// Spin budget (3 seconds) for a descendant process to disappear after the tree was stopped.
        /// </summary>
        public static readonly TimeSpan DescendantExitWait = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Tight launcher budget (2,000 ms) raced against <see cref="ProcessLauncherSynchronousTimeoutSeconds"/>
        /// to deliberately trip the synchronous-launch timeout path.
        /// </summary>
        /// <seealso cref="ProcessLauncherSynchronousTimeoutSeconds"/>
        public const int ProcessLauncherTimeoutTripBudgetMs = 2_000;

        /// <summary>
        /// Maximum duration (5 seconds) allowed for service restarter operations to complete a restart cycle.
        /// </summary>
        public static readonly TimeSpan ServiceRestarterRestartTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Short timeout (1 second) used to verify that services stuck in pending states trigger a <see cref="TimeoutException"/>.
        /// </summary>
        public static readonly TimeSpan ServiceRestarterStuckInPendingStateTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Extended timeout (10 seconds) allowed for service restarter operations when handling transitional errors.
        /// </summary>
        public static readonly TimeSpan ServiceRestarterHandleTransitionalErrorTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Tight budget limit (500 ms) assigned to test early expiry conditions within restarter loops.
        /// </summary>
        /// <seealso cref="ServiceRestarterMidLoopExpiryBurn"/>
        public static readonly TimeSpan ServiceRestarterMidLoopExpiryBudget = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Artificial sleep delay (750 ms) injected inside restarter loop iterations to deliberately burn the allocation defined by <see cref="ServiceRestarterMidLoopExpiryBudget"/>.
        /// </summary>
        /// <seealso cref="ServiceRestarterMidLoopExpiryBudget"/>
        public static readonly TimeSpan ServiceRestarterMidLoopExpiryBurn = TimeSpan.FromMilliseconds(750);

        /// <summary>
        /// Standard wait budget (5 seconds) for the log tailer suite: the background loop startup
        /// deadline, the graceful termination deadline and the polls that observe a single pass.
        /// </summary>
        /// <remarks>
        /// Interpolate this constant into any exception or assertion message that states the budget,
        /// so a retune cannot leave the message naming a window that is no longer waited for.
        /// </remarks>
        public const int LogTailerWaitSeconds = 5;

        /// <summary>
        /// Extended wait budget (10 seconds) for the log tailer rotation observation, which has to
        /// outlast a reopen of the rotated file and so is deliberately longer than
        /// <see cref="LogTailerWaitSeconds"/>.
        /// </summary>
        /// <seealso cref="LogTailerWaitSeconds"/>
        public const int LogTailerRotationWaitSeconds = 10;

        /// <summary>
        /// Wait budget (15 seconds) for the log tailer pass that follows an unhandled error, which
        /// costs one linear back-off before the reopen and is therefore the longest of the three.
        /// </summary>
        /// <seealso cref="LogTailerWaitSeconds"/>
        public const int LogTailerErrorRecoveryWaitSeconds = 15;

        /// <summary>
        /// Delay (150 ms) that lets the log tailer background loop complete at least one pass before
        /// the test cancels it.
        /// </summary>
        public const int LogTailerLoopPassDelayMs = 150;

        /// <summary>
        /// Settle window (50 ms) after disposing a log tailer, long enough for a rogue background
        /// pass to surface before the post-dispose assertion reads the pass counter.
        /// </summary>
        public const int LogTailerPostDisposeSettleMs = 50;

        /// <summary>
        /// Standard wait time in milliseconds for process teardown and kill cleanup operations.
        /// </summary>
        public const int CleanupWaitMs = 2000;

        /// <summary>
        /// Default interval in milliseconds between retry polls.
        /// </summary>
        public const int PollIntervalMs = 500;

        /// <summary>
        /// Default maximum number of attempts for polling loops.
        /// </summary>
        public const int MaxPollAttempts = 10;
    }
}
