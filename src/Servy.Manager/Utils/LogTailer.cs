using Servy.Manager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Manager.Utils
{
    /// <summary>
    /// Provides functionality to monitor and stream lines from a text file in real-time.
    /// Handles initial history loading, file rotation, and batched updates.
    /// </summary>
    public class LogTailer
    {
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
        /// Starts a continuous tailing loop for a specific file, beginning at a designated position.
        /// This method handles file rotation detection and batched UI updates.
        /// </summary>
        /// <param name="path">The full filesystem path to the log file.</param>
        /// <param name="type">The stream type (StdOut/StdErr) used for UI color-coding.</param>
        /// <param name="startPos">The byte offset from which to start reading (usually the end of the history load).</param>
        /// <param name="startCreated">The creation timestamp of the file when history was loaded, used to detect rotation.</param>
        /// <param name="token">A token used to stop the tailing loop when switching services or closing the app.</param>
        /// <returns>A Task representing the long-running polling operation.</returns>
        public async Task RunFromPosition(string path, LogType type, long startPos, DateTime startCreated, CancellationToken token)
        {
            if (string.IsNullOrEmpty(path)) return;

            long lastPosition = startPos;
            DateTime lastCreationTime = startCreated;

            // Tailing Loop: Continuously poll the file for new content
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        await Task.Delay(1000, token);
                        continue;
                    }

                    FileInfo info = new FileInfo(path);

                    // Rotation Detection: Check if file was replaced or truncated (e.g., log rotate or 'echo "" > file')
                    if (info.CreationTimeUtc != lastCreationTime || info.Length < lastPosition)
                    {
                        lastPosition = 0;
                        lastCreationTime = info.CreationTimeUtc;
                    }

                    // FileShare.ReadWrite is critical here to allow the service to keep writing while we read
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        fs.Seek(lastPosition, SeekOrigin.Begin);
                        using (StreamReader reader = new StreamReader(fs))
                        {
                            while (!token.IsCancellationRequested)
                            {
                                info.Refresh();
                                // If file changed identity while we were reading, break to reopen the stream
                                if (info.CreationTimeUtc != lastCreationTime || info.Length < lastPosition)
                                    break;

                                List<LogLine> batch = new List<LogLine>();
                                string line;
                                while ((line = await reader.ReadLineAsync()) != null)
                                {
                                    var logLine = new LogLine(line, type);
                                    batch.Add(logLine);

                                    // Batching prevents the UI from being flooded with thousands of individual updates
                                    if (batch.Count >= 500)
                                    {
                                        OnNewLines?.Invoke(batch);
                                        batch = new List<LogLine>();
                                    }
                                    lastPosition = fs.Position;
                                }

                                if (batch.Count > 0) OnNewLines?.Invoke(batch);

                                // Polling interval to balance CPU usage vs latency
                                await Task.Delay(150, token);
                            }
                        }
                    }
                }
                catch (IOException) { await Task.Delay(500, token); }
                catch (Exception) { await Task.Delay(1000, token); }
            }
        }

        /// <summary>
        /// Just loads the history and returns the state without starting the tailing loop.
        /// </summary>
        public async Task<HistoryResult> GetHistoryAsync(string path, LogType type, int maxLines)
        {
            long pos = 0;
            DateTime created = DateTime.MinValue;
            var lines = await Task.Run(() => LoadHistory(path, type, maxLines, out pos, out created));
            return new HistoryResult(lines, pos, created);
        }

        /// <summary>
        /// Reads the tail end of a file to provide historical context when the console is first opened.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="type">The log type for the resulting <see cref="LogLine"/> objects.</param>
        /// <param name="maxLines">Maximum number of historical lines to retrieve.</param>
        /// <param name="finalPos">Outputs the file position where the history ended (to start tailing from).</param>
        /// <param name="creationTime">Outputs the creation time of the file used for rotation detection.</param>
        /// <returns>A list of log lines retrieved from the end of the file.</returns>
        private List<LogLine> LoadHistory(string path, LogType type, int maxLines, out long finalPos, out DateTime creationTime)
        {
            List<LogLine> lines = new List<LogLine>();
            FileInfo info = new FileInfo(path);
            creationTime = info.CreationTimeUtc;
            DateTime lastWrite = info.LastWriteTimeUtc;

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                finalPos = fs.Length;
                if (fs.Length == 0) return lines;

                long pos = fs.Length;
                int count = 0;
                byte[] buffer = new byte[4096];

                // Backwards scan for newline characters to locate the start of the last 'maxLines'
                while (pos > 0 && count <= maxLines)
                {
                    int toRead = (int)Math.Min(pos, buffer.Length);
                    pos -= toRead;
                    fs.Seek(pos, SeekOrigin.Begin);
                    fs.Read(buffer, 0, toRead);
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
                    string line;
                    var tempLines = new List<string>();
                    while ((line = sr.ReadLine()) != null) tempLines.Add(line);

                    // We work backwards from the LastWriteTime
                    // Every line gets 1 tick less than the one after it
                    for (int i = 0; i < tempLines.Count; i++)
                    {
                        // Logic: The very last line in the file is 'lastWrite'
                        // Every line before it is 1 tick older.
                        int offset = tempLines.Count - i;
                        DateTime syntheticTime = lastWrite.AddTicks(-offset);

                        lines.Add(new LogLine(tempLines[i], type, syntheticTime));
                    }
                }
                return lines;
            }
        }
    }
}