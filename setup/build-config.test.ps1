#requires -Version 5.0
<#
.SYNOPSIS
    Simple sanity test for build-config.ps1.
#>

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$configPath = Join-Path $scriptDir "build-config.ps1"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running build-config.ps1 Tests                " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $configPath)) {
    Write-Host "FAIL: build-config.ps1 was not found at path: $configPath" -ForegroundColor Red
    exit 1
}

try {
    # Dot-source build-config.ps1 to load ConvertTo-NormalizedConfig helper function
    . $configPath | Out-Null

    # Verify normalization function explicitly trims padded hashtable values
    $paddedInput = @{
        Version            = "  10.0  "
        Tfm                = " net10.0-windows "
        BuildConfiguration = "Release "
        Runtime            = " win-x64 "
    }

    $normalizedResult = ConvertTo-NormalizedConfig -Config $paddedInput
    foreach ($key in $paddedInput.Keys) {
        $expected = $paddedInput[$key].Trim()
        $actual = $normalizedResult[$key]
        if ($actual -ne $expected) {
            Write-Host "FAIL: ConvertTo-NormalizedConfig failed to trim key '$key'. Expected '$expected', got '$actual'." -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "  [OK] ConvertTo-NormalizedConfig successfully normalizes and trims whitespace." -ForegroundColor Gray

    # Execute build-config.ps1 script execution
    $cfg = & $configPath

    if ($null -eq $cfg -or -not ($cfg -is [hashtable])) {
        Write-Host "FAIL: Output is null or not a hashtable." -ForegroundColor Red
        exit 1
    }

    # Verify required keys exist
    $requiredKeys = @('Version', 'Tfm', 'BuildConfiguration', 'Runtime')
    foreach ($key in $requiredKeys) {
        if (-not $cfg.ContainsKey($key)) {
            Write-Host "FAIL: Missing required key '$key' in build configuration." -ForegroundColor Red
            exit 1
        }

        $val = $cfg[$key]
        if ([string]::IsNullOrWhiteSpace($val)) {
            Write-Host "FAIL: Value for key '$key' is empty or null." -ForegroundColor Red
            exit 1
        }

        Write-Host "  [OK] $key = $val" -ForegroundColor Gray
    }

    # Verify values are in the form every consumer requires
    if ($cfg.Version -notmatch '^\d+\.\d+$') {
        Write-Host "FAIL: Version '$($cfg.Version)' is not Major.Minor; Assert-ServyVersion and publish.yml both reject it." -ForegroundColor Red
        exit 1
    }

    if ($cfg.Runtime -notin @('win-x64', 'win-arm64')) {
        Write-Host "FAIL: Runtime '$($cfg.Runtime)' is not one of win-x64 / win-arm64; publish-sc.ps1 would silently build x64." -ForegroundColor Red
        exit 1
    }

    if ($cfg.BuildConfiguration -notin @('Debug', 'Release')) {
        Write-Host "FAIL: BuildConfiguration '$($cfg.BuildConfiguration)' is not Debug / Release." -ForegroundColor Red
        exit 1
    }

    if ($cfg.Tfm -notmatch '^net\d+\.\d+-windows$') {
        Write-Host "FAIL: Tfm '$($cfg.Tfm)' does not look like a Windows TFM." -ForegroundColor Red
        exit 1
    }

    # Version has a contract beyond its shape: bump-version.ps1 keeps it in step with
    # Directory.Build.props and Servy.psd1, so a hand edit to one of the three must fail here.
    $repoRoot = Join-Path $scriptDir '..'
    $expectedFull = "$($cfg.Version).0"
    $expectedFile = "$expectedFull.0"
    $propsPath = Join-Path $repoRoot 'Directory.Build.props'

    $mirrors = @(
        @{
            Name    = 'Directory.Build.props <Version>'
            Path    = $propsPath
            Pattern = "<Version>$([regex]::Escape($expectedFull))</Version>"
        },
        @{
            Name    = 'Directory.Build.props <FileVersion>'
            Path    = $propsPath
            Pattern = "<FileVersion>$([regex]::Escape($expectedFile))</FileVersion>"
        },
        @{
            Name    = 'Directory.Build.props <AssemblyVersion>'
            Path    = $propsPath
            Pattern = "<AssemblyVersion>$([regex]::Escape($expectedFile))</AssemblyVersion>"
        },
        @{
            Name    = 'Servy.psd1'
            Path    = Join-Path (Join-Path (Join-Path $repoRoot 'src') 'Servy.CLI') 'Servy.psd1'
            Pattern = "ModuleVersion\s*=\s*'$([regex]::Escape($expectedFull))'"
        }
    )

    foreach ($mirror in $mirrors) {
        if (-not (Test-Path $mirror.Path)) {
            Write-Host "FAIL: $($mirror.Name) was not found at path: $($mirror.Path)" -ForegroundColor Red
            exit 1
        }

        if ((Get-Content $mirror.Path -Raw) -notmatch $mirror.Pattern) {
            Write-Host "FAIL: $($mirror.Name) does not carry version $expectedFull, but build-config.ps1 Version is '$($cfg.Version)'; bump-version.ps1 keeps the two in step." -ForegroundColor Red
            exit 1
        }

        Write-Host "  [OK] $($mirror.Name) agrees on $expectedFull" -ForegroundColor Gray
    }

    # Tfm has the same single-source contract as Version, kept the other way round: every
    # publishing script defaults -Tfm to the empty string and resolves it from this file, so the
    # moniker is written here and nowhere else. A literal put back into one of them re-creates the
    # drift that only bump-runtime.ps1's blanket rewrite used to repair.

    # Arrange
    $tfmLiteralPattern = '\$Tfm\s*=\s*["'']net'
    $repoRootFull = (Resolve-Path $repoRoot).Path
    $tfmScripts = Get-ChildItem -Path $repoRoot -Recurse -Filter *.ps1 |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.FullName -ne $PSCommandPath }

    # Act
    $tfmCopies = @()
    $tfmConsumers = @()
    foreach ($tfmScript in $tfmScripts) {
        $tfmLines = Get-Content $tfmScript.FullName
        if ($tfmLines -match '\$Tfm') { $tfmConsumers += $tfmScript.FullName }
        foreach ($tfmLine in $tfmLines) {
            if ($tfmLine -match $tfmLiteralPattern) {
                $tfmRelative = if ($tfmScript.FullName.StartsWith($repoRootFull)) {
                    $tfmScript.FullName.Substring($repoRootFull.Length).TrimStart([char]92, [char]47)
                } else { $tfmScript.FullName }
                $tfmCopies += "$($tfmRelative): $($tfmLine.Trim())"
            }
        }
    }

    # Assert
    if ($tfmConsumers.Count -eq 0) {
        Write-Host "FAIL: no script under $repoRoot takes a -Tfm parameter; the scan proves nothing." -ForegroundColor Red
        exit 1
    }

    if ($tfmCopies.Count -gt 0) {
        Write-Host "FAIL: $($tfmCopies.Count) script(s) carry their own TFM literal instead of reading it from build-config.ps1:" -ForegroundColor Red
        foreach ($tfmCopy in $tfmCopies) { Write-Host "        $tfmCopy" -ForegroundColor Red }
        exit 1
    }

    Write-Host "  [OK] $($tfmConsumers.Count) scripts take -Tfm and none carries its own copy of $($cfg.Tfm)" -ForegroundColor Gray

    # The set of shipped executables is enumerated independently in three places, in three
    # different orders: build.yml's $projects array, publish.yml's five build-and-sign step
    # groups (which stay as separate steps per #1037) and generate-sbom.ps1's $projects table.
    # Nothing ties them together, so adding a sixth executable to one and forgetting another is
    # silent - a binary that is built but never SBOM'd, or published but never ARM64-verified.
    # Membership is what has to agree; the three orders are deliberately left alone.

    # Arrange
    $workflowDir = Join-Path (Join-Path $repoRoot '.github') 'workflows'
    $projectListSources = @(
        @{ Name = '.github/workflows/build.yml';   Path = Join-Path $workflowDir 'build.yml' },
        @{ Name = '.github/workflows/publish.yml'; Path = Join-Path $workflowDir 'publish.yml' },
        @{ Name = 'setup/generate-sbom.ps1';       Path = Join-Path $scriptDir 'generate-sbom.ps1' }
    )
    $projectPathPattern = 'src[\\/][^"''\s]+\.csproj'

    # Act
    $projectLists = @()
    foreach ($listSource in $projectListSources) {
        if (-not (Test-Path $listSource.Path)) {
            Write-Host "FAIL: $($listSource.Name) was not found at path: $($listSource.Path)" -ForegroundColor Red
            exit 1
        }

        $listProjects = @([regex]::Matches((Get-Content $listSource.Path -Raw), $projectPathPattern) |
            ForEach-Object { $_.Value -replace '\\', '/' } |
            Sort-Object -Unique)

        $projectLists += @{ Name = $listSource.Name; Projects = $listProjects }
    }

    # Assert
    foreach ($projectList in $projectLists) {
        if ($projectList.Projects.Count -eq 0) {
            Write-Host "FAIL: no .csproj path was found in $($projectList.Name); the comparison proves nothing." -ForegroundColor Red
            exit 1
        }
    }

    $referenceList = $projectLists[0]
    foreach ($projectList in $projectLists) {
        if ($projectList.Name -eq $referenceList.Name) { continue }

        $missingProjects = @($referenceList.Projects | Where-Object { $_ -notin $projectList.Projects })
        $extraProjects   = @($projectList.Projects   | Where-Object { $_ -notin $referenceList.Projects })

        if ($missingProjects.Count -gt 0 -or $extraProjects.Count -gt 0) {
            Write-Host "FAIL: $($projectList.Name) does not ship the same project set as $($referenceList.Name)." -ForegroundColor Red
            foreach ($missingProject in $missingProjects) {
                Write-Host "        in $($referenceList.Name) but not in $($projectList.Name): $missingProject" -ForegroundColor Red
            }
            foreach ($extraProject in $extraProjects) {
                Write-Host "        in $($projectList.Name) but not in $($referenceList.Name): $extraProject" -ForegroundColor Red
            }
            exit 1
        }
    }

    Write-Host "  [OK] build.yml, publish.yml and generate-sbom.ps1 enumerate the same $($referenceList.Projects.Count) shipped projects" -ForegroundColor Gray

    Write-Host "`n====================================================" -ForegroundColor Cyan
    Write-Host "SUCCESS: build-config.ps1 validated successfully!" -ForegroundColor Green
    Write-Host "====================================================" -ForegroundColor Cyan
}
catch {
    Write-Host "`n====================================================" -ForegroundColor Cyan
    Write-Host "FAIL: Unexpected error during execution: $_" -ForegroundColor Red
    Write-Host "====================================================" -ForegroundColor Cyan
    exit 1
}
