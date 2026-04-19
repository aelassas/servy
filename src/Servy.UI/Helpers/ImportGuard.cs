using Servy.Core.Logging;
using Servy.UI.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Servy.UI.Helpers
{
    /// <summary>
    /// Provides shared validation logic for importing configuration files.
    /// </summary>
    public static class ImportGuard
    {
        /// <summary>
        /// Validates that a configuration file exists and stays within a safe size threshold.
        /// </summary>
        /// <param name="path">Path to the file.</param>
        /// <param name="messageBoxService">The UI service to display errors.</param>
        /// <param name="caption">The message box caption.</param>
        /// <param name="maxFileSizeMb">The maximum allowed size in MB.</param>
        /// <param name="sizeLimitFormat">The localized format string for the size error (expects {0} for path).</param>
        /// <returns>True if the file is valid for import; otherwise false.</returns>
        public static async Task<bool> ValidateFileSizeAsync(
            string path,
            IMessageBoxService messageBoxService,
            string caption,
            int maxFileSizeMb,
            string sizeLimitFormat)
        {
            string fullPath;
            try
            {
                // Canonicalize path and validate characters
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                Logger.Error($"Invalid path provided for file size validation: {path}", ex);
                return false;
            }

            var fileInfo = new FileInfo(fullPath);

            // 1. Existence Check
            if (!fileInfo.Exists)
            {
                var errorMsg = $"[Import] File not found: {fullPath}";
                Logger.Error(errorMsg);
                await messageBoxService.ShowErrorAsync(errorMsg, caption);
                return false;
            }

            // 2. Size Guard (Safety threshold)
            if (fileInfo.Length > (long)maxFileSizeMb * 1024 * 1024)
            {
                var errorMsg = string.Format(sizeLimitFormat, fullPath);
                Logger.Error(errorMsg);
                await messageBoxService.ShowErrorAsync(errorMsg, caption);
                return false;
            }

            return true;
        }
    }
}