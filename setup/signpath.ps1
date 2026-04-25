#requires -Version 5.0
<#
.SYNOPSIS
    Signs a file using SignPath when SIGN=true is set in a .signpath configuration file.

.DESCRIPTION
    This script performs code signing only when:
        - A .signpath or .signpath.env file exists, and
        - The file contains SIGN=true

    It uses the official SignPath PowerShell module to:
        - Submit a signing request
        - Wait for completion
        - Download the signed artifact
        - Replace the original file with the signed version
        
    SECURITY: The script prioritizes the SIGNPATH_API_TOKEN environment variable. 
    If falling back to a configuration file, ensure the file has restricted 
    filesystem ACLs to prevent unauthorized read access.

.PARAMETER Path
    Path to the file that should be signed.

.EXAMPLE
    PS> .\signpath.ps1 "C:\build\Servy.Manager.exe"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

# ----------------------------------------------------------
# CONFIGURATION & VERSION PINNING
# ----------------------------------------------------------
$RequiredSignPathVersion = '4.4.1' # Pinned known-good version

# ----------------------------------------------------------
# ENSURE SIGNPATH MODULE EXISTS & LOAD CORRECT VERSION
# ----------------------------------------------------------
$availableModule = Get-Module -ListAvailable -Name SignPath | 
                   Where-Object { $_.Version -eq $RequiredSignPathVersion }

if (-not $availableModule) {
    Write-Host "SignPath module (v$RequiredSignPathVersion) not found. Installing..."
    Install-Module -Name SignPath -RequiredVersion $RequiredSignPathVersion -Force -Scope CurrentUser -AllowClobber -SkipPublisherCheck
}

# 1. Clean up the current session to prevent version "pollution"
# We remove ALL loaded SignPath modules so we start with a clean slate.
while (Get-Module -Name SignPath) {
    Remove-Module -Name SignPath -ErrorAction SilentlyContinue
}

# 2. Explicitly import the pinned version
Import-Module SignPath -RequiredVersion $RequiredSignPathVersion -Force

# 3. Verify exactly ONE module is loaded and it matches the version
# We filter Get-Module to ensure we are looking at the version we just asked for.
$loadedModule = Get-Module -Name SignPath | Where-Object { $_.Version -eq $RequiredSignPathVersion }

if ($null -eq $loadedModule) {
    # If we get here, the import failed or another version is blocking it
    $currentVersions = (Get-Module -Name SignPath).Version -join ', '
    Write-Error "Failed to load the correct SignPath module version. Expected: $RequiredSignPathVersion, Found Loaded: [$currentVersions]"
    exit 1
}

Write-Host "SignPath module v$($loadedModule.Version) loaded and verified for build provenance." -ForegroundColor Green

# ----------------------------------------------------------
# LOCATE CONFIG FILE
# ----------------------------------------------------------
$scriptDir = $PSScriptRoot
$configCandidates = @(
    (Join-Path $scriptDir ".signpath"),
    (Join-Path $scriptDir ".signpath.env"),
    ".signpath",
    ".signpath.env"
)

$configPath = $configCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $configPath) {
    Write-Host ".signpath not found. Skipping signing."
    exit 0
}

Write-Host "Loading config from $configPath"

# ----------------------------------------------------------
# LOAD CONFIG
# ----------------------------------------------------------
$config = @{}
Get-Content $configPath | ForEach-Object {
    if ($_ -match "^\s*#") { return }
    if ($_ -match "^\s*$") { return }
    if ($_ -match "^\s*([^=]+)=(.*)$") {
        $config[$matches[1].Trim().ToUpper()] = $matches[2].Trim()
    }
}

# ----------------------------------------------------------
# CHECK SIGN FLAG
# ----------------------------------------------------------
$signFlag = $config["SIGN"]
if ($signFlag -ine "true") {
    Write-Host "SIGN is not true in $configPath. Skipping signing."
    exit 0
}

