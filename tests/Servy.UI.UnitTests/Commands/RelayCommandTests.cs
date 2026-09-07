using Servy.UI.Commands;
using System;
using Xunit;

namespace Servy.UI.UnitTests.Commands
{
    public class RelayCommandTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_NullExecute_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            // Branch: execute ?? throw new ArgumentNullException(nameof(execute))
            Assert.Throws<ArgumentNullException>(() => new RelayCommand<string>(null));
        }

        #endregion

        #region CanExecute Tests

        [Fact]
        public void CanExecute_NoPredicate_ReturnsTrue()
        {
            // Arrange
            // Branch: if (_canExecute == null) return true;
            var command = new RelayCommand<string>(_ => { });

            // Act
            var result = command.CanExecute("test");

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("valid", true)]
        [InlineData("invalid", false)]
        public void CanExecute_WithPredicate_ReturnsPredicateResult(string input, bool expected)
        {
            // Arrange
            // Branch: Unbox(parameter) -> parameter is T typed (Matching Type)
            var command = new RelayCommand<string>(_ => { }, p => p == "valid");

            // Act
            var result = command.CanExecute(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CanExecute_NullParameter_PassesDefaultTToPredicate()
        {
            // Arrange
            // Branch: Unbox(parameter) -> parameter is null (Null input)
            // Null parameters (e.g. WPF bindings with no CommandParameter specified)
            // safely unbox to default(T?) without throwing. We use int to verify that
            // default(int), which is 0, is passed to the predicate.
            bool receivedDefault = false;
            var command = new RelayCommand<int>(_ => { }, p =>
            {
                if (p == 0) receivedDefault = true;
                return true;
            });

            // Act
            command.CanExecute(null);

            // Assert
            Assert.True(receivedDefault);
        }

        [Fact]
        public void CanExecute_MismatchingType_ThrowsArgumentException()
        {
            // Arrange
            // Branch: Unbox(parameter) -> non-null parameter of mismatched type
            // Non-null parameters of a mismatched type throw ArgumentException detailing the type mismatch.
            var command = new RelayCommand<int>(_ => { }, _ => true);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => command.CanExecute("not an int"));
            Assert.Contains("CommandParameter of type 'System.String' cannot be bound to RelayCommand<System.Int32>", ex.Message);
        }

        #endregion

        #region Execute Tests

        [Fact]
        public void Execute_ValidType_InvokesActionWithParameter()
        {
            // Arrange
            // Branch: Unbox(parameter) -> parameter is T typed (Matching Type)
            string receivedValue = null;
            var command = new RelayCommand<string>(p => receivedValue = p);

            // Act
            command.Execute("hello");

            // Assert
            Assert.Equal("hello", receivedValue);
        }

        [Fact]
        public void Execute_NullParameter_InvokesActionWithDefaultT()
        {
            // Arrange
            // Branch: Unbox(parameter) -> parameter is null (Null input)
            int receivedValue = -1;
            var command = new RelayCommand<int>(p => receivedValue = p);

            // Act
            command.Execute(null);

            // Assert
            Assert.Equal(0, receivedValue);
        }

        [Fact]
        public void Execute_MismatchingType_ThrowsArgumentException()
        {
            // Arrange
            // Branch: Unbox(parameter) -> non-null parameter of mismatched type
            var command = new RelayCommand<int>(_ => { });

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => command.Execute("not an int"));
            Assert.Contains("CommandParameter of type 'System.String' cannot be bound to RelayCommand<System.Int32>", ex.Message);
        }

        #endregion

        #region Event and Manager Tests

        [Fact]
        public void CanExecuteChanged_SubscribeAndUnsubscribe_DoesNotThrow()
        {
            // Arrange
            // Exercises the custom add/remove accessors, which forward to CommandManager.RequerySuggested.
            // Firing the event requires a pumped dispatcher and is intentionally out of scope here.
            var command = new RelayCommand<string>(_ => { });
            EventHandler handler = (s, e) => { };

            // Act
            var exception = Record.Exception(() =>
            {
                command.CanExecuteChanged += handler;
                command.CanExecuteChanged -= handler;
            });

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void RaiseCanExecuteChanged_DoesNotThrow()
        {
            // Arrange
            // Verifies RaiseCanExecuteChanged() is safe to call (does not throw) from a standard thread.
            // CommandManager.InvalidateRequerySuggested is a static WPF call and is not directly verifiable here.
            var command = new RelayCommand<string>(_ => { });

            // Act
            var exception = Record.Exception(() => command.RaiseCanExecuteChanged());

            // Assert
            Assert.Null(exception);
        }

        #endregion
    }
}
