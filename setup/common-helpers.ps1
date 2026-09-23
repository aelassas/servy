<#
    .SYNOPSIS
    Common error-handling helper shared across all Servy publish and packaging scripts.

    .DESCRIPTION
    Provides Assert-LastExitCode, which terminates the script with the failing
    exit code when the last native command returned non-zero, and Invoke-WithRetry,
    the single retry policy every script that drives an unreliable native command
    uses. Cleanup, Inno Setup installer generation, artifact copying and 7-Zip
    packaging live in publish-common.ps1.

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

<#
.SYNOPSIS
    Executes a scriptblock with automatic retries on failure.
#>
function Invoke-WithRetry {
    param(
        [Parameter(Mandatory=$true)]
        [scriptblock]$Command,

        [Parameter(Mandatory=$true)]
        [string]$ErrorMessage,

        [int]$MaxRetries = 3,
        [int]$RetryDelaySeconds = 5
    )

    $attempt = 0
    $success = $false

    while ($attempt -lt $MaxRetries) {
        $attempt++
        if ($attempt -gt 1) {
            Write-Host "Retrying command (Attempt $attempt of $MaxRetries)..." -ForegroundColor Yellow
        }

        # Reset exit code before execution
        $global:LASTEXITCODE = 0

        & $Command

        if ($global:LASTEXITCODE -eq 0) {
            $success = $true
            break
        } else {
            Write-Warning "Command exited with code $($global:LASTEXITCODE)."
            if ($attempt -lt $MaxRetries) {
                Write-Host "Waiting $RetryDelaySeconds seconds before next attempt..." -ForegroundColor DarkGray
                Start-Sleep -Seconds $RetryDelaySeconds
            }
        }
    }

    if (-not $success) {
        throw "$ErrorMessage (Failed after $MaxRetries attempts)"
    }
}
