using Servy.Core.DTOs;
using System;
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
                .Where(p => p.GetCustomAttribute<XmlIgnoreAttribute>() == null)
                .Where(p =>
                {
                    var value = p.GetValue(dto);
                    if (value is null) return true;
                    if (value is string s) return s.Length == 0;
                    var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                    return t.IsValueType && value.Equals(Activator.CreateInstance(t));
                })
                .Select(p => p.Name)
                .ToList();

            Assert.True(unset.Count == 0, $"Unset properties on ServiceDto: {string.Join(", ", unset)}");
        }
    }
}
