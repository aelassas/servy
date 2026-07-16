using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Services;
using Servy.Core.UnitTests.Helpers;
using System;
using Xunit;

namespace Servy.Core.UnitTests.Services
{
    public class XmlServiceSerializerTests
    {
        private readonly XmlServiceSerializer _serializer = new XmlServiceSerializer();

        #region Deserialize Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Deserialize_NullOrWhitespace_ReturnsNull(string input)
        {
            // Arrange & Act
            var result = _serializer.Deserialize(input);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Deserialize_PartialXml_AppliesDefaults()
        {
            // Arrange: Minimal valid XML
            string xml = "<ServiceDto><Name>PartialXmlService</Name></ServiceDto>";

            // Act
            var result = _serializer.Deserialize(xml);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("PartialXmlService", result.Name);

            // Integration check: Verify hydration via ServiceDtoHelper.ApplyDefaults
            Assert.Equal(AppConfig.DefaultStartTimeout, result.StartTimeout);
            Assert.Equal(AppConfig.DefaultStopTimeout, result.StopTimeout);
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, result.RunAsLocalSystem);
        }

        [Fact]
        public void Deserialize_AllFields_MapsCorrectly()
        {
            // Arrange: Create a DTO with specific values for every single field
            var expected = ServiceDtoFactory.CreateFull("Xml");

            // Convert to XML string using the standard Serializer
            var xml = ServiceDtoXml.Serialize(expected);

            // Act
            var actual = _serializer.Deserialize(xml);

            // Assert
            Assert.NotNull(actual);

            // Validate all major categories
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.ExecutablePath, actual.ExecutablePath);
            Assert.Equal(expected.StartupType, actual.StartupType);
            Assert.Equal(expected.RotationSize, actual.RotationSize);
            Assert.Equal(expected.MaxFailedChecks, actual.MaxFailedChecks);
            Assert.Equal(expected.RecoveryAction, actual.RecoveryAction);
            Assert.Equal(expected.PreLaunchTimeoutSeconds, actual.PreLaunchTimeoutSeconds);
            Assert.Equal(expected.PreStopLogAsError, actual.PreStopLogAsError);
            Assert.Equal(expected.PostStopParameters, actual.PostStopParameters);
            Assert.Equal(expected.EnvironmentVariables, actual.EnvironmentVariables);
            Assert.Equal(expected.ServiceDependencies, actual.ServiceDependencies);

            // Check that the Password/Account (Sensitive data) handled by UntrustedDataSettings
            Assert.Null(actual.UserAccount);
            Assert.Null(actual.Password);
            Assert.True(actual.RunAsLocalSystem);
        }

        [Fact]
        public void Deserialize_MalformedXml_ReturnsNull()
        {
            // Arrange: Invalid XML structure
            string malformedXml = "<ServiceDto><Name>UnclosedTag";

            // Act & Assert
            Assert.Null(_serializer.Deserialize(malformedXml));
        }

        [Fact]
        public void Deserialize_EmptyRoot_ReturnsHydratedDto()
        {
            // Arrange: Valid XML structure but NO properties set
            string emptyXml = "<ServiceDto />";

            // Act
            var result = _serializer.Deserialize(emptyXml);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(AppConfig.DefaultStopTimeout, result.StopTimeout);
            Assert.Equal(AppConfig.DefaultRotationSizeMB, result.RotationSize);
            Assert.Equal((int)AppConfig.DefaultStartupType, result.StartupType);
        }

        #endregion

        #region Serialize Tests

        [Fact]
        public void Serialize_NullDto_ReturnsNull()
        {
            // Arrange & Act
            var result = _serializer.Serialize(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Serialize_ValidDto_ReturnsFormattedXmlWithCorrectPreamble()
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "XmlSerializationService",
                DisplayName = "Friendly Name",
                StartTimeout = 30
            };

            // Act
            var xmlResult = _serializer.Serialize(dto);

            // Assert
            Assert.NotNull(xmlResult);

            // Check that it contains indented properties and tags
            Assert.Contains(Environment.NewLine + "  <Name>", xmlResult); // newline + indentation before child elements
            Assert.Contains("<Name>XmlSerializationService</Name>", xmlResult);
            Assert.Contains("<StartTimeout>30</StartTimeout>", xmlResult);

            // Verify Utf8StringWriter integration: ensures encoding reflects lowercase 'utf-8' without BOM corruptions
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xmlResult);
        }

        [Fact]
        public void Serialize_InvalidDtoStateOrSerializationFailure_CatchesExceptionAndReturnsNull()
        {
            // Arrange
            // Passing an undeclared derived type through an XmlSerializer instantiated for the base type
            // natively forces an InvalidOperationException, exercising the internal try-catch fallback block.
            var invalidDto = new InvalidServiceDtoMock();

            // Act
            var result = _serializer.Serialize(invalidDto);

            // Assert
            Assert.Null(result);
        }

        #endregion
    }

    /// <summary>
    /// Derived class designed to simulate an unexpected serialization type.
    /// Serializing this runtime subtype through a standard base XmlSerializer(typeof(ServiceDto)) 
    /// throws an InvalidOperationException because it lacks explicit XmlInclude configuration declarations.
    /// </summary>
    public class InvalidServiceDtoMock : ServiceDto
    {
    }
}