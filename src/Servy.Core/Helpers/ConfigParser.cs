using Servy.Core.Logging;
using System.Globalization;
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
            string? rawValue,
            int defaultValue,
            [CallerArgumentExpression("rawValue")] string fieldName = "")
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return defaultValue; // Normal empty state, no warning needed
            }

            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            Logger.Warn($"Invalid integer value '{rawValue}' for {fieldName}. Falling back to default: {defaultValue}.");
            return defaultValue;
        }

        /// <summary>
        /// Attempts to parse a string into a boolean. 
        /// Supports standard .NET booleans alongside common config-file semantic variants ('1', '0', 'yes', 'no', 'on', 'off', 'y', 'n').
        /// Logs a warning and returns the default value if the input is malformed.
        /// </summary>
        /// <param name="rawValue">The raw string value to be parsed.</param>
        /// <param name="defaultValue">The fallback value to return if parsing fails or the input is empty.</param>
        /// <param name="fieldName">
        /// The name of the field being parsed. Automatically captured via <see cref="CallerArgumentExpressionAttribute"/>.
        /// </param>
        /// <returns>The parsed boolean, or <paramref name="defaultValue"/> if parsing fails.</returns>
        public static bool ParseBool(
            string? rawValue,
            bool defaultValue,
            [CallerArgumentExpression("rawValue")] string fieldName = "")
        {
            // Normal empty state for configuration DTOs; returns the default without cluttering logs.
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return defaultValue;
            }

            string sanitized = rawValue.Trim();

            // .NET's bool.TryParse handles common "True"/"False" strings (case-insensitive).
            if (bool.TryParse(sanitized, out var result))
            {
                return result;
            }

            // Fall back to evaluating common environment, human-edited, and cross-platform config aliases
            switch (sanitized.ToLowerInvariant())
            {
                case "1":
                case "yes":
                case "y":
                case "on":
                    return true;

                case "0":
                case "no":
                case "n":
                case "off":
                    return false;
            }

            // Consistent with Servy's diagnostic patterns to ensure misconfigured services are easily triaged.
            Logger.Warn($"Invalid boolean value '{rawValue}' for {fieldName}. Falling back to default: {defaultValue}.");
            return defaultValue;
        }

        /// <summary>
        /// Validates that a numeric value exists within the defined range or valid bitwise combinations of a specific <see cref="Enum"/>.
        /// </summary>
        /// <typeparam name="TEnum">The target enum type to validate against.</typeparam>
        /// <param name="value">The nullable numeric value to be mapped to the enum.</param>
        /// <param name="defaultValue">The fallback enum member if the input is null or out of range.</param>
        /// <param name="fieldName">
        /// The name of the field being parsed. Automatically captured via <see cref="CallerArgumentExpressionAttribute"/>.
        /// </param>
        /// <returns>A validated member of <typeparamref name="TEnum"/>.</returns>
        /// <remarks>
        /// <para>
        /// For standard enumerations, this method uses <see cref="Enum.IsDefined(Type, object)"/> to ensure that the numeric value
        /// corresponds to a declared member, preventing invalid casts from reaching native layers.
        /// </para>
        /// <para>
        /// For enumerations decorated with <see cref="FlagsAttribute"/>, it skips the strict lookup check to accommodate valid 
        /// bitwise combinations, while verifying that the input contains no unmapped or invalid high bits.
        /// </para>
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

            var enumType = typeof(TEnum);
            var underlyingType = Enum.GetUnderlyingType(enumType);

            try
            {
                // Ensure the numeric type matches the underlying enum type for valid Enum.IsDefined check
                var convertedValue = Convert.ChangeType(value.Value, underlyingType);

                // FORWARD-COMPATIBILITY: Check if this is a bitmask flags enum.
                if (enumType.IsDefined(typeof(FlagsAttribute), false))
                {
                    // Convert numeric value back to enum type to allow safe processing
                    TEnum parsedEnum = (TEnum)convertedValue;

                    // ToString() on a [Flags] enum returns member names when every bit maps to a declared
                    // member, and the raw number otherwise - so comparing against the numeric string
                    // detects unmapped bits across all underlying types.
                    if (parsedEnum.ToString() != convertedValue.ToString())
                    {
                        return parsedEnum;
                    }
                }
                else if (Enum.IsDefined(enumType, convertedValue))
                {
                    return (TEnum)convertedValue;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Type conversion failed for {fieldName} with value '{value}'. Falling back to default: {defaultValue}. Exception: {ex.Message}");
                return defaultValue;
            }

            Logger.Warn($"Undefined enum value '{value}' for {enumType.Name} ({fieldName}). Falling back to default: {defaultValue}.");
            return defaultValue;
        }

        /// <summary>
        /// Converts the string representation of the name or numeric value of one or more 
        /// enumerated constants to an equivalent enumerated object.
        /// </summary>
        /// <typeparam name="TEnum">The target enum type to validate against.</typeparam>
        /// <param name="value">The nullable string value to be parsed.</param>
        /// <param name="defaultValue">The fallback enum member if the input is null, empty, or out of range.</param>
        /// <param name="fieldName">
        /// The name of the field being parsed. Automatically captured via <see cref="CallerArgumentExpressionAttribute"/>.
        /// </param>
        /// <returns>A validated member of <typeparamref name="TEnum"/>.</returns>
        /// <remarks>
        /// This method goes beyond a simple TryParse by checking metadata definitions. For non-flags enums, it enforces 
        /// strict verification via <see cref="Enum.IsDefined(Type, object)"/> to prevent raw numbers like "999" from slipping past. 
        /// For <see cref="FlagsAttribute"/> enums, comma-separated combinations are parsed and honored correctly.
        /// </remarks>
        public static TEnum ParseEnum<TEnum>(
            string? value,
            TEnum defaultValue,
            [CallerArgumentExpression("value")] string fieldName = "") where TEnum : struct, Enum
        {
            // 1. Guard against null or empty input by returning the default immediately
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            var enumType = typeof(TEnum);

            // 2. Attempt to parse the string using the built-in Enum engine
            if (Enum.TryParse<TEnum>(value, true, out var result))
            {
                // 3. Robustness check: Separate standard enums from bitmask flag enums.
                // If it's a flags enum, TryParse naturally handles named combinations (e.g. "Read, Write")
                // as well as combined numeric variants. We validate string parity to ensure it contains no out-of-range values.
                if (enumType.IsDefined(typeof(FlagsAttribute), false))
                {
                    // Accept any combination whose ToString() is not a bare number (i.e. all bits map to names)
                    var asString = result.ToString();
                    if (!long.TryParse(asString, out _))   // ToString() returned names, not a residual number
                        return result;
                }
                else if (Enum.IsDefined(enumType, result))
                {
                    return result;
                }
            }

            // 4. Log the failure and return the default value to prevent service interruption
            Logger.Warn($"Undefined or malformed enum value '{value}' for {enumType.Name} ({fieldName}). Falling back to default: {defaultValue}.");
            return defaultValue;
        }
    }
}