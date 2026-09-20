using Servy.Core.Config;
using System;
using System.IO;
using Xunit;

namespace Servy.Core.IntegrationTests.Setup
{
    /// <summary>
    /// Pins the convergence of the taskschd VBScript launchers. The pair has been edited back into
    /// agreement by hand several times (#4782 quoting and layout, #5879 the event-log call, #2210 the
    /// propagated exit code, #6634 the pinned powershell.exe path) without the invariant ever being
    /// established, so an edit to one copy could re-open the same class of drift unnoticed. This is the
    /// .vbs counterpart of the event-ID parity theory that already guards their .ps1 siblings.
    /// </summary>
    public class TaskSchdLauncherConvergenceIntegrationTests
    {
        private const string TaskSchdPath = "setup/taskschd";
        private const string BaseNamePlaceholder = "<launcher>";

        /// <summary>
        /// The launchers that must stay interchangeable. Each differs from the others only by its own
        /// base name, which <see cref="NormalizeBaseName"/> removes before the comparison. A third
        /// notification task adds its file name here rather than a new assertion.
        /// </summary>
        private static readonly string[] LauncherFileNames =
        {
            "ServyFailureEmail.vbs",
            "ServyFailureNotification.vbs",
        };

        private readonly string _taskSchdDir;

        public TaskSchdLauncherConvergenceIntegrationTests()
        {
            // Same repo-root lookup the event-ID parity theory uses, so both run from the test bin folder.
            _taskSchdDir = Path.Combine(AppConfig.FindRepoRoot(AppDomain.CurrentDomain.BaseDirectory), TaskSchdPath);
        }

        /// <summary>
        /// Fails when any edit to one launcher is not mirrored into the others. Only each file's own
        /// base name may differ; everything else has to be byte-identical.
        /// </summary>
        [Fact]
        public void VbsLaunchers_DifferOnlyInTheirOwnBaseName()
        {
            // Arrange
            string referenceName = LauncherFileNames[0];
            string reference = ReadNormalized(referenceName);

            // Act & Assert
            for (int i = 1; i < LauncherFileNames.Length; i++)
            {
                string otherName = LauncherFileNames[i];
                Assert.True(
                    reference == ReadNormalized(otherName),
                    $"{referenceName} and {otherName} differ by more than their own base name. " +
                    "Mirror the edit into every taskschd .vbs launcher, or the pair drifts apart again.");
            }
        }

        /// <summary>
        /// Guards the theory above against passing vacuously: if a launcher stopped naming itself,
        /// normalization would remove nothing and two genuinely different files could still compare
        /// equal on the parts that happen to match.
        /// </summary>
        [Fact]
        public void VbsLaunchers_EachReferenceTheirOwnBaseName()
        {
            foreach (string fileName in LauncherFileNames)
            {
                string text = File.ReadAllText(Path.Combine(_taskSchdDir, fileName));
                Assert.Contains(Path.GetFileNameWithoutExtension(fileName), text);
            }
        }

        private string ReadNormalized(string fileName)
        {
            return NormalizeBaseName(
                File.ReadAllText(Path.Combine(_taskSchdDir, fileName)),
                Path.GetFileNameWithoutExtension(fileName));
        }

        private static string NormalizeBaseName(string text, string baseName)
        {
            return text.Replace(baseName, BaseNamePlaceholder);
        }
    }
}
