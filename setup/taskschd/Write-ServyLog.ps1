<#
.SYNOPSIS
    Safely writes log messages with UTF-8 encoding and performs size-based rotation.
#>
function Write-ServyLog {
    param(
        [Parameter(Mandatory=$true)][string]$FilePath,
        [Parameter(Mandatory=$true)][string]$Message,
        [int]$MaxSizeBytes = 1048576 # 1 MB limit
    )

    try {
        $logDir = Split-Path $FilePath
        if (-not [string]::IsNullOrEmpty($logDir) -and -not (Test-Path $logDir)) {
            New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        }

        # Handle log rotation if it exceeds max size
        if (Test-Path $FilePath) {
            $fileInfo = Get-Item $FilePath
            if ($fileInfo.Length -gt $MaxSizeBytes) {
                # Rotate using local time to maintain chronologic consistency
                $localTime = Get-Date -Format "yyyyMMdd-HHmmss"
                $ext = [System.IO.Path]::GetExtension($FilePath)
                $baseName = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
                
                # Format: FileName_20260501-062849.log
                $rotatedFileName = "{0}_{1}{2}" -f $baseName, $localTime, $ext
                
                Rename-Item -Path $FilePath -NewName $rotatedFileName -Force
            }
        }

        # Enforce consistent UTF-8 logging with no BOM/UTF-16LE mix-ups
        $timestampedMsg = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $Message"
        [System.IO.File]::AppendAllText($FilePath, $timestampedMsg + [Environment]::NewLine, [System.Text.Encoding]::UTF8)
    }
    catch {
        # Silent fail-safe for the ultimate fallback layer to avoid crashing the caller
        Write-Warning "Servy Critical Logging Failure: $_"
    }
}