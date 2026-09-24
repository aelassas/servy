#requires -Version 5.0
<#
.SYNOPSIS
    Sanity tests for build-common.ps1's target-framework resolution site.

.DESCRIPTION
    build-config.ps1 is the single source of truth for the target framework moniker.
    Each per-project publish.ps1 wrapper resolves it once and hands the result to
    Invoke-StandardPublish, which must trust its caller rather than loading
    build-config.ps1 a second time. These tests pin the three things a second
    resolution site inside the function would be free to drift on: that -Tfm is
    mandatory there, that the function carries no build-config.ps1 lookup of its own,
    and that every caller still resolves the moniker before it calls.
#>

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running build-common.ps1 Tests                     " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

$buildCommonPath = Join-Path $scriptDir "build-common.ps1"
$buildConfigPath = Join-Path $scriptDir "build-config.ps1"

foreach ($required in @($buildCommonPath, $buildConfigPath)) {
    if (-not (Test-Path $required)) {
        Write-Host "FAIL: required script was not found at path: $required" -ForegroundColor Red
        exit 1
    }
}

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("servy-build-common-tests-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

function Remove-TestArtifacts {
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue }
}

try {
    . $buildCommonPath | Out-Null
    $cfg = & $buildConfigPath

    # --- -Tfm is mandatory on Invoke-StandardPublish --------------------------------
    # An optional -Tfm is what let the function quietly substitute its own resolution
    # when a caller passed nothing; mandatory is what makes the caller the only site.

    # Arrange
    $publishCommand = Get-Command Invoke-StandardPublish -ErrorAction SilentlyContinue

    # Act
    $tfmParameter = if ($null -ne $publishCommand) { $publishCommand.Parameters['Tfm'] } else { $null }
    $tfmMandatory = $false
    if ($null -ne $tfmParameter) {
        foreach ($attribute in $tfmParameter.Attributes) {
            if ($attribute -is [System.Management.Automation.ParameterAttribute] -and $attribute.Mandatory) {
                $tfmMandatory = $true
            }
        }
    }

    # Assert
    if ($null -eq $publishCommand) {
        Write-Host "FAIL: Invoke-StandardPublish is not available after dot-sourcing build-common.ps1." -ForegroundColor Red
        exit 1
    }
    if ($null -eq $tfmParameter) {
        Write-Host "FAIL: Invoke-StandardPublish does not take a -Tfm parameter." -ForegroundColor Red
        exit 1
    }
    if (-not $tfmMandatory) {
        Write-Host "FAIL: Invoke-StandardPublish -Tfm is optional again; the function can silently supply its own moniker." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Invoke-StandardPublish -Tfm is mandatory." -ForegroundColor Gray

    # --- Invoke-StandardPublish holds no build-config.ps1 lookup --------------------
    # This is the duplication itself: the moniker is resolved by the caller, so a
    # second load inside the function is a copy of that resolution, not a fallback.

    # Arrange
    $publishBody = $publishCommand.ScriptBlock.ToString()

    # Act
    $ownLookup = $publishBody -match 'build-config\.ps1'

    # Assert
    if ($ownLookup) {
        Write-Host "FAIL: Invoke-StandardPublish loads build-config.ps1 itself; the moniker is resolved twice per publish." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Invoke-StandardPublish resolves no moniker of its own." -ForegroundColor Gray

    # --- Every caller resolves the moniker before it calls --------------------------
    # The single site only reaches everyone while each wrapper actually reads
    # build-config.ps1 and forwards the result by name.

    # Arrange
    $repoRoot = Join-Path $scriptDir '..'
    $repoRootFull = (Resolve-Path $repoRoot).Path
    $callerScripts = Get-ChildItem -Path $repoRoot -Recurse -Filter *.ps1 |
        Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
            $_.FullName -ne $PSCommandPath -and
            $_.FullName -ne $buildCommonPath
        }

    # Act
    $callers = @()
    $unresolvedCallers = @()
    foreach ($callerScript in $callerScripts) {
        $callerText = Get-Content $callerScript.FullName -Raw
        if ($callerText -notmatch 'Invoke-StandardPublish') { continue }

        $callerRelative = if ($callerScript.FullName.StartsWith($repoRootFull)) {
            $callerScript.FullName.Substring($repoRootFull.Length).TrimStart([char]92, [char]47)
        } else { $callerScript.FullName }
        $callers += $callerRelative

        if ($callerText -notmatch 'build-config\.ps1' -or $callerText -notmatch '-Tfm\s+\$Tfm') {
            $unresolvedCallers += $callerRelative
        }
    }

    # Assert
    if ($callers.Count -eq 0) {
        Write-Host "FAIL: no script calls Invoke-StandardPublish; the scan proves nothing." -ForegroundColor Red
        exit 1
    }
    if ($unresolvedCallers.Count -gt 0) {
        Write-Host "FAIL: $($unresolvedCallers.Count) caller(s) of Invoke-StandardPublish do not resolve the moniker from build-config.ps1 and pass it as -Tfm:" -ForegroundColor Red
        foreach ($unresolvedCaller in $unresolvedCallers) { Write-Host "        $unresolvedCaller" -ForegroundColor Red }
        exit 1
    }
    Write-Host "  [OK] All $($callers.Count) callers resolve the moniker from build-config.ps1 and pass -Tfm." -ForegroundColor Gray

    # --- The caller's moniker is the one the publish steps receive ------------------
    # Observed at the resource step, which is the first thing Invoke-StandardPublish
    # forwards -Tfm to. A moniker deliberately unlike build-config.ps1's proves the
    # value travelled from the call site rather than from a central default.

    # Arrange
    $callerTfm = "net99.0-windows"
    if ($callerTfm -eq $cfg.Tfm) {
        Write-Host "FAIL: the probe moniker equals build-config.ps1's '$($cfg.Tfm)'; the test could not tell the two apart." -ForegroundColor Red
        exit 1
    }
    $projectDir = Join-Path $tempDir "Servy.Probe"
    New-Item -ItemType Directory -Path $projectDir -Force | Out-Null
    $forwardedLog = Join-Path $tempDir "forwarded-tfm.txt"
    $resScriptLines = @(
        'param([string]$Tfm, [string]$Runtime)',
        "Set-Content -LiteralPath '$forwardedLog' -Value `$Tfm",
        'exit 0'
    )
    Set-Content -LiteralPath (Join-Path $projectDir "publish-res-release.ps1") -Value $resScriptLines -Encoding UTF8
    $caught = $null

    # Act
    try {
        Invoke-StandardPublish -ProjectDir $projectDir -ProjectName "Servy.Probe" -Tfm $callerTfm | Out-Null
    }
    catch {
        $caught = $_
    }

    # Assert
    if ($null -eq $caught -or $caught.Exception.Message -notlike "*Project file not found*") {
        Write-Host "FAIL: the probe should have stopped at the missing project file, got '$caught'." -ForegroundColor Red
        exit 1
    }
    if (-not (Test-Path $forwardedLog)) {
        Write-Host "FAIL: the resource step never ran, so nothing recorded which moniker it was given." -ForegroundColor Red
        exit 1
    }
    $forwardedTfm = (Get-Content -LiteralPath $forwardedLog -Raw).Trim()
    if ($forwardedTfm -ne $callerTfm) {
        Write-Host "FAIL: the publish steps were given '$forwardedTfm' instead of the caller's '$callerTfm'." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] The caller's moniker reaches the publish steps unchanged." -ForegroundColor Gray

    Write-Host ""
    Write-Host "All build-common.ps1 tests passed." -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "FAIL: unexpected error: $_" -ForegroundColor Red
    exit 1
}
finally {
    Remove-TestArtifacts
}
