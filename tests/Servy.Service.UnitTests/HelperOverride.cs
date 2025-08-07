namespace Servy.Service.UnitTests
{
    /// <summary>
    /// Override Helper.IsValidPath for testing
    /// </summary>
    internal static class HelperOverride
    {
        public static Func<string, bool>? IsValidPathOverride;

        public static bool IsValidPath(string path) => IsValidPathOverride?.Invoke(path) ?? true;
    }
}
