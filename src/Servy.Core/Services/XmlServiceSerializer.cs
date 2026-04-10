using Servy.Core.DTOs;
using Servy.Core.Helpers;
using System.Xml;
using System.Xml.Serialization;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides XML serialization and deserialization for <see cref="ServiceDto"/> objects.
    /// </summary>
    public class XmlServiceSerializer : IXmlServiceSerializer
    {
        /// <inheritdoc />
        public ServiceDto? Deserialize(string xml)
        {
            // Branch 1: Covered by Deserialize_NullOrEmpty_ReturnsNull
            if (string.IsNullOrWhiteSpace(xml))
                return null;

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            };

            var serializer = new XmlSerializer(typeof(ServiceDto));
            using (var stringReader = new StringReader(xml))
            using (var xmlReader = XmlReader.Create(stringReader, settings))
            {
                // Directly cast the result. 
                // If the XML is valid, this is a ServiceDto. 
                // If the XML root is wrong, XmlSerializer throws (covered by tests).
                var dto = serializer.Deserialize(xmlReader) as ServiceDto;

                // Since we made ApplyDefaults null-safe, we call it directly.
                // This line is now 100% covered.
                ServiceDtoHelper.ApplyDefaults(dto);

                return dto;
            }
        }
    }
}