namespace Servy.Service.Helpers
{
    /// <summary>
    /// Detects if an executable requires a console based on its PE header.
    /// Aligned with Servy's architecture where ExecutablePath is a binary (runtime or compiled app).
    /// </summary>
    public static class ConsoleAppDetector
    {
        /// <summary>
        /// Analyzes the executable's headers to determine if it belongs to the Console Subsystem.
        /// </summary>
        /// <param name="path">The path to the executable (e.g., 'python.exe', 'myapp.exe').</param>
        /// <returns>True if it's a console app (CUI); False if it's a GUI app or unknown.</returns>
        public static bool IsConsoleApp(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            var extension = Path.GetExtension(path).ToLowerInvariant();

            switch (extension)
            {
                case ".exe":
                case ".dll":
                    // 1. Direct Check: Works for .NET Framework, C++, Go, Rust, and Runtimes (python.exe, etc.)
                    if (CheckPEHeaderForConsole(path)) return true;

                    // 2. Modern .NET Shim Fix: 
                    // If myapp.exe is a GUI shim, check myapp.dll for the actual Subsystem 3 flag.
                    if (extension == ".exe")
                    {
                        var dllPath = Path.ChangeExtension(path, ".dll");
                        if (File.Exists(dllPath))
                            return CheckPEHeaderForConsole(dllPath);
                    }
                    return false;

                case "":
                    // Extensionless files: Check for Unix-style shebang (#!) or check PE header anyway
                    // (Some compiled binaries like Go/Rust can be extensionless)
                    return IsUnixExecutable(path) || CheckPEHeaderForConsole(path);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Reads the Portable Executable (PE) header.
        /// Handles 32-bit and 64-bit binaries by jumping to the Subsystem field.
        /// </summary>
        private static bool CheckPEHeaderForConsole(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new BinaryReader(fs))
                {
                    // 1. Validate DOS Header ("MZ")
                    if (fs.Length < 64 || reader.ReadUInt16() != 0x5A4D) return false;

                    // 2. Get PE Header Offset
                    fs.Seek(0x3C, SeekOrigin.Begin);
                    uint peOffset = reader.ReadUInt32();
                    if (peOffset == 0 || peOffset > fs.Length - 24) return false;

                    // 3. Validate PE Signature ("PE\0\0")
                    fs.Seek(peOffset, SeekOrigin.Begin);
                    if (reader.ReadUInt32() != 0x00004550) return false;

                    // 4. Move to Optional Header Magic
                    // Signature (4) + COFF Header (20) = 24 bytes
                    fs.Seek(peOffset + 24, SeekOrigin.Begin);
                    ushort magic = reader.ReadUInt16();

                    // 5. Determine Subsystem Offset
                    // PE32 (32-bit) uses Magic 0x10B. PE32+ (64-bit) uses 0x20B.
                    // Subsystem is 68 bytes into the Optional Header for BOTH.
                    fs.Seek(peOffset + 24 + 68, SeekOrigin.Begin);
                    ushort subsystem = reader.ReadUInt16();

                    // 3 = IMAGE_SUBSYSTEM_WINDOWS_CUI (Console)
                    // 2 = IMAGE_SUBSYSTEM_WINDOWS_GUI
                    return subsystem == 3;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks for a shebang (#!) at the start of the file for extensionless scripts.
        /// </summary>
        private static bool IsUnixExecutable(string path)
        {
            try
            {
                using (var reader = new StreamReader(path))
                {
                    var line = reader.ReadLine();
                    return line != null && line.StartsWith("#!");
                }
            }
            catch { return false; }
        }
    }
}