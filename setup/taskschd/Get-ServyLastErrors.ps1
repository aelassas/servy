#Requires -Version 5.1

<#
.SYNOPSIS
    Retrieves recent error events from the 'Servy' event source.

.DESCRIPTION
    Queries the Windows Application log for errors produced by Servy. It uses
    high-performance hashtable filtering to minimize CPU impact on the host system.

.PARAMETER LastProcessed
    The timestamp of the last processed event. Only events strictly newer
    than this timestamp will be returned.

.PARAMETER EventLogErrorId
    The Event ID to use when writing a fallback failure log to the Application event log.
    Defaults to 3103.

.NOTES
    Author      : Akram El Assas
    Project     : Servy

    Requirements:
      - PowerShell 5.1 or later.
      - Windows 7 SP1 / Windows Server 2008 R2 or newer (required by PowerShell 5.1).
      - Note: This script is NOT compatible with Windows XP or Server 2003
        due to the dependency on the Get-WinEvent cmdlet.

.LINK
    https://github.com/aelassas/servy
#>

# Event ID Taxonomy (Refer to src/Servy.Core/Logging/EventIds.cs for updates)
# 3000-3099: Core Errors | 3100-3199: Script Errors

function ConvertFrom-WatermarkString {
    <#
    .SYNOPSIS
        Safely converts an ISO-8601 string to a UTC DateTime object.
    #>
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    try {
        $dt = [DateTime]::Parse(
          $Value.Trim(),
          [System.Globalization.CultureInfo]::InvariantCulture,
          [System.Globalization.DateTimeStyles]::RoundtripKind
          )
          return $dt.ToUniversalTime()
    } catch {
          return $null
    }
}

function Get-ServyLastErrors {
    param(
        $LastProcessed,
        [int]$EventLogErrorId = 3103
    )

    # 1. Self-derive location for logging
    $scriptHome = $PSScriptRoot

    if ($null -ne $LastProcessed -and -not ($LastProcessed -is [datetime])) {
        try {
            $LastProcessed = ConvertFrom-WatermarkString -Value $LastProcessed
        }
        catch {
            throw "Invalid datetime value for LastProcessed"
        }
    }

    $filter = @{
        LogName = 'Application'
        ProviderName = 'Servy'
        Level = 2  # Error
    }
    $errors = @()

    try {
        # "Filter Left" - let the Event Log service handle the time filtering natively
        if ($LastProcessed) {
            $filter.StartTime = ([datetime]$LastProcessed).ToLocalTime()
            # Get-WinEvent requires Vista/2008+ (Event Log 6.0 API)
            $errors = @(Get-WinEvent -FilterHashtable $filter -ErrorAction Stop)
        } else {
            # First run: fetch a bounded window of recent error events to inspect on initial setup (per #2315).
            $errors = @(Get-WinEvent -FilterHashtable $filter -MaxEvents 20 -ErrorAction Stop)
        }
    }
    catch {
        # Language-agnostic check: relies on the internal Error ID rather than translated text
        if ($_.FullyQualifiedErrorId -match "NoMatchingEventsFound") {
            # This is a standard state, not an error
            return @()
        }

        $errorMsg = "Servy Notification Error: Failed to query Windows event log for Servy errors: $_"
        try {
            # Fallback A: Try the Event Log
            # NOTE (-EntryType Warning): Written at Warning (Level 3) rather than Error (Level 2) on purpose.
            # The Task Scheduler notification trigger strictly filters for Level = 2 (Error). Writing this error at
            # Warning prevents a query failure from re-triggering the task in an infinite recursive loop (see #3160).
            Write-EventLog -LogName Application -Source "Servy" -EventId $EventLogErrorId -EntryType Warning -Message $errorMsg -ErrorAction Stop
        }
        catch {
            # Fallback B: Try the local file log
            $logPath = Join-Path $scriptHome "Get-ServyLastErrors.log"
            $loggerScript = Join-Path $scriptHome "Write-ServyLog.ps1"

            if (Test-Path $loggerScript) {
                . $loggerScript
                Write-ServyLog -FilePath $logPath -Message $errorMsg
            }
            else {
                Write-Warning "Get-ServyLastErrors: Missing required dependencies in '$scriptHome'"
            }
        }

        # Throw instead of exit to preserve caller's process/cleanup
        throw $errorMsg
    }

    # -------------------------------
    # Precision Filtering
    # -------------------------------
    # Filter out the event that exactly matches $LastProcessed (>= vs > issue)
    if ($LastProcessed) {
      $lastUtc = ([datetime]$LastProcessed).ToUniversalTime()
      $errors = @($errors | Where-Object { $_.TimeCreated.ToUniversalTime().Ticks -gt $lastUtc.Ticks })
    }

    return $errors
}
