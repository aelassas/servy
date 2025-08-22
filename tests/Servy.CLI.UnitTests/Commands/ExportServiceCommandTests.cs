using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.Core.Data;
using Servy.Core.DTOs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    public class ExportServiceCommandTests : IDisposable
    {
        private readonly Mock<IServiceRepository> _serviceRepoMock;
        private readonly ExportServiceCommand _command;
        private readonly string _tempDir;

        public ExportServiceCommandTests()
        {
            _serviceRepoMock = new Mock<IServiceRepository>();
            _command = new ExportServiceCommand(_serviceRepoMock.Object);

            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenServiceNameIsNullOrEmpty()
        {
            var opts = new ExportServiceOptions { ServiceName = "", ConfigFileType = "xml", Path = "file.xml" };
            var result = await _command.Execute(opts);
            Assert.False(result.Success);
            Assert.Contains("Service name is required", result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenConfigFileTypeIsInvalid()
        {
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "invalid", Path = "file.xml" };
            var result = await _command.Execute(opts);
            Assert.False(result.Success);
            Assert.Contains("Configuration output file type is required", result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenPathIsNullOrEmpty()
        {
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = "" };
            var result = await _command.Execute(opts);
            Assert.False(result.Success);
            Assert.Contains("Output file path is required", result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenServiceNotFound()
        {
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto)null);
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = Path.Combine(_tempDir, "out.xml") };
            var result = await _command.Execute(opts);
            Assert.False(result.Success);
            Assert.Contains("Service not found", result.Message);
        }

        [Fact]
        public async Task Execute_ShouldExportXml_WhenConfigTypeIsXml()
        {
            var filePath = Path.Combine(_tempDir, "out.xml");
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = "TestService" });
            _serviceRepoMock.Setup(r => r.ExportXML("svc", It.IsAny<CancellationToken>())).ReturnsAsync("<xml>data</xml>");

            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = filePath };
            var result = await _command.Execute(opts);

            Assert.True(result.Success);
            Assert.Contains("saved successfully", result.Message);
            Assert.True(File.Exists(filePath));
            Assert.Equal("<xml>data</xml>", File.ReadAllText(filePath));
        }

        [Fact]
        public async Task Execute_ShouldExportJson_WhenConfigTypeIsJson()
        {
            var filePath = Path.Combine(_tempDir, "out.json");
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = "TestService" });
            _serviceRepoMock.Setup(r => r.ExportJSON("svc", It.IsAny<CancellationToken>())).ReturnsAsync("{\"name\":\"svc\"}");

            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "json", Path = filePath };
            var result = await _command.Execute(opts);

            Assert.True(result.Success);
            Assert.Contains("saved successfully", result.Message);
            Assert.True(File.Exists(filePath));
            Assert.Equal("{\"name\":\"svc\"}", File.ReadAllText(filePath));
        }

        [Fact]
        public async Task Execute_ShouldHandleException()
        {
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("boom"));

            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = Path.Combine(_tempDir, "out.xml") };
            var result = await _command.Execute(opts);

            Assert.False(result.Success);
            Assert.Contains("An unhandled error occured: boom", result.Message);
        }

        [Fact]
        public void SaveFile_ShouldCreateDirectoryIfNotExists()
        {
            var filePath = Path.Combine(_tempDir, "subdir", "file.txt");
            var content = "hello";

            // Use reflection to call private SaveFile
            var method = typeof(ExportServiceCommand).GetMethod("SaveFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(_command, new object[] { filePath, content });

            Assert.True(File.Exists(filePath));
            Assert.Equal(content, File.ReadAllText(filePath));
        }
    }
}
