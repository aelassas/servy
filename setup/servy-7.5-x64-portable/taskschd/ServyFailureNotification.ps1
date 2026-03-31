<#
.SYNOPSIS
    Displays Windows toast notifications for the latest Servy error events.

.DESCRIPTION
    This script:
      1. Filters the Windows Application event log for errors related to 'Servy'.
      2. Retrieves the most recent error.
      3. Parses the error message to extract the service name and log text.
      4. Shows a Windows toast notification with the error details.

.NOTES
    Author : Akram El Assas
    Project: Servy
    Requirements:
      - PowerShell 5.1+ (or PowerShell Core)
      - Access to the Windows Application event log
      - Running on Windows 10 or later for toast notifications

.EXAMPLE
    .\ServyFailureNotification.ps1
    Displays a toast notification for the latest Servy error event.

#>

# -------------------------------
# Function to show toast notification
# -------------------------------
function Show-Notification {
    [cmdletbinding()]
    Param (
        [string] $ToastTitle,
        [string] [parameter(ValueFromPipeline)] $ToastText
    )

    [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null
    $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)

    $rawXml = [xml] $template.GetXml()
    ($rawXml.toast.visual.binding.text | where {$_.id -eq "1"}).AppendChild($rawXml.CreateTextNode($ToastTitle)) > $null
    ($rawXml.toast.visual.binding.text | where {$_.id -eq "2"}).AppendChild($rawXml.CreateTextNode($ToastText)) > $null

    $serializedXml = New-Object Windows.Data.Xml.Dom.XmlDocument
    $serializedXml.LoadXml($rawXml.OuterXml)

    $toast = [Windows.UI.Notifications.ToastNotification]::new($serializedXml)
    $toast.Tag = "Servy"
    $toast.Group = "Servy"
    $toast.ExpirationTime = [DateTimeOffset]::Now.AddMinutes(1)

    $notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("PowerShell")
    $notifier.Show($toast);
}

# -------------------------------
# Get latest Servy error event
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
        $serviceName = $matches[1]
        $logText = $matches[2]
    } else {
        $serviceName = "Unknown Service"
        $logText = $message
    }

    Show-Notification -ToastTitle "Servy - $serviceName" -ToastText $logText
} else {
    Write-Host "No Servy error events found."
}
