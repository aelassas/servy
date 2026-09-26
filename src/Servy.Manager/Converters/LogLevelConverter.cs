using Servy.Core.Enums;
using Servy.Manager.Resources;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts <see cref="EventLogLevel"/> values to localized display strings.
    /// </summary>
    public class LogLevelConverter : EnumLocalizedConverter<EventLogLevel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LogLevelConverter"/> class.
        /// </summary>
        public LogLevelConverter() : base(new Dictionary<EventLogLevel, Func<string>>
        {
            [EventLogLevel.Information] = () => Strings.LogLevel_Information,
            [EventLogLevel.Warning] = () => Strings.LogLevel_Warning,
            [EventLogLevel.Error] = () => Strings.LogLevel_Error,
            [EventLogLevel.All] = () => Strings.LogLevel_All,
        })
        { }

        /// <inheritdoc />
        protected override string GetFallbackValue(object? value)
        {
            // Returns raw string representation or empty string on non-matching or null values
            return value?.ToString() ?? string.Empty;
        }
    }
}
