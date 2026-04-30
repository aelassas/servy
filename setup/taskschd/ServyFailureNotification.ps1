#requires -Version 5.1
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
# 1. Determine Script Root (PS 2.0+ Compatible)
# -------------------------------
if ($PSVersionTable.PSVersion.Major -ge 3) {
  $scriptDir = $PSScriptRoot
} else {
  $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
}

$timestampFile = Join-Path $scriptDir "last-processed-toast.dat"
$fallbackLogFile = "ServyNotification.log"

# -------------------------------
# 2. Imports
# -------------------------------
$requiredDependencies = @(
    "Servy-Watermark.psm1",
    "ServySecurity.ps1"
)

foreach ($dep in $requiredDependencies) {
    $depPath = Join-Path $scriptDir $dep

    if (-not (Test-Path $depPath)) {
        $errorMsg = "Servy Notification Error: Required dependency not found at '$depPath'. Please ensure the file exists in the script directory."
        
        # 1. Attempt to log to Event Log for administrator visibility
        try {
            Write-EventLog -LogName Application -Source "Servy" -EventId 3104 `
                -EntryType Error -Message $errorMsg -ErrorAction Stop
        } catch {
            # 2. Fallback to stderr if Event Log fails (or source isn't registered)
            Write-Error $errorMsg
        }

        # 3. Exit with error code
        exit 1
    }

    # File exists, proceed with dot-sourcing or importing
    if ($dep -like "*.psm1") { Import-Module $depPath -Force } else { . $depPath }
}

# -------------------------------
# Function to show toast notification
# -------------------------------
function Show-Notification {
  [CmdletBinding()]
  param (
    [string]$ServiceName,
    [string]$LogText,
    [string]$scriptDir,
    [string]$FallbackLogFile
  )

  # Mask sensitive data in the notification before sending
  $LogText = Protect-SensitiveString -Text $LogText

  $ToastTitle = "Servy - $ServiceName"
    
  try {
    # Load WinRT assemblies
    [void][Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
        
    $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent(
      [Windows.UI.Notifications.ToastTemplateType]::ToastText02
    )

    $rawXml = [xml]$template.GetXml()

    # --- VALIDATION GATE ---
    # Select nodes based on standard ToastText02 schema
    $titleNode = $rawXml.toast.visual.binding.text | Where-Object { $_.id -eq "1" }
    $bodyNode  = $rawXml.toast.visual.binding.text | Where-Object { $_.id -eq "2" }

    if ($null -eq $titleNode -or $null -eq $bodyNode) {
        # If the specific IDs are missing, fallback to ordinal selection
        $titleNode = $rawXml.toast.visual.binding.text[0]
        $bodyNode  = $rawXml.toast.visual.binding.text[1]
    }

    if ($null -eq $titleNode -or $null -eq $bodyNode) {
        throw "Unsupported Toast XML structure: Could not locate text nodes for Title or Body."
    }

    # Append content
    [void]$titleNode.AppendChild($rawXml.CreateTextNode($ToastTitle))
    [void]$bodyNode.AppendChild($rawXml.CreateTextNode($LogText))

    # Re-wrap in WinRT XML DOM
    $serializedXml = New-Object Windows.Data.Xml.Dom.XmlDocument
    $serializedXml.LoadXml($rawXml.OuterXml)

    # Initialize Notification
    $toast = New-Object Windows.UI.Notifications.ToastNotification($serializedXml)
    $toast.Tag = "Servy"
    $toast.Group = "Servy"
    $toast.ExpirationTime = [DateTimeOffset]::Now.AddMinutes(5)

    # Event Handlers (Async Error Capture)
    $null = $toast.add_Failed({
        param($evtSender, $evtArgs)
        Write-FallbackError -Message "ServyToast: Delivery failed (0x$($evtArgs.ErrorCode.ToString('X')))." -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
      })

    $notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("PowerShell")
    $notifier.Show($toast)
  } catch {
    $syncError = "ServyToast: Notification path failed. Details: $($_.Exception.Message)"
    Write-FallbackError -Message $syncError -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
  }
}

# -------------------------------
# 3. Read Last Processed Timestamp
# -------------------------------
$lastProcessed = Read-Watermark -TimestampFile $timestampFile

# -------------------------------
# 4. Get Latest Errors
# -------------------------------
$eventsToProcess = Get-EventsToProcess -ScriptDir $scriptDir -LastProcessed $lastProcessed

if ($null -eq $eventsToProcess) {
    Write-Host "No new errors to process."
    exit 0
}

# -------------------------------
# 5. Process Events & Send Toast Notifications
# -------------------------------
foreach ($evt in $eventsToProcess) {
    $parsed = ConvertFrom-ServyEventMessage -Message $evt.Message
    
    # LOGIC FIX: Prevent toast loop
    if ($parsed.IsFeedbackLoop) {
        Write-Host "Skipping self-generated toast error to prevent feedback loop." -ForegroundColor DarkGray
        Update-Watermark -TimestampFile $timestampFile -TimeCreated $evt.TimeCreated -ScriptDir $scriptDir
        continue
    }

    # Show the notification
    Show-Notification -ServiceName $parsed.ServiceName -LogText $parsed.LogText -scriptDir $scriptDir -FallbackLogFile $fallbackLogFile
    
    # Track this timestamp as successfully processed
    Update-Watermark -TimestampFile $timestampFile -TimeCreated $evt.TimeCreated -ScriptDir $scriptDir
    
    Start-Sleep -Milliseconds 500
}