using Servy.Core.Enums;
using Servy.Manager.Resources;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts between <see cref="ServiceStartType"/> enum values and their localized string representations
    /// defined in <see cref="Strings.resx"/>.
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
        /// Provides a fallback string value when the start type calculation routing pass breaks.
        /// </summary>
        /// <param name="value">The raw unmapped source value.</param>
        /// <returns>The string representation or a customized fetching notice label.</returns>
        protected override string GetFallbackValue(object value)
        {
            return value?.ToString() ?? Strings.Label_Fetching;
        }
    }
}