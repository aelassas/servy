#pragma warning disable SA1310 // Field names should not contain underscore

namespace Servy.Service.Native
{
    /// <summary>
    /// Defines common Windows error codes used by service control operations.
    /// </summary>
    internal static class Errors
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        internal const int ERROR_SUCCESS = 0;

        /// <summary>
        /// Access is denied. The operation requires higher privileges or permissions.
        /// </summary>
        internal const int ERROR_ACCESS_DENIED = 5;

        /// <summary>
        /// The handle is invalid or no longer valid for the requested operation.
        /// </summary>
        internal const int ERROR_INVALID_HANDLE = 6;

        /// <summary>
        /// One or more parameters are invalid.
        /// </summary>
        internal const int ERROR_INVALID_PARAMETER = 7;

        /// <summary>
        /// The service is already running.
        /// </summary>
        internal const int ERROR_SERVICE_ALREADY_RUNNING = 1056;

        /// <summary>
        /// The specified service does not exist as an installed service.
        /// </summary>
        internal const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;

        /// <summary>
        /// The service has not been started.
        /// </summary>
        internal const int ERROR_SERVICE_NOT_ACTIVE = 1062;

        /// <summary>
        /// The specified service is marked for deletion and cannot be started or modified.
        /// </summary>
        internal const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;

        /// <summary>
        /// The specified service already exists.
        /// </summary>
        internal const int ERROR_SERVICE_EXISTS = 1073;

        /// <summary>
        /// The operation was canceled by the user or another process.
        /// </summary>
        internal const int ERROR_CANCELLED = 1223;
    }
}
