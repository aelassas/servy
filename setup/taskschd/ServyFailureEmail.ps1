#Requires -Version 5.1
<#
.SYNOPSIS
    Monitors Servy error events in the Windows Application log and sends notification emails.

.DESCRIPTION
    This script performs a targeted audit of the Windows Application event log to identify 
    failures within services managed by Servy. It provides an automated alerting mechanism 
    for administrators by:
      1. Filtering the Application log specifically for 'Servy' error sources.
      2. Tracking the last processed event via a local timestamp file to prevent duplicate alerts.
      3. Parsing event messages to identify the specific service name and error context.
      4. Dispatching HTML-formatted notification emails using a robust .NET SMTP implementation.
      5. Providing fallback logging to the Event Log or local disk if email delivery fails.

.NOTES
    Author      : Akram El Assas
    Project     : Servy
    Repository  : https://github.com/aelassas/servy
    
    No parameters are required. SMTP settings (Server, Port, From, To) are loaded 
    from 'smtp-config.xml'. Credentials are managed via 'smtp-cred.xml'.

    Requirements:
      - PowerShell 5.1 or later.
      - 'smtp-config.xml' and 'smtp-cred.xml' must exist in the script directory.

    Setup (Secure Credentials):
      To avoid hardcoding passwords, this script requires an encrypted XML credential file.
      Run the following command as the user account that will execute the Scheduled Task:
      
      $cred = Get-Credential
      $cred | Export-Clixml (Join-Path "C:\Path\To\Servy" "smtp-cred.xml")

.EXAMPLE
    .\ServyFailureEmail.ps1
#>

# -------------------------------
# 1. Determine Script Root (PS 3.0+ Compatible)
# -------------------------------
$scriptDir = $PSScriptRoot
$timestampFile = Join-Path $scriptDir "last-processed-email.dat"
$fallbackLogFile = "ServyFailureEmail.log"

# Central Sentinel Guard Domain Definition (RFC 2606 Reserved Domain).
# Keeps the template independently modifiable and dynamically secures From/To fields.
$DefaultPlaceholderDomain = "example.com"

# Event ID Taxonomy (Refer to src/Servy.Core/Logging/EventIds.cs for updates)
# 3000-3099: Core Errors | 3100-3199: Script Errors
$EVENT_ID_DEPENDENCY_ERROR = 3104

# -------------------------------
# 2. Imports
# -------------------------------
$requiredDependencies = @(
    "Servy-Watermark.psm1",
    "ServySecurity.ps1"
)

