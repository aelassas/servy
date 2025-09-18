using Servy.Core.DTOs;
using System.IO;
using System.Xml.Serialization;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides XML serialization and deserialization for <see cref="ServiceDto"/> objects.
    /// </summary>
    public class XmlServiceSerializer : IXmlServiceSerializer
    {
        /// <inheritdoc />
        public ServiceDto Deserialize(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                return null;

            var serializer = new XmlSerializer(typeof(ServiceDto));
            using (var reader = new StringReader(xml))
            {
                return serializer.Deserialize(reader) as ServiceDto;
            }
        }
    }
}
