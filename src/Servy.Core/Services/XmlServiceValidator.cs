using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.Helpers;
using System.Xml;
using System.Xml.Serialization;

namespace Servy.Core.Services
{
    /// <summary>
    /// Validates XML input to ensure it can be deserialized into a <see cref="ServiceDto"/>
    /// and meets strict Windows SCM and security rules before database persistence.
    /// </summary>
    public static class XmlServiceValidator
    {
        /// <summary>
        /// Validates that the given XML string represents a valid and safe <see cref="ServiceDto"/>.
        /// </summary>
        /// <param name="xml">The XML string to validate.</param>
        /// <param name="errorMessage">If validation fails, contains the specific security or logic error.</param>
        /// <returns><c>true</c> if the XML is well-formed and logically valid; otherwise, <c>false</c>.</returns>
        public static bool TryValidate(string xml, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(xml))
            {
                errorMessage = "XML cannot be empty.";
                return false;
            }

            // 1. Prevent XXE Attacks
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            ServiceDto? dto;
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

            // 2. DEEP DOMAIN VALIDATION (The Security Fix)
            // This applies the same rules as the CLI installer to ensure 
            // parity between manual and automated setup.
            var validation = ServiceValidator.ValidateDto(dto);
            if (!validation.IsValid)
            {
                errorMessage = validation.ErrorMessage;
                Logger.Warn($"Import Blocked: Crafted or invalid XML for service '{dto.Name}'. Reason: {errorMessage}");
                return false;
            }

            // 3. Path Validation
            if (!ProcessHelper.ValidatePath(dto.ExecutablePath))
            {
                errorMessage = "The executable path in the XML is invalid or inaccessible.";
                return false;
            }

            return true;
        }
    }
}