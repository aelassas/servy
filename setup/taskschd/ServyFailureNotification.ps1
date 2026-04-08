<#
.SYNOPSIS
    Displays Windows toast notifications for the latest Servy error events.

.DESCRIPTION
    This script leverages modern Windows Runtime (WinRT) APIs to provide 
    interactive desktop alerts for service failures.
    
    This script:
      1. Filters the Windows Application event log for errors related to 'Servy'.
      2. Retrieves all new errors since the last execution using a timestamp file.
      3. Parses the error messages to extract service names and log text.
      4. Shows individual Windows toast notifications for each failure.

.NOTES
    Author      : Akram El Assas
    Project     : Servy
    Repository  : https://github.com/aelassas/servy
    
    Requirements:
      - PowerShell 5.1 or later (Required for WinRT).
      - Windows 10 (Build 10240+) or Windows 11.
      - Access to the Windows Application event log.
      - An active interactive user session (Toasts do not show in Session 0).

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

    # --- VERSION GATE ---
    # WinRT projection and the ::new() constructor require PowerShell 5.0+.
    if ($PSVersionTable.PSVersion.Major -lt 5) {
        $verError = "ServyToast: Skipping toast for '$ServiceName'. Toasts require PowerShell 5.0+ (Detected: $($PSVersionTable.PSVersion.Major))."
        Write-FallbackError -Message $verError -ModuleRoot $ModuleRoot
        return
    }

    $ToastTitle = "Servy - $ServiceName"
    
    try {
        # 1. VALIDATE WinRT AVAILABILITY
        # If this fails, catch block triggers the fallback immediately.
        [void][Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
        
        $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent(
            [Windows.UI.Notifications.ToastTemplateType]::ToastText02
        )

        # 2. BUILD NOTIFICATION XML
        $rawXml = [xml]$template.GetXml()
        
        # Using full cmdlet names to avoid alias issues (where-object)
        $titleNode = $rawXml.toast.visual.binding.text | Where-Object { $_.id -eq "1" }
        $bodyNode  = $rawXml.toast.visual.binding.text | Where-Object { $_.id -eq "2" }

        [void]$titleNode.AppendChild($rawXml.CreateTextNode($ToastTitle))
        [void]$bodyNode.AppendChild($rawXml.CreateTextNode($LogText))

        $serializedXml = New-Object Windows.Data.Xml.Dom.XmlDocument
        $serializedXml.LoadXml($rawXml.OuterXml)

        # 3. CONFIGURE TOAST OBJECT
        $toast                = New-Object Windows.UI.Notifications.ToastNotification($serializedXml)
        $toast.Tag            = "Servy"
        $toast.Group          = "Servy"
        $toast.ExpirationTime = [DateTimeOffset]::Now.AddMinutes(5)

        # 4. ASYNCHRONOUS FALLBACK (Handles delivery failures like Focus Assist)
        $null = $toast.add_Failed({
            param($evtSender, $evtArgs)
            $asyncError = "ServyToast: Delivery failed for '$ServiceName'. ErrorCode: $($evtArgs.ErrorCode)"
            Write-FallbackError -Message $asyncError -ModuleRoot $ModuleRoot
        })

        # 5. ATTEMPT DISPLAY
        $notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("PowerShell")
        $notifier.Show($toast)
    }
    catch {
        # SYNCHRONOUS FALLBACK
        # Handles: Type-load errors, no interactive session, or UI-thread blocking.
        $syncError = "ServyToast: Notification path failed for '$ServiceName'. Details: $($_.Exception.Message)"
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
        $logFile = Join-Path $ModuleRoot "ServyNotification.log"
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

# CHECK: If no errors, exit quietly
if ($null -eq $errors -or $errors.Count -eq 0) {
    Write-Host "No new errors to process."
    exit 0
}

# -------------------------------
# 5. Process Events & Send Toast Notifications
# -------------------------------
if ($null -eq $lastProcessed) {
    # FIRST RUN LOGIC: Only notify for the most recent to avoid flood
    $eventsToProcess = @($errors[0])
} else {
    # NORMAL RUN LOGIC: Chronological order
    $eventsToProcess = $errors | Sort-Object TimeCreated
}

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

    # Show the notification
    Show-Notification -ServiceName $serviceName -LogText $logText -ModuleRoot $ModuleRoot
    
    # Track this timestamp as successfully processed
    $lastSuccessfulTimestamp = $evt.TimeCreated
    
    Start-Sleep -Milliseconds 500
}

# -------------------------------
# 6. Update Timestamp File (Now safe and at the end)
# -------------------------------
if ($null -ne $lastSuccessfulTimestamp) {
    $newestTimestamp = $lastSuccessfulTimestamp.AddTicks(1)
    $newestTimestamp.ToString("o") | Out-File $timestampFile -Force
    Write-Host "Timestamp updated to: $($newestTimestamp.ToString('o'))"
}
