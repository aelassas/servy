<#
    .SYNOPSIS
    Common error-handling helper shared across all Servy publish and packaging scripts.

    .DESCRIPTION
    Provides Assert-LastExitCode, which terminates the script with the failing
    exit code when the last native command returned non-zero. Cleanup, Inno Setup
    installer generation, artifact copying and 7-Zip packaging live in publish-common.ps1.

    .PARAMETER ErrorMessage
    The contextual error message to display if the exit code is non-zero.
#>
function Assert-LastExitCode {
    param([string]$ErrorMessage)
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: $ErrorMessage (Exit Code: $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}