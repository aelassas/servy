using Moq;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Resources;
using Servy.Core.Services;
using Servy.Core.UnitTests.Logging;
using Servy.Core.Validation;
using Servy.Testing;
using System;
using Xunit;

namespace Servy.Core.UnitTests.Services
{
    public class XmlServiceValidatorTests
    {
        private readonly XmlServiceValidator _validator;
        private readonly TestableXmlServiceValidator _testableValidator;
        private readonly Mock<IProcessHelper> _processHelperMock;

        // The product serializer, so every fixture below is shaped like a real export file
        private readonly XmlServiceSerializer _serializer = new XmlServiceSerializer();

        internal sealed class TestableXmlServiceValidator : XmlServiceValidator
        {
            public TestableXmlServiceValidator(IServiceValidationRules rules, long maxPayloadBytes)
                : base(rules)
            {
                MaxPayloadBytes = maxPayloadBytes;
            }

            protected override long MaxPayloadBytes { get; }
        }

        // A validator whose parse step raises a caller-supplied exception, so the detail-message
        // composition shared by both catch arms of ServiceDtoImportValidator can be asserted
        // without depending on the framework serializer's own wording.
        internal sealed class ThrowingXmlServiceValidator : XmlServiceValidator
        {
            private readonly Exception _toThrow;

            public ThrowingXmlServiceValidator(IServiceValidationRules rules, Exception toThrow)
                : base(rules)
            {
                _toThrow = toThrow;
            }

            protected override ServiceDto Parse(string content)
            {
                throw _toThrow;
            }
        }

        public XmlServiceValidatorTests()
        {
            _processHelperMock = new Mock<IProcessHelper>();
            var rules = new ServiceValidationRules(_processHelperMock.Object);
            _validator = new XmlServiceValidator(rules);
            _testableValidator = new TestableXmlServiceValidator(rules, 32);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullRules_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new XmlServiceValidator(null));
            Assert.Equal("serviceValidationRules", ex.ParamName);
        }

        #endregion

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void TryValidate_NullOrWhitespaceXml_ReturnsFalse(string xml)
        {
            // Arrange
            var expectedError = string.Format(Strings.Msg_ImportInputEmptyOrWhitespace, "XML");

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(expectedError, error);
        }

        [Fact]
        public void TryValidate_InvalidXml_ReturnsFalse()
        {
            // Arrange
            // Missing closing tag
            var invalidXml = "<ServiceDto><Name>Test</Name>";
            var expectedPrefix = string.Format(Strings.Msg_ImportInvalidStructure, "XML", string.Empty).TrimEnd('.', ':', ' ');

            // Act
            var result = _validator.TryValidate(invalidXml, out var error);

            // Assert
            Assert.False(result);
            Assert.StartsWith(expectedPrefix, error);
        }

        [Fact]
        public void TryValidate_XmlNotMatchingServiceDto_ReturnsFalse()
        {
            // Arrange
            // Valid XML, but doesn't map to ServiceDto properties
            var xml = "<NotServiceDto><Foo>bar</Foo></NotServiceDto>";
            var expectedPrefix = string.Format(Strings.Msg_ImportStructureError, "XML", string.Empty).TrimEnd('.', ':', ' ');

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.Contains(expectedPrefix, error);
        }

        [Fact]
        public void TryValidate_XmlWithUnknownElement_ReturnsFalse()
        {
            // Arrange
            var xml = "<ServiceDto><Name>Test</Name><UnknownElement>Value</UnknownElement></ServiceDto>";

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.NotNull(error);
            Assert.Contains(string.Format(Strings.Msg_UnknownXmlElement, "UnknownElement"), error);
        }

        [Fact]
        public void TryValidate_XmlWithUnknownAttribute_ReturnsFalse()
        {
            // Arrange
            var xml = "<ServiceDto UnknownAttribute=\"Value\"><Name>Test</Name></ServiceDto>";

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.NotNull(error);
            Assert.Contains(string.Format(Strings.Msg_UnknownXmlAttribute, "UnknownAttribute"), error);
        }

