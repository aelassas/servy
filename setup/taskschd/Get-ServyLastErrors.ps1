#Requires -Version 3.0
<#
.SYNOPSIS
    Retrieves recent error events from the 'Servy' event source.

.DESCRIPTION
    Queries the Windows Application log for errors produced by Servy. It uses 
    high-performance hashtable filtering to minimize CPU impact on the host system.

.PARAMETER LastProcessed
    The timestamp of the last processed event. Only events strictly newer 
    than this timestamp will be returned.

.NOTES
    Author      : Akram El Assas
    Project     : Servy
    
    Requirements:
      - PowerShell 3.0 or later.
      - Windows Vista / Windows Server 2008 or newer.
      - Note: This script is NOT compatible with Windows XP or Server 2003 
        due to the dependency on the Get-WinEvent cmdlet.

.LINK
    https://github.com/aelassas/servy
#>

# Event ID Taxonomy (Refer to src/Servy.Core/Logging/EventIds.cs for updates)
# 3000-3099: Core Errors | 3100-3199: Script Errors
$EVENT_ID_ERROR = 3103

function Get-ServyLastErrors {
  param(
    $LastProcessed
  )

  # 1. Self-derive location for logging
  $scriptHome = $PSScriptRoot

  if ($null -ne $LastProcessed -and -not ($LastProcessed -is [datetime])) {
      try {
          $LastProcessed = [DateTime]::Parse($LastProcessed)
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

  # "Filter Left" - let the Event Log service handle the time filtering natively
  if ($LastProcessed) {
      $filter.StartTime = $LastProcessed
  }

  try {
      # Get-WinEvent requires Vista/2008+ (Event Log 6.0 API)
      $errors = @(Get-WinEvent -FilterHashtable $filter -ErrorAction Stop)
  }
  catch {
    # Language-agnostic check: relies on the internal Error ID rather than translated text
    if ($_.FullyQualifiedErrorId -match "NoMatchingEventsFound") {
      # This is a standard state, not an error
      return @() 
    }

    $errorMsg = "Failed to query Windows event log for Servy errors: $_"
    try {
      # Fallback A: Try the Event Log
      Write-EventLog -LogName Application -Source "Servy" -EventId $EVENT_ID_ERROR -EntryType Error -Message $errorMsg -ErrorAction Stop
    }
    catch {
      # Fallback B: Try the local file log
      $logPath = Join-Path $scriptHome "Get-ServyLastErrors.log"
      $timestampedMsg = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $errorMsg"
      $timestampedMsg | Out-File -FilePath $logPath -Append
    }

    # Throw instead of exit to preserve caller's process/cleanup
    throw "Servy Event Query Failure: $errorMsg"
  }

  # -------------------------------
  # Precision Filtering
  # -------------------------------
  # Filter out the event that exactly matches $LastProcessed (>= vs > issue)
  if ($LastProcessed) {
      $errors = @($errors | Where-Object { $_.TimeCreated -gt $LastProcessed })
  }

  return $errors
}
