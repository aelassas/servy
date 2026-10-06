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
            var secondRead = ReadAll(splitter, Encoding.UTF8.GetBytes("remainder\n"));

            // Assert
            Assert.Equal(new[] { "complete" }, firstRead);
            Assert.Equal("partial-".Length, pendingAfterFirstRead);
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
        /// The constructor rejects a null encoding by name, so a caller that forgot to resolve one fails
        /// at construction and not later inside the first read.
        /// </summary>
        [Fact]
        public void Constructor_NullEncoding_ThrowsArgumentNullException()
        {
            // Arrange
#pragma warning disable CS8625
            Encoding encoding = null;
#pragma warning restore CS8625

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new LogLineSplitter(encoding));

            // Assert
            Assert.Equal("encoding", ex.ParamName);
        }

        /// <summary>
        /// The argument guards are the contract the tailer relies on when it passes its read count
        /// straight through. Each row trips a different guard, and the parameter name is what tells the
        /// guards apart: ThrowsAny covers both the null and the range exception types.
        /// </summary>
        /// <param name="nullBuffer">Whether to pass a null buffer instead of a 4-byte one.</param>
        /// <param name="count">The count passed to the call.</param>
        /// <param name="index">The starting index passed to the call.</param>
        /// <param name="expectedParam">The parameter the thrown exception must name.</param>
        [Theory]
        [InlineData(true, 0, 0, "buffer")]
        [InlineData(false, -1, 0, "count")]
        [InlineData(false, 5, 0, "count")]
        [InlineData(false, 2, -1, "index")]
        public void TryReadLine_InvalidArguments_ThrowsNamingTheParameter(bool nullBuffer, int count, int index, string expectedParam)
        {
            // Arrange
            var splitter = new LogLineSplitter(Encoding.UTF8);
#pragma warning disable CS8600
            byte[] buffer = nullBuffer ? null : new byte[4];
#pragma warning restore CS8600

            // Act
            var ex = Assert.ThrowsAny<ArgumentException>(() =>
            {
                int position = index;
                string line;
#pragma warning disable CS8604
                splitter.TryReadLine(buffer, count, ref position, out line);
#pragma warning restore CS8604
            });

            // Assert
            Assert.Equal(expectedParam, ex.ParamName);
        }

        /// <summary>
        /// A log written with LF endings holds an empty line as a bare 0x0A at the start of a line, so
        /// the carriage-return probe sees fewer bytes than the sequence is long and must not match.
        /// </summary>
        [Fact]
        public void TryReadLine_BareLineFeedAtLineStart_ReturnsAnEmptyLine()
        {
            // Arrange
            var splitter = new LogLineSplitter(Encoding.UTF8);

            // Act
            var lines = ReadAll(splitter, Encoding.UTF8.GetBytes("\nnext\n"));

            // Assert
            Assert.Equal(new[] { string.Empty, "next" }, lines);
        }
    }
}
