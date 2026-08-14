namespace Servy.Testing
{
    /// <summary>
    /// Centralised timing budgets and timeout constraints shared across the test suite.
    /// Ensures CI-sensitive timeouts are tuned predictably in a single location.
    /// </summary>
    /// <remarks>
    /// Members suffixed with <c>Ms</c> are expressed in milliseconds and members suffixed
    /// with <c>Seconds</c> are expressed in seconds; all other members are <see cref="TimeSpan"/> instances.
    /// </remarks>
    public static class TestTimeouts
    {
        /// <summary>
        /// A generous upper bound (20 seconds) for async operations and wait conditions that must
        /// not flake when running on loaded or resource-constrained CI build agents.
        /// </summary>
        public static readonly TimeSpan CiGenerous = TimeSpan.FromSeconds(20);

        /// <summary>
        /// How long (15 seconds) a spawned PowerShell leaf process in a process tree fixture stays alive.
        /// </summary>
        /// <remarks>
        /// Must remain greater than or equal to <see cref="ChildTimeoutSeconds"/> to ensure the leaf process
        /// does not exit before process enumeration or tree stabilization checks complete.
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
    }
}
