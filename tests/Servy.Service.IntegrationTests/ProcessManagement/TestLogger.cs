using Servy.Core.Logging;
using System;
using System.Collections.Generic;

namespace Servy.Service.IntegrationTests.ProcessManagement
{
    public class TestLogger : IServyLogger
    {
        public List<string> Infos { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        public string? Prefix => string.Empty;

        public void Info(string message, Exception? ex = null) => Infos.Add(message);
        public void Warn(string message, Exception? ex = null) => Warnings.Add(message);
        public void Error(string message, Exception? ex = null) => Errors.Add(message);
        public void Debug(string message, Exception? ex = null) { }

        public IServyLogger CreateScoped(string prefix) => throw new NotImplementedException();
        public void SetLogLevel(LogLevel level) { }
        public void SetIsEventLogEnabled(bool isEnabled) { }
        public void Dispose() { }
    }
}
