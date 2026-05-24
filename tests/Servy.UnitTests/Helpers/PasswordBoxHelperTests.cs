using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Xunit;
using Servy.Helpers;

namespace Servy.Helpers.UnitTests
{
    public class PasswordBoxHelperTests
    {
        /// <summary>
        /// Verifies standard getter and setter flows for the attached property.
        /// </summary>
        [WpfFact] // Executes test on an STA thread to allow WPF control creation
        public void BoundPassword_GetAndSet_WorksCorrectly()
        {
            // Arrange
            var passwordBox = new PasswordBox();
            string expectedPassword = "SuperSecretPassword123";

            // Act
            PasswordBoxHelper.SetBoundPassword(passwordBox, expectedPassword);
            string actualPassword = PasswordBoxHelper.GetBoundPassword(passwordBox);

            // Assert
            Assert.Equal(expectedPassword, actualPassword);
        }

        /// <summary>
        /// Covers the branch where OnBoundPasswordChanged is called on a dependency 
        /// object that is NOT a PasswordBox (should fail gracefully and do nothing).
        /// </summary>
        [WpfFact]
        public void OnBoundPasswordChanged_WhenNotPasswordBox_DoesNotThrow()
        {
            // Arrange
            var dummyObject = new DependencyObject();

            // Act & Assert
            var exception = Record.Exception(() =>
            {
                PasswordBoxHelper.SetBoundPassword(dummyObject, "test");
            });

            Assert.Null(exception);
        }

        /// <summary>
        /// Covers the branch where a ViewModel update pushes down a new password 
        /// while IsUpdating is false.
        /// </summary>
        [WpfFact]
        public void OnBoundPasswordChanged_WhenNotUpdating_UpdatesPasswordBoxPassword()
        {
            // Arrange
            var passwordBox = new PasswordBox();
            string targetPassword = "VmDrivenPassword";

            // Act
            PasswordBoxHelper.SetBoundPassword(passwordBox, targetPassword);

            // Assert
            Assert.Equal(targetPassword, passwordBox.Password);
        }

        /// <summary>
        /// Covers the internal feedback-loop branch: when IsUpdating is true, 
        /// changes to BoundPasswordProperty must NOT alter the PasswordBox.Password property.
        /// </summary>
        [WpfFact]
        public void OnBoundPasswordChanged_WhenIsUpdatingIsTrue_DoesNotOverwritePassword()
        {
            // Arrange
            var passwordBox = new PasswordBox();
            passwordBox.Password = "OriginalControlPassword";

            // Explicitly set the internal IsUpdating property to true via reflection or 
            // by accessing the private field to simulate an active internal update loop.
            var isUpdatingPropertyField = typeof(PasswordBoxHelper).GetField("IsUpdatingProperty",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            var isUpdatingProperty = (DependencyProperty)isUpdatingPropertyField.GetValue(null);
            passwordBox.SetValue(isUpdatingProperty, true);

            // Act
            PasswordBoxHelper.SetBoundPassword(passwordBox, "AttemptedVmOverride");

            // Assert
            // The password must remain untouched because IsUpdating was true.
            Assert.Equal("OriginalControlPassword", passwordBox.Password);
        }

        /// <summary>
        /// Covers the PasswordBox_PasswordChanged event branch where user input updates the control password, 
        /// which correctly syncs the value back up to the attached property.
        /// </summary>
        [WpfFact]
        public void PasswordBox_PasswordChanged_PushesValueToBoundPassword()
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
        }

        /// <summary>
        /// Covers the binding infrastructure branch where an active BindingExpression 
        /// is evaluated and forced to execute an immediate UpdateSource().
        /// </summary>
        [WpfFact]
        public void PasswordBox_PasswordChanged_WithActiveBinding_UpdatesSource()
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
        }

        private class FakePasswordViewModel
        {
            public string Password { get; set; } = string.Empty;
        }
    }
}