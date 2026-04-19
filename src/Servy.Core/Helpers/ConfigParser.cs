using Servy.Core.Logging;
using System.Runtime.CompilerServices;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides unified parsing and validation primitives for service configuration values.
    /// </summary>
    /// <remarks>
    /// This utility ensures consistent fallback behavior and centralized warning logging across the 
    /// UI, Manager, and repository mappers. It prevents silent configuration drift by explicitly 
    /// logging when a default value is substituted due to invalid input.
    /// </remarks>
    public static class ConfigParser
    {
        /// <summary>
        /// Attempts to parse a string into an integer. 
        /// Logs a warning and returns the default value if the input is malformed.
        /// </summary>
        /// <param name="rawValue">The raw string value to be parsed.</param>
        /// <param name="defaultValue">The fallback value to return if parsing fails or the input is empty.</param>
        /// <param name="fieldName">
        /// The name of the field being parsed. Automatically captured via <see cref="CallerArgumentExpressionAttribute"/>.
        /// </param>
        /// <returns>The parsed integer, or <paramref name="defaultValue"/> if parsing fails.</returns>
        public static int ParseInt(
            string rawValue,
            int defaultValue,
            [CallerArgumentExpression("rawValue")] string fieldName = "")
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return defaultValue; // Normal empty state, no warning needed
            }

            if (int.TryParse(rawValue, out var result))
            {
                return result;
            }

            Logger.Warn($"Invalid integer value '{rawValue}' for {fieldName}. Falling back to default: {defaultValue}.");
            return defaultValue;
        }

        /// <summary>
        /// Validates that a numeric value exists within the defined range of a specific <see cref="Enum"/>.
        /// </summary>
        /// <typeparam name="TEnum">The target enum type to validate against.</typeparam>
        /// <param name="value">The nullable numeric value to be mapped to the enum.</param>
        /// <param name="defaultValue">The fallback enum member if the input is null or out of range.</param>
        /// <param name="fieldName">
        /// The name of the field being parsed. Automatically captured via <see cref="CallerArgumentExpressionAttribute"/>.
        /// </param>
        /// <returns>A validated member of <typeparamref name="TEnum"/>.</returns>
        /// <remarks>
        /// This method uses <see cref="Enum.IsDefined(Type, object)"/> to ensure that the numeric value
        /// corresponds to a valid member of the enumeration, preventing invalid casts from reaching Win32 API calls.
        /// </remarks>
        public static TEnum ParseEnum<TEnum>(
            int? value,
            TEnum defaultValue,
            [CallerArgumentExpression("value")] string fieldName = "") where TEnum : struct, Enum
        {
            if (!value.HasValue)
            {
                return defaultValue;
            }

            var underlyingType = Enum.GetUnderlyingType(typeof(TEnum));

            try
            {
                // Ensure the numeric type matches the underlying enum type for valid Enum.IsDefined check
                var convertedValue = Convert.ChangeType(value.Value, underlyingType);

                if (Enum.IsDefined(typeof(TEnum), convertedValue))
                {
                    return (TEnum)convertedValue;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Type conversion failed for {fieldName} with value '{value}'. Falling back to default: {defaultValue}. Exception: {ex.Message}");
                return defaultValue;
            }

            Logger.Warn($"Undefined enum value '{value}' for {typeof(TEnum).Name} ({fieldName}). Falling back to default: {defaultValue}.");
            return defaultValue;
        }
    }
}