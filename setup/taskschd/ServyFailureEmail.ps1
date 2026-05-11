#Requires -Version 3.0
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

.PARAMETER None
    No parameters are required. SMTP settings (Server, Port, From, To) are loaded 
    from 'smtp-config.xml'. Credentials are managed via 'smtp-cred.xml'.

.NOTES
    Author      : Akram El Assas
    Project     : Servy
    Repository  : https://github.com/aelassas/servy
    
    Requirements:
      - PowerShell 3.0 or later.
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
            # exported variables from a module that has fundamentally failed to load.
            Write-EventLog -LogName Application -Source "Servy" -EventId $EVENT_ID_DEPENDENCY_ERROR `
                -EntryType Error -Message $errorMsg -ErrorAction Stop
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
        Converts plain text to HTML-safe format by encoding metacharacters.
    .DESCRIPTION
        This helper provides a manual replacement chain for metacharacters to ensure 
        compatibility with .NET 3.5 and PowerShell 2.0 environments.
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
  Write-FallbackError -Message $errorMsg -scriptDir $scriptDir -FallbackFileName $fallbackLogFile
  exit 1
}

try {
  [xml]$SmtpConfig = Get-Content $configPath -ErrorAction Stop
} catch {
  $errorMsg = "ServyFailureEmail: Failed to parse XML configuration. Error: $($_.Exception.Message)"
  Write-FallbackError -Message $errorMsg -scriptDir $scriptDir -FallbackFileName $fallbackLogFile
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

    .PARAMETER scriptDir
        The directory context for configuration and credential files.
        
    .PARAMETER FallbackLogFile
        The log file string to route fallback errors towards.
  #>
  [CmdletBinding()]
  param (
    [string]$Subject,
    [string][Parameter(ValueFromPipeline)]$Body,
    [string]$scriptDir,
    [string]$FallbackLogFile
  )

  # LOGIC: Masking is now performed by the caller before HTML encoding. 
  # This ensures the regex tail (?:"[^"]*"|'[^']*'|\S+) matches full quoted strings 
  # before quotes are converted to &quot; or &#39;.

  # --- HARDENED CONFIGURATION ACCESS ---
  
  # 1. Check root structure
  $configRoot = $SmtpConfig.SmtpConfig
  if ($null -eq $configRoot) {
    Write-FallbackError -Message "ServyFailureEmail: Could not find <SmtpConfig> root element." -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
  }

  $smtpServer = $configRoot.Server
  $from       = $configRoot.From
  $to         = $configRoot.To
  
  # 2. Safe Port Resolution (Prevents [int]$null becoming 0)
  $rawPort = $configRoot.Port
  $smtpPort = if ($null -ne $rawPort -and $rawPort -match '^\d+$') { [int]$rawPort } else { 0 }
  
  # 3. Safe SSL Preference Resolution (Case-insensitive, defaults to true)
  # LOGIC: Casts to string and trims whitespace to prevent parsing errors. 
  # Uses case-insensitive regex '(?i)' to match "false", "FALSE", "False", or "0".
  $rawUseSsl = $configRoot.UseSsl
  $useSsl = if ($null -ne $rawUseSsl -and ([string]$rawUseSsl).Trim() -match '^(?i)(false|0)$') { $false } else { $true }

  # 4. Safe Timeout Resolution (Defaults to 30000ms / 30s)
  $rawTimeout = $configRoot.TimeoutMs
  $timeout = if ($null -ne $rawTimeout -and $rawTimeout -match '^\d+$') { [int]$rawTimeout } else { 30000 }

  $credPath = Join-Path $scriptDir "smtp-cred.xml"
  $emailRegex = '^[^@]+@[^@]+\.[^@]+$'

  # --- VALIDATION GATE (Permanent Failures) ---
  
  # Check for missing essential fields
  if ([string]::IsNullOrEmpty($smtpServer) -or [string]::IsNullOrEmpty($from) -or [string]::IsNullOrEmpty($to)) {
    Write-FallbackError -Message "ServyFailureEmail: Incomplete configuration. Missing Server, From, or To." -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
  }

  # Check for invalid port
  if ($smtpPort -le 0 -or $smtpPort -gt 65535) {
    Write-FallbackError -Message "ServyFailureEmail: Invalid or missing Port ($smtpPort) in smtp-config.xml." -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
  }

  # Default placeholder check
  if ($smtpServer -eq "smtp.example.com") {
    Write-FallbackError -Message "ServyFailureEmail: SMTP Server is still set to default placeholder. Email skipped." -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
  }

  # Email format checks (Prevent .NET ArgumentException/FormatException)
  if ($from -notmatch $emailRegex) {
    Write-FallbackError -Message "ServyFailureEmail: Invalid 'From' email format ($from) in smtp-config.xml." -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
  }

  if ($to -notmatch $emailRegex) {
    Write-FallbackError -Message "ServyFailureEmail: Invalid 'To' email format ($to) in smtp-config.xml." -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
  }

  if (-not (Test-Path $credPath)) {
    Write-FallbackError -Message "ServyFailureEmail: Credential file not found at '$credPath'. Skipping email." -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
    return 'PermanentFailure'
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
    $mailMessage.To.Add($to)
    $mailMessage.Subject = $Subject
    $mailMessage.Body = $Body
    $mailMessage.IsBodyHtml = $true

    $smtp.Send($mailMessage)
    return 'Success'
  } catch [System.Security.Cryptography.CryptographicException] {
      # The credential file exists but cannot be decrypted (e.g., scheduled task running as wrong user)
      $errorMsg = "ServyFailureEmail: Failed to decrypt credentials. Ensure the task runs as the user who created smtp-cred.xml. Error: $($_.Exception.Message)"
      Write-FallbackError -Message $errorMsg -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
      return 'PermanentFailure'
  } catch [System.Net.Mail.SmtpException] {
      # SMTP-level error: classify by status code.
      # 4xx (deferred) = transient; 5xx (rejected) = permanent.
      $status = [int]$_.Exception.StatusCode
      $isPermanent = $status -ge 500 -and $status -lt 600

      $errorMsg = "ServyFailureEmail: SMTP $status sending to $to. Error: $($_.Exception.Message)"
      Write-FallbackError -Message $errorMsg -scriptDir $scriptDir -FallbackFileName $FallbackLogFile

      if ($isPermanent) { return 'PermanentFailure' }
      return 'TransientFailure'
  } catch [System.FormatException] {
      # Malformed e-mail address slipped past validation — never going to succeed.
      Write-FallbackError -Message "ServyFailureEmail: Permanent format failure: $($_.Exception.Message)" -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
      return 'PermanentFailure'
  } catch {
      # Network drops, timeouts, etc. — try again next run.
      $errorMsg = "ServyFailureEmail: Transient failure to $to. Error: $($_.Exception.Message)"
      Write-FallbackError -Message $errorMsg -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
      return 'TransientFailure'
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
  # LOGIC: We mask the raw strings before any HTML encoding occurs.
  # This ensures the regex successfully captures PASSWORD="my secret token" 
  # before it becomes PASSWORD=&quot;my secret token&quot;
  $maskedLogText = Protect-SensitiveString -Text $parsed.LogText
  $maskedServiceName = Protect-SensitiveString -Text $parsed.ServiceName

  # 2. ENCODING (Stage 2: Markup Preparation)
  # Logic: Now that secrets are replaced with asterisks, we can safely convert 
  # any remaining metacharacters to HTML entities.
  $safeLogText = ConvertTo-HtmlSafe -Text $maskedLogText
  $safeServiceName = ConvertTo-HtmlSafe -Text $maskedServiceName

  # 3. COMPOSITION
  # Scrub the subject using the raw service name (masker handles this internally)
  $subject = "Servy - $($parsed.ServiceName) Failure"
  $subject = Protect-SensitiveString -Text $subject

  # Build the HTML body using the safe, pre-masked segments
  $body = "A failure has been detected in service '$safeServiceName'." + 
          [Environment]::NewLine + "Details: $safeLogText"
  
  # Basic HTML formatting (newlines to breaks)
  $htmlBody = $body -replace "`r?`n", "<br>"
    
  # Attempt to send the email
  $sendStatus = Send-NotificationEmail -Subject $subject -Body $htmlBody -scriptDir $scriptDir -FallbackLogFile $fallbackLogFile
  
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