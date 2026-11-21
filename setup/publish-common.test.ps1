#requires -Version 5.0
<#
.SYNOPSIS
    Sanity tests for publish-common.ps1's shared retry policy, Task Scheduler exclusion list and target framework moniker.

.DESCRIPTION
    Invoke-BuildInstaller in publish-common.ps1 drives ISCC.exe through Invoke-WithRetry,
    the single retry policy that lives in common-helpers.ps1 beside Assert-LastExitCode.
    These tests pin the three things a second, hand-rolled copy of that loop would be free
    to drift on: where the surviving policy lives, how many attempts it makes, and the
    failure message the caller finally sees. They also pin that the Task Scheduler
    exclusion list is declared once and enforced by both of its consumers (including under
    bracketed checkout paths), and that the target framework moniker is supplied by the
    caller rather than resolved a second time.
#>

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running publish-common.ps1 Tests                   " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

$publishCommonPath = Join-Path $scriptDir "publish-common.ps1"
$commonHelpersPath = Join-Path $scriptDir "common-helpers.ps1"
$buildConfigPath = Join-Path $scriptDir "build-config.ps1"

foreach ($required in @($publishCommonPath, $commonHelpersPath, $buildConfigPath)) {
    if (-not (Test-Path -LiteralPath $required)) {
        Write-Host "FAIL: required script was not found at path: $required" -ForegroundColor Red
        exit 1
    }
}

# Every attempt the fake compiler makes appends one line here, so the attempt count is a
# file length rather than a timing observation.
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("servy-publish-common-tests-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

function Remove-TestArtifacts {
    if (Test-Path -LiteralPath $tempDir) { Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue }
}

# Writes a stand-in for ISCC.exe that logs each invocation and fails the first
# $FailuresBeforeSuccess times. A negative count never succeeds.
function New-FakeInnoCompiler {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$AttemptLog,
        [Parameter(Mandatory = $true)][int]$FailuresBeforeSuccess
    )

    $lines = @(
        'param()',
        "Add-Content -LiteralPath '$AttemptLog' -Value (`$args -join ' ')",
        "`$attempts = @(Get-Content -LiteralPath '$AttemptLog').Count",
        "if ($FailuresBeforeSuccess -lt 0 -or `$attempts -le $FailuresBeforeSuccess) { exit 1 }",
        'exit 0'
    )
    Set-Content -LiteralPath $Path -Value $lines -Encoding UTF8
}

function Get-AttemptCount {
    param([Parameter(Mandatory = $true)][string]$AttemptLog)
    if (-not (Test-Path -LiteralPath $AttemptLog)) { return 0 }
    return @(Get-Content -LiteralPath $AttemptLog).Count
}

