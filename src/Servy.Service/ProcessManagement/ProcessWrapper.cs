using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Servy.Service
{
    public class ProcessWrapper : IProcessWrapper
    {
        private readonly Process _process;

        public ProcessWrapper(ProcessStartInfo psi)
        {
            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        }

        public event DataReceivedEventHandler OutputDataReceived
        {
            add { _process.OutputDataReceived += value; }
            remove { _process.OutputDataReceived -= value; }
        }

        public event DataReceivedEventHandler ErrorDataReceived
        {
            add { _process.ErrorDataReceived += value; }
            remove { _process.ErrorDataReceived -= value; }
        }

        public event EventHandler Exited
        {
            add { _process.Exited += value; }
            remove { _process.Exited -= value; }
        }

        public int Id => _process.Id;
        public bool HasExited => _process.HasExited;
        public IntPtr Handle => _process.Handle;
        public int ExitCode => _process.ExitCode;
        public IntPtr MainWindowHandle => _process.MainWindowHandle;

        public bool EnableRaisingEvents
        {
            get => _process.EnableRaisingEvents;
            set => _process.EnableRaisingEvents = value;
        }

        public ProcessPriorityClass PriorityClass
        {
            get => _process.PriorityClass;
            set => _process.PriorityClass = value;
        }

        public void Start() => _process.Start();
        public void Kill() => _process.Kill();
        public bool WaitForExit(int milliseconds) => _process.WaitForExit(milliseconds);
        public void WaitForExit() => _process.WaitForExit();
        public bool CloseMainWindow() => _process.CloseMainWindow();
        public void BeginOutputReadLine() => _process.BeginOutputReadLine();
        public void BeginErrorReadLine() => _process.BeginErrorReadLine();

        public void Dispose() => _process.Dispose();
    }

}
