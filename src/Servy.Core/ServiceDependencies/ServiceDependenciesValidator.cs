using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Resources;

namespace Servy.Core.ServiceDependencies
{
    /// <summary>
    /// Validates user-entered Windows service dependency lists (semicolon- or newline-separated service names).
    /// </summary>
    public static class ServiceDependenciesValidator
    {
        /// <summary>
        /// Validates the input string containing service dependencies.
        /// Service names must be separated by semicolons or new lines.
        /// Each service name must satisfy the shared service name character set rules,
        /// optionally preceded by '+' to reference a load-order group,
        /// and must not exceed 256 characters.
        /// </summary>
        /// <param name="input">Raw input string with service dependencies.</param>
        /// <param name="errors">List of validation error messages.</param>
        /// <returns>True if all service names are valid; otherwise false.</returns>
        public static bool Validate(string? input, out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(input))
            {
                // No dependencies is valid (empty)
                return true;
            }

            // Validate each dependency name produced by the shared tokenizer.
            foreach (string serviceName in ServiceDependenciesParser.Tokenize(input))
            {
                if (serviceName.Length > AppConfig.MaxServiceNameLength)
                {
                    errors.Add(string.Format(Strings.Msg_ServiceDependencyNameLengthReachedForName, serviceName, AppConfig.MaxServiceNameLength));
                    continue;
                }

                string nameToValidate = serviceName.StartsWith("+", StringComparison.Ordinal)
                    ? serviceName.Substring(1)
                    : serviceName;

                if (string.IsNullOrEmpty(nameToValidate) || !Helper.IsValidServiceNameCharset(nameToValidate))
                {
                    errors.Add(string.Format(Strings.Msg_InvalidServiceDependencyName, serviceName));
                }
            }

            return errors.Count == 0;
        }
    }
}
