<#
.SYNOPSIS
    Submits a file to SignPath for code signing only if SIGN=true is set
    in the .signpath config file. Handles very large files in PowerShell 5.

.DESCRIPTION
    The script will perform signing ONLY when:
        - A .signpath or .signpath.env file exists
        - The file contains SIGN=true

    For large files (>100MB), it streams the file during upload/download
    to avoid memory issues in PowerShell 5.

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
$Config = @{}
Get-Content $configPath | ForEach-Object {
    if ($_ -match "^\s*#") { return }
    if ($_ -match "^\s*$") { return }
    if ($_ -match "^\s*([^=]+)=(.*)$") {
        $Config[$matches[1].Trim()] = $matches[2].Trim()
    }
}

# ----------------------------------------------------------
# CHECK SIGN FLAG (case-insensitive)
# ----------------------------------------------------------
$SignFlag = $Config["SIGN"]
if ($SignFlag -ine "true") {
    Write-Host "SIGN is not true in $configPath. Skipping signing."
    exit 0
}

Write-Host "SIGN=true detected. Proceeding with code signing."

# ----------------------------------------------------------
# EXTRACT REQUIRED FIELDS
# ----------------------------------------------------------
$ApiToken        = $Config["API_TOKEN"]
$OrganizationId  = $Config["ORGANIZATION_ID"]
$ProjectSlug     = $Config["PROJECT_SLUG"]
$WorkflowSlug    = $Config["WORKFLOW_SLUG"]

if (!$ApiToken -or !$OrganizationId -or !$ProjectSlug -or !$WorkflowSlug) {
    Write-Error "Missing required SignPath configuration values."
    exit 1
}

$SignPathBaseUrl = "https://app.signpath.io"

if (-not (Test-Path $FilePath)) {
    Write-Error "File not found: $FilePath"
    exit 1
}

$FileName = Split-Path $FilePath -Leaf
Write-Host "Submitting signing job for $FileName..."

# ----------------------------------------------------------
# CREATE SIGNING JOB
# ----------------------------------------------------------
$createJobUrl = "$SignPathBaseUrl/api/v1/$OrganizationId/projects/$ProjectSlug/signing-jobs"
$createBody = @{ workflow = $WorkflowSlug } | ConvertTo-Json

$createRequest = [System.Net.WebRequest]::Create($createJobUrl)
$createRequest.Method = "POST"
$createRequest.ContentType = "application/json"
$createRequest.Headers["Authorization"] = "Bearer $ApiToken"

$bytes = [System.Text.Encoding]::UTF8.GetBytes($createBody)
$createRequest.ContentLength = $bytes.Length
$stream = $createRequest.GetRequestStream()
$stream.Write($bytes, 0, $bytes.Length)
$stream.Close()

$response = $createRequest.GetResponse()
$respStream = $response.GetResponseStream()
$reader = New-Object System.IO.StreamReader($respStream)
$jobResult = $reader.ReadToEnd() | ConvertFrom-Json
$reader.Close()
$response.Close()

$JobId = $jobResult.signingJobId

# ----------------------------------------------------------
# UPLOAD ARTIFACT (streamed)
# ----------------------------------------------------------
Write-Host "Uploading artifact..."
$uploadUrl = "$SignPathBaseUrl/api/v1/$OrganizationId/signing-jobs/$JobId/artifacts/original-file"
$uploadRequest = [System.Net.WebRequest]::Create($uploadUrl)
$uploadRequest.Method = "PUT"
$uploadRequest.Headers["Authorization"] = "Bearer $ApiToken"
$uploadRequest.ContentType = "application/octet-stream"

$fileStream = [System.IO.File]::OpenRead($FilePath)
$uploadRequest.ContentLength = $fileStream.Length
$requestStream = $uploadRequest.GetRequestStream()

$bufferSize = 4MB
$buffer = New-Object byte[] $bufferSize
while (($read = $fileStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
    $requestStream.Write($buffer, 0, $read)
}

$requestStream.Close()
$fileStream.Close()

$uploadResponse = $uploadRequest.GetResponse()
$uploadResponse.Close()

# ----------------------------------------------------------
# START WORKFLOW
# ----------------------------------------------------------
Write-Host "Starting signing job..."
$startUrl = "$SignPathBaseUrl/api/v1/$OrganizationId/signing-jobs/$JobId/start"
$startReq = [System.Net.WebRequest]::Create($startUrl)
$startReq.Method = "POST"
$startReq.Headers["Authorization"] = "Bearer $ApiToken"
$startReq.ContentLength = 0
$startResp = $startReq.GetResponse()
$startResp.Close()

# ----------------------------------------------------------
# POLL FOR STATUS
# ----------------------------------------------------------
Write-Host "Waiting for signing to complete..."
while ($true) {
    Start-Sleep -Seconds 5
    $statusUrl = "$SignPathBaseUrl/api/v1/$OrganizationId/signing-jobs/$JobId"
    $statusReq = [System.Net.WebRequest]::Create($statusUrl)
    $statusReq.Method = "GET"
    $statusReq.Headers["Authorization"] = "Bearer $ApiToken"
    $statusResp = $statusReq.GetResponse()
    $statusReader = New-Object System.IO.StreamReader($statusResp.GetResponseStream())
    $statusJson = $statusReader.ReadToEnd() | ConvertFrom-Json
    $statusReader.Close()
    $statusResp.Close()

    Write-Host "Status: $($statusJson.status)"
    switch ($statusJson.status) {
        "Succeeded" { break }
        "Failed"    { Write-Error "SignPath signing failed."; exit 2 }
        "Rejected"  { Write-Error "SignPath workflow rejected the file."; exit 3 }
    }
}

# ----------------------------------------------------------
# DOWNLOAD SIGNED FILE (streamed)
# ----------------------------------------------------------
$signedPath = "$FilePath.signed"
Write-Host "Downloading signed file..."
$downloadUrl = "$SignPathBaseUrl/api/v1/$OrganizationId/signing-jobs/$JobId/artifacts/signed-file"
$downloadReq = [System.Net.WebRequest]::Create($downloadUrl)
$downloadReq.Method = "GET"
$downloadReq.Headers["Authorization"] = "Bearer $ApiToken"
$downloadResp = $downloadReq.GetResponse()
$downloadStream = $downloadResp.GetResponseStream()
$fileOut = [System.IO.File]::Open($signedPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)

$buffer = New-Object byte[] 4MB
while (($read = $downloadStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
    $fileOut.Write($buffer, 0, $read)
}

$fileOut.Close()
$downloadStream.Close()
$downloadResp.Close()

# ----------------------------------------------------------
# REPLACE ORIGINAL FILE
# ----------------------------------------------------------
Write-Host "Replacing original file..."
Move-Item -Force -Path $signedPath -Destination $FilePath

Write-Host "Signing complete: $FilePath"
exit 0
