using Servy.Core.DTOs;

namespace Servy.Core.Helpers
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
    }
}