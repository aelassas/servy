using Servy.Core.DTOs;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class ServiceDtoFactoryTests
    {
        /// <summary>
        /// Ensures that CreateFull populates every writable, serialized property on ServiceDto.
        /// Prevents regressions where newly added properties remain null and are skipped during serialization round-trips.
        /// </summary>
        [Fact]
        public void CreateFull_PopulatesEverySerializedProperty()
        {
            var dto = ServiceDtoFactory.CreateFull();

            var unset = typeof(ServiceDto)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .Where(p => p.GetCustomAttribute<XmlIgnoreAttribute>() == null) // ignore Id/Pid/Active*/PreviousStopTimeout
                .Where(p => p.GetValue(dto) is null)
                .Select(p => p.Name)
                .ToList();

            Assert.True(unset.Count == 0,
                $"CreateFull left these serialized properties null, so no round-trip test covers them: {string.Join(", ", unset)}");
        }
    }
}
