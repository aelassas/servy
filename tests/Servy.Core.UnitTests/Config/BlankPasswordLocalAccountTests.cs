using Moq;
using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.Core.Validation;
using Servy.Testing;
using System;
using Xunit;

namespace Servy.Core.UnitTests.Config
{
    /// <summary>
    /// A local user account with a blank password counts as passwordless only when LimitBlankPasswordUse is 0
    /// and a blank-password logon for it succeeds. Every test that replaces the probe lives in this one class,
    /// so the process-wide seam is never swapped by two tests at once (see <see cref="BlankPasswordProbe"/>).
    /// </summary>
    public class BlankPasswordLocalAccountTests
    {
        private const string BlankAccount = @".\servy_blank_svc";
        private const string ExePath = @"C:\Windows\System32\notepad.exe";

        #region IsBlankPasswordLocalAccount

        [Fact]
        public void IsBlankPasswordLocalAccount_PolicyAllowsAndBlankLogonSucceeds_ReturnsTrue()
        {
            // Arrange
            using (var probe = new BlankPasswordProbe(0, BlankAccount))
            {
                // Act
                var result = ServiceAccounts.IsBlankPasswordLocalAccount(BlankAccount);

                // Assert
                Assert.True(result);
                Assert.True(probe.WasValidated(BlankAccount));
            }
        }

        [Fact]
        public void IsBlankPasswordLocalAccount_MachineNameForm_ReturnsTrue()
        {
            // Arrange
            var account = Environment.MachineName + @"\servy_blank_svc";
            using (new BlankPasswordProbe(0, account))
            {
                // Act
                var result = ServiceAccounts.IsBlankPasswordLocalAccount(account);

                // Assert
                Assert.True(result);
            }
        }

