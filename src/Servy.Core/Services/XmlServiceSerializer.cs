using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.IO;
using Servy.Core.Logging;
using Servy.Core.Security;
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Servy.Core.Services
{
    /// <inheritdoc />
    public class XmlServiceSerializer : IXmlServiceSerializer
    {
        /// <inheritdoc />
        public ServiceDto Deserialize(string xml)
        {
            // 1. Initial Guard
            if (string.IsNullOrWhiteSpace(xml))
                return null;

            try
            {
                var serializer = new XmlSerializer(typeof(ServiceDto));

                // 2. Security-First (XXE Protection)
                using (var stringReader = new StringReader(xml))
                using (var xmlReader = SecureXml.CreateReader(stringReader))
                {
                    // 3. Attempt Deserialization
                    var dto = serializer.Deserialize(xmlReader) as ServiceDto;

                    // 4. Apply Defaults only on success
                    if (dto != null)
                    {
                        ServiceDtoHelper.ApplyDefaultsAndResetIdentity(dto);
                    }

                    return dto;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is XmlException)
            {
                // LOGIC: XmlException contains line info, but InvalidOperationException (thrown by XmlSerializer)
                // often wraps the actual XmlException as an InnerException.
                var xmlEx = (ex as XmlException) ?? (ex.InnerException as XmlException);
                int lineNumber = xmlEx?.LineNumber ?? 0;
                int linePosition = xmlEx?.LinePosition ?? 0;
                var lineInfoMessage = (lineNumber > 0 && linePosition > 0) ? $" at line {lineNumber}, position {linePosition}" : string.Empty;

                Logger.Error($"XML Deserialization failed{lineInfoMessage}.", ex);

                // Fulfills the contract by returning null instead of crashing the UI
                return null;
            }
        }

        /// <inheritdoc/>
        public string Serialize(ServiceDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(ServiceDto));
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    Encoding = new UTF8Encoding(false), // UTF-8 without BOM
                };

                // Use the custom Utf8StringWriter to ensure the XML preamble declares 'utf-8' correctly.
                using (var stringWriter = new Utf8StringWriter())
                using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
                {
                    serializer.Serialize(xmlWriter, dto);
                    return stringWriter.ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"XML Serialization failed for service: {dto.Name}", ex);
                return null;
            }
        }

    }
}