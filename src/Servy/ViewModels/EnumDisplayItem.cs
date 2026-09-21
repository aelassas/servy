namespace Servy.ViewModels
{
    /// <summary>
    /// Represents a generic item used to encapsulate an enumeration value alongside its localized text representation for UI binding.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type being wrapped for display.</typeparam>
    public class EnumDisplayItem<TEnum> where TEnum : struct, Enum
    {
        /// <summary>Gets the target enumeration value.</summary>
        public required TEnum Value { get; init; }

        /// <summary>Gets the localized display name for the enumeration value.</summary>
        public required string DisplayName { get; init; }
    }
}
