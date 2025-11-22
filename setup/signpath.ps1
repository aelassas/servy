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
# CHECK SIGN FLAG (case-insensitive)
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
$CreateJobUrl = "$SignPathBaseUrl/api/v1/$OrganizationId/projects/$ProjectSlug/signing-jobs"
$CreateBody = @{ workflow = $WorkflowSlug } | ConvertTo-Json

$CreateRequest = [System.Net.WebRequest]::Create($CreateJobUrl)
$CreateRequest.Method = "POST"
$CreateRequest.ContentType = "application/json"
$CreateRequest.Headers["Authorization"] = "Bearer $ApiToken"

$Bytes = [System.Text.Encoding]::UTF8.GetBytes($CreateBody)
$CreateRequest.ContentLength = $Bytes.Length
$Stream = $CreateRequest.GetRequestStream()
$Stream.Write($Bytes, 0, $Bytes.Length)
$Stream.Close()

$Response = $CreateRequest.GetResponse()
$RespStream = $Response.GetResponseStream()
$Reader = New-Object System.IO.StreamReader($RespStream)
$JobResult = $Reader.ReadToEnd() | ConvertFrom-Json
$Reader.Close()
$Response.Close()

$JobId = $JobResult.signingJobId

# ----------------------------------------------------------
# UPLOAD ARTIFACT (streamed)
# ----------------------------------------------------------
Write-Host "Uploading artifact..."
$UploadUrl = "$SignPathBaseUrl/api/v1/$OrganizationId/signing-jobs/$JobId/artifacts/original-file"
$UploadRequest = [System.Net.WebRequest]::Create($UploadUrl)
$UploadRequest.Method = "PUT"
$UploadRequest.Headers["Authorization"] = "Bearer $ApiToken"
$UploadRequest.ContentType = "application/octet-stream"

$FileStream = [System.IO.File]::OpenRead($FilePath)
$UploadRequest.ContentLength = $FileStream.Length
$RequestStream = $UploadRequest.GetRequestStream()

$BufferSize = 4MB
$Buffer = New-Object byte[] $BufferSize
while (($Read = $FileStream.Read($Buffer, 0, $Buffer.Length)) -gt 0) {
    $RequestStream.Write($Buffer, 0, $Read)
}

$RequestStream.Close()
$FileStream.Close()

$UploadResponse = $UploadRequest.GetResponse()
$UploadResponse.Close()

# ----------------------------------------------------------
# START WORKFLOW
# ----------------------------------------------------------
Write-Host "Starting signing job..."
$StartUrl = "$SignPathBaseUrl/api/v1/$OrganizationId/signing-jobs/$JobId/start"
$StartReq = [System.Net.WebRequest]::Create($StartUrl)
$StartReq.Method = "POST"
$StartReq.Headers["Authorization"] = "Bearer $ApiToken"
$StartReq.ContentLength = 0
$StartResp = $StartReq.GetResponse()
$StartResp.Close()

# ----------------------------------------------------------
# POLL FOR STATUS
# ----------------------------------------------------------
Write-Host "Waiting for signing to complete..."
while ($true) {
    Start-Sleep -Seconds 5
    $StatusUrl = "$SignPathBaseUrl/api/v1/$OrganizationId/signing-jobs/$JobId"
    $StatusReq = [System.Net.WebRequest]::Create($StatusUrl)
    $StatusReq.Method = "GET"
    $StatusReq.Headers["Authorization"] = "Bearer $ApiToken"
    $StatusResp = $StatusReq.GetResponse()
    $StatusReader = New-Object System.IO.StreamReader($StatusResp.GetResponseStream())
    $StatusJson = $StatusReader.ReadToEnd() | ConvertFrom-Json
    $StatusReader.Close()
    $StatusResp.Close()

    Write-Host "Status: $($StatusJson.status)"
    switch ($StatusJson.status) {
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
$DownloadUrl = "$SignPathBaseUrl/api/v1/$OrganizationId/signing-jobs/$JobId/artifacts/signed-file"
$DownloadReq = [System.Net.WebRequest]::Create($DownloadUrl)
$DownloadReq.Method = "GET"
$DownloadReq.Headers["Authorization"] = "Bearer $ApiToken"
$DownloadResp = $DownloadReq.GetResponse()
$DownloadStream = $DownloadResp.GetResponseStream()
$FileOut = [System.IO.File]::Open($signedPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)

$Buffer = New-Object byte[] 4MB
while (($Read = $DownloadStream.Read($Buffer, 0, $Buffer.Length)) -gt 0) {
    $FileOut.Write($Buffer, 0, $Read)
}

$FileOut.Close()
$DownloadStream.Close()
$DownloadResp.Close()

# ----------------------------------------------------------
# REPLACE ORIGINAL FILE
# ----------------------------------------------------------
Write-Host "Replacing original file..."
Move-Item -Force -Path $signedPath -Destination $FilePath

Write-Host "Signing complete: $FilePath"
exit 0
cd ..