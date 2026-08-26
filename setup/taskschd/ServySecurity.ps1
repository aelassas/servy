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

.PARAMETER TimeoutMs
    Regex match timeout in milliseconds (default: 2000 ms).

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
        [string]$Text,

        [Parameter(Mandatory=$false)]
        [int]$TimeoutMs = 2000
    )

    if ([string]::IsNullOrWhiteSpace($Text)) { return $Text }

    # A collection of keywords used to identify potentially sensitive information.
    #
    # WARNING: keep in sync with the parity twin in:
    #    src/Servy.Service/Helpers/ServiceHelper.cs (SensitiveKeyWords) - same keyword-pattern masker.
    #
    # NOTE: src/Servy.CLI/Servy.psm1 (Format-SecureLogMessage) is a SEPARATE mechanism that
    # masks CLI option values (--password=…) and is kept in sync with the [Sensitive]
    # attribute on CLI option properties, not with this keyword list.

    # Long/unambiguous: a letter/digit prefix is allowed (e.g. DBPASSWORD, PGPASSWORD, APITOKEN, APIKEY, MY_PASSWORD)
    $looseKeys = @(
        # --- Core Credentials ---
        "PASSWORD", "PASSPHRASE", "USERPWD",

        # --- Web & Mobile Auth (JWT/OAuth/Personal Tokens) ---
        "TOKEN", "CREDENTIAL", "CLIENT_SECRET",

        # --- Cloud & Infrastructure (AWS/Azure/GCP) ---
        "SECRET", "ACCOUNTKEY", "ACCESSKEY", "SIGNATURE",

        # --- Databases & Storage ---
        "CONNECTIONSTRING", "CONNSTR", "DATABASE_URL",
        "PROVIDER_CONNECTION_STRING", "DATABASE_PASSWORD",

        # --- Cryptography & Identity (Specific KEY variants) ---
        "PRIVATE_KEY", "SSH_KEY", "SECRET_KEY", "API_KEY", "APIKEY",
        "CERTIFICATE", "THUMBPRINT",

        # --- API & Integration Tokens ---
        "APP_SECRET", "BROWSER_KEY", "WEBHOOK_URL",
        "KUBE_CONFIG", "TELEGRAM_TOKEN", "DISCORD_TOKEN"
    )

    # Short/ambiguous: keep strict leading boundary to avoid false positives (e.g., COMPAT, CONCERT, ARKANSAS)
    $strictKeys = @(
        # --- Short Core Credentials ---
        "PWD", "PIN",

        # --- Web & Mobile Auth ---
        "AUTH", "BEARER", "JWT", "SESSION", "COOKIE", "PAT",

        # --- Cloud & Infrastructure ---
        "SAS", "SKEY", "TENANT_ID",

        # --- Databases & Storage ---
        "DSN",

        # --- Cryptography & Identity ---
        "CERT", "PFX", "PEM", "SALT", "PEPPER",

        # --- API Service Identifiers ---
        "API"
    )

    $loosePattern  = [string]::Join('|', ($looseKeys  | ForEach-Object { [regex]::Escape($_) }))
    $strictPattern = [string]::Join('|', ($strictKeys | ForEach-Object { [regex]::Escape($_) }))

    $keyBoundary = "(?i)(?:(?<=^|[^a-zA-Z0-9])(?<key>[A-Za-z0-9]*(?:$loosePattern)(?:_[A-Za-z0-9]+)*)|(?<![a-zA-Z0-9])(?<key>(?:$strictPattern)(?:_[A-Za-z0-9]+)*))(?![a-zA-Z0-9])"

    # Constructed via concatenation to avoid multi-line here-string whitespace issues.
    # Branch B (space separator) consumes multi-word unquoted values up to the next
    # command-flag delimiter (-x / /x). To maintain security guarantees, any matched
    # value is fully redacted as ********.
    # Suffix matching logic pulled inside the (?<key>...) group boundary to protect composite keys.
    # Entire choice blocks are wrapped in atomic groups (?>...) to eliminate catastrophic backtracking timeouts.
    $regexPattern = $keyBoundary +
        "(?>(?:" +
            # BRANCH A: Explicit Separators (:, =, /)
            "(?<sep>\s*[:=]\s*|/)" +
            "(?>(?:" +
                "(?<val>`"[^`"]*`")|" +             # Double quoted: captures quotes so the whole string gets masked cleanly
                "(?<val>'[^']*')|" +                # Single quoted: captures quotes so the whole string gets masked cleanly
                "(?<val>[^\s`"']+(?:\s+(?![\-/]+[a-zA-Z])[^\s`"']+)*)" + # Unquoted: isolates spaces cleanly without nested loops
            "))" +
            "|" +
            # BRANCH B: Space Separator
            "(?<sep>\s+)(?![\-/]+[a-zA-Z])" +
            "(?>(?:" +
                "(?<val>`"[^`"]*`")|" +             # Double quoted: captures quotes so the whole string gets masked cleanly
                "(?<val>'[^']*')|" +                # Single quoted: captures quotes so the whole string gets masked cleanly
                "(?<val>[^\s`"']+(?:\s+(?![\-/]+[a-zA-Z])[^\s`"']+)*)" + # Unquoted: isolates spaces cleanly without nested loops
            "))" +
        "))"

    $maskingRegex = New-Object System.Text.RegularExpressions.Regex (
        $regexPattern,
        [System.Text.RegularExpressions.RegexOptions]::None,
        [TimeSpan]::FromMilliseconds($TimeoutMs)
    )

    # Use MatchEvaluator to extract key and separator groups and redact the value completely
    $evaluator = [System.Text.RegularExpressions.MatchEvaluator] {
        param($m)

        $key = $m.Groups["key"].Value
        $sep = $m.Groups["sep"].Value

        return "$key$sep********"
    }

    try {
        return $maskingRegex.Replace($Text, $evaluator)
    } catch [System.Text.RegularExpressions.RegexMatchTimeoutException] {
        # Safety: a malformed/oversized payload triggered the ReDoS guard.
        # Fail closed: redact the whole payload rather than crash the caller.
        return '[MASKED DUE TO TIMEOUT]'
    }
}
