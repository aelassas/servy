namespace Servy.Core.Common
{
    /// <summary>
    /// Represents the result of an operation, providing a unified way to signal success 
    /// or failure with mandatory error context on failure.
    /// </summary>
    public class OperationResult
    {
        /// <summary>
        /// Gets a value indicating whether the operation completed successfully.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the error message associated with a failed operation, or <c>null</c> if successful.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OperationResult"/> class.
        /// </summary>
        /// <param name="isSuccess">Whether the operation succeeded.</param>
        /// <param name="errorMessage">The reason for failure, if applicable.</param>
        protected OperationResult(bool isSuccess, string? errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Returns an <see cref="OperationResult"/> representing a successful operation.
        /// </summary>
        /// <returns>A successful <see cref="OperationResult"/>.</returns>
        public static OperationResult Success() => new OperationResult(true, null);

        /// <summary>
        /// Returns an <see cref="OperationResult"/> representing a failed operation.
        /// </summary>
        /// <param name="error">The mandatory error message describing why the operation failed.</param>
        /// <returns>A failed <see cref="OperationResult"/> containing the error context.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="error"/> is null.</exception>
        public static OperationResult Failure(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("Failure result must include an error message.", nameof(error));

            return new OperationResult(false, error);
        }
    }
}