#pragma warning disable SA1310 // Field names should not contain underscore

namespace Servy.Service.Native
{
    /// <summary>
    /// Defines common Windows error codes used by service control operations.
    /// </summary>
    internal static class Errors
    {
        /// <summary>
        /// The handle is invalid or no longer valid for the requested operation.
        /// </summary>
        internal const int ERROR_INVALID_HANDLE = 6;

        /// <summary>
        /// One or more parameters are invalid.
        /// </summary>
        internal const int ERROR_INVALID_PARAMETER = 7;
    }
}
