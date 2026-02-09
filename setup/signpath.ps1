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

.PARAMETER FilePath
    Path to the file that should be signed.

.EXAMPLE
    PS> .\signpath.ps1 "C:\build\Servy.Manager.exe"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath
)

# ----------------------------------------------------------
# ENSURE SIGNPATH MODULE EXISTS
# ----------------------------------------------------------
if (-not (Get-Module -ListAvailable -Name SignPath)) {

    Write-Host "SignPath module not found. Installing..."
    
    # Install in CurrentUser scope to avoid requiring admin
    Install-Module -Name SignPath -Force

    # Import the module to make it available in this session
    Import-Module SignPath -Force

    Write-Host "SignPath module installed and imported."
} else {
    # Module exists; import it anyway to ensure availability
    Import-Module SignPath -Force
    Write-Host "SignPath module already installed."
}

# ----------------------------------------------------------
# LOCATE CONFIG FILE
# ----------------------------------------------------------
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
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
Get-Content $configPath| ForEach-Object {
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
# EXTRACT REQUIRED FIELDS
# ----------------------------------------------------------
$apiToken                  = $config["API_TOKEN"]
$organizationId            = $config["ORGANIZATION_ID"]
$projectSlug               = $config["PROJECT_SLUG"]
$signingPolicySlug         = $config["SIGNING_POLICY_SLUG"]
$artifactConfigurationSlug = $config["ARTIFACT_CONFIGURATION_SLUG"]  # optional

if (!$apiToken -or !$organizationId -or !$projectSlug -or !$signingPolicySlug) {
    Write-Error "Missing required SignPath configuration values."
    exit 1
}

if (-not (Test-Path $FilePath)) {
    Write-Error "File not found: $FilePath"
    exit 1
}

$fileName = Split-Path $FilePath -Leaf
Write-Host "Submitting signing job for $fileName..."

# ----------------------------------------------------------
# SUBMIT SIGNING REQUEST
# ----------------------------------------------------------
$signedPath = "$FilePath.signed"

try {
    $commonParams = @{
        OrganizationId     = $organizationId
        ApiToken           = $apiToken
        ProjectSlug        = $projectSlug
        SigningPolicySlug  = $signingPolicySlug
        InputArtifactPath  = $FilePath
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

    $signingRequestId = Submit-SigningRequest @CommonParams
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
    Move-Item -Force -Path $signedPath -Destination $FilePath
    Write-Host "Signing complete: $FilePath"
}
catch {
    Write-Error "Failed to replace the original file: $_"
    exit 1
}

exit 0
