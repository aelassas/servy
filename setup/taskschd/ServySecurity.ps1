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

.EXAMPLE
    $safeBody = Protect-SensitiveString -Text "myapp.exe --password mysecret"
    # Returns: "myapp.exe --password ********"

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

        # --- Web & Mobile Auth (JWT/OAuth/Personal Tokens) ---
        "TOKEN", "AUTH", "CREDENTIAL", "BEARER", "JWT",
        "SESSION", "COOKIE", "CLIENT_SECRET", "PAT",

        # --- Cloud & Infrastructure (AWS/Azure/GCP) ---
        "SECRET", "SAS", "ACCOUNTKEY", "ACCESSKEY", "SKEY",
        "SIGNATURE", "TENANT_ID",

        # --- Databases & Storage ---
        "CONNECTIONSTRING", "CONNSTR", "DSN", "DATABASE_URL",
        "PROVIDER_CONNECTION_STRING", "DATABASE_PASSWORD",

        # --- Cryptography & Identity (Specific KEY variants) ---
        "PRIVATE_KEY", "SSH_KEY", "SECRET_KEY", "API_KEY",
        "CERTIFICATE", "CERT", "THUMBPRINT", "PFX", "PEM", "SALT", "PEPPER",

        # --- API & Integration Tokens ---
        "API", "APP_SECRET", "BROWSER_KEY", "WEBHOOK_URL",
        "KUBE_CONFIG", "TELEGRAM_TOKEN", "DISCORD_TOKEN"
    )

    $keyPattern = [string]::Join('|', ($sensitiveKeys | ForEach-Object { [regex]::Escape($_) }))
    
    # Constructed safely using concatenation to avoid multi-line string whitespace issues
    $regexPattern = "(?i)(?<![a-zA-Z0-9])($keyPattern)(?![a-zA-Z0-9])" +
        "(?:" +
            # BRANCH A: Explicit Separators (:, =, /)
            "(\s*[:=]\s*|/)" +
            "(?:`"[^`"]*`"|'[^']*'|(?:[^\s`"']+(?:\s+(?![\-/]+[a-zA-Z])[^\s`"']+)*))" +
            "|" +
            # BRANCH B: Space Separator
            "(\s+)(?![\-/]+[a-zA-Z])" +
            "(?:`"[^`"]*`"|'[^']*'|[^\s`"']+)" +
        ")"

    $maskingRegex = New-Object System.Text.RegularExpressions.Regex (
        $regexPattern,
        [System.Text.RegularExpressions.RegexOptions]::None,
        [TimeSpan]::FromMilliseconds(200)
    )

    # Use MatchEvaluator to conditionally extract the matched separator group (A or B)
    $evaluator = [System.Text.RegularExpressions.MatchEvaluator] {
        param($m)
        $sep = if ($m.Groups[2].Success) { $m.Groups[2].Value } else { $m.Groups[3].Value }
        return "$($m.Groups[1].Value)$sep********"
    }

    return $maskingRegex.Replace($Text, $evaluator)
}