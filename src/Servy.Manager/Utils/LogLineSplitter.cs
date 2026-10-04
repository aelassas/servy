using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Servy.Manager.Utils
{
    /// <summary>
    /// Splits raw log-file bytes into terminated lines, holding back the unterminated bytes that
    /// follow the last terminator until the bytes completing them are fed.
    /// </summary>
    /// <remarks>
    /// This type exists because <see cref="StreamReader.ReadLine"/> discards the one fact the
    /// tailer needs: whether the text it returned was followed by a terminator. A reader that
    /// reaches the end of a live log file inside a line returns the prefix as an ordinary line, and
    /// the remainder the writer appends a moment later arrives as a second one, so a single log line
    /// is shown torn in two. Splitting on the encoded newline sequence makes "terminated" a property
    /// of each line as it is produced, which no probe against the file can recover afterwards.
    /// <para>
    /// The held-back fragment is kept as BYTES rather than as decoded text, so a multi-byte
    /// character cut across two reads is decoded once, whole.
    /// </para>
    /// <para>
    /// Instances are not thread-safe; the tailing loop owns one and feeds it from a single task.
    /// </para>
    /// </remarks>
    internal sealed class LogLineSplitter
    {
        /// <summary>
        /// The encoding used to decode a completed line and to spell the line terminators.
        /// </summary>
        private readonly Encoding _encoding;

        /// <summary>
        /// The encoded form of the line feed, the sequence that terminates a line.
        /// </summary>
        private readonly byte[] _newline;

        /// <summary>
        /// The encoded form of the carriage return, stripped from the end of a completed line.
        /// </summary>
        private readonly byte[] _carriageReturn;

        /// <summary>
        /// The bytes read since the last terminator, that is the unterminated trailing fragment.
        /// </summary>
        private readonly List<byte> _pending = new List<byte>();

        /// <summary>
        /// Initializes a new instance of the <see cref="LogLineSplitter"/> class.
        /// </summary>
        /// <param name="encoding">The encoding used to decode lines and to encode the terminators.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="encoding"/> is <c>null</c>.</exception>
        internal LogLineSplitter(Encoding encoding)
        {
            if (encoding == null) throw new ArgumentNullException(nameof(encoding));

            _encoding = encoding;
            _newline = encoding.GetBytes("\n");
            _carriageReturn = encoding.GetBytes("\r");
        }

        /// <summary>
        /// Gets the number of bytes currently held back as the unterminated trailing fragment.
        /// </summary>
        /// <returns>The pending byte count, <c>0</c> when the last byte fed completed a line.</returns>
        internal int PendingByteCount
        {
            get { return _pending.Count; }
        }

        /// <summary>
        /// Consumes bytes from <paramref name="buffer"/> until one line is complete.
        /// </summary>
        /// <remarks>
        /// Bytes that do not complete a line stay pending and are prepended to the next call, so a
        /// line split across two reads is returned once, whole, by the call that terminates it. The
        /// caller loops until this returns <c>false</c>, at which point <paramref name="index"/> has
        /// reached <paramref name="count"/>; <paramref name="index"/> then also marks, relative to
        /// the buffer, the first byte of the held-back fragment.
        /// </remarks>
        /// <param name="buffer">The buffer holding the bytes read from the log file.</param>
        /// <param name="count">The number of valid bytes in <paramref name="buffer"/>, from index 0.</param>
        /// <param name="index">
        /// The position to resume from, advanced past the bytes consumed, terminator included.
        /// </param>
        /// <param name="line">
        /// The completed line, without its terminator, or <see cref="string.Empty"/> when the buffer
        /// ran out before one completed. Empty is also a valid completed line, so the return value
        /// is what distinguishes the two.
        /// </param>
        /// <returns><c>true</c> when a line was completed; <c>false</c> when the buffer ran out first.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="count"/> is negative or exceeds the length of
        /// <paramref name="buffer"/>, or when <paramref name="index"/> is negative.
        /// </exception>
        internal bool TryReadLine(byte[] buffer, int count, ref int index, out string line)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (count < 0 || count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));

            while (index < count)
            {
                _pending.Add(buffer[index]);
                index++;

                // The encoded newline is one code unit long and the fragment always starts on a code-unit
                // boundary, so a match that does not end on one straddles two characters and is not a
                // terminator. In UTF-16 LE, for example, the 0A 00 pair also occurs between a character in
                // U+0A00-U+0AFF and a following U+xx00 one. For UTF-8 the length is 1 and nothing changes.
                if (_pending.Count % _newline.Length == 0 && EndsWith(_newline, _pending.Count))
                {
                    line = DecodeCompletedLine();
                    _pending.Clear();
                    return true;
                }
            }

            line = string.Empty;
            return false;
        }

        /// <summary>
        /// Decodes the unterminated trailing fragment without consuming it.
        /// </summary>
        /// <returns>The pending fragment as text; <see cref="string.Empty"/> when nothing is pending.</returns>
        internal string PeekPending()
        {
            return _pending.Count == 0 ? string.Empty : _encoding.GetString(_pending.ToArray(), 0, _pending.Count);
        }

        /// <summary>
        /// Discards the unterminated trailing fragment.
        /// </summary>
        /// <remarks>
        /// Called when the file rotated or was truncated, so the bytes read before the swap no
        /// longer belong to the line being assembled.
        /// </remarks>
        internal void Reset()
        {
            _pending.Clear();
        }

        /// <summary>
        /// Decodes the pending bytes as a finished line, dropping the terminator and one optional
        /// preceding carriage return.
        /// </summary>
        /// <returns>The decoded line, without its terminator.</returns>
        private string DecodeCompletedLine()
        {
            int length = _pending.Count - _newline.Length;

            if (EndsWith(_carriageReturn, length))
            {
                length -= _carriageReturn.Length;
            }

            return length <= 0 ? string.Empty : _encoding.GetString(_pending.ToArray(), 0, length);
        }

        /// <summary>
        /// Tests whether the first <paramref name="length"/> pending bytes end with the given sequence.
        /// </summary>
        /// <param name="sequence">The encoded sequence to look for.</param>
        /// <param name="length">The number of leading pending bytes to consider.</param>
        /// <returns><c>true</c> when those bytes end with <paramref name="sequence"/>; otherwise <c>false</c>.</returns>
        private bool EndsWith(byte[] sequence, int length)
        {
            if (sequence.Length == 0 || length < sequence.Length) return false;

            for (int i = 0; i < sequence.Length; i++)
            {
                if (_pending[length - sequence.Length + i] != sequence[i]) return false;
            }

            return true;
        }
    }
}
