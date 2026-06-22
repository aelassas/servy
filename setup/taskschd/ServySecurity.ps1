#Requires -Version 5.1
<#
.SYNOPSIS
    Masks sensitive credentials and keys in a given text string.

.DESCRIPTION
    Uses a lookaround-based regular expression to identify and mask sensitive 
    configuration keys or environment variable names without destroying the 
    surrounding text or original separators. 
    
    Maintained in strict parity with the Servy.Service C# MaskingRegex implementation 
    (src/Servy.Service/Helpers/ServiceHelper.cs) to ensure logs and email notifications 
    have identical redaction behavior.

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
        [Parameter(Mandatory=$false)]
        [string]$Text
    )
    
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Text }

    # A collection of keywords used to identify potentially sensitive information.
    #
    # WARNING: keep in sync with the parity twin in:
    #   src/Servy.Service/Helpers/ServiceHelper.cs (SensitiveKeyWords) - same keyword-pattern masker.
    #
    # NOTE: src/Servy.CLI/Servy.psm1 (Format-SecureLogMessage) is a SEPARATE mechanism that
    # masks CLI option values (--password=…) and is kept in sync with the [Sensitive]
    # attribute on CLI option properties, not with this keyword list.
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
    
    # Constructed via concatenation to avoid multi-line here-string whitespace issues.
    # Branch B (space separator) consumes multi-word unquoted values up to the next
    # command-flag delimiter (-x / /x). To maintain architecture safety constraints,
    # only the final whitespace-delimited token of an unquoted value is masked;
    # multi-word unquoted sequences are partially redacted.
    # Suffix matching logic pulled inside the (?<key>...) group boundary to protect composite keys.
    $regexPattern = "(?i)(?<![a-zA-Z0-9])(?<key>(?:$keyPattern)(?:_[A-Za-z0-9]+)*)(?![a-zA-Z0-9])" +
        "(?:" +
            # BRANCH A: Explicit Separators (:, =, /)
            "(?<sep>\s*[:=]\s*|/)" +
            "(?<val>`"[^`"]*`"|'[^']*'|(?:[^\s`"']+(?:\s+(?![\-/]+[a-zA-Z])[^\s`"']+)*))" +
            "|" +
            # BRANCH B: Space Separator
            "(?<sep>\s+)(?![\-/]+[a-zA-Z])" +
            "(?<val>`"[^`"]*`"|'[^']*'|(?:[^\s`"']+(?:\s+(?![\-/]+[a-zA-Z])[^\s`"']+)*))" +
        ")"

    $maskingRegex = New-Object System.Text.RegularExpressions.Regex (
        $regexPattern,
        [System.Text.RegularExpressions.RegexOptions]::None,
        [TimeSpan]::FromMilliseconds(2000)
    )

    # Use MatchEvaluator to conditionally extract the matched separator group (A or B)
    $evaluator = [System.Text.RegularExpressions.MatchEvaluator] {
        param($m)
        
        $key = $m.Groups["key"].Value
        $sep = $m.Groups["sep"].Value
        $val = $m.Groups["val"].Value

        # Use foolproof native string manipulation to check encapsulation boundaries safely
        if (($val.StartsWith('"') -and $val.EndsWith('"')) -or ($val.StartsWith("'") -and $val.EndsWith("'"))) {
            return "$key$sep********"
        }

        # Branch B unquoted fallback logic
        $lastTokenIndex = $val.LastIndexOf(' ')
        if ($lastTokenIndex -ge 0) {
            return "$key$sep$($val.Substring(0, $lastTokenIndex + 1))********"
        }

        return "$key$sep********"
    }

    try {
        return $maskingRegex.Replace($Text, $evaluator)
    } catch [System.Text.RegularExpressions.RegexMatchTimeoutException] {
        # Safety: a malformed/oversized payload triggered the ReDoS guard.
        # Fail closed: redact the whole payload rather than crash the caller.
        return '********'
    }
}