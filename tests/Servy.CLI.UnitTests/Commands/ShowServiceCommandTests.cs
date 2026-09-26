using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Services;
using System.ServiceProcess;
using CliStrings = Servy.CLI.Resources.Strings;

namespace Servy.CLI.UnitTests.Commands
{
    /// <summary>
    /// Tests for the <c>show</c> verb, which renders stored service configuration for a human reader.
    /// The class does not derive from <see cref="ServiceCommandTestsBase{TCommand, TOptions}"/> because
    /// that skeleton assumes a single <see cref="IServiceManager"/> constructor argument and a required
    /// service name, and this command takes a repository as well and treats the name as optional.
    /// </summary>
    [Collection(ElevationTestCollection.Name)]
    public class ShowServiceCommandTests : IDisposable
    {
        private const string ServiceName = "TestService";

        private readonly Mock<IServiceRepository> _repository;
        private readonly Mock<IServiceManager> _serviceManager;
        private readonly ShowServiceCommand _command;

        public ShowServiceCommandTests()
        {
            // The verb runs the elevation pre-flight, so take the test seam explicitly rather than
            // relying on an elevated host or on a sibling class having left the flag set.
            BaseCommand.BypassElevationCheck = true;

            _repository = new Mock<IServiceRepository>();
            _serviceManager = new Mock<IServiceManager>();
            _command = new ShowServiceCommand(_repository.Object, _serviceManager.Object);
        }

        public void Dispose()
        {
            BaseCommand.BypassElevationCheck = false;
        }

        #region Helpers

        /// <summary>Builds a DTO carrying only the fields the core block always renders.</summary>
        /// <param name="name">The service name.</param>
        /// <returns>A minimally populated DTO.</returns>
        private static ServiceDto MinimalDto(string name = ServiceName) => new ServiceDto
        {
            Name = name,
            DisplayName = "Test Service",
            Description = "A test service",
            ExecutablePath = @"C:\apps\test.exe",
            StartupDirectory = @"C:\apps",
            Parameters = "--flag",
            StartupType = (int)ServiceStartType.AutomaticDelayedStart,
            Priority = (int)ProcessPriority.Normal
        };

