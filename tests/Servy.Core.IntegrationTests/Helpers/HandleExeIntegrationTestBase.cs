using System.IO;
using Xunit;

namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// Shared abstract base class managing the thread-safe extraction and registration 
    /// of Sysinternals diagnostic assets across separate integration test containers.
    /// </summary>
    public abstract class HandleExeIntegrationTestBase
    {
        protected readonly string _handleExePath;
        private static readonly object _extractionLock = new object();

        protected HandleExeIntegrationTestBase()
        {
            // Thread-safe isolation block prevents parallel suites from fighting over disk access handles
            lock (_extractionLock)
            {
                // 1. Force execution asset extraction to disk
                Testing.Helper.ExtractHandleExe();

                // 2. Fetch the resolved cross-architecture path string token
                _handleExePath = Testing.Helper.HandleExePath;

                // 3. CRITICAL DEFECT GUARD: Assert file physically exists right now
                // If extraction fails due to directory locks, this stops the test context immediately with an explicit error.
                Assert.True(File.Exists(_handleExePath), $"Lifecycle Extraction Fault: '{_handleExePath}' could not be verified on the local disk file table.");

                // Auto-accept Sysinternals EULA in the registry hive context to prevent headless runner hangs
                Testing.Helper.AcceptSysinternalsEula();
            }
        }
    }
}