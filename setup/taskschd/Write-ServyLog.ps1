<#
.SYNOPSIS
    Safely writes log messages with UTF-8 encoding and performs size-based rotation.
#>
function Write-ServyLog {
    param(
        [Parameter(Mandatory=$true)][string]$FilePath,
        [Parameter(Mandatory=$true)][string]$Message,
        [int]$MaxSizeBytes   = 1048576, # 1 MB limit
        [int]$MaxBackupFiles = 10
    )

    try {
        # Normalize the path for consistent Mutex hashing
        $absPath = [System.IO.Path]::GetFullPath($FilePath)
        
        # Create a deterministic, valid system Mutex name from the file path.
        # We use a SHA256 hash to ensure the Mutex name is unique to the file but avoids Win32 
        # invalid Mutex characters (like backslashes) or path length limits.
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hashBytes  = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($absPath.ToLowerInvariant()))
            $hashString = [System.BitConverter]::ToString($hashBytes).Replace('-', '')
        } finally {
            $sha.Dispose()
        }
        
        
        try {
            $mutex = New-Object System.Threading.Mutex($false, "Global\ServyLog_$hashString")
        } catch [System.UnauthorizedAccessException] {
            # Standard users lack SeCreateGlobalPrivilege; per-session coordination is enough here.
            $mutex = New-Object System.Threading.Mutex($false, "Local\ServyLog_$hashString")
        }
        $hasLock = $false

        try {
            # Wait up to 1 second for the lock to clear high-contention traffic jams
            try {
                $hasLock = $mutex.WaitOne(1000)
            }
            catch [System.Threading.AbandonedMutexException] {
                # Previous owner was killed mid-write; we now own the mutex. Proceed.
                $hasLock = $true
                Write-Warning "Servy Logging: recovered an abandoned log mutex for $absPath."
            }
            if (-not $hasLock) {
                # Fail gracefully: log a warning to the console so the admin knows 
                # why the file was skipped, but allow the calling task to proceed.
                Write-Warning "Servy Logging: Mutex timeout after 1s for $absPath. Log entry dropped to prevent service stall."
                return
            }

            $logDir = Split-Path $absPath
            if (-not [string]::IsNullOrEmpty($logDir) -and -not (Test-Path $logDir)) {
                New-Item -ItemType Directory -Path $logDir -Force | Out-Null
            }

            # Handle log rotation if it exceeds max size (Now safely inside the Mutex)
            if (Test-Path $absPath) {
                $fileInfo = Get-Item $absPath
                if ($fileInfo.Length -gt $MaxSizeBytes) {
                    # Rotate using local time to maintain chronologic consistency
                    $localTime = Get-Date -Format "yyyyMMdd-HHmmss-fff"
                    $ext = [System.IO.Path]::GetExtension($absPath)
                    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($absPath)
                    
                    # Format: FileName_20260501-062849-301.log
                    $rotatedFileName = "{0}_{1}{2}" -f $baseName, $localTime, $ext
                    $target = Join-Path $logDir $rotatedFileName

                    if ([System.IO.File]::Exists($target)) {
                        $attempt = 0
                        do {
                            if ($attempt -gt 0) { Start-Sleep -Milliseconds 1; $localTime = Get-Date -Format "yyyyMMdd-HHmmss-fff" }
                            $rotatedFileName = "{0}_{1}{2}" -f $baseName, $localTime, $ext
                            $target = Join-Path $logDir $rotatedFileName
                            $attempt++
                        } while ([System.IO.File]::Exists($target) -and $attempt -lt 100)
                    }
                    
                    # Use .NET IO for atomic renaming; Rename-Item can exhibit quirky behavior under load
                    [System.IO.File]::Move($absPath, $target)

                    $rotatedPattern = "${baseName}_*${ext}"
                    Get-ChildItem -Path $logDir -Filter $rotatedPattern -ErrorAction SilentlyContinue |
                        Sort-Object LastWriteTime -Descending |
                        Select-Object -Skip $MaxBackupFiles |
                        Remove-Item -Force -ErrorAction SilentlyContinue
                }
            }

            # Enforce consistent UTF-8 logging with no BOM/UTF-16LE mix-ups
            $timestampedMsg = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $Message"
            
            # Use FileStream with FileShare.None inside the Mutex lock. 
            # This completely eliminates interleaved lines or swallowed "file in use" exceptions.
            $fs = New-Object System.IO.FileStream($absPath, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            try {
                $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
                $sw = New-Object System.IO.StreamWriter($fs, $utf8NoBom)
                try {
                    $sw.WriteLine($timestampedMsg)
                    $sw.Flush()
                } finally {
                    $sw.Dispose()
                }
            } finally {
                $fs.Dispose()
            }
        }
        finally {
            if ($hasLock) {
                $mutex.ReleaseMutex()
            }
            if ($null -ne $mutex) {
                $mutex.Dispose()
            }
        }
    }
    catch {
        # Silent fail-safe for the ultimate fallback layer to avoid crashing the caller
        Write-Warning "Servy Critical Logging Failure: $_"
    }
}
