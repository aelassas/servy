#Requires -Version 5.1
<#
.SYNOPSIS
    Generates a consolidated Servy dump archive containing all service configurations in XML format.

.DESCRIPTION
    Servy-Dump.ps1 inspects the local Servy SQLite configuration database (%ProgramData%\Servy\db\Servy.db),
    retrieves all registered service definitions using Windows native winsqlite3.dll, and exports each service
    configuration into an individual XML file using the official Servy PowerShell module. The exported XML files
    are then compressed into a single zip archive along with a SHA-256 sidecar file for integrity verification.

    Per-service export errors are caught gracefully. If at least one service exports successfully and one or more
    fail, or if the SHA-256 integrity sidecar could not be written, the zip archive is still generated and an exit
    code of 7 is returned to flag an incomplete backup to automated workflows.

    If the -Uninstall switch parameter is supplied, each successfully exported service is also uninstalled from
    the Windows Service Control Manager (SCM) and removed from the Servy database.

    EXIT CODES:
    - 0 : Success. All registered service configurations were successfully exported and archived (or no services exist).
    - 1 : Execution Failure. The script is not running in an elevated PowerShell session with Administrator privileges.
    - 2 : Import Failure. The official Servy PowerShell module (Servy.psm1) could not be located or imported.
    - 3 : Target Conflict. The destination archive file already exists and the -Overwrite switch was not specified.
    - 4 : I/O & Inspection Failure. The database could not be read, the destination path is invalid or unwritable, an existing SHA-256 sidecar could not be replaced under -Overwrite, archive compression or ACL hardening failed, or an unexpected runtime error occurred.
    - 5 : Setup Compilation Failure. Failed to compile native SQLite dynamic P/Invoke assembly bindings.
    - 6 : Complete Export Failure. No service configurations could be exported; no output archive was generated.
    - 7 : Partial Export Warning. The dump archive was successfully created, but one or more services failed to export or uninstall, or the SHA-256 integrity sidecar could not be written.
    - 8 : Archive Staging Mismatch. Staged configuration count does not match exported count; dump aborted.

    CRITICAL SECURITY NOTICE:
    The generated dump archive is highly sensitive. Exported XML configuration files contain sensitive plain-text
    data including execution parameters, command-line arguments, and process environment variables.
    Note that Windows Service Account credentials (Usernames and Passwords) are intentionally excluded from exports.
    When restoring configurations via Servy Manager, servy-cli, or Servy-Restore.ps1, all imported services will
    default to 'LocalSystem' and passwords must be re-entered manually for security reasons.

.PARAMETER DestinationArchivePath
    Mandatory path specifying the target zip archive destination file (e.g., 'C:\Backups\Servy_Dump.zip').
    If a directory path or trailing separator is provided, it writes to '$DestinationArchivePath\Servy_Dump.zip'; if no file extension is specified, `.zip` is appended.
    Use `-Overwrite` to replace an existing archive.

.PARAMETER Overwrite
    Optional switch parameter. Forces the script to overwrite the destination dump archive if it already exists.

.PARAMETER Uninstall
    Optional switch parameter. When present, uninstalls each successfully exported service from the Windows SCM
    and removes it from the Servy database. Prompts for confirmation via ShouldProcess before uninstallation
    unless -Confirm:$false is specified.

.EXAMPLE
    .\Servy-Dump.ps1 -DestinationArchivePath "C:\Backups\Servy_Dump.zip"

.EXAMPLE
    .\Servy-Dump.ps1 -DestinationArchivePath "C:\Backups\Servy_Dump.zip" -Overwrite

.EXAMPLE
    .\Servy-Dump.ps1 -DestinationArchivePath "C:\Backups\Servy_Dump.zip" -Uninstall

.EXAMPLE
    .\Servy-Dump.ps1 -DestinationArchivePath "C:\Backups\Servy_Dump.zip" -Overwrite -Uninstall

