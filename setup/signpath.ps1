<#
.SYNOPSIS
    Sign a file using SignPath if SIGN=true is set in the .signpath config file.
.DESCRIPTION
    The script will perform signing ONLY when:
        - A .signpath or .signpath.env file exists
        - The file contains SIGN=true
    Reads configuration from .signpath or .signpath.env in the script folder.
    Uses the official SignPath PowerShell module to submit a signing request,
    wait for completion, and download the signed artifact.
    Requires the SignPath PowerShell module to be installed: Install-Module -Name SignPath
.PARAMETER FilePath
    Path to the file you want signed.
.EXAMPLE
    PS> .\signpath.ps1 "C:\build\Servy.Manager.exe"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath
)

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
Get-Content $ConfigPath | ForEach-Object {
    if ($_ -match "^\s*#") { return }
    if ($_ -match "^\s*$") { return }
    if ($_ -match "^\s*([^=]+)=(.*)$") {
        $Config[$matches[1].Trim()] = $matches[2].Trim()
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
$ApiToken        = $Config["API_TOKEN"]
$OrganizationId  = $Config["ORGANIZATION_ID"]
$ProjectSlug     = $Config["PROJECT_SLUG"]
$SigningPolicy   = $Config["SIGNING_POLICY_SLUG"]
$ArtifactConfig  = $Config["ARTIFACT_CONFIGURATION_SLUG"]  # Optional

if (!$ApiToken -or !$OrganizationId -or !$ProjectSlug -or !$SigningPolicy) {
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
# SUBMIT SIGNING REQUEST (with module)
# ----------------------------------------------------------
try {
    $SigningRequestId = Submit-SigningRequest `
        -OrganizationId $OrganizationId `
        -ApiToken $ApiToken `
        -ProjectSlug $ProjectSlug `
        -SigningPolicySlug $SigningPolicy `
        -ArtifactConfigurationSlug $ArtifactConfig `
        -InputArtifactPath $FilePath `
        -WaitForCompletion `
        -OutputArtifactPath "$FilePath.signed"

    Write-Host "Signing request completed: $SigningRequestId"
}
catch {
    Write-Error "Failed to submit signing request: $_"
    exit 1
}

# ----------------------------------------------------------
# REPLACE ORIGINAL FILE
# ----------------------------------------------------------
Move-Item -Force -Path "$FilePath.signed" -Destination $FilePath
Write-Host "Signing complete: $FilePath"
exit 0
