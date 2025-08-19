using Newtonsoft.Json;
using Servy.Core.DTOs;
using Servy.Core.Helpers;

namespace Servy.Core.Services
{
    public static class ServiceExporter
    {
        public static string ExportXml(ServiceDto service)
        {
            var serializer = new XmlServiceSerializer();
            var xml = new StringWriter();
            new System.Xml.Serialization.XmlSerializer(typeof(ServiceDto)).Serialize(xml, service);
            return xml.ToString();
        }

        public static void ExportXml(ServiceDto service, string filePath)
        {
            File.WriteAllText(filePath, ExportXml(service));
        }

        public static string ExportJson(ServiceDto service)
        {
            var json = JsonConvert.SerializeObject(service, Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
            return json.ToString();
        }

        public static void ExportJson(ServiceDto service, string filePath)
        {
            File.WriteAllText(filePath, ExportJson(service));
        }
    }
}
