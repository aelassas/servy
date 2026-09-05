## 目录
1. [简介](#introduction)
1. [安装选项](#installation-options)
   1. [手动下载与静默安装](#manual-download--silent-install)
   1. [包管理器安装](#package-manager-installation)
1. [CLI 用法](#cli-usage)
1. [Jenkins 集成](#jenkins-integration)
1. [TeamCity 集成](#teamcity-integration)
1. [GitHub Actions 集成](#github-actions-integration)
1. [Azure DevOps 集成](#azure-devops-integration)
1. [自动化最佳实践](#automation-best-practices)
1. [参考](#references)

## Introduction

Servy 专为可编程管理而设计，非常适合自动化环境和 CI/CD 流水线。

## Installation Options

### Manual Download & Silent Install
**.NET 10.0+ 版本（x64）**
```text
https://github.com/aelassas/servy/releases/download/v<version>/servy-<version>-x64-installer.exe
```
**.NET 10.0+ 版本（ARM64）**
```text
https://github.com/aelassas/servy/releases/download/v<version>/servy-<version>-arm64-installer.exe
```
**.NET Framework 4.8 版本（x64）**
```text
https://github.com/aelassas/servy/releases/download/v<version>/servy-<version>-net48-x64-installer.exe
```

**静默安装命令：**
```cmd
.\servy-<version>-x64-installer.exe /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL
.\servy-<version>-arm64-installer.exe /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL
.\servy-<version>-net48-x64-installer.exe /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL
```

**静默安装命令（仅 CLI）：**
```cmd
.\servy-<version>-x64-installer.exe /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL /SetupType=custom /Components=install_cli
.\servy-<version>-arm64-installer.exe /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL /SetupType=custom /Components=install_cli
.\servy-<version>-net48-x64-installer.exe /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL /SetupType=custom /Components=install_cli
```

更多详情请参阅 [安装指南](./Installation-Guide)。

### Package Manager Installation
- **WinGet**：
  ```powershell
  winget install --id aelassas.Servy -e --accept-package-agreements --accept-source-agreements --silent
  ```
- **Chocolatey**：
  ```powershell
  choco install -y servy
  ```
- **Scoop**：
  ```powershell
  scoop bucket add extras
  scoop install servy
  ```

- **Patch My PC**：

  Servy 可在官方 [Patch My PC 目录](https://patchmypc.com/supported-products/) 中获取，用于通过 Microsoft Intune 和 ConfigMgr（SCCM）进行企业自动化部署与更新。

> [!NOTE]
> 对于无人值守的自动化环境和 CI/CD 流水线，WinGet 命令必须显式包含 `--accept-package-agreements --accept-source-agreements --silent`，以防止交互式提示卡住；同时使用 `--id aelassas.Servy -e` 按精确标识固定包，而不是进行模糊搜索。

安装完成后，CLI 可执行文件位于：
```text
%ProgramFiles%\Servy\servy-cli.exe
```

PowerShell 模块位于：
```text
%ProgramFiles%\Servy\Servy.psm1
```

默认安装后，`servy-cli` 已在系统 **PATH** 中——参见 [安装指南](./Installation-Guide#add-servy-to-path) 了解控制此项的选项以及便携包说明。

## CLI Usage

Servy CLI 可以编程方式管理服务。典型命令：
```powershell
# Install or update a service
servy-cli install --name="MyApp" --path="C:\MyApp\MyApp.exe" --startupType="Automatic"

# Start a service
servy-cli start --name="MyApp"

# Get a service status
servy-cli status --name="MyApp"

# Stop a service
servy-cli stop --name="MyApp"

# Uninstall a service
servy-cli uninstall --name="MyApp"
```

**说明：**
- 所有命令返回适合 CI/CD 流水线检查的退出码。
- 有关幂等性、重启规则和安全指南，请参阅 [自动化最佳实践](#automation-best-practices)。
- 完整 CLI 文档见 [Servy CLI](./Servy-CLI) 参考。
- 完整 PowerShell 模块文档见 [Servy PowerShell 模块](./Servy-PowerShell-Module) 参考。

## Jenkins Integration

1. 在 Jenkins Windows 代理上安装 Servy CLI。
2. 添加构建步骤（执行 Windows 批处理命令或 PowerShell）：
```powershell
# Install or update service
servy-cli install --name="MyApp" --path="C:\MyApp\MyApp.exe" --startupType="Automatic"

# Start the service
servy-cli start --name="MyApp"
```

3. 使用退出码在 Servy 命令失败时使构建失败。

## TeamCity Integration

1. 在 TeamCity 代理上安装 Servy CLI。
2. 添加命令行构建步骤：
```powershell
servy-cli install --name="MyApp" --path="C:\TeamCity\Builds\MyApp.exe" --startupType="Automatic"
servy-cli start --name="MyApp"
```
3. 监控退出码以验证成功。

## GitHub Actions Integration

Windows runner 的示例步骤：
```yaml
name: Install MyApp with Servy

on:
  workflow_dispatch:

jobs:
  test-servy:
    runs-on: windows-latest

    steps:
      - name: Install Servy via WinGet
        run: |
          winget install --id aelassas.Servy -e --accept-package-agreements --accept-source-agreements --silent
        shell: powershell

      - name: Verify Servy CLI installed
        run: |
          & "C:\Program Files\Servy\servy-cli.exe" --version --quiet
        shell: powershell

      - name: Install MyApp as a Windows Service
        run: |
          & "C:\Program Files\Servy\servy-cli.exe" install `
            --name="MyApp" `
            --path="C:\MyApp\MyApp.exe" `
            --startupType="Automatic" `
            --startupDir="C:\MyApp"
        shell: powershell

      - name: Start MyApp service
        run: |
          & "C:\Program Files\Servy\servy-cli.exe" start --name="MyApp"
        shell: powershell

      - name: Verify service status
        run: |
          Get-Service -Name MyApp
        shell: powershell
```

## Azure DevOps Integration

1. 向流水线添加 Windows 代理。
2. 添加 PowerShell 任务：
```powershell
servy-cli install --name="MyApp" --path="$(Build.ArtifactStagingDirectory)\MyApp.exe" --startupType="Automatic"
servy-cli start --name="MyApp"
```

3. 使用任务结果码在出错时使流水线失败。

## Automation Best Practices

- **幂等性：** 脚本中始终使用 `install` 命令。它可安全处理全新安装和对现有服务的更新。
- **需要重启：** 配置更新生效需要重启服务。
- **安全最佳实践：** 避免在生产环境或脚本中使用敏感标志（例如 `--password`、`--params`、`--envVars`、`--preLaunchEnv`）。将这些值作为命令行参数传递会使其对能访问 Windows 进程列表或 shell 历史文件的任何用户或进程可见。应改为在运行 install 命令前设置相应的环境变量（例如 `SERVY_PASSWORD`、`SERVY_PROCESS_PARAMETERS`、`SERVY_ENVIRONMENT_VARIABLES`）。更多信息见 [安全](./Security#6-sensitive-command-line-arguments--service-account-credentials) 页面。
- **健康检查：** 在流水线中使用 `servy-cli status --name="MyApp"` 验证部署后服务是否达到“Running”状态。
- **退出码：** Servy CLI 返回标准退出码（成功为 `0`，失败为非零）。确保您的 CI/CD 平台配置为在非零返回时使构建失败。
- **关机处理：** 对于使用临时 runner 或自动扩缩组的 CI/CD 环境，请确保服务使用 **Servy v6.2+** 安装，以利用 `SERVICE_CONTROL_PRESHUTDOWN` 实现优雅拆除。
- **引用参数：** 在 PowerShell 中，双引号字符串会在 CLI 接收之前展开 `$variable` 和 `$(...)`。当需要 CLI 接收字面量 `$`/`` ` ``/`"` 字符时，请使用单引号（`'...'`）——例如：`--params='Set-Item Env:Foo "$bar"'`。当确实需要 PowerShell 替换时使用双引号，例如 `--path="$env:ProgramFiles\MyApp\app.exe"`。

## References

- [Servy Releases](https://github.com/aelassas/servy/releases)
- [Servy CLI](./Servy-CLI)
- [Servy PowerShell 模块](./Servy-PowerShell-Module)