foreach ($dep in $requiredDependencies) {
    $depPath = Join-Path $scriptDir $dep

    if (-not (Test-Path $depPath)) {
        $errorMsg = "Servy Notification Error: Required dependency not found at '$depPath'. Please ensure the file exists in the script directory."
        
        # 1. Attempt to log to Event Log for administrator visibility
        try {
            # Best-effort: the 'Servy' event source may not be registered, so guard with try/catch.
            Write-EventLog -LogName Application -Source "Servy" -EventId $EVENT_ID_DEPENDENCY_ERROR `
                -EntryType Warning -Message $errorMsg -ErrorAction Stop
        } catch {
            # 2. Fallback to stderr if Event Log fails (or source isn't registered)
            Write-Error $errorMsg
        }

        # 3. Exit with error code
        exit 1
    }

    # File exists, proceed with dot-sourcing or importing
    if ($dep -like "*.psm1") { Import-Module $depPath -Force } else { . $depPath }
}

function ConvertTo-HtmlSafe {
    <#
    .SYNOPSIS
        Converts plain text to HTML-safe format by encoding XML/HTML metacharacters (&, <, >, ", ').
    #>
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return "" }
    return ($Text -replace '&', '&amp;' `
                  -replace '<', '&lt;' `
                  -replace '>', '&gt;' `
                  -replace '"', '&quot;' `
                  -replace "'", '&#39;')
}

# -------------------------------
# 3. Load Configuration
# -------------------------------
$configPath = Join-Path $scriptDir "smtp-config.xml"
if (-not (Test-Path $configPath)) {
  $errorMsg = "ServyFailureEmail: Configuration file not found at '$configPath'. Stopping script."
  Write-FallbackError -Message $errorMsg -ScriptDir $scriptDir -FallbackFileName $fallbackLogFile
  exit 1
}

try {
  [xml]$SmtpConfig = Get-Content $configPath -ErrorAction Stop
} catch {
  $errorMsg = "ServyFailureEmail: Failed to parse XML configuration. Error: $($_.Exception.Message)"
  Write-FallbackError -Message $errorMsg -ScriptDir $scriptDir -FallbackFileName $fallbackLogFile
  exit 1
}

# -------------------------------
# 4. Email Notification Function
# -------------------------------
function Send-NotificationEmail {
  <#
    .SYNOPSIS
        Dispatches a sanitised HTML notification email via SMTP.

    .DESCRIPTION
        This function handles the low-level SMTP transport. It expects a pre-sanitised Body 
        that has already been passed through the sensitive string masker and HTML encoder. 
        It returns a status string indicating Success, PermanentFailure, or TransientFailure.

    .PARAMETER Subject
        The masked subject line for the email.

    .PARAMETER Body
        The pre-masked and HTML-encoded body content.

    .PARAMETER Config
        The raw XML configuration tree structure containing active SMTP endpoint mappings.

    .PARAMETER ScriptDir
        The directory context for reading credential files and routing fallback logs.
        
    .PARAMETER FallbackLogFile
        The log file string to route fallback errors towards.
  #>
  [CmdletBinding()]
  param (
    [string]$Subject,
    [string]$Body,
    [xml]$Config,
    [string]$ScriptDir,
    [string]$FallbackLogFile
  )

  # Masking is now performed by the caller before HTML encoding. 
  # This ensures the regex tail (?:"[^"]*"|'[^']*'|\S+) matches full quoted strings 
  # before quotes are converted to &quot; or &#39;.

  # --- HARDENED CONFIGURATION ACCESS ---
  
  # 1. Check root structure passed via parameters
  $configRoot = $Config.SmtpConfig
  if ($null -eq $configRoot) {
    Write-FallbackError -Message "ServyFailureEmail: Could not find <SmtpConfig> root element in configuration context." -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
  }

  $smtpServer = $configRoot.Server
  $from        = $configRoot.From
  $to          = $configRoot.To

  $rawPort    = ([string]$configRoot.Port).Trim()
  $rawUseSsl  = ([string]$configRoot.UseSsl).Trim()
  $rawTimeout = ([string]$configRoot.TimeoutMs).Trim()
  
  # 2. Safe Port Resolution (Prevents [int]$null becoming 0)
  $portRef = 0
  $smtpPort = if ([int]::TryParse($rawPort, [ref]$portRef)) { $portRef } else { 0 }
  
  # 3. Safe SSL Preference Resolution (Case-insensitive, defaults to true)
  # Casts to string and trims whitespace to prevent parsing errors. 
  # Uses case-insensitive regex '(?i)' to match "false", "FALSE", "False", or "0".
  $useSsl = if ($rawUseSsl  -match '^(?i)(false|0)$') { $false }        else { $true }

  # 4. Safe Timeout Resolution (Defaults to 30000ms / 30s)
  $timeoutRef = 0
  $timeout = if ([int]::TryParse($rawTimeout, [ref]$timeoutRef)) { $timeoutRef } else { 30000 }

  $credPath = Join-Path $ScriptDir "smtp-cred.xml"
  $emailRegex = '^[^@\s]+@[^@\s]+\.[^@\s]+$' # Definition of the single address format validation rule

  # --- VALIDATION GATE (Permanent Failures) ---
  
  # Check for missing essential fields
  if ([string]::IsNullOrWhiteSpace($smtpServer) -or [string]::IsNullOrWhiteSpace($from) -or [string]::IsNullOrWhiteSpace($to)) {
    Write-FallbackError -Message "ServyFailureEmail: Incomplete configuration. Missing Server, From, or To." -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
  }

  # Check for invalid port
  if ($smtpPort -le 0 -or $smtpPort -gt 65535) {
    Write-FallbackError -Message "ServyFailureEmail: Invalid or missing Port ($smtpPort) in smtp-config.xml." -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
  }

  # Email format checks (Prevent .NET ArgumentException/FormatException)
  if ($from -notmatch $emailRegex) {
    Write-FallbackError -Message "ServyFailureEmail: Invalid 'From' email format ($from) in smtp-config.xml." -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
  }

  # --- ROBUSTNESS: Parse and accommodate multi-recipient address fields ---
  # Split the input on commas or semicolons, trimming surrounding whitespace automatically
  $toList = $to -split '\s*[,;]\s*' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

  if ($toList.Count -eq 0) {
      Write-FallbackError -Message "ServyFailureEmail: The 'To' field evaluates to empty in smtp-config.xml." -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile
      return 'PermanentFailure'
  }

  # Validate each split address block individually against the single-address regex gate
  foreach ($addr in $toList) {
      if ($addr -notmatch $emailRegex) {
          Write-FallbackError -Message "ServyFailureEmail: Invalid 'To' email format ($addr) in smtp-config.xml. Multi-recipient lists must be separated by commas or semicolons." -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile
          return 'PermanentFailure'
      }
  }

  # Hardened domain verification derived from global $DefaultPlaceholderDomain variable scope.
  # Polymorphically screens Server, From, and sub-recipient properties against unconfigured templates.
  # Adjusted From and To patterns to support matching both standard domain contexts and sub-domain structures after the '@' separator.
  $isPlaceholderServer = $smtpServer -eq $DefaultPlaceholderDomain -or $smtpServer -like "*.$DefaultPlaceholderDomain"
  $isPlaceholderFrom   = $from -like "*@$DefaultPlaceholderDomain" -or $from -like "*@*.$DefaultPlaceholderDomain"
  $isPlaceholderTo     = $toList | Where-Object { $_ -like "*@$DefaultPlaceholderDomain" -or $_ -like "*@*.$DefaultPlaceholderDomain" }

  if ($isPlaceholderServer -or $isPlaceholderFrom -or $isPlaceholderTo) {
    Write-FallbackError -Message "ServyFailureEmail: SMTP pipeline fields are still using default placeholder domain references ($DefaultPlaceholderDomain). Email skipped." -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
  }

  if (-not (Test-Path $credPath)) {
    Write-FallbackError -Message "ServyFailureEmail: Credential file not found at '$credPath'. Skipping email." -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
  }

  # --- CRYPTOGRAPHIC HARDENING (Issue #2078 Mitigation) ---
  # Pin TLS to prevent silent downgrade to broken protocols (SSL3/TLS1.0) on older Windows host defaults.
  try {
      # Build the explicit allow-list from scratch instead of OR-ing onto the runtime default.
      $allowed = [Net.SecurityProtocolType]::Tls12
      if ([enum]::IsDefined([Net.SecurityProtocolType], 'Tls13')) {
          $allowed = $allowed -bor [Net.SecurityProtocolType]::Tls13
      }
      [Net.ServicePointManager]::SecurityProtocol = $allowed
  } catch {
      Write-Warning "ServyFailureEmail: Could not pin explicit TLS version; relying on system environment defaults. $_"
  }

  # --- EXECUTION ---
  try {
    $cred = Import-Clixml $credPath

    $smtp = New-Object System.Net.Mail.SmtpClient($smtpServer, $smtpPort)
    $smtp.EnableSsl = $useSsl
    $smtp.Timeout = $timeout
    $smtp.Credentials = $cred.GetNetworkCredential()

    $mailMessage = New-Object System.Net.Mail.MailMessage
    $mailMessage.From = $from
    # Safely load the validated addresses into the MailAddressCollection array
    foreach ($addr in $toList) {
        $mailMessage.To.Add($addr)
    }
    $mailMessage.Subject = $Subject
    $mailMessage.Body = $Body
    $mailMessage.IsBodyHtml = $true

    $smtp.Send($mailMessage)
    return 'Success'
  } catch [System.Security.Cryptography.CryptographicException] {
      # The credential file exists but cannot be decrypted (e.g., scheduled task running as wrong user)
      $errorMsg = "ServyFailureEmail: Failed to decrypt credentials. Ensure the task runs as the user who created smtp-cred.xml. Error: $($_.Exception.Message)"
      Write-FallbackError -Message $errorMsg -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile
      return 'PermanentFailure'
  } catch [System.Net.Mail.SmtpException] {
      # SMTP-level errors: Apply granular classification based on RFC 5321 codes.
      # 4xx (e.g., 421, 450) are treated as Transient; 5xx (e.g., 550, 554) are Permanent.
      $status = $_.Exception.StatusCode
      
      # Determine if the error is recoverable (Transient) or invalid (Permanent).
      # ServiceNotAvailable, MailboxBusy, and TransactionFailed are explicitly mapped 
      # to Transient, even if they sometimes present as 5xx-like behavior in specific drivers.
      $isTransient = ($status -ge 400 -and $status -lt 500) `
        -or $status -eq [System.Net.Mail.SmtpStatusCode]::ServiceNotAvailable `
        -or $status -eq [System.Net.Mail.SmtpStatusCode]::MailboxBusy `
        -or $status -eq [System.Net.Mail.SmtpStatusCode]::TransactionFailed
      
      $errorMsg = "ServyFailureEmail: SMTP $status sending to $to. Error: $($_.Exception.Message)"
      
      # Record to fallback logs (disk and Application Event Log) before deciding exit status.
      Write-FallbackError -Message $errorMsg -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile    
      
      # Return status determines if the watermark advances.
      # TransientFailure: Queue processing halts to wait for system recovery.
      # PermanentFailure: Event processed (watermark advances) to prevent head-of-line blocking.
      if ($isTransient) { return 'TransientFailure' }
      return 'PermanentFailure'
  } catch [System.FormatException] {
      # Malformed e-mail address slipped past validation - never going to succeed.
      Write-FallbackError -Message "ServyFailureEmail: Permanent format failure: $($_.Exception.Message)" -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile
      return 'PermanentFailure'
  } catch [System.IO.IOException], [System.Net.WebException], [System.Net.Sockets.SocketException], [System.TimeoutException] {
      # ROBUSTNESS: Explicitly isolate known transient/retryable physical infrastructure and network faults.
      $errorMsg = "ServyFailureEmail: Transient network I/O failure to $to. Error: $($_.Exception.Message)"
      Write-FallbackError -Message $errorMsg -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile
      return 'TransientFailure'
  } catch {
      # ROBUSTNESS: Treat unrecognized structural errors (e.g. ArgumentException on CRLF header injection) 
      # as permanent failures to ensure a corrupted log payload cannot block the entire pipeline execution loop.
      $errorMsg = "ServyFailureEmail: Unexpected permanent script failure to $to. Type: $($_.Exception.GetType().FullName). Error: $($_.Exception.Message)"
      Write-FallbackError -Message $errorMsg -ScriptDir $ScriptDir -FallbackFileName $FallbackLogFile
      return 'PermanentFailure'
  } finally {
      if ($null -ne $mailMessage) { $mailMessage.Dispose() }
      if ($null -ne $smtp) { $smtp.Dispose() }
  }
}

# -------------------------------
# 5. Read Last Processed Timestamp
# -------------------------------
$lastProcessed = Read-Watermark -TimestampFile $timestampFile

# -------------------------------
# 6. Fetch and Filter Errors
# -------------------------------
$eventsToProcess = Get-EventsToProcess -ScriptDir $scriptDir -LastProcessed $lastProcessed

if ($null -eq $eventsToProcess) {
    Write-Host "No new errors to process."
    exit 0
}

# -------------------------------
# 7. Process Events & Send Emails
# -------------------------------
foreach ($evt in $eventsToProcess) {
  $parsed = ConvertFrom-ServyEventMessage -Message $evt.Message

  # 1. MASKING (Stage 1: Plain Text)
  # We mask the raw strings before any HTML encoding occurs.
  # This ensures the regex successfully captures PASSWORD="my secret token" 
  # before it becomes PASSWORD=&quot;my secret token&quot;
  $maskedLogText = Protect-SensitiveString -Text $parsed.LogText
  $maskedServiceName = Protect-SensitiveString -Text $parsed.ServiceName

  # 2. ENCODING (Stage 2: Markup Preparation)
  # Now that secrets are replaced with asterisks, we can safely convert 
  # any remaining metacharacters to HTML entities.
  $safeLogText = ConvertTo-HtmlSafe -Text $maskedLogText
  $safeServiceName = ConvertTo-HtmlSafe -Text $maskedServiceName

  # 3. COMPOSITION
  # Scrub the subject using the raw service name (masker handles this internally)
  $subject = "Servy - $($parsed.ServiceName) Failure"
  $subject = Protect-SensitiveString -Text $subject
  
  # ROBUSTNESS FIX: Sanitise the subject string value of CR/LF injection characters
  # explicitly to block .NET ArgumentException errors at the MailMessage property setter stage.
  $subject = $subject -replace "[\r\n]", ' '

  # Build the HTML body using the safe, pre-masked segments
  $body = "A failure has been detected in service '$safeServiceName'." + 
          [Environment]::NewLine + "Details: $safeLogText"
  
  # Basic HTML formatting (newlines to breaks)
  $htmlBody = $body -replace "`r?`n", "<br>"
    
  # Attempt to send the email with explicit configuration encapsulation mapping
  $sendStatus = Send-NotificationEmail -Subject $subject -Body $htmlBody -Config $SmtpConfig -ScriptDir $scriptDir -FallbackLogFile $fallbackLogFile
  
  switch ($sendStatus) {
      'Success' {
          Write-Host "Email Notification sent for '$($parsed.ServiceName)'."
          # Track this timestamp as successfully processed
          Update-Watermark -TimestampFile $timestampFile -TimeCreated $evt.TimeCreated -ScriptDir $scriptDir
      }
      'PermanentFailure' {
          # Logged internally. Advance the watermark because retrying won't fix bad config.
          Write-Host "Permanent configuration failure for '$($parsed.ServiceName)'. Skipping to prevent endless fallback logging." -ForegroundColor Yellow
          Update-Watermark -TimestampFile $timestampFile -TimeCreated $evt.TimeCreated -ScriptDir $scriptDir
      }
      'TransientFailure' {
          # Network drop, timeout, or SMTP temp-fail. DO NOT advance the watermark. 
          # We break the loop immediately; if SMTP is down, subsequent events in this batch will fail too.
          Write-Host "Transient failure sending email for '$($parsed.ServiceName)'. Halting processing to preserve event queue." -ForegroundColor Red
      }
  }
  if ($sendStatus -eq 'TransientFailure') { break }   # break the foreach explicitly
}