using CtrlCApp.Native;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static CtrlCApp.Native.NativeMethods;

_ = NativeMethods.FreeConsole();
_ = NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);

_ = NativeMethods.AllocConsole(); // ensure we have a console
_ = NativeMethods.SetConsoleCtrlHandler(null, false);
_ = NativeMethods.SetConsoleOutputCP(NativeMethods.CP_UTF8);

var realExePath = @"C:\Users\aelassas\AppData\Local\Programs\Python\Python313\python.exe";
var realArgs = @"E:\dev\servy\src\tests\ctrlc.py";
var workingDir = @"E:\dev\servy\src\tests\";
const string log = @"E:\dev\servy\python_ctrlc.txt";

Directory.CreateDirectory(Path.GetDirectoryName(log)!);

var psi = new ProcessStartInfo
{
    FileName = realExePath,
    Arguments = realArgs,
    WorkingDirectory = workingDir,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    StandardOutputEncoding = Encoding.UTF8,
    StandardErrorEncoding = Encoding.UTF8,
    CreateNoWindow = true, 
};

EnsurePythonUTF8EncodingAndBufferedMode(psi);

var _childProcess = new Process { StartInfo = psi };
_childProcess.EnableRaisingEvents = true;

// Helper to safely log asynchronously from event handlers
async Task LogAsync(string message) => await File.AppendAllTextAsync(log, message + "\n");

_childProcess.OutputDataReceived += (sender, e) =>
{
    if (e.Data != null)
        _ = LogAsync($"stdout: {e.Data}");
};

_childProcess.ErrorDataReceived += (sender, e) =>
{
    if (e.Data != null)
        _ = LogAsync($"stderr: {e.Data}");
};

_childProcess.Exited += (sender, e) =>
{
    _ = LogAsync($"child process exited with code: {_childProcess.ExitCode}");
};

try
{
    _childProcess.Start();

    await LogAsync($"Started child process with PID: {_childProcess.Id}");
}
finally
{
    _ = NativeMethods.FreeConsole();
    _ = NativeMethods.SetConsoleCtrlHandler(null, true);
}

_childProcess.BeginOutputReadLine();
_childProcess.BeginErrorReadLine();

// Wait a bit for the child to initialize
await Task.Delay(2000);

// Send Ctrl+C
await SendCtrlCAsync(_childProcess);

await _childProcess.WaitForExitAsync();
await LogAsync("Parent exiting...");

static void EnsurePythonUTF8EncodingAndBufferedMode(ProcessStartInfo psi)
{
    if (psi.FileName.Contains("python", StringComparison.OrdinalIgnoreCase) ||
        psi.Arguments.Contains(".py", StringComparison.OrdinalIgnoreCase))
    {
        psi.Environment["PYTHONLEGACYWINDOWSSTDIO"] = "0";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONUNBUFFERED"] = "1";
    }
}

static async Task<bool?> SendCtrlCAsync(Process process)
{
    if (!AttachConsole(process.Id))
    {
        int error = Marshal.GetLastWin32Error();
        switch (error)
        {
            case Errors.ERROR_INVALID_HANDLE:
                await File.AppendAllTextAsync(log, $"Sending Ctrl+C: The child process does not have a console.\n");
                return false;

            case Errors.ERROR_INVALID_PARAMETER:
                return null;

            default:
                await File.AppendAllTextAsync(log, $"Sending Ctrl+C: Failed to attach the child process to console: {new Win32Exception(error).Message}\n");
                return false;
        }
    }

    _ = GenerateConsoleCtrlEvent(CtrlEvents.CTRL_C_EVENT, 0);    
    await File.AppendAllTextAsync(log, $"Sent Ctrl+C to process.\n");

    _ = FreeConsole();

    return true;
}
