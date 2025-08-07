namespace Servy.Service.Validation
{
    /// <summary>
    /// Defines a contract for validating file system paths.
    /// </summary>
    public interface IPathValidator
    {
        /// <summary>
        /// Determines whether the specified path is valid.
        /// </summary>
        /// <param name="path">The file system path to validate.</param>
        /// <returns><c>true</c> if the path is valid; otherwise, <c>false</c>.</returns>
        bool IsValidPath(string path);
    }
}