try {
    . $publishCommonPath | Out-Null
    $cfg = & $buildConfigPath

    # --- Invoke-WithRetry is centralized in common-helpers.ps1 -----------------------
    # This is the duplication itself: publish-common.ps1 must reach the shared policy
    # rather than carry a private copy of the loop.

    # Arrange
    $retryCommand = Get-Command Invoke-WithRetry -ErrorAction SilentlyContinue

    # Act
    $retryHome = if ($null -ne $retryCommand) { $retryCommand.ScriptBlock.File } else { $null }

    # Assert
    if ($null -eq $retryCommand) {
        Write-Host "FAIL: Invoke-WithRetry is not available after dot-sourcing publish-common.ps1." -ForegroundColor Red
        exit 1
    }
    if ($retryHome -ne $commonHelpersPath) {
        Write-Host "FAIL: Invoke-WithRetry should be defined in '$commonHelpersPath', but it came from '$retryHome'." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Invoke-WithRetry is defined once, in common-helpers.ps1." -ForegroundColor Gray

    if (Get-Content -LiteralPath $publishCommonPath -Raw | Select-String -SimpleMatch -Quiet 'MaxRetry') {
        Write-Host "FAIL: publish-common.ps1 still carries a private MaxRetry knob; the retry loop is duplicated again." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] publish-common.ps1 carries no private retry knob." -ForegroundColor Gray

    # --- A transient ISCC failure is retried and then succeeds -----------------------

    # Arrange
    $transientLog = Join-Path $tempDir "transient-attempts.txt"
    $transientCompiler = Join-Path $tempDir "iscc-transient.ps1"
    New-FakeInnoCompiler -Path $transientCompiler -AttemptLog $transientLog -FailuresBeforeSuccess 1

    # Act
    Invoke-BuildInstaller -InnoCompiler $transientCompiler -IssFile "servy.iss" -Version "1.2.3" -Tfm $cfg.Tfm | Out-Null

    # Assert
    $transientAttempts = Get-AttemptCount -AttemptLog $transientLog
    if ($transientAttempts -ne 2) {
        Write-Host "FAIL: a single transient failure should cost exactly 2 attempts, got $transientAttempts." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] A transient ISCC failure is retried once and then succeeds." -ForegroundColor Gray

    # --- A permanent ISCC failure gives up after the shared attempt count ------------
    # Invoke-WithRetry defaults to 3 attempts; the hand-rolled loop this replaced used
    # its own MaxRetry knob, and that is exactly the drift this asserts against.

    # Arrange
    $permanentLog = Join-Path $tempDir "permanent-attempts.txt"
    $permanentCompiler = Join-Path $tempDir "iscc-permanent.ps1"
    New-FakeInnoCompiler -Path $permanentCompiler -AttemptLog $permanentLog -FailuresBeforeSuccess -1
    $caught = $null

    # Act
    try {
        Invoke-BuildInstaller -InnoCompiler $permanentCompiler -IssFile "servy.iss" -Version "1.2.3" -Tfm $cfg.Tfm | Out-Null
    }
    catch {
        $caught = $_
    }

    # Assert
    if ($null -eq $caught) {
        Write-Host "FAIL: a permanently failing ISCC.exe should have thrown." -ForegroundColor Red
        exit 1
    }
    $permanentAttempts = Get-AttemptCount -AttemptLog $permanentLog
    if ($permanentAttempts -ne 3) {
        Write-Host "FAIL: the shared retry policy should make exactly 3 attempts, got $permanentAttempts." -ForegroundColor Red
        exit 1
    }
    $message = $caught.Exception.Message
    if ($message -notlike "*Inno Setup (ISCC.exe) failed*" -or $message -notlike "*Failed after 3 attempts*") {
        Write-Host "FAIL: the failure should carry the shared policy's message, got '$message'." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] A permanent ISCC failure gives up after 3 attempts with the shared message." -ForegroundColor Gray

    # --- The Task Scheduler exclusion list is declared once --------------------------
    # The copy filter and the post-copy leak check are two deliberately different
    # mechanisms, but they must enforce the same list, so the list itself is one array.

    # Arrange
    $publishCommonText = Get-Content -LiteralPath $publishCommonPath -Raw

    # Act
    $literalListCount = ([regex]::Matches($publishCommonText, [regex]::Escape("'smtp-cred.xml'"))).Count

    # Assert
    if ($null -eq $script:TaskSchdExcludedPatterns -or @($script:TaskSchdExcludedPatterns).Count -eq 0) {
        Write-Host "FAIL: `$script:TaskSchdExcludedPatterns is not declared by publish-common.ps1." -ForegroundColor Red
        exit 1
    }
    if ($literalListCount -ne 1) {
        Write-Host "FAIL: the exclusion list should be spelled out exactly once, found $literalListCount copies." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] The Task Scheduler exclusion list is declared once, as a script-scoped array." -ForegroundColor Gray

    # --- The copy filter enforces every pattern in that one array (including bracketed paths) ---
    # The sample filenames are derived from the patterns, so a sixth pattern added to the
    # array is exercised by this test without editing it. The bracketed folder name
    # (`exclusions[bracket]`) is there because `[` and `]` are wildcard characters to `-Path`:
    # the copy filter has to read the tree through `-LiteralPath`.

    # Arrange
    $bracketRoot = Join-Path $tempDir "exclusions[bracket]"
    $filterSource = Join-Path $bracketRoot "taskschd"
    $filterDest = Join-Path $bracketRoot "dst"
    [void][System.IO.Directory]::CreateDirectory((Join-Path $filterSource "SubFolder"))
    [void][System.IO.Directory]::CreateDirectory($filterDest)

    $payloadName = "ServySecurity.ps1"
    [System.IO.File]::WriteAllText((Join-Path $filterSource $payloadName), "payload")

    $excludedSamples = @()
    foreach ($pattern in $script:TaskSchdExcludedPatterns) {
        $sample = $pattern -replace '\*', 'sample'
        $excludedSamples += $sample
        [System.IO.File]::WriteAllText((Join-Path $filterSource $sample), "sensitive")
        [System.IO.File]::WriteAllText((Join-Path (Join-Path $filterSource "SubFolder") $sample), "sensitive")
    }

    # Act
    Copy-TaskSchdArtifacts -SourcePath $filterSource -DestPath $filterDest
    $copied = @(Get-ChildItem -LiteralPath $filterDest -Recurse -File | Select-Object -ExpandProperty Name)

    # Assert
    if ($copied -notcontains $payloadName) {
        Write-Host "FAIL: the copy filter dropped the payload file '$payloadName'." -ForegroundColor Red
        exit 1
    }
    foreach ($sample in $excludedSamples) {
        if ($copied -contains $sample) {
            Write-Host "FAIL: the copy filter let '$sample' through; it matches an excluded pattern." -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "  [OK] The copy filter excludes every pattern in the shared array, nested included." -ForegroundColor Gray

    # --- Copy-CommonArtifacts catches a leaked file even under a bracketed path -------
    # Simulate a bug in Copy-TaskSchdArtifacts that copies files unfiltered into a
    # bracketed destination, verifying the post-copy leak check still throws SECURITY ERROR.
    # The leak check is the second, independent post-copy scan: it reads the destination tree
    # through -LiteralPath (safe for bracketed checkout paths) with the same shared array.

    # Arrange
    $mockCliDir = Join-Path $bracketRoot "cli"
    $pkgCleanDest = Join-Path $bracketRoot "pkg-clean"
    $pkgLeakDest = Join-Path $bracketRoot "pkg-leak"
    [void][System.IO.Directory]::CreateDirectory($mockCliDir)
    [void][System.IO.Directory]::CreateDirectory($pkgCleanDest)
    [void][System.IO.Directory]::CreateDirectory($pkgLeakDest)

    foreach ($art in @("Servy.psm1", "Servy.psd1", "servy-module-examples.ps1", "Servy-Dump.ps1", "Servy-Restore.ps1")) {
        [System.IO.File]::WriteAllText((Join-Path $mockCliDir $art), "stub")
    }

    # Act 1: Normal Copy-CommonArtifacts run in a bracketed path succeeds cleanly
    Copy-CommonArtifacts -ScriptDir $bracketRoot -CliDir $mockCliDir -DestFolder $pkgCleanDest

    # Act 2: Bypass filtering in Copy-TaskSchdArtifacts to verify the leak check catches leaks in bracketed paths
    $origCopyFn = (Get-Item Function:\Copy-TaskSchdArtifacts).ScriptBlock
    $leakCaught = $null
    try {
        Set-Item Function:\Copy-TaskSchdArtifacts -Value {
            param([string]$SourcePath, [string]$DestPath)
            Get-ChildItem -LiteralPath $SourcePath -Recurse -File | ForEach-Object {
                $target = Join-Path $DestPath $_.Name
                Copy-Item -LiteralPath $_.FullName -Destination $target -Force
            }
        }

        try {
            Copy-CommonArtifacts -ScriptDir $bracketRoot -CliDir $mockCliDir -DestFolder $pkgLeakDest
        }
        catch {
            $leakCaught = $_
        }
    }
    finally {
        Set-Item Function:\Copy-TaskSchdArtifacts -Value $origCopyFn
    }

    # Assert
    if ($null -eq $leakCaught -or $leakCaught.Exception.Message -notlike "*SECURITY ERROR: Excluded files leaked into package*") {
        Write-Host "FAIL: Copy-CommonArtifacts failed to detect leaked files under a bracketed path." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Copy-CommonArtifacts post-copy leak check catches leaked files even under bracketed paths." -ForegroundColor Gray

    # --- The leak check sees every pattern, at depth, inside Copy-CommonArtifacts -----
    # One excluded sample at a time, planted one folder down, with a stand-in copy that
    # keeps the folder structure: only a leak check that recurses and knows that pattern
    # can object (#7883).

    # Arrange
    $structureKeepingCopy = {
        param([string]$SourcePath, [string]$DestPath)
        Get-ChildItem -LiteralPath $SourcePath | Copy-Item -Destination $DestPath -Recurse -Force
    }
    $realCopyFn = (Get-Item Function:\Copy-TaskSchdArtifacts).ScriptBlock
    $missedSamples = @()

    # Act
    try {
        Set-Item Function:\Copy-TaskSchdArtifacts -Value $structureKeepingCopy
        $sampleIndex = 0
        foreach ($sample in $excludedSamples) {
            $sampleIndex++
            $sampleRoot = Join-Path $bracketRoot "nested-$sampleIndex"
            $sampleNested = Join-Path (Join-Path $sampleRoot "taskschd") "SubFolder"
            $sampleDest = Join-Path $sampleRoot "pkg"
            [void][System.IO.Directory]::CreateDirectory($sampleNested)
            [void][System.IO.Directory]::CreateDirectory($sampleDest)
            [System.IO.File]::WriteAllText((Join-Path (Join-Path $sampleRoot "taskschd") $payloadName), "payload")
            [System.IO.File]::WriteAllText((Join-Path $sampleNested $sample), "sensitive")

            $sampleError = $null
            try { Copy-CommonArtifacts -ScriptDir $sampleRoot -CliDir $mockCliDir -DestFolder $sampleDest }
            catch { $sampleError = $_ }

            if ($null -eq $sampleError -or -not $sampleError.Exception.Message.Contains("SECURITY ERROR") -or -not $sampleError.Exception.Message.Contains($sample)) {
                $missedSamples += $sample
            }
        }
    }
    finally {
        Set-Item Function:\Copy-TaskSchdArtifacts -Value $realCopyFn
    }

    # Assert
    if ($missedSamples.Count -ne 0) {
        Write-Host "FAIL: Copy-CommonArtifacts let a nested leak through for: $($missedSamples -join ', ')." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Copy-CommonArtifacts refuses each excluded pattern when it leaks one folder down." -ForegroundColor Gray

    # --- The moniker is resolved by the caller, not a second time in here -----------
    # publish-sc.ps1, the one production caller, reads build-config.ps1 and passes the
    # result in. A second load inside Invoke-BuildInstaller is a copy of that
    # resolution whose fallback no real call site can reach.

    # Arrange
    $installerCommand = Get-Command Invoke-BuildInstaller -ErrorAction SilentlyContinue
    $publishScPath = Join-Path $scriptDir "publish-sc.ps1"
    if ($null -eq $installerCommand) {
        Write-Host "FAIL: Invoke-BuildInstaller is not available after dot-sourcing publish-common.ps1." -ForegroundColor Red
        exit 1
    }
    if (-not (Test-Path -LiteralPath $publishScPath)) {
        Write-Host "FAIL: the production caller was not found at path: $publishScPath" -ForegroundColor Red
        exit 1
    }

    # Act
    $installerTfmParameter = $installerCommand.Parameters['Tfm']
    $installerTfmMandatory = $false
    if ($null -ne $installerTfmParameter) {
        foreach ($attribute in $installerTfmParameter.Attributes) {
            if ($attribute -is [System.Management.Automation.ParameterAttribute] -and $attribute.Mandatory) {
                $installerTfmMandatory = $true
            }
        }
    }
    $installerOwnLookup = $installerCommand.ScriptBlock.ToString() -match 'build-config\.ps1'
    $publishScText = Get-Content -LiteralPath $publishScPath -Raw
    $publishScResolves = ($publishScText -match 'build-config\.ps1') -and ($publishScText -match '-Tfm\s+\$Tfm')

    # Assert
    if ($null -eq $installerTfmParameter) {
        Write-Host "FAIL: Invoke-BuildInstaller does not take a -Tfm parameter." -ForegroundColor Red
        exit 1
    }
    if (-not $installerTfmMandatory) {
        Write-Host "FAIL: Invoke-BuildInstaller -Tfm is optional again; the function can silently supply its own moniker." -ForegroundColor Red
        exit 1
    }
    if ($installerOwnLookup) {
        Write-Host "FAIL: Invoke-BuildInstaller loads build-config.ps1 itself; the moniker is resolved twice per installer build." -ForegroundColor Red
        exit 1
    }
    if (-not $publishScResolves) {
        Write-Host "FAIL: publish-sc.ps1 no longer resolves the moniker from build-config.ps1 and passes it as -Tfm." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] The moniker is resolved once, by publish-sc.ps1, and required by Invoke-BuildInstaller." -ForegroundColor Gray

    # --- The caller's moniker is the one ISCC.exe is given --------------------------
    # A moniker deliberately unlike build-config.ps1's proves the value reached the
    # preprocessor directive from the call site rather than from a central default.

    # Arrange
    $probeTfm = "net99.0-windows"
    if ($probeTfm -eq $cfg.Tfm) {
        Write-Host "FAIL: the probe moniker equals build-config.ps1's '$($cfg.Tfm)'; the test could not tell the two apart." -ForegroundColor Red
        exit 1
    }
    $probeLog = Join-Path $tempDir "probe-attempts.txt"
    $probeCompiler = Join-Path $tempDir "iscc-probe.ps1"
    New-FakeInnoCompiler -Path $probeCompiler -AttemptLog $probeLog -FailuresBeforeSuccess 0

    # Act
    Invoke-BuildInstaller -InnoCompiler $probeCompiler -IssFile "servy.iss" -Version "1.2.3" -Tfm $probeTfm | Out-Null

    # Assert
    $probeArgs = (Get-Content -LiteralPath $probeLog -Raw).Trim()
    if ($probeArgs -notmatch [regex]::Escape("/DTfm=$probeTfm")) {
        Write-Host "FAIL: ISCC.exe was given '$probeArgs' instead of the caller's /DTfm=$probeTfm." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] The caller's moniker reaches ISCC.exe unchanged." -ForegroundColor Gray

    Write-Host ""
    Write-Host "SUCCESS: publish-common.ps1 retry policy, moniker and exclusion list tests passed." -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "FAIL: Unexpected error during execution: $_" -ForegroundColor Red
    exit 1
}
finally {
    Remove-TestArtifacts
}
