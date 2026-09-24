using Servy.Core.Config;
using System;
using System.IO;
using Xunit;

namespace Servy.Core.IntegrationTests.Setup
{
    /// <summary>
    /// Pins the convergence of the taskschd VBScript launchers. Since the launchers dynamically
    /// resolve their sibling .ps1 script using their own file base name, all launcher files under
    /// setup/taskschd must remain strictly byte-identical on disk.
    /// </summary>
    public class TaskSchdLauncherConvergenceIntegrationTests
    {
        private const string TaskSchdPath = "setup/taskschd";

        /// <summary>
        /// The launchers that must stay byte-identical.
        /// </summary>
        private static readonly string[] LauncherFileNames =
        {
            "ServyFailureEmail.vbs",
            "ServyFailureNotification.vbs",
        };

        private readonly string _taskSchdDir;

        public TaskSchdLauncherConvergenceIntegrationTests()
        {
            _taskSchdDir = Path.Combine(AppConfig.FindRepoRoot(AppDomain.CurrentDomain.BaseDirectory), TaskSchdPath);
        }

        /// <summary>
        /// Ensures all .vbs launchers are 100% byte-identical.
        /// </summary>
        [Fact]
        public void VbsLaunchers_AreByteIdentical()
        {
            // Arrange
            string referenceName = LauncherFileNames[0];
            byte[] referenceBytes = File.ReadAllBytes(Path.Combine(_taskSchdDir, referenceName));

            // Act & Assert
            for (int i = 1; i < LauncherFileNames.Length; i++)
            {
                string otherName = LauncherFileNames[i];
                byte[] otherBytes = File.ReadAllBytes(Path.Combine(_taskSchdDir, otherName));

                Assert.Equal(referenceBytes, otherBytes);
            }
        }

        /// <summary>
        /// Guards against reverting to hardcoded script references by asserting that the
        /// dynamic base-name resolution via WScript.ScriptFullName is present in all launchers.
        /// </summary>
        [Fact]
        public void VbsLaunchers_UseDynamicBaseNameResolution()
        {
            foreach (string fileName in LauncherFileNames)
            {
                string text = File.ReadAllText(Path.Combine(_taskSchdDir, fileName));
                Assert.Contains("WScript.ScriptFullName", text);
                Assert.Contains("GetBaseName", text);
            }
        }
    }
}
