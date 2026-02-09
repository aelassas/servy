<#
.SYNOPSIS
    Monitors Servy error events in the Windows Application log and sends notification emails.

.DESCRIPTION
    This script performs the following:
      1. Filters the Windows Application event log for errors related to 'Servy'.
      2. Retrieves the most recent error.
      3. Parses the error message to extract the service name and log text.
      4. Sends a notification email to the administrator with the details.

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

    # Configure your SMTP settings
    $smtpServer = "smtp.example.com"
    $smtpPort   = 587
    $smtpUser   = "username@example.com"
    $smtpPass   = "password"
    $from       = "servy.notifications@example.com"
    $to         = "admin@example.com"

    $securePass = ConvertTo-SecureString $smtpPass -AsPlainText -Force
    $cred = New-Object System.Management.Automation.PSCredential ($smtpUser, $securePass)

    Send-MailMessage -From $from -To $to -Subject $Subject -Body $Body -BodyAsHtml -SmtpServer $smtpServer -Port $smtpPort -Credential $cred -UseSsl
}

# -------------------------------
# Get the latest Servy error event
# -------------------------------
$filter = @{
    LogName = 'Application'
    ProviderName = 'Servy'
    Level = 2  # Error
}

$lastError = Get-WinEvent -FilterHashtable $filter | Sort-Object TimeCreated -Descending | Select-Object -First 1

if ($lastError) {
    $message = $lastError.Message
    if ($message -match "^\[(.+?)\]\s*(.+)$") {
        $ServiceName = $matches[1]
        $LogText = $matches[2]
    } else {
        $ServiceName = "Unknown Service"
        $LogText = $message
    }

    $subject = "Servy - $ServiceName Failure"
    $body    = "A failure has been detected in service '$ServiceName'." + [Environment]::NewLine + "Details: $LogText"
    $htmlBody = $body -replace "`r?`n", "<br>"

    Send-NotificationEmail -Subject $subject -Body $htmlBody
} else {
    Write-Host "No Servy error events found."
}
