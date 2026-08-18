using Moq;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.Manager.Mappers;
using Servy.Manager.Models;
using UiAppConfig = Servy.Manager.Config.UiAppConfig;

namespace Servy.Manager.UnitTests.Mappers
{
    public class ServiceMapperTests
    {
        private readonly Mock<IServiceManager> _mockServiceManager;
        private readonly Mock<IProcessHelper> _mockProcessHelper;

        public ServiceMapperTests()
        {
            _mockServiceManager = new Mock<IServiceManager>();
            _mockProcessHelper = new Mock<IProcessHelper>();
        }

        #region ToModelAsync Tests

        [Fact]
        public async Task ToModelAsync_NullService_ReturnsNull()
        {
            // Act
            var result = await ServiceMapper.ToModelAsync(null, true, false, _mockProcessHelper.Object, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ToModelAsync_EmptyName_ReturnsNull()
        {
            // Arrange
            var domainService = new Core.Domain.Service(_mockServiceManager.Object) { Name = string.Empty };

            // Act
            var result = await ServiceMapper.ToModelAsync(domainService, true, false,
                _mockProcessHelper.Object, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ToModelAsync_CancelledToken_Throws()
        {
            // Arrange
            var domainService = new Core.Domain.Service(_mockServiceManager.Object) { Name = "Test" };
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act & Assert
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => ServiceMapper.ToModelAsync(domainService, true, false, _mockProcessHelper.Object, cts.Token));
            }
        }

        [Fact]
        public async Task ToModelAsync_CancelledToken_NullService_ReturnsNullWithoutThrowing()
        {
            // Arrange
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act
                var result = await ServiceMapper.ToModelAsync(null, true, false, _mockProcessHelper.Object, cts.Token);

                // Assert
                Assert.Null(result);
            }
        }

        [Fact]
        public async Task ToModelAsync_ValidService_MapsPropertiesCorrectly()
        {
            // Arrange
            var domainService = new Core.Domain.Service(_mockServiceManager.Object)
            {
                Name = "Test",
                Description = "High performance background daemon service.",
                Pid = 1234,
                RunAsLocalSystem = true,
                UserAccount = @"CONTOSO\svc-account",
                StdoutPath = @"C:\Logs\stdout.log",
                StderrPath = @"C:\Logs\stderr.log",
                ActiveStdoutPath = @"C:\Logs\active_stdout.log",
                ActiveStderrPath = @"C:\Logs\active_stderr.log"
            };

            // Act
            var result = await ServiceMapper.ToModelAsync(domainService, true, false, _mockProcessHelper.Object, cancellationToken: TestContext.Current.CancellationToken);

            // Assert: Pin down every mapped target field, including shallow mapping placeholder defaults
            Assert.NotNull(result);
            Assert.Equal("Test", result.Name);
            Assert.Equal("High performance background daemon service.", result.Description);
            Assert.Equal(1234, result.Pid);
            Assert.True(result.IsPidEnabled);
            Assert.True(result.IsDesktopAppAvailable);
            Assert.Equal(UiAppConfig.LocalSystem, result.LogOnAs);

            Assert.Equal(@"C:\Logs\stdout.log", result.StdoutPath);
            Assert.Equal(@"C:\Logs\stderr.log", result.StderrPath);
            Assert.Equal(@"C:\Logs\active_stdout.log", result.ActiveStdoutPath);
            Assert.Equal(@"C:\Logs\active_stderr.log", result.ActiveStderrPath);

            // Verify performance calculation metrics remain unassigned when calc flag is false
            Assert.Null(result.CpuUsage);
            Assert.Null(result.RamUsage);

            // Critical Contract Verification: Verify shallow mapping placeholder defaults are preserved
            // to shield the UI synchronization thread from bulk Service Control Manager block overhead.
            Assert.Null(result.StartupType);
            Assert.Equal(ServiceStatus.None, result.Status);
            Assert.False(result.IsInstalled);
        }

        [Fact]
        public async Task ToModelAsync_RunAsLocalSystem_MapsLocalSystemDisplayName()
        {
            // Arrange
            var domainService = new Core.Domain.Service(_mockServiceManager.Object)
            {
                Name = "Test",
                RunAsLocalSystem = true,
                UserAccount = @"CONTOSO\svc-account",
            };

            // Act
            var result = await ServiceMapper.ToModelAsync(domainService, true, false,
                _mockProcessHelper.Object, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(UiAppConfig.LocalSystem, result!.LogOnAs);
        }

        [Fact]
        public async Task ToModelAsync_NamedAccount_MapsUserAccountDisplayName()
        {
            // Arrange
            var domainService = new Core.Domain.Service(_mockServiceManager.Object)
            {
                Name = "Test",
                RunAsLocalSystem = false,
                UserAccount = @"CONTOSO\svc-account",
            };

            // Act
            var result = await ServiceMapper.ToModelAsync(domainService, true, false,
                _mockProcessHelper.Object, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(@"CONTOSO\svc-account", result!.LogOnAs);
        }

        [Fact]
        public async Task ToModelAsync_CalculatePerf_CallsHelper()
        {
            // Arrange
            var domainService = new Core.Domain.Service(_mockServiceManager.Object) { Name = "Test", Pid = 1234 };
            _mockProcessHelper.Setup(h => h.GetProcessTreeMetrics(1234))
                .Returns(new ProcessMetrics(10.0, 500));

            // Act
            var result = await ServiceMapper.ToModelAsync(domainService, true, true, _mockProcessHelper.Object, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(10.0, result!.CpuUsage);
            Assert.Equal(500, result.RamUsage);
            _mockProcessHelper.Verify(h => h.GetProcessTreeMetrics(1234), Times.Once);
        }

        [Fact]
        public async Task ToModelAsync_CalculatePerf_NoPid_SkipsHelper()
        {
            // Arrange
            var domainService = new Core.Domain.Service(_mockServiceManager.Object) { Name = "Test", Pid = null };

            // Act
            var result = await ServiceMapper.ToModelAsync(domainService, true, true, _mockProcessHelper.Object,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result!.CpuUsage);
            Assert.Null(result.RamUsage);
            _mockProcessHelper.Verify(h => h.GetProcessTreeMetrics(It.IsAny<int>()), Times.Never);
        }

        #endregion

        #region ToModel Tests

        [Fact]
        public void ToModel_NullItem_ReturnsNull()
        {
            // Act
            var result = ServiceMapper.ToModel(null);

            // Assert
            Assert.Null(result);
        }

        public static IEnumerable<object?[]> GetToModelSubtypeData()
        {
            yield return new object?[] { new ConsoleService { Name = "C", Pid = 1234, StdoutPath = "out.txt", StderrPath = "err.txt" }, "C", 1234, true, "out.txt", "err.txt" };
            yield return new object?[] { new DependencyService { Name = "D", Pid = 4321 }, "D", 4321, true, null, null };
            yield return new object?[] { new PerformanceService { Name = "P", Pid = null }, "P", null, false, null, null };
        }

        [Theory]
        [MemberData(nameof(GetToModelSubtypeData))]
        public void ToModel_Subtypes_MapsBaseFieldsAndConsolePathsPolymorphically(
            ServiceItemBase item,
            string expectedName,
            int? expectedPid,
            bool expectedIsPidEnabled,
            string? expectedStdoutPath,
            string? expectedStderrPath)
        {
            // Act
            var result = ServiceMapper.ToModel(item);

            // Assert: Common ServiceItemBase fields
            Assert.NotNull(result);
            Assert.Equal(expectedName, result.Name);
            Assert.Equal(expectedPid, result.Pid);
            Assert.Equal(expectedIsPidEnabled, result.IsPidEnabled);

            // Assert: Console-specific paths (populated only for ConsoleService, null for others)
            Assert.Equal(expectedStdoutPath, result.StdoutPath);
            Assert.Equal(expectedStderrPath, result.StderrPath);
        }

        #endregion

        #region GetLogOnAsDisplayName Tests

        public static IEnumerable<object[]> GetAliasData() =>
            ServiceAccounts.LocalSystemAliases.Select(a => new object[] { a, UiAppConfig.LocalSystem })
                .Concat(ServiceAccounts.LocalServiceAliases.Select(a => new object[] { a, UiAppConfig.LocalService }))
                .Concat(ServiceAccounts.NetworkServiceAliases.Select(a => new object[] { a, UiAppConfig.NetworkService }));

        [Theory]
        [MemberData(nameof(GetAliasData))]
        public void GetLogOnAsDisplayName_EveryAlias_ResolvesToItsDisplayName(string alias, string expected)
        {
            // Act
            var result = ServiceMapper.GetLogOnAsDisplayName(alias);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, "LocalSystem")]
        [InlineData("MyCustomUser", "MyCustomUser")]
        [InlineData(@"nt authority\localservice", "LocalService")]
        [InlineData(@"builtin\networkservice", "NetworkService")]
        public void GetLogOnAsDisplayName_ResolvesCorrectly(string? input, string expectedDisplayNameProp)
        {
            // Arrange
            string expected;
            switch (expectedDisplayNameProp)
            {
                case "LocalSystem":
                    expected = UiAppConfig.LocalSystem;
                    break;
                case "LocalService":
                    expected = UiAppConfig.LocalService;
                    break;
                case "NetworkService":
                    expected = UiAppConfig.NetworkService;
                    break;
                default:
                    expected = input!;
                    break;
            }

            // Act
            var result = ServiceMapper.GetLogOnAsDisplayName(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion
    }
}
