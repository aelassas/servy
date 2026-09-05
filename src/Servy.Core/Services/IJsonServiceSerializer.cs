using Servy.Core.DTOs;
using Servy.Core.Security;

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
        /// The deserialized <see cref="ServiceDto"/> instance, or <c>null</c> if the input is null, empty, or whitespace-only, or deserialization fails.
        /// </returns>
        /// <remarks>
        /// Absent optional fields are populated from <see cref="Servy.Core.Config.AppConfig"/> defaults, and the service
        /// identity is reset to a password-less LocalSystem baseline: <c>UserAccount</c> and
        /// <c>Password</c> in the payload are discarded. Configure a custom account after import.
        /// </remarks>
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
        /// Serialization reuses <see cref="JsonSecurity.UntrustedDataSettings"/> so that a single
        /// hardened settings source governs both directions. Note that <c>NullValueHandling.Ignore</c>
        /// applies to both: null members are omitted from the output, and explicit nulls in the input are
        /// skipped rather than assigned. Properties with a non-null initializer (<c>Name</c>,
        /// <c>ExecutablePath</c>) therefore read back as their default, not as null.
        /// </remarks>
        string Serialize(ServiceDto dto);
    }
}
