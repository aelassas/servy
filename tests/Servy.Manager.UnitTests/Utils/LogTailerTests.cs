using Servy.Core.Config;
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
            // Best-effort delete; swallow exceptions if a running test still holds the file open
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

        #region Path Validation & History Guard Branch Tests

        [Fact]
        public async Task GetHistoryAsync_NullOrEmptyPath_ReturnsEmptyHistoryImmediately()
        {
            // Arrange
            var tailer = new LogTailer();

            // Act
            var resultNull = await tailer.GetHistoryAsync(null, LogType.StdOut, 10);
            var resultEmpty = await tailer.GetHistoryAsync(string.Empty, LogType.StdOut, 10);

            // Assert
            Assert.NotNull(resultNull);
            Assert.Empty(resultNull.Lines);
            Assert.NotNull(resultEmpty);
            Assert.Empty(resultEmpty.Lines);
        }

        [Fact]
        public async Task GetHistoryAsync_MissingFile_ReturnsEmptyHistoryImmediately()
        {
            // Arrange
            var tailer = new LogTailer();
            string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.log");

            // Act
            var result = await tailer.GetHistoryAsync(missingPath, LogType.StdOut, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Lines);
        }

        [Fact]
        public async Task GetHistoryAsync_EmptyFile_ReturnsEmptyHistoryAndZeroOffset()
        {
            // Arrange
            var tailer = new LogTailer();
            File.WriteAllText(_tempFilePath, string.Empty);

            // Act
            var result = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Lines);
            Assert.Equal(0, result.Position);
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

        [Fact]
        public async Task GetHistoryAsync_ShouldHandleSyntheticTimestampsCorrectly()
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
            Assert.True(result?.Lines[0].IsSyntheticTime);
            Assert.True(result?.Lines[1].IsSyntheticTime);
        }

        [Fact]
        public async Task RunFromPosition_NullOrEmptyPath_ExitsEarlyWithoutLoopAllocation()
        {
            // Arrange
            var tailer = new LogTailer();
            var cts = new CancellationTokenSource();

            // Act
            var taskNull = tailer.RunFromPosition(null, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);
            var taskEmpty = tailer.RunFromPosition(string.Empty, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

            // Assert
            await taskNull;
            await taskEmpty;
            // Early return branches pass cleanly if they do not block or throw exceptions
        }

        #endregion

        #region Exception & Directory/File Race Mitigation Branch Tests

        [Fact]
        public async Task RunFromPosition_MissingDirectoryException_TriggersCatchBlockAndDelays()
        {
            // Arrange
            var tailer = new LogTailer();
            // Create a path inside a directory structure that does not exist to force DirectoryNotFoundException
            string invalidDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "app.log");
            using (var cts = new CancellationTokenSource())
            {
                // Act
                var tailTask = tailer.RunFromPosition(invalidDirectoryPath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                // Let it cycle once into the outer loop try-catch infrastructure block
                await Task.Delay(150, CancellationToken.None);
                cts.Cancel();

                try { await tailTask; } catch (OperationCanceledException) { }
            }
        }

        [Fact]
        public async Task RunFromPosition_FileLockedWithIOException_TriggersIoExceptionCatchBlockAndRetries()
        {
            // Arrange
            var tailer = new LogTailer();
            File.WriteAllText(_tempFilePath, "Initial content\n");

            using (var cts = new CancellationTokenSource())
            {
                // Lock the file exclusively to force an IOException when LogTailer tries to open a new FileStream
                using (var exclusiveLock = new FileStream(_tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                    // Allow the loop to cycle and strike the locked IOException handle branch
                    await Task.Delay(150, CancellationToken.None);
                    cts.Cancel();

                    try { await tailTask; } catch (OperationCanceledException) { }
                }
            }
        }

        [Fact]
        public async Task GetHistoryAsync_FileNotFoundRaceConditionCatch_ReturnsEmptyList()
        {
            // Arrange
            var tailer = new LogTailer();
            // Use a path that is guaranteed not to exist, which is a cleaner way to 
            // force the catch block than racing a File.Delete operation.
            string nonExistentPath = Path.Combine(Path.GetTempPath(), $"race_{Guid.NewGuid()}.log");

            // Act
            // We invoke GetHistoryAsync directly. Since the file does not exist,
            // the LoadHistory method will trigger the FileNotFoundException catch block.
            var result = await tailer.GetHistoryAsync(nonExistentPath, LogType.StdOut, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Lines); // Should return empty result list as defined in the catch block
        }

        #endregion

        #region Tailing Lifecycle & Batch Buffer Flush Branch Tests

        [Fact]
        public async Task RunFromPosition_BatchFlushThresholdReached_InvokesOnNewLinesDuringReadLoop()
        {
            // Arrange
            var tailer = new LogTailer();
            File.WriteAllText(_tempFilePath, "Pre-existing header lines\n");

            var capturedBatches = new List<List<LogLine>>();
            tailer.OnNewLines += (lines) =>
            {
                lock (capturedBatches) capturedBatches.Add(new List<LogLine>(lines));
            };

            // Setup a completion tracking signal task for strict loop synchronization
            var loopCompletedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            tailer.OnLoopCompleted += () => loopCompletedTcs.TrySetResult(true);

            using (var cts = new CancellationTokenSource())
            {
                // Start tailing from the end of pre-existing content
                var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, 26, DateTime.UtcNow, cts.Token);

                // Wait for the background reader loop to fully complete its initial cycle 
                // and position its internal StreamReader handle directly at the EOF boundary.
                await tailer.LoopStartedSignal.Task;
                await loopCompletedTcs.Task;

                // Append enough lines to cross AppConfig.LogTailerBatchFlushThreshold (e.g. 50 or 100 lines)
                using (var fs = new FileStream(_tempFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var sw = new StreamWriter(fs) { AutoFlush = true })
                {
                    for (int i = 0; i < AppConfig.LogTailerBatchFlushThreshold + 5; i++)
                    {
                        await sw.WriteLineAsync($"BatchLine_{i}");
                    }
                }

                // Wait for background batch splitting mechanics to propagate updates
                await WaitUntilAsync(() =>
                {
                    lock (capturedBatches) return capturedBatches.Count >= 2 || (capturedBatches.Count == 1 && capturedBatches[0].Count >= AppConfig.LogTailerBatchFlushThreshold);
                }, TimeSpan.FromSeconds(5));

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }

                lock (capturedBatches)
                {
                    Assert.NotEmpty(capturedBatches);
                }
            }
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
            tailer.OnNewLines += (lines) =>
            {
                lock (capturedLines) capturedLines.AddRange(lines);
            };

            // Setup a completion tracking signal task for strict loop synchronization
            var loopCompletedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            tailer.OnLoopCompleted += () => loopCompletedTcs.TrySetResult(true);

            using (var cts = new CancellationTokenSource())
            {
                // Act
                // Start tailing from the end of the "Old content"
                var tailTask = tailer.RunFromPosition(initialPath, LogType.StdOut, fileInfo.Length, fileInfo.CreationTimeUtc, cts.Token);

                // DETERMINISTIC WAIT 1: Ensure the loop has fully completed its first pass setup
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
                var completedTask = await Task.WhenAny(tailer.LoopStartedSignal.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("The LogTailer background loop failed to start within 5 seconds.");
                }

                // Ensure the loop completes its initial pass tracking before simulating the file swap
                await loopCompletedTcs.Task;

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

        [Fact]
        public async Task RunFromPosition_InitialAttachRotationTrigger_ResetsOffsetToZero()
        {
            // Arrange
            var tailer = new LogTailer();
            // Write payload content
            File.WriteAllText(_tempFilePath, "Line After Truncated Rotation\n");

            var capturedLines = new List<LogLine>();
            tailer.OnNewLines += (lines) => { lock (capturedLines) capturedLines.AddRange(lines); };

            // Setup a completion tracking signal task for strict loop synchronization
            var loopCompletedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            tailer.OnLoopCompleted += () => loopCompletedTcs.TrySetResult(true);

            using (var cts = new CancellationTokenSource())
            {
                // Pass a highly advanced past timestamp or a lastPosition that forces the metadata 
                // check branch (info.Length < lastPosition) to validate initial attach rotation mapping logic
                var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, 999999, DateTime.UtcNow.AddDays(-1), cts.Token);

                // Enforce execution stabilization before running content validations
                await tailer.LoopStartedSignal.Task;
                await loopCompletedTcs.Task;

                await WaitUntilAsync(() => { lock (capturedLines) return capturedLines.Count > 0; }, TimeSpan.FromSeconds(5));
                cts.Cancel();

                try { await tailTask; } catch (OperationCanceledException) { }

                lock (capturedLines)
                {
                    Assert.NotEmpty(capturedLines);
                    Assert.Contains(capturedLines, l => l.Text.Contains("Line After Truncated Rotation"));
                }
            }
        }

        #endregion

        #region Multi-Threaded Early Disposal & Re-entrancy Tests

        [Fact]
        public void Dispose_CalledMultipleTimes_ReturnsSilentlyThroughAtomicGuard()
        {
            // Arrange
            var tailer = new LogTailer();

            // Act & Assert
            tailer.Dispose();
            var secondDisposeException = Record.Exception(() => tailer.Dispose());

            Assert.Null(secondDisposeException); // Atomic Interlocked flag prevents duplicate execution crashes
        }

        [Fact]
        public async Task RunFromPosition_DisposedMidStream_HandlesLinkedCancellationAndClosesClean()
        {
            // Arrange
            var tailer = new LogTailer();
            File.WriteAllText(_tempFilePath, "Baseline text data string\n");

            using (var cts = new CancellationTokenSource())
            {
                var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                // Allow the reader thread context to attach to file structures before hitting dispose
                await Task.Delay(100, CancellationToken.None);

                // Act
                tailer.Dispose();

                // Assert
                try
                {
                    await tailTask;
                }
                catch (OperationCanceledException)
                {
                    // Catch internal cancellation context safely
                }
            }
        }

        #endregion
    }
}