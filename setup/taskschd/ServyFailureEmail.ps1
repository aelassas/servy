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

# Event ID Taxonomy (Refer to src/Servy.Core/Logging/EventIds.cs for updates)
# 3000-3099: Core Errors | 3100-3199: Script Errors
$EVENT_ID_ERROR = 3103
$EVENT_ID_ERROR_DEP = 3104

# -------------------------------
# 1. Determine Script Root (PS 3.0+ Compatible)
# -------------------------------
$scriptDir = $PSScriptRoot

# -------------------------------
# 2. Helper: Fallback Logging
# -------------------------------
function Write-FallbackError {
  param(
    [string]$Message, 
    [string]$scriptDir
  )
    
  Write-Host "ERROR: $Message" -ForegroundColor Red

  try {
    # Ensure source exists before writing to event log
    Write-EventLog -LogName Application -Source "Servy" -EventId $EVENT_ID_ERROR `
      -EntryType Error -Message $Message -ErrorAction Stop
  } catch {
    $logFile = Join-Path $scriptDir "ServyFailureEmail.log"
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $Message" | Out-File -FilePath $logFile -Append
  }
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
  Write-FallbackError -Message $errorMsg -scriptDir $scriptDir
  exit 1
}

try {
  [xml]$SmtpConfig = Get-Content $configPath -ErrorAction Stop
} catch {
  $errorMsg = "ServyFailureEmail: Failed to parse XML configuration. Error: $($_.Exception.Message)"
  Write-FallbackError -Message $errorMsg -scriptDir $scriptDir
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
        It no longer performs internal masking to avoid regex failures against HTML entities.

    .PARAMETER Subject
        The masked subject line for the email.

    .PARAMETER Body
        The pre-masked and HTML-encoded body content.

    .PARAMETER scriptDir
        The directory context for configuration and credential files.
  #>
  [CmdletBinding()]
  param (
    [string]$Subject,
    [string][Parameter(ValueFromPipeline)]$Body,
    [string]$scriptDir
  )

  # LOGIC: Masking is now performed by the caller before HTML encoding. 
  # This ensures the regex tail (?:"[^"]*"|'[^']*'|\S+) matches full quoted strings 
  # before quotes are converted to &quot; or &#39;.

  # --- HARDENED CONFIGURATION ACCESS ---
  
  # 1. Check root structure
  $configRoot = $SmtpConfig.SmtpConfig
  if ($null -eq $configRoot) {
    Write-FallbackError -Message "ServyFailureEmail: Could not find <SmtpConfig> root element." -scriptDir $scriptDir
    return $false
  }

  $smtpServer = $configRoot.Server
  $from       = $configRoot.From
  $to         = $configRoot.To
  
  # 2. Safe Port Resolution (Prevents [int]$null becoming 0)
  $rawPort = $configRoot.Port
  $smtpPort = if ($null -ne $rawPort -and $rawPort -match '^\d+$') { [int]$rawPort } else { 0 }
  
  $credPath = Join-Path $scriptDir "smtp-cred.xml"
  $emailRegex = '^[^@]+@[^@]+\.[^@]+$'

  # --- VALIDATION GATE ---
  
  # Check for missing essential fields
  if ([string]::IsNullOrEmpty($smtpServer) -or [string]::IsNullOrEmpty($from) -or [string]::IsNullOrEmpty($to)) {
    Write-FallbackError -Message "ServyFailureEmail: Incomplete configuration." -scriptDir $scriptDir
    return $false
  }

  # Check for invalid port
  if ($smtpPort -le 0 -or $smtpPort -gt 65535) {
    Write-FallbackError -Message "ServyFailureEmail: Invalid or missing Port ($smtpPort) in smtp-config.xml." -scriptDir $scriptDir
    return $false
  }

  # Default placeholder check
  if ($smtpServer -eq "smtp.example.com") {
    Write-FallbackError -Message "ServyFailureEmail: SMTP Server is still set to default placeholder. Email skipped." -scriptDir $scriptDir
    return $false
  }

  # Email format checks (Prevent .NET ArgumentException/FormatException)
  if ($from -notmatch $emailRegex) {
    Write-FallbackError -Message "ServyFailureEmail: Invalid 'From' email format ($from) in smtp-config.xml." -scriptDir $scriptDir
    return $false
  }

  if ($to -notmatch $emailRegex) {
    Write-FallbackError -Message "ServyFailureEmail: Invalid 'To' email format ($to) in smtp-config.xml." -scriptDir $scriptDir
    return $false
  }

  if (-not (Test-Path $credPath)) {
    Write-FallbackError -Message "ServyFailureEmail: Credential file not found at '$credPath'. Skipping email." -scriptDir $scriptDir
    return $false
  }

  # --- EXECUTION ---
  try {
    $cred = Import-Clixml $credPath

    $smtp = New-Object System.Net.Mail.SmtpClient($smtpServer, $smtpPort)
    $smtp.EnableSsl = $true
    $smtp.Credentials = $cred.GetNetworkCredential()

    $mailMessage = New-Object System.Net.Mail.MailMessage
    $mailMessage.From = $from
    $mailMessage.To.Add($to)
    $mailMessage.Subject = $Subject
    $mailMessage.Body = $Body
    $mailMessage.IsBodyHtml = $true

    $smtp.Send($mailMessage)
    return $true
  } catch {
    $errorMsg = "ServyFailureEmail: Failed to send notification to $to. Error: $($_.Exception.Message)"
    Write-FallbackError -Message $errorMsg -scriptDir $scriptDir
    return $false 
  } finally {
    if ($null -ne $mailMessage) { $mailMessage.Dispose() }
    if ($null -ne $smtp) { $smtp.Dispose() }
  }
}

# -------------------------------
# 5. Imports and Timestamp Init
# -------------------------------
$timestampFile = Join-Path $scriptDir "last-processed-email.dat"
$requiredScripts = @(
    "Get-ServyLastErrors.ps1",
    "ServySecurity.ps1"
)

foreach ($scriptName in $requiredScripts) {
    $scriptPath = Join-Path $scriptDir $scriptName

    if (-not (Test-Path $scriptPath)) {
        $errorMsg = "Servy Notification Error: Required dependency not found at '$scriptPath'. Please ensure the file exists in the script directory."
        
        # 1. Attempt to log to Event Log for administrator visibility
        try {
            Write-EventLog -LogName Application -Source "Servy" -EventId $EVENT_ID_ERROR_DEP `
                -EntryType Error -Message $errorMsg -ErrorAction Stop
        } catch {
            # 2. Fallback to stderr if Event Log fails (or source isn't registered)
            Write-Error $errorMsg
        }

        # 3. Exit with error code
        exit 1
    }

    # File exists, proceed with dot-sourcing
    . $scriptPath
}

