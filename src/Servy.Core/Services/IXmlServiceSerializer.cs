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
        /// The deserialized <see cref="ServiceDto"/> instance, or <c>null</c> if the input is null, empty, or whitespace-only, or deserialization fails.
        /// </returns>
        /// <remarks>
        /// Absent optional fields are populated from <see cref="Servy.Core.Config.AppConfig"/> defaults, and the service
        /// identity follows the Global Identity Reset on Import policy: a passwordless <c>UserAccount</c>
        /// (LocalService, NetworkService, NT SERVICE\..., IIS APPPOOL\..., a gMSA) is kept, and so is a local user
        /// account that can log on with a blank password on this machine (LimitBlankPasswordUse = 0); any other
        /// account is discarded and the service is reset to a password-less LocalSystem baseline. <c>Password</c> is
        /// never read from the payload. Configure an account that needs a password after import.
        /// </remarks>
        ServiceDto? Deserialize(string? xml);

        /// <summary>
        /// Serializes a service DTO instance into an XML-formatted string using UTF-8 encoding and standard indentation.
        /// </summary>
        /// <param name="dto">The service DTO instance to be converted into an XML string.</param>
        /// <returns>A UTF-8 encoded XML string representing the service; otherwise, <c>null</c> if the input is <c>null</c> or serialization fails.</returns>
        string? Serialize(ServiceDto? dto);
    }
}
