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

# Event ID Taxonomy (Refer to src/Servy.Core/Logging/EventIds.cs for updates)
# 3000-3099: Core Errors | 3100-3199: Script Errors
$EVENT_ID_ERROR = 3103
$EVENT_ID_ERROR_DEP = 3104

# -------------------------------
# Function to show toast notification
# -------------------------------
function Show-Notification {
  [CmdletBinding()]
  param (
    [string]$ServiceName,
    [string]$LogText,
    [string]$scriptDir
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
        Write-FallbackError -Message "ServyToast: Delivery failed (0x$($evtArgs.ErrorCode.ToString('X')))." -scriptDir $PSScriptRoot
      })

    $notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("PowerShell")
    $notifier.Show($toast)
  } catch {
    $syncError = "ServyToast: Notification path failed. Details: $($_.Exception.Message)"
    Write-FallbackError -Message $syncError -scriptDir $scriptDir
  }
}

# -------------------------------
# Helper: Fallback Logging
# -------------------------------
function Write-FallbackError {
  param(
    [string]$Message, 
    [string]$scriptDir
  )
    
  try {
    Write-EventLog -LogName Application -Source "Servy" -EventId $EVENT_ID_ERROR `
      -EntryType Error -Message $Message -ErrorAction Stop
  }
  catch {
    $logFile = Join-Path $scriptDir "ServyNotification.log"
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $Message" | Out-File -FilePath $logFile -Append
  }
}

# -------------------------------
# 1. Determine Script Root (PS 2.0+ Compatible)
# -------------------------------
if ($PSVersionTable.PSVersion.Major -ge 3) {
  $scriptDir = $PSScriptRoot
} else {
  $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
}

$timestampFile = Join-Path $scriptDir "last-processed-toast.dat"

# -------------------------------
# 2. Imports
# -------------------------------
$requiredScripts = @(
    "Get-ServyLastErrors.ps1",
    "ServySecurity.ps1"
)

foreach ($scriptName in $requiredScripts) {
    $scriptPath = Join-Path $scriptDir $scriptName

    if (-not (Test-Path $scriptPath)) {
        $errorMsg = "Servy Notification Error: Required dependency not found at '$scriptPath'. Please ensure the file exists in the script directory."
        
        # 1. Attempt to log to Event Log for administrator visibility
        try {
            Write-EventLog -LogName Application -Source "Servy" -EventId $EVENT_ID_ERROR_DEP `
                -EntryType Error -Message $errorMsg -ErrorAction Stop
        } catch {
            # 2. Fallback to stderr if Event Log fails (or source isn't registered)
            Write-Error $errorMsg
        }

        # 3. Exit with error code
        exit 1
    }

    # File exists, proceed with dot-sourcing
    . $scriptPath
}

# -------------------------------
# 3. Read Last Processed Timestamp
# -------------------------------
$lastProcessed = $null
if (Test-Path $timestampFile) {
  try {
    $lastProcessed = [DateTime]::ParseExact(
        (Get-Content $timestampFile -ErrorAction Stop),
        'o',
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::RoundtripKind
    )
  } catch { 
    Write-Warning "Could not parse timestamp file; treating as first run - will only show the most recent event."
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
    # Wrapping in @() ensures $eventsToProcess is always an array
    $eventsToProcess = @($errors[0])
} else {
    # NORMAL RUN LOGIC: Chronological order
    # Explicitly cast to array to handle single-event scenarios in PS 2.0
    $eventsToProcess = @($errors | Sort-Object TimeCreated)
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
  Show-Notification -ServiceName $serviceName -LogText $logText -scriptDir $scriptDir
    
  # Track this timestamp as successfully processed
  $lastSuccessfulTimestamp = $evt.TimeCreated
    
  # Update timestamp immediately for this specific event
  if ($null -ne $lastSuccessfulTimestamp) {
      $newestTimestamp = $lastSuccessfulTimestamp.AddTicks(1)
      $shouldWrite = $true
      
      # 1. Ensure the new timestamp is strictly greater than the one currently in the file
      if (Test-Path $timestampFile) {
          try {
              # Read current file text to catch concurrent updates from other script instances
              $currentFileContent = [System.IO.File]::ReadAllText($timestampFile).Trim()
              if (-not [string]::IsNullOrWhiteSpace($currentFileContent)) {
                  $fileTimestamp = [DateTime]::ParseExact(
                      $currentFileContent,
                      'o',
                      [System.Globalization.CultureInfo]::InvariantCulture,
                      [System.Globalization.DateTimeStyles]::RoundtripKind
                  )

                  if ($newestTimestamp -le $fileTimestamp) {
                      $shouldWrite = $false
                  }
              }
          } catch {
              # If file is locked, corrupt (e.g. previous NULL char bug), or unparseable, overwrite it
              Write-Host "Could not parse current timestamp file during update check. Overwriting to heal file."
          }
      }
      
      # 2. Write to file only if necessary, explicitly forcing UTF8
      if ($shouldWrite) {
          $timestampString = $newestTimestamp.ToString("o")
          try {
              # Explicitly use UTF8 encoding to prevent PowerShell from writing UTF-16LE (which causes the NULL chars)
              [System.IO.File]::WriteAllText($timestampFile, $timestampString, [System.Text.Encoding]::UTF8)
              Write-Host "Timestamp updated to: $timestampString"
          }
          catch {
              Write-FallbackError -Message "Failed to update timestamp file: $($_.Exception.Message)" -scriptDir $scriptDir
          }
      }
  }

  Start-Sleep -Milliseconds 500
}
