using Servy.Core.Enums;
using Servy.Manager.Resources;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts between <see cref="ServiceStartType"/> enum values and their localized string
    /// representations defined in <see cref="Strings"/>.
    /// </summary>
    public class StartupTypeConverter : EnumLocalizedConverter<ServiceStartType>
    {
        private static readonly Dictionary<ServiceStartType, Func<string>> StartupMap = new Dictionary<ServiceStartType, Func<string>>()
        {
            [ServiceStartType.Automatic] = () => Strings.StartupType_Automatic,
            [ServiceStartType.AutomaticDelayedStart] = () => Strings.StartupType_AutomaticDelayedStart,
            [ServiceStartType.Manual] = () => Strings.StartupType_Manual,
            [ServiceStartType.Disabled] = () => Strings.StartupType_Disabled,
            [ServiceStartType.Unknown] = () => Strings.StartupType_Unknown,
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="StartupTypeConverter"/> class.
        /// </summary>
        public StartupTypeConverter() : base(StartupMap)
        {
        }

        /// <summary>
        /// Returns a fallback string when the bound value is not a mapped <see cref="ServiceStartType"/>.
        /// </summary>
        /// <param name="value">The unmapped source value.</param>
        /// <returns>The value's string representation, or <see cref="Strings.Label_Fetching"/> when null.</returns>
        protected override string GetFallbackValue(object? value)
        {
            // Unlike StatusConverter, ServiceStartType has no in-band 'not yet known' member
            // (Unknown is a real SCM value), so null is the loading state and renders as 'Fetching...'.
            return value?.ToString() ?? Strings.Label_Fetching;
        }
    }
}
