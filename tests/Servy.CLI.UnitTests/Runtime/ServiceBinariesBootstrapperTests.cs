using Moq;
using Servy.CLI.Runtime;
using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Security;
using Servy.Core.Services;
using System.Runtime.InteropServices;

namespace Servy.CLI.UnitTests.Runtime
{
    public class ServiceBinariesBootstrapperTests
    {
        /// <summary>
        /// Records every extraction request and answers from per-resource settings, so each branch of
        /// <see cref="ServiceBinariesBootstrapper.EnsureAsync"/> can be driven without writing to disk.
        /// </summary>
        private sealed class FakeExtractor : IEmbeddedResourceExtractor
        {
            public string BaseExtractionDirectory { get; set; } = @"C:\FakeVault\bin";

            /// <summary>The resources ("name.ext") <see cref="IsExtractionNeeded"/> reports as missing or stale.</summary>
            public HashSet<string> NeedingExtraction { get; } = new HashSet<string>();

            /// <summary>The resources ("name.ext") <see cref="CopyAsync"/> fails to extract.</summary>
            public HashSet<string> FailingCopies { get; } = new HashSet<string>();

            /// <summary>Every resource ("name.ext") <see cref="CopyAsync"/> was asked for, in order.</summary>
            public List<string> Copied { get; } = new List<string>();

            public bool IsExtractionNeeded(string fileName, string extension)
                => NeedingExtraction.Contains($"{fileName}.{extension}");

            public Task<bool> CopyAsync(string fileName, string extension, CancellationToken cancellationToken)
            {
                var resource = $"{fileName}.{extension}";
                Copied.Add(resource);
                return Task.FromResult(!FailingCopies.Contains(resource));
            }
        }

        private readonly FakeExtractor _extractor = new FakeExtractor();
        private readonly Mock<IServiceHelper> _serviceHelper = new Mock<IServiceHelper>();
        private readonly Mock<IServyHostInstaller> _hostInstaller = new Mock<IServyHostInstaller>();
        private readonly Mock<IServiceRepository> _repository = new Mock<IServiceRepository>();
        private readonly Mock<IServyExePermissionsHardener> _hardener = new Mock<IServyExePermissionsHardener>();

        public ServiceBinariesBootstrapperTests()
        {
            // Arrange (shared): a host that is installed and running Servy, no running Servy service, a host
            // install that succeeds, and a repository holding two services.
            _hostInstaller.Setup(h => h.GetRunningServyServices(It.IsAny<IServiceHelper>())).Returns(new List<string>());
            _hostInstaller.Setup(h => h.GetState()).Returns(ServyHostServiceState.ServyHost);
            _hostInstaller
                .Setup(h => h.EnsureInstalledAndRunningAsync(It.IsAny<string>(), It.IsAny<IServiceHelper>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());
            _repository
                .Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ServiceDto { Name = "Alpha" }, new ServiceDto { Name = "Beta" } });
        }

        private ServiceBinariesBootstrapper CreateBootstrapper(Architecture architecture = Architecture.X64)
            => new ServiceBinariesBootstrapper(_extractor, _serviceHelper.Object, _hostInstaller.Object, _repository.Object, _hardener.Object, architecture);

