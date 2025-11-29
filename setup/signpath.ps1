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
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ConfigCandidates = @(
    (Join-Path $ScriptDir ".signpath"),
    (Join-Path $ScriptDir ".signpath.env"),
    ".signpath",
    ".signpath.env"
)

$ConfigPath = $ConfigCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $ConfigPath) {
    Write-Host ".signpath not found. Skipping signing."
    exit 0
}

Write-Host "Loading config from $ConfigPath"

# ----------------------------------------------------------
# LOAD CONFIG
# ----------------------------------------------------------
$Config = @{}
Get-Content $ConfigPath| ForEach-Object {
    if ($_ -match "^\s*#") { return }
    if ($_ -match "^\s*$") { return }
    if ($_ -match "^\s*([^=]+)=(.*)$") {
        $Config[$matches[1].Trim().ToUpper()] = $matches[2].Trim()
    }
}

# ----------------------------------------------------------
# CHECK SIGN FLAG
# ----------------------------------------------------------
$SignFlag = $Config["SIGN"]

if ($SignFlag -ine "true") {
    Write-Host "SIGN is not true in $ConfigPath. Skipping signing."
    exit 0
}

Write-Host "SIGN=true detected. Proceeding with code signing."

# ----------------------------------------------------------
# EXTRACT REQUIRED FIELDS
# ----------------------------------------------------------
$ApiToken                  = $Config["API_TOKEN"]
$OrganizationId            = $Config["ORGANIZATION_ID"]
$ProjectSlug               = $Config["PROJECT_SLUG"]
$SigningPolicySlug         = $Config["SIGNING_POLICY_SLUG"]
$ArtifactConfigurationSlug = $Config["ARTIFACT_CONFIGURATION_SLUG"]  # optional

if (!$ApiToken -or !$OrganizationId -or !$ProjectSlug -or !$SigningPolicySlug) {
    Write-Error "Missing required SignPath configuration values."
    exit 1
}

if (-not (Test-Path $FilePath)) {
    Write-Error "File not found: $FilePath"
    exit 1
}

$FileName = Split-Path $FilePath -Leaf
Write-Host "Submitting signing job for $FileName..."

# ----------------------------------------------------------
# SUBMIT SIGNING REQUEST
# ----------------------------------------------------------
$SignedPath = "$FilePath.signed"

try {
    $CommonParams = @{
        OrganizationId     = $OrganizationId
        ApiToken           = $ApiToken
        ProjectSlug        = $ProjectSlug
        SigningPolicySlug  = $SigningPolicySlug
        InputArtifactPath  = $FilePath
        WaitForCompletion  = $true
        OutputArtifactPath = $SignedPath
    }

    $RepoUrl    = "https://github.com/aelassas/servy.git"
    $CommitId   = $env:GITHUB_SHA
    $BranchName = $env:GITHUB_REF_NAME

    # BuildData.Url must point to the RUN URL — NOT the job URL
    $BuildUrl = "https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"

    if ($CommitId -and $BranchName) {

        $CommonParams.Origin = @{
            RepositoryData = @{
                SourceControlManagementType = "git"
                Url         = $RepoUrl
                CommitId    = $CommitId
                BranchName  = $BranchName
            }
            BuildData = @{
                Url = $BuildUrl
            }
        }

        Write-Host "Setting origin info:"
        Write-Host "  Repo      = $RepoUrl"
        Write-Host "  Commit    = $CommitId"
        Write-Host "  Branch    = $BranchName"
        Write-Host "  Build URL = $BuildUrl"
    }
    else {
        Write-Warning "Could not retrieve Git origin information from GitHub environment variables."
    }

    if ($ArtifactConfigurationSlug) {
        $CommonParams.ArtifactConfigurationSlug = $ArtifactConfigurationSlug
    }

    $SigningRequestId = Submit-SigningRequest @CommonParams
    Write-Host "Signing request completed: $SigningRequestId"
}
catch {
    Write-Error "Failed to submit signing request: $_"
    exit 1
}

# ----------------------------------------------------------
# REPLACE ORIGINAL FILE WITH SIGNED VERSION
# ----------------------------------------------------------
try {
    if (-not (Test-Path $SignedPath)) {
        Write-Error "SignPath did not produce the expected output file: $SignedPath"
        exit 1
    }
    Move-Item -Force -Path $SignedPath -Destination $FilePath
    Write-Host "Signing complete: $FilePath"
}
catch {
    Write-Error "Failed to replace the original file: $_"
    exit 1
}

exit 0
