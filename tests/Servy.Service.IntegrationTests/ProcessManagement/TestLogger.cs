using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Servy.Service.IntegrationTests.ProcessManagement
{
    public class TestLogger : IServyLogger
    {
        private readonly object _lockGate = new object();
        private readonly List<string> _infos = new List<string>();
        private readonly List<string> _warnings = new List<string>();
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _debugs = new List<string>();

        // Return snapshot lists under lock protection to make reads isolated and thread-safe
        public IReadOnlyList<string> Infos { get { lock (_lockGate) return _infos.ToList(); } }
        public IReadOnlyList<string> Warnings { get { lock (_lockGate) return _warnings.ToList(); } }
        public IReadOnlyList<string> Errors { get { lock (_lockGate) return _errors.ToList(); } }
        public IReadOnlyList<string> Debugs { get { lock (_lockGate) return _debugs.ToList(); } }

        public string Prefix => string.Empty;

        public void Info(string message, Exception ex = null)
        {
            lock (_lockGate)
            {
                _infos.Add(Format(message, ex));
            }
        }

        public void Warn(string message, Exception ex = null)
        {
            lock (_lockGate)
            {
                _warnings.Add(Format(message, ex));
            }
        }

        public void Error(string message, Exception ex = null)
        {
            lock (_lockGate)
            {
                _errors.Add(Format(message, ex));
            }
        }

        public void Debug(string message, Exception ex = null)
        {
            lock (_lockGate)
            {
                _debugs.Add(message);
            }
        }

        public IServyLogger CreateScoped(string prefix) => this;
        public void SetLogLevel(LogLevel level) { }
        public void SetIsEventLogEnabled(bool isEnabled) { }
        public void Dispose() { }

        #region Helper Methods

        /// <summary>
        /// Standardizes structural text formatting across all internal test entry lists.
        /// </summary>
        private static string Format(string message, Exception ex)
        {
            if (ex == null) return message;
            return $"{message} | Exception: {ex.Message}";
        }

        #endregion
    }
}