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
            // Returns raw string representation or empty string on non-matching or null values.
            // Unlike StartupTypeConverter there is no 'loading' state to show: the bindings are the level
            // ComboBox, whose items are the fixed EventLogLevel list, and the Level column cell in LogsView
            // (text and automation name), whose values come from EventLogReader.ParseLevel, so null is not
            // expected and renders blank.
            return value?.ToString() ?? string.Empty;
        }
    }
}
