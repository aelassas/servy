using Newtonsoft.Json;
using Servy.Core.DTOs;
using System.IO;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides static methods to export <see cref="ServiceDto"/> instances to XML or JSON.
    /// </summary>
    public static class ServiceExporter
    {
        /// <summary>
        /// Serializes a <see cref="ServiceDto"/> instance to an XML string.
        /// </summary>
        /// <param name="service">The service DTO to serialize.</param>
        /// <returns>An XML-formatted string representing the service.</returns>
        public static string ExportXml(ServiceDto service)
        {
            var xml = new StringWriter();
            new System.Xml.Serialization.XmlSerializer(typeof(ServiceDto)).Serialize(xml, service);
            return xml.ToString();
        }

        /// <summary>
        /// Serializes a <see cref="ServiceDto"/> instance to XML and writes it to a file.
        /// </summary>
        /// <param name="service">The service DTO to serialize.</param>
        /// <param name="filePath">The full path to the file to write.</param>
        public static void ExportXml(ServiceDto service, string filePath)
        {
            File.WriteAllText(filePath, ExportXml(service));
        }

        /// <summary>
        /// Serializes a <see cref="ServiceDto"/> instance to a JSON string.
        /// </summary>
        /// <param name="service">The service DTO to serialize.</param>
        /// <returns>A JSON-formatted string representing the service.</returns>
        public static string ExportJson(ServiceDto service)
        {
            var json = JsonConvert.SerializeObject(service, Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
            return json.ToString();
        }

        /// <summary>
        /// Serializes a <see cref="ServiceDto"/> instance to JSON and writes it to a file.
        /// </summary>
        /// <param name="service">The service DTO to serialize.</param>
        /// <param name="filePath">The full path to the file to write.</param>
        public static void ExportJson(ServiceDto service, string filePath)
        {
            File.WriteAllText(filePath, ExportJson(service));
        }
    }
}
