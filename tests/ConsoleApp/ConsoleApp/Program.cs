using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ConsoleApp
{
    internal class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output.txt");
        private static readonly ManualResetEvent QuitEvent = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            // Ensure this app has its own console
            AllocConsole();

            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("Ctrl+C detected. Exiting...");
                File.AppendAllText(LogFile, $"[{DateTime.Now}] Ctrl+C received.{Environment.NewLine}");
                e.Cancel = true;
                QuitEvent.Set();
            };

            File.AppendAllText(LogFile, $"[{DateTime.Now}] ConsoleApp started (PID: {Process.GetCurrentProcess().Id}).{Environment.NewLine}");
            Console.WriteLine("Launching Python script in a new console...");

            try
            {
                //var psi = new ProcessStartInfo
                //{
                //    FileName = "cmd.exe",
                //    Arguments = "/c start \"PythonScript\" " +
                //        "\"C:\\Users\\aelassas\\AppData\\Local\\Programs\\Python\\Python313\\python.exe\" " +
                //        "\"-u\" \"E:\\dev\\servy\\src\\tests\\ctrlc.py\"",
                //    UseShellExecute = true,
                //    CreateNoWindow = false,
                //    WindowStyle = ProcessWindowStyle.Normal
                //};

                Process.Start(new ProcessStartInfo
                {
                    FileName = @"C:\Windows\System32\notepad.exe",
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Normal
                });

                var psi = new ProcessStartInfo
                {
                    FileName = "C:\\Users\\aelassas\\AppData\\Local\\Programs\\Python\\Python313\\python.exe",
                    Arguments = 
                    "\"-u\" \"E:\\dev\\servy\\src\\tests\\ctrlc.py\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };


                Process.Start(psi);

                File.AppendAllText(LogFile, $"[{DateTime.Now}] Started Python script in new console.{Environment.NewLine}");

   
            }
            catch (Exception ex)
            {
                File.AppendAllText(LogFile, $"[{DateTime.Now}] Error launching Python: {ex.Message}{Environment.NewLine}");
            }

            Console.WriteLine("Press Ctrl+C to exit...");
            QuitEvent.WaitOne();

            File.AppendAllText(LogFile, $"[{DateTime.Now}] ConsoleApp exiting.{Environment.NewLine}");
        }
    }

}
