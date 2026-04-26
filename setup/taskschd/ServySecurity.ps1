#Requires -Version 3.0
<#
.SYNOPSIS
    Masks sensitive credentials and keys in a given text string.

.DESCRIPTION
    Uses a lookaround-based regular expression to identify and mask sensitive 
    configuration keys or environment variable names without destroying the 
    surrounding text or original separators. 
    
    Maintained in strict parity with the Servy.Core C# MaskingRegex implementation 
    to ensure logs and email notifications have identical redaction behavior.

.PARAMETER Text
    The raw string (e.g., an email body, notification text, or log message) to be scrubbed.

.EXAMPLE
    $safeBody = Protect-SensitiveString -Text "API_KEY: my-secret-token"
    # Returns: "API_KEY: ********"

.NOTES
    Author      : Akram El Assas
    Project     : Servy
#>
function Protect-SensitiveString {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$false, ValueFromPipeline=$true)]
        [string]$Text
    )
    
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Text }

    # A collection of keywords used to identify potentially sensitive information.
    $sensitiveKeys = @(
        # --- Core Credentials ---
        "PASSWORD", "PWD", "PASSPHRASE", "PIN", "USERPWD",

        # --- Web & Mobile Auth (JWT/OAuth) ---
        "TOKEN", "AUTH", "CREDENTIAL", "BEARER", "JWT",
        "SESSION", "COOKIE", "CLIENT_SECRET",

        # --- Cloud & Infrastructure (AWS/Azure/GCP) ---
        "SECRET", "SAS", "ACCOUNTKEY", "ACCESSKEY", "SKEY",
        "SIGNATURE", "TENANT_ID",

        # --- Databases & Storage ---
        "CONNECTIONSTRING", "CONNSTR", "DSN", "DATABASE_URL",
        "PROVIDER_CONNECTION_STRING",

        # --- Cryptography & Identity ---
        "KEY", "PRIVATE", "CERTIFICATE", "CERT", "THUMBPRINT",
        "PFX", "PEM", "SALT", "PEPPER",

        # --- API Service Identifiers ---
        "API", "APP_SECRET", "BROWSER_KEY", "WEBHOOK_URL"
    )

    $keyPattern = $sensitiveKeys -join '|'
    
    # $1: Keyword (using lookarounds to allow _, -, and . as boundaries)
    # $2: Separator (preserves the original : or =)
    # Matches unquoted strings (\S+) OR quoted strings ("..." / '...')
    $maskingRegex = "(?i)(?<![a-zA-Z0-9])($keyPattern)(?![a-zA-Z0-9])(\s*[:=]\s*)(?:`"[^`"]*`"|'[^']*'|\S+)"

    return $Text -replace $maskingRegex, '$1$2********'
}