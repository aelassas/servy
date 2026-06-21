using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Resources;

namespace Servy.Core.Validation
{
    /// <summary>
    /// Provides shared validation logic for importing and exporting configuration files.
    /// </summary>
    public static class ImportGuard
    {
        /// <summary>
        /// Enforces defense-in-depth security guards against path traversal, UNC bypasses, and unauthorized system reads.
        /// Validates that a configuration file exists, is secure, and stays within a safe size threshold, and returns a structured validation result.
        /// </summary>
        /// <param name="path">The file path to validate.</param>
        /// <param name="fileContent">On success, outputs the full text content read from the validated file; otherwise, null.</param>
        /// <returns>A strongly-typed result containing the secure path token on success, or a rejection reason on failure.</returns>
        public static PathSecurityResult ValidatePathSecurityAndSize(string path, out string? fileContent)
        {
            fileContent = null;

            // Invoke the shared security gate using read intent semantics
            var securityCheck = PathSecurityGuard.ValidatePath(path, FileMode.Open, FileAccess.Read, FileShare.Read, out var fileStream);
            if (!securityCheck.IsValid || fileStream == null)
            {
                return securityCheck;
            }

            using (fileStream)
            {
                if (fileStream.Length > AppConfig.MaxConfigFileSizeBytes)
                {
                    var errorMsg = string.Format(Strings.Msg_ConfigSizeLimitReached, securityCheck.ValidPath!.ResolvedPath);
                    Logger.Error(errorMsg);
                    return PathSecurityResult.Fail(PathSecurityFailureKind.InvalidArgument, errorMsg);
                }

                // Success: Set content output and return validated path token
                using (var sr = new StreamReader(fileStream))
                {
                    fileContent = sr.ReadToEnd();
                }
                return securityCheck;
            }
        }
    }
}