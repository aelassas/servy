#Requires -Version 5.1

# Dot-source the production script from the same directory securely
. (Join-Path $PSScriptRoot "ServySecurity.ps1")

# ---------------------------------------------------------------------
# TEST SUITE HARNESS
# ---------------------------------------------------------------------
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running ServySecurity.ps1 Tests               " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

$testCases = @(
    # --- Empty / Whitespace Input Guards ---
    @{ Name = "Empty input passthrough"; Input = ""; Expected = "" },
    @{ Name = "Whitespace-only passthrough"; Input = "    "; Expected = "    " },

    # --- ReDoS Fail-Closed Timeout Redaction ---
    @{ Name = "ReDoS Fail-Closed Timeout Redaction"; Input = "PASSWORD=" + ("x" * 100000000); TimeoutMs = 1; Expected = "[MASKED DUE TO TIMEOUT]" },

    # --- The Composite Suffix Bug Fix Layer (Issue #3765) ---
    @{ Name = "Underscore Suffix: PASSWORD_HASH="; Input = "PASSWORD_HASH=hunter2"; Expected = "PASSWORD_HASH=********" },
    @{ Name = "Underscore Suffix: SECRET_DATA="; Input = "SECRET_DATA=sensitive_payload"; Expected = "SECRET_DATA=********" },
    @{ Name = "Underscore Suffix: PASSWORD_ENC:"; Input = "PASSWORD_ENC: encrypted_blob"; Expected = "PASSWORD_ENC: ********" },
    @{ Name = "Underscore Suffix: CLI Flag Suffix"; Input = "myapp.exe --password_hash secret_value"; Expected = "myapp.exe --password_hash ********" },

    # --- Pre-existing Prefix Structural Sanity Checks ---
    @{ Name = "Underscore Prefix: MY_PASSWORD="; Input = "MY_PASSWORD=hunter2"; Expected = "MY_PASSWORD=********" },
    @{ Name = "Underscore Prefix/Suffix Composite"; Input = "MY_PASSWORD_HASH=hunter2"; Expected = "MY_PASSWORD_HASH=********" },

    # --- Prefix Without Separator Tests (Issue #5877) ---
    @{ Name = "Prefix without separator: PGPASSWORD="; Input = "PGPASSWORD=abc"; Expected = "PGPASSWORD=********" },
    @{ Name = "Prefix without separator: DBPASSWORD="; Input = "DBPASSWORD=hunter2"; Expected = "DBPASSWORD=********" },
    @{ Name = "Prefix without separator: SMTPPASSWORD="; Input = "SMTPPASSWORD=s3cr3t"; Expected = "SMTPPASSWORD=********" },
    @{ Name = "Prefix without separator: APITOKEN="; Input = "APITOKEN=abc123"; Expected = "APITOKEN=********" },
    @{ Name = "Prefix without separator: GITHUBTOKEN="; Input = "GITHUBTOKEN=ghp_xxx"; Expected = "GITHUBTOKEN=********" },
    @{ Name = "Prefix without separator: AWSSECRET="; Input = "AWSSECRET=zz"; Expected = "AWSSECRET=********" },
    @{ Name = "Prefix without separator: --apikey"; Input = "myapp.exe --apikey KKK"; Expected = "myapp.exe --apikey ********" },

    # --- Plural Suffix Keyword Tests (Issue #6511) ---
    @{ Name = "Plural Suffix: AZURE_CREDENTIALS="; Input = "AZURE_CREDENTIALS=my_azure_secret_json"; Expected = "AZURE_CREDENTIALS=********" },
    @{ Name = "Plural Suffix: CREDENTIALS="; Input = "CREDENTIALS=sensitive_blob"; Expected = "CREDENTIALS=********" },
    @{ Name = "Plural Suffix: SECRETS="; Input = "SECRETS=top_secret_val"; Expected = "SECRETS=********" },
    @{ Name = "Plural Suffix: DOCKER_SECRETS="; Input = "DOCKER_SECRETS=token_val"; Expected = "DOCKER_SECRETS=********" },
    @{ Name = "Plural Suffix: TOKENS="; Input = "TOKENS=bearer_123"; Expected = "TOKENS=********" },
    @{ Name = "Plural Suffix: GITHUB_TOKENS="; Input = "GITHUB_TOKENS=ghp_abc"; Expected = "GITHUB_TOKENS=********" },
    @{ Name = "Plural Suffix: PASSWORDS="; Input = "PASSWORDS=secret123"; Expected = "PASSWORDS=********" },
    @{ Name = "Plural Suffix: CERTIFICATES="; Input = "CERTIFICATES=cert_data"; Expected = "CERTIFICATES=********" },
    @{ Name = "Plural Suffix: COOKIES="; Input = "COOKIES=session_cookie_val"; Expected = "COOKIES=********" },
    @{ Name = "Plural Suffix: CLI --secrets="; Input = "myapp.exe --secrets=secret_payload"; Expected = "myapp.exe --secrets=********" },
    @{ Name = "Plural composite: SECRETS_FILE="; Input = "SECRETS_FILE=/etc/x"; Expected = "SECRETS_FILE=********" },
    @{ Name = "Plural composite: TOKENS_PATH="; Input = "TOKENS_PATH=/x"; Expected = "TOKENS_PATH=********" },
    @{ Name = "Plural composite: DB_PASSWORDS_ENC="; Input = "DB_PASSWORDS_ENC=xx"; Expected = "DB_PASSWORDS_ENC=********" },

    # --- Short-Key False Positive Guards (Issue #5877 Invariants) ---
    @{ Name = "Short-key false positive stays clean: COMPAT"; Input = "COMPAT=1"; Expected = "COMPAT=1" },
    @{ Name = "Short-key false positive stays clean: CONCERT"; Input = "CONCERT=tonight"; Expected = "CONCERT=tonight" },
    @{ Name = "Short-key false positive stays clean: ARKANSAS"; Input = "ARKANSAS=little_rock"; Expected = "ARKANSAS=little_rock" },
    @{ Name = "Short-key false positive stays clean: SECRETARY"; Input = "SECRETARY=john_doe"; Expected = "SECRETARY=john_doe" },

    # --- Base Component Separator Branch Verifications ---
    @{ Name = "Branch A: Colon Separator"; Input = "API_KEY: my-secret-token"; Expected = "API_KEY: ********" },
    @{ Name = "Branch A: Forward Slash Separator"; Input = "API_KEY/my-secret-token"; Expected = "API_KEY/********" },
    @{ Name = "Branch B: Space Separator"; Input = "myapp.exe --password mysecret"; Expected = "myapp.exe --password ********" },

    # Standardized expectation to match the engine's targeted keyword redaction architecture
    @{ Name = "Branch B: Space Separator Multi-Word Value"; Input = "CONNSTR my server address password"; Expected = "CONNSTR ********" },

    # --- Quoted Values Preservation Constraints ---
    @{ Name = "Double Quoted Secret Value Mapping"; Input = 'PASSWORD="secret value with spaces"'; Expected = 'PASSWORD=********' },
    @{ Name = "Single Quoted Secret Value Mapping"; Input = "PASSWORD='secret value with spaces'"; Expected = "PASSWORD=********" },

    # --- Edge Boundaries & Non-Masking Invariant Safeguards ---
    @{ Name = "Flag Stop Lookahead Guard Invariant"; Input = "myapp.exe --password mysecret --verbose"; Expected = "myapp.exe --password ******** --verbose" },
    @{ Name = "False Positive Boundary Exemption"; Input = "PASSWORDLESS login attempt"; Expected = "PASSWORDLESS login attempt" }
)

