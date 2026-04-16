using Servy.Core.DTOs;

namespace Servy.Core.Services
{
    /// <summary>
    /// Defines methods to serialize and deserialize <see cref="ServiceDto"/> objects from JSON.
    /// </summary>
    public interface IJsonServiceSerializer
    {
        /// <summary>
        /// Deserializes the specified JSON string into a <see cref="ServiceDto"/> object.
        /// </summary>
        /// <param name="json">The JSON string representing a <see cref="ServiceDto"/>.</param>
        /// <returns>
        /// The deserialized <see cref="ServiceDto"/> instance, or <c>null</c> if deserialization fails.
        /// </returns>
        ServiceDto? Deserialize(string? json);
    }
}