.NOTES
    SYSTEM REQUIREMENTS:
    - Operating System: Windows 10, Windows 11, or Windows Server 2016 and later (requires native %SystemRoot%\System32\winsqlite3.dll).
    - PowerShell Version: Windows PowerShell 5.1 or PowerShell 7+ (Core).
    - Servy Core Components: Servy CLI and Servy PowerShell module (Servy.psm1) must be installed in %ProgramFiles%\Servy or portable root.
    - SQLite Engine: Windows native WinRT/Win32 SQLite library (winsqlite3.dll); no external SQLite DLL drivers required.
    - Execution Privileges: Administrator privileges are required to interact with %ProgramData%\Servy and invoke Servy cmdlets.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true, HelpMessage = '指定目标归档输出文件路径（例如 "C:\Backups\Servy_Dump.zip"）。')]
    [ValidateNotNullOrEmpty()]
    [string]$DestinationArchivePath,

    [Parameter(Mandatory = $false, HelpMessage = '若目标转储归档已存在，则强制覆盖。')]
    [switch]$Overwrite,

    [Parameter(Mandatory = $false, HelpMessage = '导出成功后从 SCM 卸载服务并从数据库中移除。')]
    [switch]$Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
    Resolves and normalizes the target zip archive destination path for Servy-Dump.

.DESCRIPTION
    Detects directory-style inputs (trailing slashes, bare drive roots, or existing directories) and normalizes missing extensions to .zip.

.PARAMETER DestinationArchivePath
    Mandatory path specifying the target zip archive destination file or directory.

.PARAMETER PSCmdletContext
    Optional PSCmdlet context for provider path resolution.

.OUTPUTS
    System.String - The resolved absolute archive destination path.
#>
function Resolve-ServyDumpDestinationPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationArchivePath,

        [Parameter(Mandatory = $false)]
        $PSCmdletContext
    )

    $targetPath = $DestinationArchivePath.Trim()

    # Normalize bare drive-root inputs (e.g., 'C:' or 'c:') to 'C:\' before path resolution
    if ($targetPath -match '^[a-zA-Z]:$') {
        $targetPath += '\'
    }

    # Detect directory-style destination inputs (trailing path separator)
    $isDirDestination = $false
    if ($targetPath.EndsWith('\') -or $targetPath.EndsWith('/')) {
        $isDirDestination = $true
    }

    # Resolve against the PowerShell location, not the process working directory
    if ($null -ne $PSCmdletContext) {
        $resolvedArchivePath = $PSCmdletContext.GetUnresolvedProviderPathFromPSPath($targetPath)
    }
    else {
        if ([System.IO.Path]::IsPathRooted($targetPath)) {
            $resolvedArchivePath = [System.IO.Path]::GetFullPath($targetPath)
        }
        else {
            $resolvedArchivePath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location).ProviderPath $targetPath))
        }
    }

    if (-not $isDirDestination -and (Test-Path -LiteralPath $resolvedArchivePath -PathType Container)) {
        $isDirDestination = $true
    }

    if ($isDirDestination) {
        $dirPart = $resolvedArchivePath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        if ($dirPart.EndsWith(':')) { $dirPart += [System.IO.Path]::DirectorySeparatorChar }
        $resolvedArchivePath = [System.IO.Path]::Combine($dirPart, 'Servy_Dump.zip')
        Write-Host "目标路径是目录；已自动追加默认文件名到 '$resolvedArchivePath'。" -ForegroundColor Yellow
    }
    elseif ([string]::IsNullOrEmpty([System.IO.Path]::GetExtension($resolvedArchivePath))) {
        $resolvedArchivePath += '.zip'
        Write-Host "未指定文件扩展名；已将目标规范化为 '$resolvedArchivePath'。" -ForegroundColor Yellow
    }

    return $resolvedArchivePath
}

<#
.SYNOPSIS
    Sanitizes a service name into a safe, valid filesystem filename for XML exports.

.DESCRIPTION
    Replaces invalid characters, guards against reserved Win32 device names, and disambiguates collisions using a suffix counter.

.PARAMETER ServiceName
    Mandatory raw service name string to sanitize.

.PARAMETER InvalidChars
    Mandatory array of characters invalid in Win32 filenames.

.PARAMETER ReservedNames
    Mandatory array of reserved Win32 device names.

.PARAMETER UsedBaseNames
    Mandatory HashSet tracking already assigned base filenames to detect and resolve collisions.

