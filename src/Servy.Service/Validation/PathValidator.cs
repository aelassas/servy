using Servy.Core;

namespace Servy.Service
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
