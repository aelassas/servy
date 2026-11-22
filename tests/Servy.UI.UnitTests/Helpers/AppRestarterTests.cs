using Servy.UI.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace Servy.UI.UnitTests.Helpers
{
    public class AppRestarterTests
    {
        private const string ExePath = @"C:\Program Files\Servy\Servy.exe";

        [Fact]
        public void BuildStartInfo_ArgumentsWithLanguageOption_DropsTheOptionAndQuotesTheRest()
        {
            // Act
            var psi = AppRestarter.BuildStartInfo(ExePath, new[] { "--lang", "en", "false", "My Service", "--force-sr" });

            // Assert
            Assert.Equal(ExePath, psi.FileName);
            Assert.Equal("\"false\" \"My Service\" \"--force-sr\"", psi.Arguments);
            Assert.True(psi.UseShellExecute);
        }

        [Fact]
        public void BuildStartInfo_OnlyTheLanguageOption_PassesNoArguments()
        {
            // Act
            var psi = AppRestarter.BuildStartInfo(ExePath, new[] { "--lang=zh-CN" });

            // Assert
            Assert.Equal(string.Empty, psi.Arguments);
        }

        [Fact]
        public void BuildStartInfo_NoArguments_PassesNoArguments()
        {
            // Act
            var psi = AppRestarter.BuildStartInfo(ExePath, new string[0]);

            // Assert
            Assert.Equal(string.Empty, psi.Arguments);
        }

        [Fact]
        public void BuildStartInfo_NullArguments_PassesNoArguments()
        {
            // Act
            var psi = AppRestarter.BuildStartInfo(ExePath, null);

            // Assert
            Assert.Equal(string.Empty, psi.Arguments);
        }

        [Fact]
        public void StartNewInstance_ExecutableKnown_StartsItOnceWithTheBuiltStartInfo()
        {
            // Arrange
            var started = new List<ProcessStartInfo>();

            // Act
            var result = AppRestarter.StartNewInstance(ExePath, new[] { "--lang", "zh-CN", "MyService" }, psi =>
            {
                started.Add(psi);
                return null;
            });

            // Assert
            Assert.True(result);
            var psiUsed = Assert.Single(started);
            Assert.Equal(ExePath, psiUsed.FileName);
            Assert.Equal("\"MyService\"", psiUsed.Arguments);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void StartNewInstance_ExecutablePathUnknown_StartsNothingAndReturnsFalse(string executablePath)
        {
            // Arrange
            var calls = 0;

            // Act
            var result = AppRestarter.StartNewInstance(executablePath, new[] { "MyService" }, psi =>
            {
                calls++;
                return null;
            });

            // Assert
            Assert.False(result);
            Assert.Equal(0, calls);
        }

        [Fact]
        public void StartNewInstance_StartThrows_ReturnsFalseInsteadOfThrowing()
        {
            // Act
            var result = AppRestarter.StartNewInstance(ExePath, null, psi => throw new InvalidOperationException("The executable could not be started."));

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void StartNewInstance_NullStart_ThrowsArgumentNullException()
        {
            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => AppRestarter.StartNewInstance(ExePath, null, null));

            // Assert
            Assert.Equal("start", ex.ParamName);
        }

        [Fact]
        public void Restart_NoRunningApplication_StartsNothing()
        {
            // Arrange
            var calls = 0;

            // Act
            AppRestarter.Restart(null, ExePath, new[] { "MyService" }, psi =>
            {
                calls++;
                return null;
            });

            // Assert
            Assert.Equal(0, calls);
        }

        [Fact]
        public void GetExecutablePath_CurrentProcess_ReturnsARootedPathOfAnExistingFile()
        {
            // Act
            var path = AppRestarter.GetExecutablePath();

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(System.IO.File.Exists(path), $"'{path}' does not exist.");
        }
    }
}
