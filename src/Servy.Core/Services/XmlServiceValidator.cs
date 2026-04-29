using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Validators;
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Servy.Core.Services
{
    /// <summary>
    /// Validates XML input to ensure it can be deserialized into a <see cref="ServiceDto"/>
    /// and meets strict Windows SCM and security rules before database persistence.
    /// </summary>
    public class XmlServiceValidator : IXmlServiceValidator
    {
        private readonly IProcessHelper _processHelper;
        private readonly IServiceValidationRules _serviceValidationRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="XmlServiceValidator"/> class with the specified process helper.
        /// </summary>
        /// <param name="processHelper">Provides methods to validate executable paths and gather process metrics.</param>
        /// <param name="serviceValidationRules">Provides rules for validating service properties.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="processHelper"/> or <paramref name="serviceValidationRules"/> is null.</exception>
        public XmlServiceValidator(IProcessHelper processHelper, IServiceValidationRules serviceValidationRules)
        {
            _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
            _serviceValidationRules = serviceValidationRules ?? throw new ArgumentNullException(nameof(serviceValidationRules));
        }

        /// <inheritdoc/>
        public bool TryValidate(string xml, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(xml))
            {
                errorMessage = "XML cannot be empty.";
                return false;
            }

            // Prevent Memory Exhaustion / DoS
            if (xml.Length > AppConfig.MaxImportPayloadSizeChars)
            {
                errorMessage = $"XML payload exceeds the maximum allowed size of {AppConfig.MaxImportPayloadSizeChars} characters.";
                Logger.Warn("XML Import Blocked: Payload size limit exceeded.");
                return false;
            }

            // 1. Prevent XXE Attacks
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            ServiceDto dto;
            try
            {
                var serializer = new XmlSerializer(typeof(ServiceDto));
                using (var stringReader = new StringReader(xml))
                using (var xmlReader = XmlReader.Create(stringReader, settings))
                {
                    dto = serializer.Deserialize(xmlReader) as ServiceDto;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"XML structure error: {ex.Message}";
                return false;
            }

            if (dto == null)
            {
                errorMessage = "Failed to deserialize XML.";
                return false;
            }

            // 2. DEEP DOMAIN VALIDATION
            var validation = _serviceValidationRules.Validate(dto);
            if (validation.Errors.Any() || validation.Warnings.Any())
            {
                errorMessage = string.Join("\n", validation.Errors.Concat(validation.Warnings));

                // Sanitize the untrusted name before logging
                var sanitizedName = (dto.Name ?? "Unknown").Replace("\r", "").Replace("\n", "");

                Logger.Warn($"Import Blocked: Crafted or invalid XML for service '{sanitizedName}'. Reason: {errorMessage}");
                return false;
            }

            // 3. Path Validation
            if (!_processHelper.ValidatePath(dto.ExecutablePath))
            {
                errorMessage = "The executable path in the XML is invalid or inaccessible.";
                return false;
            }

            return true;
        }
    }
}