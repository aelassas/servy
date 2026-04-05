# taskschd/Get-ServyLastErrors.ps1
function Get-ServyLastErrors {
  param(
    $LastProcessed
  )

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
      Write-Host "No Servy error events found."
      exit 0
    }

    $errorMsg = "Failed to query Windows event log for Servy errors: $_"
    try {
      Write-EventLog -LogName Application -Source "Servy" -EventId 9901 -EntryType Warning -Message $errorMsg -ErrorAction Stop
    }
    catch {
      $errorMsg | Out-File -FilePath (Join-Path $ModuleRoot "ServyFailureEmail.log") -Append -ErrorAction SilentlyContinue
    }

    exit 1
  }

  # -------------------------------
  # Precision Filtering
  # -------------------------------
  # StartTime uses >= and truncates ticks, so the last event is returned again.
  # We must explicitly filter it out to ensure we only have strictly NEW events.
  if ($LastProcessed) {
      $errors = @($errors | Where-Object { $_.TimeCreated -gt $LastProcessed })
  }

  # If no strictly new events remain, exit cleanly
  if ($errors.Count -eq 0) {
      exit 0
  }

  return $errors
}
