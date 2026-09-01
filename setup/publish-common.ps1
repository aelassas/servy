#Requires -Version 5.0
<#
    .SYNOPSIS
    Shared publish utilities for Servy projects.

    .DESCRIPTION
    Provides standard shared utilities functions.
#>

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
    Copies Task Scheduler artifacts from a source path to a destination path, filtering out excluded/sensitive files.

    .DESCRIPTION
    Transfers Task Scheduler files recursively while applying standard exclusion rules for sensitive credentials,
    logs, temporary files, and test scripts. Reconstructs relative directory structures at the target.

    .PARAMETER SourcePath
    The source directory containing Task Scheduler definitions and scripts.

    .PARAMETER DestPath
    The target directory where filtered artifacts should be copied.
#>
function Copy-TaskSchdArtifacts {
    param(
        [Parameter(Mandatory=$true)][string]$SourcePath,
        [Parameter(Mandatory=$true)][string]$DestPath
    )

    Get-ChildItem -Path $SourcePath -Recurse -File |
        Where-Object {
            $_.Name -notin @('smtp-cred.xml', 'temp.ps1') -and
            $_.Extension -notin @('.dat', '.log') -and
            $_.Name -notlike '*.test.ps1'
        } |
        ForEach-Object {
            $rel    = $_.FullName.Substring((Resolve-Path $SourcePath).Path.Length + 1)
            $target = Join-Path $DestPath $rel
            $parent = Split-Path $target -Parent
            if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
            Copy-Item -Path $_.FullName -Destination $target -Force
        }
}

<#
.SYNOPSIS
    Validates that a provided version string conforms to the Servy version format.
#>
function Assert-ServyVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Version
    )

    $versionPattern = "^\d+\.\d+$"
    if ($Version -notmatch $versionPattern) {
        throw "Version must match pattern '$versionPattern'. Provided: '$Version'"
    }
}
