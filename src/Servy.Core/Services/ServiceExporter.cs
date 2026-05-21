using Newtonsoft.Json;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.IO;
using Servy.Core.Logging;
using Servy.Core.Security;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides static methods to export <see cref="ServiceDto"/> instances to XML or JSON.
    /// </summary>
    public static class ServiceExporter
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(ServiceDto));

        /// <summary>
        /// Centralized XML settings to guarantee identical encoding (UTF-8 without BOM) 
        /// and formatting across both in-memory strings and file streams.
        /// </summary>
        private static readonly XmlWriterSettings StandardXmlSettings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(false), // UTF-8 without BOM
            CloseOutput = false // Explicitly prevent closing the underlying atomic stream or writer
        };

        /// <summary>
        /// Serializes a <see cref="ServiceDto"/> instance to an XML string.
        /// Uses a custom StringWriter to ensure the declaration specifies UTF-8 without BOM.
        /// </summary>
        /// <param name="service">The service DTO to serialize.</param>
        /// <returns>An XML-formatted string representing the service, or null if the service is null.</returns>
        public static string? ExportXml(ServiceDto? service)
        {
            if (service == null)
            {
                Logger.Warn("Attempted to export a null ServiceDto to an XML string. Operation aborted.");
                return null;
            }

            using (var stringWriter = new Utf8StringWriter())
            using (var xmlWriter = XmlWriter.Create(stringWriter, StandardXmlSettings))
            {
                Serializer.Serialize(xmlWriter, service);
                return stringWriter.ToString();
            }
        }

        /// <summary>
        /// Serializes a <see cref="ServiceDto"/> instance to XML and writes it directly to a file.
        /// This ensures the file encoding matches the XML declaration (UTF-8 without BOM)
        /// and guarantees an atomic write to prevent zero-byte files on interruption.
        /// </summary>
        /// <param name="service">The service DTO to serialize.</param>
        /// <param name="filePath">The full path to the file to write.</param>
        public static void ExportXml(ServiceDto? service, string filePath)
        {
            if (service == null)
            {
                Logger.Warn($"Attempted to export a null ServiceDto to XML at '{filePath}'. Operation aborted.");
                return;
            }

            Helper.WriteFileAtomic(filePath, stream =>
            {
                using (var writer = XmlWriter.Create(stream, StandardXmlSettings))
                {
                    Serializer.Serialize(writer, service);
                }
            });
        }

        /// <summary>
        /// Serializes a <see cref="ServiceDto"/> instance to a JSON string.
        /// </summary>
        /// <param name="service">The service DTO to serialize.</param>
        /// <returns>A JSON-formatted string representing the service, or null if the service is null.</returns>
        public static string? ExportJson(ServiceDto? service)
        {
            if (service == null)
            {
                Logger.Warn("Attempted to export a null ServiceDto to a JSON string. Operation aborted.");
                return null;
            }

            // ROBUSTNESS: Switched to centralized UntrustedDataSettings to resolve asymmetry with IJsonServiceSerializer.
            return JsonConvert.SerializeObject(service, Newtonsoft.Json.Formatting.Indented, JsonSecurity.UntrustedDataSettings);
        }

        /// <summary>
        /// Serializes a <see cref="ServiceDto"/> instance to JSON and writes it to a file.
        /// Guarantees an atomic write to prevent zero-byte or partial files on interruption.
        /// </summary>
        /// <param name="service">The service DTO to serialize.</param>
        /// <param name="filePath">The full path to the file to write.</param>
        public static void ExportJson(ServiceDto? service, string filePath)
        {
            if (service == null)
            {
                Logger.Warn($"Attempted to export a null ServiceDto to JSON at '{filePath}'. Operation aborted.");
                return;
            }

            Helper.WriteFileAtomic(filePath, stream =>
            {
                // We use leaveOpen: true so the using block doesn't prematurely close the stream,
                // allowing the outer WriteFileAtomic to properly flush before the atomic move.
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true))
                using (var jsonWriter = new JsonTextWriter(writer) { Formatting = Newtonsoft.Json.Formatting.Indented })
                {
                    var serializer = JsonSerializer.Create(JsonSecurity.UntrustedDataSettings);
                    serializer.Serialize(jsonWriter, service);
                }
            });
        }
    }
}