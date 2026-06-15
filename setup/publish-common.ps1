#requires -Version 5.0
<#
    .SYNOPSIS
    Shared publish utilities for Servy projects.

    .DESCRIPTION
    Provides standard functions to build installer, portable zip, and copy artifacts,
    ensuring DRY compliance across all project scripts.
#>

$PC_ScriptDir = $PSScriptRoot

# Import helpers
. (Join-Path $PC_ScriptDir "common-helpers.ps1")

<#
    .SYNOPSIS
    Safely removes a file or directory if it exists on the system.

    .DESCRIPTION
    Checks for the presence of the specified path and forcefully removes it and all child items without prompting. This function is essential for ensuring a clean workspace before initiating builds.

    .PARAMETER Path
    The absolute or relative path to the file or directory to remove.
#>
function Remove-ItemSafely {
    param ([string]$Path)
    if (Test-Path $Path) {
        Write-Host "Cleaning: $Path" -ForegroundColor Gray
        Remove-Item -Recurse -Force $Path
    }
}

<#
    .SYNOPSIS
    Builds an Inno Setup installer executable using the provided configuration, incorporating retry logic for file locks.

    .DESCRIPTION
    Executes the Inno Setup compiler against the provided ISS file. This implementation features a retry loop to automatically recover from transient Anti-Virus file locks that frequently occur during compilation sequences.

    .PARAMETER InnoCompiler
    The resolved path to the ISCC compiler executable.

    .PARAMETER IssFile
    The path to the Inno Setup script defining the installation package.

    .PARAMETER Version
    The version string to stamp onto the installer package via the preprocessor directive.
#>
function Build-Installer {
    param (
        [Parameter(Mandatory=$true)][string]$InnoCompiler,
        [Parameter(Mandatory=$true)][string]$IssFile,
        [Parameter(Mandatory=$true)][string]$Version,
        [int]$MaxRetry = 3,
        [int]$RetryDelaySeconds = 2
    )
    
    Write-Host "--- Building Installer ---" -ForegroundColor Cyan
    
    $currentAttempt = 0
    $success = $false

    while (-not $success -and $currentAttempt -lt $MaxRetry) {
        try {
            $currentAttempt++
            if ($currentAttempt -gt 1) { 
                Write-Host "Inno Setup retry attempt $currentAttempt..." -ForegroundColor Yellow 
            }

            & $InnoCompiler $IssFile /DMyAppVersion=$Version

            # MUST check exit code manually to trigger the 'catch' block
            if ($LASTEXITCODE -eq 0) { 
                $success = $true 
                Write-Host "Installer built successfully." -ForegroundColor Green
            } else {
                throw "ISCC.exe failed with exit code $LASTEXITCODE"
            }
        }
        catch {
            # Treat any non-zero exit as potentially transient for the first few retries.
            # This avoids the complex and unreliable string-parsing logic.
            if ($currentAttempt -lt $MaxRetry) {
                # Transient failure: pause to let the AV file lock release before retrying.
                Write-Warning "Inno Setup failed (likely AV lock). Waiting $($RetryDelaySeconds)s before retry..."
                Start-Sleep -Seconds $RetryDelaySeconds
            } else {
                # This bubbles up to the global catch block at the bottom of publish.ps1
                throw "Inno Setup failed after $MaxRetry attempts. $_"
            }
        }
    }
}

<#
    .SYNOPSIS
    Copies shared configuration and PowerShell module artifacts into the destination package folder.

    .DESCRIPTION
    Transfers the Task Scheduler XML definitions and the Servy PowerShell module files into the target directory. This process ensures all requisite foundational files exist before allowing the packaging to proceed.

    .PARAMETER ScriptDir
    The directory containing the publish scripts and the task schedule subfolder.

    .PARAMETER CliDir
    The directory containing the Servy CLI project and its PowerShell module artifacts.

    .PARAMETER DestFolder
    The target directory where the artifacts should be copied.
#>
function Copy-CommonArtifacts {
    param (
        [Parameter(Mandatory=$true)][string]$ScriptDir,
        [Parameter(Mandatory=$true)][string]$CliDir,
        [Parameter(Mandatory=$true)][string]$DestFolder
    )
    
    # 1. Include Task Scheduler hooks
    $taskSchdSource = Join-Path $ScriptDir "taskschd"
    if (Test-Path $taskSchdSource) {
        $taskSchdDest = Join-Path $DestFolder "taskschd"
        [void](New-Item -Path $taskSchdDest -ItemType Directory -Force)

        # Use Get-ChildItem -Recurse -Exclude to ensure the exclusion propagates to all levels
        Get-ChildItem -Path $taskSchdSource -Recurse -Exclude 'smtp-cred.xml','*.dat','*.log' |
            Copy-Item -Destination {
                Join-Path $taskSchdDest $_.FullName.Substring($taskSchdSource.Length).TrimStart('\')
            } -Force

        # Post-copy verification to ensure no sensitive files leaked into the package
        $leaks = Get-ChildItem -Path $taskSchdDest -Recurse -Include 'smtp-cred.xml','*.dat','*.log'
        if ($leaks) { 
            throw "SECURITY ERROR: Excluded files leaked into package: $($leaks.FullName -join ', ')" 
        }
    }

    # 2. Include PowerShell Module artifacts with Test-Path guards
    $cliArtifacts = @("Servy.psm1", "Servy.psd1", "servy-module-examples.ps1")
    foreach ($art in $cliArtifacts) {
        $sourcePath = Join-Path $CliDir $art
        if (Test-Path $sourcePath) {
            Copy-Item -Path $sourcePath -Destination $DestFolder -Force
        } else {
            throw "Required CLI artifact missing: $sourcePath"
        }
    }
}

<#
    .SYNOPSIS
    Compresses a staging directory into a portable 7-Zip package.

    .DESCRIPTION
    Invokes the 7-Zip archiver with maximum compression settings to create a solid archive of the specified folder contents.

    .PARAMETER SevenZipExe
    The resolved path to the 7-Zip executable.

    .PARAMETER OutputZip
    The absolute path where the final archive should be created.

    .PARAMETER PackageFolder
    The staging directory whose contents will be compressed.
#>
function New-PortablePackage {
    param (
        [Parameter(Mandatory=$true)][string]$SevenZipExe,
        [Parameter(Mandatory=$true)][string]$OutputZip,
        [Parameter(Mandatory=$true)][string]$PackageFolder
    )
    
    # Compress the folder including its top-level directory entry, so extraction
    # preserves the staging directory name as a wrapper.
    
    # ROBUSTNESS: Use the call operator (&) instead of Start-Process -ArgumentList
    # to guarantee that parameters containing spaces (like OutputZip and PackageFolder) 
    # are correctly quoted by the PowerShell parser before native execution.
    & $SevenZipExe a -t7z -m0=lzma2 -mx=9 -mfb=273 -md=128m -ms=on $OutputZip $PackageFolder
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        throw "7-Zip failed with exit code $exitCode"
    }

    Write-Host "Success: $OutputZip" -ForegroundColor Green
}