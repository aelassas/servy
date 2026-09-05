#Requires -Version 5.1
<#
.SYNOPSIS
    Restores Servy service configurations from a consolidated XML dump archive.

.DESCRIPTION
    Servy-Restore.ps1 verifies the integrity of a Servy dump archive against its SHA-256 sidecar file,
    safely extracts individual service XML configuration files into an ACL-hardened temporary location, and
    imports each configuration into Servy using the official Servy PowerShell module (Import-ServyServiceConfig).

    If the -Install switch parameter is supplied, the script also installs each imported service into the Windows
    Service Control Manager (SCM).

    Per-service import errors are caught gracefully; every file in the archive is processed regardless of earlier
    failures. If at least one service imports successfully and one or more fail, an exit code of 7 is returned to
    flag an incomplete restore.

    EXIT CODES:
    - 0 : Success. All service configurations were successfully imported (or no XML files exist in the archive).
    - 1 : Execution Failure. The script is not running in an elevated PowerShell session with Administrator privileges.
    - 2 : Import Failure. The official Servy PowerShell module (Servy.psm1) could not be located or imported.
    - 3 : Target Missing. The specified dump archive file does not exist.
    - 4 : I/O & Extraction Failure. The archive path is invalid, the archive could not be extracted, ACL hardening failed, malformed entries were detected, the -MaxAllowedEntries or -MaxUncompressedBytes safety limit was exceeded, or an unexpected runtime error occurred.
    - 5 : Checksum Verification Failure. The .sha256 sidecar is missing (without -SkipIntegrityCheck), could not be read, or a hash mismatch was detected.
    - 6 : Complete Import Failure. No service configurations could be imported from the archive.
    - 7 : Partial Import Warning. The restore completed, but one or more services failed to import.

    CRITICAL SECURITY NOTICE:
    The dump archive being restored contains highly sensitive information, including unencrypted execution
    parameters, command-line arguments, and process environment variables.
    Service logon credentials (Usernames and Passwords) are intentionally excluded from configuration exports.
    Importing configurations resets all service logon accounts to 'LocalSystem' by default.
    You must manually re-enter Logon Usernames and Passwords via Servy Manager, servy-cli, or the Servy PowerShell
    module for any services that require specific custom service runner accounts.

.PARAMETER DumpArchivePath
    Mandatory path specifying the Servy dump zip archive file to restore (e.g., 'C:\Backups\Servy_Dump.zip').
    The file must exist; otherwise the script exits with code 3.

.PARAMETER Install
    Optional switch parameter. When present, each imported service configuration is automatically installed
    into the Windows Service Control Manager.

.PARAMETER SkipIntegrityCheck
    Optional switch parameter. Skips SHA-256 sidecar verification entirely: the archive is restored
    without an integrity check, whether the .sha256 sidecar is absent, stale, or mismatching.

.PARAMETER MaxAllowedEntries
    Optional integer parameter. Specifies the maximum number of entries allowed in the dump archive
    to prevent zip bomb attacks during extraction. Defaults to 1000 (range: 1-100,000).

.PARAMETER MaxUncompressedBytes
    Optional 64-bit integer parameter. Specifies the maximum total uncompressed size (in bytes) allowed
    when extracting the dump archive. Defaults to 104857600 bytes / 100 MB (range: 1-10737418240 bytes / 10 GB).

.EXAMPLE
    .\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip"

.EXAMPLE
    .\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip" -Install

.EXAMPLE
    .\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip" -SkipIntegrityCheck

