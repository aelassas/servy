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
# 1. Determine Script Root
# -------------------------------
$scriptDir = $PSScriptRoot

$timestampFile = Join-Path $scriptDir "last-processed-toast.dat"
$fallbackLogFile = "ServyNotification.log"

# Event ID Taxonomy (Refer to src/Servy.Core/Logging/EventIds.cs for updates)
# 3000-3099: Core Errors | 3100-3199: Script Errors
$EVENT_ID_DEPENDENCY_ERROR = 3104 # mirrors EventIds.ScheduledTaskScriptDependencyError

$MaxToastTagLength       = 64    # WinRT ToastNotification.Tag limit
$ToastExpirationMinutes  = 5
$InterToastDelayMs       = 500   # debounce flood of toasts

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
            # Best-effort: the 'Servy' event source may not be registered, so guard with try/catch.
            Write-EventLog -LogName Application -Source "Servy" -EventId $EVENT_ID_DEPENDENCY_ERROR `
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
    [DateTime]$TimeCreated,
    [string]$scriptDir,
    [string]$FallbackLogFile
  )

  try {
      # Mask sensitive data in the notification before sending
      $LogText     = Protect-SensitiveString -Text $LogText
      $ServiceName = Protect-SensitiveString -Text $ServiceName
  } catch [System.Text.RegularExpressions.RegexMatchTimeoutException] {
      $LogText     = "[Redacted - log scrubbing timed out]"
      $ServiceName = "[Redacted]"
      Write-FallbackError -Message "ServyToast: Protect-SensitiveString timed out; falling back to placeholder body." `
                        -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
  }

  $ToastTitle = "Servy - $ServiceName"
    
  try {
    # Load WinRT assemblies
    [void][Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
    [void][Windows.UI.Notifications.NotificationSetting, Windows.UI.Notifications, ContentType = WindowsRuntime]
        
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
        # ROBUSTNESS: Schema structure mismatches are unrecoverable; classify as a permanent failure.
        Write-FallbackError -Message "ServyToast: Unsupported Toast XML structure. Could not locate text nodes for Title or Body." -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
        return 'PermanentFailure'
    }

    # Append content
    [void]$titleNode.AppendChild($rawXml.CreateTextNode($ToastTitle))
    [void]$bodyNode.AppendChild($rawXml.CreateTextNode($LogText))

    # Re-wrap in WinRT XML DOM
    $serializedXml = New-Object Windows.Data.Xml.Dom.XmlDocument
    $serializedXml.LoadXml($rawXml.OuterXml)

    # Initialize Notification
    $toast = New-Object Windows.UI.Notifications.ToastNotification($serializedXml)
    $tag = "Servy-$($TimeCreated.ToString('yyyyMMddHHmmssfff'))-$($ServiceName -replace '\s','')"
    $tag = $tag.Substring(0, [Math]::Min($tag.Length, $MaxToastTagLength)) # Max $MaxToastTagLength chars
    $toast.Tag = $tag
    $toast.Group = "Servy" # cluster all Servy toasts together
    $toast.ExpirationTime = [DateTimeOffset]::Now.AddMinutes($ToastExpirationMinutes)

    # --- ROBUSTNESS: PRE-FLIGHT NOTIFICATION ENVIRONMENT PROBE ---
    # Verify app notification permissions and OS-level Focus Assist/DND status before staging execution.
    # If notifications are suppressed globally or scoped away via policy, fail-fast to save the watermark.
    $notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("PowerShell")
    try {
        if ($notifier.Setting -ne [Windows.UI.Notifications.NotificationSetting]::Enabled) {
            $settingState = $notifier.Setting.ToString()
            Write-FallbackError -Message "ServyToast: Notification delivery aborted due to platform settings suppression ($settingState). Skipping watermark advance." -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
            
            # ROBUSTNESS: Treat platform-level notification suppression as a terminal delivery result.
            # Returning 'PermanentFailure' ensures the watermark is updated and the queue 
            # continues processing, avoiding head-of-line blocking loops while notifications 
            # remain unavailable (e.g., Focus Assist or DND enabled).
            return 'PermanentFailure'
        }
    } catch {
        # Setting probe restricted; proceed with delivery anyway.
    }

    # Track synchronous platform failures safely via local reference tracking blocks.
    $failedRef = [ref]$false
    $failCode  = [ref]([int]0)

    # Event Handlers (Async Error Capture)
    # ROBUSTNESS: WinRT fires the toast failure asynchronously on an external threadpool thread that 
    # lacks a PowerShell runspace. Avoid calling any cmdlets inside the scriptblock handler to 
    # prevent RunspaceAvailability exceptions; capture primitive types across the boundary instead.
    $null = $toast.add_Failed({
        param($evtSender, $evtArgs)
        $failedRef.Value = $true
        $failCode.Value  = [int]$evtArgs.ErrorCode
      })

    $notifier.Show($toast)
    
    # ROBUSTNESS: Block execution and loop to await synchronous platform failures.
    # Extended safety deadline window from 750ms to 2000ms to absorb heavy asynchronous
    # system load anomalies, ensuring late-firing WinRT callbacks are reliably captured.
    $deadline = [DateTime]::UtcNow.AddMilliseconds(2000)
    while ([DateTime]::UtcNow -lt $deadline -and -not $failedRef.Value) {
        Start-Sleep -Milliseconds 50
    }

    if ($failedRef.Value) {
        Write-FallbackError -Message ("ServyToast: Delivery failed (0x{0:X})." -f $failCode.Value) -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
        return 'TransientFailure'
    }

    return 'Success'
  } catch {
    # System path drops or memory boundaries represent runtime environmental/transient errors
    $syncError = "ServyToast: Notification path failed. Details: $($_.Exception.Message)"
    Write-FallbackError -Message $syncError -scriptDir $scriptDir -FallbackFileName $FallbackLogFile
    return 'TransientFailure'
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

    # Show the notification and capture the tri-state delivery resolution
    $deliveryStatus = Show-Notification -ServiceName $parsed.ServiceName `
                                        -LogText $parsed.LogText `
                                        -TimeCreated $evt.TimeCreated `
                                        -scriptDir $scriptDir `
                                        -FallbackLogFile $fallbackLogFile
    
    # ROBUSTNESS: Re-architected watermarking filter path to match ServyFailureEmail.ps1.
    # Advance the event queue timestamp pointer for either clear success or permanent, unfixable configuration blocks.
    # Only break the processing loop without modifying the index watermark on clear TransientFailure states.
    switch ($deliveryStatus) {
        'Success'          { Update-Watermark -TimestampFile $timestampFile -TimeCreated $evt.TimeCreated -ScriptDir $scriptDir }
        'PermanentFailure' { Update-Watermark -TimestampFile $timestampFile -TimeCreated $evt.TimeCreated -ScriptDir $scriptDir }
    }

    if ($deliveryStatus -eq 'TransientFailure') {
        Write-Host "Notification failed due to a transient condition. Halting queue processing for retry."
        break
    }
    
    Start-Sleep -Milliseconds $InterToastDelayMs
}