using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Servy.Manager.Models;
using Servy.Manager.Utils;
using Xunit;

namespace Servy.Manager.UnitTests.Utils
{
    public class LogTailerTests : IDisposable
    {
        private readonly string _tempFilePath;

        public LogTailerTests()
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), $"logtailer_test_{Guid.NewGuid()}.log");
        }

        public void Dispose()
        {
            // Note: We avoid deleting here if tests didn't clean up their tasks
            // to prevent the "File in use" error in Dispose.
            try { if (File.Exists(_tempFilePath)) File.Delete(_tempFilePath); } catch { }
        }

        [Fact]
        public async Task GetHistoryAsync_ShouldRetrieveExactlyLastNLines()
        {
            // Arrange
            var tailer = new LogTailer();
            // 5 distinct lines with 4 newlines in between
            var linesToWrite = new[] { "L1", "L2", "L3", "L4", "L5" };
            File.WriteAllLines(_tempFilePath, linesToWrite);

            // Act
            var result = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 3);

            // Assert
            Assert.Equal(3, result.Lines.Count);
            Assert.Equal("L3", result.Lines[0].Text);
            Assert.Equal("L5", result.Lines[2].Text);
        }

        [Fact]
        public async Task RunFromPosition_ShouldHandleFileRotation()
        {
            // Arrange
            var tailer = new LogTailer();
            string initialPath = _tempFilePath;
            File.WriteAllText(initialPath, "Old content that should be ignored after rotation\n");
            var fileInfo = new FileInfo(initialPath);

            var capturedLines = new List<LogLine>();
            tailer.OnNewLines += (lines) => {
                lock (capturedLines) capturedLines.AddRange(lines);
            };

            using (var cts = new CancellationTokenSource())
            {

                // Act
                // Start tailing from the end of the "Old content"
                var tailTask = tailer.RunFromPosition(initialPath, LogType.StdOut, fileInfo.Length, fileInfo.CreationTimeUtc, cts.Token);

                // Wait for the tailer to actually enter its inner loop
                await Task.Delay(300);

                // Simulate Rotation: Truncate and write fresh content
                // We use a specific string "ROTATED_CONTENT" to avoid partial match issues
                using (var fs = new FileStream(initialPath, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite))
                using (var sw = new StreamWriter(fs) { AutoFlush = true })
                {
                    await sw.WriteLineAsync("ROTATED_CONTENT");
                }

                // Give the polling loop (150ms) and ReadLineAsync enough time to process the truncation
                await Task.Delay(1000);

                cts.Cancel();

                try
                {
                    await tailTask;
                }
                catch (OperationCanceledException) { }

                // Assert
                lock (capturedLines)
                {
                    Assert.NotEmpty(capturedLines);
                    Assert.Contains(capturedLines, l => l.Text.Contains("ROTATED_CONTENT"));
                }
            }
        }

        [Fact]
        public async Task GetHistoryAsync_ShouldHandleSyntheticTimestampsCorrecty()
        {
            // Arrange
            var tailer = new LogTailer();
            File.WriteAllLines(_tempFilePath, new[] { "Line1", "Line2" });

            // Act
            var result = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 10);

            // Assert
            Assert.Equal(2, result.Lines.Count);
            // Verify chronologically ordered
            Assert.True(result.Lines[0].Timestamp < result.Lines[1].Timestamp);
        }
    }
}