.NOTES
    SYSTEM REQUIREMENTS:
    - Operating System: Windows 10, Windows 11, or Windows Server 2016 and later.
    - PowerShell Version: Windows PowerShell 5.1 or PowerShell 7+ (Core).
    - Servy Core Components: Servy CLI and Servy PowerShell module (Servy.psm1) must be installed in %ProgramFiles%\Servy or portable root.
    - Archive Support: System.IO.Compression.FileSystem (.NET 4.5+ assembly).
    - Execution Privileges: Administrator privileges are required to interact with Servy configurations and manage Windows services.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = '指定 Servy 转储 zip 归档路径（例如 "C:\Backups\Servy_Dump.zip"）。')]
    [ValidateNotNullOrEmpty()]
    [string]$DumpArchivePath,

    [Parameter(Mandatory = $false, HelpMessage = '导入后可选地将每个服务安装到 Windows SCM。')]
    [switch]$Install,

    [Parameter(Mandatory = $false, HelpMessage = '完全跳过 SHA-256 伴随文件校验（无论伴随文件缺失、过期或不匹配）。')]
    [switch]$SkipIntegrityCheck,

    [Parameter(Mandatory = $false, HelpMessage = '归档允许的最大条目数（默认：1000）。')]
    [ValidateRange(1, 100000)]
    [int]$MaxAllowedEntries = 1000,

    [Parameter(Mandatory = $false, HelpMessage = '解压时允许的最大未压缩总字节数（默认：104857600 = 100 MB）。')]
    [ValidateRange(1, 10737418240)] # Up to 10 GB max safety ceiling
    [long]$MaxUncompressedBytes = 104857600
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
    Resolves and normalizes the target dump archive path for Servy-Restore.

.DESCRIPTION
    Resolves relative paths against the PowerShell provider path context.

.PARAMETER DumpArchivePath
    Mandatory path specifying the Servy dump zip archive file to restore.

.PARAMETER PSCmdletContext
    Optional PSCmdlet context for provider path resolution.

.OUTPUTS
    System.String - The resolved absolute archive path.
#>
function Resolve-ServyRestoreDumpPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DumpArchivePath,

        [Parameter(Mandatory = $false)]
        $PSCmdletContext
    )

    if ($null -ne $PSCmdletContext) {
        return $PSCmdletContext.GetUnresolvedProviderPathFromPSPath($DumpArchivePath)
    }
    else {
        if ([System.IO.Path]::IsPathRooted($DumpArchivePath)) {
            return [System.IO.Path]::GetFullPath($DumpArchivePath)
        }
        else {
            return [System.IO.Path]::GetFullPath((Join-Path (Get-Location).ProviderPath $DumpArchivePath))
        }
    }
}

<#
.SYNOPSIS
    Parses the expected SHA-256 checksum string from sidecar file content.

.DESCRIPTION
    Extracts the leading hex checksum token from sidecar text content formatted as '<hash> *<filename>'.

.PARAMETER SidecarText
    Mandatory sidecar file text content string.

.OUTPUTS
    System.String - The extracted expected SHA-256 hash string.
#>
function Get-ServySidecarExpectedHash {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SidecarText
    )

    if ([string]::IsNullOrWhiteSpace($SidecarText)) {
        return $null
    }

    return ($SidecarText.Trim() -split '\s+')[0]
}

<#
.SYNOPSIS
    Validates an individual zip archive entry against security and format rules.

.DESCRIPTION
    Verifies flat directory structure, duplicate entry prevention, path traversal safety, and .xml extension requirements.

.PARAMETER EntryName
    Mandatory entry short name within the archive.

.PARAMETER EntryFullName
    Mandatory full path name of the entry within the archive.

.PARAMETER RootPath
    Mandatory full target extraction root directory path.

.PARAMETER SeenEntryNames
    Mandatory HashSet tracking previously seen entry names for duplicate detection.

.OUTPUTS
    Hashtable containing 'IsValid', 'IsDirectory', 'TargetPath', and 'ErrorMessage'.
