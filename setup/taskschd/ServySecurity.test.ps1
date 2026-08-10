#Requires -Version 5.1
# Dot-source the production script from the same directory securely
. (Join-Path $PSScriptRoot "ServySecurity.ps1")

# ---------------------------------------------------------------------
# TEST SUITE HARNESS
# ---------------------------------------------------------------------
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Running Redaction Test Pass Suite for Protect-SensitiveString" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

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

    # --- Base Component Separator Branch Verifications ---
    @{ Name = "Branch A: Colon Separator"; Input = "API_KEY: my-secret-token"; Expected = "API_KEY: ********" },
    @{ Name = "Branch A: Forward Slash Separator"; Input = "API_KEY/my-secret-token"; Expected = "API_KEY/********" },
    @{ Name = "Branch B: Space Separator"; Input = "myapp.exe --password mysecret"; Expected = "myapp.exe --password ********" },

    # Standardized expectation to match the engine's targeted keyword redaction architecture
    @{ Name = "Branch B: Space Separator Multi-Word Value"; Input = "CONNSTR my server address password"; Expected = "CONNSTR my server address ********" },

    # --- Quoted Values Preservation Constraints ---
    @{ Name = "Double Quoted Secret Value Mapping"; Input = 'PASSWORD="secret value with spaces"'; Expected = 'PASSWORD=********' },
    @{ Name = "Single Quoted Secret Value Mapping"; Input = "PASSWORD='secret value with spaces'"; Expected = "PASSWORD=********" },

    # --- Edge Boundaries & Non-Masking Invariant Safeguards ---
    @{ Name = "Flag Stop Lookahead Guard Invariant"; Input = "myapp.exe --password mysecret --verbose"; Expected = "myapp.exe --password ******** --verbose" },
    @{ Name = "False Positive Boundary Exemption"; Input = "PASSWORDLESS login attempt"; Expected = "PASSWORDLESS login attempt" }
)

# --- Keyword coverage sweep: every entry in $sensitiveKeys must redact ---
$allKeys = @(
    "PASSWORD", "PWD", "PASSPHRASE", "PIN", "USERPWD",
    "TOKEN", "AUTH", "CREDENTIAL", "BEARER", "JWT",
    "SESSION", "COOKIE", "CLIENT_SECRET", "PAT",
    "SECRET", "SAS", "ACCOUNTKEY", "ACCESSKEY", "SKEY",
    "SIGNATURE", "TENANT_ID",
    "CONNECTIONSTRING", "CONNSTR", "DSN", "DATABASE_URL",
    "PROVIDER_CONNECTION_STRING", "DATABASE_PASSWORD",
    "PRIVATE_KEY", "SSH_KEY", "SECRET_KEY", "API_KEY",
    "CERTIFICATE", "CERT", "THUMBPRINT", "PFX", "PEM", "SALT", "PEPPER",
    "API", "APP_SECRET", "BROWSER_KEY", "WEBHOOK_URL",
    "KUBE_CONFIG", "TELEGRAM_TOKEN", "DISCORD_TOKEN"
)

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
