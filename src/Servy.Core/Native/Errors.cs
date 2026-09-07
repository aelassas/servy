#pragma warning disable SA1310 // Field names should not contain underscore

namespace Servy.Core.Native
{
    /// <summary>
    /// Defines common Windows error codes used by service control operations.
    /// </summary>
    public static class Errors
    {
        /// <summary>Access is denied.</summary>
        public const int ERROR_ACCESS_DENIED = 5;

        /// <summary>The handle is invalid or no longer valid for the requested operation.</summary>
        public const int ERROR_INVALID_HANDLE = 6;

        /// <summary>An attempt was made to move a file to a different device (ERROR_NOT_SAME_DEVICE).</summary>
        public const int ERROR_NOT_SAME_DEVICE = 0x11; // 17

        /// <summary>Represents the Win32 error code indicating that a data block or process table length changed between internal allocation queries (commonly thrown transiently by Toolhelp32 APIs).</summary>
        public const int ERROR_BAD_LENGTH = 24;

        /// <summary>A general device or pipe failure.</summary>
        public const int ERROR_GEN_FAILURE = 31;

        /// <summary>One or more parameters are invalid.</summary>
        public const int ERROR_INVALID_PARAMETER = 87;

        /// <summary>Represents the Win32 error code indicating the provided buffer is too small to contain the data.</summary>
        public const int ERROR_INSUFFICIENT_BUFFER = 122;

        /// <summary>The pipe is not connected (no console attached to the target process).</summary>
        public const int ERROR_PIPE_NOT_CONNECTED = 233;

        /// <summary>An instance of the service is already running.</summary>
        public const int ERROR_SERVICE_ALREADY_RUNNING = 1056;

        /// <summary>The service cannot be started, either because it is disabled or because it has no enabled devices associated with it.</summary>
        public const int ERROR_SERVICE_DISABLED = 1058;

        /// <summary>The specified service does not exist as an installed service.</summary>
        public const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;

        /// <summary>The service cannot accept control messages at this time (e.g., while in a pending transition state).</summary>
        public const int ERROR_SERVICE_CANNOT_ACCEPT_CTRL = 1061;

        /// <summary>The specified service has not been started.</summary>
        public const int ERROR_SERVICE_NOT_ACTIVE = 1062;

        /// <summary>The service did not start due to a logon failure.</summary>
        public const int ERROR_SERVICE_LOGON_FAILED = 1069;

        /// <summary>The specified service has been marked for deletion.</summary>
        public const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;

        /// <summary>The user name or password is incorrect (LogonUserW failure).</summary>
        public const int ERROR_LOGON_FAILURE = 1326;

        /// <summary>Account restrictions prevent logon (e.g., blank passwords disallowed).</summary>
        public const int ERROR_ACCOUNT_RESTRICTION = 1327;

        /// <summary>The user has not been granted the requested logon type at this computer.</summary>
        public const int ERROR_LOGON_TYPE_NOT_GRANTED = 1385;
    }
}
