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
      - PowerShell 2.0 or later.
      - Windows Vista / Windows Server 2008 or newer.
      - Note: This script is NOT compatible with Windows XP or Server 2003 
        due to the dependency on the Get-WinEvent cmdlet.

.LINK
    https://github.com/aelassas/servy
#>

function Get-ServyLastErrors {
  param(
    $LastProcessed
  )

  # 1. Self-derive location for logging (PS 2.0+ compatible)
  $scriptHome = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }

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
    if ($_.Exception.Message -like "*No events were found*") {
      # This is a standard state, not an error
      return @() 
    }

    $errorMsg = "Failed to query Windows event log for Servy errors: $_"
    try {
      # Fallback A: Try the Event Log
      Write-EventLog -LogName Application -Source "Servy" -EventId 9901 -EntryType Warning -Message $errorMsg -ErrorAction Stop
    }
    catch {
      # Fallback B: Try the local file log
      $logPath = Join-Path $scriptHome "ServyFailureEmail.log"
      $timestampedMsg = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $errorMsg"
      $timestampedMsg | Out-File -FilePath $logPath -Append
    }

    exit 1
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