#pragma warning disable SA1310 // Field names should not contain underscore

namespace Servy.Core.Native
{
    /// <summary>
    /// Defines common Windows error codes used by service control operations.
    /// </summary>
    public static class Errors
    {
        /// <summary>The handle is invalid or no longer valid for the requested operation.</summary>
        public const int ERROR_INVALID_HANDLE = 6;

        /// <summary>One or more parameters are invalid.</summary>
        public const int ERROR_INVALID_PARAMETER = 87;

        /// <summary>An attempt was made to move a file to a different device (ERROR_NOT_SAME_DEVICE).</summary>
        public const int ERROR_NOT_SAME_DEVICE = 0x11; // 17

        /// <summary>The pipe is not connected (no console attached to the target process).</summary>
        public const int ERROR_PIPE_NOT_CONNECTED = 233;

        /// <summary>A general device or pipe failure.</summary>
        public const int ERROR_GEN_FAILURE = 31;

        /// <summary>The user name or password is incorrect (LogonUserW failure).</summary>
        public const int ERROR_LOGON_FAILURE = 1326;

        /// <summary>Account restrictions prevent logon (e.g., blank passwords disallowed).</summary>
        public const int ERROR_ACCOUNT_RESTRICTION = 1327;

        /// <summary>The user has not been granted the requested logon type at this computer.</summary>
        public const int ERROR_LOGON_TYPE_NOT_GRANTED = 1385;

        /// <summary>Represents the Win32 error code indicating the provided buffer is too small to contain the data.</summary>
        public const int ERROR_INSUFFICIENT_BUFFER = 122;

        /// <summary>Represents the Win32 error code indicating that a data block or process table length changed between internal allocation queries (commonly thrown transiently by Toolhelp32 APIs).</summary>
        public const int ERROR_BAD_LENGTH = 24;

        /// <summary>Access is denied.</summary>
        public const int ERROR_ACCESS_DENIED = 5;

        /// <summary>The specified service does not exist as an installed service.</summary>
        public const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;

        ///<summary>The specified service has not been started.</summary>
        public const int ERROR_SERVICE_NOT_ACTIVE = 1062;
    }
}
