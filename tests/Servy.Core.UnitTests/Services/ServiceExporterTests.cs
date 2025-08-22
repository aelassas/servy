using Servy.Core.DTOs;
using Servy.Core.Services;
using System.IO;
using Xunit;

namespace Servy.Core.UnitTests.Services
{
    public class ServiceExporterTests
    {
        private ServiceDto CreateSampleService()
        {
            return new ServiceDto
            {
                Id = 1,
                Name = "MyService",
                Description = "Test service",
                ExecutablePath = "C:\\service.exe",
                StartupDirectory = "C:\\",
                Parameters = "-arg1 -arg2",
                StartupType = 2,
                Priority = 1,
                StdoutPath = "stdout.log",
                StderrPath = "stderr.log",
                EnableRotation = true,
                RotationSize = 1024,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 10,
                MaxFailedChecks = 3,
                RecoveryAction = 1,
                MaxRestartAttempts = 5,
                EnvironmentVariables = "VAR1=VAL1;VAR2=VAL2",
                ServiceDependencies = "dep1;dep2",
                RunAsLocalSystem = true,
                UserAccount = "user",
                Password = "pass",
                PreLaunchExecutablePath = "pre.exe",
                PreLaunchStartupDirectory = "C:\\pre",
                PreLaunchParameters = "-preArg",
                PreLaunchEnvironmentVariables = "PREVAR=VAL",
                PreLaunchStdoutPath = "preout.log",
                PreLaunchStderrPath = "preerr.log",
                PreLaunchTimeoutSeconds = 30,
                PreLaunchRetryAttempts = 2,
                PreLaunchIgnoreFailure = true
            };
        }

        [Fact]
        public void ExportXml_ShouldReturnValidXmlString()
        {
            var service = CreateSampleService();
            var xml = ServiceExporter.ExportXml(service);

            Assert.False(string.IsNullOrWhiteSpace(xml));
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-16\"?>", xml);
            Assert.Contains("<Name>MyService</Name>", xml);
           
        }

        [Fact]
        public void ExportXml_File_ShouldWriteFile()
        {
            var service = CreateSampleService();
            var tempFile = Path.GetTempFileName();

            try
            {
                ServiceExporter.ExportXml(service, tempFile);
                var content = File.ReadAllText(tempFile);

                Assert.False(string.IsNullOrWhiteSpace(content));
                Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-16\"?>", content.Trim());
                Assert.Contains("<Name>MyService</Name>", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ExportJson_ShouldReturnValidJsonString()
        {
            var service = CreateSampleService();
            var json = ServiceExporter.ExportJson(service);

            Assert.False(string.IsNullOrWhiteSpace(json));
            Assert.Contains("\"Name\": \"MyService\"", json);
            Assert.Contains("\"ExecutablePath\": \"C:\\\\service.exe\"", json);
        }

        [Fact]
        public void ExportJson_File_ShouldWriteFile()
        {
            var service = CreateSampleService();
            var tempFile = Path.GetTempFileName();

            try
            {
                ServiceExporter.ExportJson(service, tempFile);
                var content = File.ReadAllText(tempFile);

                Assert.False(string.IsNullOrWhiteSpace(content));
                Assert.Contains("\"Name\": \"MyService\"", content);
                Assert.Contains("\"ExecutablePath\": \"C:\\\\service.exe\"", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ExportJson_ShouldIgnoreNullValues()
        {
            var service = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\service.exe"
                // other properties left null
            };

            var json = ServiceExporter.ExportJson(service);

            Assert.Contains("\"Name\": \"TestService\"", json);
            Assert.Contains("\"ExecutablePath\": \"C:\\\\service.exe\"", json);
            Assert.DoesNotContain("Description", json); // null properties should be ignored
        }
    }
}
