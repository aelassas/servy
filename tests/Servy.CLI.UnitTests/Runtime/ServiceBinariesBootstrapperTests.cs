using Moq;
using Servy.CLI.Runtime;
using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Security;
using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

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

            /// <summary>Gets or sets a value indicating whether <see cref="CopyAllAsync"/> fails.</summary>
            public bool CopyAllFails { get; set; }

            /// <summary>Every single resource ("name.ext") <see cref="CopyAsync"/> was asked for, and "*" for each <see cref="CopyAllAsync"/>, in order.</summary>
            public List<string> Copied { get; } = new List<string>();

            /// <summary>The resources ("name.ext") of the last <see cref="CopyAllAsync"/> call.</summary>
            public List<string> CopiedTogether { get; } = new List<string>();

            public bool IsExtractionNeeded(string fileName, string extension)
                => NeedingExtraction.Contains($"{fileName}.{extension}");

            public Task<bool> CopyAsync(string fileName, string extension, CancellationToken cancellationToken)
            {
                var resource = $"{fileName}.{extension}";
                Copied.Add(resource);
                return Task.FromResult(!FailingCopies.Contains(resource));
            }

            public Task<bool> CopyAllAsync(List<ResourceItem> resourceItems, CancellationToken cancellationToken)
            {
                Copied.Add("*");
                CopiedTogether.Clear();
                CopiedTogether.AddRange(resourceItems.Select(i => $"{i.FileNameWithoutExtension}.{i.Extension}"));
                return Task.FromResult(!CopyAllFails);
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

        private ServiceBinariesBootstrapper CreateBootstrapper()
            => new ServiceBinariesBootstrapper(_extractor, _serviceHelper.Object, _hostInstaller.Object, _repository.Object, _hardener.Object);

        [Fact]
        public async Task EnsureAsync_NothingNeedsExtraction_CopiesHandleThenTheSetThenTheHostWithoutPausing()
        {
            // Arrange
            var bootstrapper = CreateBootstrapper();

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            // Sysinternals Handle comes first, before the pause decision; the wrapper set and then the host follow.
            var singles = _extractor.Copied.Where(c => !c.EndsWith(".pdb")).ToList();
            Assert.Equal(new[] { $"{AppConfig.HandleExeFileName}.exe", "*", $"{AppConfig.ServyHostFileName}.exe" }, singles);
            Assert.Equal($"{AppConfig.ServyServiceCLIFileName}.exe", _extractor.CopiedTogether[0]);
            Assert.Equal($"{AppConfig.ServyRestarterFileName}.exe", _extractor.CopiedTogether[1]);

            // Nothing is stale, so no running Servy service and no host is stopped.
            _hostInstaller.Verify(h => h.GetRunningServyServices(It.IsAny<IServiceHelper>()), Times.Never);
            _hostInstaller.Verify(h => h.StopAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("wrapper")]
        [InlineData("restarter")]
        [InlineData("host")]
        public async Task EnsureAsync_ABinaryOfTheSetOrTheHostNeedsExtraction_StopsTheHostFirstAndStartsItAgain(string staleBinary)
        {
            // Arrange
            // Any item of the extracted set, or the host, being stale must pause: replacing one needs both stopped.
            string stale;
            switch (staleBinary)
            {
                case "wrapper": stale = $"{AppConfig.ServyServiceCLIFileName}.exe"; break;
                case "restarter": stale = $"{AppConfig.ServyRestarterFileName}.exe"; break;
                default: stale = $"{AppConfig.ServyHostFileName}.exe"; break;
            }
            _extractor.NeedingExtraction.Add(stale);
            var bootstrapper = CreateBootstrapper();

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            _hostInstaller.Verify(h => h.GetRunningServyServices(It.IsAny<IServiceHelper>()), Times.Once);
            _hostInstaller.Verify(h => h.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
            _hostInstaller.Verify(h => h.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EnsureAsync_TheSetFailsToExtract_ThrowsAndStillResumesTheServices()
        {
            // Arrange
            _extractor.CopyAllFails = true;

            // The host is stale, so the services are paused before the failing copy: the finally block must resume them.
            _extractor.NeedingExtraction.Add($"{AppConfig.ServyHostFileName}.exe");
            var bootstrapper = CreateBootstrapper();

            // Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrapper.EnsureAsync(CancellationToken.None));

            // Assert
            Assert.Contains("Failed to extract embedded resources.", ex.Message);
            Assert.Equal("*", _extractor.Copied.Last());
            _hostInstaller.Verify(h => h.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
            _hostInstaller.Verify(h => h.EnsureInstalledAndRunningAsync(It.IsAny<string>(), It.IsAny<IServiceHelper>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EnsureAsync_HostFailsToExtract_ThrowsNamingItAndStillResumesTheServices()
        {
            // Arrange
            _extractor.FailingCopies.Add($"{AppConfig.ServyHostFileName}.exe");
            _extractor.NeedingExtraction.Add($"{AppConfig.ServyHostFileName}.exe");
            var bootstrapper = CreateBootstrapper();

            // Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrapper.EnsureAsync(CancellationToken.None));

            // Assert
            Assert.Contains($"Failed to extract embedded resource '{AppConfig.ServyHostExe}'", ex.Message);
            _hostInstaller.Verify(h => h.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
            _hostInstaller.Verify(h => h.EnsureInstalledAndRunningAsync(It.IsAny<string>(), It.IsAny<IServiceHelper>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EnsureAsync_OptionalBinariesFailToExtract_ContinuesToTheHostInstall()
        {
            // Arrange
            // Sysinternals Handle (and, in Debug builds, the three .pdb files) only degrade features when missing.
            _extractor.FailingCopies.Add($"{AppConfig.HandleExeFileName}.exe");
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
        public async Task EnsureAsync_DebugBuild_ExtractsTheDebugSymbolsAndNoDllsAndNeverHardens()
        {
            // Arrange
            _extractor.NeedingExtraction.Add($"{AppConfig.ServyServiceCLIFileName}.exe");
            var bootstrapper = CreateBootstrapper();

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            // Debug builds extract next to the executable, outside the vault, so there is nothing to harden, and the
            // DLLs are already there.
            Assert.Equal(
                new[]
                {
                    $"{AppConfig.ServyServiceCLIFileName}.pdb",
                    $"{AppConfig.ServyRestarterFileName}.pdb",
                    $"{AppConfig.ServyHostFileName}.pdb",
                },
                _extractor.Copied.Where(c => c.EndsWith(".pdb")));
            Assert.Equal(2, _extractor.CopiedTogether.Count);
            _hardener.Verify(h => h.SecureVaultAsync(It.IsAny<IServiceRepository>(), It.IsAny<CancellationToken>()), Times.Never);
        }
#else
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task EnsureAsync_ReleaseBuild_ExtractsTheDllsAndSecuresTheVaultOnEveryStart(bool extractionNeeded)
        {
            // Arrange
            if (extractionNeeded)
                _extractor.NeedingExtraction.Add($"{AppConfig.ServyServiceCLIFileName}.exe");
            var bootstrapper = CreateBootstrapper();

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            // Release builds extract the 16 DLLs the wrapper loads with it, copy no debug symbols, and purge the vault
            // and re-grant the service accounts on every start, whether or not a file was extracted (#7526).
            Assert.Equal(18, _extractor.CopiedTogether.Count);
            Assert.Contains("Servy.Core.dll", _extractor.CopiedTogether);
            Assert.Contains("System.Data.SQLite.dll", _extractor.CopiedTogether);
            Assert.DoesNotContain(_extractor.Copied, c => c.EndsWith(".pdb"));
            _hardener.Verify(h => h.SecureVaultAsync(_repository.Object, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EnsureAsync_ReleaseBuild_AStaleDllPausesTheServices()
        {
            // Arrange
            // The wrapper loads the DLLs, so replacing one needs the services stopped like replacing the wrapper.
            _extractor.NeedingExtraction.Add("Servy.Core.dll");
            var bootstrapper = CreateBootstrapper();

            // Act
            await bootstrapper.EnsureAsync(CancellationToken.None);

            // Assert
            _hostInstaller.Verify(h => h.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
#endif

        [Fact]
        public async Task EnsureAsync_HostInstalled_InstallsItFromTheExtractionFolderAndMakesEveryServiceDependOnIt()
        {
            // Arrange
            var bootstrapper = CreateBootstrapper();
            List<string> dependents = null;
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
            var helper = _serviceHelper.Object;
            var host = _hostInstaller.Object;
            var repository = _repository.Object;
            var hardener = _hardener.Object;

            // Act
            var nullExtractor = Record.Exception(() => new ServiceBinariesBootstrapper(null, helper, host, repository, hardener));
            var nullHelper = Record.Exception(() => new ServiceBinariesBootstrapper(_extractor, null, host, repository, hardener));
            var nullHost = Record.Exception(() => new ServiceBinariesBootstrapper(_extractor, helper, null, repository, hardener));
            var nullRepository = Record.Exception(() => new ServiceBinariesBootstrapper(_extractor, helper, host, null, hardener));
            var nullHardener = Record.Exception(() => new ServiceBinariesBootstrapper(_extractor, helper, host, repository, null));

            // Assert
            Assert.Equal("extractor", Assert.IsType<ArgumentNullException>(nullExtractor).ParamName);
            Assert.Equal("serviceHelper", Assert.IsType<ArgumentNullException>(nullHelper).ParamName);
            Assert.Equal("hostInstaller", Assert.IsType<ArgumentNullException>(nullHost).ParamName);
            Assert.Equal("serviceRepository", Assert.IsType<ArgumentNullException>(nullRepository).ParamName);
            Assert.Equal("hardener", Assert.IsType<ArgumentNullException>(nullHardener).ParamName);
        }
    }
}
