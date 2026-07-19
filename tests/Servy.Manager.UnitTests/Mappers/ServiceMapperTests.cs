using Moq;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.Manager.Mappers;
using Servy.Manager.Models;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
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
            // Arrange (Vacuous setup for static method target validation)

            // Act
            var result = await ServiceMapper.ToModelAsync(null, true, false, _mockProcessHelper.Object, cancellationToken: CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ToModelAsync_ValidService_MapsPropertiesCorrectly()
        {
            // Arrange: Set up every single mapped property on the domain object to avoid hidden default pass-throughs
            var domainService = new Core.Domain.Service(_mockServiceManager.Object)
            {
                Name = "Test",
                Description = "High performance background daemon service.",
                Pid = 1234,
                RunAsLocalSystem = true,
                StdoutPath = @"C:\Logs\stdout.log",
                StderrPath = @"C:\Logs\stderr.log",
                ActiveStdoutPath = @"C:\Logs\active_stdout.log",
                ActiveStderrPath = @"C:\Logs\active_stderr.log"
            };

            // Act
            var result = await ServiceMapper.ToModelAsync(domainService, true, false, _mockProcessHelper.Object, cancellationToken: CancellationToken.None);

            // Assert: Pin down all 14 mapped target fields, including shallow mapping placeholder defaults
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
        public async Task ToModelAsync_CalculatePerf_CallsHelper()
        {
            // Arrange
            var domainService = new Core.Domain.Service(_mockServiceManager.Object) { Name = "Test", Pid = 1234 };
            _mockProcessHelper.Setup(h => h.GetProcessTreeMetrics(1234))
                .Returns(new ProcessMetrics(10.0, 500));

            // Act
            var result = await ServiceMapper.ToModelAsync(domainService, true, true, _mockProcessHelper.Object, cancellationToken: CancellationToken.None);

            // Assert
            Assert.Equal(10.0, result.CpuUsage);
            Assert.Equal(500, result.RamUsage);
        }

        #endregion

        #region ToModel Tests

        [Fact]
        public void ToModel_NullItem_ReturnsNull()
        {
            // Arrange (Vacuous setup for static null validation)

            // Act
            var result = ServiceMapper.ToModel(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ToModel_ConsoleService_MapsPaths()
        {
            // Arrange
            var consoleService = new ConsoleService { Name = "C", Pid = 1234, StdoutPath = "out.txt", StderrPath = "err.txt" };

            // Act
            var result = ServiceMapper.ToModel(consoleService);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("C", result.Name);
            Assert.Equal(1234, result.Pid);
            Assert.True(result.IsPidEnabled);
            Assert.Equal("out.txt", result.StdoutPath);
            Assert.Equal("err.txt", result.StderrPath);
        }

        #endregion

        #region GetLogOnAsDisplayName Tests

        [Theory]
        [InlineData(null, "LocalSystem")]
        [InlineData("LocalSystem", "LocalSystem")]
        [InlineData("NT AUTHORITY\\System", "LocalSystem")]
        [InlineData("NT AUTHORITY\\LocalService", "LocalService")]
        [InlineData("NT AUTHORITY\\NetworkService", "NetworkService")] // Issue #2565: Pin network service branch mapping alias
        [InlineData("MyCustomUser", "MyCustomUser")]
        public void GetLogOnAsDisplayName_ResolvesCorrectly(string input, string expectedDisplayNameProp)
        {
            // Arrange: Map literal token labels onto their respective static target property mappings
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
                    expected = input;
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