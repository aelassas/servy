<#
.SYNOPSIS
    Shared utility module for Servy notification systems.

.DESCRIPTION
    Provides standardized methods for reading/writing event watermarks, parsing Servy event logs,
    and managing fallback logging. This ensures DRY compliance across Toast and Email notification channels.

.NOTES
    Author      : Akram El Assas
    Project     : Servy
    Repository  : https://github.com/aelassas/servy
#>

# -------------------------------
# Internal Dependencies
# -------------------------------
# Dot-source the event fetching script into the module's private scope
$getErrorsScript = Join-Path $PSScriptRoot "Get-ServyLastErrors.ps1"
if (Test-Path $getErrorsScript) {
    . $getErrorsScript
} else {
    Write-Warning "Servy-Watermark Module: Missing required dependency at '$getErrorsScript'"
}

# Event ID Taxonomy (Refer to src/Servy.Core/Logging/EventIds.cs for updates)
# 3000-3099: Core Errors | 3100-3199: Script Errors
$EVENT_ID_ERROR = 3103
$EVENT_ID_ERROR_DEP = 3104

# -------------------------------
# Helper: Fallback Logging
# -------------------------------
function Write-FallbackError {
    <#
    .SYNOPSIS
        Logs an error to the Windows Application Event Log, with a local file fallback.
    #>
    param(
        [string]$Message, 
        [string]$scriptDir,
        [string]$FallbackFileName = "ServyNotificationFallback.log"
    )
    
    Write-Host "ERROR: $Message" -ForegroundColor Red

    try {
        # Ensure source exists before writing to event log
        Write-EventLog -LogName Application -Source "Servy" -EventId $EVENT_ID_ERROR `
          -EntryType Error -Message $Message -ErrorAction Stop
    } catch {
        $logFile = Join-Path $scriptDir $FallbackFileName
        "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $Message" | Out-File -FilePath $logFile -Append
    }
}

function Read-Watermark {
    <#
    .SYNOPSIS
        Reads the last successfully processed event timestamp from disk.
    #>
    param([string]$TimestampFile)
    
    $lastProcessed = $null
    if (Test-Path $TimestampFile) {
        try {
            $lastProcessed = [DateTime]::ParseExact(
                (Get-Content $TimestampFile -ErrorAction Stop),
                'o',
                [System.Globalization.CultureInfo]::InvariantCulture,
                [System.Globalization.DateTimeStyles]::RoundtripKind
            )
        } catch { 
            Write-Warning "Could not parse timestamp file; treating as first run - will only show the most recent event."
        }
    }
    return $lastProcessed
}

function Update-Watermark {
    <#
    .SYNOPSIS
        Safely increments and writes the processing watermark to disk, guarding against concurrent overwrites.
    #>
    param(
        [string]$TimestampFile,
        [DateTime]$TimeCreated,
        [string]$ScriptDir
    )

    if ($null -eq $TimeCreated) { return }

    # --- CRITICAL: Always advance the watermark ---
    # Update timestamp immediately for this specific event, regardless of email/toast success.
    $newestTimestamp = $TimeCreated.AddTicks(1)
    $shouldWrite = $true
    
    # 1. Ensure the new timestamp is strictly greater than the one currently in the file
    if (Test-Path $TimestampFile) {
        try {
            # Read current file text to catch concurrent updates from other script instances
            $currentFileContent = [System.IO.File]::ReadAllText($TimestampFile).Trim()
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
            [System.IO.File]::WriteAllText($TimestampFile, $timestampString, [System.Text.Encoding]::UTF8)
            Write-Host "Timestamp updated to: $timestampString"
        } catch {
            Write-FallbackError -Message "Failed to update timestamp file: $($_.Exception.Message)" -scriptDir $ScriptDir
        }
    }
}

function Get-EventsToProcess {
    <#
    .SYNOPSIS
        Fetches and sorts the new Servy errors based on the provided watermark.
    #>
    param(
        [string]$ScriptDir,
        [System.Nullable[DateTime]]$LastProcessed = $null
    )

    # LOGIC FIX: Calling the cmdlet directly since it is now dot-sourced in the module scope
    $errors = Get-ServyLastErrors -LastProcessed $LastProcessed

    # CHECK: If no errors, exit quietly
    if ($null -eq $errors -or $errors.Count -eq 0) {
        return $null
    }

    # Chronological sorting for email sequence
    if ($null -eq $LastProcessed) {
        # FIRST RUN LOGIC: Only notify for the most recent to avoid flood
        # Wrapping in @() ensures eventsToProcess is always an array
        return @($errors[0])
    } else {
        # NORMAL RUN LOGIC: Chronological order
        # Explicitly cast to array to handle single-event scenarios in PS 2.0
        return @($errors | Sort-Object TimeCreated)
    }
}

function ConvertFrom-ServyEventMessage {
    <#
    .SYNOPSIS
        Extracts the service name and detailed log text from a raw Servy event message.
    #>
    param([string]$Message)

    # LOGIC FIX: Identify logs written by the notification scripts to prevent feedback loops
    $isFeedbackLoop = ($Message -match "^ServyFailureEmail:") -or 
                      ($Message -match "^ServyToast:") -or 
                      ($Message -match "^Servy Notification Error:")

    # 1. Parse raw message context
    if ($Message -match "^\[(.+?)\]\s*(.+)$") {
        return @{
            ServiceName = $matches[1]
            LogText = $matches[2]
            IsFeedbackLoop = $false
        }
    } else {
        return @{
            ServiceName = "Unknown Service"
            LogText = $Message
            IsFeedbackLoop = $isFeedbackLoop
        }
    }
}

Export-ModuleMember -Function Write-FallbackError, Read-Watermark, Update-Watermark, Get-EventsToProcess, ConvertFrom-ServyEventMessage -Variable EVENT_ID_ERROR, EVENT_ID_ERROR_DEP