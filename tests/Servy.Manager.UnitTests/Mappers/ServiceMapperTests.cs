using Moq;
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
            // Arrange (Vacuous setup for static method target validation)

            // Act
            var result = await ServiceMapper.ToModelAsync(null, true, false, _mockProcessHelper.Object, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ToModelAsync_ValidService_MapsPropertiesCorrectly()
        {
            // Arrange
            var domainService = new Core.Domain.Service(_mockServiceManager.Object)
            {
                Name = "Test",
                Pid = 1234,
                RunAsLocalSystem = true
            };

            // Act
            var result = await ServiceMapper.ToModelAsync(domainService, true, false, _mockProcessHelper.Object, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test", result.Name);
            Assert.Equal(1234, result.Pid);
            Assert.Equal(UiAppConfig.LocalSystem, result.LogOnAs);
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
        }

        #endregion

        #region ToModel Tests

        [Fact]
        public void ToModel_NullItem_ReturnsNull()
        {
            // Arrange (Vacuous setup for static null validation)

            // Act
            var result = ServiceMapper.ToModel(null!);

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
        [InlineData(null, UiAppConfig.LocalSystem)]
        [InlineData("LocalSystem", UiAppConfig.LocalSystem)]
        [InlineData("NT AUTHORITY\\LOCALSERVICE", UiAppConfig.LocalService)]
        [InlineData("MyCustomUser", "MyCustomUser")]
        public void GetLogOnAsDisplayName_ResolvesCorrectly(string? input, string expected)
        {
            // Arrange: Ensure ServiceAccounts constants match these inputs for test accuracy

            // Act
            var result = ServiceMapper.GetLogOnAsDisplayName(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion
    }
}