# taskschd/Get-ServyLastErrors.ps1

function Get-ServyLastErrors {
  param(
    $LastProcessed
  )

  # 1. Self-derive location for logging (PS 2.0+ compatible)
  # This replaces the leaked $ModuleRoot variable
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
      # Get-WinEvent returns events in descending order (newest first)
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
      # Using $scriptHome ensures the log always goes to the taskschd folder
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