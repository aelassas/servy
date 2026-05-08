<#
    .SYNOPSIS
    Contains common functions used across all Servy publish and packaging scripts.

    .DESCRIPTION
    This module centralizes error handling, cleanup, installer generation via Inno Setup, common artifact copying, and 7-Zip package creation to ensure a robust architectural foundation.
    #>

    <#
    .SYNOPSIS
    Verifies the exit code of the last executed command and terminates the script if it indicates failure.

    .DESCRIPTION
    Checks the global exit code variable. If the value is not zero, an error is written to the host and the script is terminated with that exit code to prevent cascading failures.

    .PARAMETER ErrorMessage
    The contextual error message to display if the exit code is non-zero.
#>
function Assert-LastExitCode {
    param([string]$ErrorMessage)
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ERROR: $ErrorMessage (Exit Code: $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}