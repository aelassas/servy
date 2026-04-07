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
    $ModuleRoot = $PSScriptRoot
} else {
    $ModuleRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
}

# -------------------------------
# 2. Helper: Fallback Logging
# -------------------------------
function Write-FallbackError {
    Param($Message, $ModuleRoot)
    
    Write-Host "ERROR: $Message" -ForegroundColor Red

    try {
        # Attempt to write to Application Log. Requires 'Servy' source to be registered.
        Write-EventLog -LogName Application -Source "Servy" -EventId 9903 `
                       -EntryType Warning -Message $Message -ErrorAction Stop
    }
    catch {
        # Last resort: File log
        $logFile = Join-Path $ModuleRoot "ServyFailureEmail.log"
        "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $Message" | Out-File -FilePath $logFile -Append
    }
}

# -------------------------------
# 3. Load Configuration
# -------------------------------
$configPath = Join-Path $ModuleRoot "smtp-config.xml"

if (-not (Test-Path $configPath)) {
    $errorMsg = "ServyFailureEmail: Configuration file not found at '$configPath'. Stopping script."
    Write-FallbackError -Message $errorMsg -ModuleRoot $ModuleRoot
    exit 1
}

try {
    [xml]$SmtpConfig = Get-Content $configPath -ErrorAction Stop
}
catch {
    $errorMsg = "ServyFailureEmail: Failed to parse XML configuration. Error: $($_.Exception.Message)"
    Write-FallbackError -Message $errorMsg -ModuleRoot $ModuleRoot
    exit 1
}

# -------------------------------
# 4. Email Notification Function
# -------------------------------
function Send-NotificationEmail {
  [cmdletbinding()]
  Param (
    [string] $Subject,
    [string] [parameter(ValueFromPipeline)] $Body
  )

  # --- CONFIGURATION FROM XML ---
  $smtpServer = $SmtpConfig.SmtpConfig.Server
  $smtpPort   = [int]$SmtpConfig.SmtpConfig.Port
  $from       = $SmtpConfig.SmtpConfig.From
  $to         = $SmtpConfig.SmtpConfig.To
  
  # Path to the encrypted credential file
  $credPath   = Join-Path $ModuleRoot "smtp-cred.xml"

  # --- VALIDATION GATE ---
  if ([string]::IsNullOrEmpty($smtpServer) -or $smtpServer -eq "smtp.example.com") {
      $warnMsg = "ServyFailureEmail: SMTP Server is not configured or set to placeholder. Skipping email."
      Write-FallbackError -Message $warnMsg -ModuleRoot $ModuleRoot
      return $false
  }

  if (-not (Test-Path $credPath)) {
      $warnMsg = "ServyFailureEmail: Credential file not found at '$credPath'. Skipping email."
      Write-FallbackError -Message $warnMsg -ModuleRoot $ModuleRoot
      return $false
  }

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
  }
  catch {
    $errorMsg = "ServyFailureEmail: Failed to send notification. Error: $($_.Exception.Message)"
    Write-FallbackError -Message $errorMsg -ModuleRoot $ModuleRoot
    return $false 
  }
  finally {
    if ($null -ne $mailMessage) { $mailMessage.Dispose() }
    if ($null -ne $smtp) { $smtp.Dispose() }
  }
}

# -------------------------------
# 5. Imports and Timestamp Init
# -------------------------------
$timestampFile = Join-Path $ModuleRoot "last-processed-email.dat"
$helperScript = Join-Path $ModuleRoot "Get-ServyLastErrors.ps1"

if (-not (Test-Path $helperScript)) {
    $errorMsg = "Servy Notification Error: Required dependency not found at '$helperScript'."
    Write-FallbackError -Message $errorMsg -ModuleRoot $ModuleRoot
    exit 1
}

. $helperScript

$lastProcessed = $null
if (Test-Path $timestampFile) {
  try {
    $lastProcessed = [DateTime]::Parse((Get-Content $timestampFile -ErrorAction Stop))
  }
  catch { 
    # Warning handled visually; logic continues to process available events
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

# Determine which events to process based on chronological order
if ($null -eq $lastProcessed) {
  # Notify only for the single most recent error to avoid flooding on first run
  $eventsToProcess = @($errors[0])
} else {
  # Sort ascending so emails are sent in the order they happened
  $eventsToProcess = $errors | Sort-Object TimeCreated
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
  $body = "A failure has been detected in service '$serviceName'." +
  [Environment]::NewLine + "Details: $logText"
  $htmlBody = $body -replace "`r?`n", "<br>"
    
  if (Send-NotificationEmail -Subject $subject -Body $htmlBody) {
      Write-Host "Email Notification sent for '$serviceName'."
      # Track the most recent successfully notified event
      $lastSuccessfulTimestamp = $evt.TimeCreated
  } else {
      # If email fails, we STOP processing subsequent errors.
      # This ensures the next run starts from this failed event.
      Write-Host "Aborting further processing due to email failure." -ForegroundColor Yellow
      break
  }
}

# -------------------------------
# 8. Update Timestamp File (Final Step)
# -------------------------------
if ($null -ne $lastSuccessfulTimestamp) {
    # Add 1 tick to avoid duplicate processing on next run
    $newestTimestamp = $lastSuccessfulTimestamp.AddTicks(1)
    $newestTimestamp.ToString("o") | Out-File $timestampFile -Force
    Write-Host "Timestamp updated to: $($newestTimestamp.ToString('o'))"
}
