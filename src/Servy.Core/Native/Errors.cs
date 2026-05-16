#pragma warning disable SA1310 // Field names should not contain underscore

namespace Servy.Core.Native
{
    /// <summary>
    /// Defines common Windows error codes used by service control operations.
    /// </summary>
    public static class Errors
    {
        /// <summary>
        /// The handle is invalid or no longer valid for the requested operation.
        /// </summary>
        public const int ERROR_INVALID_HANDLE = 6;

        /// <summary>
        /// One or more parameters are invalid.
        /// </summary>
        public const int ERROR_INVALID_PARAMETER = 87;

        /// <summary>
        /// The Windows API native error code indicating that an attempt was made to move a file or directory 
        /// across different storage volumes or device boundaries.
        /// </summary>
        /// <remarks>
        /// This constant maps to the standard system error <c>ERROR_NOT_SAME_DEVICE</c> (decimal 17). It typically occurs 
        /// when invoking native filesystem functions like <c>MoveFileEx</c> or <c>ReplaceFile</c> without specifying 
        /// a cross-volume copy-and-delete fallback flag (such as <c>MOVEFILE_COPY_ALLOWED</c>) when processing paths 
        /// that cross logical drive boundaries (e.g., from <c>C:</c> to <c>D:</c>).
        /// </remarks>
        public const int ERROR_NOT_SAME_DEVICE = 0x11; // 17
    }
}
