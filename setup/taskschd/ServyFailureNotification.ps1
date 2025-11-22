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
    $Template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)

    $RawXml = [xml] $Template.GetXml()
    ($RawXml.toast.visual.binding.text | where {$_.id -eq "1"}).AppendChild($RawXml.CreateTextNode($ToastTitle)) > $null
    ($RawXml.toast.visual.binding.text | where {$_.id -eq "2"}).AppendChild($RawXml.CreateTextNode($ToastText)) > $null

    $SerializedXml = New-Object Windows.Data.Xml.Dom.XmlDocument
    $SerializedXml.LoadXml($RawXml.OuterXml)

    $Toast = [Windows.UI.Notifications.ToastNotification]::new($SerializedXml)
    $Toast.Tag = "Servy"
    $Toast.Group = "Servy"
    $Toast.ExpirationTime = [DateTimeOffset]::Now.AddMinutes(1)

    $Notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("PowerShell")
    $Notifier.Show($Toast);
}

# -------------------------------
# Get latest Servy error event
# -------------------------------
$Filter = @{
    LogName = 'Application'
    ProviderName = 'Servy'
    Level = 2  # Error
}

$LastError = Get-WinEvent -FilterHashtable $Filter | Sort-Object TimeCreated -Descending | Select-Object -First 1

if ($LastError) {
    $Message = $LastError.Message
    if ($Message -match "^\[(.+?)\]\s*(.+)$") {
        $ServiceName = $matches[1]
        $LogText = $matches[2]
    } else {
        $ServiceName = "Unknown Service"
        $LogText = $Message
    }

    Show-Notification -ToastTitle "Servy - $ServiceName" -ToastText $LogText
} else {
    Write-Host "No Servy error events found."
}