# --- Keyword coverage sweep: every entry in $looseKeys and $strictKeys must redact ---
# The two lists are locals of Protect-SensitiveString, so dot-sourcing does not expose them.
# Parse them out of the production script text instead of keeping a hand-synced copy here:
# a keyword added to the engine is then swept the moment it exists, and a removed one still fails.
function Get-KeywordListFromScript {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$VariableName
    )

    # The array is terminated by a ')' at the start of its own line. Anchor on that, NOT on
    # the first ')': the comments inside $looseKeys contain parentheses, so a [^)]* body would
    # silently capture only the first few keywords and the sweep would still pass.
    $pattern = '(?sm)\$' + [regex]::Escape($VariableName) + '\s*=\s*@\((?<body>.*?)^\s*\)'
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) {
        throw "Could not locate `$$VariableName in ServySecurity.ps1 - the sweep would silently test nothing."
    }

    # Drop comments before harvesting the quoted tokens.
    $body = [regex]::Replace($match.Groups['body'].Value, '#[^\r\n]*', '')
    $keys = @([regex]::Matches($body, '"(?<k>[^"]+)"') | ForEach-Object { $_.Groups['k'].Value })

    if ($keys.Count -eq 0) {
        throw "`$$VariableName parsed to an empty list - the sweep would silently test nothing."
    }

    return $keys
}

$securityScriptText = Get-Content (Join-Path $PSScriptRoot "ServySecurity.ps1") -Raw
$engineLooseKeys  = Get-KeywordListFromScript -Text $securityScriptText -VariableName 'looseKeys'
$engineStrictKeys = Get-KeywordListFromScript -Text $securityScriptText -VariableName 'strictKeys'
$allKeys = $engineLooseKeys + $engineStrictKeys

foreach ($k in $allKeys) {
    $testCases += @{ Name = "Keyword coverage: $k"; Input = "$k=hunter2"; Expected = "$k=********" }
}

$passedCount = 0
$failedCount = 0

foreach ($case in $testCases) {
    $timeoutParam = if ($case.ContainsKey("TimeoutMs")) { $case.TimeoutMs } else { 2000 }
    $actual = Protect-SensitiveString -Text $case.Input -TimeoutMs $timeoutParam

    if ($actual -eq $case.Expected) {
        Write-Host "[PASS] " -ForegroundColor Green -NoNewline
        Write-Host "$($case.Name)" -ForegroundColor Gray
        $passedCount++
    } else {
        Write-Host "[FAIL] " -ForegroundColor Red -NoNewline
        Write-Host "$($case.Name)" -ForegroundColor White -BackgroundColor Red
        Write-Host "       Input   : $($case.Input)" -ForegroundColor DarkGray
        Write-Host "       Expected: $($case.Expected)" -ForegroundColor Yellow
        Write-Host "       Actual  : $actual" -ForegroundColor Magenta
        $failedCount++
    }
}

Write-Host "----------------------------------------------------------" -ForegroundColor Cyan

if ($failedCount -eq 0) {
    Write-Host "ALL $passedCount TESTS PASSED SUCCESSFULLY!" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "SUITE COMPLETE: $passedCount Passed, $failedCount Failed." -ForegroundColor Red
    Write-Host "==========================================================" -ForegroundColor Cyan
    exit 1
}
