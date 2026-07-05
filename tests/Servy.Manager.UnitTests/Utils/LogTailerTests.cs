using Servy.Core.Config;
using Servy.Manager.Models;
using Servy.Manager.Utils;
using Servy.Testing;

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

        #region Path Validation & History Guard Branch Tests

        [Fact]
        public async Task GetHistoryAsync_NullOrEmptyPath_ReturnsEmptyHistoryImmediately()
        {
            // Arrange
            var tailer = new LogTailer();

            // Act
            var resultNull = await tailer.GetHistoryAsync(null, LogType.StdOut, 10, cancellationToken: TestContext.Current.CancellationToken);
            var resultEmpty = await tailer.GetHistoryAsync(string.Empty, LogType.StdOut, 10, cancellationToken: TestContext.Current.CancellationToken);

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
            var result = await tailer.GetHistoryAsync(missingPath, LogType.StdOut, 10, cancellationToken: TestContext.Current.CancellationToken);

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
            var result = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 10, cancellationToken: TestContext.Current.CancellationToken);

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
            var linesToWrite = new[] { "L1", "L2", "L3", "L4", "L5" };
            File.WriteAllLines(_tempFilePath, linesToWrite);

            // Act
            var result = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 3, cancellationToken: TestContext.Current.CancellationToken);

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
            var result = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 10, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, result?.Lines.Count);
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
            bool loopStartedFired = false;

            _ = tailer.LoopStartedSignal.Task.ContinueWith(_ => loopStartedFired = true);

            // Act
            var taskNull = tailer.RunFromPosition(null, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);
            var taskEmpty = tailer.RunFromPosition(string.Empty, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);
            await taskNull;
            await taskEmpty;

            // Assert
            Assert.False(loopStartedFired, "The log tailer incorrectly allocated loop resources for a null or empty file path context.");
        }

        #endregion

        #region Exception & Directory/File Race Mitigation Branch Tests

        [Fact]
        public async Task RunFromPosition_MissingDirectoryException_TriggersCatchBlockAndDelays()
        {
            // Arrange
            var tailer = new LogTailer();
            string invalidDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "app.log");

            bool linesEmitted = false;
            int loopPassesCount = 0;

            tailer.OnNewLines += (lines) => linesEmitted = true;
            tailer.OnLoopCompleted += () => Interlocked.Increment(ref loopPassesCount);

            using (var cts = new CancellationTokenSource())
            {
                // Act
                var tailTask = tailer.RunFromPosition(invalidDirectoryPath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                await Task.Delay(150, TestContext.Current.CancellationToken);
                cts.Cancel();

                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert
                Assert.False(linesEmitted, "Lines should not be emitted when pointing to a completely missing directory structure.");
                Assert.Equal(0, loopPassesCount);
            }
        }

        [Fact]
        public async Task RunFromPosition_FileLockedWithIOException_TriggersIoExceptionCatchBlockAndRetries()
        {
            // Arrange
            var tailer = new LogTailer();
            File.WriteAllText(_tempFilePath, "Initial content\n");

            bool linesEmitted = false;
            int successfulLoopIterations = 0;

            tailer.OnNewLines += (lines) => linesEmitted = true;
            tailer.OnLoopCompleted += () => Interlocked.Increment(ref successfulLoopIterations);

            using (var cts = new CancellationTokenSource())
            {
                using (var exclusiveLock = new FileStream(_tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    // Act
                    var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                    await Task.Delay(150, TestContext.Current.CancellationToken);
                    cts.Cancel();

                    try { await tailTask; } catch (OperationCanceledException) { }
                }

                // Assert
                Assert.False(linesEmitted, "LogTailer incorrectly surfaced lines from an exclusively locked file descriptor stream.");
                Assert.Equal(0, successfulLoopIterations);
            }
        }

        [Fact]
        public async Task GetHistoryAsync_FileNotFoundRaceConditionCatch_ReturnsEmptyList()
        {
            // Arrange
            var tailer = new LogTailer();
            string lockTestPath = Path.Combine(Path.GetTempPath(), $"lock_race_{Guid.NewGuid()}.log");

            File.WriteAllText(lockTestPath, "Historical line context payload stream\n");

            // Act
            using (var exclusiveLock = new FileStream(lockTestPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var result = await tailer.GetHistoryAsync(lockTestPath, LogType.StdOut, 10, cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.NotNull(result);
                Assert.Empty(result.Lines);
            }

            try
            {
                if (File.Exists(lockTestPath))
                    File.Delete(lockTestPath);
            }
            catch
            {
                // Swallow cleanup failures to protect runtime step bounds
            }
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

                // Append enough lines to cross AppConfig.LogTailerBatchFlushThreshold
                using (var fs = new FileStream(_tempFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var sw = new StreamWriter(fs) { AutoFlush = true })
                {
                    for (int i = 0; i < AppConfig.LogTailerBatchFlushThreshold + 5; i++)
                    {
                        await sw.WriteLineAsync($"BatchLine_{i}");
                    }
                }

                // Wait for background batch splitting mechanics to propagate updates
                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedBatches) return capturedBatches.Count >= 2 || (capturedBatches.Count == 1 && capturedBatches[0].Count >= AppConfig.LogTailerBatchFlushThreshold);
                }, TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);

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
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
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
                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedLines)
                    {
                        return capturedLines.Exists(l => l.Text.Contains("ROTATED_CONTENT"));
                    }
                }, TimeSpan.FromSeconds(10), cancellationToken: TestContext.Current.CancellationToken);

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }

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

                await Helper.WaitUntilAsync(() => { lock (capturedLines) return capturedLines.Count > 0; }, 
                    TimeSpan.FromSeconds(5),
                    cancellationToken: TestContext.Current.CancellationToken);
                cts.Cancel();

                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert
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

            int loopPassesPostDisposeCount = 0;

            using (var cts = new CancellationTokenSource())
            {
                var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                // Await initial execution attach before triggering disposal path
                await tailer.LoopStartedSignal.Task;

                // Act
                // Hook the event handler right before disposal to catch any rogue subsequent spins
                tailer.OnLoopCompleted += () => Interlocked.Increment(ref loopPassesPostDisposeCount);
                tailer.Dispose();

                try { await tailTask; } catch (OperationCanceledException) { }

                // Let the thread pools settle for a brief window frame to guarantee no secondary ticks leak out
                await Task.Delay(50, TestContext.Current.CancellationToken);

                // Assert
                // Verify that the background loop is completely halted. 
                // It must not complete any additional execution passes once the cancellation token is processed.
                Assert.True(loopPassesPostDisposeCount <= 1,
                    $"LogTailer incorrectly allowed recursive loop cycles ({loopPassesPostDisposeCount}) to execute after disposal.");
            }
        }

        #endregion
    }
}