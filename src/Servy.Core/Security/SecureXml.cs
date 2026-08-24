using Servy.Core.DTOs;
using Servy.Core.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

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
        /// Dynamically resolves all property names on <see cref="ServiceDto"/> decorated with <see cref="XmlIgnoreAttribute"/>.
        /// These element names are safely skipped during deserialization to match JSON import behavior.
        /// </summary>
        private static readonly HashSet<string> KnownIgnoredElements = new HashSet<string>(
            typeof(ServiceDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<XmlIgnoreAttribute>() != null)
                .Select(p => p.Name),
            StringComparer.Ordinal
        );

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

        /// <summary>
        /// Creates an <see cref="XmlSerializer"/> configured with strict unknown element and unknown attribute handlers
        /// to prevent unmapped or unexpected XML payloads from deserializing into a <see cref="ServiceDto"/>.
        /// Properties decorated with <see cref="XmlIgnoreAttribute"/> (such as credentials and runtime state) are ignored.
        /// </summary>
        /// <returns>A new <see cref="XmlSerializer"/> instance configured with strict parsing rules.</returns>
        public static XmlSerializer CreateStrictServiceDtoSerializer()
        {
            var serializer = new XmlSerializer(typeof(ServiceDto));

            serializer.UnknownElement += (sender, e) =>
            {
                if (KnownIgnoredElements.Contains(e.Element.Name))
                {
                    return;
                }

                throw new XmlException(string.Format(Strings.Msg_UnknownXmlElement, e.Element.Name), null, e.LineNumber, e.LinePosition);
            };
            serializer.UnknownAttribute += (sender, e) =>
            {
                throw new XmlException(string.Format(Strings.Msg_UnknownXmlAttribute, e.Attr.Name), null, e.LineNumber, e.LinePosition);
            };

            return serializer;
        }
    }
}
