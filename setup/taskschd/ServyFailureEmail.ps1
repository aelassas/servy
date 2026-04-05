<#
.SYNOPSIS
    Monitors Servy error events in the Windows Application log and sends notification emails.

.DESCRIPTION
    This script performs the following:
      1. Filters the Windows Application event log for errors related to 'Servy'.
      2. Retrieves all new errors since the last execution.
      3. Parses the error messages to extract the service name and log text.
      4. Sends a notification email to the administrator for each failure.
      5. Saves the timestamp of the latest event to prevent duplicate alerts.

.PARAMETER None
    No parameters are required. All settings (SMTP, recipient, etc.) are configured inside the script.

.NOTES
    Author : Akram El Assas
    Project: Servy
    Requirements:
      - PowerShell 5.1+ (or Core)
      - Access to Application event log
      - SMTP server credentials configured in the script

.EXAMPLE
    .\ServyFailureEmail.ps1
    Sends an email for the latest Servy error event in the Application log.

#>

# -------------------------------
# Email notification function
# -------------------------------
function Send-NotificationEmail {
    [cmdletbinding()]
    Param (
        [string] $Subject,
        [string] [parameter(ValueFromPipeline)] $Body
    )

    # ---------------------------------------------------------------
    # IMPORTANT: These are placeholder values. Replace them before use.
    # For production environments, consider using encrypted credentials:
    #   $cred = Get-Credential
    #   $cred | Export-Clixml "C:\Secure\smtp-cred.xml"
    # Then load with:
    #   $cred = Import-Clixml "C:\Secure\smtp-cred.xml"
    # ---------------------------------------------------------------
    $smtpServer = "smtp.example.com"
    $smtpPort   = 587
    $smtpUser   = "username@example.com"
    $smtpPass   = "password"
    $from       = "servy.notifications@example.com"
    $to         = "admin@example.com"

    try {
      $securePass = ConvertTo-SecureString $smtpPass -AsPlainText -Force
      $cred = New-Object System.Management.Automation.PSCredential ($smtpUser, $securePass)

      Send-MailMessage -From $from -To $to -Subject $Subject -Body $Body -BodyAsHtml -SmtpServer $smtpServer -Port $smtpPort -Credential $cred -UseSsl -ErrorAction Stop
    }
    catch {
      $errorMsg = "ServyFailureEmail: Failed to send notification email for service '$serviceName'. Error: $_"
      Write-Error $errorMsg

      # Attempt to log to Windows Event Log as fallback
      try {
        Write-EventLog -LogName Application -Source "Servy" -EventId 9900 -EntryType Warning -Message $errorMsg -ErrorAction Stop
      }
      catch {
        # Last resort: write to a local log file
        $errorMsg | Out-File -FilePath (Join-Path $PSScriptRoot "ServyFailureEmail.log") -Append -ErrorAction SilentlyContinue
      }
      
      exit 1
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
    
    Send-NotificationEmail -Subject $subject -Body $htmlBody
    Write-Host "Email Notification sent for '$serviceName'."
}
