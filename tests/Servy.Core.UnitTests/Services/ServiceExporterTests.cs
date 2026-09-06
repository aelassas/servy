using Newtonsoft.Json;
using Servy.Core.DTOs;
using Servy.Core.Services;
using Servy.Core.UnitTests.Helpers;
using System.Xml.Serialization;

namespace Servy.Core.UnitTests.Services
{
    public class ServiceExporterTests
    {
        [Fact]
        public void ExportXml_ShouldReturnValidXmlString()
        {
            // Arrange
            var service = ServiceDtoFactory.CreateSampleExport();
            PopulateIgnoredProperties(service);

            // Act
            var xml = ServiceExporter.ExportXml(service);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(xml));
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml);

            // Core Identity
            Assert.Contains("<Name>MyService</Name>", xml);

            // Priority & Startup
            Assert.Contains("<StartupType>2</StartupType>", xml);
            Assert.Contains("<Priority>1</Priority>", xml);

            // Logging & Size Rotation
            Assert.Contains("<EnableSizeRotation>true</EnableSizeRotation>", xml);
            Assert.Contains("<RotationSize>1024</RotationSize>", xml);

            // Health Monitoring
            Assert.Contains("<EnableHealthMonitoring>true</EnableHealthMonitoring>", xml);
            Assert.Contains("<HeartbeatInterval>10</HeartbeatInterval>", xml);

            // Environment & Dependencies
            Assert.Contains("<EnvironmentVariables>VAR1=VAL1;VAR2=VAL2</EnvironmentVariables>", xml);
            Assert.Contains("<ServiceDependencies>dep1;dep2</ServiceDependencies>", xml);

            // PreLaunch Block
            Assert.Contains("<PreLaunchExecutablePath>pre.exe</PreLaunchExecutablePath>", xml);
            Assert.Contains("<PreLaunchTimeoutSeconds>30</PreLaunchTimeoutSeconds>", xml);

            // Security Bounds Hardening: Ensure credential values are omitted
            Assert.DoesNotContain("pass", xml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("user", xml, StringComparison.OrdinalIgnoreCase);

            // Dynamic Attribute-Driven Security Check: Validate every [XmlIgnore] property is strictly omitted
            AssertXmlPropertiesAbsent(xml, GetXmlIgnoredPropertyNames());
        }

