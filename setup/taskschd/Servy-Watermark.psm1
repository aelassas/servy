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

    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        $fs = $null
        $reader = $null
        $writer = $null
        try {
            # 1. Acquire exclusive lock to make the read+compare+write atomic across all processes
            $fs = [System.IO.File]::Open($TimestampFile, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
            
            # Prevent PowerShell from writing a UTF-8 BOM on every file rewrite
            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            $shouldWrite = $true

            # Read current file text to catch concurrent updates from other script instances
            # leaveOpen=$true prevents the reader from closing the underlying $fs
            $reader = New-Object System.IO.StreamReader($fs, $utf8NoBom, $false, 1024, $true)
            $currentFileContent = $reader.ReadToEnd().Trim()
            $reader.Dispose()
            $reader = $null

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
                    # If file is locked, corrupt (e.g. previous NULL char bug), or unparseable, overwrite it
                    Write-Warning "Could not parse current timestamp file during update check. Overwriting to heal file."
                }
            }

            # 2. Write to file only if necessary, explicitly forcing BOM-less UTF8
            if ($shouldWrite) {
                $fs.Position = 0
                $fs.SetLength(0) # Truncate the file before writing the new content

                # leaveOpen=$true prevents the writer from closing the underlying $fs during GC
                $writer = New-Object System.IO.StreamWriter($fs, $utf8NoBom, 1024, $true)
                $writer.Write($timestampString)
                $writer.Dispose() # Implicitly flushes the buffer to the file stream boundary
                $writer = $null
                
                Write-Verbose "Timestamp updated to: $timestampString"
            }

            break # Success, exit retry loop

        } catch [System.IO.IOException], [System.UnauthorizedAccessException] {
            if ($attempt -lt $maxRetries) {
                Start-Sleep -Milliseconds $retryDelayMs
                continue
            }
            
            # If we exhausted retries, process as a normal error
            # Bypass EventLog to prevent feedback loops. Record locally only.
            $errorMessage = "Failed to update timestamp file (Lock timeout): $($_.Exception.Message)"
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
            # Safely release the active handles and buffers
            if ($null -ne $writer) { try { $writer.Dispose() } catch {} }
            if ($null -ne $reader) { try { $reader.Dispose() } catch {} }
            if ($null -ne $fs)     { $fs.Dispose() }
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