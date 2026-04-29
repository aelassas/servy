using Servy.Core.DTOs;

namespace Servy.Core.Services
{
    /// <summary>
    /// Defines methods to serialize and deserialize <see cref="ServiceDto"/> objects from XML.
    /// </summary>
    public interface IXmlServiceSerializer
    {
        /// <summary>
        /// Deserializes the specified XML string into a <see cref="ServiceDto"/> object.
        /// </summary>
        /// <param name="xml">The XML string representing a <see cref="ServiceDto"/>.</param>
        /// <returns>
        /// The deserialized <see cref="ServiceDto"/> instance, or <c>null</c> if deserialization fails.
        /// </returns>
        ServiceDto Deserialize(string xml);

        /// <summary>
        /// Serializes a service DTO instance into an XML-formatted string using UTF-8 encoding and standard indentation.
        /// </summary>
        /// <param name="dto">The service DTO instance to be converted into an XML string.</param>
        /// <returns>A UTF-8 encoded XML string representing the service; otherwise, null if serialization fails or the input is empty.</returns>
        string Serialize(ServiceDto dto);
    }
}