namespace Servy.Core.Native
{
    /// <summary>Provides a unique identifier for a file on a specific volume.</summary>
    /// <remarks>
    /// Declared outside <see cref="NativeMethods"/> on purpose. That class carries a
    /// class-level <c>[ExcludeFromCodeCoverage]</c> for its P/Invoke surface, and the attribute
    /// reaches nested types, so while this struct lived there the only method body in the file
    /// was absent from the coverage report and the tests driving it counted for nothing.
    /// </remarks>
    public struct FILE_IDENTITY
    {
        /// <summary>The unique file index.</summary>
        public ulong FileIndex;

        /// <summary>The volume serial number.</summary>
        public uint VolumeSerialNumber;

        /// <summary>A digest of the start of the file for secondary identification.</summary>
        public string PrefixDigest;

        /// <summary>Indicates if handle-based information was successfully retrieved.</summary>
        public bool IsValidHandleInfo;

        /// <summary>
        /// Compares the current file identity against another to determine if the underlying
        /// file object on disk has been replaced (rotated) or truncated.
        /// </summary>
        /// <param name="other">The previously known file identity to compare against.</param>
        /// <returns>
        /// <c>true</c> if the files are proven different or if identity is undeterminable;
        /// <c>false</c> only if they are proven to be the same file object.
        /// </returns>
        public bool IsDifferentFrom(FILE_IDENTITY other)
        {
            // If one probe succeeded and the other failed, they are fundamentally different states.
            if (IsValidHandleInfo != other.IsValidHandleInfo) return true;

            // 1. Primary Probe: Win32 File Index and Volume Serial Number (Most reliable)
            if (IsValidHandleInfo)   // other.IsValidHandleInfo is guaranteed equal here
            {
                if (FileIndex != other.FileIndex || VolumeSerialNumber != other.VolumeSerialNumber)
                    return true;
                return false;
            }

            // 2. Secondary Probe: Content Prefix Digest (Used when handle info is unavailable, e.g., FAT32)
            if (PrefixDigest != null && other.PrefixDigest != null)
            {
                // If content digests differ, the file has definitely changed.
                // If both are empty strings (empty files), we treat them as same content-wise.
                return PrefixDigest != other.PrefixDigest;
            }

            // 3. Fallback: Identity Undeterminable
            // If we reach this point, both robust probes failed or yielded null data (e.g., due to
            // exclusive file locks, antivirus interference, or I/O errors).
            //
            // SAFE DEFAULT: We return 'true' to signal a potential difference. This forces the
            // caller (like LogTailer) to break its inner loop and perform a "soft refresh" via
            // metadata-guarded re-opening. This prevents masking rotations on hostile file
            // systems where tunneling hides metadata changes.
            return true;
        }
    }
}
