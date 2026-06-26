#requires -Version 5.1
<#
.SYNOPSIS
    Centralized macro runner to validate and import script dependencies into the caller's active scope.

.DESCRIPTION
    Loops through required assets, checks filesystem paths, writes warning errors to the 
    Windows Application Event Log on failure, and handles dot-sourcing or module imports dynamically.
#>

foreach ($dep in $RequiredDependencies) {
    $depPath = Join-Path $scriptDir $dep

    if (-not (Test-Path $depPath)) {
        $errorMsg = "Servy Notification Error: Required dependency not found at '$depPath'. Please ensure the file exists in the script directory."
        
        # 1. Attempt to log to Event Log for administrator visibility
        try {
            # Best-effort: the 'Servy' event source may not be registered, so guard with try/catch.
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