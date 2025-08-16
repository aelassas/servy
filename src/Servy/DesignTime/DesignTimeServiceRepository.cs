#nullable enable

using Newtonsoft.Json;
using Servy.Core.Data;
using Servy.Core.Domain;
using Servy.Core.DTOs;
using Servy.Core.Services;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Servy.DesignTime
{
    /// <summary>
    /// Design-time repository for preview and testing, supports both DTO and Domain service methods.
    /// </summary>
    public class DesignTimeServiceRepository : IServiceRepository
    {
        // -------------------- DTO METHODS --------------------

        /// <inheritdoc />
        public Task<int> AddAsync(ServiceDto service) => Task.FromResult(1);

        /// <inheritdoc />
        public Task<int> UpdateAsync(ServiceDto service) => Task.FromResult(1);

        /// <inheritdoc />
        public Task<int> UpsertAsync(ServiceDto service) => Task.FromResult(1);

        /// <inheritdoc />
        public Task<int> DeleteAsync(int id) => Task.FromResult(1);

        /// <inheritdoc />
        public Task<int> DeleteAsync(string name) => Task.FromResult(1);

        /// <inheritdoc />
        public Task<IEnumerable<ServiceDto>> GetAllAsync() =>
            Task.FromResult((IEnumerable<ServiceDto>)new List<ServiceDto>
            {
                new ServiceDto { Name = "Demo Service", Description = "Sample service for design-time" }
            });

        /// <inheritdoc />
        public Task<ServiceDto?> GetByIdAsync(int id) => Task.FromResult<ServiceDto?>(null);

        /// <inheritdoc />
        public Task<ServiceDto?> GetByNameAsync(string name) => Task.FromResult<ServiceDto?>(null);

        /// <inheritdoc />
        public Task<IEnumerable<ServiceDto>> Search(string keyword) =>
            Task.FromResult((IEnumerable<ServiceDto>)new List<ServiceDto>());

        /// <inheritdoc />
        public Task<string> ExportXML(string name)
        {
            var service = new ServiceDto { Name = name, Description = "Design-time XML export sample" };
            var serializer = new XmlSerializer(typeof(ServiceDto));
            using (var stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, service);
                return Task.FromResult(stringWriter.ToString());
            }
        }

        /// <inheritdoc />
        public Task<bool> ImportXML(string xml)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(ServiceDto));
                using (var stringReader = new StringReader(xml))
                {
                    var importedService = (ServiceDto?)serializer.Deserialize(stringReader);
                    return Task.FromResult(importedService != null);
                }
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc />
        public Task<string> ExportJSON(string name)
        {
            var service = new ServiceDto { Name = name, Description = "Design-time JSON export sample" };
            var json = JsonConvert.SerializeObject(service, Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            return Task.FromResult(json);
        }

        /// <inheritdoc />
        public Task<bool> ImportJSON(string json)
        {
            try
            {
                var service = JsonConvert.DeserializeObject<ServiceDto>(json);
                return Task.FromResult(service != null);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        // -------------------- DOMAIN METHODS --------------------

        /// <inheritdoc />
        public Task<int> AddDomainServiceAsync(Service service) => Task.FromResult(1);

        /// <inheritdoc />
        public Task<int> UpdateDomainServiceAsync(Service service) => Task.FromResult(1);

        /// <inheritdoc />
        public Task<int> UpsertDomainServiceAsync(Service service) => Task.FromResult(1);

        /// <inheritdoc />
        public Task<int> DeleteDomainServiceAsync(int id) => Task.FromResult(1);

        /// <inheritdoc />
        public Task<int> DeleteDomainServiceAsync(string name) => Task.FromResult(1);

        /// <inheritdoc />
        public Task<Service?> GetDomainServiceByIdAsync(IServiceManager serviceManager, int id) =>
            Task.FromResult<Service?>(new Service(null!) { Name = "Demo Service" });

        /// <inheritdoc />
        public Task<Service?> GetDomainServiceByNameAsync(IServiceManager serviceManager, string name) =>
            Task.FromResult<Service?>(new Service(null!) { Name = name, Description = "Design-time domain service" });

        /// <inheritdoc />
        public Task<IEnumerable<Service>> GetAllDomainServicesAsync(IServiceManager serviceManager) =>
            Task.FromResult((IEnumerable<Service>)new List<Service>
            {
                new Service(null !) { Name = "Demo Service", Description = "Sample domain service" }
            });

        /// <inheritdoc />
        public Task<IEnumerable<Service>> SearchDomainServicesAsync(IServiceManager serviceManager, string keyword) =>
            Task.FromResult((IEnumerable<Service>)new List<Service>());

        /// <inheritdoc />
        public Task<string> ExportDomainServiceXMLAsync(string name)
        {
            var service = new Service(null!) { Name = name, Description = "Design-time domain XML export sample" };
            var serializer = new XmlSerializer(typeof(Service));
            using (var stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, service);
                return Task.FromResult(stringWriter.ToString());
            }
        }

        /// <inheritdoc />
        public Task<bool> ImportDomainServiceXMLAsync(string xml)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(Service));
                using (var stringReader = new StringReader(xml))
                {
                    var importedService = (Service?)serializer.Deserialize(stringReader);
                    return Task.FromResult(importedService != null);
                }
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc />
        public Task<string> ExportDomainServiceJSONAsync(string name)
        {
            var service = new Service(null!) { Name = name, Description = "Design-time domain JSON export sample" };
            var json = JsonConvert.SerializeObject(service, Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            return Task.FromResult(json);
        }

        /// <inheritdoc />
        public Task<bool> ImportDomainServiceJSONAsync(string json)
        {
            try
            {
                var service = JsonConvert.DeserializeObject<Service>(json);
                return Task.FromResult(service != null);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}
