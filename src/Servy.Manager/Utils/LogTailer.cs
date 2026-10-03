using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Manager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Manager.Utils
{
    /// <summary>
    /// Provides functionality to monitor and stream lines from a text file in real-time.
    /// Handles initial history loading, file rotation, and batched updates.
    /// </summary>
    /// <remarks>
    /// Rotation detection uses three signals, in order of reliability:
    ///   1. File identity - GetFileInformationByHandle index plus a SHA-256 prefix
    ///      digest fallback, via NativeMethodsHelpers.GetFileIdentity. Fails safe:
    ///      an undeterminable identity is reported as "different".
    ///   2. CreationTimeUtc drift.
    ///   3. Length < lastPosition, which also covers in-place truncation.
    ///
    /// Signals 2 and 3 are the fallback rather than the primary check because on
    /// FAT32, network shares and NAS - and under Windows "File System Tunneling",
    /// where a file deleted and recreated under the same name within the tunneling
    /// window (default 15s) keeps its original CreationTime - the metadata can be
    /// stale or absent.
    /// </remarks>
    public class LogTailer : IDisposable
    {
        /// <summary>
        /// Allows tests to wait until the background loop is actually running.
        /// </summary>
        internal TaskCompletionSource<bool> LoopStartedSignal { get; private set; }
            = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Event hook triggered at the end of a polling loop iteration for synchronization profiling.
        /// </summary>
        internal event Action OnLoopCompleted;

        /// <summary>
        /// Internal token source to ensure the tailing loop stops immediately upon disposal.
        /// </summary>
        private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();

        /// <summary>
        /// Indicates whether the current instance has been disposed.
        /// </summary>
        private int _isDisposed;   // 0 = alive, 1 = disposed

        /// <summary>
        /// Delegate for handling a batch of new log lines.
        /// </summary>
        /// <param name="lines">The list of newly discovered log lines.</param>
        public delegate void NewLinesHandler(List<LogLine> lines);

        /// <summary>
        /// Occurs when new lines are read from the file or during the initial history load.
        /// </summary>
        public event NewLinesHandler OnNewLines;

        /// <summary>
        /// Evaluates metadata signals (creation timestamp drift or file truncation) to determine if a log file has rotated.
        /// </summary>
        /// <param name="info">The file metadata snapshot.</param>
        /// <param name="lastCreationTime">The expected creation timestamp from the previous check.</param>
        /// <param name="lastPosition">The last known byte offset read from the file.</param>
        /// <returns><c>true</c> if the file metadata indicates a rotation or truncation event; otherwise, <c>false</c>.</returns>
        private static bool LooksRotated(FileInfo info, DateTime lastCreationTime, long lastPosition) =>
            info.CreationTimeUtc != lastCreationTime || info.Length < lastPosition;

        /// <summary>
        /// Probes the end of a file stream to determine if it terminates with a trailing newline byte.
        /// Restores the original stream position upon completion.
        /// </summary>
        /// <param name="fs">The open file stream to inspect.</param>
        /// <returns><c>true</c> if the file is empty or ends with <c>\n</c>; otherwise, <c>false</c>.</returns>
        private static bool EndsWithNewline(FileStream fs) => EndsWithNewlineAt(fs, fs.Length);

        /// <summary>
        /// Probes whether the byte immediately before <paramref name="offset"/> is a trailing newline.
        /// Restores the original stream position upon completion.
        /// </summary>
        /// <remarks>
        /// Used by the history load, which measures a file it is not also tailing. The tailing loop does
        /// not probe the file at all: <see cref="LogLineSplitter"/> splits only where a terminator
        /// actually is, so whether a line was terminated is known when the line is produced rather than
        /// guessed afterwards from a file the writer may already have appended to.
        /// </remarks>
        /// <param name="fs">The open file stream to inspect.</param>
        /// <param name="offset">The exclusive byte offset to probe behind.</param>
        /// <returns><c>true</c> when nothing precedes <paramref name="offset"/> or the preceding byte is <c>\n</c>; otherwise, <c>false</c>.</returns>
        private static bool EndsWithNewlineAt(FileStream fs, long offset)
        {
            if (offset <= 0) return true;
            long saved = fs.Position;
            try
            {
                fs.Position = offset - 1;
                return fs.ReadByte() == (byte)'\n';
            }
            finally
            {
                fs.Position = saved;
            }
        }

        /// <summary>
        /// Scans a file stream backwards for the last <c>\n</c> byte and returns the offset of the byte
        /// that follows it, that is the first byte of the file's unterminated trailing line.
        /// </summary>
        /// <remarks>
        /// A <c>\n</c> byte never occurs inside a multi-byte UTF-8 sequence, so scanning raw bytes is safe.
        /// The stream position is left wherever the scan ended; callers seek before reading again.
        /// </remarks>
        /// <param name="fs">The open file stream to inspect.</param>
        /// <returns>
        /// The offset just past the last <c>\n</c> byte, or <c>0</c> when the file contains none.
        /// </returns>
        /// <exception cref="EndOfStreamException">
        /// Thrown when the stream ends before the expected number of bytes has been read.
        /// </exception>
        private static long OffsetAfterLastNewline(FileStream fs)
        {
            long pos = fs.Length;
            byte[] buffer = new byte[AppConfig.LogTailerHistoryScanBufferSize];

            while (pos > 0)
            {
                int toRead = (int)Math.Min(pos, buffer.Length);
                pos -= toRead;
                fs.Seek(pos, SeekOrigin.Begin);

                // Deterministic read loop for .NET Framework 4.8, as the backward scan in LoadHistory
                // uses: guarantees the chunk is fully populated even on a short read.
                int totalBytesRead = 0;
                while (totalBytesRead < toRead)
                {
                    int bytesRead = fs.Read(buffer, totalBytesRead, toRead - totalBytesRead);
                    if (bytesRead == 0)
                    {
                        throw new EndOfStreamException("Expected to read more bytes from log history stream, but reached EOF.");
                    }
                    totalBytesRead += bytesRead;
                }

                for (int i = toRead - 1; i >= 0; i--)
                {
                    if (buffer[i] == (byte)'\n') return pos + i + 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// Reads the byte order mark at the start of the file and reports the encoding it names.
        /// </summary>
        /// <remarks>
        /// Recognises the same marks a <see cref="StreamReader"/> constructed with
        /// <c>detectEncodingFromByteOrderMarks: true</c> does, and reports the offset just past the mark
        /// so the caller never feeds the preamble to the line splitter. The stream position is left
        /// wherever the probe ended; callers seek before reading again.
        /// </remarks>
        /// <param name="fs">The open file stream to inspect, positioned anywhere.</param>
        /// <param name="firstDataOffset">Outputs the offset of the first byte after the mark, or <c>0</c> when there is none.</param>
        /// <returns>The encoding the mark names, or <see cref="Encoding.UTF8"/> when the file carries none.</returns>
        private static Encoding DetectEncoding(FileStream fs, out long firstDataOffset)
        {
            byte[] mark = new byte[4];
            fs.Seek(0, SeekOrigin.Begin);
            int read = fs.Read(mark, 0, mark.Length);

            if (read >= 3 && mark[0] == 0xEF && mark[1] == 0xBB && mark[2] == 0xBF)
            {
                firstDataOffset = 3;
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            }

            if (read >= 4 && mark[0] == 0xFF && mark[1] == 0xFE && mark[2] == 0x00 && mark[3] == 0x00)
            {
                firstDataOffset = 4;
                return new UTF32Encoding(bigEndian: false, byteOrderMark: false);
            }

            if (read >= 4 && mark[0] == 0x00 && mark[1] == 0x00 && mark[2] == 0xFE && mark[3] == 0xFF)
            {
                firstDataOffset = 4;
                return new UTF32Encoding(bigEndian: true, byteOrderMark: false);
            }

            if (read >= 2 && mark[0] == 0xFF && mark[1] == 0xFE)
            {
                firstDataOffset = 2;
                return new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
            }

            if (read >= 2 && mark[0] == 0xFE && mark[1] == 0xFF)
            {
                firstDataOffset = 2;
                return new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
            }

            firstDataOffset = 0;
            return Encoding.UTF8;
        }

        /// <summary>
        /// Starts a continuous tailing loop for a specific file, beginning at a designated position.
        /// This method handles file rotation detection and batched UI updates.
        /// </summary>
        /// <param name="path">The full filesystem path to the log file.</param>
        /// <param name="type">The stream type (StdOut/StdErr) used for UI color-coding.</param>
        /// <param name="startPos">The byte offset from which to start reading (usually the end of the history load).</param>
        /// <param name="startCreated">The creation timestamp of the file when history was loaded, used to detect rotation.</param>
        /// <param name="token">A token used to stop the tailing loop when switching services or closing the app.</param>
        /// <returns>A Task representing the long-running polling operation.</returns>
        public async Task RunFromPositionAsync(string path, LogType type, long startPos, DateTime startCreated, CancellationToken token)
        {
            if (Volatile.Read(ref _isDisposed) != 0) throw new ObjectDisposedException(nameof(LogTailer));

            if (string.IsNullOrEmpty(path)) return;

            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, _disposeCts.Token))
            {
                var linkedToken = linkedCts.Token;

                long lastPosition = startPos;
                DateTime lastCreationTime = startCreated;
                FileIdentity? knownIdentity = null;
                int consecutiveFailures = 0;

                while (!linkedToken.IsCancellationRequested)
                {
                    try
                    {
                        if (!File.Exists(path))
                        {
                            await Task.Delay(AppConfig.LogTailerFileNotFoundRetryDelayMs, linkedToken);
                            continue;
                        }

                        FileInfo info = new FileInfo(path);
                        FileStream fs = null;

                        try
                        {
                            // FileShare.Delete is critical here so we don't block an external process trying to rotate the log.
                            fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        }
                        catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                        {
                            await Task.Delay(AppConfig.LogTailerFileNotFoundRetryDelayMs, linkedToken);
                            continue;
                        }
                        catch (IOException)
                        {
                            await Task.Delay(AppConfig.LogTailerIoErrorRetryDelayMs, linkedToken);
                            continue;
                        }

                        using (fs)
                        {
                            var currentIdentity = NativeMethodsHelpers.GetFileIdentity(fs);

                            // 1. Initial attach or Post-Rotation setup
                            if (knownIdentity == null)
                            {
                                if (LooksRotated(info, lastCreationTime, lastPosition))
                                {
                                    lastPosition = 0;
                                    lastCreationTime = info.CreationTimeUtc;
                                    Logger.Debug("[LogTailer] Rotation detected before first open (Metadata fallback).");
                                }
                            }
                            else
                            {
                                // 2. Identity Check: Did the file object on disk swap?
                                // 3. Metadata Check: Even if the identity is the same (truncation),
                                //    or handle info failed, check for size/time signals of rotation.
                                if (currentIdentity.IsDifferentFrom(knownIdentity.Value) ||
                                    LooksRotated(info, lastCreationTime, lastPosition))
                                {
                                    lastPosition = 0;
                                    lastCreationTime = info.CreationTimeUtc;
                                    Logger.Debug("[LogTailer] Rotation or truncation detected on reopen.");
                                }
                            }

                            knownIdentity = currentIdentity;

                            // The byte order mark names the encoding, and only the start of the file carries
                            // one; a reopen that resumes mid-file falls back to UTF-8, exactly as the
                            // StreamReader this loop used to construct did.
                            Encoding encoding = Encoding.UTF8;

                            if (lastPosition == 0)
                            {
                                encoding = DetectEncoding(fs, out lastPosition);
                            }

                            // The splitter owns the unterminated trailing fragment, and lastPosition is
                            // committed at that fragment's first byte, so a reopen re-reads it from disk
                            // rather than carrying it across a handle it may no longer belong to.
                            LogLineSplitter splitter = new LogLineSplitter(encoding);
                            byte[] readBuffer = new byte[AppConfig.LogTailerTailReadBufferSize];

                            fs.Seek(lastPosition, SeekOrigin.Begin);

                            try
                            {
                                LoopStartedSignal.TrySetResult(true);

                                while (!linkedToken.IsCancellationRequested)
                                {
                                    List<LogLine> batch = new List<LogLine>();
                                    int read;

                                    // Split on the raw bytes rather than through StreamReader.ReadLine, which
                                    // drops the only fact that matters here - whether the text it returned was
                                    // followed by a terminator. A reader that hits the end of the file inside a
                                    // line returned the prefix as a line of its own, and the remainder the writer
                                    // appended a moment later came back as a second one, within the same pass, so
                                    // no probe against the file afterwards could re-join them.
                                    while ((read = await fs.ReadAsync(readBuffer, 0, readBuffer.Length, linkedToken)) > 0)
                                    {
                                        consecutiveFailures = 0;

                                        long chunkStart = fs.Position - read;
                                        int index = 0;
                                        string line;

                                        while (splitter.TryReadLine(readBuffer, read, ref index, out line))
                                        {
                                            batch.Add(new LogLine(line, type));

                                            // Determine if this batch hit the flush threshold
                                            if (batch.Count >= AppConfig.LogTailerBatchFlushThreshold)
                                            {
                                                // index sits just past this line's terminator, so the committed
                                                // offset covers exactly what has been published and nothing more.
                                                lastPosition = chunkStart + index;

                                                OnNewLines?.Invoke(batch);

                                                batch = new List<LogLine>(AppConfig.LogTailerBatchFlushThreshold);
                                            }
                                        }
                                    }

                                    // --- The readable bytes are exhausted. Anything the splitter still holds is
                                    //     an unterminated tail, so the writer was caught mid-flush: it stays
                                    //     pending and is published once, whole, by the pass that reads its
                                    //     terminator - which may be this same pass, since the bytes are only
                                    //     split where a terminator actually is.
                                    lastPosition = fs.Position - splitter.PendingByteCount;

                                    if (batch.Count > 0) OnNewLines?.Invoke(batch);

                                    info.Refresh();
                                    bool rotated = false;

                                    if (!info.Exists)
                                    {
                                        rotated = true;
                                        Logger.Debug("[LogTailer] Rotation detected: File no longer exists.");
                                    }
                                    else if (LooksRotated(info, lastCreationTime, lastPosition))
                                    {
                                        rotated = true;
                                        Logger.Debug("[LogTailer] Rotation detected during tailing (Metadata fallback).");
                                    }
                                    else
                                    {
                                        // We are at EOF, check if the file object on disk swapped identities out from under us
                                        try
                                        {
                                            using (var checkFs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                                            {
                                                var pathIdentity = NativeMethodsHelpers.GetFileIdentity(checkFs);
                                                if (pathIdentity.IsDifferentFrom(knownIdentity.Value))
                                                {
                                                    rotated = true;
                                                    Logger.Debug("[LogTailer] Rotation detected during tailing via stable identity change.");
                                                }
                                            }
                                        }
                                        catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                                        {
                                            rotated = true;
                                        }
                                        catch (IOException)
                                        {
                                            // File might be exclusively locked during a rename/rotation event.
                                            // Ignore here, we will catch the rotation on the next pass.
                                        }
                                    }

                                    if (rotated)
                                    {
                                        break; // Break the inner loop to drop the stale handle and reopen
                                    }

                                    // We successfully reached the EOF polling point without crashing.
                                    consecutiveFailures = 0;
                                    // Signal to the test framework that the current stream buffer is drained
                                    // and the loop iteration is completing its pass.
                                    OnLoopCompleted?.Invoke();
                                    await Task.Delay(AppConfig.LogTailerEofPollIntervalMs, linkedToken);
                                }
                            }
                            finally
                            {
                                // Reset the signal if the task ends, ensuring subsequent runs (if any) can re-signal
                                LoopStartedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                            }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        consecutiveFailures++;

                        // The splitter and its held-back fragment go with the handle: lastPosition is
                        // committed at that fragment's first byte, so the reopen re-reads it from disk and
                        // the rotation checks there discard it if the file changed.

                        // CIRCUIT BREAKER: Suppress continuous log spam for recurring permanent failures.
                        if (consecutiveFailures == 1 || consecutiveFailures % AppConfig.LogTailerErrorLogThrottlingInterval == 0)
                        {
                            Logger.Error($"Unexpected error in log tailer for {path} (occurrence #{consecutiveFailures}, throttled).", ex);
                        }

                        // LINEAR BACKOFF: Progressively scale recovery wait by attempt number, capped at MaxDelay.
                        int delay = Math.Min(AppConfig.LogTailerMaxUnhandledErrorRecoveryDelayMs, AppConfig.LogTailerUnhandledErrorRecoveryDelayMs * consecutiveFailures);
                        try { await Task.Delay(delay, linkedToken); }
                        catch (OperationCanceledException) { break; }
                    }
                }
            }
        }

        /// <summary>
        /// Just loads the history and returns the state without starting the tailing loop.
        /// </summary>
        /// <param name="path">The full filesystem path to the log file. A null, empty or missing path yields an empty history.</param>
        /// <param name="type">The stream type (StdOut/StdErr) assigned to the returned <see cref="LogLine"/> objects.</param>
        /// <param name="maxLines">Maximum number of historical lines to retrieve; clamped to the range 0 to <see cref="AppConfig.LogTailerMaxSafeLines"/>.</param>
        /// <param name="cancellationToken">A token used to cancel the history load operation.</param>
        /// <returns>A <see cref="HistoryResult"/> holding the lines read, the byte position to start tailing from (see <see cref="RunFromPositionAsync"/>), and the file's UTC creation time.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        public async Task<HistoryResult> GetHistoryAsync(string path, LogType type, int maxLines, CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _isDisposed) != 0) throw new ObjectDisposedException(nameof(LogTailer));
            long pos = 0;
            DateTime created = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            var lines = await Task.Run(() => LoadHistory(path, type, maxLines, out pos, out created, cancellationToken), cancellationToken);
            return new HistoryResult(lines, pos, created);
        }

        /// <summary>
        /// Reads the tail end of a file to provide historical context when the console is first opened.
        /// </summary>
        /// <remarks>
        /// Historical lines are assigned synthetic timestamps based on the file's last write time
        /// with a 1-tick (100-nanosecond) backward offset per line. These lines are explicitly marked with
        /// <see cref="LogLine.IsSyntheticTime"/> to indicate the time is an estimate.
        /// </remarks>
        /// <param name="path">The file path.</param>
        /// <param name="type">The log type for the resulting <see cref="LogLine"/> objects.</param>
        /// <param name="maxLines">Maximum number of historical lines to retrieve.</param>
        /// <param name="finalPos">Outputs the file position where the history ended (to start tailing from).</param>
        /// <param name="creationTimeUtc">Outputs the UTC creation time of the file used for rotation detection.</param>
        /// <param name="cancellationToken">A token used to cancel the history load operation.</param>
        /// <returns>A list of log lines retrieved from the end of the file.</returns>
        private List<LogLine> LoadHistory(string path, LogType type, int maxLines, out long finalPos, out DateTime creationTimeUtc, CancellationToken cancellationToken = default)
        {
            finalPos = 0;
            creationTimeUtc = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            List<LogLine> lines = new List<LogLine>();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return lines;
            }

            maxLines = Math.Min(Math.Max(maxLines, 0), AppConfig.LogTailerMaxSafeLines);

            try
            {
                FileInfo info = new FileInfo(path);
                creationTimeUtc = info.CreationTimeUtc;
                DateTime lastWrite = info.LastWriteTimeUtc;

                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    finalPos = fs.Length;
                    if (fs.Length == 0) return lines;

                    // Pre-increment the line count if the file does not end with a trailing newline.
                    // This ensures the backward scanner accurately bounds the "last N lines" even when
                    // catching a live log file mid-flush.
                    bool tornTail = !EndsWithNewline(fs);
                    int count = tornTail ? 1 : 0;

                    long pos = fs.Length;
                    byte[] buffer = new byte[AppConfig.LogTailerHistoryScanBufferSize];

                    // Backwards scan for newline characters to locate the start of the last 'maxLines'
                    while (pos > 0 && count <= maxLines)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int toRead = (int)Math.Min(pos, buffer.Length);
                        pos -= toRead;
                        fs.Seek(pos, SeekOrigin.Begin);

                        // Hand-crafted a deterministic read loop for .NET Framework 4.8.
                        // This guarantees the buffer chunk is fully populated even if the OS
                        // returns a partial/short read, preventing stale data corruption.
                        int totalBytesRead = 0;
                        while (totalBytesRead < toRead)
                        {
                            int bytesRead = fs.Read(buffer, totalBytesRead, toRead - totalBytesRead);
                            if (bytesRead == 0)
                            {
                                // Clean boundary handling: reached an unexpected end of stream/truncation
                                throw new EndOfStreamException("Expected to read more bytes from log history stream, but reached EOF.");
                            }
                            totalBytesRead += bytesRead;
                        }

                        for (int i = toRead - 1; i >= 0; i--)
                        {
                            if (buffer[i] == (byte)'\n')
                            {
                                count++;
                                if (count > maxLines) { pos = pos + i + 1; break; }
                            }
                        }
                    }

                    // Read forward from the discovered position. Resolve the torn tail's first byte
                    // before the StreamReader takes the stream over, so nothing seeks underneath it.
                    long tornTailStart = tornTail ? OffsetAfterLastNewline(fs) : 0;
                    fs.Seek(pos, SeekOrigin.Begin);
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                    {
                        string line;
                        var tempLines = new List<string>();
                        while ((line = sr.ReadLine()) != null && tempLines.Count < maxLines)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            tempLines.Add(line);
                        }

                        // The file does not end with a newline, so the writer may still be flushing its
                        // last line. Leave that line out of the history and point the live tailer at its
                        // first byte instead: RunFromPositionAsync holds an unterminated fragment in its
                        // LogLineSplitter and publishes it once, whole, when the newline arrives. Without
                        // this the history published the torn prefix and the tail published the remainder as
                        // a second line. While a line is mid-flush the history therefore holds at most
                        // maxLines - 1 complete lines.
                        if (tornTail && tempLines.Count > 0)
                        {
                            tempLines.RemoveAt(tempLines.Count - 1);
                            finalPos = tornTailStart;
                        }

                        for (int i = 0; i < tempLines.Count; i++)
                        {
                            // Logic: The very last line in the file is 'lastWrite'
                            // Every line before it is 1 tick (100 nanoseconds) older.
                            long offsetTicks = tempLines.Count - 1 - i;
                            DateTime syntheticTime = lastWrite.AddTicks(-offsetTicks);

                            // Create the line and explicitly mark the time as synthetic
                            LogLine logLine = new LogLine(tempLines[i], type, syntheticTime)
                            {
                                IsSyntheticTime = true
                            };

                            lines.Add(logLine);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (FileNotFoundException) { return lines; }
            catch (DirectoryNotFoundException) { return lines; }
            catch (IOException ex) { Logger.Debug($"History load IO error for {path}: {ex.Message}"); return lines; }
            catch (UnauthorizedAccessException ex) { Logger.Debug($"History load access denied for {path}: {ex.Message}"); return lines; }

            return lines;
        }

        /// <summary>
        /// Cancels active background tailing tasks, detaches event handlers, and releases managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the managed resources used by <see cref="LogTailer"/>.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> when called from <see cref="Dispose()"/>. This type has no finalizer,
        /// so it is never <see langword="false"/>; the parameter exists for derived types to override.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;

            if (disposing)
            {
                // 1. Break the strong reference to the subscriber
                OnNewLines = null;
                OnLoopCompleted = null;

                // 2. CRITICAL: Cancel the internal token to instantly kill the while-loop
                // and release any active FileStreams or Task.Delays.
                Helpers.Helper.CancelAndDisposeSafely(_disposeCts);
            }
        }
    }
}
