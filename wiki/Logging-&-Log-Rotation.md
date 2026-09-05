## 目录

1. [简介](#简介)
1. [Servy Manager 中的日志](#servy-manager-中的日志)
1. [事件 ID](#事件-id)
1. [日志设置](#日志设置)
1. [CLI 选项](#cli-选项)
1. [CLI 示例](#cli-示例)
1. [PowerShell 选项](#powershell-选项)
1. [PowerShell 示例](#powershell-示例)
1. [Servy 内部日志](#servy-内部日志)

## 简介

Servy 提供灵活的日志功能，用于实时监控服务，将重要事件记录到 Windows 事件日志、`%ProgramData%\Servy\logs\` 下的本地日志文件，以及标准输出与错误流（`stdout`/`stderr`）。可在 Servy Manager 中查看日志，按级别、日期或关键字过滤，并可选择轮转日志以避免磁盘空间问题。

## Servy Manager 中的日志

<img alt="servy-manager-logs" src="https://github.com/user-attachments/assets/53eba82a-a879-4aa6-8af8-68928fa5aa5a" />

- 重要事件会记录到 **Windows 事件查看器**，并可从 **Servy Manager** 浏览。
- **Logs 选项卡**：可按日志级别、日期和关键字快速搜索日志，便于监控与排查。
- **消息格式**：每条日志以 `[ServiceName]` 开头，后接消息内容。
  - 示例：`[MyService] Health monitoring started.`

## 事件 ID

Servy 为不同类型的服务日志分配特定的 **Event ID**。这些 ID 记录在 **Windows 事件查看器** 中，便于过滤、搜索与自动化。

| 级别 | Event ID 范围 | 用途 |
|-------|----------------|---------|
| Info | 1000-1099 | 生命周期里程碑（启动、停止、恢复成功） |
| Warning | 2000-2099 | 可恢复的降级（重试、回退路径） |
| Error | 3000-3099 | 核心错误（DPAPI 失败、数据库初始化失败等） |
| Error | 3100-3199 | 脚本 / 计划任务错误（`ServyFailureEmail.ps1` 等） |

- 2002 = 临时迁移警告（升级前；下次成功读取后自动清除）
- 3001 = 密钥解除保护失败
- 3003 = 持久迁移失败
- 3103 = 计划任务脚本错误
- 3104 = 计划任务脚本依赖错误

> [!NOTE]
> Event ID 按事件类型分配，并非每条日志条目唯一。

## 日志设置

桌面应用中的 “Logging” 选项卡可配置 `stdout` 与 `stderr` 日志，包括基于大小的日志轮转、基于日期的日志轮转、保留的已轮转日志文件最大数量，以及启用调试日志等附加选项。

<img alt="servy-config-logging" src="https://github.com/user-attachments/assets/b79978ec-1740-464d-b9c7-57b2151cc853" />

> [!NOTE]
> 启用调试选项会将敏感信息记录到本地日志文件 `%ProgramData%\Servy\logs\Servy.Service.log`。此行为仅在启用本地日志时发生（默认启用）。为保障安全，敏感数据绝不会写入 Windows 事件日志，也不会在 CLI 和 PowerShell 模块中显示。

> [!NOTE]
> 启用 **Console UI**（`--enableConsoleUI`）会在操作系统层面禁用 stdout/stderr 重定向——子进程保留附加的控制台句柄，而不是管道。该模式下 Servy 无法捕获或轮转输出，因此配置的 `--stdout` / `--stderr` 路径将保持为空。两者择一使用，不要同时启用。

## CLI 选项

- `--stdout` - stdout 日志路径
- `--stderr` - stderr 日志路径
- `--enableSizeRotation`
- `--rotationSize` - 大小（MB）
- `--enableDateRotation`
- `--dateRotationType` - Daily、Weekly、Monthly、None（None 禁用基于日期的轮转；仅需大小轮转时使用）
- `--useLocalTimeForRotation` - 使用本地时间而非 UTC（默认：false）
- `--maxRotations` - 文件数量（0 = 无限制）
- `--debug` - 启用调试日志。会将环境变量和进程参数记录到 `%ProgramData%\Servy\logs\Servy.Service.log`。不建议用于生产环境。

## CLI 示例

```powershell
servy-cli install `
  --name="My NodeJS Service" `
  --description="My NodeJS Server" `
  --path="C:\Program Files\nodejs\node.exe" `
  --startupDir="C:\Apps\App" `
  --params="C:\Apps\App\index.js" `
  --startupType="Automatic" `
  --priority="Normal" `
  --stdout="C:\Apps\App\stdout.log" `
  --stderr="C:\Apps\App\stderr.log" `
  --enableSizeRotation `
  --rotationSize="10" `
  --maxRotations="5" `
  --debug
```

## PowerShell 选项

- `-Stdout`（string）
- `-Stderr`（string）
- `-EnableSizeRotation`（switch）
- `-RotationSize`（int）
- `-EnableDateRotation`（switch）
- `-DateRotationType`（Daily、Weekly、Monthly、None — None 禁用基于日期的轮转；仅需大小轮转时使用）
- `-UseLocalTimeForRotation`（switch）
- `-MaxRotations`（int，0 = 无限制）
- `-EnableDebugLogs` - 启用调试日志。会将环境变量和进程参数记录到 `%ProgramData%\Servy\logs\Servy.Service.log`。不建议用于生产环境。

## PowerShell 示例

```powershell
Import-Module "C:\Program Files\Servy\Servy.psm1" -Force

$installParams = @{
    Quiet              = $true
    Name               = "My NodeJS Service"
    Description        = "My NodeJS Server"
    Path               = "C:\Program Files\nodejs\node.exe"
    StartupDir         = "C:\Apps\App"
    Params             = "C:\Apps\App\index.js"
    StartupType        = "Automatic"
    Priority           = "Normal"

    Stdout             = "C:\Apps\App\stdout.log"
    Stderr             = "C:\Apps\App\stderr.log"
    EnableSizeRotation = $true
    RotationSize       = 10
    MaxRotations       = 5
    EnableDebugLogs    = $true
}

Install-ServyService @installParams
```

## Servy 内部日志

参见 [高级配置](./Advanced-Configuration)。

> [!TIP]
> 可将日志轮转与健康监控、启动前脚本结合使用，以避免磁盘空间问题。
