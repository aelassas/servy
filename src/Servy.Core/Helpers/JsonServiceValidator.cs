using Newtonsoft.Json;
using Servy.Core.DTOs;
using System;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides validation for JSON strings that should represent a <see cref="ServiceDto"/>.
    /// </summary>
    public static class JsonServiceValidator
    {
        /// <summary>
        /// Validates that the input JSON is a valid <see cref="ServiceDto"/> and contains required fields.
        /// </summary>
        /// <param name="json">The JSON string to validate.</param>
        /// <param name="errorMessage">If validation fails, contains the reason.</param>
        /// <returns>True if valid; otherwise false.</returns>
        public static bool TryValidate(string json, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "JSON cannot be null or empty.";
                return false;
            }

            ServiceDto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<ServiceDto>(json);
            }
            catch (Exception ex)
            {
                errorMessage = $"Invalid JSON format: {ex.Message}";
                return false;
            }

            if (dto == null)
            {
                errorMessage = "Failed to deserialize JSON to ServiceDto.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errorMessage = "Service name is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(dto.ExecutablePath))
            {
                errorMessage = "Executable path is required.";
                return false;
            }

            return true;
        }
    }
}