Write-Host "SIGN=true detected. Proceeding with code signing."

# ----------------------------------------------------------
# EXTRACT REQUIRED FIELDS & SECURITY CHECK
# ----------------------------------------------------------
$organizationId            = $config["ORGANIZATION_ID"]
$projectSlug               = $config["PROJECT_SLUG"]
$signingPolicySlug         = $config["SIGNING_POLICY_SLUG"]
$artifactConfigurationSlug = $config["ARTIFACT_CONFIGURATION_SLUG"]  # optional

# API Token Resolution: Environment Variable > Config File
$apiToken = $env:SIGNPATH_API_TOKEN

if ([string]::IsNullOrWhiteSpace($apiToken)) {
    $apiToken = $config["API_TOKEN"]
    
    if (-not [string]::IsNullOrWhiteSpace($apiToken)) {
        Write-Warning "SECURITY: Using API_TOKEN from plaintext config file ($configPath)."
        Write-Warning "Ensure this file has strict ACLs applied (read/write only for the build user) and is excluded from source control."
        Write-Warning "Recommendation: Inject the token via the `$env:SIGNPATH_API_TOKEN environment variable instead."
    }
}

if (!$apiToken -or !$organizationId -or !$projectSlug -or !$signingPolicySlug) {
    Write-Error "Missing required SignPath configuration values (API Token, Organization ID, Project Slug, or Signing Policy Slug)."
    exit 1
}

if (-not (Test-Path $Path)) {
    Write-Error "File not found: $Path"
    exit 1
}

$fileName = Split-Path $Path -Leaf
Write-Host "Submitting signing job for $fileName..."

# ----------------------------------------------------------
# SUBMIT SIGNING REQUEST
# ----------------------------------------------------------
$signedPath = "$Path.signed"

try {
    $commonParams = @{
        OrganizationId     = $organizationId
        ApiToken           = $apiToken
        ProjectSlug        = $projectSlug
        SigningPolicySlug  = $signingPolicySlug
        InputArtifactPath  = $Path
        WaitForCompletion  = $true
        OutputArtifactPath = $signedPath
    }

    $repoUrl    = "https://github.com/aelassas/servy.git"
    $commitId   = $env:GITHUB_SHA
    $branchName = $env:GITHUB_REF_NAME

    # BuildData.Url must point to the RUN URL — NOT the job URL
    $buildUrl = "https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"

    if ($commitId -and $branchName) {
        $commonParams.Origin = @{
            RepositoryData = @{
                SourceControlManagementType = "git"
                Url         = $repoUrl
                CommitId    = $commitId
                BranchName  = $branchName
            }
            BuildData = @{
                Url = $buildUrl
            }
        }

        Write-Host "Setting origin info:"
        Write-Host "  Repo      = $repoUrl"
        Write-Host "  Commit    = $commitId"
        Write-Host "  Branch    = $branchName"
        Write-Host "  Build URL = $buildUrl"
    }
    else {
        Write-Warning "Could not retrieve Git origin information from GitHub environment variables."
    }

    if ($artifactConfigurationSlug) {
        $commonParams.ArtifactConfigurationSlug = $artifactConfigurationSlug
    }

    $signingRequestId = Submit-SigningRequest @commonParams
    Write-Host "Signing request completed: $signingRequestId"
}
catch {
    Write-Error "Failed to submit signing request: $_"
    exit 1
}

# ----------------------------------------------------------
# REPLACE ORIGINAL FILE WITH SIGNED VERSION
# ----------------------------------------------------------
try {
    if (-not (Test-Path $signedPath)) {
        Write-Error "SignPath did not produce the expected output file: $signedPath"
        exit 1
    }
    Move-Item -Force -Path $signedPath -Destination $Path
    Write-Host "Signing complete: $Path"
}
catch {
    Write-Error "Failed to replace the original file: $_"
    exit 1
}

exit 0