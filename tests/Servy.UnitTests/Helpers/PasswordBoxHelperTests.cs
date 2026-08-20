using Servy.Helpers;
using Servy.Testing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Xunit;
using Helper = Servy.Testing.Helper;

namespace Servy.UnitTests.Helpers
{
    /// <summary>
    /// WPF control creation requires an STA thread, so every test here runs its body through
    /// <see cref="Helper.RunOnSTA(Action, bool)"/> with an Application instance.
    /// </summary>
    public class PasswordBoxHelperTests
    {
        /// <summary>
        /// Verifies standard getter and setter flows for the attached property.
        /// </summary>
        [Fact]
        public void BoundPassword_GetAndSet_WorksCorrectly()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var passwordBox = new PasswordBox();
                string expectedPassword = "SuperSecretPassword123";

                // Act
                PasswordBoxHelper.SetBoundPassword(passwordBox, expectedPassword);
                string actualPassword = PasswordBoxHelper.GetBoundPassword(passwordBox);

                // Assert
                Assert.Equal(expectedPassword, actualPassword);
            }, createApp: true);
        }

        /// <summary>
        /// Covers the branch where OnBoundPasswordChanged is called on a dependency
        /// object that is NOT a PasswordBox (should fail gracefully and do nothing).
        /// </summary>
        [Fact]
        public void OnBoundPasswordChanged_WhenNotPasswordBox_DoesNotThrow()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var dummyObject = new DependencyObject();

                // Act & Assert
                var exception = Record.Exception(() =>
                {
                    PasswordBoxHelper.SetBoundPassword(dummyObject, "test");
                });

                Assert.Null(exception);
            }, createApp: true);
        }

        /// <summary>
        /// Covers the branch where a ViewModel update pushes down a new password
        /// while IsUpdating is false.
        /// </summary>
        [Fact]
        public void OnBoundPasswordChanged_WhenNotUpdating_UpdatesPasswordBoxPassword()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var passwordBox = new PasswordBox();
                string targetPassword = "VmDrivenPassword";

                // Act
                PasswordBoxHelper.SetBoundPassword(passwordBox, targetPassword);

                // Assert
                Assert.Equal(targetPassword, passwordBox.Password);
            }, createApp: true);
        }

        /// <summary>
        /// Covers the internal feedback-loop branch: when IsUpdating is true,
        /// changes to BoundPasswordProperty must NOT alter the PasswordBox.Password property.
        /// </summary>
        [Fact]
        public void OnBoundPasswordChanged_WhenIsUpdatingIsTrue_DoesNotOverwritePassword()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var passwordBox = new PasswordBox();
                passwordBox.Password = "OriginalControlPassword";

                // Set the internal IsUpdating attached property to true via reflection helper to simulate an active internal update loop
                var isUpdatingProperty = TestReflection.GetFieldStatic<DependencyProperty>(typeof(PasswordBoxHelper), "IsUpdatingProperty");
                passwordBox.SetValue(isUpdatingProperty, true);

                // Act
                PasswordBoxHelper.SetBoundPassword(passwordBox, "AttemptedVmOverride");

                // Assert
                // The password must remain untouched because IsUpdating was true.
                Assert.Equal("OriginalControlPassword", passwordBox.Password);
            }, createApp: true);
        }

        /// <summary>
        /// Verifies that when the bound password matches the control's current password,
        /// the control password is not reassigned (preventing redundant updates and caret jumps).
        /// </summary>
        [Fact]
        public void OnBoundPasswordChanged_ValueAlreadyInControl_DoesNotReassignPassword()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var passwordBox = new PasswordBox();
                passwordBox.Password = "SamePassword";   // control already holds the value

                int changedCount = 0;
                passwordBox.PasswordChanged += (_, e) => changedCount++;

                // Act
                // DP transitions null -> "SamePassword", so the callback runs,
                // finds Password == newValue, and must skip the assignment.
                PasswordBoxHelper.SetBoundPassword(passwordBox, "SamePassword");

                Assert.Equal(0, changedCount);
            }, createApp: true);
        }

        /// <summary>
        /// Verifies that setting <see cref="PasswordBoxHelper.BoundPasswordProperty"/> to <see langword="null"/>
        /// safely falls back to <see cref="string.Empty"/> and clears the control password without throwing an exception.
        /// </summary>
        [Fact]
        public void OnBoundPasswordChanged_NullValue_ClearsPasswordInsteadOfThrowing()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var passwordBox = new PasswordBox();
                PasswordBoxHelper.SetBoundPassword(passwordBox, "ExistingPassword123");

                // Act
                var exception = Record.Exception(() =>
                    passwordBox.SetValue(PasswordBoxHelper.BoundPasswordProperty, null));

                // Assert
                Assert.Null(exception);
                Assert.Equal(string.Empty, passwordBox.Password);
            }, createApp: true);
        }

        /// <summary>
        /// Covers the PasswordBox_PasswordChanged event branch where user input updates the control password,
        /// which correctly syncs the value back up to the attached property.
        /// </summary>
        [Fact]
        public void PasswordBox_PasswordChanged_PushesValueToBoundPassword()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var passwordBox = new PasswordBox();
                string typedPassword = "UserTypedThisInUI";

                // Establish the event hooks by setting an initial binding value
                PasswordBoxHelper.SetBoundPassword(passwordBox, string.Empty);

                // Act
                passwordBox.Password = typedPassword; // Triggers PasswordChanged internally

                // Assert
                var boundValue = PasswordBoxHelper.GetBoundPassword(passwordBox);
                Assert.Equal(typedPassword, boundValue);
            }, createApp: true);
        }

        /// <summary>
        /// Covers the binding infrastructure branch where an active BindingExpression
        /// is evaluated and forced to execute an immediate UpdateSource().
        /// </summary>
        [Fact]
        public void PasswordBox_PasswordChanged_WithActiveBinding_UpdatesSource()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var passwordBox = new PasswordBox();
                var dummyViewModel = new FakePasswordViewModel { Password = "Initial" };

                // Set up a standard Two-Way binding to simulate realistic application configuration
                var binding = new Binding("Password")
                {
                    Source = dummyViewModel,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.Explicit // explicit to test the UpdateSource line directly
                };
                BindingOperations.SetBinding(passwordBox, PasswordBoxHelper.BoundPasswordProperty, binding);

                // Act
                passwordBox.Password = "ChangedInUI";

                // Assert
                // If the explicit trigger updated the source, our viewmodel will receive the string immediately
                Assert.Equal("ChangedInUI", dummyViewModel.Password);
            }, createApp: true);
        }

        private class FakePasswordViewModel
        {
            public string Password { get; set; } = string.Empty;
        }
    }
}