        [Fact]
        public async Task EnsureAsync_NothingNeedsExtraction_CopiesInOrderWithoutPausingTheServices()
        {
            // Arrange
            var bootstrapper = CreateBootstrapper();

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            // The wrapper, the restarter and the host come first, in that order, then Sysinternals Handle.
            Assert.Equal(
                new[]
                {
                    $"{AppConfig.ServyServiceCLIFileName}.exe",
                    $"{AppConfig.ServyRestarterFileName}.exe",
                    $"{AppConfig.ServyHostFileName}.exe",
                    $"{AppConfig.HandleExeX64FileName}.exe",
                },
                _extractor.Copied.Take(4));

            // Neither the wrapper nor the host is stale, so no running Servy service and no host is stopped.
            _hostInstaller.Verify(h => h.GetRunningServyServices(It.IsAny<IServiceHelper>()), Times.Never);
            _hostInstaller.Verify(h => h.StopAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task EnsureAsync_WrapperOrHostNeedsExtraction_StopsTheHostFirstAndStartsItAgain(bool wrapperStale, bool hostStale)
        {
            // Arrange
            // Either operand of the stale check must pause: replacing the wrapper or the host needs both stopped.
            if (wrapperStale) _extractor.NeedingExtraction.Add($"{AppConfig.ServyServiceCLIFileName}.exe");
            if (hostStale) _extractor.NeedingExtraction.Add($"{AppConfig.ServyHostFileName}.exe");
            var bootstrapper = CreateBootstrapper();

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            _hostInstaller.Verify(h => h.GetRunningServyServices(It.IsAny<IServiceHelper>()), Times.Once);
            _hostInstaller.Verify(h => h.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
            _hostInstaller.Verify(h => h.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(nameof(AppConfig.ServyServiceCLIFileName))]
        [InlineData(nameof(AppConfig.ServyRestarterFileName))]
        [InlineData(nameof(AppConfig.ServyHostFileName))]
        public async Task EnsureAsync_RequiredBinaryFailsToExtract_ThrowsNamingItAndStillResumesTheServices(string requiredBinary)
        {
            // Arrange
            var (fileName, exeName) = requiredBinary switch
            {
                nameof(AppConfig.ServyServiceCLIFileName) => (AppConfig.ServyServiceCLIFileName, AppConfig.ServyServiceCLIExe),
                nameof(AppConfig.ServyRestarterFileName) => (AppConfig.ServyRestarterFileName, AppConfig.ServyRestarterExe),
                _ => (AppConfig.ServyHostFileName, AppConfig.ServyHostExe),
            };
            _extractor.FailingCopies.Add($"{fileName}.exe");

            // The host is stale, so the services are paused before the failing copy: the finally block must resume them.
            _extractor.NeedingExtraction.Add($"{AppConfig.ServyHostFileName}.exe");
            var bootstrapper = CreateBootstrapper();

            // Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrapper.EnsureAsync(CancellationToken.None));

            // Assert
            Assert.Contains($"Failed to extract embedded resource '{exeName}'", ex.Message);
            Assert.Equal($"{fileName}.exe", _extractor.Copied.Last());
            _hostInstaller.Verify(h => h.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
            _hostInstaller.Verify(h => h.EnsureInstalledAndRunningAsync(It.IsAny<string>(), It.IsAny<IServiceHelper>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(Architecture.Arm64, true)]
        [InlineData(Architecture.X64, false)]
        [InlineData(Architecture.X86, false)]
        public async Task EnsureAsync_SelectsTheHandleBuildOfTheOperatingSystemArchitecture(Architecture architecture, bool expectArm64)
        {
            // Arrange
            var bootstrapper = CreateBootstrapper(architecture);
            var expected = expectArm64 ? AppConfig.HandleExeARM64FileName : AppConfig.HandleExeX64FileName;
            var other = expectArm64 ? AppConfig.HandleExeX64FileName : AppConfig.HandleExeARM64FileName;

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            Assert.Contains($"{expected}.exe", _extractor.Copied);
            Assert.DoesNotContain($"{other}.exe", _extractor.Copied);
        }

        [Fact]
        public async Task EnsureAsync_OptionalBinariesFailToExtract_ContinuesToTheHostInstall()
        {
            // Arrange
            // Sysinternals Handle (and, in Debug builds, the three .pdb files) only degrade features when missing.
            _extractor.FailingCopies.Add($"{AppConfig.HandleExeX64FileName}.exe");
            _extractor.FailingCopies.Add($"{AppConfig.ServyServiceCLIFileName}.pdb");
            _extractor.FailingCopies.Add($"{AppConfig.ServyRestarterFileName}.pdb");
            _extractor.FailingCopies.Add($"{AppConfig.ServyHostFileName}.pdb");
            var bootstrapper = CreateBootstrapper();

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            _hostInstaller.Verify(h => h.EnsureInstalledAndRunningAsync(It.IsAny<string>(), It.IsAny<IServiceHelper>(), It.IsAny<CancellationToken>()), Times.Once);
        }

#if DEBUG
        [Fact]
        public async Task EnsureAsync_DebugBuild_ExtractsTheDebugSymbolsAndNeverHardens()
        {
            // Arrange
            _extractor.NeedingExtraction.Add($"{AppConfig.ServyServiceCLIFileName}.exe");
            var bootstrapper = CreateBootstrapper();

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            // Debug builds extract next to the executable, outside the vault, so there is nothing to harden.
            Assert.Equal(
                new[]
                {
                    $"{AppConfig.ServyServiceCLIFileName}.pdb",
                    $"{AppConfig.ServyRestarterFileName}.pdb",
                    $"{AppConfig.ServyHostFileName}.pdb",
                },
                _extractor.Copied.Skip(4));
            _hardener.Verify(h => h.HardenServiceAccountsAsync(It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
            _hardener.Verify(h => h.SecureVaultAsync(It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }
#else
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task EnsureAsync_ReleaseBuild_SecuresTheVaultOnEveryStart(bool extractionNeeded)
        {
            // Arrange
            if (extractionNeeded)
                _extractor.NeedingExtraction.Add($"{AppConfig.ServyServiceCLIFileName}.exe");
            var bootstrapper = CreateBootstrapper();

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            // Release builds copy no debug symbols, and purge the vault and re-grant the service accounts on every start,
            // whether or not a file was extracted (#7526).
            Assert.Equal(4, _extractor.Copied.Count);
            _hardener.Verify(h => h.SecureVaultAsync(_repository.Object, It.IsAny<CancellationToken>()), Times.Once);
            _hardener.Verify(h => h.HardenServiceAccountsAsync(It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }
#endif

        [Fact]
        public async Task EnsureAsync_HostInstalled_InstallsItFromTheExtractionFolderAndMakesEveryServiceDependOnIt()
        {
            // Arrange
            var bootstrapper = CreateBootstrapper();
            IEnumerable<string>? dependents = null;
            _hostInstaller
                .Setup(h => h.EnsureServicesDependOnHostAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<string>, CancellationToken>((names, _) => dependents = names.ToList())
                .ReturnsAsync(2);

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            _hostInstaller.Verify(h => h.EnsureInstalledAndRunningAsync(
                Path.Combine(_extractor.BaseExtractionDirectory, AppConfig.ServyHostExe), _serviceHelper.Object, It.IsAny<CancellationToken>()), Times.Once);
            _repository.Verify(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(new[] { "Alpha", "Beta" }, dependents);
        }

        [Fact]
        public async Task EnsureAsync_HostInstallFails_DoesNotTouchTheServiceDependencies()
        {
            // Arrange
            _hostInstaller
                .Setup(h => h.EnsureInstalledAndRunningAsync(It.IsAny<string>(), It.IsAny<IServiceHelper>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Failure("The host could not be installed."));
            var bootstrapper = CreateBootstrapper();

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            // A failed host install is only logged: the command still runs, and no dependency is rewritten.
            _repository.Verify(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            _hostInstaller.Verify(h => h.EnsureServicesDependOnHostAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void Constructor_NullDependency_Throws()
        {
            // Arrange
            var arch = Architecture.X64;

            // Act
            var nullExtractor = Record.Exception(() => new ServiceBinariesBootstrapper(null!, _serviceHelper.Object, _hostInstaller.Object, _repository.Object, _hardener.Object, arch));
            var nullHelper = Record.Exception(() => new ServiceBinariesBootstrapper(_extractor, null!, _hostInstaller.Object, _repository.Object, _hardener.Object, arch));
            var nullHost = Record.Exception(() => new ServiceBinariesBootstrapper(_extractor, _serviceHelper.Object, null!, _repository.Object, _hardener.Object, arch));
            var nullRepository = Record.Exception(() => new ServiceBinariesBootstrapper(_extractor, _serviceHelper.Object, _hostInstaller.Object, null!, _hardener.Object, arch));
            var nullHardener = Record.Exception(() => new ServiceBinariesBootstrapper(_extractor, _serviceHelper.Object, _hostInstaller.Object, _repository.Object, null!, arch));

            // Assert
            Assert.Equal("extractor", Assert.IsType<ArgumentNullException>(nullExtractor).ParamName);
            Assert.Equal("serviceHelper", Assert.IsType<ArgumentNullException>(nullHelper).ParamName);
            Assert.Equal("hostInstaller", Assert.IsType<ArgumentNullException>(nullHost).ParamName);
            Assert.Equal("serviceRepository", Assert.IsType<ArgumentNullException>(nullRepository).ParamName);
            Assert.Equal("hardener", Assert.IsType<ArgumentNullException>(nullHardener).ParamName);
        }
    }
}
