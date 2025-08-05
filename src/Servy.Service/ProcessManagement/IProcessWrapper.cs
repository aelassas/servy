using System;
using System.Diagnostics;

namespace Servy.Service
{
    public interface IProcessWrapper : IDisposable
    {
        event DataReceivedEventHandler OutputDataReceived;
        event DataReceivedEventHandler ErrorDataReceived;
        event EventHandler Exited;

        int Id { get; }
        bool HasExited { get; }
        IntPtr Handle { get; }
        int ExitCode { get; }
        bool EnableRaisingEvents { get; set; }

        void Start();
        void Kill();
        bool WaitForExit(int milliseconds);
        void WaitForExit(); // Waits indefinitely for the associated process to exit.
        bool CloseMainWindow();
        IntPtr MainWindowHandle { get; }
        ProcessPriorityClass PriorityClass { get; set; }
        void BeginOutputReadLine();
        void BeginErrorReadLine();
    }

}
