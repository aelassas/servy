using System;
using System.Windows.Input;
using Xunit;
using Servy.UI.Commands;

namespace Servy.UI.UnitTests.Commands
{
    public class RelayCommandTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_NullExecute_ThrowsArgumentNullException()
        {
            // Branch: execute ?? throw new ArgumentNullException
            Assert.Throws<ArgumentNullException>(() => new RelayCommand<string>(null!));
        }

        #endregion

        #region CanExecute Tests

        [Fact]
        public void CanExecute_NoPredicate_ReturnsTrue()
        {
            // Branch: if (_canExecute == null) return true;
            var command = new RelayCommand<string>(_ => { });
            Assert.True(command.CanExecute("test"));
        }

        [Theory]
        [InlineData("valid", true)]
        [InlineData("invalid", false)]
        public void CanExecute_WithPredicate_ReturnsPredicateResult(string input, bool expected)
        {
            // Branch: parameter is T typed ? typed : default(T) (Matching Type)
            var command = new RelayCommand<string>(_ => { }, p => p == "valid");
            Assert.Equal(expected, command.CanExecute(input));
        }

        [Fact]
        public void CanExecute_MismatchingType_PassesDefaultTToPredicate()
        {
            // Branch: parameter is T typed ? typed : default(T) (Mismatching Type)
            // We use int to verify that default(int) which is 0 is passed to the predicate
            bool receivedZero = false;
            var command = new RelayCommand<int>(_ => { }, p =>
            {
                if (p == 0) receivedZero = true;
                return true;
            });

            // Pass a string to a Command expecting an int
            command.CanExecute("not an int");

            Assert.True(receivedZero);
        }

        #endregion

        #region Execute Tests

        [Fact]
        public void Execute_ValidType_InvokesActionWithParameter()
        {
            // Branch: parameter is T typed ? typed : default(T) (Matching Type)
            string? receivedValue = null;
            var command = new RelayCommand<string>(p => receivedValue = p);

            command.Execute("hello");

            Assert.Equal("hello", receivedValue);
        }

        [Fact]
        public void Execute_NullOrMismatchingType_InvokesActionWithDefaultT()
        {
            // Branch: parameter is T typed ? typed : default(T) (Mismatching Type/Null)
            int receivedValue = -1;
            var command = new RelayCommand<int>(p => receivedValue = p);

            // Passing null to an int command should result in default(int) = 0
            command.Execute(null);

            Assert.Equal(0, receivedValue);
        }

        #endregion

        #region Event and Manager Tests

        [Fact]
        public void RaiseCanExecuteChanged_DoesNotThrow()
        {
            // Verifies the call to CommandManager.InvalidateRequerySuggested
            var command = new RelayCommand<string>(_ => { });
            var exception = Record.Exception(() => command.RaiseCanExecuteChanged());
            Assert.Null(exception);
        }

        #endregion
    }
}