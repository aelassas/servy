using Servy.Manager.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Servy.Manager.UnitTests.Utils
{
    /// <summary>
    /// Unit tests for <see cref="LogLineSplitter"/>, the byte-level line splitter the live tailer
    /// reads through.
    /// </summary>
    public class LogLineSplitterTests
    {
        /// <summary>
        /// Drains a splitter of every line the given bytes complete.
        /// </summary>
        /// <param name="splitter">The splitter under test.</param>
        /// <param name="bytes">The bytes to feed, as one read.</param>
        /// <returns>The lines completed by this read, in order.</returns>
        private static List<string> ReadAll(LogLineSplitter splitter, byte[] bytes)
        {
            var lines = new List<string>();
            int index = 0;
            string line;

            while (splitter.TryReadLine(bytes, bytes.Length, ref index, out line))
            {
                lines.Add(line);
            }

            return lines;
        }

        /// <summary>
        /// The defect of #7333: a reader that reached the end of a live log file inside a line returned
        /// the prefix as a finished line, and the remainder the writer appended a moment later came back
        /// as a second one. Splitting on the terminator makes the two reads produce one line.
        /// </summary>
        [Fact]
        public void TryReadLine_LineCompletedAcrossTwoReads_ReturnsItOnceWhole()
        {
            // Arrange
            var splitter = new LogLineSplitter(Encoding.UTF8);

            // Act
            var firstRead = ReadAll(splitter, Encoding.UTF8.GetBytes("complete\npartial-"));
            int pendingAfterFirstRead = splitter.PendingByteCount;
            string peekedAfterFirstRead = splitter.PeekPending();
            var secondRead = ReadAll(splitter, Encoding.UTF8.GetBytes("remainder\n"));

            // Assert
            Assert.Equal(new[] { "complete" }, firstRead);
            Assert.Equal("partial-".Length, pendingAfterFirstRead);
            Assert.Equal("partial-", peekedAfterFirstRead);
            Assert.Equal(new[] { "partial-remainder" }, secondRead);
            Assert.Equal(0, splitter.PendingByteCount);
        }

        /// <summary>
        /// Servy writes its logs with CRLF endings, so the carriage return belongs to the terminator
        /// and not to the line, exactly as StreamReader.ReadLine treated it.
        /// </summary>
        [Fact]
        public void TryReadLine_CarriageReturnLineFeedEndings_StripsTheCarriageReturn()
        {
            // Arrange
            var splitter = new LogLineSplitter(Encoding.UTF8);

            // Act
            var lines = ReadAll(splitter, Encoding.UTF8.GetBytes("first\r\n\r\nsecond\r\n"));

            // Assert
            Assert.Equal(new[] { "first", string.Empty, "second" }, lines);
        }

        /// <summary>
        /// The held-back fragment is kept as bytes, so a multi-byte character cut across two reads is
        /// decoded once, whole. Carrying it as decoded text would have produced two replacement
        /// characters instead.
        /// </summary>
        [Fact]
        public void TryReadLine_MultiByteCharacterSplitAcrossTwoReads_DecodesItWhole()
        {
            // Arrange
            var splitter = new LogLineSplitter(Encoding.UTF8);
            byte[] encoded = Encoding.UTF8.GetBytes("caf\u00e9\n");
            byte[] head = new byte[4];
            byte[] tail = new byte[encoded.Length - 4];
            Array.Copy(encoded, 0, head, 0, head.Length);
            Array.Copy(encoded, head.Length, tail, 0, tail.Length);

            // Act
            var firstRead = ReadAll(splitter, head);
            var secondRead = ReadAll(splitter, tail);

            // Assert
            Assert.Empty(firstRead);
            Assert.Equal(new[] { "caf\u00e9" }, secondRead);
        }

        /// <summary>
        /// The split is on the ENCODED newline sequence, so a 0x0A byte that is only half of a UTF-16
        /// code unit does not end a line. Splitting on the raw byte would have cut every line of a
        /// UTF-16 log in two.
        /// </summary>
        [Fact]
        public void TryReadLine_Utf16Encoding_SplitsOnTheEncodedTerminatorOnly()
        {
            // Arrange
            var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
            var splitter = new LogLineSplitter(encoding);

            // Act
            var lines = ReadAll(splitter, encoding.GetBytes("alpha\nbeta\n"));

            // Assert
            Assert.Equal(new[] { "alpha", "beta" }, lines);
        }

        /// <summary>
        /// The defect of #7379: the terminator was matched at any byte offset, so in a UTF-16 or UTF-32
        /// log the encoded newline's bytes occurring across two adjacent characters ended a line in the
        /// middle of a character. The match must end on a code-unit boundary.
        /// </summary>
        /// <param name="encodingName">The encoding the row runs under.</param>
        /// <param name="text">A single line whose bytes carry the terminator's bytes out of phase.</param>
        [Theory]
        [InlineData("utf-16LE", "\u0a15\u4e00 ok")]
        [InlineData("utf-16BE", "\u0100\u0a15 ok")]
        [InlineData("utf-32LE", "\u0a15\u0100 ok")]
        public void TryReadLine_NewlineBytesStraddlingTwoCharacters_DoNotEndTheLine(string encodingName, string text)
        {
            // Arrange
            Encoding encoding;
            switch (encodingName)
            {
                case "utf-16LE":
                    encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
                    break;
                case "utf-16BE":
                    encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
                    break;
                case "utf-32LE":
                    encoding = new UTF32Encoding(bigEndian: false, byteOrderMark: false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(encodingName), encodingName, null);
            }

            var splitter = new LogLineSplitter(encoding);

            // Act
            var lines = ReadAll(splitter, encoding.GetBytes(text + "\n"));

            // Assert
            Assert.Equal(new[] { text }, lines);
            Assert.Equal(0, splitter.PendingByteCount);
        }

        /// <summary>
        /// The tailing loop resets the splitter when the file rotated or was truncated, because the
        /// bytes read before the swap no longer belong to the line being assembled.
        /// </summary>
        [Fact]
        public void Reset_PendingFragment_IsDiscarded()
        {
            // Arrange
            var splitter = new LogLineSplitter(Encoding.UTF8);
            ReadAll(splitter, Encoding.UTF8.GetBytes("stale-"));

            // Act
            splitter.Reset();
            var lines = ReadAll(splitter, Encoding.UTF8.GetBytes("fresh\n"));

            // Assert
            Assert.Equal(0, splitter.PendingByteCount);
            Assert.Equal(new[] { "fresh" }, lines);
        }
    }
}
