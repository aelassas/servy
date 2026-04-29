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
function Check-LastExitCode {
    param([string]$ErrorMessage)
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ERROR: $ErrorMessage (Exit Code: $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}

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
        [Parameter(Mandatory=$true)][string]$Version
    )
    
    Write-Host "--- Building Installer ---" -ForegroundColor Cyan
    
    $maxRetry = 3
    $currentAttempt = 0
    $success = $false

    while (-not $success -and $currentAttempt -lt $maxRetry) {
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
            if ($currentAttempt -lt $maxRetry) {
                # Now this will actually execute and wait for the AV lock to release
                Write-Warning "Inno Setup failed (likely AV lock). Waiting 2s before retry..."
                Start-Sleep -Seconds 2
            } else {
                # This bubbles up to your global catch block at the bottom of publish.ps1
                throw "Inno Setup failed after $maxRetry attempts. $_"
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
    
    # 2. Include Task Scheduler hooks
    $taskSchdSource = Join-Path $ScriptDir "taskschd"
    if (Test-Path $taskSchdSource) {
        $taskSchdDest = Join-Path $DestFolder "taskschd"
        [void](New-Item -Path $taskSchdDest -ItemType Directory -Force)
        Copy-Item -Path (Join-Path $taskSchdSource "*") -Destination $taskSchdDest -Recurse -Force -Exclude "smtp-cred.xml", "*.dat", "*.log"
    }

    # 3. Include PowerShell Module artifacts with Test-Path guards
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
    
    # Compress contents of the folder, not the folder itself
    $zipArgs = @("a", "-t7z", "-m0=lzma2", "-mx=9", "-ms=on", $OutputZip, $PackageFolder)
    $process = Start-Process -FilePath $SevenZipExe -ArgumentList $zipArgs -Wait -NoNewWindow -PassThru

    if ($process.ExitCode -ne 0) {
        throw "7-Zip failed with exit code $($process.ExitCode)"
    }

    Write-Host "Success: $OutputZip" -ForegroundColor Green
}