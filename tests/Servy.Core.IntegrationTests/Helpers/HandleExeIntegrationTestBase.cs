using System.IO;
using Xunit;

namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// Base class for integration tests that need handle64.exe: extracts it, keeps its path,
    /// and accepts the Sysinternals EULA so the tool does not wait for input on a headless runner.
    /// </summary>
    public abstract class HandleExeIntegrationTestBase
    {
        protected readonly string _handleExePath;

        protected HandleExeIntegrationTestBase()
        {
            Testing.Helper.ExtractHandleExe();
            _handleExePath = Testing.Helper.HandleExePath;

            // Fail here rather than in the first test that runs handle64.exe, and name the path we looked at.
            Assert.True(File.Exists(_handleExePath), $"handle64.exe was not extracted to '{_handleExePath}'.");

            // Accepting the EULA up front stops handle64.exe from prompting for it on a headless runner.
            Testing.Helper.AcceptSysinternalsEula();
        }
    }
}
