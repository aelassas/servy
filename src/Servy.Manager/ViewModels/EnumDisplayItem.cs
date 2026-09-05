namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// Binds an enum value to a localized display name for ComboBox items.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    public class EnumDisplayItem<TEnum> where TEnum : struct, Enum
    {
        /// <summary>
        /// Gets or sets the underlying enum value.
        /// </summary>
        public TEnum Value { get; set; }

        /// <summary>
        /// Gets or sets the localized label shown in the UI.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <inheritdoc />
        public override string ToString() => DisplayName ?? Value.ToString();
    }
}