        [Fact]
        public void ExportXml_File_ShouldWriteFile()
        {
            // Arrange
            var service = ServiceDtoFactory.CreateSampleExport();
            PopulateIgnoredProperties(service);
            var tempFile = Path.GetTempFileName();

            // Act & Assert
            try
            {
                ServiceExporter.ExportXml(service, tempFile);

                // The XML declaration says utf-8, so the bytes on disk must not carry a BOM.
                // File.ReadAllText strips the preamble before the assertions below ever see it,
                // so the guarantee has to be asserted at the byte level.
                var bytes = File.ReadAllBytes(tempFile);

                Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                    "Exported XML must be UTF-8 without a BOM.");
                Assert.Equal((byte)'<', bytes[0]);

                var content = File.ReadAllText(tempFile);

                Assert.False(string.IsNullOrWhiteSpace(content));
                Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", content);

                // Assert structural validation subsets
                Assert.Contains("<Name>MyService</Name>", content);
                Assert.Contains("<StartupType>2</StartupType>", content);
                Assert.Contains("<RotationSize>1024</RotationSize>", content);
                Assert.Contains("<PreLaunchExecutablePath>pre.exe</PreLaunchExecutablePath>", content);

                // Security Bounds Hardening: Ensure file output on disk is strictly stripped of secrets
                Assert.DoesNotContain("pass", content, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("user", content, StringComparison.OrdinalIgnoreCase);

                // Dynamic Attribute-Driven Security Check: Validate every [XmlIgnore] property is strictly omitted
                AssertXmlPropertiesAbsent(content, GetXmlIgnoredPropertyNames());
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void ExportJson_ShouldReturnValidJsonString()
        {
            // Arrange
            var service = ServiceDtoFactory.CreateSampleExport();
            PopulateIgnoredProperties(service);

            // Act
            var json = ServiceExporter.ExportJson(service);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(json));

            // Core Identity
            Assert.Contains("\"Name\": \"MyService\"", json);
            Assert.Contains("\"ExecutablePath\": \"C:\\\\service.exe\"", json);

            // Priority & Startup
            Assert.Contains("\"StartupType\": 2", json);
            Assert.Contains("\"Priority\": 1", json);

            // Logging & Size Rotation
            Assert.Contains("\"EnableSizeRotation\": true", json);
            Assert.Contains("\"RotationSize\": 1024", json);

            // Health Monitoring
            Assert.Contains("\"EnableHealthMonitoring\": true", json);
            Assert.Contains("\"HeartbeatInterval\": 10", json);

            // Environment & Dependencies
            Assert.Contains("\"EnvironmentVariables\": \"VAR1=VAL1;VAR2=VAL2\"", json);
            Assert.Contains("\"ServiceDependencies\": \"dep1;dep2\"", json);

            // PreLaunch Block
            Assert.Contains("\"PreLaunchExecutablePath\": \"pre.exe\"", json);
            Assert.Contains("\"PreLaunchTimeoutSeconds\": 30", json);

            // Security Bounds Hardening: Ensure credential values are omitted
            Assert.DoesNotContain("pass", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("user", json, StringComparison.OrdinalIgnoreCase);

            // Dynamic Attribute-Driven Security Check: Validate every [JsonIgnore] property is strictly omitted
            AssertJsonPropertiesAbsent(json, GetJsonIgnoredPropertyNames());
        }

        [Fact]
        public void ExportJson_File_ShouldWriteFile()
        {
            // Arrange
            var service = ServiceDtoFactory.CreateSampleExport();
            PopulateIgnoredProperties(service);
            var tempFile = Path.GetTempFileName();

            // Act & Assert
            try
            {
                ServiceExporter.ExportJson(service, tempFile);

                // The JSON writer is created with UTF8Encoding(false), so the bytes on disk must not
                // carry a BOM. File.ReadAllText strips the preamble before the assertions below ever
                // see it, so the guarantee has to be asserted at the byte level.
                var bytes = File.ReadAllBytes(tempFile);

                Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                    "Exported JSON must be UTF-8 without a BOM.");
                Assert.Equal((byte)'{', bytes[0]);

                var content = File.ReadAllText(tempFile);

                Assert.False(string.IsNullOrWhiteSpace(content));

                // Assert structural validation subsets
                Assert.Contains("\"Name\": \"MyService\"", content);
                Assert.Contains("\"StartupType\": 2", content);
                Assert.Contains("\"RotationSize\": 1024", content);
                Assert.Contains("\"PreLaunchExecutablePath\": \"pre.exe\"", content);

                // Security Bounds Hardening: Ensure file output on disk is strictly stripped of secrets
                Assert.DoesNotContain("pass", content, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("user", content, StringComparison.OrdinalIgnoreCase);

                // Dynamic Attribute-Driven Security Check: Validate every [JsonIgnore] property is strictly omitted
                AssertJsonPropertiesAbsent(content, GetJsonIgnoredPropertyNames());
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void ExportJson_ShouldIgnoreNullValues()
        {
            // Arrange
            var service = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\service.exe"
                // All other optional reference and nullable value types are left null/empty
            };

            // Act
            var json = ServiceExporter.ExportJson(service);

            // Assert
            // 1. Verify populated properties are correctly rendered with explicit key-value structure
            Assert.Contains("\"Name\": \"TestService\"", json);
            Assert.Contains("\"ExecutablePath\": \"C:\\\\service.exe\"", json);

            // 2. Comprehensive validation: Ensure all properties matching dynamic conditional
            // serialization rules (ShouldSerialize*) and [JsonIgnore] attributes are completely omitted when null or unassigned.
            var alwaysPresent = new[] { "Name", "ExecutablePath" };

            var conditionalProps = typeof(ServiceDto).GetMethods()
                .Where(m => m.Name.StartsWith("ShouldSerialize", StringComparison.Ordinal) && m.ReturnType == typeof(bool))
                .Select(m => m.Name.Substring("ShouldSerialize".Length));

            var ignoredProps = GetJsonIgnoredPropertyNames();

            var keysToProveAbsent = conditionalProps
                .Concat(ignoredProps)
                .Distinct()
                .Except(alwaysPresent);

            AssertJsonPropertiesAbsent(json, keysToProveAbsent);
        }

        #region Helper Methods

        /// <summary>
        /// Populates internal, unmanaged, and sensitive properties on a <see cref="ServiceDto"/> instance with non-null test values.
        /// </summary>
        /// <remarks>
        /// Populating these properties ensures that export serialization tests verify explicit attribute-based exclusion
        /// (<see cref="Newtonsoft.Json.JsonIgnoreAttribute"/> and <see cref="System.Xml.Serialization.XmlIgnoreAttribute"/>)
        /// rather than null-value suppression.
        /// </remarks>
        /// <param name="service">The <see cref="ServiceDto"/> instance to populate.</param>
        private static void PopulateIgnoredProperties(ServiceDto service)
        {
            service.Id = 1;
            service.Pid = 1234;
            service.RunAsLocalSystem = true;
            service.UserAccount = "user";
            service.Password = "pass";
            service.PreviousStopTimeout = 10;
            service.ActiveStdoutPath = @"C:\logs\stdout.log";
            service.ActiveStderrPath = @"C:\logs\stderr.log";
        }

        /// <summary>
        /// Gets all property names on <see cref="ServiceDto"/> decorated with <see cref="XmlIgnoreAttribute"/>.
        /// </summary>
        private static IEnumerable<string> GetXmlIgnoredPropertyNames()
        {
            return typeof(ServiceDto).GetProperties()
                .Where(p => p.IsDefined(typeof(XmlIgnoreAttribute), inherit: true))
                .Select(p => p.Name);
        }

        /// <summary>
        /// Gets all property names on <see cref="ServiceDto"/> decorated with <see cref="JsonIgnoreAttribute"/>.
        /// </summary>
        private static IEnumerable<string> GetJsonIgnoredPropertyNames()
        {
            return typeof(ServiceDto).GetProperties()
                .Where(p => p.IsDefined(typeof(JsonIgnoreAttribute), inherit: true))
                .Select(p => p.Name);
        }

        /// <summary>
        /// Asserts that the specified XML element names are completely absent from the XML payload.
        /// </summary>
        private static void AssertXmlPropertiesAbsent(string xml, IEnumerable<string> propertyNames)
        {
            foreach (var propName in propertyNames)
            {
                Assert.DoesNotContain($"<{propName}>", xml);
            }
        }

        /// <summary>
        /// Asserts that the specified JSON property keys are completely absent from the JSON payload.
        /// </summary>
        private static void AssertJsonPropertiesAbsent(string? json, IEnumerable<string> propertyNames)
        {
            foreach (var propName in propertyNames)
            {
                // Quote the keys to prevent false positive substring matching against string field values
                Assert.DoesNotContain($"\"{propName}\"", json);
            }
        }

        #endregion

        #region Null-DTO Contract Fallback Tests

        [Fact]
        public void ExportXml_NullService_ReturnsNull()
        {
            // Arrange & Act
            var result = ServiceExporter.ExportXml((ServiceDto?)null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExportJson_NullService_ReturnsNull()
        {
            // Arrange & Act
            var result = ServiceExporter.ExportJson((ServiceDto?)null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExportXml_File_NullService_DoesNotWriteFile()
        {
            // Arrange
            var path = Path.Combine(Path.GetTempPath(), "ExportXml_Null_" + Path.GetRandomFileName());

            try
            {
                // Act
                ServiceExporter.ExportXml(null, path);

                // Assert
                // Verify the structural no-op contract: execution exits cleanly without touching disk storage.
                Assert.False(File.Exists(path), "ExportXml must not create a file placeholder when supplied a null DTO payload.");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void ExportJson_File_NullService_DoesNotWriteFile()
        {
            // Arrange
            var path = Path.Combine(Path.GetTempPath(), "ExportJson_Null_" + Path.GetRandomFileName());

            try
            {
                // Act
                ServiceExporter.ExportJson(null, path);

                // Assert
                // Verify the structural no-op contract: execution exits cleanly without touching disk storage.
                Assert.False(File.Exists(path), "ExportJson must not create a file placeholder when supplied a null DTO payload.");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        #endregion
    }
}
