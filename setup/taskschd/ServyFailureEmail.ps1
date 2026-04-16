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
      - PowerShell 2.0 or later (Compatible with legacy Windows Server environments).
      - .NET Framework 3.5+ (Standard on most Windows systems).
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
# 1. Determine Script Root (PS 2.0+ Compatible)
# -------------------------------
if ($PSVersionTable.PSVersion.Major -ge 3) {
  $scriptDir = $PSScriptRoot
} else {
  $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
}

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
    Write-EventLog -LogName Application -Source "Servy" -EventId 9903 `
      -EntryType Warning -Message $Message -ErrorAction Stop
  } catch {
    $logFile = Join-Path $scriptDir "ServyFailureEmail.log"
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $Message" | Out-File -FilePath $logFile -Append
  }
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
  [CmdletBinding()]
  param (
    [string]$Subject,
    [string][Parameter(ValueFromPipeline)]$Body,
    [string]$scriptDir
  )

  # Mask sensitive data in the body before sending
  $Body = $Body -replace '(?i)(password|secret|key|token)\s*[:=]\s*\S+', '$1=***'

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
    Write-FallbackError -Message "ServyFailureEmail: Incomplete configuration in smtp-config.xml (Server, From, or To is missing)." -scriptDir $scriptDir
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
    # .NET 3.5 SmtpClient doesn't implement IDisposable (PS 2.0 limitation)
    # but we null it for GC safety.
    $smtp = $null
  }
}

# -------------------------------
# 5. Imports and Timestamp Init
# -------------------------------
$timestampFile = Join-Path $scriptDir "last-processed-email.dat"
$helperScript = Join-Path $scriptDir "Get-ServyLastErrors.ps1"

if (-not (Test-Path $helperScript)) {
  $errorMsg = "Servy Notification Error: Required dependency not found at '$helperScript'."
  Write-FallbackError -Message $errorMsg -scriptDir $scriptDir
  exit 1
}

# Dot-source the helper script
. $helperScript

$lastProcessed = $null
if (Test-Path $timestampFile) {
  try {
    $lastProcessed = [DateTime]::Parse((Get-Content $timestampFile -ErrorAction Stop))
  } catch { 
    # Fallback to null if file is corrupt, resulting in processing the most recent event
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
$lastSuccessfulTimestamp = $null

foreach ($evt in $eventsToProcess) {
  $message = $evt.Message
  if ($message -match "^\[(.+?)\]\s*(.+)$") {
    $serviceName = $matches[1]
    $logText = $matches[2]
  } else {
    $serviceName = "Unknown Service"
    $logText = $message
  }

  $subject = "Servy - $serviceName Failure"
  # Manual HTML encode for .NET 3.5 / PS 2.0 compatibility
  $safeLogText = $logText -replace '&', '&amp;' `
                          -replace '<', '&lt;' `
                          -replace '>', '&gt;' `
                          -replace '"', '&quot;' `
                          -replace "'", '&#39;'
  $body = "A failure has been detected in service '$serviceName'." +
  [Environment]::NewLine + "Details: $safeLogText"
  
  # Basic HTML formatting
  $htmlBody = $body -replace "`r?`n", "<br>"
    
  if (Send-NotificationEmail -Subject $subject -Body $htmlBody -scriptDir $scriptDir) {
    Write-Host "Email Notification sent for '$serviceName'."
    $lastSuccessfulTimestamp = $evt.TimeCreated

    # Update timestamp immediately for this specific event
    if ($null -ne $lastSuccessfulTimestamp) {
        $newestTimestamp = $lastSuccessfulTimestamp.AddTicks(1)
        $timestampString = $newestTimestamp.ToString("o")
        
        try {
            # Using [System.IO.File] ensures the file is created/overwritten 
            # exactly as a .NET application would, avoiding PS pipe overhead.
            [System.IO.File]::WriteAllText($timestampFile, $timestampString)
            
            Write-Host "Timestamp updated to: $timestampString"
        }
        catch {
            Write-FallbackError -Message "Failed to update timestamp file: $($_.Exception.Message)" -scriptDir $scriptDir
        }
    }

  } else {
    Write-Host "Aborting further processing due to email failure." -ForegroundColor Yellow
    break
  }
}
