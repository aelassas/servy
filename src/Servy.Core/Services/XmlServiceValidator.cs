using Servy.Core.DTOs;
using Servy.Core.Validators;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Servy.Core.Services
{
    /// <summary>
    /// Validates XML input to ensure it can be deserialized into a <see cref="ServiceDto"/>
    /// and meets strict Windows SCM and security rules before database persistence.
    /// </summary>
    public class XmlServiceValidator : ServiceDtoImportValidator<Exception>, IXmlServiceValidator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XmlServiceValidator"/> class.
        /// </summary>
        /// <param name="serviceValidationRules">Provides rules for validating service properties.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceValidationRules"/> is null.</exception>
        public XmlServiceValidator(IServiceValidationRules serviceValidationRules)
            : base(serviceValidationRules)
        {
        }

        /// <inheritdoc/>
        protected override string FormatName => "XML";

        /// <inheritdoc/>
        protected override ServiceDto? Parse(string content)
        {
            // 1. Prevent XXE Attacks
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            var serializer = new XmlSerializer(typeof(ServiceDto));
            using (var stringReader = new StringReader(content))
            using (var xmlReader = XmlReader.Create(stringReader, settings))
            {
                return serializer.Deserialize(xmlReader) as ServiceDto;
            }
        }
    }
}