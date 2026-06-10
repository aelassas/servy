#Requires -Version 5.1
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
$writeLogScript = Join-Path $PSScriptRoot "Write-ServyLog.ps1"

if ((Test-Path $getErrorsScript) -and (Test-Path $writeLogScript)) {
    . $getErrorsScript
    . $writeLogScript
} else {
    $missing = @()
    if (-not (Test-Path $getErrorsScript)) { $missing += 'Get-ServyLastErrors.ps1' }
    if (-not (Test-Path $writeLogScript))  { $missing += 'Write-ServyLog.ps1' }
    throw "Servy-Watermark Module: Required dependency missing in '$PSScriptRoot': $($missing -join ', ')"
}

# Event ID Taxonomy (Refer to src/Servy.Core/Logging/EventIds.cs for updates)
# 3000-3099: Core Errors | 3100-3199: Script Errors
$EVENT_ID_ERROR = 3103

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
    
    # Console visibility: Write-Warning is captured by transcripts and visible in interactive shells.
    # Unlike Write-Host, it is not stripped in non-interactive Task Scheduler contexts.
    Write-Warning "Servy Notification Error: $Message"

    # Disk logging: Record to a physical file first. 
    # This acts as the primary audit trail for non-interactive background tasks.
    if ($scriptDir) {
        $logFile = Join-Path $scriptDir $FallbackFileName
        try {
            Write-ServyLog -FilePath $logFile -Message $Message
        } catch {
            # Silence internal disk-logging errors to allow the Event Log attempt to proceed
        }
    }

    # System visibility: Attempt to notify the Windows Application Event Log.
    try {
        # Ensure source exists before writing to event log
        Write-EventLog -LogName Application -Source "Servy" -EventId $EVENT_ID_ERROR `
          -EntryType Error -Message $Message -ErrorAction Stop
    } catch {
        # If the Event Log fails, the disk log above remains the final source of truth.
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
            $raw = (Get-Content $TimestampFile -Raw -ErrorAction Stop)
            $lastProcessed = [DateTime]::ParseExact(
                $raw.Trim(),
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
        [System.Nullable[DateTime]]$TimeCreated,
        [string]$ScriptDir
    )

    if ($null -eq $TimeCreated) { return }

    # --- CRITICAL: Always advance the watermark ---
    # Update timestamp immediately for this specific event, regardless of email/toast success.
    $newestTimestamp = $TimeCreated
    $timestampString = $newestTimestamp.ToString("o")

    # Retry loop to gracefully handle concurrent FileShare.None lock collisions
    $maxRetries = 5
    $retryDelayMs = 200

    # Define explicit temporary paths in the same directory to execute an atomic NTFS swap
    $tempFile   = "$TimestampFile.new"
    $backupFile = "$TimestampFile.bak"

    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        $fs = $null
        $reader = $null
        $writer = $null
        try {
            # Initialize state checks
            $shouldWrite = $true

            # 1. Atomic Read/Compare Phase: Safely evaluate if the watermark needs advancing
            if (Test-Path $TimestampFile) {
                $fs = [System.IO.File]::Open($TimestampFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
                $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
                $reader = New-Object System.IO.StreamReader($fs, $utf8NoBom, $false, 1024, $true)
                $currentFileContent = $reader.ReadToEnd().Trim()
                $reader.Dispose()
                $reader = $null
                $fs.Dispose()
                $fs = $null

                if (-not [string]::IsNullOrWhiteSpace($currentFileContent)) {
                    try {
                        $fileTimestamp = [DateTime]::ParseExact(
                            $currentFileContent,
                            'o',
                            [System.Globalization.CultureInfo]::InvariantCulture,
                            [System.Globalization.DateTimeStyles]::RoundtripKind
                        )
                        if ($newestTimestamp -le $fileTimestamp) {
                            $shouldWrite = $false
                        }
                    } catch {
                        # If file is corrupt or unparseable, overwrite it to heal state boundary
                        Write-Warning "Could not parse current timestamp file during update check. Overwriting to heal file."
                    }
                }
            }

            # 2. Atomic CAS Write Phase: Write to staging space, then commit atomically
            if ($shouldWrite) {
                $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
                
                # Write to isolated temporary storage file first
                $fs = [System.IO.File]::Open($tempFile, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
                $writer = New-Object System.IO.StreamWriter($fs, $utf8NoBom, 1024, $true)
                $writer.Write($timestampString)
                $writer.Dispose()
                $writer = $null
                $fs.Dispose()
                $fs = $null

                # Commit changes atomically. 
                if (Test-Path $tempFile) {
                    # PATH CONFORMANCE: Convert all paths to explicit absolute forms.
                    # Instead of $null, we provide a valid, conforming backup path string ($absoluteBackup) 
                    # to keep the underlying Win32 subsystem happy under .NET Framework 4.8 / PS 5.1.
                    $absoluteTemp      = [System.IO.Path]::GetFullPath($tempFile)
                    $absoluteTimestamp = [System.IO.Path]::GetFullPath($TimestampFile)
                    $absoluteBackup    = [System.IO.Path]::GetFullPath($backupFile)

                    if (Test-Path $absoluteTimestamp) {
                        [System.IO.File]::Replace($absoluteTemp, $absoluteTimestamp, $absoluteBackup)
                    } else {
                        # First run scenario: The target file doesn't exist yet, so a basic Move is safe and atomic.
                        [System.IO.File]::Move($absoluteTemp, $absoluteTimestamp)
                    }
                    Write-Verbose "Timestamp updated atomically to: $timestampString"
                }
            }

            break # Success, exit retry loop

        } catch [System.IO.IOException], [System.UnauthorizedAccessException] {
            if ($attempt -lt $maxRetries) {
                Start-Sleep -Milliseconds $retryDelayMs
                continue
            }
            
            # ESCALATED ERROR: Explicitly inform operator that watermark update failed and duplicate notices may fire
            $errorMessage = "CRITICAL: Watermark update lost due to persistent lock contention. Duplicate notifications will follow for this time slice. Error: $($_.Exception.Message)"
            Write-Warning "ServyWatermark: $errorMessage"
            
            if ($ScriptDir) {
                $logFile = Join-Path $ScriptDir "ServyWatermarkErrors.log"
                try {
                    Write-ServyLog -FilePath $logFile -Message $errorMessage
                } catch {
                    # Silence to prevent script termination if the disk is completely inaccessible
                }
            }
        } catch {
            # Bypass EventLog to prevent feedback loops. Record locally only.
            $errorMessage = "Failed to update timestamp file: $($_.Exception.Message)"
            Write-Warning "ServyWatermark: $errorMessage"
            
            if ($ScriptDir) {
                $logFile = Join-Path $ScriptDir "ServyWatermarkErrors.log"
                try {
                    Write-ServyLog -FilePath $logFile -Message $errorMessage
                } catch {
                    # Silence to prevent script termination if the disk is completely inaccessible
                }
            }
            break # Exit loop for non-IO exceptions
        } finally {
            # Safely release active tracking handles, files, and staging contexts
            if ($null -ne $writer) { try { $writer.Dispose() } catch {} }
            if ($null -ne $reader) { try { $reader.Dispose() } catch {} }
            if ($null -ne $fs)     { try { $fs.Dispose() }     catch {} }
            
            # Post-swap cleanup: Scrub both transient staging and backup assets from disk
            if (Test-Path $tempFile)   { try { Remove-Item $tempFile -Force }   catch {} }
            if (Test-Path $backupFile) { try { Remove-Item $backupFile -Force } catch {} }
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
    # FIX: Explicitly pass the event ID to decouple scope inheritance
    $rawErrors = Get-ServyLastErrors -LastProcessed $LastProcessed -EventLogErrorId $EVENT_ID_ERROR

    # CHECK: If no errors, exit quietly
    if ($null -eq $rawErrors -or $rawErrors.Count -eq 0) {
        return $null
    }

    # PRE-FILTER: Prevent feedback loops *before* selecting the most recent event.
    # This ensures a notification failure doesn't mask a genuine service crash during a first run.
    $errors = @($rawErrors | Where-Object {
        $_.Message -notmatch "^ServyFailureEmail:" -and 
        $_.Message -notmatch "^ServyToast:" -and 
        $_.Message -notmatch "^Servy Notification Error:"
    })

    # CHECK AGAIN: Exit if the array is empty after filtering out feedback loops
    if ($errors.Count -eq 0) {
        return $null
    }

    # Chronological sorting for email sequence
    if ($null -eq $LastProcessed) {
        # FIRST RUN LOGIC: Take the most recent VALID (non-feedback) event.
        # Ensure errors are sorted by time so we are sure '0' is the newest.
        return @(($errors | Sort-Object TimeCreated -Descending)[0])
    } else {
        # NORMAL RUN LOGIC: Chronological order
        # Explicitly cast to array to handle single-event scenarios
        return @($errors | Sort-Object TimeCreated)
    }
}

function ConvertFrom-ServyEventMessage {
    <#
    .SYNOPSIS
        Extracts the service name and detailed log text from a raw Servy event message.
    #>
    param([string]$Message)

    # Parse raw message context
    # Split the first line to safely extract the service prefix, preventing multi-line stack traces 
    # from forcing a fallback to an 'Unknown Service' state.
    $firstLine, $rest = $Message -split "\r?\n", 2

    if ($firstLine -match '^\[(.+?)\]\s*(.*)$') {
        return @{
            ServiceName = $matches[1]
            LogText     = if ($rest) { "$($matches[2])`n$rest" } else { $matches[2] }
        }
    } else {
        return @{
            ServiceName = "Unknown Service"
            LogText     = $Message
        }
    }
}

Export-ModuleMember -Function Write-FallbackError, Read-Watermark, Update-Watermark, Get-EventsToProcess, ConvertFrom-ServyEventMessage