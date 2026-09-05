## 目录
1. [简介](#简介)
1. [系统要求](#系统要求)
   1. [版本对比](#版本对比)
1. [安装选项](#安装选项)
   1. [快速安装](#快速安装)
   1. [将 Servy 加入 PATH](#将-servy-加入-path)
   1. [手动安装](#手动安装)
   1. [静默安装/卸载](#静默安装卸载)
1. [安全与杀毒软件设置](#安全与杀毒软件设置)

## 简介

本指南介绍在 Windows 系统上安装 Servy 的要求与多种方法。

Servy 提供两个不同构建版本，以兼容现代与旧版 Windows 环境。

* **现代版本（.NET 10.0+）：** 适用于 Windows 10、11 以及现代 Windows Server。
* **旧版构建（.NET Framework 4.8）：** 适用于 Windows 7、8 以及较旧的 Server 版本。

## 系统要求

> [!CAUTION]
> **需要管理员权限**才能安装 Servy 以及管理 Windows 服务。

### 版本对比

| 功能 | .NET 10.0+ 版本 (x64) | .NET 10.0+ 版本 (ARM64) | .NET Framework 4.8 版本（旧版 x64） |
| --- | --- | --- | --- |
| **文件名** | `servy-x.x-x64-installer.exe` | `servy-x.x-arm64-installer.exe` | `servy-x.x-net48-x64-installer.exe` |
| **操作系统支持** | Windows 10 (1809+)、11、Server 2016+ | Windows 11 on ARM、Windows on ARM | Windows 7 SP1、8.x、Server 2008 R2+ |
| **依赖** | 无（自包含） | 无（自包含） | [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) |
| **性能** | 优化（现代运行时） | 原生 ARM64（优化运行时） | 标准（旧版运行时） |

**关于旧版操作系统的说明：** 尽管 .NET 10.0 构建可能能在 Windows 7 上运行，但属于**不受支持**。对于 Windows 7 SP1 或 Windows Server 2008 R2，必须使用 **.NET Framework 4.8** 版本以确保稳定性。

## 安装选项

安装 Servy 有两种途径：若环境支持，可使用包管理器；若需要完全掌控安装过程，也可[下载安装程序](https://github.com/aelassas/servy/releases/latest)并手动安装。

### 快速安装

**WinGet**
```powershell
winget install servy
```

**Chocolatey**
```powershell
choco install -y servy
```

**Scoop**
```powershell
scoop bucket add extras
scoop install servy
```

**Patch My PC**

Servy 已收录于官方 [Patch My PC 目录](https://patchmypc.com/supported-products/)，可通过 Microsoft Intune 与 ConfigMgr（SCCM）进行企业自动化部署与更新。

> [!NOTE]
> **旧版操作系统支持（Windows 7 SP1 / 8.x / Server 2008 R2）：** 包管理器提供的是自包含现代版本。对于较旧平台，请直接从 [GitHub Releases](https://github.com/aelassas/servy/releases/latest) 下载 `servy-x.x-net48-x64-installer.exe` 或 `servy-x.x-net48-x64-portable.7z`（需要 .NET Framework 4.8）。

### 将 Servy 加入 PATH

安装程序的 **Add Servy to PATH** 选项（Additional Options 页，默认启用，需要 CLI 组件）会将 Servy 目录加入系统 **PATH**，从而可在任意提升权限的命令提示符或 PowerShell 会话中运行 `servy-cli`。取消勾选则不改动 `PATH`；仅当由安装程序添加时，卸载才会移除该条目。便携包不会修改 `PATH`——请自行添加其目录，或使用 Scoop（通过自身 shim 将 `servy-cli` 加入 `PATH`）。

### 手动安装
1. [下载最新版本](https://github.com/aelassas/servy/releases/latest)。
2. 运行安装程序（会提示请求管理员权限）。
3. 从开始菜单或桌面快捷方式启动 Servy。

### 静默安装/卸载

#### 静默安装

可从管理员命令提示符使用以下命令静默安装 Servy（无任何 UI）：

```powershell
# 适用于 .NET 10.0+ (x64)
.\servy-<version>-x64-installer.exe /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL

# 适用于 .NET 10.0+ (ARM64)
.\servy-<version>-arm64-installer.exe /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL

# 适用于 .NET Framework 4.8
.\servy-<version>-net48-x64-installer.exe /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL
```

使用 PowerShell：
```powershell
# 适用于 .NET 10.0+ (x64)
Start-Process -FilePath ".\servy-<version>-x64-installer.exe" -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CLOSEAPPLICATIONS /NOCANCEL' -Verb RunAs -Wait

# 适用于 .NET 10.0+ (ARM64)
Start-Process -FilePath ".\servy-<version>-arm64-installer.exe" -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CLOSEAPPLICATIONS /NOCANCEL' -Verb RunAs -Wait

# 适用于 .NET Framework 4.8
Start-Process -FilePath ".\servy-<version>-net48-x64-installer.exe" -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CLOSEAPPLICATIONS /NOCANCEL' -Verb RunAs -Wait

# 刷新当前会话中的 PATH
$env:Path = (((
    [Environment]::GetEnvironmentVariable('Path', 'Machine'),
    [Environment]::GetEnvironmentVariable('Path', 'User')
) -join ';').Split(';', [StringSplitOptions]::RemoveEmptyEntries) |
    Select-Object -Unique) -join ';'
```

安装完成需要稍等片刻。

若遇到问题，可通过添加以下选项启用日志：
```text
/LOG="servy-install.log"
```

#### 自定义静默安装

仅安装 CLI，使用这些选项：
```text
/SetupType=custom /Components=install_cli
```

仅安装桌面应用，使用这些选项：
```text
/SetupType=custom /Components=install_main_app
```

仅安装 Manager 应用，使用这些选项：
```text
/SetupType=custom /Components=install_manager
```

仅安装桌面应用与 CLI，使用这些选项：
```text
/SetupType=custom /Components=install_main_app,install_cli
```

仅安装 Manager 应用与 CLI，使用这些选项：
```text
/SetupType=custom /Components=install_manager,install_cli
```

#### 静默卸载

可从管理员命令提示符使用以下命令静默卸载 Servy（无任何 UI）（PowerShell）：
```powershell
# 默认位置；若 Servy 安装到自定义目录，请调整路径
& "$env:ProgramFiles\Servy\unins000.exe" /VERYSILENT /NORESTART /SUPPRESSMSGBOXES
```

#### 开关说明

| 开关 | 说明 |
|--------|-------------|
| `/VERYSILENT` | 完全静默安装，无向导或进度窗口。 |
| `/NORESTART` | 阻止安装后自动重启系统。 |
| `/SUPPRESSMSGBOXES` | 抑制安装期间的所有消息框（包括错误）。 |
| `/SP-` | 禁用“This will install...”启动提示。 |
| `/CLOSEAPPLICATIONS` | 若运行中的应用占用文件，请求其关闭。 |
| `/NOCANCEL` | 阻止用户取消安装。 |

## 安全与杀毒软件设置

Servy 经过数字签名，并已由 Microsoft Security Intelligence 审核，确认安全。它仅执行标准安装任务，不含恶意软件、广告软件或不需要的软件。Servy 通过 VirusTotal 扫描，并发布于 Windows Package Manager（WinGet）、Chocolatey、Scoop 以及 Patch My PC 企业目录。你可安全地从 GitHub、WinGet、Chocolatey、Scoop 或 Patch My PC 安装 Servy。

为确保 Servy Windows 服务顺畅运行，建议将以下文件夹加入 Microsoft Defender 或第三方杀毒软件的排除列表：
```text
%ProgramData%\Servy
%ProgramFiles%\Servy
```

这可防止杀毒软件干扰由 Servy 管理的服务二进制文件的执行。

从 v9.1 起，Servy 采用混合单文件执行方式。托管 C# 程序集（如 `Servy.CLI.dll`、`Servy.dll`、`Servy.Manager.dll`、`Servy.Service.dll` 和 `Servy.Restarter.dll`）**不再解压**到 `%TEMP%\.net`。而是从主可执行文件（`servy-cli.exe`、`Servy.Manager.exe` 等）直接加载到内存中，**这些文件均已完整 Authenticode 签名**。

Servy **仅**将非托管原生 C/C++ 二进制文件解压到 `%TEMP%`（Windows `LoadLibrary` 所必需）。这可防止安全软件拦截并标记动态解压的 DLL。
