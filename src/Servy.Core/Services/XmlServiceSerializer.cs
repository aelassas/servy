using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Servy.Core.DTOs;
using Servy.Core.IO;
using Servy.Core.Security;

namespace Servy.Core.Services
{
    /// <inheritdoc cref="IXmlServiceSerializer" />
    public class XmlServiceSerializer : ServiceDtoSerializer, IXmlServiceSerializer
    {
        /// <inheritdoc />
        protected override string FormatName => "XML";

        /// <inheritdoc />
        protected override ServiceDto? DeserializeCore(string content)
        {
            var serializer = new XmlSerializer(typeof(ServiceDto));

            // Security-First (XXE Protection)
            using (var stringReader = new StringReader(content))
            using (var xmlReader = SecureXml.CreateReader(stringReader))
            {
                // Attempt Deserialization
                return serializer.Deserialize(xmlReader) as ServiceDto;
            }
        }

        /// <inheritdoc />
        protected override string? SerializeCore(ServiceDto dto)
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

        /// <inheritdoc />
        protected override string FormatLineInfo(Exception ex)
        {
            // Drill down to locate explicit parsing line data if present.
            // XmlException contains line info directly, but InvalidOperationException (thrown by XmlSerializer)
            // often wraps the actual XmlException as an InnerException.
            var xmlEx = (ex as XmlException) ?? (ex.InnerException as XmlException);
            int lineNumber = xmlEx?.LineNumber ?? 0;
            int linePosition = xmlEx?.LinePosition ?? 0;

            if (lineNumber > 0 && linePosition > 0)
            {
                return $" at line {lineNumber}, position {linePosition}";
            }

            return string.Empty;
        }
    }
}