#>
function Test-ServyDumpArchiveEntry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$EntryName,

        [Parameter(Mandatory = $true)]
        [string]$EntryFullName,

        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        $SeenEntryNames
    )

    if ([string]::IsNullOrEmpty($EntryName)) {
        return @{ IsValid = $true; IsDirectory = $true; TargetPath = $null; ErrorMessage = $null }
    }

    if ($EntryName -ne $EntryFullName) {
        return @{ IsValid = $false; IsDirectory = $false; TargetPath = $null; ErrorMessage = "归档条目 '$EntryFullName' 包含子目录。不允许使用非扁平结构的转储归档。已中止。" }
    }

    if (-not $SeenEntryNames.Add($EntryName)) {
        return @{ IsValid = $false; IsDirectory = $false; TargetPath = $null; ErrorMessage = "归档包含重复条目 '$EntryName'。已中止：归档格式异常。" }
    }

    $targetPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($RootPath, $EntryFullName))

    if (-not $targetPath.StartsWith($RootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return @{ IsValid = $false; IsDirectory = $false; TargetPath = $null; ErrorMessage = "归档条目 '$EntryFullName' 解析到暂存目录之外。已中止：归档格式异常或存在恶意内容。" }
    }

    if (-not $EntryName.EndsWith('.xml', [System.StringComparison]::OrdinalIgnoreCase)) {
        return @{ IsValid = $false; IsDirectory = $false; TargetPath = $null; ErrorMessage = "归档条目 '$EntryFullName' 不是 XML 配置文件。已中止。" }
    }

    return @{ IsValid = $true; IsDirectory = $false; TargetPath = $targetPath; ErrorMessage = $null }
}

# If dot-sourced for testing, return immediately without executing main script body
if ($MyInvocation.InvocationName -eq '.') {
    return
}

# Render non-ASCII service names correctly in console output while preserving original session encoding
$previousOutputEncoding   = [Console]::OutputEncoding
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$tempExtractDir = $null

