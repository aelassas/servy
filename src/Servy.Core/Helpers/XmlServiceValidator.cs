using Servy.Core.DTOs;
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Validates XML input to ensure it can be deserialized into a <see cref="ServiceDto"/>
    /// and meets basic business rules before inserting into the database.
    /// </summary>
    public static class XmlServiceValidator
    {
        /// <summary>
        /// Validates that the given XML string represents a valid <see cref="ServiceDto"/>.
        /// </summary>
        /// <param name="xml">The XML string to validate.</param>
        /// <param name="errorMessage">If validation fails, contains the reason.</param>
        /// <returns><c>true</c> if the XML is valid; otherwise, <c>false</c>.</returns>
        public static bool TryValidate(string xml, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(xml))
            {
                errorMessage = "XML cannot be empty.";
                return false;
            }

            try
            {
                // Basic XML well-formedness check
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xml);
            }
            catch (XmlException ex)
            {
                errorMessage = $"Invalid XML format: {ex.Message}";
                return false;
            }

            // Try deserializing to ServiceDto
            ServiceDto dto;
            try
            {
                var serializer = new XmlSerializer(typeof(ServiceDto));
                using (var reader = new StringReader(xml))
                {
                    dto = serializer.Deserialize(reader) as ServiceDto;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"XML does not match ServiceDto format: {ex.Message}";
                return false;
            }

            if (dto == null)
            {
                errorMessage = "Failed to deserialize XML to ServiceDto.";
                return false;
            }

            // Basic required field checks
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errorMessage = "Service name is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(dto.ExecutablePath))
            {
                errorMessage = "Executable path is required.";
                return false;
            }

            // All checks passed
            return true;
        }
    }
}
