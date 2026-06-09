using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.DTOs;
using System;
using System.IO;
using System.Reflection;
using System.Security;
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

        #region Constructor Tests

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenRepositoryIsNull()
        {
            // Assert & Act
            Assert.Throws<ArgumentNullException>(() => new ExportServiceCommand(null));
        }

        #endregion

        #region Execute Method Tests

        [Fact]
        public async Task Execute_ShouldFail_WhenServiceNameIsNullOrEmpty()
        {
            var opts = new ExportServiceOptions { ServiceName = "", ConfigFileType = "xml", Path = "file.xml" };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_ServiceNameRequired, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenConfigFileTypeIsInvalid()
        {
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "invalid", Path = "file.xml" };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_InvalidConfigFileType, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenPathIsNullOrEmpty()
        {
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = "" };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_PathRequired, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenServiceNotFound()
        {
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto)null);
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = Path.Combine(_tempDir, "out.xml") };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_ServiceNotFound, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldExportXml_WhenConfigTypeIsXml()
        {
            var filePath = Path.Combine(_tempDir, "out.xml");
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = "TestService" });
            _serviceRepoMock.Setup(r => r.ExportXmlAsync("svc", It.IsAny<CancellationToken>())).ReturnsAsync("<xml>data</xml>");

            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = filePath };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ExportSuccess, "XML", opts.Path), result.Message);
            Assert.True(File.Exists(filePath));
            Assert.Equal("<xml>data</xml>", File.ReadAllText(filePath));
        }

        [Fact]
        public async Task Execute_ShouldExportJson_WhenConfigTypeIsJson()
        {
            var filePath = Path.Combine(_tempDir, "out.json");
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = "TestService" });
            _serviceRepoMock.Setup(r => r.ExportJsonAsync("svc", It.IsAny<CancellationToken>())).ReturnsAsync("{\"name\":\"svc\"}");

            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "json", Path = filePath };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ExportSuccess, "JSON", opts.Path), result.Message);
            Assert.True(File.Exists(filePath));
            Assert.Equal("{\"name\":\"svc\"}", File.ReadAllText(filePath));
        }

        [Fact]
        public async Task Execute_ShouldHandleException()
        {
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("boom"));

            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = Path.Combine(_tempDir, "out.xml") };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Failed to export configuration for service 'svc'", result.Message);
        }

        #endregion

        #region SaveFile Validation Invariant Checks

        [Fact]
        public void SaveFile_ShouldCreateDirectoryIfNotExists()
        {
            var filePath = Path.Combine(_tempDir, "subdir", "file.xml");
            var content = "hello";

            InvokeSaveFile(filePath, content);

            Assert.True(File.Exists(filePath));
            Assert.Equal(content, File.ReadAllText(filePath));
        }

        [Fact]
        public void SaveFile_ShouldThrowSecurityException_WhenValidationFailsWithStandardError()
        {
            // Providing an invalid extension ("txt") routes to PathSecurityGuard's extension filter,
            // producing an error payload that does not contain "Access Denied" or "Security Alert".
            var filePath = Path.Combine(_tempDir, "denied_extension.txt");

            var ex = Assert.Throws<TargetInvocationException>(() => InvokeSaveFile(filePath, "data"));
            Assert.IsType<SecurityException>(ex.InnerException);
            Assert.Equal(string.Format(Core.Resources.Strings.Msg_SecurityInvalidFileType, ".txt"), ex.InnerException.Message);
        }

        [Fact]
        public void SaveFile_ShouldThrowSecurityException_WhenValidationResultTriggersSecurityAlert()
        {
            // Forcing a path sequence targeting a structural Windows system environment folder
            // triggers an internal "Access Denied" rule inside PathSecurityGuard, hitting the SecurityException branch.
            string protectedDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var filePath = Path.Combine(protectedDir, "malicious_export.json");

            var ex = Assert.Throws<TargetInvocationException>(() => InvokeSaveFile(filePath, "data"));
            Assert.IsType<SecurityException>(ex.InnerException);
            Assert.Contains("Access Denied", ex.InnerException.Message);
        }

        #endregion

        #region SaveFile I/O Error Catch Boundary Checks

        [Fact]
        public void SaveFile_ShouldThrowArgumentException_WhenFileStreamWriteFailsFromExternalLock()
        {
            var filePath = Path.Combine(_tempDir, "locked_out.json");
            File.WriteAllText(filePath, "original contents");

            // Exclusively open and lock down the target filesystem file channel before running SaveFile.
            // When SaveFile invokes PathSecurityGuard, it opens with FileMode.OpenOrCreate and FileAccess.ReadWrite.
            // Because our file handle context restricts any sharing options (FileShare.None), PathSecurityGuard
            // will throw an IOException/UnauthorizedAccessException while trying to allocate the stream.
            using (var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var ex = Assert.Throws<TargetInvocationException>(() => InvokeSaveFile(filePath, "new config payload"));

                // Unwraps target reflection errors to expose the inner thrown exception rule
                Assert.IsType<ArgumentException>(ex.InnerException);
                Assert.Contains(string.Format(Core.Resources.Strings.Msg_SecurityHandleValidationFailed, string.Empty), ex.InnerException.Message);
            }
        }

        #endregion

        #region Reflection Helper Definition

        /// <summary>
        /// Safely executes the private SaveFile system method using reflection layers.
        /// </summary>
        private void InvokeSaveFile(string path, string content)
        {
            var method = typeof(ExportServiceCommand).GetMethod(
                "SaveFile",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
            {
                throw new InvalidOperationException("Could not locate private method SaveFile inside ExportServiceCommand target reference metadata.");
            }

            method.Invoke(_command, new object[] { path, content });
        }

        #endregion
    }
}