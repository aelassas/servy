using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Servy.Helpers
{
    /// <summary>
    /// Provides an attached property to allow two-way data binding of the <see cref="PasswordBox.Password"/> property.
    /// This helper simulates <see cref="UpdateSourceTrigger.PropertyChanged"/> behavior so that the ViewModel property
    /// updates immediately whenever the user types, pastes, or deletes characters in the password field.
    /// </summary>
    /// <remarks>
    /// The WPF <see cref="PasswordBox.Password"/> property is not a dependency property, so it cannot be bound directly.
    /// This helper works around that limitation by exposing an attached <c>BoundPassword</c> property and synchronizing
    /// it with the <see cref="PasswordBox.Password"/> value via event handling.
    /// <para>
    /// To prevent caret position jumps and reversed text issues, the password is only set on the control
    /// if it has actually changed. An internal <c>IsUpdating</c> flag prevents recursive updates.
    /// </para>
    /// </remarks>
    public static class PasswordBoxHelper
    {
        /// <summary>
        /// Identifies the attached <c>BoundPassword</c> dependency property.
        /// Bind this property to your ViewModel's password property to enable two-way updates.
        /// </summary>
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BoundPassword",
                typeof(string),
                typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnBoundPasswordChanged));

        /// <summary>
        /// Identifies an internal attached property used to track whether a password update is currently in progress.
        /// This is used to prevent feedback loops when the bound value changes from both the control and the ViewModel.
        /// </summary>
        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached(
                "IsUpdating",
                typeof(bool),
                typeof(PasswordBoxHelper),
                new PropertyMetadata(false));

        /// <summary>
        /// Gets the bound password value from the specified <see cref="PasswordBox"/>.
        /// </summary>
        /// <param name="dp">The <see cref="DependencyObject"/> (should be a <see cref="PasswordBox"/>).</param>
        /// <returns>The current bound password string.</returns>
        public static string GetBoundPassword(DependencyObject dp) =>
            (string)dp.GetValue(BoundPasswordProperty);

        /// <summary>
        /// Sets the bound password value for the specified <see cref="PasswordBox"/>.
        /// </summary>
        /// <param name="dp">The <see cref="DependencyObject"/> (should be a <see cref="PasswordBox"/>).</param>
        /// <param name="value">The new password value to set.</param>
        public static void SetBoundPassword(DependencyObject dp, string value) =>
            dp.SetValue(BoundPasswordProperty, value);

        /// <summary>
        /// Gets a value indicating whether the specified <see cref="PasswordBox"/> is currently updating
        /// its password from the ViewModel (to prevent recursive changes).
        /// </summary>
        private static bool GetIsUpdating(DependencyObject dp) =>
            (bool)dp.GetValue(IsUpdatingProperty);

        /// <summary>
        /// Sets a value indicating whether the specified <see cref="PasswordBox"/> is currently updating
        /// its password from the ViewModel.
        /// </summary>
        private static void SetIsUpdating(DependencyObject dp, bool value) =>
            dp.SetValue(IsUpdatingProperty, value);

        /// <summary>
        /// Handles changes to the <see cref="BoundPasswordProperty"/>.
        /// Synchronizes the new bound value to the <see cref="PasswordBox.Password"/> without
        /// overwriting if the values are already equal.
        /// </summary>
        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox passwordBox)
            {
                passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;

                if (!GetIsUpdating(passwordBox))
                {
                    passwordBox.Password = e.NewValue as string ?? string.Empty;
                }

                passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
            }
        }

        /// <summary>
        /// Handles the <see cref="PasswordBox.PasswordChanged"/> event by pushing the current password
        /// to the bound property and updating the ViewModel immediately (like <see cref="UpdateSourceTrigger.PropertyChanged"/>).
        /// </summary>
        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                SetIsUpdating(passwordBox, true);
                SetBoundPassword(passwordBox, passwordBox.Password);

                // Force the binding source to update immediately
                BindingExpression be = BindingOperations.GetBindingExpression(passwordBox, BoundPasswordProperty);
                be?.UpdateSource();

                SetIsUpdating(passwordBox, false);
            }
        }
    }
}
