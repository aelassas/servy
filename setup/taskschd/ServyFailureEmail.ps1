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
    No parameters are required. All SMTP configurations (Server, Port, Credentials) 
    are managed internally within the script for scheduled task compatibility.

.NOTES
    Author      : Akram El Assas
    Project     : Servy
    Repository  : https://github.com/aelassas/servy
    
    Requirements:
      - PowerShell 2.0 or later (Compatible with legacy Windows Server environments).
      - .NET Framework 3.5+ (Standard on most Windows systems).
      - The 'Servy' Event Source must be registered in the Application log.
      - Network access to the configured SMTP server.

    Setup (Secure Credentials):
      To avoid hardcoding passwords, this script requires an encrypted XML credential file.
      Run the following command as the user account that will execute the Scheduled Task:
      
      $cred = Get-Credential
      $cred | Export-Clixml (Join-Path "C:\Path\To\Servy" "smtp-cred.xml")

.EXAMPLE
    .\ServyFailureEmail.ps1
    Processes all new Servy errors since the last run and sends individual email alerts.

.EXAMPLE
    # First Run / Manual Reset
    Remove-Item .\last-processed-email.dat
    .\ServyFailureEmail.ps1
    Will alert only for the single most recent error to prevent mailbox flooding.
#>

# -------------------------------
# Email notification function
# -------------------------------
function Send-NotificationEmail {
<#
.SYNOPSIS
    Sends an HTML-formatted email notification via SMTP.

.DESCRIPTION
    Constructs and sends an email using the System.Net.Mail.SmtpClient class. 
    It securely retrieves credentials from an encrypted XML file (smtp-cred.xml) 
    using Import-Clixml. This ensures that no plaintext passwords are stored 
    within the script body.

.PARAMETER Subject
    The subject line of the email.

.PARAMETER Body
    The HTML content of the email body.

.EXAMPLE
    Send-NotificationEmail -Subject "Servy Alert" -Body "Service 'MySvc' failed."

.NOTES
    The credential file must be generated using Export-Clixml by the same user 
    running this script, as the encryption is tied to the user's Windows profile (DPAPI).
#>  
  [cmdletbinding()]
  Param (
    [string] $Subject,
    [string] [parameter(ValueFromPipeline)] $Body
  )

  # --- CONFIGURATION ---
  # Best Practice: Move these to a separate config file later.
  $smtpServer = "smtp.example.com" 
  $smtpPort   = 587
  $from       = "servy.notifications@example.com"
  $to         = "admin@example.com"
  
  # Path to the encrypted credential file (created via Export-Clixml)
  $credPath   = Join-Path $ModuleRoot "smtp-cred.xml"

  # --- VALIDATION GATE ---
  if ($smtpServer -eq "smtp.example.com") {
      $warnMsg = "ServyFailureEmail: SMTP Server is still set to placeholder 'smtp.example.com'. Skipping email."
      Write-FallbackError -Message $warnMsg -ModuleRoot $ModuleRoot
      return $false
  }

  if (-not (Test-Path $credPath)) {
      $warnMsg = "ServyFailureEmail: Credential file not found at '$credPath'. Please see script documentation to generate it."
      Write-FallbackError -Message $warnMsg -ModuleRoot $ModuleRoot
      return $false
  }

  try {
    # Import-Clixml handles the decryption for the local user/machine automatically
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
    $errorMsg = "ServyFailureEmail: Failed to send notification for service '$serviceName'. Error: $($_.Exception.Message)"
    Write-FallbackError -Message $errorMsg -ModuleRoot $ModuleRoot
    exit 1
  }
  finally {
    if ($null -ne $mailMessage) { $mailMessage.Dispose() }
    if ($null -ne $smtp) { $smtp.Dispose() }
  }
}

# -------------------------------
# Helper: Fallback Logging
# -------------------------------
function Write-FallbackError {
    Param($Message, $ModuleRoot)
    
    # Immediate console visibility
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
# 1. Determine Script Root (PS 2.0+ Compatible)
# -------------------------------
if ($PSVersionTable.PSVersion.Major -ge 3) {
  # PS3+ has automatic $PSScriptRoot
  $ModuleRoot = $PSScriptRoot
} else {
  # PS2 does not have $PSScriptRoot
  $ModuleRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
}

$timestampFile = Join-Path $ModuleRoot "last-processed-email.dat"

# -------------------------------
# 2. Imports
# -------------------------------
$helperScript = Join-Path $ModuleRoot "Get-ServyLastErrors.ps1"

if (-not (Test-Path $helperScript)) {
  $errorMsg = "Servy Notification Error: Required dependency not found at '$helperScript'. Please ensure the file exists in the script directory."
    
  # 1. Attempt to log to Event Log for administrator visibility
  try {
    Write-EventLog -LogName Application -Source "Servy" -EventId 9901 `
      -EntryType Error -Message $errorMsg -ErrorAction Stop
  }
  catch {
    # 2. Fallback to stderr if Event Log fails (or source isn't registered)
    Write-Error $errorMsg
  }

  # 3. Exit with error code
  exit 1
}

# File exists, proceed with dot-sourcing
. $helperScript

# -------------------------------
# 3. Read Last Processed Timestamp
# -------------------------------
$lastProcessed = $null
if (Test-Path $timestampFile) {
  try {
    $lastProcessed = [DateTime]::Parse((Get-Content $timestampFile -ErrorAction Stop))
  }
  catch { 
    Write-Warning "Could not parse timestamp file. Will process all available events."
  }
}

# -------------------------------
# 4. Get Latest Errors
# -------------------------------
$errors = Get-ServyLastErrors -LastProcessed $lastProcessed

# -------------------------------
# 5. Update Timestamp File
# -------------------------------
# The first item in the array is the most recent event. Save its timestamp.
# Adding 1 tick ensures the exact same event isn't pulled again on the next run.
$newestTimestamp = $errors[0].TimeCreated.AddTicks(1)
$newestTimestamp.ToString("o") | Out-File $timestampFile -Force

# -------------------------------
# 6. Process Events & Send Emails
# -------------------------------
# Sort ascending so emails are sent in the exact chronological order the errors occurred
if ($null -eq $lastProcessed) {
  # FIRST RUN LOGIC:
  # We only notify for the single most recent event to avoid flooding.
  # $errors[0] is always the newest because Get-WinEvent returns newest-first.
  $eventsToProcess = @($errors[0])
  Write-Host "No timestamp found. Notifying only for the most recent error to avoid flooding."
} else {
  # NORMAL RUN LOGIC:
  # Sort ascending so emails are sent in the order they actually happened.
  $eventsToProcess = $errors | Sort-Object TimeCreated
}

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
    
  # Check the return value of the function
  if (Send-NotificationEmail -Subject $subject -Body $htmlBody) {
      Write-Host "Email Notification sent for '$serviceName'."
  }
}
