#Requires -Version 5.1

<#
.SYNOPSIS
    Centralized macro runner to validate and import script dependencies into the caller's active scope.

.DESCRIPTION
    Loops through required assets, checks filesystem paths, writes warning errors to the
    Windows Application Event Log on failure, and handles dot-sourcing or module imports dynamically.

.NOTES
    Required Caller-Scope Variables:
    - $RequiredDependencies : String array of dependency filenames to import (e.g. @('Servy-Watermark.psm1')).
    - $scriptDir           : Resolved absolute path to the directory containing script dependencies.
    - $EVENT_ID_DEPENDENCY_ERROR : Integer event ID for logging missing dependency warnings to Windows Application Event Log.
#>

# Caller contract validation: Fail fast if required variables are missing or invalid
if ($null -eq $RequiredDependencies -or $RequiredDependencies.Count -eq 0) {
    throw "Import-ServyDependencies: caller must set `$RequiredDependencies before dot-sourcing."
}
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    throw "Import-ServyDependencies: caller must set `$scriptDir before dot-sourcing."
}
if (-not $EVENT_ID_DEPENDENCY_ERROR) {
    throw "Import-ServyDependencies: caller must set `$EVENT_ID_DEPENDENCY_ERROR before dot-sourcing."
}

foreach ($dep in $RequiredDependencies) {
    $depPath = Join-Path $scriptDir $dep

    if (-not (Test-Path $depPath)) {
        $errorMsg = "Servy Notification Error: Required dependency not found at '$depPath'. Please ensure the file exists in the script directory."

        # 1. Attempt to log to Event Log for administrator visibility
        try {
            # Best-effort: the 'Servy' event source may not be registered, so guard with try/catch.
            # NOTE (-EntryType Warning): Written at Warning (Level 3) rather than Error (Level 2) on purpose.
            # The Task Scheduler notification trigger strictly filters for Level = 2 (Error). Logging a missing
            # script dependency at Warning prevents the EventTrigger from firing recursively on missing files (see #3160 / #4038).
            Write-EventLog -LogName Application -Source "Servy" -EventId $EVENT_ID_DEPENDENCY_ERROR `
                -EntryType Warning -Message $errorMsg -ErrorAction Stop
        } catch {
            # 2. Fallback to stderr if Event Log fails (or source isn't registered)
            Write-Error $errorMsg
        }

        # 3. Exit with error code
        exit 1
    }

    # File exists, proceed with dot-sourcing or importing into the caller's immediate scope pipeline context
    if ($dep -like "*.psm1") { Import-Module $depPath -Force } else { . $depPath }
}
