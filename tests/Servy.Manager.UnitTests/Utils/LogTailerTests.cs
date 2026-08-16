using Servy.Core.Config;
using Servy.Manager.Models;
using Servy.Manager.Utils;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// Awaits the background worker startup signal using a deterministic safety deadline to prevent indefinite test hangs.
        /// </summary>
        /// <param name="tailer">The log tailer instance under evaluation.</param>
        /// <param name="cancellationToken">Cancels the wait (propagated to the timeout delay).</param>
        private static async Task WaitForLoopStartAsync(LogTailer tailer, CancellationToken cancellationToken)
        {
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            var completedTask = await Task.WhenAny(tailer.LoopStartedSignal.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException("The LogTailer background loop failed to start within 5 seconds.");
            }
        }

        #region Path Validation & History Guard Branch Tests

        [Fact]
        public async Task GetHistoryAsync_NullOrEmptyPath_ReturnsEmptyHistoryImmediately()
        {
            // Arrange
            using (var tailer = new LogTailer())
            {
                // Act
                var resultNull = await tailer.GetHistoryAsync(null, LogType.StdOut, 10, cancellationToken: CancellationToken.None);
                var resultEmpty = await tailer.GetHistoryAsync(string.Empty, LogType.StdOut, 10, cancellationToken: CancellationToken.None);

                // Assert
                Assert.NotNull(resultNull);
                Assert.Empty(resultNull.Lines);
                Assert.NotNull(resultEmpty);
                Assert.Empty(resultEmpty.Lines);
            }
        }

        [Fact]
        public async Task GetHistoryAsync_MissingFile_ReturnsEmptyHistoryImmediately()
        {
            // Arrange
            using (var tailer = new LogTailer())
            {
                string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.log");

                // Act
                var result = await tailer.GetHistoryAsync(missingPath, LogType.StdOut, 10, cancellationToken: CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.Empty(result.Lines);
            }
        }

        [Fact]
        public async Task GetHistoryAsync_EmptyFile_ReturnsEmptyHistoryAndZeroOffset()
        {
            // Arrange
            using (var tailer = new LogTailer())
            {
                File.WriteAllText(_tempFilePath, string.Empty);

                // Act
                var result = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 10, cancellationToken: CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.Empty(result.Lines);
                Assert.Equal(0, result.Position);
            }
        }

        [Fact]
        public async Task GetHistoryAsync_ShouldRetrieveExactlyLastNLines()
        {
            // Arrange
            using (var tailer = new LogTailer())
            {
                var linesToWrite = new[] { "L1", "L2", "L3", "L4", "L5" };
                File.WriteAllLines(_tempFilePath, linesToWrite);

                // Act
                var result = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 3, cancellationToken: CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(new FileInfo(_tempFilePath).Length, result.Position);
                Assert.Equal(3, result.Lines.Count);
                Assert.Equal(new[] { "L3", "L4", "L5" }, result?.Lines.Select(l => l.Text).ToArray());
            }
        }

        [Fact]
        public async Task GetHistoryAsync_ShouldHandleSyntheticTimestampsCorrectly()
        {
            // Arrange
            using (var tailer = new LogTailer())
            {
                File.WriteAllLines(_tempFilePath, new[] { "Line1", "Line2" });

                // Act
                var result = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 10, cancellationToken: CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(new FileInfo(_tempFilePath).Length, result.Position);
                Assert.Equal(2, result.Lines.Count);
                Assert.True(result?.Lines[0].Timestamp < result?.Lines[1].Timestamp);
                Assert.True(result?.Lines[0].IsSyntheticTime);
                Assert.True(result?.Lines[1].IsSyntheticTime);
            }
        }

        [Fact]
        public async Task RunFromPosition_NullOrEmptyPath_ExitsEarlyWithoutLoopAllocation()
        {
            // Arrange
            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                // Act
                var taskNull = tailer.RunFromPosition(null, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);
                var taskEmpty = tailer.RunFromPosition(string.Empty, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);
                await taskNull;
                await taskEmpty;

                // Assert
                Assert.False(tailer.LoopStartedSignal.Task.IsCompleted,
                    "The log tailer incorrectly allocated loop resources for a null or empty file path context.");
            }
        }

        #endregion

        #region Exception & Directory/File Race Mitigation Branch Tests

        [Fact]
        public async Task RunFromPosition_MissingDirectoryPath_ExecutesMissingFileDelayFastPath()
        {
            // Arrange
            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                string invalidDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "app.log");

                bool linesEmitted = false;
                int loopPassesCount = 0;

                tailer.OnNewLines += (lines) => linesEmitted = true;
                tailer.OnLoopCompleted += () => Interlocked.Increment(ref loopPassesCount);

                // Act
                var tailTask = tailer.RunFromPosition(invalidDirectoryPath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                await Task.Delay(150, CancellationToken.None);
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
            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                File.WriteAllText(_tempFilePath, "Initial content\n");

                bool linesEmitted = false;
                int successfulLoopIterations = 0;

                tailer.OnNewLines += (lines) => linesEmitted = true;
                tailer.OnLoopCompleted += () => Interlocked.Increment(ref successfulLoopIterations);

                using (var exclusiveLock = new FileStream(_tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    // Act
                    var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                    await Task.Delay(150, CancellationToken.None);
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
            using (var tailer = new LogTailer())
            {
                string lockTestPath = Path.Combine(Path.GetTempPath(), $"lock_race_{Guid.NewGuid()}.log");
                File.WriteAllText(lockTestPath, "Historical line context payload stream\n");

                // Act
                using (var exclusiveLock = new FileStream(lockTestPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var result = await tailer.GetHistoryAsync(lockTestPath, LogType.StdOut, 10, cancellationToken: CancellationToken.None);

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
        }

        #endregion

        #region Tailing Lifecycle & Batch Buffer Flush Branch Tests

        [Fact]
        public async Task RunFromPosition_BatchFlushThresholdReached_InvokesOnNewLinesDuringReadLoop()
        {
            // Arrange
            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                File.WriteAllText(_tempFilePath, "Pre-existing header lines\n");

                var capturedBatches = new List<List<LogLine>>();
                tailer.OnNewLines += (lines) =>
                {
                    lock (capturedBatches) capturedBatches.Add(new List<LogLine>(lines));
                };

                var loopCompletedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                tailer.OnLoopCompleted += () => loopCompletedTcs.TrySetResult(true);

                // Query precise FileInfo metadata so CreationTimeUtc matches and doesn't trigger a false rotation reset to offset 0
                var fileInfo = new FileInfo(_tempFilePath);
                var startPos = fileInfo.Length;
                var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, startPos, fileInfo.CreationTimeUtc, cts.Token);

                // Wait for the background reader loop to fully complete its initial cycle
                // and position its internal StreamReader handle directly at the EOF boundary.
                await WaitForLoopStartAsync(tailer, CancellationToken.None);
                await loopCompletedTcs.Task;

                // Pre-create and flush all lines to disk synchronously
                var contentToAppend = string.Join("\n", Enumerable.Range(0, AppConfig.LogTailerBatchFlushThreshold + 5).Select(i => $"BatchLine_{i}")) + "\n";

                using (var fs = new FileStream(_tempFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                using (var writer = new StreamWriter(fs))
                {
                    await writer.WriteAsync(contentToAppend);
                }

                // Wait for background batch splitting mechanics to propagate updates
                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedBatches) return capturedBatches.Count >= 2;
                }, TimeSpan.FromSeconds(5), cancellationToken: CancellationToken.None);

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert
                lock (capturedBatches)
                {
                    Assert.True(capturedBatches.Count >= 2, "Expected a mid-read threshold flush followed by the end-of-pass flush.");
                    Assert.Equal(AppConfig.LogTailerBatchFlushThreshold, capturedBatches[0].Count);
                }
            }
        }

        [Fact]
        public async Task RunFromPosition_ThresholdBatchWithUnterminatedLine_HoldsBackTornFragmentUntilNewline()
        {
            // Arrange
            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                File.WriteAllText(_tempFilePath, string.Empty);
                var fileInfo = new FileInfo(_tempFilePath);

                var capturedLines = new List<LogLine>();
                tailer.OnNewLines += (lines) =>
                {
                    lock (capturedLines) capturedLines.AddRange(lines);
                };

                var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, 0, fileInfo.CreationTimeUtc, cts.Token);
                await WaitForLoopStartAsync(tailer, CancellationToken.None);

                // Construct AppConfig.LogTailerBatchFlushThreshold lines where the last line lacks a trailing newline
                int threshold = AppConfig.LogTailerBatchFlushThreshold;
                var fullLines = Enumerable.Range(1, threshold - 1).Select(i => $"FullLine_{i}");
                string contentWithTornTail = string.Join("\n", fullLines) + "\nUNTERMINATED_TAIL_LINE";

                // Act
                File.WriteAllText(_tempFilePath, contentWithTornTail);

                // Wait for the complete lines to be published
                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedLines) return capturedLines.Count == threshold - 1;
                }, TimeSpan.FromSeconds(5), cancellationToken: CancellationToken.None);

                // Assert - The torn tail line should be held back and not published prematurely
                lock (capturedLines)
                {
                    Assert.Equal(threshold - 1, capturedLines.Count);
                    Assert.DoesNotContain(capturedLines, l => l.Text.Contains("UNTERMINATED_TAIL_LINE"));
                }

                // Act - Terminate the tail line with a newline
                using (var fs = new FileStream(_tempFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                using (var writer = new StreamWriter(fs))
                {
                    await writer.WriteAsync("\n");
                }

                // Wait for the full line to be published
                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedLines) return capturedLines.Count == threshold;
                }, TimeSpan.FromSeconds(5), cancellationToken: CancellationToken.None);

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert - The reconstructed full line should now be emitted exactly once
                lock (capturedLines)
                {
                    Assert.Equal(threshold, capturedLines.Count);
                    Assert.Equal(1, capturedLines.Count(l => l.Text == "UNTERMINATED_TAIL_LINE"));
                }
            }
        }

        [Fact]
        public async Task RunFromPosition_MidPassExceptionAfterFlush_DoesNotReplayFlushedLines()
        {
            // Arrange
            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                File.WriteAllText(_tempFilePath, string.Empty);
                var fileInfo = new FileInfo(_tempFilePath);

                var capturedBatches = new List<List<LogLine>>();
                tailer.OnNewLines += (lines) =>
                {
                    lock (capturedBatches) capturedBatches.Add(new List<LogLine>(lines));
                };

                var loopCompletedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                tailer.OnLoopCompleted += () => loopCompletedTcs.TrySetResult(true);

                var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, 0, fileInfo.CreationTimeUtc, cts.Token);
                await WaitForLoopStartAsync(tailer, CancellationToken.None);
                await loopCompletedTcs.Task;

                // Act - Append a full batch threshold of complete lines using shared write permissions
                int threshold = AppConfig.LogTailerBatchFlushThreshold;
                var batchContent = string.Join("\n", Enumerable.Range(1, threshold).Select(i => $"Line_{i}")) + "\n";

                using (var fs = new FileStream(_tempFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                using (var writer = new StreamWriter(fs))
                {
                    await writer.WriteAsync(batchContent);
                }

                // Wait for the threshold flush to be published and committed
                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedBatches) return capturedBatches.Count >= 1;
                }, TimeSpan.FromSeconds(5), cancellationToken: CancellationToken.None);

                // Append additional lines after the threshold flush using shared write permissions
                using (var fs = new FileStream(_tempFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                using (var writer = new StreamWriter(fs))
                {
                    await writer.WriteAsync("PostFlushLine1\nPostFlushLine2\n");
                }

                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedBatches) return capturedBatches.SelectMany(b => b).Any(l => l.Text == "PostFlushLine2");
                }, TimeSpan.FromSeconds(5), cancellationToken: CancellationToken.None);

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert - Flushed lines from earlier passes must not be duplicated
                lock (capturedBatches)
                {
                    var allLines = capturedBatches.SelectMany(b => b).Select(l => l.Text).ToList();
                    Assert.Equal(1, allLines.Count(l => l == "Line_1"));
                    Assert.Equal(1, allLines.Count(l => l == $"Line_{threshold}"));
                    Assert.Equal(1, allLines.Count(l => l == "PostFlushLine1"));
                    Assert.Equal(1, allLines.Count(l => l == "PostFlushLine2"));
                }
            }
        }

        [Fact]
        public async Task RunFromPosition_ShouldHandleFileRotation()
        {
            // Arrange
            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
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

                // Act
                // Start tailing from the end of the "Old content"
                var tailTask = tailer.RunFromPosition(initialPath, LogType.StdOut, fileInfo.Length, fileInfo.CreationTimeUtc, cts.Token);

                // DETERMINISTIC WAIT 1: Ensure the loop has fully completed its first pass setup
                await WaitForLoopStartAsync(tailer, CancellationToken.None);

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
                }, TimeSpan.FromSeconds(10), cancellationToken: CancellationToken.None);

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
        public async Task RunFromPosition_InitialAttachRotationTrigger_TimestampMismatch_ResetsOffsetToZero()
        {
            // Arrange
            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                File.WriteAllText(_tempFilePath, "Line After Truncated Rotation\n");

                var capturedLines = new List<LogLine>();
                tailer.OnNewLines += (lines) => { lock (capturedLines) capturedLines.AddRange(lines); };

                var loopCompletedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                tailer.OnLoopCompleted += () => loopCompletedTcs.TrySetResult(true);

                // Act
                // ROTATION OPERAND ISOLATION: Set lastPosition within the valid file length boundary (0 <= 30 bytes)
                // to force operand #2 (info.Length < lastPosition) to evaluate as FALSE.
                // Pass a stale timestamp to force operand #1 (info.CreationTimeUtc != lastCreationTime) to evaluate as TRUE.
                var fileInfo = new FileInfo(_tempFilePath);
                var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, (long)fileInfo.Length, DateTime.UtcNow.AddDays(-1), cts.Token);

                // Enforce execution stabilization before running content validations
                await WaitForLoopStartAsync(tailer, CancellationToken.None);
                await loopCompletedTcs.Task;

                await Helper.WaitUntilAsync(() => { lock (capturedLines) return capturedLines.Count > 0; },
                    TimeSpan.FromSeconds(5),
                    cancellationToken: CancellationToken.None);
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

        [Fact]
        public async Task RunFromPosition_InitialAttachRotationTrigger_Truncation_ResetsOffsetToZero()
        {
            // Arrange
            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                File.WriteAllText(_tempFilePath, "Line After Truncated Rotation\n");

                var capturedLines = new List<LogLine>();
                tailer.OnNewLines += (lines) => { lock (capturedLines) capturedLines.AddRange(lines); };

                var loopCompletedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                tailer.OnLoopCompleted += () => loopCompletedTcs.TrySetResult(true);

                // Act
                // ROTATION OPERAND ISOLATION: Query and pass the precise CreationTimeUtc metadata token
                // to force operand #1 (info.CreationTimeUtc != lastCreationTime) to evaluate as FALSE.
                // Pass a highly advanced past lastPosition (999999) that forces the metadata check branch
                // (info.Length < lastPosition) to evaluate as TRUE to validate initial attach truncation logic.
                var fileInfo = new FileInfo(_tempFilePath);
                var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, 999999, fileInfo.CreationTimeUtc, cts.Token);

                // Enforce execution stabilization before running content validations
                await WaitForLoopStartAsync(tailer, CancellationToken.None);
                await loopCompletedTcs.Task;

                await Helper.WaitUntilAsync(() => { lock (capturedLines) return capturedLines.Count > 0; },
                    TimeSpan.FromSeconds(5),
                    cancellationToken: CancellationToken.None);
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

            // Act - Verify initial state before disposal
            bool isDisposedBefore = TestReflection.GetField<int>(tailer, "_isDisposed") == 1;
            Assert.False(isDisposedBefore, "A new LogTailer instance should not initialize in a pre-disposed state.");

            // Act - First disposal
            tailer.Dispose();

            bool isDisposedAfterFirst = TestReflection.GetField<int>(tailer, "_isDisposed") == 1;
            Assert.True(isDisposedAfterFirst, "The internal _isDisposed state guard was not toggled on the primary cleanup path execution.");

            // Act - Reset guard field back to 0 (alive) to verify second disposal hits Interlocked.Exchange
            TestReflection.SetField(tailer, "_isDisposed", 0);
            var doubleDisposeException = Record.Exception(tailer.Dispose);

            // Assert
            Assert.Null(doubleDisposeException);
            Assert.True(TestReflection.GetField<int>(tailer, "_isDisposed") == 1, "Second Dispose after guard reset did not set _isDisposed back to true.");
        }

        [Fact]
        public async Task RunFromPosition_DisposedMidStream_HandlesLinkedCancellationAndClosesClean()
        {
            // Arrange
            var tailer = new LogTailer();
            File.WriteAllText(_tempFilePath, "Baseline text data string\n");

            int loopPassesPostDisposeCount = 0;

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    var tailTask = tailer.RunFromPosition(_tempFilePath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                    // Await initial execution attach before triggering disposal path
                    await WaitForLoopStartAsync(tailer, CancellationToken.None);

                    // Act
                    // Hook the event handler right before disposal to catch any rogue subsequent spins
                    tailer.OnLoopCompleted += () => Interlocked.Increment(ref loopPassesPostDisposeCount);
                    tailer.Dispose();

                    // Assert 1: Verify prompt task completion (HandlesLinkedCancellation) via a deterministic timeout check
                    var completionDeadlineTask = Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
                    var completedTask = await Task.WhenAny(tailTask, completionDeadlineTask);

                    Assert.True(completedTask == tailTask, "The background tailer task failed to gracefully terminate within the 5-second cancellation timeout.");

                    // Unroll any aggregate or operation cancelled exceptions to confirm safe termination
                    try
                    {
                        await tailTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // Internal cancellation path context validated successfully
                    }

                    // Let the thread pools settle for a brief window frame to guarantee no secondary ticks leak out
                    await Task.Delay(50, CancellationToken.None);

                    // Assert 2: Verify that the background loop is completely halted and not spinning recursively
                    Assert.True(loopPassesPostDisposeCount <= 1,
                        $"LogTailer incorrectly allowed recursive loop cycles ({loopPassesPostDisposeCount}) to execute after disposal.");

                    // Assert 3: Verify descriptor handle cleanup (ClosesClean) by confirming exclusive file layout access
                    var fileHandleException = Record.Exception(() =>
                    {
                        using (var exclusiveStreamCheck = new FileStream(_tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    });

                    Assert.Null(fileHandleException);
                }
            }
            finally
            {
                tailer.Dispose();
            }
        }

        #endregion
    }
}
