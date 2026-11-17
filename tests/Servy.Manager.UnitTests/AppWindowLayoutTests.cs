using System.Collections.Generic;
using Xunit;

namespace Servy.Manager.UnitTests
{
    /// <summary>
    /// Tests for <see cref="App.GetLayoutChanges(string, string, string, string, string, string)"/>, which selects the
    /// window layout settings that <see cref="App.SaveWindowLayout(string, string, string)"/> writes to AppSettings.
    /// </summary>
    public class AppWindowLayoutTests
    {
        [Fact]
        public void GetLayoutChanges_EveryValueChanged_ReturnsTheThreeSettingsUnderTheirConfigurationKeys()
        {
            // Act
            var changes = App.GetLayoutChanges(
                string.Empty, "10,20,1200,700,Normal",
                "Name=100", "Name=180",
                string.Empty, "Time=200");

            // Assert
            Assert.Equal(
                new[]
                {
                    new KeyValuePair<string, string>("MainWindowPlacement", "10,20,1200,700,Normal"),
                    new KeyValuePair<string, string>("ServicesColumnWidths", "Name=180"),
                    new KeyValuePair<string, string>("LogsColumnWidths", "Time=200"),
                },
                changes);
        }

        [Fact]
        public void GetLayoutChanges_NullOrUnchangedValue_IsLeftOut()
        {
            // Act: the placement is unchanged, the services widths are not supplied, the logs widths are cleared
            var changes = App.GetLayoutChanges(
                "10,20,1200,700,Normal", "10,20,1200,700,Normal",
                "Name=100", null,
                "Time=200", string.Empty);

            // Assert
            Assert.Equal(new[] { new KeyValuePair<string, string>("LogsColumnWidths", string.Empty) }, changes);
        }

        [Fact]
        public void GetLayoutChanges_NothingChanged_ReturnsNoSettings()
        {
            // Act
            var changes = App.GetLayoutChanges(
                "10,20,1200,700,Normal", "10,20,1200,700,Normal",
                "Name=100", "Name=100",
                string.Empty, null);

            // Assert
            Assert.Empty(changes);
        }
    }
}
