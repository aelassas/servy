using Servy.UI.Commands;

namespace Servy.UI.UnitTests.Commands
{
    public class RelayCommandTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_NullExecute_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            // Branch: execute ?? throw new ArgumentNullException
            Assert.Throws<ArgumentNullException>(() => new RelayCommand<string>(null!));
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
            // Branch: parameter is T typed ? typed : default(T) (Matching Type)
            var command = new RelayCommand<string>(_ => { }, p => p == "valid");

            // Act
            var result = command.CanExecute(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CanExecute_MismatchingType_PassesDefaultTToPredicate()
        {
            // Arrange
            // Branch: parameter is T typed ? typed : default(T) (Mismatching Type)
            // We use int to verify that default(int) which is 0 is passed to the predicate
            bool receivedZero = false;
            var command = new RelayCommand<int>(_ => { }, p =>
            {
                if (p == 0) receivedZero = true;
                return true;
            });

            // Act
            // Pass a string to a Command expecting an int
            command.CanExecute("not an int");

            // Assert
            Assert.True(receivedZero);
        }

        #endregion

        #region Execute Tests

        [Fact]
        public void Execute_ValidType_InvokesActionWithParameter()
        {
            // Arrange
            // Branch: parameter is T typed ? typed : default(T) (Matching Type)
            string? receivedValue = null;
            var command = new RelayCommand<string>(p => receivedValue = p);

            // Act
            command.Execute("hello");

            // Assert
            Assert.Equal("hello", receivedValue);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not an int")]
        public void Execute_NullOrMismatchingType_InvokesActionWithDefaultT(object? parameter)
        {
            // Arrange
            int receivedValue = -1;
            var command = new RelayCommand<int>(p => receivedValue = p);

            // Act
            command.Execute(parameter);

            // Assert
            Assert.Equal(0, receivedValue);
        }

        #endregion

        #region Event and Manager Tests

        [Fact]
        public void RaiseCanExecuteChanged_DoesNotThrow()
        {
            // Arrange
            // Verifies RaiseCanExecuteChanged() is safe to call (does not throw)
            // from a standard thread. CommandManager.InvalidateRequerySuggested is a
            // static WPF call and is not directly verifiable here.
            var command = new RelayCommand<string>(_ => { });

            // Act
            var exception = Record.Exception(() => command.RaiseCanExecuteChanged());

            // Assert
            Assert.Null(exception);
        }

        #endregion
    }
}