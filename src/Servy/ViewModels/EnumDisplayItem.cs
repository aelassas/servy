namespace Servy.ViewModels
{
    /// <summary>
    /// Represents a generic item used to encapsulate an enumeration value alongside its localized text representation for UI binding.
    /// </summary>
    /// <typeparam name="TEnum">The target enumeration type context constraint.</typeparam>
    public class EnumDisplayItem<TEnum> where TEnum : struct, Enum
    {
        /// <summary>
        /// Gets or sets the target enumeration value.
        /// </summary>
        public TEnum Value { get; set; }

        /// <summary>
        /// Gets or sets the localized display name for the enumeration value.
        /// </summary>
        public string? DisplayName { get; set; }
    }
}