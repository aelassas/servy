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
        private readonly List<string> _extraTempFiles = new List<string>();

        public LogTailerTests()
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), $"logtailer_test_{Guid.NewGuid()}.log");
        }

        /// <summary>
        /// Builds an additional temp file path and registers it for cleanup in <see cref="Dispose"/>,
        /// so a failing assertion cannot orphan it.
        /// </summary>
        /// <param name="prefix">A short prefix identifying the scenario that owns the file.</param>
        /// <returns>The registered path. The file itself is not created.</returns>
        private string NewTempFilePath(string prefix)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid()}.log");
            _extraTempFiles.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (var path in _extraTempFiles)
            {
                DeleteQuietly(path);
            }

            DeleteQuietly(_tempFilePath);
        }

        /// <summary>
        /// Best-effort delete; swallows exceptions if a running test still holds the file open.
        /// </summary>
        /// <param name="path">The file to remove.</param>
        private static void DeleteQuietly(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
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
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(TestTimeouts.LogTailerWaitSeconds), cancellationToken);
            var completedTask = await Task.WhenAny(tailer.LoopStartedSignal.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException(
                    $"The LogTailer background loop failed to start within {TestTimeouts.LogTailerWaitSeconds} seconds.");
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
                var expectedLastWrite = new FileInfo(_tempFilePath).LastWriteTimeUtc;

                // Act
                var result = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 10, cancellationToken: CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(new FileInfo(_tempFilePath).Length, result.Position);
                Assert.Equal(2, result.Lines.Count);

                // The last line is anchored exactly on the file's last-write time...
                Assert.Equal(expectedLastWrite, result.Lines[1].Timestamp);
                // ...and every earlier line is exactly one tick older than the one after it.
                Assert.Equal(expectedLastWrite.AddTicks(-1), result.Lines[0].Timestamp);

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
                int loopPassesCount = 0;
                tailer.OnLoopCompleted += () => Interlocked.Increment(ref loopPassesCount);

                // Act
                var taskNull = tailer.RunFromPositionAsync(null, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);
                var taskEmpty = tailer.RunFromPositionAsync(string.Empty, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                // Assert
                // A guarded call never yields, so both tasks are already finished before the first await.
                // LoopStartedSignal cannot carry this: the tailer replaces it with a fresh, uncompleted
                // source in a finally block, so it also reads as incomplete after a loop that ran and ended.
                Assert.True(taskNull.Status == TaskStatus.RanToCompletion,
                    "A null file path should return synchronously without entering the tailing loop.");
                Assert.True(taskEmpty.Status == TaskStatus.RanToCompletion,
                    "An empty file path should return synchronously without entering the tailing loop.");

                await taskNull;
                await taskEmpty;

                // OnLoopCompleted is raised only from inside the loop and is never reset, so it survives
                // a loop that started and then finished.
                Assert.Equal(0, Volatile.Read(ref loopPassesCount));
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
                var tailTask = tailer.RunFromPositionAsync(invalidDirectoryPath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                await Task.Delay(TestTimeouts.LogTailerLoopPassDelayMs, CancellationToken.None);
                cts.Cancel();

                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert
                Assert.False(linesEmitted, "Lines should not be emitted when pointing to a completely missing directory structure.");
                Assert.Equal(0, loopPassesCount);
            }
        }

        [Fact]
        public async Task RunFromPosition_FileCreatedAfterStart_IsTailedOnceItAppears()
        {
            // Arrange
            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                // The file does not exist yet, so the loop can only reach the missing-file wait.
                string latePath = NewTempFilePath("logtailer_late");

                var capturedLines = new List<LogLine>();
                tailer.OnNewLines += (lines) =>
                {
                    lock (capturedLines) capturedLines.AddRange(lines);
                };

                var tailTask = tailer.RunFromPositionAsync(latePath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                // Give the loop time to take the missing-file arm at least twice before the file appears.
                await Task.Delay(AppConfig.LogTailerFileNotFoundRetryDelayMs * 2, CancellationToken.None);
                Assert.False(tailTask.IsCompleted, "The loop must keep waiting while the file does not exist.");

                // Act
                File.WriteAllText(latePath, "LATE_FILE_LINE\n");

                // Assert
                await Helper.WaitUntilAsync(
                    () => { lock (capturedLines) return capturedLines.Count > 0; },
                    TimeSpan.FromSeconds(TestTimeouts.LogTailerWaitSeconds),
                    cancellationToken: CancellationToken.None);

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }

                lock (capturedLines)
                {
                    Assert.Contains(capturedLines, l => l.Text == "LATE_FILE_LINE");
                }
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

                var capturedLines = new List<LogLine>();
                int successfulLoopIterations = 0;

                tailer.OnNewLines += (lines) => { lock (capturedLines) capturedLines.AddRange(lines); };
                tailer.OnLoopCompleted += () => Interlocked.Increment(ref successfulLoopIterations);

                Task tailTask;

                // Act
                // The lock is taken BEFORE the loop starts, so the first open cannot win a race with it
                // and the IOException arm is the only path the loop can take while this block runs.
                using (var exclusiveLock = new FileStream(_tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    tailTask = tailer.RunFromPositionAsync(_tempFilePath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                    await Task.Delay(TestTimeouts.LogTailerLoopPassDelayMs, CancellationToken.None);

                    // Assert (still locked): the catch block absorbed the open failure.
                    lock (capturedLines)
                    {
                        Assert.Empty(capturedLines);
                    }

                    Assert.Equal(0, successfulLoopIterations);
                }

                // Assert (lock released): the catch block resumed the loop instead of abandoning it.
                // This is the half the locked observations cannot supply - a loop that caught the
                // IOException once and stopped emits nothing and completes no pass either, so holding
                // the lock for the whole test leaves the "AndRetries" half of the name unverified.
                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedLines) return capturedLines.Count > 0;
                }, TimeSpan.FromSeconds(TestTimeouts.LogTailerWaitSeconds), cancellationToken: CancellationToken.None);

                cts.Cancel();

                try { await tailTask; } catch (OperationCanceledException) { }

                lock (capturedLines)
                {
                    Assert.Contains(capturedLines, l => l.Text.Contains("Initial content"));
                }

                Assert.True(successfulLoopIterations > 0, "A retry after the lock was released must complete at least one loop pass.");
            }
        }

        [Fact]
        public async Task GetHistoryAsync_FileLockedWithIOException_ReturnsEmptyList()
        {
            // Arrange
            // Opening the file with FileShare.None makes LoadHistory's own FileStream open throw
            // IOException, so this test covers the IOException arm and nothing else. ACCEPTED: the
            // FileNotFoundException, DirectoryNotFoundException and UnauthorizedAccessException arms
            // of LoadHistory stay untested here. The first two are reachable only if the file
            // disappears between the File.Exists guard and the open one statement later, and the
            // third needs an ACL-denied file; arranging either from a test requires the file system
            // access to be injectable, which this class has no seam for.
            using (var tailer = new LogTailer())
            {
                string lockTestPath = NewTempFilePath("lock_race");
                File.WriteAllText(lockTestPath, "Historical line context payload stream\n");

                // Act
                using (var exclusiveLock = new FileStream(lockTestPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var result = await tailer.GetHistoryAsync(lockTestPath, LogType.StdOut, 10, cancellationToken: CancellationToken.None);

                    // Assert
                    Assert.NotNull(result);
                    Assert.Empty(result.Lines);
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

                // Pre-create content containing more lines than the batch flush threshold
                int totalLinesToAppend = AppConfig.LogTailerBatchFlushThreshold + 5;
                var contentToAppend = string.Join("\n", Enumerable.Range(0, totalLinesToAppend).Select(i => $"BatchLine_{i}")) + "\n";

                // Write and flush ALL lines to disk synchronously BEFORE running or resuming the tailer pass
                using (var fs = new FileStream(_tempFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                using (var writer = new StreamWriter(fs))
                {
                    writer.Write(contentToAppend);
                    writer.Flush();
                }

                // Act - Start tailing after the full batch payload is guaranteed to be on disk
                var tailTask = tailer.RunFromPositionAsync(_tempFilePath, LogType.StdOut, startPos, fileInfo.CreationTimeUtc, cts.Token);

                // Wait for background batch splitting mechanics to propagate updates
                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedBatches) return capturedBatches.Count >= 2;
                }, TimeSpan.FromSeconds(TestTimeouts.LogTailerWaitSeconds), cancellationToken: CancellationToken.None);

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert
                lock (capturedBatches)
                {
                    Assert.True(capturedBatches.Count >= 2, "Expected a mid-read threshold flush followed by the end-of-pass flush.");
                    Assert.Equal(AppConfig.LogTailerBatchFlushThreshold, capturedBatches[0].Count);
                    Assert.Equal(totalLinesToAppend - AppConfig.LogTailerBatchFlushThreshold, capturedBatches[1].Count);
                }
            }
        }

        [Fact]
        public async Task RunFromPosition_ThresholdFlushMidBuffer_DoesNotHoldBackCompleteLines()
        {
            // Arrange: Reproduce issue #6471 where a burst of lines exceeding threshold sits in the StreamReader's
            // internal buffer while the underlying FileStream is at EOF and lacks a trailing newline at the end of the file.
            // Complete lines sitting mid-buffer during a threshold flush must NOT be mistaken for the unterminated tail line.
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

                var tailTask = tailer.RunFromPositionAsync(_tempFilePath, LogType.StdOut, 0, fileInfo.CreationTimeUtc, cts.Token);
                await WaitForLoopStartAsync(tailer, CancellationToken.None);

                int threshold = AppConfig.LogTailerBatchFlushThreshold;
                int totalLinesWritten = threshold + 20;

                // Write 'totalLinesWritten' lines where line 'threshold' is complete, but the very last line (totalLinesWritten) lacks a newline
                var fullLines = Enumerable.Range(1, totalLinesWritten - 1).Select(i => $"MidBufferLine_{i}");
                string burstContent = string.Join("\n", fullLines) + "\nUNTERMINATED_FINAL_BURST_TAIL";

                // Act
                File.WriteAllText(_tempFilePath, burstContent);

                // Wait for the threshold batch to publish
                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedLines) return capturedLines.Count >= threshold;
                }, TimeSpan.FromSeconds(TestTimeouts.LogTailerWaitSeconds), cancellationToken: CancellationToken.None);

                // Assert - Line 'threshold' must be published in the threshold batch and NOT held back or merged
                lock (capturedLines)
                {
                    Assert.Equal($"MidBufferLine_{threshold}", capturedLines[threshold - 1].Text);
                    Assert.DoesNotContain(capturedLines, l => l.Text.Contains("UNTERMINATED_FINAL_BURST_TAIL"));
                }

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }
            }
        }

        /// <summary>
        /// A history load from a file whose last line has no terminating newline must leave that line to
        /// the live tailer, so the console shows it once and whole instead of a prefix in the history and
        /// the remainder as a second live line.
        /// </summary>
        [Fact]
        public async Task GetHistoryThenRunFromPosition_UnterminatedLastLine_PublishesItOnceWhole()
        {
            // Arrange
            File.WriteAllText(_tempFilePath, "complete\npartial-");

            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                var history = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 10, cancellationToken: CancellationToken.None);

                var capturedLines = new List<LogLine>();
                tailer.OnNewLines += (lines) =>
                {
                    lock (capturedLines) capturedLines.AddRange(lines);
                };

                // Act - the writer completes the line between two passes. Appending from the
                // OnLoopCompleted handler itself is what makes that deterministic: the handler runs to
                // completion before the loop's poll delay, so the newline is on disk before the next
                // read can begin. Observing the boundary by polling and appending afterwards
                // leaves only the LogTailerEofPollIntervalMs window to land in, which an ARM64 CI runner
                // loses often enough to fail the suite. A line the writer completes while a pass is
                // mid-read is LogLineSplitter's case since #7333 and is not the subject here, which is
                // that the history hands the torn tail over whole.
                var passes = 0;
                var publishedAtFirstBoundary = -1;
                tailer.OnLoopCompleted += () =>
                {
                    if (Interlocked.Increment(ref passes) != 1)
                    {
                        return;
                    }

                    lock (capturedLines)
                    {
                        publishedAtFirstBoundary = capturedLines.Count;
                    }

                    File.AppendAllText(_tempFilePath, "remainder\n");
                };

                var tailTask = tailer.RunFromPositionAsync(_tempFilePath, LogType.StdOut, history.Position, history.CreationTimeUtc, cts.Token);
                await WaitForLoopStartAsync(tailer, CancellationToken.None);

                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedLines) return capturedLines.Count >= 1;
                }, TimeSpan.FromSeconds(TestTimeouts.LogTailerWaitSeconds), cancellationToken: CancellationToken.None);

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert
                // The pass that read the torn tail held it back instead of publishing it as a line of its
                // own, which is what the boundary handler observed before it appended the remainder. A -1
                // here would mean no pass ever completed and the append never happened.
                Assert.Equal(0, publishedAtFirstBoundary);

                // The history stops at the last newline, so the torn tail is not in it and Position points
                // at the tail's first byte rather than at the end of the file.
                Assert.Equal(new[] { "complete" }, history.Lines.Select(l => l.Text));
                Assert.Equal("complete\n".Length, (int)history.Position);

                // The live tailer read the tail from that byte, held it until the newline arrived, and
                // published the whole line once.
                lock (capturedLines)
                {
                    Assert.Equal(new[] { "partial-remainder" }, capturedLines.Select(l => l.Text));
                }
            }
        }

        /// <summary>
        /// A history load that is asked for no lines at all must still hand the live tailer the first byte
        /// of an unterminated last line. <c>maxLines</c> of 0 is inside the range the contract documents,
        /// and the read loop returns nothing for it, so a resume position taken from the end of the file
        /// would lose the start of the line the writer is still flushing.
        /// </summary>
        [Fact]
        public async Task GetHistoryAsync_ZeroMaxLinesWithUnterminatedLastLine_PointsAtTheTornTailsFirstByte()
        {
            // Arrange
            File.WriteAllText(_tempFilePath, "Line_1\nTORN_HEAD");

            using (var tailer = new LogTailer())
            {
                // Act
                var result = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 0, cancellationToken: CancellationToken.None);

                // Assert
                Assert.Empty(result.Lines);
                Assert.Equal("Line_1\n".Length, (int)result.Position);
            }
        }

        /// <summary>
        /// <c>OffsetAfterLastNewline</c> is what hands the live tailer the first byte of an unterminated
        /// trailing line (#7291). The short-file test above keeps the whole file in one buffer read that
        /// starts at offset 0, which hides three faults: dropping the buffer's own offset from the result,
        /// scanning only the last buffer, and returning something other than 0 when the file holds no
        /// newline at all. Each row is sized from the production buffer constant so it follows it.
        /// </summary>
        /// <param name="shape">1 = the newline is in a buffer that starts past offset 0, 2 = the torn tail is longer than one buffer, 3 = no newline in the file.</param>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task GetHistoryAsync_UnterminatedLastLine_PositionIsTheFirstByteOfTheTornLine(int shape)
        {
            // Arrange
            int buffer = AppConfig.LogTailerHistoryScanBufferSize;
            string complete;
            string torn;
            switch (shape)
            {
                case 1: complete = new string('a', buffer + 900) + "\n"; torn = "partial-"; break;
                case 2: complete = "complete\n"; torn = new string('x', buffer + 900); break;
                case 3: complete = string.Empty; torn = "partial-only"; break;
                default: throw new ArgumentOutOfRangeException(nameof(shape));
            }
            File.WriteAllText(_tempFilePath, complete + torn);

            using (var tailer = new LogTailer())
            {
                // Act
                var history = await tailer.GetHistoryAsync(_tempFilePath, LogType.StdOut, 10, CancellationToken.None);

                // Assert - the tail resumes at the torn line's first byte, and the history holds only the complete line
                Assert.Equal((long)Encoding.UTF8.GetByteCount(complete), history.Position);
                Assert.Equal(
                    complete.Length == 0 ? Array.Empty<string>() : new[] { complete.TrimEnd('\n') },
                    history.Lines.Select(l => l.Text));
            }
        }

        /// <summary>
        /// The history load decides whether a trailing line is torn by probing the byte before the offset
        /// it has read up to, never the live end of the file. A writer that completes the line between the
        /// read and this probe must not make the consumed fragment look terminated, or the history
        /// publishes the torn prefix and the live tailer publishes the remainder as a second line. The
        /// tailing loop itself no longer probes the file at all; it splits on terminators instead, so this
        /// helper now serves the history path only.
        /// </summary>
        [Fact]
        public void EndsWithNewlineAt_WriterAppendedAfterTheRead_StillReportsTheConsumedFragmentAsTorn()
        {
            // Arrange
            File.WriteAllText(_tempFilePath, "complete\npartial-");

            using (var fs = new FileStream(_tempFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                // The reader drained the file and stopped here, mid-line.
                long readerPosition = fs.Length;

                // The writer then finished the line, so the file now ends with a newline the reader never saw.
                File.AppendAllText(_tempFilePath, "remainder\n");

                // Act
                bool atReaderPosition = (bool)TestReflection.InvokeNonPublicStatic(
                    typeof(LogTailer), "EndsWithNewlineAt", fs, readerPosition);
                bool atLiveEndOfFile = (bool)TestReflection.InvokeNonPublicStatic(
                    typeof(LogTailer), "EndsWithNewlineAt", fs, fs.Length);
                bool afterFirstNewline = (bool)TestReflection.InvokeNonPublicStatic(
                    typeof(LogTailer), "EndsWithNewlineAt", fs, (long)"complete\n".Length);
                bool atStartOfFile = (bool)TestReflection.InvokeNonPublicStatic(
                    typeof(LogTailer), "EndsWithNewlineAt", fs, 0L);

                // Assert
                Assert.False(atReaderPosition);
                Assert.True(atLiveEndOfFile);
                Assert.True(afterFirstNewline);
                Assert.True(atStartOfFile);
            }
        }

        /// <summary>
        /// The defect of #7384: the history load read the file length once per step, so a writer appending
        /// between two steps left those steps describing different files, and the position handed to the
        /// live tailer no longer matched the lines returned. The backward scan for the torn tail's first
        /// byte now starts at the snapshotted end, so a newline the writer adds afterwards cannot move the
        /// hand-over point past a line the history is about to drop.
        /// </summary>
        [Fact]
        public void OffsetAfterLastNewline_WriterAppendedAfterTheSnapshot_ScansBackFromTheSnapshottedEnd()
        {
            // Arrange
            File.WriteAllText(_tempFilePath, "complete\npartial-");

            using (var fs = new FileStream(_tempFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                // The load snapshotted the length here, with the file ending mid-line.
                long end = fs.Length;

                // The writer then finished that line and started another one, so the file now carries a
                // newline past the snapshot that the load must not see.
                File.AppendAllText(_tempFilePath, "remainder\nmore-");

                // Act
                long atSnapshottedEnd = (long)TestReflection.InvokeNonPublicStatic(
                    typeof(LogTailer), "OffsetAfterLastNewline", fs, end);
                long atLiveEndOfFile = (long)TestReflection.InvokeNonPublicStatic(
                    typeof(LogTailer), "OffsetAfterLastNewline", fs, fs.Length);

                // Assert
                Assert.Equal("complete\n".Length, atSnapshottedEnd);
                Assert.Equal("complete\npartial-remainder\n".Length, atLiveEndOfFile);
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

                var tailTask = tailer.RunFromPositionAsync(_tempFilePath, LogType.StdOut, 0, fileInfo.CreationTimeUtc, cts.Token);
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
                }, TimeSpan.FromSeconds(TestTimeouts.LogTailerWaitSeconds), cancellationToken: CancellationToken.None);

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
                }, TimeSpan.FromSeconds(TestTimeouts.LogTailerWaitSeconds), cancellationToken: CancellationToken.None);

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
                int throwOnce = 1;
                tailer.OnNewLines += (lines) =>
                {
                    lock (capturedBatches) capturedBatches.Add(new List<LogLine>(lines));

                    // Fault the pass immediately after the first threshold flush has been published, so the
                    // loop lands in the unhandled-error handler and reopens the file from lastPosition. That
                    // is the only way to observe the commit-before-publish ordering at the flush point: if the
                    // offset were committed after the publish instead, the reopen would replay this batch.
                    // A subscriber that throws from this handler is the realistic trigger - ConsoleViewModel
                    // marshals to the UI thread from here.
                    if (Interlocked.Exchange(ref throwOnce, 0) == 1)
                    {
                        throw new InvalidOperationException("Simulated subscriber fault immediately after a threshold flush.");
                    }
                };

                var loopCompletedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                tailer.OnLoopCompleted += () => loopCompletedTcs.TrySetResult(true);

                var tailTask = tailer.RunFromPositionAsync(_tempFilePath, LogType.StdOut, 0, fileInfo.CreationTimeUtc, cts.Token);
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
                }, TimeSpan.FromSeconds(TestTimeouts.LogTailerWaitSeconds), cancellationToken: CancellationToken.None);

                // Append additional lines after the threshold flush using shared write permissions
                using (var fs = new FileStream(_tempFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                using (var writer = new StreamWriter(fs))
                {
                    await writer.WriteAsync("PostFlushLine1\nPostFlushLine2\n");
                }

                // The faulted pass costs one linear back-off (LogTailerUnhandledErrorRecoveryDelayMs)
                // before the reopen, so this wait is longer than its siblings.
                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedBatches) return capturedBatches.SelectMany(b => b).Any(l => l.Text == "PostFlushLine2");
                }, TimeSpan.FromSeconds(TestTimeouts.LogTailerErrorRecoveryWaitSeconds), cancellationToken: CancellationToken.None);

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert - the mid-pass fault must actually have fired, or nothing below is meaningful
                Assert.Equal(0, Volatile.Read(ref throwOnce));

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

        /// <summary>
        /// A pass that holds an unterminated tail back commits the offset at that fragment's first byte before
        /// it publishes the batch, because the fragment itself lives only in the splitter, which an exception on
        /// the generic error path discards with the handle. The reopen must therefore re-read the fragment from
        /// disk: an offset committed past it leaves the reopen resuming after those bytes, and the console
        /// shows only the remainder of the line as a line of its own.
        /// </summary>
        [Fact]
        public async Task RunFromPosition_SubscriberFaultsAfterHoldingBackATornTail_PublishesTheWholeLineAfterRecovery()
        {
            // Arrange
            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                // A threshold batch whose last line is torn: the pass publishes the complete lines, holds
                // TORN_HEAD back and commits lastPosition at its first byte.
                int threshold = AppConfig.LogTailerBatchFlushThreshold;
                var completeLines = Enumerable.Range(1, threshold - 1).Select(i => $"Line_{i}").ToList();
                File.WriteAllText(_tempFilePath, string.Join("\n", completeLines) + "\nTORN_HEAD");
                var fileInfo = new FileInfo(_tempFilePath);

                var capturedLines = new List<LogLine>();
                int throwOnce = 1;
                tailer.OnNewLines += (lines) =>
                {
                    lock (capturedLines) capturedLines.AddRange(lines);

                    // A subscriber that throws from this handler is the realistic trigger - ConsoleViewModel
                    // marshals to the UI thread from here - and it faults the pass after the flush, which is
                    // after the fragment was held back and the offset committed at its first byte.
                    if (Interlocked.Exchange(ref throwOnce, 0) == 1)
                    {
                        throw new InvalidOperationException("Simulated subscriber fault after a threshold flush that held a torn tail back.");
                    }
                };

                // Act - the writer finishes the torn line once the loop has recovered. The faulted pass never
                // reaches OnLoopCompleted, so the first boundary seen here is the first pass after the
                // recovery back-off; appending from the handler itself keeps that deterministic, because the
                // handler runs before the loop's poll delay and so the newline is on disk before the next
                // read begins.
                int appended = 0;
                tailer.OnLoopCompleted += () =>
                {
                    if (Volatile.Read(ref throwOnce) == 0 && Interlocked.Exchange(ref appended, 1) == 0)
                    {
                        File.AppendAllText(_tempFilePath, "_TAIL\n");
                    }
                };

                var tailTask = tailer.RunFromPositionAsync(_tempFilePath, LogType.StdOut, 0, fileInfo.CreationTimeUtc, cts.Token);
                await WaitForLoopStartAsync(tailer, CancellationToken.None);

                // The faulted pass costs one linear back-off (LogTailerUnhandledErrorRecoveryDelayMs) before
                // the reopen, so this wait is longer than its siblings. Matching the suffix rather than the
                // whole line lets the wait finish on the defective behaviour too, where the remainder arrives
                // on its own as "_TAIL" and the assertions below are what reports it.
                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedLines) return capturedLines.Any(l => l.Text.EndsWith("_TAIL"));
                }, TimeSpan.FromSeconds(TestTimeouts.LogTailerErrorRecoveryWaitSeconds), cancellationToken: CancellationToken.None);

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert - the mid-pass fault must actually have fired, or nothing below is meaningful
                Assert.Equal(0, Volatile.Read(ref throwOnce));

                // Assert - the reopen re-read the held-back fragment from disk, so the completed line is published
                // once and whole instead of as its remainder alone
                lock (capturedLines)
                {
                    Assert.Equal(completeLines.Concat(new[] { "TORN_HEAD_TAIL" }), capturedLines.Select(l => l.Text));
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
                var tailTask = tailer.RunFromPositionAsync(initialPath, LogType.StdOut, fileInfo.Length, fileInfo.CreationTimeUtc, cts.Token);

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
                }, TimeSpan.FromSeconds(TestTimeouts.LogTailerRotationWaitSeconds), cancellationToken: CancellationToken.None);

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert
                lock (capturedLines)
                {
                    Assert.Contains(capturedLines, l => l.Text.Contains("ROTATED_CONTENT"));
                }
            }
        }
        [Fact]
        public async Task RunFromPosition_RenameRotationWithSameCreationTimeAndLength_IsDetectedByIdentityChange()
        {
            // Arrange
            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                string path = _tempFilePath;
                string renamedPath = NewTempFilePath("logtailer_renamed");
                File.WriteAllText(path, "old line\r\n");
                var original = new FileInfo(path);
                var originalCreation = original.CreationTimeUtc;
                var originalLength = original.Length;

                var capturedLines = new List<LogLine>();
                tailer.OnNewLines += (lines) =>
                {
                    lock (capturedLines) capturedLines.AddRange(lines);
                };

                // Setup a completion tracking signal task for strict loop synchronization
                var loopCompletedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                tailer.OnLoopCompleted += () => loopCompletedTcs.TrySetResult(true);

                // Act
                // Start tailing from the end of "old line", so the loop parks at EOF
                var tailTask = tailer.RunFromPositionAsync(path, LogType.StdOut, originalLength, originalCreation, cts.Token);

                // DETERMINISTIC WAIT 1: Ensure the loop has fully completed its first pass setup
                await WaitForLoopStartAsync(tailer, CancellationToken.None);

                // Ensure the loop has recorded the original file identity before the rename
                await loopCompletedTcs.Task;

                // Rename the tailed file away, then create a new, LONGER file at the same path carrying the
                // SAME creation time, so neither term of LooksRotated can fire and only the EOF identity
                // re-check can notice the swap. Both handles open with FileShare.Delete, so the rename
                // succeeds and the tailer's own handle follows the renamed file, which never grows again.
                File.Move(path, renamedPath);
                File.WriteAllText(path, "NEW_FILE_AFTER_RENAME_ROTATION\r\n");
                File.SetCreationTimeUtc(path, originalCreation);

                // Premise guard: if the file system refuses either signal the scenario is not the one under
                // test, so fail here rather than let the assertion below pass for the wrong reason.
                var rotatedInfo = new FileInfo(path);
                Assert.Equal(originalCreation, rotatedInfo.CreationTimeUtc);
                Assert.True(rotatedInfo.Length >= originalLength,
                    "The replacement file must not be shorter than the committed offset, or LooksRotated would fire on size.");

                // DETERMINISTIC WAIT 2: Poll for the new file's content reaching capturedLines
                await Helper.WaitUntilAsync(() =>
                {
                    lock (capturedLines)
                    {
                        return capturedLines.Exists(l => l.Text.Contains("NEW_FILE_AFTER_RENAME_ROTATION"));
                    }
                }, TimeSpan.FromSeconds(TestTimeouts.LogTailerRotationWaitSeconds), cancellationToken: CancellationToken.None);

                cts.Cancel();
                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert
                lock (capturedLines)
                {
                    Assert.Contains(capturedLines, l => l.Text.Contains("NEW_FILE_AFTER_RENAME_ROTATION"));
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
                var tailTask = tailer.RunFromPositionAsync(_tempFilePath, LogType.StdOut, (long)fileInfo.Length, DateTime.UtcNow.AddDays(-1), cts.Token);

                // Enforce execution stabilization before running content validations
                await WaitForLoopStartAsync(tailer, CancellationToken.None);
                await loopCompletedTcs.Task;

                await Helper.WaitUntilAsync(() => { lock (capturedLines) return capturedLines.Count > 0; },
                    TimeSpan.FromSeconds(TestTimeouts.LogTailerWaitSeconds),
                    cancellationToken: CancellationToken.None);
                cts.Cancel();

                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert
                lock (capturedLines)
                {
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
                var tailTask = tailer.RunFromPositionAsync(_tempFilePath, LogType.StdOut, 999999, fileInfo.CreationTimeUtc, cts.Token);

                // Enforce execution stabilization before running content validations
                await WaitForLoopStartAsync(tailer, CancellationToken.None);
                await loopCompletedTcs.Task;

                await Helper.WaitUntilAsync(() => { lock (capturedLines) return capturedLines.Count > 0; },
                    TimeSpan.FromSeconds(TestTimeouts.LogTailerWaitSeconds),
                    cancellationToken: CancellationToken.None);
                cts.Cancel();

                try { await tailTask; } catch (OperationCanceledException) { }

                // Assert
                lock (capturedLines)
                {
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
                    var tailTask = tailer.RunFromPositionAsync(_tempFilePath, LogType.StdOut, 0, DateTime.UtcNow, cts.Token);

                    // Await initial execution attach before triggering disposal path
                    await WaitForLoopStartAsync(tailer, CancellationToken.None);

                    // Act
                    // Hook the event handler right before disposal to catch any rogue subsequent spins
                    tailer.OnLoopCompleted += () => Interlocked.Increment(ref loopPassesPostDisposeCount);
                    tailer.Dispose();

                    // Assert 1: Verify prompt task completion (HandlesLinkedCancellation) via a deterministic timeout check
                    var completionDeadlineTask = Task.Delay(TimeSpan.FromSeconds(TestTimeouts.LogTailerWaitSeconds), CancellationToken.None);
                    var completedTask = await Task.WhenAny(tailTask, completionDeadlineTask);

                    Assert.True(completedTask == tailTask,
                        $"The background tailer task failed to gracefully terminate within the {TestTimeouts.LogTailerWaitSeconds}-second cancellation timeout.");

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
                    await Task.Delay(TestTimeouts.LogTailerPostDisposeSettleMs, CancellationToken.None);

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
