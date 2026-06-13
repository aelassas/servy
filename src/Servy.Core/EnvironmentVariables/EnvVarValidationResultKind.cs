namespace Servy.Core.EnvironmentVariables
{
    /// <summary>
    /// Specifies the structural failure kind encountered during environment variable record processing.
    /// </summary>
    public enum EnvVarValidationResultKind
    {
        /// <summary>The record parsed and validated successfully.</summary>
        Success = 0,

        /// <summary>The record is missing an unescaped equals assignment operator.</summary>
        MissingEquals = 1,

        /// <summary>The variable record contains an empty or whitespace-only key identifier.</summary>
        EmptyKey = 2,

        /// <summary>The variable record value carries unescaped literal newline characters.</summary>
        ForbiddenNewline = 3,

        /// <summary>A generic or unmapped validation failure occurred.</summary>
        GeneralFailure = 4
    }
}