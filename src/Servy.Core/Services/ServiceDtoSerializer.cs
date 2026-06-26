using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides a unified base class that orchestrates the operational skeleton for serializing and 
    /// deserializing <see cref="ServiceDto"/> objects, ensuring structural consistency across formats.
    /// </summary>
    public abstract class ServiceDtoSerializer
    {
        /// <summary>
        /// Gets the name of the data format (e.g., "JSON", "XML") used in structural logging diagnostics.
        /// </summary>
        protected abstract string FormatName { get; }

        /// <summary>
        /// When overridden in a derived class, executes the format-specific parsing mechanics to reconstruct a DTO from a string representation.
        /// </summary>
        /// <param name="content">The formatted string containing the service definition data.</param>
        /// <returns>A populated <see cref="ServiceDto"/> instance if successful; otherwise, <c>null</c>.</returns>
        protected abstract ServiceDto DeserializeCore(string content);

        /// <summary>
        /// When overridden in a derived class, executes the format-specific serialization mechanics to transform a DTO into its string expression.
        /// </summary>
        /// <param name="dto">The service definition metadata transfer object to transform.</param>
        /// <returns>A formatted representation string of the service definition.</returns>
        protected abstract string SerializeCore(ServiceDto dto);

        /// <summary>
        /// Extracts and formats line, index, or position metadata from a format-specific processing exception.
        /// </summary>
        /// <param name="ex">The thrown format runtime exception.</param>
        /// <returns>A formatted string detailing the exact exception location context, or an empty string if unavailable.</returns>
        protected virtual string FormatLineInfo(Exception ex) => string.Empty;

        /// <summary>
        /// Deserializes a format-specific textual stream representation into a structured <see cref="ServiceDto"/>.
        /// </summary>
        /// <param name="input">The raw text block content.</param>
        /// <returns>The populated data contract on successful parsing; otherwise, <c>null</c> if input is void or parsing errors materialize.</returns>
        public ServiceDto Deserialize(string input)
        {
            // Initial guard: null/empty input deserializes to null per contract
            if (string.IsNullOrWhiteSpace(input))
                return null;

            try
            {
                // Delegate to format-specific parsing engine
                var dto = DeserializeCore(input);

                // If deserialization succeeded, apply defaults (e.g., setting missing timeouts)
                if (dto != null)
                {
                    ServiceDtoHelper.ApplyDefaultsAndResetIdentity(dto);
                }

                return dto;
            }
            catch (Exception ex)
            {
                var lineInfoMessage = FormatLineInfo(ex);

                if (!string.IsNullOrEmpty(lineInfoMessage))
                {
                    Logger.Error($"{FormatName} Deserialization failed{lineInfoMessage}.", ex);
                }
                else
                {
                    // Catch-all fallthrough layer to protect against secondary runtime anomalies,
                    // such as custom contract faults or helper assignment errors.
                    Logger.Error($"{FormatName} Deserialization encountered a failure.", ex);
                }

                // Returning null fulfills the contract: "returns null if deserialization fails"
                return null;
            }
        }

        /// <summary>
        /// Serializes a structured <see cref="ServiceDto"/> instance into its corresponding format-specific textual expression.
        /// </summary>
        /// <param name="dto">The source metadata object transfer layout.</param>
        /// <returns>The formatted output manifest representation string; or <c>null</c> if serialization fails or argument is missing.</returns>
        public string Serialize(ServiceDto dto)
        {
            if (dto == null)
                return null;

            try
            {
                return SerializeCore(dto);
            }
            catch (Exception ex)
            {
                Logger.Error($"{FormatName} Serialization failed for service: {dto.Name}", ex);
                return null;
            }
        }
    }
}