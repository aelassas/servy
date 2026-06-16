using System.Xml;

namespace Servy.Core.Security
{
    /// <summary>
    /// Provides security-hardened XML processing utilities.
    /// This class enforces strict DTD prohibition and disables external entity resolution
    /// to prevent XXE (XML External Entity) injection attacks.
    /// </summary>
    internal static class SecureXml
    {
        /// <summary>
        /// Configures <see cref="XmlReaderSettings"/> to disable DTD processing and external entity resolution.
        /// </summary>
        private static readonly XmlReaderSettings ReaderSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        /// <summary>
        /// Creates a security-hardened <see cref="XmlReader"/> from the provided <see cref="TextReader"/>.
        /// </summary>
        /// <param name="input">The <see cref="TextReader"/> containing the XML source.</param>
        /// <returns>A new <see cref="XmlReader"/> instance configured with restricted security settings.</returns>
        /// <remarks>
        /// This reader is configured to strictly prohibit DTDs and block external resolvers, 
        /// making it suitable for processing untrusted or externally sourced XML input.
        /// </remarks>
        public static XmlReader CreateReader(TextReader input) => XmlReader.Create(input, ReaderSettings);
    }
}