        [Fact]
        public void TryValidate_DeserializedDtoIsNull_ReturnsFalse()
        {
            // Arrange
            var xml = "<ServiceDto xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\" />";
            var expectedError = string.Format(Strings.Msg_ImportEmptyDefinition, "XML");

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(expectedError, error);
        }

        [Fact]
        public void TryValidate_DomainValidationFailure_ReturnsFalse()
        {
            // Arrange
            // Name length is a domain check via ServiceValidationRules.Validate (importMode: true)
            var dto = new ServiceDto
            {
                Name = new string('A', AppConfig.MaxServiceNameLength + 1),
                ExecutablePath = "C:\\path\\to\\exe"
            };
            var xml = _serializer.Serialize(dto);

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.Contains(string.Format(Strings.Msg_ServiceNameLengthReached, AppConfig.MaxServiceNameLength), error);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(AppConfig.MaxStartTimeout + 1)]
        public void TryValidate_InvalidTimeout_ReturnsFalse(int invalidTimeout)
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\path\\to\\exe",
                StartTimeout = invalidTimeout
            };
            var xml = _serializer.Serialize(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(string.Format(Strings.Msg_InvalidStartTimeout, AppConfig.MinStartTimeout, AppConfig.MaxStartTimeout), error);
        }

        [Fact]
        public void TryValidate_InvalidExecutablePath_ReturnsFalse()
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "MyService",
                ExecutablePath = "INVALID_PATH_CHAR_<>|"
            };
            var xml = _serializer.Serialize(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(false);

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(Strings.Msg_InvalidPath, error);
        }

        [Fact]
        public void TryValidate_ServiceNamedServy_IsRefusedAsReservedForTheHost()
        {
            // Arrange: an export from an earlier version could carry a service named after the host service
            var dto = new ServiceDto
            {
                Name = "Servy",
                ExecutablePath = "C:\\Windows\\System32\\notepad.exe",
                StopTimeout = 30
            };
            var xml = _serializer.Serialize(dto);
            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(string.Format(Strings.Msg_ServiceNameReservedForServyHost, "Servy"), error);
        }

        [Fact]
        public void TryValidate_ValidXml_ReturnsTrue()
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "MyService",
                ExecutablePath = "C:\\Windows\\System32\\notepad.exe",
                StopTimeout = 30
            };
            var xml = _serializer.Serialize(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var result = _validator.TryValidate(xml, out var error);

            // Assert
            Assert.True(result);
            Assert.Null(error);
        }

        [Fact]
        public void TryValidate_PayloadExceedsMaxConfigFileSize_ReturnsFalse()
        {
            // Arrange: 33 ASCII bytes > 32 byte limit
            var oversized = new string('x', 33);

            // Act
            var result = _testableValidator.TryValidate(oversized, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(string.Format(Strings.Msg_ImportPayloadTooLarge, "XML", AppConfig.MaxConfigFileSizeMB), error);
        }

        [Fact]
        public void TryValidate_MultibytePayloadOverByteLimitButUnderCharLimit_ReturnsFalse()
        {
            // Arrange: 17 'é' characters = 34 UTF-8 bytes > 32 byte limit (under 32 char limit)
            var oversized = new string('é', 17);

            // Act
            var result = _testableValidator.TryValidate(oversized, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(string.Format(Strings.Msg_ImportPayloadTooLarge, "XML", AppConfig.MaxConfigFileSizeMB), error);
        }

        [Fact]
        public void TryValidate_PayloadAtSizeLimit_IsAccepted()
        {
            // Arrange: a payload whose UTF-8 byte length is exactly the limit. The guard rejects
            // only strictly-larger payloads, so this one must still be accepted.
            var dto = new ServiceDto
            {
                Name = "MyService",
                ExecutablePath = "C:\\Windows\\System32\\notepad.exe",
                StopTimeout = 30
            };
            var xml = _serializer.Serialize(dto);
            Assert.NotNull(xml);

            var atLimitValidator = new TestableXmlServiceValidator(
                new ServiceValidationRules(_processHelperMock.Object),
                System.Text.Encoding.UTF8.GetByteCount(xml));

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var result = atLimitValidator.TryValidate(xml, out var error);

            // Assert
            Assert.True(result);
            Assert.Null(error);
        }

        [Fact]
        public void TryValidate_StructuralExceptionWithInnerException_ReportsInnerMessageWithWrapperInParentheses()
        {
            // Arrange: an InvalidOperationException wrapping an XmlException takes the structural arm.
            var thrown = new InvalidOperationException("wrapper-structural", new System.Xml.XmlException("inner-structural"));
            var validator = new ThrowingXmlServiceValidator(new ServiceValidationRules(_processHelperMock.Object), thrown);
            var expected = string.Format(Strings.Msg_ImportInvalidStructure, "XML", "inner-structural (wrapper-structural)");

            // Act
            var result = validator.TryValidate("<ServiceDto />", out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(expected, error);
        }

        [Fact]
        public void TryValidate_UnexpectedExceptionWithInnerException_ReportsInnerMessageWithWrapperInParentheses()
        {
            // Arrange: an inner exception that is not an XmlException falls through to the catch-all arm,
            // which composes its detail fragment the same way.
            var thrown = new InvalidOperationException("wrapper-unexpected", new FormatException("inner-unexpected"));
            var validator = new ThrowingXmlServiceValidator(new ServiceValidationRules(_processHelperMock.Object), thrown);
            var expected = string.Format(Strings.Msg_ImportStructureError, "XML", "inner-unexpected (wrapper-unexpected)");

            // Act
            var result = validator.TryValidate("<ServiceDto />", out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(expected, error);
        }

        #region TryValidate overload that hands the parsed definition back

        [Fact]
        public void TryValidate_ValidXml_HandsBackHydratedDtoWithIdentityReset()
        {
            // Arrange
            // RunAsLocalSystem/Password are [XmlIgnore] and a null UserAccount is not written, so the raw parse leaves them null;
            // HeartbeatInterval is absent from the manifest and must come from AppConfig.
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = "C:\\path\\to\\exe" };
            var xml = _serializer.Serialize(dto);
            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var result = _validator.TryValidate(xml, out var error, out var parsed);

            // Assert
            Assert.True(result);
            Assert.Null(error);
            Assert.NotNull(parsed);
            Assert.Equal("TestService", parsed.Name);
            // Defaults hydrated, exactly as ServiceDtoSerializer.Deserialize used to do for the caller
            Assert.Equal(AppConfig.DefaultHeartbeatInterval, parsed.HeartbeatInterval);
            // Global Identity Reset on Import
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, parsed.RunAsLocalSystem);
            Assert.Null(parsed.UserAccount);
            Assert.Null(parsed.Password);
        }

        [Fact]
        public void TryValidate_ValidXmlWithPasswordlessAccount_HandsBackTheAccount()
        {
            // Arrange
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = "C:\\path\\to\\exe", RunAsLocalSystem = false, UserAccount = @"NT SERVICE\TestService" };
            var content = _serializer.Serialize(dto);
            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var result = _validator.TryValidate(content, out var error, out var parsed);

            // Assert
            Assert.True(result);
            Assert.Null(error);
            Assert.NotNull(parsed);
            Assert.False(parsed.RunAsLocalSystem);
            Assert.Equal(@"NT SERVICE\TestService", parsed.UserAccount);
            Assert.Null(parsed.Password);
        }

        [Fact]
        public void TryValidate_ValidXmlWithAccountThatNeedsAPassword_HandsBackTheLocalSystemBaseline()
        {
            // Arrange: the account is written by hand, because an export never carries one that needs a password
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = "C:\\path\\to\\exe", RunAsLocalSystem = false, UserAccount = @"NT SERVICE\TestService" };
            var content = (_serializer.Serialize(dto) ?? string.Empty).Replace(@"NT SERVICE\", @"DOMAIN\");
            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var result = _validator.TryValidate(content, out var error, out var parsed);

            // Assert
            Assert.True(result);
            Assert.Null(error);
            Assert.NotNull(parsed);
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, parsed.RunAsLocalSystem);
            Assert.Null(parsed.UserAccount);
            Assert.Null(parsed.Password);
        }

        [Fact]
        public void TryValidate_ValidXml_BothOverloadsAgree()
        {
            // Arrange
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = "C:\\path\\to\\exe" };
            var xml = _serializer.Serialize(dto);
            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var twoArg = _validator.TryValidate(xml, out var twoArgError);
            var threeArg = _validator.TryValidate(xml, out var threeArgError, out _);

            // Assert
            // Hydration runs after validation, so adding it cannot change which payloads are accepted.
            Assert.Equal(twoArg, threeArg);
            Assert.Equal(twoArgError, threeArgError);
        }

        [Fact]
        public void TryValidate_InvalidXml_HandsBackNoDto()
        {
            // Arrange
            var invalidXml = "<ServiceDto><Name>Test</Name>";

            // Act
            var result = _validator.TryValidate(invalidXml, out var error, out var parsed);

            // Assert
            Assert.False(result);
            Assert.NotNull(error);
            Assert.Null(parsed);
        }

        [Fact]
        public void TryValidate_DomainValidationFailure_HandsBackNoDto()
        {
            // Arrange
            // The payload parses, so this is the arm where a definition exists but must not escape.
            var dto = new ServiceDto
            {
                Name = new string('A', AppConfig.MaxServiceNameLength + 1),
                ExecutablePath = "C:\\path\\to\\exe"
            };
            var xml = _serializer.Serialize(dto);

            // Act
            var result = _validator.TryValidate(xml, out var error, out var parsed);

            // Assert
            Assert.False(result);
            Assert.NotNull(error);
            Assert.Null(parsed);
        }

        #endregion
    }

    /// <summary>
    /// Covers the log entry each rejection arm of <see cref="ServiceDtoImportValidator{TException}"/> writes.
    /// In a class of its own, like <see cref="XmlServiceSerializerLogTests"/>, so that only these cases leave
    /// parallel execution for <see cref="LoggerCollection"/>.
    /// </summary>
    [Collection(LoggerCollection.Name)] // the rejection entries are asserted through the static Logger
    public class XmlServiceValidatorLogTests
    {
        /// <summary>Domain rules whose path check accepts every path, so only the arranged defect fails.</summary>
        private static ServiceValidationRules Rules()
        {
            var processHelper = new Mock<IProcessHelper>();
            processHelper.Setup(ph => ph.ValidatePath(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);
            return new ServiceValidationRules(processHelper.Object);
        }

        /// <summary>The product validator over the permissive rules.</summary>
        private static XmlServiceValidator Validator => new XmlServiceValidator(Rules());

        /// <summary>The empty-input arm logs a warning that names the format.</summary>
        [Fact]
        public void TryValidate_EmptyInput_LogsTheRejectionAsAWarning()
        {
            // Arrange & Act
            var (_, log) = LogCapture.Run(() => Validator.TryValidate("   ", out _));

            // Assert
            Assert.Contains("[WARN] | XML import blocked: input was empty or whitespace.", log);
        }

        /// <summary>The size-gate arm logs the user message at error level.</summary>
        [Fact]
        public void TryValidate_PayloadOverTheLimit_LogsTheUserMessageAsAnError()
        {
            // Arrange
            string error = null;

            // Act
            var (_, log) = LogCapture.Run(() => new XmlServiceValidatorTests.TestableXmlServiceValidator(Rules(), 32).TryValidate(new string('x', 33), out error));

            // Assert
            Assert.NotNull(error);
            Assert.Contains("[ERROR] | " + error, log);
        }

        /// <summary>The structural parse failure logs its own line and not the unexpected-exception line.</summary>
        [Fact]
        public void TryValidate_StructuralParseFailure_LogsTheMalformedStructureLineOnly()
        {
            // Arrange
            var thrown = new InvalidOperationException("wrapper-structural", new System.Xml.XmlException("inner-structural"));
            var validator = new XmlServiceValidatorTests.ThrowingXmlServiceValidator(Rules(), thrown);

            // Act
            var (_, log) = LogCapture.Run(() => validator.TryValidate("<ServiceDto />", out _));

            // Assert: the two parse-failure arms must stay distinguishable in the log (#4398)
            Assert.Contains("[ERROR] | XML import blocked: malformed document structure.", log);
            Assert.DoesNotContain("unexpected parser exception", log);
        }

        /// <summary>The unexpected parse failure logs the exception type and not the structural line.</summary>
        [Fact]
        public void TryValidate_UnexpectedParseFailure_LogsTheExceptionTypeOnly()
        {
            // Arrange
            var thrown = new InvalidOperationException("wrapper-unexpected", new FormatException("inner-unexpected"));
            var validator = new XmlServiceValidatorTests.ThrowingXmlServiceValidator(Rules(), thrown);

            // Act
            var (_, log) = LogCapture.Run(() => validator.TryValidate("<ServiceDto />", out _));

            // Assert
            Assert.Contains("[ERROR] | XML import blocked: unexpected parser exception (InvalidOperationException).", log);
            Assert.DoesNotContain("malformed document structure", log);
        }

        /// <summary>The null-definition arm logs a warning.</summary>
        [Fact]
        public void TryValidate_DeserializedDtoIsNull_LogsTheRejectionAsAWarning()
        {
            // Arrange
            var xml = "<ServiceDto xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\" />";

            // Act
            var (_, log) = LogCapture.Run(() => Validator.TryValidate(xml, out _));

            // Assert
            Assert.Contains("[WARN] | XML import blocked: parser returned no service definition.", log);
        }

        /// <summary>The domain-failure arm logs the service name and the reason.</summary>
        [Fact]
        public void TryValidate_DomainValidationFailure_LogsTheServiceNameAndTheReason()
        {
            // Arrange: an out-of-range timeout fails the domain rules of an otherwise valid definition
            var dto = new ServiceDto { Name = "MyService", ExecutablePath = "C:\\path\\to\\exe", StartTimeout = AppConfig.MaxStartTimeout + 1 };
            var xml = new XmlServiceSerializer().Serialize(dto);
            var expectedReason = string.Format(Strings.Msg_InvalidStartTimeout, AppConfig.MinStartTimeout, AppConfig.MaxStartTimeout);

            // Act
            var (_, log) = LogCapture.Run(() => Validator.TryValidate(xml, out _));

            // Assert
            Assert.Contains("[WARN] | XML import blocked: logical violation for service 'MyService'. Reason: ", log);
            Assert.Contains(expectedReason, log);
        }

        /// <summary>A definition whose name is null is still attributed in the log, as <c>Unknown</c>.</summary>
        /// <remarks>
        /// The XML serializer never yields a null name (<see cref="ServiceDto.Name"/> defaults to the empty string),
        /// so the definition is handed over by a double whose parse step returns it directly.
        /// </remarks>
        [Fact]
        public void TryValidate_DomainValidationFailureWithoutName_LogsUnknownAsTheServiceName()
        {
            // Arrange
            var validator = new NullNameXmlServiceValidator(Rules());

            // Act
            var (_, log) = LogCapture.Run(() => validator.TryValidate("<ServiceDto />", out _));

            // Assert
            Assert.Contains("[WARN] | XML import blocked: logical violation for service 'Unknown'. Reason: ", log);
            Assert.Contains(Strings.Msg_ServiceNameRequired, log);
        }

        /// <summary>A validator whose parse step returns a definition with a null name.</summary>
        private sealed class NullNameXmlServiceValidator : XmlServiceValidator
        {
            /// <summary>Creates the double over the given domain rules.</summary>
            /// <param name="rules">The domain rules the base validator applies after the parse step.</param>
            public NullNameXmlServiceValidator(IServiceValidationRules rules)
                : base(rules)
            {
            }

            /// <summary>Ignores the input and returns a definition whose name is null.</summary>
            /// <param name="content">The raw input; not read.</param>
            /// <returns>A service definition with a null name and every other field at its default.</returns>
            protected override ServiceDto Parse(string content)
            {
                return new ServiceDto { Name = null };
            }
        }
    }
}