        /// <summary>Arranges the repository to return <paramref name="dto"/> for a by-name lookup.</summary>
        /// <param name="dto">The DTO to return, or <c>null</c> for a miss.</param>
        /// <param name="name">The service name the lookup is keyed on.</param>
        private void GivenService(ServiceDto? dto, string name = ServiceName)
        {
            _repository
                .Setup(r => r.GetByNameAsync(name, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);
        }

        /// <summary>Arranges the repository to return <paramref name="services"/> for the list query.</summary>
        /// <param name="services">The services the search returns.</param>
        private void GivenServices(params ServiceDto[] services)
        {
            _repository
                .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(services);
        }

        /// <summary>Arranges the SCM to report <paramref name="status"/> for every service.</summary>
        /// <param name="status">The status to report.</param>
        private void GivenStatus(ServiceControllerStatus status)
        {
            _serviceManager
                .Setup(sm => sm.GetServiceStatus(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(status);
        }

        /// <summary>Returns the rendered value of a labelled row, with its padding removed.</summary>
        /// <param name="report">The rendered report.</param>
        /// <param name="label">The row label to find.</param>
        /// <returns>The trimmed value, or <c>null</c> when the row is absent.</returns>
        private static string? RowValue(string? report, string label)
        {
            var line = (report ?? string.Empty)
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .FirstOrDefault(l => l.TrimStart().StartsWith(label + " ", StringComparison.Ordinal)
                                  || l.TrimStart().StartsWith(label + ":", StringComparison.Ordinal));

            if (line == null) return null;

            var idx = line.IndexOf(": ", StringComparison.Ordinal);
            return idx < 0 ? string.Empty : line.Substring(idx + 2);
        }

        #endregion

        #region Constructor

        [Fact]
        public void Constructor_NullRepository_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceRepository? nullRepository = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>("serviceRepository", () => new ShowServiceCommand(nullRepository!, _serviceManager.Object));
        }

        [Fact]
        public void Constructor_NullServiceManager_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceManager? nullManager = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>("serviceManager", () => new ShowServiceCommand(_repository.Object, nullManager!));
        }

        #endregion

        #region Single-service mode

        [Fact]
        public async Task ExecuteAsync_ServiceNotInDatabase_ReturnsServiceNotFound()
        {
            // Arrange
            GivenService(null);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(Core.Resources.Strings.Msg_ServiceNotFound, result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_ServiceFound_StartsWithNameThenStatus()
        {
            // Arrange
            GivenService(MinimalDto());
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            var lines = (result.Message ?? string.Empty).Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            Assert.True(result.IsSuccess);
            Assert.StartsWith(CliStrings.Msg_Show_Label_Name, lines[0]);
            Assert.StartsWith(CliStrings.Msg_Show_Label_Status, lines[1]);
            Assert.Equal(ServiceName, RowValue(result.Message, CliStrings.Msg_Show_Label_Name));
            Assert.Equal(nameof(ServiceControllerStatus.Running), RowValue(result.Message, CliStrings.Msg_Show_Label_Status));
        }

        [Fact]
        public async Task ExecuteAsync_ServiceFound_RendersPid()
        {
            // Arrange
            var dto = MinimalDto();
            dto.Pid = 4242;
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("4242", RowValue(result.Message, CliStrings.Msg_Show_Label_Pid));
        }

        [Fact]
        public async Task ExecuteAsync_ServiceFoundWithoutPid_RendersNotSetPlaceholder()
        {
            // Arrange
            var dto = MinimalDto();
            dto.Pid = null;
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Stopped);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(CliStrings.Msg_Show_ValueNotSet, RowValue(result.Message, CliStrings.Msg_Show_Label_Pid));
        }

        [Fact]
        public async Task ExecuteAsync_ServiceFound_RendersEnumMemberNames()
        {
            // Arrange
            var dto = MinimalDto();
            dto.RecoveryAction = (int)RecoveryAction.RestartService;
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            // ServiceStartType is backed by uint, so this also pins the underlying-type conversion.
            Assert.Equal(nameof(ServiceStartType.AutomaticDelayedStart), RowValue(result.Message, CliStrings.Msg_Show_Label_StartupType));
            Assert.Equal(nameof(ProcessPriority.Normal), RowValue(result.Message, CliStrings.Msg_Show_Label_Priority));
            Assert.Equal(nameof(RecoveryAction.RestartService), RowValue(result.Message, CliStrings.Msg_Show_Label_Recovery));
        }

        [Fact]
        public async Task ExecuteAsync_UndefinedEnumOrdinal_FallsBackToTheStoredNumber()
        {
            // Arrange
            var dto = MinimalDto();
            dto.Priority = 99;
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("99", RowValue(result.Message, CliStrings.Msg_Show_Label_Priority));
        }

        [Fact]
        public async Task ExecuteAsync_PopulatedCategory_RendersItsHeadingAndRows()
        {
            // Arrange
            var dto = MinimalDto();
            dto.StdoutPath = @"C:\logs\out.log";
            dto.EnableSizeRotation = true;
            dto.RotationSize = 10;
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(CliStrings.Msg_Show_Group_Logs, result.Message);
            Assert.Equal(@"C:\logs\out.log", RowValue(result.Message, CliStrings.Msg_Show_Label_Stdout));
            Assert.Equal(CliStrings.Msg_Show_Enabled, RowValue(result.Message, CliStrings.Msg_Show_Label_SizeRotation));
            Assert.Equal(string.Format(CliStrings.Msg_Show_Megabytes, 10), RowValue(result.Message, CliStrings.Msg_Show_Label_RotationSize));
        }

        [Fact]
        public async Task ExecuteAsync_UnconfiguredCategory_IsOmittedEntirely()
        {
            // Arrange
            GivenService(MinimalDto());
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            // Nothing pre-launch, post-stop or failure-program is configured, so those headings must
            // not appear at all rather than appear above a run of placeholders.
            Assert.DoesNotContain(CliStrings.Msg_Show_Group_PreLaunch, result.Message);
            Assert.DoesNotContain(CliStrings.Msg_Show_Group_PostStop, result.Message);
            Assert.DoesNotContain(CliStrings.Msg_Show_Group_FailureProgram, result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_ServiceFound_NeverRendersTheStoredPassword()
        {
            // Arrange
            var dto = MinimalDto();
            dto.UserAccount = @".\svcuser";
            dto.Password = "SuperSecret123!";
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            // The account is shown; the credential never is. This is the contract the service already
            // documents for debug logging - sensitive data is never shown by the CLI or the module.
            Assert.Equal(@".\svcuser", RowValue(result.Message, CliStrings.Msg_Show_Label_UserAccount));
            Assert.DoesNotContain("SuperSecret123!", result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_SingleService_ReadsTheRecordDecrypted()
        {
            // Arrange
            GivenService(MinimalDto());
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            // Eight of the nine columns encrypted at rest are rendered by the detail view, so reading
            // with decrypt:false printed the stored ciphertext. export reads the same way.
            _repository.Verify(r => r.GetByNameAsync(ServiceName, true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_EncryptedAtRestFields_AreRenderedAsPlaintext()
        {
            // Arrange
            // Every value below sits in a column listed by ServiceRepository.SensitiveFields, so each
            // one reaches the renderer as ciphertext unless the read decrypts.
            var dto = MinimalDto();
            dto.Parameters = "--config telegraf.conf";
            dto.EnvironmentVariables = "API_HOST=example.internal";
            dto.PreLaunchEnvironmentVariables = "PRELAUNCH_MODE=check";
            dto.PreLaunchParameters = "--warmup";
            dto.PostLaunchParameters = "--notify";
            dto.PreStopParameters = "--drain";
            dto.PostStopParameters = "--cleanup";
            dto.FailureProgramParameters = "--alert";
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("--config telegraf.conf", RowValue(result.Message, CliStrings.Msg_Show_Label_Parameters));
            Assert.Contains("API_HOST=example.internal", result.Message);
            Assert.Contains("PRELAUNCH_MODE=check", result.Message);
            Assert.Contains("--warmup", result.Message);
            Assert.Contains("--notify", result.Message);
            Assert.Contains("--drain", result.Message);
            Assert.Contains("--cleanup", result.Message);
            Assert.Contains("--alert", result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_ListMode_DoesNotPayForDecryption()
        {
            // Arrange
            GivenServices(MinimalDto("alpha"));
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions();

            // Act
            await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            // None of the six listed columns is encrypted at rest, so the list must not pay to decrypt.
            _repository.Verify(r => r.SearchAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()), Times.Once);
            _repository.Verify(r => r.SearchAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_StatusUnreadableForInstalledService_ReportsUnknown()
        {
            // Arrange
            GivenService(MinimalDto());
            _serviceManager.Setup(sm => sm.GetServiceStatus(ServiceName, It.IsAny<CancellationToken>())).Returns((ServiceControllerStatus?)null);
            _serviceManager.Setup(sm => sm.IsServiceInstalled(ServiceName, It.IsAny<CancellationToken>())).Returns(true);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(nameof(ServiceStatus.Unknown), RowValue(result.Message, CliStrings.Msg_Show_Label_Status));
        }

        [Fact]
        public async Task ExecuteAsync_ServiceInDatabaseButNotInScm_ReportsNotInstalled()
        {
            // Arrange
            GivenService(MinimalDto());
            _serviceManager.Setup(sm => sm.GetServiceStatus(ServiceName, It.IsAny<CancellationToken>())).Returns((ServiceControllerStatus?)null);
            _serviceManager.Setup(sm => sm.IsServiceInstalled(ServiceName, It.IsAny<CancellationToken>())).Returns(false);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(nameof(ServiceStatus.NotInstalled), RowValue(result.Message, CliStrings.Msg_Show_Label_Status));
        }

        [Fact]
        public async Task ExecuteAsync_CoreBlockOnly_AlignsEveryValueInOneColumn()
        {
            // Arrange
            GivenService(MinimalDto());
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            var separators = (result.Message ?? string.Empty)
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => l.IndexOf(": ", StringComparison.Ordinal) >= 0)
                .Select(l => l.IndexOf(": ", StringComparison.Ordinal))
                .Distinct()
                .ToList();
            Assert.Single(separators);
            Assert.Equal(16, separators[0]);
        }

        #endregion

        #region List mode

        [Fact]
        public async Task ExecuteAsync_NoName_ListsEveryServiceWithItsColumns()
        {
            // Arrange
            var first = MinimalDto("alpha");
            first.Pid = 11;
            var second = MinimalDto("beta");
            second.Pid = 22;
            GivenServices(first, second);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions();

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Contains(CliStrings.Msg_Show_Column_Name, result.Message);
            Assert.Contains(CliStrings.Msg_Show_Column_DisplayName, result.Message);
            Assert.Contains(CliStrings.Msg_Show_Column_Description, result.Message);
            Assert.Contains(CliStrings.Msg_Show_Column_StartupType, result.Message);
            Assert.Contains(CliStrings.Msg_Show_Column_Status, result.Message);
            Assert.Contains(CliStrings.Msg_Show_Column_Pid, result.Message);
            Assert.Contains("alpha", result.Message);
            Assert.Contains("beta", result.Message);
            Assert.Contains("11", result.Message);
            Assert.Contains("22", result.Message);
            Assert.Contains(string.Format(CliStrings.Msg_Show_ServiceCount, 2), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_NoName_OrdersServicesByNameIgnoringCase()
        {
            // Arrange
            GivenServices(MinimalDto("zulu"), MinimalDto("Alpha"), MinimalDto("mike"));
            GivenStatus(ServiceControllerStatus.Stopped);
            var opts = new ShowServiceOptions();

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            var body = result.Message ?? string.Empty;
            Assert.True(body.IndexOf("Alpha", StringComparison.Ordinal) < body.IndexOf("mike", StringComparison.Ordinal));
            Assert.True(body.IndexOf("mike", StringComparison.Ordinal) < body.IndexOf("zulu", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ExecuteAsync_NoServicesStored_ReturnsTheEmptyListMessage()
        {
            // Arrange
            GivenServices();
            var opts = new ShowServiceOptions();

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(CliStrings.Msg_Show_NoServices, result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_SearchKeyword_IsPassedToTheRepository()
        {
            // Arrange
            GivenServices(MinimalDto("alpha"));
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { SearchKeyword = "alph" };

            // Act
            await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            _repository.Verify(r => r.SearchAsync("alph", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NoSearchKeyword_QueriesWithTheEmptyKeyword()
        {
            // Arrange
            GivenServices(MinimalDto("alpha"));
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions();

            // Act
            await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            // An empty keyword is the repository's "everything" query, so list mode needs no second call.
            _repository.Verify(r => r.SearchAsync(string.Empty, false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NameCombinedWithSearch_IsRefused()
        {
            // Arrange
            var opts = new ShowServiceOptions { ServiceName = ServiceName, SearchKeyword = "alph" };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(CliStrings.Msg_Show_NameAndSearchNotAllowed, result.Message);
            _repository.Verify(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            _repository.Verify(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Error handling

        [Fact]
        public async Task ExecuteAsync_UnauthorizedAccessException_ReturnsAdminPrivilegesRequired()
        {
            // Arrange
            _repository
                .Setup(r => r.GetByNameAsync(ServiceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException());
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(string.Format(CliStrings.Msg_AdminPrivilegesRequired, "show"), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_OperationCanceled_ReturnsCancelledResult()
        {
            // Arrange
            _repository
                .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());
            var opts = new ShowServiceOptions();

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(string.Format(CliStrings.Msg_CommandCancelled, "show"), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_GenericException_ReportsTheActionAndTheSuggestion()
        {
            // Arrange
            _repository
                .Setup(r => r.GetByNameAsync(ServiceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database unreadable"));
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(string.Format(CliStrings.Msg_ShowServiceAction, ServiceName), result.Message);
            Assert.Contains(CliStrings.Msg_ShowServiceSuggestion, result.Message);
        }

        #endregion

        #region Verb metadata

        [Fact]
        public void ShowVerb_DeclaresAnOptionalNameAndAnOptionalSearch()
        {
            // Arrange
            var verb = typeof(ShowServiceOptions)
                .GetCustomAttributes(typeof(CommandLine.VerbAttribute), false)
                .FirstOrDefault() as CommandLine.VerbAttribute;

            // Act
            var options = typeof(ShowServiceOptions)
                .GetProperties()
                .Select(p => p.GetCustomAttributes(typeof(CommandLine.OptionAttribute), false).FirstOrDefault())
                .OfType<CommandLine.OptionAttribute>()
                .ToList();

            // Assert
            Assert.NotNull(verb);
            Assert.Equal("show", verb!.Name);
            Assert.Contains(options, o => o.LongName == "name" && o.ShortName == "n" && !o.Required);
            Assert.Contains(options, o => o.LongName == "search" && o.ShortName == "s" && !o.Required);
        }

        #endregion
    }
}
