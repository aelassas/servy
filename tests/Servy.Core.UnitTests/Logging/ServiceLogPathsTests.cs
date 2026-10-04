using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;

namespace Servy.Core.UnitTests.Logging
{
    /// <summary>
    /// Unit tests for <see cref="ServiceLogPaths"/>, which maps a service to its own log folder,
    /// <c>logs\services\&lt;ServiceName&gt;\</c>, under a name that is valid on NTFS and that no other service shares.
    /// </summary>
    public class ServiceLogPathsTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetFolderName_BlankServiceName_Throws(string? serviceName)
        {
            // Act
            var ex = Record.Exception(() => ServiceLogPaths.GetFolderName(serviceName!));

            // Assert
            var argumentException = Assert.IsType<ArgumentException>(ex);
            Assert.Equal("serviceName", argumentException.ParamName);
        }

        [Theory]
        [InlineData("MyService")]
        [InlineData("My Service")]
        [InlineData("my.service-1_2")]
        [InlineData("Ünïcødé服务")]
        [InlineData("CONSOLE")]
        [InlineData("COM10")]
        public void GetFolderName_SafeName_IsKeptAsItIs(string serviceName)
        {
            // Act
            var folder = ServiceLogPaths.GetFolderName(serviceName);

            // Assert
            Assert.Equal(serviceName, folder);
        }

        [Theory]
        [InlineData("a<b", "a%3Cb")]
        [InlineData("a>b", "a%3Eb")]
        [InlineData("a:b", "a%3Ab")]
        [InlineData("a\"b", "a%22b")]
        [InlineData("a/b", "a%2Fb")]
        [InlineData("a\\b", "a%5Cb")]
        [InlineData("a|b", "a%7Cb")]
        [InlineData("a?b", "a%3Fb")]
        [InlineData("a*b", "a%2Ab")]
        [InlineData("a\tb", "a%09b")]
        [InlineData("a\u007Fb", "a%7Fb")]
        [InlineData("50%", "50%25")]
        [InlineData("a~b", "a%7Eb")]
        public void GetFolderName_CharacterAFolderNameCannotHold_IsPercentEncoded(string serviceName, string expected)
        {
            // Act
            var folder = ServiceLogPaths.GetFolderName(serviceName);

            // Assert
            Assert.Equal(expected, folder);
        }

        [Theory]
        [InlineData("service.", "service%2E")]
        [InlineData("service ", "service%20")]
        [InlineData(" service", "%20service")]
        [InlineData(".", "%2E")]
        [InlineData("..", ".%2E")]
        [InlineData("a. b", "a. b")]
        public void GetFolderName_DotOrSpaceWindowsWouldStrip_IsEncoded(string serviceName, string expected)
        {
            // Act
            var folder = ServiceLogPaths.GetFolderName(serviceName);

            // Assert
            Assert.Equal(expected, folder);
        }

        [Theory]
        [InlineData("CON", "%43ON")]
        [InlineData("con", "%63on")]
        [InlineData("PRN", "%50RN")]
        [InlineData("AUX", "%41UX")]
        [InlineData("NUL", "%4EUL")]
        [InlineData("COM1", "%43OM1")]
        [InlineData("LPT9", "%4CPT9")]
        [InlineData("nul.log", "%6Eul.log")]
        [InlineData("CON .txt", "%43ON .txt")]
        [InlineData("COM¹", "%43OM¹")]
        [InlineData("COM²", "%43OM²")]
        [InlineData("LPT³", "%4CPT³")]
        [InlineData("CONIN$", "%43ONIN$")]
        [InlineData("conout$.log", "%63onout$.log")]
        public void GetFolderName_ReservedDeviceName_HasItsFirstCharacterEncoded(string serviceName, string expected)
        {
            // Act
            var folder = ServiceLogPaths.GetFolderName(serviceName);

            // Assert
            Assert.Equal(expected, folder);
        }

        [Fact]
        public void GetFolderName_EveryNameInTheSharedReservedList_IsMadeSafe()
        {
            // Arrange: the shared list is the one every other caller guards against, so the folder name of
            // each of its entries must differ from the entry itself, whatever is added to the list later
            var reserved = ReservedNames.ReservedDeviceNames;

            // Act
            var unchanged = reserved.Where(name => ServiceLogPaths.GetFolderName(name) == name).ToList();

            // Assert
            Assert.NotEmpty(reserved);
            Assert.Empty(unchanged);
        }

