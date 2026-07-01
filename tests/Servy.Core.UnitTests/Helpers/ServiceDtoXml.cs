using Servy.Core.DTOs;
using System.Xml.Serialization;

namespace Servy.Core.UnitTests.Helpers
{
    /// <summary>
    /// Provides XML serialization utility methods specifically for <see cref="ServiceDto"/> object graphs 
    /// within testing contexts.
    /// </summary>
    public static class ServiceDtoXml
    {
        /// <summary>
        /// Serializes a given <see cref="ServiceDto"/> instance into its equivalent structured XML string representation.
        /// </summary>
        /// <param name="dto">The service data transfer object to serialize.</param>
        /// <returns>A string containing the serialized XML data representing the specified DTO.</returns>
        public static string Serialize(ServiceDto dto)
        {
            var serializer = new XmlSerializer(typeof(ServiceDto));
            using (var sw = new StringWriter())
            {
                serializer.Serialize(sw, dto);
                return sw.ToString();
            }
        }
    }
}