$lastProcessed = $null
if (Test-Path $timestampFile) {
  try {
    $lastProcessed = [DateTime]::ParseExact(
        (Get-Content $timestampFile -ErrorAction Stop),
        'o',
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::RoundtripKind
    )
  } catch { 
    Write-Warning "Could not parse timestamp file; treating as first run - will only show the most recent event."
  }
}

# -------------------------------
# 6. Fetch and Filter Errors
# -------------------------------
$errors = Get-ServyLastErrors -LastProcessed $lastProcessed

if ($null -eq $errors -or $errors.Count -eq 0) {
  Write-Host "No new errors to process."
  exit 0
}

# Chronological sorting for email sequence
if ($null -eq $lastProcessed) {
    # FIRST RUN LOGIC: Only notify for the most recent to avoid flood
    # Wrapping in @() ensures $eventsToProcess is always an array
    $eventsToProcess = @($errors[0])
} else {
    # NORMAL RUN LOGIC: Chronological order
    # Explicitly cast to array to handle single-event scenarios in PS 2.0
    $eventsToProcess = @($errors | Sort-Object TimeCreated)
}

# -------------------------------
# 7. Process Events & Send Emails
# -------------------------------

foreach ($evt in $eventsToProcess) {
  # 1. Parse raw message context
  $message = $evt.Message
  if ($message -match "^\[(.+?)\]\s*(.+)$") {
    $serviceName = $matches[1]
    $logText = $matches[2]
  } else {
    $serviceName = "Unknown Service"
    $logText = $message
  }

  # 2. MASKING (Stage 1: Plain Text)
  # LOGIC: We mask the raw strings before any HTML encoding occurs.
  # This ensures the regex successfully captures PASSWORD="my secret token" 
  # before it becomes PASSWORD=&quot;my secret token&quot;
  $maskedLogText = Protect-SensitiveString -Text $logText
  $maskedServiceName = Protect-SensitiveString -Text $serviceName

  # 3. ENCODING (Stage 2: Markup Preparation)
  # Logic: Now that secrets are replaced with asterisks, we can safely convert 
  # any remaining metacharacters to HTML entities.
  $safeLogText = ConvertTo-HtmlSafe -Text $maskedLogText
  $safeServiceName = ConvertTo-HtmlSafe -Text $maskedServiceName

  # 4. COMPOSITION
  # Scrub the subject using the raw service name (masker handles this internally)
  $subject = "Servy - $serviceName Failure"
  $subject = Protect-SensitiveString -Text $subject

  # Build the HTML body using the safe, pre-masked segments
  $body = "A failure has been detected in service '$safeServiceName'." +
          [Environment]::NewLine + "Details: $safeLogText"
  
  # Basic HTML formatting (newlines to breaks)
  $htmlBody = $body -replace "`r?`n", "<br>"
    
  # Attempt to send the email
  if (Send-NotificationEmail -Subject $subject -Body $htmlBody -scriptDir $scriptDir) {
    Write-Host "Email Notification sent for '$serviceName'."
  } else {
    # We log the failure but intentionally DO NOT break the loop.
    # The event is dropped from the email queue to prevent alert storms upon SMTP recovery.
    Write-Host "Failed to send email for '$serviceName'. Skipping to next event to prevent storms." -ForegroundColor Yellow
  }

  # --- CRITICAL: Always advance the watermark ---
  # Update timestamp immediately for this specific event, regardless of email success.
  $currentEventTimestamp = $evt.TimeCreated

  if ($null -ne $currentEventTimestamp) {
      $newestTimestamp = $currentEventTimestamp.AddTicks(1)
      $shouldWrite = $true
      
      # 1. Ensure the new timestamp is strictly greater than the one currently in the file
      if (Test-Path $timestampFile) {
          try {
              # Read current file text to catch concurrent updates from other script instances
              $currentFileContent = [System.IO.File]::ReadAllText($timestampFile).Trim()
              if (-not [string]::IsNullOrWhiteSpace($currentFileContent)) {
                  $fileTimestamp = [DateTime]::ParseExact(
                      $currentFileContent,
                      'o',
                      [System.Globalization.CultureInfo]::InvariantCulture,
                      [System.Globalization.DateTimeStyles]::RoundtripKind
                  )
                  
                  if ($newestTimestamp -le $fileTimestamp) {
                      $shouldWrite = $false
                  }
              }
          } catch {
              # If file is locked, corrupt, or unparseable, overwrite it
              Write-Host "Could not parse current timestamp file during update check. Overwriting to heal file."
          }
      }
      
      # 2. Write to file only if necessary, explicitly forcing UTF8
      if ($shouldWrite) {
          $timestampString = $newestTimestamp.ToString("o")
          try {
              # Explicitly use UTF8 encoding to prevent PowerShell from writing UTF-16LE
              [System.IO.File]::WriteAllText($timestampFile, $timestampString, [System.Text.Encoding]::UTF8)
              Write-Host "Timestamp updated to: $timestampString"
          }
          catch {
              Write-FallbackError -Message "Failed to update timestamp file: $($_.Exception.Message)" -scriptDir $scriptDir
          }
      }
  }
}