        [Fact]
        public void GetFolderName_NamesThatOnlyDifferInAnEncodedCharacter_NeverShareAFolder()
        {
            // Arrange: "_" is the usual replacement character, and "%3A" is what ':' encodes to
            var names = new[] { "a:b", "a_b", "a%3Ab", "a%b", "a%25b", "CON", "%43ON", "a.", "a%2E", "a~0123456789ABCDEF" };

            // Act
            var folders = names.Select(ServiceLogPaths.GetFolderName).ToList();

            // Assert
            Assert.Equal(folders.Count, folders.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void GetFolderName_NameUpToTheLimit_IsNotShortened()
        {
            // Arrange
            var serviceName = new string('s', ServiceLogPaths.MaxFolderNameLength);

            // Act
            var folder = ServiceLogPaths.GetFolderName(serviceName);

            // Assert
            Assert.Equal(serviceName, folder);
        }

        [Fact]
        public void GetFolderName_NameUnderTheLimitWhoseEncodedFormIsOver_IsShortened()
        {
            // Arrange: 99 characters, but ':' encodes to "%3A", so the safe form is 101 characters
            var serviceName = new string('s', 98) + ":";

            // Act
            var folder = ServiceLogPaths.GetFolderName(serviceName);

            // Assert
            Assert.Equal(64 + 1 + 16, folder.Length);
            Assert.StartsWith(new string('s', 64) + "~", folder);
        }

        [Fact]
        public void GetFolderName_NameOneOverTheLimit_IsShortened()
        {
            // Arrange
            var serviceName = new string('s', ServiceLogPaths.MaxFolderNameLength + 1);

            // Act
            var folder = ServiceLogPaths.GetFolderName(serviceName);

            // Assert
            Assert.Equal(64 + 1 + 16, folder.Length);
            Assert.StartsWith(new string('s', 64) + "~", folder);
        }

        [Fact]
        public void GetFolderName_NameMadeOnlyOfEncodedCharacters_StaysWithinTheLimit()
        {
            // Arrange: 100 characters that each encode to three
            var serviceName = new string(':', ServiceLogPaths.MaxFolderNameLength);

            // Act
            var folder = ServiceLogPaths.GetFolderName(serviceName);

            // Assert: the cut lands on the escape at index 63, so 21 whole escapes, the separator and the hash
            Assert.True(folder.Length <= ServiceLogPaths.MaxFolderNameLength);
            Assert.Equal(string.Concat(Enumerable.Repeat("%3A", 21)) + "~", folder.Substring(0, 64));
        }

        [Fact]
        public void GetFolderName_LongName_IsShortenedWithAHashOfTheName()
        {
            // Arrange
            var serviceName = new string('s', 200);

            // Act
            var folder = ServiceLogPaths.GetFolderName(serviceName);

            // Assert: 64 characters of the name, a separator no safe name contains, and 16 hexadecimal characters
            Assert.True(folder.Length <= ServiceLogPaths.MaxFolderNameLength);
            Assert.Equal(64 + 1 + 16, folder.Length);
            Assert.StartsWith(new string('s', 64) + "~", folder);
            Assert.Matches("^[0-9A-F]{16}$", folder.Substring(65));
        }

        [Fact]
        public void GetFolderName_LongNamesWithTheSamePrefix_GetDifferentFolders()
        {
            // Arrange
            var prefix = new string('s', 150);

            // Act
            var first = ServiceLogPaths.GetFolderName(prefix + "-one");
            var second = ServiceLogPaths.GetFolderName(prefix + "-two");

            // Assert
            Assert.NotEqual(first, second);
        }

        [Fact]
        public void GetFolderName_LongNamesThatOnlyDifferInCase_ShareAFolder()
        {
            // Arrange: the Service Control Manager compares service names case-insensitively, and so does NTFS
            var name = new string('s', 150);

            // Act
            var lower = ServiceLogPaths.GetFolderName(name);
            var upper = ServiceLogPaths.GetFolderName(name.ToUpperInvariant());

            // Assert
            Assert.Equal(lower, upper, StringComparer.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(63)]
        [InlineData(62)]
        public void GetFolderName_ShorteningAtAnEscape_NeverCutsItInHalf(int plainPrefix)
        {
            // Arrange: an escape sequence that starts one or two characters before the cut
            var serviceName = new string('s', plainPrefix) + ":" + new string('t', 120);

            // Act
            var folder = ServiceLogPaths.GetFolderName(serviceName);

            // Assert: the prefix stops right before the '%', and the hash follows
            Assert.Equal(new string('s', plainPrefix) + "~", folder.Substring(0, plainPrefix + 1));
            Assert.DoesNotContain("%", folder);
        }

        [Fact]
        public void GetFolderName_ShorteningAfterACompleteEscape_KeepsIt()
        {
            // Arrange: the escape ends exactly at the cut
            var serviceName = new string('s', 61) + ":" + new string('t', 120);

            // Act
            var folder = ServiceLogPaths.GetFolderName(serviceName);

            // Assert
            Assert.StartsWith(new string('s', 61) + "%3A~", folder);
        }

        [Fact]
        public void GetFolderPath_IsTheServiceFolderUnderTheServiceLogsFolder()
        {
            // Act
            var path = ServiceLogPaths.GetFolderPath("My:Service");

            // Assert
            Assert.Equal(Path.Combine(AppConfig.ProgramDataPath, "logs", "services", "My%3AService"), path);
        }

        [Fact]
        public void GetRelativeFolderPath_IsRelativeToTheVault()
        {
            // Act
            var path = ServiceLogPaths.GetRelativeFolderPath("MyService");

            // Assert
            Assert.Equal(Path.Combine("logs", "services", "MyService"), path);
        }
    }
}
