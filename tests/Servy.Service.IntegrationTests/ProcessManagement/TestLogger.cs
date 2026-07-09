using Servy.Core.Logging;

namespace Servy.Service.IntegrationTests.ProcessManagement
{
    public class TestLogger : IServyLogger
    {
        public List<string> Infos { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        public string? Prefix => string.Empty;

        public void Info(string message, Exception? ex = null) => Infos.Add(Format(message, ex));
        public void Warn(string message, Exception? ex = null) => Warnings.Add(Format(message, ex));
        public void Error(string message, Exception? ex = null) => Errors.Add(Format(message, ex));
        public void Debug(string message, Exception? ex = null) { }

        public IServyLogger CreateScoped(string? prefix) => this;
        public void SetLogLevel(LogLevel level) { }
        public void SetIsEventLogEnabled(bool isEnabled) { }
        public void Dispose() { }

        #region Helper Methods

        /// <summary>
        /// Standardizes structural text formatting across all internal test entry lists.
        /// </summary>
        private static string Format(string message, Exception? ex)
        {
            if (ex == null) return message;
            return $"{message} | Exception: {ex.Message}";
        }

        #endregion
    }
}