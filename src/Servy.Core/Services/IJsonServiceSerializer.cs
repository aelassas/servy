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
        ServiceDto Deserialize(string json);

        /// <summary>
        /// Serializes a <see cref="ServiceDto"/> into an indented JSON string.
        /// </summary>
        /// <param name="dto">The service data transfer object to serialize.</param>
        /// <returns>
        /// A formatted JSON string representation of the <paramref name="dto"/>, 
        /// or <see langword="null"/> if the input is null or serialization fails.
        /// </returns>
        /// <remarks>
        /// This method utilizes the same <see cref="JsonSecurity.UntrustedDataSettings"/> 
        /// as the deserialization path to ensure perfect round-trip symmetry for custom 
        /// converters, enum formatting, and contract resolvers.
        /// </remarks>
        string Serialize(ServiceDto dto);
    }
}