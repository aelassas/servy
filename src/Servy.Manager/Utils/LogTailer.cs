using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Manager.Models;
using System.IO;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Manager.Utils
{
    /// <summary>
    /// Provides functionality to monitor and stream lines from a text file in real-time.
    /// Handles initial history loading, file rotation, and batched updates.
    /// </summary>
    /// <remarks>
    /// CreationTime Tunneling / Unreliable File System Metadata
    /// ------------------------------------------------------------------
    /// On certain file systems (FAT32, Network Shares, NAS) or due to Windows 
    /// "File System Tunneling," the CreationTime might not update if a file is 
    /// deleted and recreated with the same name within the tunneling window 
    /// (default 15s). 
    ///
    /// We use 'Length < lastPosition' as a secondary safety net for truncation.
    /// However, if a rotation occurs and the new file immediately becomes larger 
    /// than the old offset, a rotation might be missed on these platforms.
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
        internal event Action? OnLoopCompleted;

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
        public event NewLinesHandler? OnNewLines;

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
        public async Task RunFromPosition(string? path, LogType type, long startPos, DateTime startCreated, CancellationToken token)
        {
            if (Volatile.Read(ref _isDisposed) != 0) throw new ObjectDisposedException(nameof(LogTailer));

            if (string.IsNullOrEmpty(path)) return;

            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, _disposeCts.Token))
            {
                var linkedToken = linkedCts.Token;

                long lastPosition = startPos;
                DateTime lastCreationTime = startCreated;
                FILE_IDENTITY? knownIdentity = null;
                int consecutiveFailures = 0;

                // Track flush-torn string segments across polling boundaries
                string carryOverFragment = string.Empty;

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
                        FileStream? fs = null;

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
                            info.Refresh();

                            // 1. Initial attach or Post-Rotation setup
                            if (knownIdentity == null)
                            {
                                if (info.CreationTimeUtc != lastCreationTime || info.Length < lastPosition)
                                {
                                    lastPosition = 0;
                                    lastCreationTime = info.CreationTimeUtc;
                                    carryOverFragment = string.Empty; // Wipe state context on rotation
                                    Logger.Debug("[LogTailer] Rotation detected before first open (Metadata fallback).");
                                }
                            }
                            else
                            {
                                // 2. Identity Check: Did the file object on disk swap?
                                // 3. Metadata Check: Even if the identity is the same (truncation), 
                                //    or handle info failed, check for size/time signals of rotation.
                                if (currentIdentity.IsDifferentFrom(knownIdentity.Value) ||
                                    info.CreationTimeUtc != lastCreationTime ||
                                    (lastPosition > 0 && info.Length < lastPosition))
                                {
                                    lastPosition = 0;
                                    lastCreationTime = info.CreationTimeUtc;
                                    carryOverFragment = string.Empty; // Wipe state context on rotation
                                    Logger.Debug("[LogTailer] Rotation or truncation detected on reopen.");
                                }
                            }

                            knownIdentity = currentIdentity;
                            fs.Seek(lastPosition, SeekOrigin.Begin);

                            // Construct the reader specifying a fallback default encoding for files lacking BOM headers
                            using (StreamReader reader = new StreamReader(fs, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                            {
                                try
                                {
                                    while (!linkedToken.IsCancellationRequested)
                                    {
                                        LoopStartedSignal.TrySetResult(true);

                                        List<LogLine> batch = new List<LogLine>();
                                        string? line;

                                        // Capture our starting point position for this polling execution loop
                                        long streamStartOffset = fs.Position;

                                        while ((line = await reader.ReadLineAsync(linkedToken)) != null)
                                        {
                                            consecutiveFailures = 0;

                                            // If the previous pass held back an unterminated segment, prepend it now
                                            if (!string.IsNullOrEmpty(carryOverFragment))
                                            {
                                                line = carryOverFragment + line;
                                                carryOverFragment = string.Empty;
                                            }

                                            // TRACK CORRUPT / FLUSH-TORN LINES PURELY ASYNCHRONOUSLY
                                            // Calculate exactly how many bytes were advanced on disk for this line
                                            long currentPosition = fs.Position;
                                            long bytesConsumed = currentPosition - streamStartOffset;

                                            // Calculate the raw byte weight of the string characters read
                                            int stringByteCount = System.Text.Encoding.UTF8.GetByteCount(line);

                                            // If the stream advanced exactly by the string size (or less due to a missing 
                                            // trailing delimiter), we've caught a live log file mid-flush at EOF.
                                            if (bytesConsumed <= stringByteCount)
                                            {
                                                // Double check if we are truly at the physical boundary of the file length
                                                // before deciding to hold it back as a partial fragment.
                                                if (currentPosition >= fs.Length)
                                                {
                                                    carryOverFragment = line;
                                                    lastPosition = streamStartOffset; // Roll back position pointer
                                                    break;
                                                }
                                            }

                                            batch.Add(new LogLine(line, type));
                                            if (batch.Count >= AppConfig.LogTailerBatchFlushThreshold)
                                            {
                                                OnNewLines?.Invoke(batch);
                                                batch = new List<LogLine>(AppConfig.LogTailerBatchFlushThreshold);
                                            }

                                            // Securely advance our loop tracking state offsets
                                            streamStartOffset = currentPosition;
                                        }

                                        if (batch.Count > 0) OnNewLines?.Invoke(batch);

                                        // --- EOF Reached. Verify File Integrity / Rotation ---
                                        // Only advance lastPosition if we are not currently buffering a flush-torn line fragment.
                                        if (string.IsNullOrEmpty(carryOverFragment))
                                        {
                                            lastPosition = fs.Position;
                                        }

                                        info.Refresh();
                                        bool rotated = false;

                                        if (!info.Exists)
                                        {
                                            rotated = true;
                                            Logger.Debug("[LogTailer] Rotation detected: File no longer exists.");
                                        }
                                        else if (info.CreationTimeUtc != lastCreationTime || info.Length < lastPosition)
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
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        consecutiveFailures++;

                        // CIRCUIT BREAKER: Suppress continuous log spam for recurring permanent failures.
                        if (consecutiveFailures == 1 || consecutiveFailures % AppConfig.LogTailerErrorLogThrottlingInterval == 0)
                        {
                            Logger.Error($"Unexpected error in log tailer for {path} (Concealed Failure Block #{consecutiveFailures}).", ex);
                        }

                        // LINEAR BACKOFF: Progressively scale recovery wait by attempt number, capped at MaxDelay.
                        int delay = Math.Min(AppConfig.LogTailerMaxUnhandledErrorRecoveryDelayMs, AppConfig.LogTailerUnhandledErrorRecoveryDelayMs * consecutiveFailures);
                        await Task.Delay(delay, linkedToken);
                    }
                }
            }
        }

        /// <summary>
        /// Just loads the history and returns the state without starting the tailing loop.
        /// </summary>
        public async Task<HistoryResult?> GetHistoryAsync(string? path, LogType type, int maxLines)
        {
            if (Volatile.Read(ref _isDisposed) != 0) throw new ObjectDisposedException(nameof(LogTailer));
            long pos = 0;
            DateTime created = DateTime.MinValue;
            var lines = await Task.Run(() => LoadHistory(path, type, maxLines, out pos, out created));
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
        /// <param name="creationTime">Outputs the creation time of the file used for rotation detection.</param>
        /// <returns>A list of log lines retrieved from the end of the file.</returns>
        private List<LogLine> LoadHistory(string? path, LogType type, int maxLines, out long finalPos, out DateTime creationTime)
        {
            // Initialize out parameters immediately to satisfy the compiler
            finalPos = 0;
            creationTime = DateTime.MinValue;
            List<LogLine> lines = new List<LogLine>();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return lines;
            }

            maxLines = Math.Min(Math.Max(maxLines, 0), AppConfig.LogTailerMaxSafeLines);

            try
            {
                FileInfo info = new FileInfo(path);
                creationTime = info.CreationTimeUtc;
                DateTime lastWrite = info.LastWriteTimeUtc;

                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    finalPos = fs.Length;
                    if (fs.Length == 0) return lines;

                    // Pre-increment the line count if the file does not end with a trailing newline.
                    // This ensures the backward scanner accurately bounds the "last N lines" even when
                    // catching a live log file mid-flush.
                    fs.Seek(-1, SeekOrigin.End);
                    int lastByte = fs.ReadByte();
                    int count = (lastByte == (byte)'\n') ? 0 : 1;

                    long pos = fs.Length;
                    byte[] buffer = new byte[4096];

                    // Backwards scan for newline characters to locate the start of the last 'maxLines'
                    while (pos > 0 && count <= maxLines)
                    {
                        int toRead = (int)Math.Min(pos, buffer.Length);
                        pos -= toRead;
                        fs.Seek(pos, SeekOrigin.Begin);
                        fs.ReadExactly(buffer, 0, toRead);
                        for (int i = toRead - 1; i >= 0; i--)
                        {
                            if (buffer[i] == (byte)'\n')
                            {
                                count++;
                                if (count > maxLines) { pos = pos + i + 1; break; }
                            }
                        }
                    }

                    // Read forward from the discovered position
                    fs.Seek(pos, SeekOrigin.Begin);
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        string? line;
                        var tempLines = new List<string>();
                        while ((line = sr.ReadLine()) != null && tempLines.Count < maxLines)
                        {
                            tempLines.Add(line);
                        }

                        // We work backwards from the LastWriteTime
                        // Every line gets exactly 1 tick less than the one after it
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
            catch (FileNotFoundException)
            {
                // Handle the race condition where file existed a moment ago but is gone now
                return lines;
            }
            catch (DirectoryNotFoundException)
            {
                return lines;
            }

            return lines;
        }

        /// <summary>
        /// Releases resources and detaches event handlers to prevent memory leaks.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of the Dispose pattern.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; 
        /// <c>false</c> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;

            if (disposing)
            {
                // 1. Break the strong reference to the subscriber
                OnNewLines = null;

                // 2. CRITICAL: Cancel the internal token to instantly kill the while-loop 
                // and release any active FileStreams or Task.Delays.
                try
                {
                    if (!_disposeCts.IsCancellationRequested)
                    {
                        _disposeCts.Cancel();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"LogTailer.Dispose: _disposeCts.Cancel() threw ({ex.Message}); proceeding to Dispose.");
                }
                finally
                {
                    try { _disposeCts.Dispose(); }
                    catch (Exception ex)
                    {
                        Logger.Debug($"LogTailer.Dispose: _disposeCts.Dispose() threw ({ex.Message}); ignoring.");
                    }
                }
            }
        }
    }
}