.OUTPUTS
    System.String - Sanitized and collision-free base filename (without .xml extension).
#>
function Get-ServySanitizedFileName {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServiceName,

        [Parameter(Mandatory = $true)]
        [char[]]$InvalidChars,

        [Parameter(Mandatory = $true)]
        [string[]]$ReservedNames,

        [Parameter(Mandatory = $true)]
        $UsedBaseNames
    )

    # Sanitize service name for safe filesystem usage
    $baseFileName = $ServiceName
    foreach ($char in $InvalidChars) {
        $baseFileName = $baseFileName.Replace($char, '_')
    }

    # Prefix reserved Win32 device names to prevent mapping to device handles
    # Match PathSecurityGuard's stem-before-first-dot evaluation; only trailing spaces can survive the invalid-char sanitization above
    $stem = $baseFileName.Split('.')[0].TrimEnd(' ')
    if ($ReservedNames -contains $stem.ToUpperInvariant()) {
        $baseFileName = "_$baseFileName"
    }

    # Disambiguate names that sanitize onto an existing file
    $candidateName = $baseFileName
    $suffixCounter = 1
    while (-not $UsedBaseNames.Add($candidateName)) {
        $candidateName = "{0}_{1}" -f $baseFileName, $suffixCounter
        $suffixCounter++
    }

    if ($candidateName -ne $baseFileName) {
        Write-Host "  名称冲突：'$ServiceName' 清理后为 '$baseFileName'；改为写入 '$candidateName.xml'。" -ForegroundColor Yellow
    }

    return $candidateName
}

# Centralized reserved Win32 device names array, accessible across dot-sourced test harnesses.
# Synchronized with ReservedNames.cs canonical definition block.
$script:reservedNames = @(
    'CON', 'PRN', 'AUX', 'NUL', 'CONIN$', 'CONOUT$',
    'COM1', 'COM2', 'COM3', 'COM4', 'COM5', 'COM6', 'COM7', 'COM8', 'COM9',
    'COM¹', 'COM²', 'COM³',
    'LPT1', 'LPT2', 'LPT3', 'LPT4', 'LPT5', 'LPT6', 'LPT7', 'LPT8', 'LPT9',
    'LPT¹', 'LPT²', 'LPT³'
)

# Canonical set of built-in passwordless service accounts matching ServiceAccounts.cs
$script:runnableServiceAccounts = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($a in @(
    'LocalSystem', 'System', 'Local System', '.\LocalSystem', '.\System', '.\Local System',
    'NT AUTHORITY\LocalSystem', 'NT AUTHORITY\Local System', 'NT AUTHORITY\SYSTEM',
    'BUILTIN\LocalSystem', 'BUILTIN\System',
    'LocalService', 'NT AUTHORITY\LocalService', '.\LocalService', '.\Local Service',
    'NT AUTHORITY\Local Service', 'Local Service', 'BUILTIN\LocalService',
    'NetworkService', 'NT AUTHORITY\NetworkService', '.\NetworkService', '.\Network Service',
    'NT AUTHORITY\Network Service', 'Network Service', 'BUILTIN\NetworkService'
)) { [void]$script:runnableServiceAccounts.Add($a) }

# If dot-sourced for testing, return immediately without executing main script body
if ($MyInvocation.InvocationName -eq '.') {
    return
}

# Render non-ASCII service names correctly in console output while preserving original session encoding
$previousOutputEncoding   = [Console]::OutputEncoding
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$createdParentPath   = $null
$createdRootBoundary = $null
$tempStagingDir      = $null

