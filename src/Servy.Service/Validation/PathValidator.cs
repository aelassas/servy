using Servy.Core.Helpers;

namespace Servy.Service.Validation
{
    /// <summary>
    /// Provides an implementation of <see cref="IPathValidator"/> that uses <see cref="Helper"/> for path validation.
    /// </summary>
    public class PathValidator : IPathValidator
    {
        /// <inheritdoc />
        public bool IsValidPath(string path) => Helper.IsValidPath(path);
    }
}
