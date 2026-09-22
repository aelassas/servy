using Servy.Testing;

namespace Servy.Core.UnitTests.Helpers
{
    /// <summary>
    /// Covers the shared test-support helpers in <see cref="Helper"/>.
    /// </summary>
    public class TestingHelperTests
    {
        [Fact]
        public void AcceptSysinternalsEula_RegistryWriteFails_WarnsOnStandardError()
        {
            // Arrange
            // A sub key name component over the registry 255 character limit cannot be created, so this
            // reaches the best-effort catch without disturbing the real HKCU Sysinternals key.
            var unwritableSubKey = @"Software\Servy.Tests\" + new string('x', 300);

            // Act
            var captured = ConsoleCapture.Run(() => Helper.AcceptSysinternalsEula(unwritableSubKey));

            // Assert
            // The point of the issue is that this diagnostic reaches a stream dotnet test captures,
            // so the assertion is on stderr rather than on the call returning quietly.
            Assert.Contains("Failed to pre-seed EulaAccepted registry key; handle64.exe may prompt for its EULA", captured.StdErr);
        }
    }
}