try {
    # Ensure the script is executing with Administrator privileges
    $currentIdentity  = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $currentPrincipal = New-Object System.Security.Principal.WindowsPrincipal($currentIdentity)
    $adminRole        = [System.Security.Principal.WindowsBuiltInRole]::Administrator

    if (-not $currentPrincipal.IsInRole($adminRole)) {
        Write-Host "Servy-Dump.ps1 需要管理员权限。请在提升的 PowerShell 会话中重新运行此脚本。" -ForegroundColor Red
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

    # Catch-all for destination resolution (e.g. invalid path characters or invalid drive letters)
    try {
        $resolvedArchivePath = Resolve-ServyDumpDestinationPath -DestinationArchivePath $DestinationArchivePath -PSCmdletContext $PSCmdlet
        $sidecarPath         = "$resolvedArchivePath.sha256"

        # Check if destination dump file already exists
        if (Test-Path -LiteralPath $resolvedArchivePath) {
            if (-not $Overwrite.IsPresent) {
                Write-Host "目标转储文件已存在：'$resolvedArchivePath'。为防止覆盖，操作已中止。" -ForegroundColor Red
                exit 3
            }
            Write-Host "发现现有转储归档。已指定 -Overwrite；将替换目标文件。" -ForegroundColor Yellow
        }

        # Verify existing sidecar file can be written/overwritten under -Overwrite before proceeding with exports
        if ($Overwrite.IsPresent -and (Test-Path -LiteralPath $sidecarPath)) {
            try {
                $fs = [System.IO.File]::Open($sidecarPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
                $fs.Dispose()
            }
            catch {
                Write-Host "现有 SHA-256 伴随文件 '$sidecarPath' 被锁定或不可写，无法替换。为防止校验和不匹配，操作已中止。" -ForegroundColor Red
                exit 4
            }
        }

        # Prove destination parent directory is created and writable BEFORE exporting
        $parentDir = [System.IO.Path]::GetDirectoryName($resolvedArchivePath)

        if (-not [string]::IsNullOrEmpty($parentDir)) {
            if (-not (Test-Path -LiteralPath $parentDir)) {
                try {
                    # Determine deepest ancestor that already exists to prevent leaving empty ancestor dirs on failure
                    $existingAncestor = $parentDir
                    while (-not [string]::IsNullOrEmpty($existingAncestor) -and -not (Test-Path -LiteralPath $existingAncestor)) {
                        $existingAncestor = [System.IO.Path]::GetDirectoryName($existingAncestor)
                    }

                    [void][System.IO.Directory]::CreateDirectory($parentDir)
                    $createdParentPath   = $parentDir
                    $createdRootBoundary = $existingAncestor
                }
                catch {
                    Write-Host "无法创建目标目录 '$parentDir'：$_" -ForegroundColor Red
                    exit 4
                }
            }

            # Write probe confirmation
            $probeFile = [System.IO.Path]::Combine($parentDir, ".servydump_probe_" + [System.IO.Path]::GetRandomFileName())
            try {
                [System.IO.File]::WriteAllBytes($probeFile, @())
                Remove-Item -LiteralPath $probeFile -Force -ErrorAction SilentlyContinue
                if (Test-Path -LiteralPath $probeFile) {
                    Write-Host "警告：无法从 '$parentDir' 删除写入探测文件 '$probeFile'（允许创建但不允许删除）——请手动删除。" -ForegroundColor Yellow
                }
            }
            catch {
                Write-Host "目标目录 '$parentDir' 不可写：$_" -ForegroundColor Red
                exit 4
            }
        }
    }
    catch {
        Write-Host "指定的目标路径无效 '$DestinationArchivePath'：$_" -ForegroundColor Red
        exit 4
    }

    # Validate existence of the Servy SQLite database file
    $dbPath = [System.IO.Path]::Combine($env:ProgramData, "Servy", "db", "Servy.db")

    if (-not (Test-Path -LiteralPath $dbPath)) {
        Write-Host "在 '$dbPath' 未找到 Servy 数据库。没有可导出的服务。" -ForegroundColor Yellow
        exit 0
    }

    # Register C# P/Invoke wrapper targeting Windows native %SystemRoot%\System32\winsqlite3.dll with UTF-16 marshaling
    if (-not ([System.Management.Automation.PSTypeName]'ServyNativeWinSqliteRecord').Type) {
        $sqliteBinding = @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class ServyServiceRecord
{
    private string name;
    private string userAccount;

    public string Name
    {
        get { return name; }
        set { name = value; }
    }

    public string UserAccount
    {
        get { return userAccount; }
        set { userAccount = value; }
    }
}

public static class ServyNativeWinSqliteRecord
{
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_open_v2(byte[] filenameUtf8, out IntPtr ppDb, int flags, IntPtr zVfs);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_close(IntPtr db);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPStr)] string zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_step(IntPtr stmt);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text16", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr sqlite3_column_text16(IntPtr stmt, int iCol);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_finalize(IntPtr stmt);

    public static List<ServyServiceRecord> GetServices(string dbPath)
    {
        List<ServyServiceRecord> result = new List<ServyServiceRecord>();
        IntPtr db;

        byte[] pathUtf8 = System.Text.Encoding.UTF8.GetBytes(dbPath + "\0");
        int rc = sqlite3_open_v2(pathUtf8, out db, 0x1 /* SQLITE_OPEN_READONLY */, IntPtr.Zero);
        if (rc != 0)
        {
            if (db != IntPtr.Zero) sqlite3_close(db);
            throw new InvalidOperationException(string.Format("sqlite3_open_v2 在 '{0}' 上失败，结果代码 {1}。", dbPath, rc));
        }

        try
        {
            IntPtr stmt;
            rc = sqlite3_prepare_v2(db, "SELECT Name, UserAccount FROM Services ORDER BY Name", -1, out stmt, IntPtr.Zero);
            if (rc != 0)
            {
                throw new InvalidOperationException(string.Format("sqlite3_prepare_v2 失败，结果代码 {0}。", rc));
            }

            try
            {
                int stepRc;
                while ((stepRc = sqlite3_step(stmt)) == 100) // SQLITE_ROW = 100
                {
                    IntPtr namePtr = sqlite3_column_text16(stmt, 0);
                    IntPtr accountPtr = sqlite3_column_text16(stmt, 1);
                    if (namePtr != IntPtr.Zero)
                    {
                        ServyServiceRecord record = new ServyServiceRecord();
                        record.Name = Marshal.PtrToStringUni(namePtr);
                        record.UserAccount = accountPtr != IntPtr.Zero ? Marshal.PtrToStringUni(accountPtr) : null;
                        result.Add(record);
                    }
                }
                if (stepRc != 101) // SQLITE_DONE
                {
                    throw new InvalidOperationException(string.Format("sqlite3_step 失败，结果代码 {0}；服务列表可能不完整。", stepRc));
                }
            }
            finally
            {
                sqlite3_finalize(stmt);
            }
        }
        finally
        {
            sqlite3_close(db);
        }

        return result;
    }
}
"@
        try {
            Add-Type -TypeDefinition $sqliteBinding
        }
        catch {
            Write-Host "编译 winsqlite3 P/Invoke 绑定程序集失败：$_" -ForegroundColor Red
            exit 5
        }
    }

    # Create an isolated temporary directory for staging exported XML files inside the try/finally scope
    $tempStagingDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ServyDump_" + [System.IO.Path]::GetRandomFileName())

    try {
        [void][System.IO.Directory]::CreateDirectory($tempStagingDir)

        # Restrict staging directory permissions to Administrators and SYSTEM exclusively using language-agnostic Well-Known SIDs
        try {
            Set-ServyHardenedFileAcl -Path $tempStagingDir -IsDirectory
        }
        catch {
            Write-Host "警告：无法限制暂存目录 '$tempStagingDir' 的权限：$($_.Exception.Message)" -ForegroundColor Red
            Write-Host "该目录将存放未加密的纯文本服务配置。为避免泄露，操作已中止。" -ForegroundColor Red
            exit 4
        }

        # Query Servy SQLite database via Windows native winsqlite3.dll
        try {
            $servicesList = [ServyNativeWinSqliteRecord]::GetServices($dbPath)
            $serviceNames = @($servicesList | Select-Object -ExpandProperty Name)

            # Build and display service account table for debugging / diagnostic inspection
            $serviceTable = foreach ($svc in $servicesList) {
                [PSCustomObject]@{
                    '服务名称' = $svc.Name
                    '用户账户' = if ([string]::IsNullOrWhiteSpace($svc.UserAccount)) { '[LocalSystem]' } else { $svc.UserAccount }
                }
            }

            Write-Host "`n已注册的 Servy 服务：" -ForegroundColor Cyan
            $serviceTable | Format-Table -AutoSize | Out-String | Write-Host
        }
        catch {
            Write-Host "查询 Servy 数据库 '$dbPath' 失败：$($_.Exception.Message)" -ForegroundColor Red
            exit 4
        }

        if ($serviceNames.Count -eq 0) {
            Write-Host "在数据库 '$dbPath' 中未找到任何服务。" -ForegroundColor Yellow
            exit 0
        }

        Write-Host "找到 $($serviceNames.Count) 个待导出的服务..." -ForegroundColor Cyan

        $exported      = New-Object System.Collections.Generic.List[string]
        $failed        = New-Object System.Collections.Generic.List[object]
        $usedBaseNames = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        $invalidChars  = [System.IO.Path]::GetInvalidFileNameChars()

        # Export each service configuration into individual XML files with per-item exception isolation
        foreach ($serviceName in $serviceNames) {
            $candidateName = Get-ServySanitizedFileName -ServiceName $serviceName -InvalidChars $invalidChars -ReservedNames $script:reservedNames -UsedBaseNames $usedBaseNames

            $xmlExportPath = [System.IO.Path]::Combine($tempStagingDir, "$candidateName.xml")

            Write-Host "正在导出 '$serviceName' 的配置 -> '$candidateName.xml'..." -ForegroundColor Green

            try {
                Export-ServyServiceConfig -Name $serviceName -ConfigFileType "xml" -Path $xmlExportPath
                $exported.Add($serviceName)
            }
            catch {
                Write-Host "  导出 '$serviceName' 失败：$($_.Exception.Message)" -ForegroundColor Red
                $failed.Add([PSCustomObject]@{ '服务' = $serviceName; '原因' = $_.Exception.Message })
            }
        }

        # If zero configurations succeeded, terminate without creating an empty archive
        if ($exported.Count -eq 0) {
            Write-Host "未能导出任何服务配置。未生成转储归档。" -ForegroundColor Red
            exit 6
        }

        # Assert staged configuration count matches successful exports before compressing
        $stagedXmlFiles = Get-ChildItem -LiteralPath $tempStagingDir -Filter "*.xml" -File
        $stagedCount    = if ($null -eq $stagedXmlFiles) { 0 } else { @($stagedXmlFiles).Count }

        if ($stagedCount -ne $exported.Count) {
            Write-Host "期望 $($exported.Count) 个已导出配置，但暂存了 $stagedCount 个。拒绝写入不完整的归档。" -ForegroundColor Red
            exit 8
        }

        # Compress the staging directory containing XML dumps into the target zip file
        Write-Host "正在将导出的配置压缩为 zip 归档..." -ForegroundColor Cyan

        $stagedItemsToCompress = $stagedXmlFiles | Select-Object -ExpandProperty FullName

        $compressParams = @{
            LiteralPath      = $stagedItemsToCompress
            DestinationPath  = $resolvedArchivePath
            CompressionLevel = "Optimal"
        }

        if ($Overwrite.IsPresent) {
            $compressParams['Force'] = $true
        }

        try {
            Compress-Archive @compressParams
        }
        catch {
            Write-Host "`nServy 配置转储在压缩期间失败：$_" -ForegroundColor Red
            Write-Host "未能在 '$resolvedArchivePath' 生成有效归档。" -ForegroundColor Red
            exit 4
        }

        # Apply ACL hardening to the newly created archive
        try {
            Set-ServyHardenedFileAcl -Path $resolvedArchivePath
        }
        catch {
            Write-Host "`n警告：无法限制归档 '$resolvedArchivePath' 的权限：$($_.Exception.Message)" -ForegroundColor Red

            # Best-effort removal of the unprotected archive
            Remove-Item -LiteralPath $resolvedArchivePath -Force -ErrorAction SilentlyContinue

            if (Test-Path -LiteralPath $resolvedArchivePath) {
                Write-Host "无法删除该归档。它以未受保护状态存在于 '$resolvedArchivePath'，并包含纯文本服务配置——请手动删除或保护。" -ForegroundColor Red
            }
            else {
                Write-Host "已删除该归档，因为它包含未加密的纯文本服务配置且无法加以保护。" -ForegroundColor Red
            }

            if ($Overwrite.IsPresent -and (Test-Path -LiteralPath $sidecarPath)) {
                Remove-Item -LiteralPath $sidecarPath -Force -ErrorAction SilentlyContinue
                if (Test-Path -LiteralPath $sidecarPath) {
                    Write-Host "警告：无法删除既有 SHA-256 伴随文件，它仍位于 '$sidecarPath'——请手动删除或更新。" -ForegroundColor Red
                }
            }

            exit 4
        }

        # Remove pre-existing sidecar only after compression and hardening succeed to avoid corrupting surviving backups
        if ($Overwrite.IsPresent -and (Test-Path -LiteralPath $sidecarPath)) {
            Remove-Item -LiteralPath $sidecarPath -Force -ErrorAction SilentlyContinue
            if (Test-Path -LiteralPath $sidecarPath) {
                Write-Host "警告：无法删除既有 SHA-256 伴随文件，它仍位于 '$sidecarPath'——请手动删除或更新。" -ForegroundColor Red
            }
        }

        $sidecarWriteFailed = $false

        # Emit SHA-256 sidecar hash file for integrity verification
        try {
            $hashValue = (Get-FileHash -LiteralPath $resolvedArchivePath -Algorithm SHA256).Hash
            [System.IO.File]::WriteAllText($sidecarPath, "$hashValue *$([System.IO.Path]::GetFileName($resolvedArchivePath))`n", (New-Object System.Text.UTF8Encoding($false)))
            Set-ServyHardenedFileAcl -Path $sidecarPath
            Write-Host "已写入 SHA-256 校验和伴随文件 -> '$sidecarPath'" -ForegroundColor Cyan
        }
        catch {
            $sidecarWriteFailed = $true
            if (Test-Path -LiteralPath $sidecarPath) {
                Remove-Item -LiteralPath $sidecarPath -Force -ErrorAction SilentlyContinue
            }
            Write-Host "已在 '$resolvedArchivePath' 创建归档，但无法写入 SHA-256 伴随文件：$($_.Exception.Message)" -ForegroundColor Red

            if (Test-Path -LiteralPath $sidecarPath) {
                Write-Host "无法删除过期的 SHA-256 伴随文件，它仍位于 '$sidecarPath'——还原前请手动删除或重新生成（Get-FileHash），否则还原校验将拒绝该归档。" -ForegroundColor Red
            }
            else {
                Write-Host "在依赖完整性校验前，请手动生成校验和（Get-FileHash）。" -ForegroundColor Yellow
            }
            $failed.Add([PSCustomObject]@{ '服务' = "SHA256 伴随文件"; '原因' = "伴随文件写入失败：$($_.Exception.Message)" })
        }

        # If -Uninstall is specified, uninstall successfully exported services from SCM and DB
        if ($Uninstall.IsPresent) {
            if ($sidecarWriteFailed) {
                Write-Host "`n警告：由于无法写入 SHA-256 完整性伴随文件，服务未被卸载。" -ForegroundColor Red
                Write-Host "在使用 -Uninstall 运行前，请手动验证归档保护与伴随文件。" -ForegroundColor Yellow
            }
            else {
                # Check for custom accounts that will lose credentials
                $customAccountServices = @($servicesList | Where-Object {
                    $svcName = $_.Name
                    $account = if ($null -ne $_.UserAccount) { $_.UserAccount.ToString().Trim() } else { "" }

                    $isExported = $exported.Contains($svcName)
                    $isCustomAccount = (-not [string]::IsNullOrWhiteSpace($account)) -and
                                       (-not $script:runnableServiceAccounts.Contains($account))

                    $isExported -and $isCustomAccount
                })

                if ($customAccountServices.Count -gt 0) {
                    Write-Host "`n警告：$($customAccountServices.Count) / $($exported.Count) 个服务使用自定义登录账户；其凭据不在归档中，卸载时将从数据库删除（还原后请手动重新输入）：" -ForegroundColor Yellow

                    $affectedTable = foreach ($svc in $customAccountServices) {
                        [PSCustomObject]@{
                            '服务名称' = $svc.Name
                            '用户账户' = $svc.UserAccount
                        }
                    }

                    $affectedTable | Format-Table -AutoSize | Out-String | Write-Host
                }

                if ($PSCmdlet.ShouldProcess("$($exported.Count) 个已导出服务", "从 SCM 卸载并从 Servy 数据库删除")) {
                    Write-Host "`n正在从 SCM 和数据库卸载已成功导出的服务..." -ForegroundColor Cyan

                    foreach ($serviceName in $exported) {
                        Write-Host "正在卸载服务 '$serviceName'..." -ForegroundColor Yellow
                        try {
                            Uninstall-ServyService -Name $serviceName -ErrorAction Stop
                        }
                        catch {
                            Write-Host "  卸载 '$serviceName' 失败：$($_.Exception.Message)" -ForegroundColor Red
                            $failed.Add([PSCustomObject]@{ '服务' = $serviceName; '原因' = "卸载失败：$($_.Exception.Message)" })
                        }
                    }
                }
            }
        }

        # Display completion status and critical security warning
        Write-Host "`nServy 配置转储已完成！" -ForegroundColor Green
        Write-Host "已成功导出 $($exported.Count) / $($serviceNames.Count) 个服务。" -ForegroundColor Cyan
        Write-Host "转储位置：$resolvedArchivePath" -ForegroundColor Cyan

        if ($failed.Count -gt 0) {
            Write-Host "`n以下服务在导出或卸载期间遇到错误：" -ForegroundColor Red
            $failed | Format-Table -AutoSize | Out-String | Write-Host
        }

        Write-Host @"

================================================================================
重要安全警告：
================================================================================
生成的转储归档包含高度敏感信息！
- 服务执行参数、环境变量和启动参数以未加密纯文本形式
  存储在导出的 XML 文件中。
- 请妥善保护此文件，并仅允许授权管理员访问。

关于服务还原的说明：
- 出于安全考虑，服务登录用户名和密码不会导出。
- 通过 Servy-Restore.ps1、servy-cli 或 Servy Manager 还原此备份时，
  默认会将所有服务登录账户自动设为 'LocalSystem'。
- 对于需要特定自定义服务运行账户的服务，必须手动重新输入登录用户名和密码。
================================================================================
"@ -ForegroundColor Yellow

        if ($failed.Count -gt 0) {
            exit 7    # Archive generated successfully, but incomplete/partial errors occurred
        }
    }
    catch {
        Write-Host "`nServy 配置转储失败：$_" -ForegroundColor Red
        exit 4
    }
    finally {
        # Clean up temporary staging directory and XML files with explicit failure reporting
        if (Test-Path -LiteralPath $tempStagingDir) {
            Remove-Item -LiteralPath $tempStagingDir -Recurse -Force -ErrorAction SilentlyContinue

            if (Test-Path -LiteralPath $tempStagingDir) {
                Write-Host @"

================================================================================
警告：检测到暂存清理失败
================================================================================
临时暂存目录未能完全删除：
  $tempStagingDir

其中包含未加密的纯文本服务配置。
请手动删除此目录，以防止凭据/配置泄露。
================================================================================
"@ -ForegroundColor Red
            }
        }
    }
}
finally {
    # If parent directory was created during execution but dump failed before creating archive, clean up orphaned folders bottom-up
    if ($null -ne $createdParentPath -and (Test-Path -LiteralPath $createdParentPath) -and -not (Test-Path -LiteralPath $resolvedArchivePath)) {
        $dir = $createdParentPath
        while ($null -ne $dir -and $dir -ne $createdRootBoundary -and (Test-Path -LiteralPath $dir)) {
            $items = Get-ChildItem -LiteralPath $dir -Force -ErrorAction SilentlyContinue
            if ($null -ne $items -and @($items).Count -gt 0) { break }
            Remove-Item -LiteralPath $dir -Force -ErrorAction SilentlyContinue
            $dir = [System.IO.Path]::GetDirectoryName($dir)
        }
    }

    # Restore host console encoding
    try { [Console]::OutputEncoding = $previousOutputEncoding } catch { }
}
