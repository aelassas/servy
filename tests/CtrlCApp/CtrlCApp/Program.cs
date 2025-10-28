using CtrlCApp.Native;
using System;
using System.ComponentModel;
using System.Diagnostics;
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
    CreateNoWindow = true, // must be false to allow Ctrl+C
    WindowStyle = ProcessWindowStyle.Hidden,
};

EnsurePythonUTF8EncodingAndBufferedMode(psi);

var _childProcess = new Process { StartInfo = psi };
_childProcess.EnableRaisingEvents = true;

_childProcess.OutputDataReceived += (sender, e) =>
{
    if (e.Data != null) File.AppendAllText(log, $"stdout: {e.Data}\n");
};

_childProcess.ErrorDataReceived += (sender, e) =>
{
    if (e.Data != null) File.AppendAllText(log, $"stderr: {e.Data}\n");
};

_childProcess.Exited += (sender, e) =>
{
    File.AppendAllText(log, $"child process exited with code: {_childProcess.ExitCode}\n");
};

try
{
    _childProcess.Start();

    File.AppendAllText(log, $"Started child process with PID: {_childProcess.Id}\n");
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
SendCtrlC(_childProcess);

await _childProcess.WaitForExitAsync();
File.AppendAllText(log, "Parent exiting...\n");

static void EnsurePythonUTF8EncodingAndBufferedMode(ProcessStartInfo psi)
{
    if (psi.FileName.Contains("python", StringComparison.OrdinalIgnoreCase) ||
        psi.Arguments.Contains(".py", StringComparison.OrdinalIgnoreCase))
    {
        psi.EnvironmentVariables["PYTHONLEGACYWINDOWSSTDIO"] = "0";
        psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        psi.EnvironmentVariables["PYTHONUTF8"] = "1";
        psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
    }
}

static bool? SendCtrlC(Process process)
{
    if (!AttachConsole(process.Id))
    {
        int error = Marshal.GetLastWin32Error();
        switch (error)
        {
            // The process does not have a console.
            case Errors.ERROR_INVALID_HANDLE:
                File.AppendAllText(log, $"Sending Ctrl+C: The child process does not have a console.\n");
                return false;

            // The process has exited.
            case Errors.ERROR_INVALID_PARAMETER:
                return null;

            // The calling process is already attached to a console.
            default:
                File.AppendAllText(log, $"Sending Ctrl+C: Failed to attach the child process to console: {new Win32Exception(error).Message}\n");
                return false;
        }
    }

    // Don't call GenerateConsoleCtrlEvent immediately after SetConsoleCtrlHandler.
    // A delay was observed as of Windows 10, version 2004 and Windows Server 2019.
    _ = GenerateConsoleCtrlEvent(CtrlEvents.CTRL_C_EVENT, 0);
    File.AppendAllText(log, $"Sent Ctrl+C to process.\n");

    _ = FreeConsole();

    return true;
}
