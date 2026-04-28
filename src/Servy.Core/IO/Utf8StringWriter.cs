using System.IO;
using System.Text;

namespace Servy.Core.IO
{
    /// <summary>
    /// A specialized <see cref="StringWriter"/> that reports its encoding as UTF-8.
    /// </summary>
    /// <remarks>
    /// By default, <see cref="StringWriter"/> reports <see cref="Encoding.Unicode"/> (UTF-16).
    /// This causes <see cref="XmlSerializer"/> to generate a preamble with 'encoding="utf-16"'.
    /// Using this class ensures the preamble matches the UTF-8 bytes written to disk by the CLI.
    /// </remarks>
    public sealed class Utf8StringWriter : StringWriter
    {
        /// <summary>
        /// Gets the <see cref="Encoding"/> in which the output is written.
        /// </summary>
        public override Encoding Encoding => Encoding.UTF8;
    }
}