try {
    # Ensure the script is executing with Administrator privileges
    $currentIdentity  = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $currentPrincipal = New-Object System.Security.Principal.WindowsPrincipal($currentIdentity)
    $adminRole        = [System.Security.Principal.WindowsBuiltInRole]::Administrator

    if (-not $currentPrincipal.IsInRole($adminRole)) {
        Write-Host "Servy-Restore.ps1 需要管理员权限。请在提升的 PowerShell 会话中重新运行此脚本。" -ForegroundColor Red
        exit 1
    }

    # Resolve Servy PowerShell module location dynamically (supports portable and non-standard installs)
    $moduleCandidates = @(
        (Join-Path $PSScriptRoot 'Servy.psm1'),
        (Join-Path $env:ProgramFiles 'Servy\Servy.psm1')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    $servyModulePath = $moduleCandidates | Select-Object -First 1

    if (-not $servyModulePath) {
        Write-Host "在此脚本旁或 %ProgramFiles%\Servy 中未找到 Servy PowerShell 模块（Servy.psm1）。" -ForegroundColor Red
        exit 2
    }

    try {
        Import-Module -Name $servyModulePath -Force -ErrorAction Stop
    }
    catch {
        Write-Host "无法从 '$servyModulePath' 导入 Servy PowerShell 模块：$_" -ForegroundColor Red
        exit 2
    }

    # Catch-all for archive path resolution (e.g. invalid path characters or invalid drive letters)
    try {
        $resolvedArchivePath = Resolve-ServyRestoreDumpPath -DumpArchivePath $DumpArchivePath -PSCmdletContext $PSCmdlet
    }
    catch {
        Write-Host "指定的转储归档路径无效 '$DumpArchivePath'：$_" -ForegroundColor Red
        exit 4
    }

    if (-not (Test-Path -LiteralPath $resolvedArchivePath)) {
        Write-Host "指定的转储归档文件不存在：'$resolvedArchivePath'。" -ForegroundColor Red
        exit 3
    }

    # Verify archive integrity against SHA-256 sidecar file if present
    $sidecarPath = "$resolvedArchivePath.sha256"

    if ($SkipIntegrityCheck.IsPresent) {
        Write-Host "警告：已跳过完整性校验（指定了 -SkipIntegrityCheck）。" -ForegroundColor Yellow
    }
    elseif (Test-Path -LiteralPath $sidecarPath) {
        Write-Host "正在对照 SHA-256 伴随文件校验归档完整性..." -ForegroundColor Cyan

        try {
            $sidecarText  = [System.IO.File]::ReadAllText($sidecarPath)
            $expectedHash = Get-ServySidecarExpectedHash -SidecarText $sidecarText
            $actualHash   = (Get-FileHash -LiteralPath $resolvedArchivePath -Algorithm SHA256).Hash
        }
        catch {
            Write-Host "读取校验和文件或计算哈希以进行校验失败：$_" -ForegroundColor Red
            exit 5
        }

        if (-not [string]::Equals($expectedHash, $actualHash, [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Host "归档校验和不匹配！期望的 SHA-256 为 '$expectedHash'，实际计算为 '$actualHash'。还原已中止。" -ForegroundColor Red
            exit 5
        }
        Write-Host "归档 SHA-256 校验和验证成功。" -ForegroundColor Green
    }
    else {
        Write-Host "在 '$sidecarPath' 未找到 SHA-256 伴随文件。" -ForegroundColor Red
        Write-Host "若要在不进行完整性校验的情况下继续，请使用 -SkipIntegrityCheck 开关重新运行。" -ForegroundColor Red
        exit 5
    }

    # Create an isolated temporary directory for extracting XML files inside try/finally scope
    $tempExtractDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ServyRestore_" + [System.IO.Path]::GetRandomFileName())

    try {
        [void][System.IO.Directory]::CreateDirectory($tempExtractDir)

        # Restrict staging directory permissions to Administrators and SYSTEM exclusively
        try {
            Set-ServyHardenedFileAcl -Path $tempExtractDir -IsDirectory
        }
        catch {
            Write-Host "警告：无法限制解压目录 '$tempExtractDir' 的权限：$($_.Exception.Message)" -ForegroundColor Red
            Write-Host "该目录将存放未加密的纯文本服务配置。为避免泄露，操作已中止。" -ForegroundColor Red
            exit 4
        }

        Write-Host "正在解压转储归档 '$resolvedArchivePath'..." -ForegroundColor Cyan

        # Entry-path validation and bounded extraction
        Add-Type -AssemblyName "System.IO.Compression.FileSystem"

        $rootPath = [System.IO.Path]::GetFullPath($tempExtractDir.TrimEnd('\') + '\')
        $zipFile  = [System.IO.Compression.ZipFile]::OpenRead($resolvedArchivePath)

        $totalUncompressedSize = 0L
        $seenEntryNames = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

        try {
            if ($zipFile.Entries.Count -gt $MaxAllowedEntries) {
                Write-Host "归档包含 $($zipFile.Entries.Count) 个条目，超过限制 $MaxAllowedEntries。已中止。" -ForegroundColor Red
                exit 4
            }

            foreach ($entry in $zipFile.Entries) {
                $validation = Test-ServyDumpArchiveEntry -EntryName $entry.Name -EntryFullName $entry.FullName -RootPath $rootPath -SeenEntryNames $seenEntryNames

                if (-not $validation.IsValid) {
                    Write-Host $validation.ErrorMessage -ForegroundColor Red
                    exit 4
                }

                if ($validation.IsDirectory) { continue }

                $targetPath = $validation.TargetPath

                # Stream-read and enforce MaxUncompressedBytes on actual decompressed bytes to prevent zip bombs with forged metadata length
                $in  = $entry.Open()
                $out = [System.IO.File]::Create($targetPath)
                try {
                    $buf = New-Object byte[] 81920
                    while (($n = $in.Read($buf, 0, $buf.Length)) -gt 0) {
                        $totalUncompressedSize += $n
                        if ($totalUncompressedSize -gt $MaxUncompressedBytes) {
                            Write-Host "未压缩数据超过 $MaxUncompressedBytes 字节的限制。为防止资源耗尽，已中止。" -ForegroundColor Red
                            exit 4
                        }
                        $out.Write($buf, 0, $n)
                    }
                }
                finally {
                    $out.Dispose()
                    $in.Dispose()
                }
            }
        }
        finally {
            $zipFile.Dispose()
        }

        # Enumerate the XML configuration files in the extracted dump directory (archive layout is enforced flat)
        $xmlFiles = Get-ChildItem -LiteralPath $tempExtractDir -Filter "*.xml" -File

        if ($null -eq $xmlFiles) {
            Write-Host "转储归档中未找到 XML 配置文件。" -ForegroundColor Yellow
            exit 0
        }

        $xmlFileList = @($xmlFiles)
        Write-Host "找到 $($xmlFileList.Count) 个待还原的服务配置文件..." -ForegroundColor Cyan

        $imported = New-Object System.Collections.Generic.List[string]
        $failed   = New-Object System.Collections.Generic.List[object]

        # Iterate through extracted XML files and import each service configuration with isolated error handling
        foreach ($xmlFile in $xmlFileList) {
            Write-Host "正在从 '$($xmlFile.Name)' 导入配置..." -ForegroundColor Green

            # Build splatting hashtable for Import-ServyServiceConfig
            $importParams = @{
                ConfigFileType = "xml"
                Path           = $xmlFile.FullName
            }

            if ($Install.IsPresent) {
                $importParams['Install'] = $true
            }

            try {
                # Invoke Servy cmdlet to import (and optionally install) the service configuration
                Import-ServyServiceConfig @importParams
                $imported.Add($xmlFile.Name)
            }
            catch {
                Write-Host "  导入 '$($xmlFile.Name)' 失败：$($_.Exception.Message)" -ForegroundColor Red
                $failed.Add([PSCustomObject]@{ '文件' = $xmlFile.Name; '原因' = $_.Exception.Message })
            }
        }

        # If zero configurations succeeded, terminate with complete failure exit code
        if ($imported.Count -eq 0) {
            Write-Host "未能从归档导入任何服务配置。" -ForegroundColor Red
            exit 6
        }

        # Display completion status and critical security notice
        if ($failed.Count -gt 0) {
            Write-Host "`nServy 配置还原已完成，但有警告！" -ForegroundColor Yellow
            Write-Host "已成功导入 $($imported.Count) / $($xmlFileList.Count) 个服务。" -ForegroundColor Cyan
            Write-Host "`n以下服务文件导入失败：" -ForegroundColor Red
            $failed | Format-Table -AutoSize | Out-String | Write-Host
        }
        else {
            Write-Host "`nServy 配置还原已成功完成！" -ForegroundColor Green
            Write-Host "已成功导入 $($imported.Count) / $($xmlFileList.Count) 个服务。" -ForegroundColor Cyan
        }

        Write-Host @"

================================================================================
重要安全提示：
================================================================================
所还原的转储归档包含高度敏感信息！
- 服务执行参数、环境变量和启动参数来自未加密的纯文本 XML 配置文件。
- 请确保备份 zip 文件妥善存放，并限制访问权限。

关于服务还原与凭据的说明：
- 出于安全考虑，服务登录用户名和密码不会导出。
- 通过 Servy-Restore.ps1 还原配置时，默认会将所有服务登录账户重置为 'LocalSystem'。
- 对于需要特定自定义服务运行账户的服务，必须通过 Servy Manager、
  servy-cli 或 PowerShell 模块手动重新输入登录用户名和密码。
================================================================================
"@ -ForegroundColor Yellow

        if ($failed.Count -gt 0) {
            exit 7    # Restore completed, but one or more services failed to import
        }
    }
    catch {
        Write-Host "`nServy 配置还原失败：$_" -ForegroundColor Red
        exit 4
    }
    finally {
        # Clean up temporary extraction directory and extracted XML files with explicit failure reporting
        if (Test-Path -LiteralPath $tempExtractDir) {
            Remove-Item -LiteralPath $tempExtractDir -Recurse -Force -ErrorAction SilentlyContinue

            if (Test-Path -LiteralPath $tempExtractDir) {
                Write-Host @"

================================================================================
警告：检测到解压清理失败
================================================================================
临时解压目录未能完全删除：
  $tempExtractDir

其中包含未加密的纯文本服务配置。
请手动删除此目录，以防止凭据/配置泄露。
================================================================================
"@ -ForegroundColor Red
            }
        }
    }
}
finally {
    # Restore host console encoding
    try { [Console]::OutputEncoding = $previousOutputEncoding } catch { }
}
