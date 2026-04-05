using Servy.Core.DTOs;
using System.Xml;
using System.Xml.Serialization;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides XML serialization and deserialization for <see cref="ServiceDto"/> objects.
    /// </summary>
    public class XmlServiceSerializer : IXmlServiceSerializer
    {
        /// <inheritdoc />
        public ServiceDto? Deserialize(string xml)
        {
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
                return serializer.Deserialize(xmlReader) as ServiceDto;
            }
        }
    }
}
