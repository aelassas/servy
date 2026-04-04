<#
.SYNOPSIS
    Displays Windows toast notifications for the latest Servy error events.

.DESCRIPTION
    This script:
      1. Filters the Windows Application event log for errors related to 'Servy'.
      2. Retrieves all new errors since the last execution using a timestamp file.
      3. Parses the error messages to extract service names and log text.
      4. Shows individual Windows toast notifications for each failure.

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
        [string] $ServiceName,
        [string] $LogText,
        [string] $ModuleRoot
    )

    $ToastTitle = "Servy - $ServiceName"
    
    try {
        [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null
        $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)

        $rawXml = [xml] $template.GetXml()
        ($rawXml.toast.visual.binding.text | Where-Object {$_.id -eq "1"}).AppendChild($rawXml.CreateTextNode($ToastTitle)) > $null
        ($rawXml.toast.visual.binding.text | Where-Object {$_.id -eq "2"}).AppendChild($rawXml.CreateTextNode($LogText)) > $null

        $serializedXml = New-Object Windows.Data.Xml.Dom.XmlDocument
        $serializedXml.LoadXml($rawXml.OuterXml)

        $toast = [Windows.UI.Notifications.ToastNotification]::new($serializedXml)
        $toast.Tag = "Servy"
        $toast.Group = "Servy"
        $toast.ExpirationTime = [DateTimeOffset]::Now.AddMinutes(5)

        # ASYNCHRONOUS FALLBACK: Handle cases where Show() succeeds but delivery fails
        $null = $toast.add_Failed({
            param($evtSender, $evtArgs)
            $asyncError = "ServyToast: Delivery failed for '$ServiceName'. ErrorCode: $($evtArgs.ErrorCode)"
            Write-FallbackError -Message $asyncError -ModuleRoot $ModuleRoot
        })

        $notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("PowerShell")
        $notifier.Show($toast)
    }
    catch {
        # SYNCHRONOUS FALLBACK: Handle immediate failures (e.g., no interactive session)
        $syncError = "ServyToast: Failed to show toast for '$ServiceName'. Details: $_. Original Log: $LogText"
        Write-FallbackError -Message $syncError -ModuleRoot $ModuleRoot
    }
}

# -------------------------------
# Helper: Fallback Logging
# -------------------------------
function Write-FallbackError {
    Param($Message, $ModuleRoot)
    
    try {
        # Attempt to write to Application Log. Requires 'Servy' source to be registered.
        Write-EventLog -LogName Application -Source "Servy" -EventId 9903 `
                       -EntryType Warning -Message $Message -ErrorAction Stop
    }
    catch {
        # Last resort: File log
        $logFile = Join-Path $ModuleRoot "ServyNotification-failures.log"
        "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $Message" | Out-File -FilePath $logFile -Append
    }
}

# -------------------------------
# 1. Determine Script Root (PS 2.0+ Compatible)
# -------------------------------
if ($PSVersionTable.PSVersion.Major -ge 3) {
    $ModuleRoot = $PSScriptRoot
} else {
    $ModuleRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
}

$timestampFile = Join-Path $ModuleRoot "last-processed-toast.dat"

# -------------------------------
# 2. Read Last Processed Timestamp
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
# 3. Build Filter and Query Events
# -------------------------------
$filter = @{
    LogName = 'Application'
    ProviderName = 'Servy'
    Level = 2  # Error
}

if ($lastProcessed) {
    $filter.StartTime = $lastProcessed
}

try {
    $errors = @(Get-WinEvent -FilterHashtable $filter -ErrorAction Stop)
}
catch {
    exit 0 
}

# -------------------------------
# 4. Precision Filtering & Timestamp Update
# -------------------------------
if ($lastProcessed) {
    $errors = @($errors | Where-Object { $_.TimeCreated -gt $lastProcessed })
}

if ($errors.Count -eq 0) { exit 0 }

# -------------------------------
# 5. Update Timestamp File
# -------------------------------
# Adding 1 tick to ensure we don't pick up the same event again next time
$newestTimestamp = $errors[0].TimeCreated.AddTicks(1)
$newestTimestamp.ToString("o") | Out-File $timestampFile -Force

# -------------------------------
# 6. Process Events & Send Toast Notifications
# -------------------------------
# Sort ascending so notifications are sent in the exact chronological order the errors occurred
if ($null -eq $lastProcessed) {
    # FIRST RUN LOGIC:
    # We only notify for the single most recent event to avoid flooding.
    # $errors[0] is always the newest because Get-WinEvent returns newest-first.
    $eventsToProcess = @($errors[0])
    Write-Host "No timestamp found. Notifying only for the most recent error to avoid flooding."
} else {
    # NORMAL RUN LOGIC:
    # Sort ascending so notifications are sent in the order they actually happened.
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

    Show-Notification -ServiceName $serviceName -LogText $logText -ModuleRoot $ModuleRoot
    
    # Brief pause to help the Action Center sequence the toasts properly
    Start-Sleep -Milliseconds 500
}
