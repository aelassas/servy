using Newtonsoft.Json;
using Servy.Core.Data;
using Servy.Core.DTOs;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Servy.DesignTime
{
    /// <summary>
    /// Design-time implementation of <see cref="IServiceRepository"/> 
    /// that provides stubbed data and methods for XAML designer usage.
    /// </summary>
    public class DesignTimeServiceRepository : IServiceRepository
    {
        /// <inheritdoc/>
        public Task<int> AddAsync(ServiceDto service) => Task.FromResult(1);


        /// <inheritdoc/>
        public Task<int> UpdateAsync(ServiceDto service) => Task.FromResult(1);

        /// <inheritdoc/>
        public Task<int> UpsertAsync(ServiceDto service) => Task.FromResult(1);

        /// <inheritdoc/>
        public Task<int> DeleteAsync(int id) => Task.FromResult(1);

        /// <inheritdoc/>
        public Task<int> DeleteAsync(string name) => Task.FromResult(1);

        /// <inheritdoc/>
        public Task<IEnumerable<ServiceDto>> GetAllAsync() =>
            Task.FromResult((IEnumerable<ServiceDto>)new List<ServiceDto>
            {
                new ServiceDto { Name = "Demo Service", Description = "Sample service for design-time" }
            });

        /// <inheritdoc/>
        public Task<ServiceDto> GetByIdAsync(int id) =>
            Task.FromResult<ServiceDto>(null);

        /// <inheritdoc/>
        public Task<ServiceDto> GetByNameAsync(string name) =>
            Task.FromResult<ServiceDto>(null);

        /// <inheritdoc/>
        public Task<IEnumerable<ServiceDto>> Search(string keyword) =>
            Task.FromResult((IEnumerable<ServiceDto>)new List<ServiceDto>());

        /// <inheritdoc/>
        public Task<string> ExportXML(string name)
        {
            // Create a dummy service for preview purposes
            var service = new ServiceDto
            {
                Name = name,
                Description = "Design-time XML export sample"
            };

            var serializer = new XmlSerializer(typeof(ServiceDto));
            using (var stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, service);
                return Task.FromResult(stringWriter.ToString());
            }
        }

        /// <inheritdoc/>
        public Task<bool> ImportXML(string xml)
        {
            // Simulate an import without actually storing anything
            try
            {
                var serializer = new XmlSerializer(typeof(ServiceDto));
                using (var stringReader = new StringReader(xml))
                {
                    var importedService = (ServiceDto)serializer.Deserialize(stringReader);
                    return Task.FromResult(true);
                }
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<string> ExportJSON(string name)
        {
            var service = new ServiceDto
            {
                Name = name,
                Description = "Design-time JSON export sample"
            };

            var json = JsonConvert.SerializeObject(service, Formatting.Indented,
                   new JsonSerializerSettings
                   {
                       NullValueHandling = NullValueHandling.Ignore
                   });

            return Task.FromResult(json);
        }

        /// <inheritdoc/>
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

    }
}
