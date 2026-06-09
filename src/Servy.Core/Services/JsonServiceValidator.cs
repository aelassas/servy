using Newtonsoft.Json;
using Servy.Core.DTOs;
using Servy.Core.Security;
using Servy.Core.Validation;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides strict validation for JSON strings representing a <see cref="ServiceDto"/>.
    /// Ensures both structural integrity and Windows SCM compatibility.
    /// </summary>
    public class JsonServiceValidator : ServiceDtoImportValidator<JsonException>, IJsonServiceValidator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonServiceValidator"/> class.
        /// </summary>
        /// <param name="serviceValidationRules">Provides rules for validating service properties.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceValidationRules"/> is null.</exception>
        public JsonServiceValidator(IServiceValidationRules serviceValidationRules)
            : base(serviceValidationRules)
        {
        }

        /// <inheritdoc/>
        protected override string FormatName => "JSON";

        /// <inheritdoc/>
        protected override ServiceDto? Parse(string content)
        {
            return JsonConvert.DeserializeObject<ServiceDto>(content, JsonSecurity.UntrustedDataSettings);
        }
    }
}