        [Fact]
        public void IsBlankPasswordLocalAccount_AccountWithSurroundingWhitespace_LogsOnWithTheTrimmedName()
        {
            // Arrange
            using (var probe = new BlankPasswordProbe(0, BlankAccount))
            {
                // Act
                var result = ServiceAccounts.IsBlankPasswordLocalAccount("  " + BlankAccount + "  ");

                // Assert
                Assert.True(result);
                Assert.True(probe.WasValidated(BlankAccount));
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(null)] // absent value: the Windows default, restricted
        public void IsBlankPasswordLocalAccount_PolicyRestrictsBlankPasswords_ReturnsFalseWithoutALogon(int? policy)
        {
            // Arrange
            using (var probe = new BlankPasswordProbe(policy, BlankAccount))
            {
                // Act
                var result = ServiceAccounts.IsBlankPasswordLocalAccount(BlankAccount);

                // Assert
                Assert.False(result);
                Assert.False(probe.WasValidated(BlankAccount));
            }
        }

        [Fact]
        public void IsBlankPasswordLocalAccount_BlankLogonFails_ReturnsFalse()
        {
            // Arrange: the policy allows blank passwords, but this account has a password
            using (var probe = new BlankPasswordProbe(0))
            {
                // Act
                var result = ServiceAccounts.IsBlankPasswordLocalAccount(BlankAccount);

                // Assert
                Assert.False(result);
                Assert.True(probe.WasValidated(BlankAccount));
            }
        }

        [Theory]
        [InlineData(@"DOMAIN\servy_blank_svc")]
        [InlineData("servy_blank_svc")]
        [InlineData(@".\servy_blank_gmsa$")]
        [InlineData(@"NT SERVICE\servy_blank_svc")]
        [InlineData(@".\LocalSystem")]
        [InlineData(@".\")]
        [InlineData("")]
        [InlineData(null)]
        public void IsBlankPasswordLocalAccount_NotALocalUserAccount_ReturnsFalseWithoutALogon(string account)
        {
            // Arrange
            using (var probe = new BlankPasswordProbe(0, account ?? string.Empty))
            {
                // Act
                var result = ServiceAccounts.IsBlankPasswordLocalAccount(account);

                // Assert
                Assert.False(result);
                Assert.False(probe.WasValidated(account ?? string.Empty));
            }
        }

        #endregion

        #region Import

        [Fact]
        public void ApplyDefaultsAndResetIdentity_BlankPasswordLocalAccountAllowed_KeepsTheAccount()
        {
            // Arrange
            var dto = new ServiceDto { Name = "BlankService", RunAsLocalSystem = true, UserAccount = BlankAccount, Password = "ignored" };
            using (new BlankPasswordProbe(0, BlankAccount))
            {
                // Act
                ServiceDtoHelper.ApplyDefaultsAndResetIdentity(dto);
            }

            // Assert
            Assert.False(dto.RunAsLocalSystem);
            Assert.Equal(BlankAccount, dto.UserAccount);
            Assert.Null(dto.Password);
        }

        [Theory]
        [InlineData(1, true)]   // the policy restricts blank passwords
        [InlineData(0, false)]  // the account has a password
        public void ApplyDefaultsAndResetIdentity_LocalAccountNotPasswordless_ResetsToLocalSystem(int policy, bool blankLogonSucceeds)
        {
            // Arrange
            var dto = new ServiceDto { Name = "BlankService", RunAsLocalSystem = false, UserAccount = BlankAccount };
            using (blankLogonSucceeds ? new BlankPasswordProbe(policy, BlankAccount) : new BlankPasswordProbe(policy))
            {
                // Act
                ServiceDtoHelper.ApplyDefaultsAndResetIdentity(dto);
            }

            // Assert
            Assert.True(dto.RunAsLocalSystem);
            Assert.Null(dto.UserAccount);
            Assert.Null(dto.Password);
        }

        [Theory]
        [InlineData("xml")]
        [InlineData("json")]
        public void Deserialize_BlankPasswordLocalAccountAllowed_KeepsTheAccount(string format)
        {
            // Arrange
            var content = Manifest(format, BlankAccount);
            ServiceDto result;

            // Act
            using (new BlankPasswordProbe(0, BlankAccount))
            {
                result = format == "xml"
                    ? new XmlServiceSerializer().Deserialize(content)
                    : new JsonServiceSerializer().Deserialize(content);
            }

            // Assert
            Assert.NotNull(result);
            Assert.False(result.RunAsLocalSystem);
            Assert.Equal(BlankAccount, result.UserAccount);
            Assert.Null(result.Password);
        }

        [Theory]
        [InlineData("xml", 0, true)]
        [InlineData("json", 0, true)]
        [InlineData("xml", 1, false)]
        [InlineData("json", 1, false)]
        public void TryValidate_LocalAccount_IsKeptOnlyWhenBlankPasswordsAreAllowed(string format, int policy, bool kept)
        {
            // Arrange
            var processHelper = new Mock<IProcessHelper>();
            processHelper.Setup(ph => ph.ValidatePath(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);
            var rules = new ServiceValidationRules(processHelper.Object);
            var content = Manifest(format, BlankAccount);
            bool valid;
            string error;
            ServiceDto parsed;

            // Act
            using (new BlankPasswordProbe(policy, BlankAccount))
            {
                valid = format == "xml"
                    ? new XmlServiceValidator(rules).TryValidate(content, out error, out parsed)
                    : new JsonServiceValidator(rules).TryValidate(content, out error, out parsed);
            }

            // Assert
            Assert.True(valid, error);
            Assert.NotNull(parsed);
            Assert.Equal(!kept, parsed.RunAsLocalSystem);
            Assert.Equal(kept ? BlankAccount : null, parsed.UserAccount);
            Assert.Null(parsed.Password);
        }

        #endregion

        #region Export

        [Theory]
        [InlineData("xml")]
        [InlineData("json")]
        public void Serialize_LocalAccountWithNoPassword_WritesTheAccountWithoutALogon(string format)
        {
            // Arrange: export never attempts a logon, so the probe must not be reached
            var dto = new ServiceDto { Name = "BlankService", ExecutablePath = ExePath, RunAsLocalSystem = false, UserAccount = BlankAccount, Password = string.Empty };
            using (var probe = new BlankPasswordProbe(1))
            {
                // Act
                var output = format == "xml"
                    ? new XmlServiceSerializer().Serialize(dto)
                    : new JsonServiceSerializer().Serialize(dto);

                // Assert
                Assert.NotNull(output);
                Assert.Contains("servy_blank_svc", output);
                Assert.DoesNotContain("Password", output);
                Assert.False(probe.WasValidated(BlankAccount));
            }
        }

        #endregion

        /// <summary>
        /// A minimal manifest naming <paramref name="account"/>, with a password the import must never read.
        /// </summary>
        private static string Manifest(string format, string account)
        {
            if (format == "xml")
            {
                return "<ServiceDto><Name>BlankService</Name><ExecutablePath>" + ExePath + "</ExecutablePath>" +
                       "<UserAccount>" + account + "</UserAccount><Password>ignored</Password></ServiceDto>";
            }

            return JsonConvert.SerializeObject(new { Name = "BlankService", ExecutablePath = ExePath, UserAccount = account, Password = "ignored" });
        }
    }
}
