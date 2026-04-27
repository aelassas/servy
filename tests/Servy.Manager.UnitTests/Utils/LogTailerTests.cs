using Servy.Manager.Models;
using Servy.Manager.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
            try
            {
                if (File.Exists(_tempFilePath))
                    File.Delete(_tempFilePath);
            }
            catch
            {
                // Ignore exceptions during cleanup, especially if the file is still in use by a running test.
            }
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
            Assert.Equal(3, result?.Lines.Count);
            Assert.Equal("L3", result?.Lines[0].Text);
            Assert.Equal("L5", result?.Lines[2].Text);
        }

#if DEBUG
        [Fact]
        public async Task RunFromPosition_ShouldHandleFileRotation()
        {
            // Arrange
            var tailer = new LogTailer();
            string initialPath = _tempFilePath;
            File.WriteAllText(initialPath, "Old content that should be ignored after rotation\n");
            var fileInfo = new FileInfo(initialPath);

            var capturedLines = new List<LogLine>();
            tailer.OnNewLines += (lines) =>
            {
                lock (capturedLines) capturedLines.AddRange(lines);
            };

            using (var cts = new CancellationTokenSource())
            {
                // Act
                // Start tailing from the end of the "Old content"
                var tailTask = tailer.RunFromPosition(initialPath, LogType.StdOut, fileInfo.Length, fileInfo.CreationTimeUtc, cts.Token);

                // DETERMINISTIC WAIT 1: Ensure the loop has started (Compatible with .NET 4.8)
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                var completedTask = await Task.WhenAny(tailer.LoopStartedSignal.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("The LogTailer background loop failed to start within 5 seconds.");
                }

                // Simulate Rotation: Truncate and write fresh content
                using (var fs = new FileStream(initialPath, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite))
                using (var sw = new StreamWriter(fs) { AutoFlush = true })
                {
                    await sw.WriteLineAsync("ROTATED_CONTENT");
                }

                // DETERMINISTIC WAIT 2: Poll for the content reaching capturedLines
                await WaitUntilAsync(() =>
                {
                    lock (capturedLines)
                    {
                        return capturedLines.Exists(l => l.Text.Contains("ROTATED_CONTENT"));
                    }
                }, TimeSpan.FromSeconds(10));

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
#endif

        /// <summary>
        /// Polls a predicate until it returns true or the timeout is reached.
        /// </summary>
        private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (predicate()) return;
                await Task.Delay(50);
            }
            throw new TimeoutException($"Test timed out waiting for condition to be met after {timeout.TotalSeconds}s.");
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
            Assert.Equal(2, result?.Lines.Count);
            // Verify chronologically ordered
            Assert.True(result?.Lines[0].Timestamp < result?.Lines[1].Timestamp);
        }
    }
}