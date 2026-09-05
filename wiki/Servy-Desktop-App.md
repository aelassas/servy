## 目录

1. [简介](#简介)
1. [概览](#概览)
   1. [服务详细信息](#服务详细信息)
   1. [日志](#日志)
   1. [恢复](#恢复)
   1. [高级](#高级)
   1. [登录](#登录)
   1. [启动前](#启动前)
   1. [启动后](#启动后)
   1. [停止前](#停止前)
   1. [停止后](#停止后)
1. [功能](#功能)
1. [可用性](#可用性)
1. [使用方法](#使用方法)
1. [另请参阅](#另请参阅)

> [!IMPORTANT]
> **控制台 UI 兼容性**
> 如果应用试图以视觉方式更新命令提示符（例如清屏或移动光标），作为后台服务运行时会崩溃，因为服务没有可见窗口。无需改代码即可修复：在安装服务时，在服务配置中启用 **Enable Console UI** 选项。

## 简介

Servy 是 Windows 服务包装器，可将任意可执行文件、脚本或批处理文件转换为后台服务。它弥合了简单控制台应用与原生 Windows 服务所期望的企业级管理之间的差距。

Servy 提供直观界面，可将任意应用运行为原生 Windows 服务，并提供配置与管理选项。使用 Servy，可确保应用在系统启动时自动启动、在意外崩溃后重启，并在无需修改任何应用代码的情况下保留详细日志。

无论托管 Web 服务器、后台工作进程还是数据库，Servy 桌面应用都通过清晰的选项卡式界面简化部署与维护。

> [!NOTE]
> 若在远程管理工具（如 MeshCentral、TeamViewer 或 AnyDesk）中出现空白屏幕，请从管理员命令提示符使用以下命令运行桌面应用：`Servy.exe --force-sr`

## 概览

以下各节说明桌面应用的关键组件，展示如何自定义服务生命周期的各个方面。

### 服务详细信息

此选项卡用于配置主要服务属性，如名称、描述、可执行文件路径、参数、启动目录和启动类型。

<img alt="servy-config-main" src="https://github.com/user-attachments/assets/ada585ff-1f6a-487f-ac37-4779137dd6b4" />

有关 CPU 亲和性的更多说明，见[常见问题](./FAQ#how-and-why-should-i-use-cpu-affinity-with-servy)。

### 日志

此选项卡用于配置 `stdout` 与 `stderr` 日志，包括按大小轮转、按日期轮转、保留的轮转日志文件最大数量，以及启用调试日志等附加选项。

<img alt="servy-config-logging" src="https://github.com/user-attachments/assets/b79978ec-1740-464d-b9c7-57b2151cc853" />

> [!NOTE]
> 启用调试选项会将敏感信息写入本地日志文件 `%ProgramData%\Servy\logs\Servy.Service.log`。仅在启用本地日志（默认开启）时发生。为保障安全，敏感数据绝不会写入 Windows 事件日志，也不会在 CLI 与 PowerShell 模块中显示。

有关日志的详细信息，请参阅[日志与日志轮转](./Logging-&-Log-Rotation)文档。

### 恢复

此选项卡用于配置自动健康监控与故障处理行为。它支持持续的内部存活检查、向外部监控平台的可选带外诊断 ping（例如 [healthchecks.io](https://healthchecks.io) 或 [Uptime Kuma](https://github.com/louislam/uptime-kuma) Push Monitors），以及可自定义的恢复操作，从而在无需人工干预的情况下可靠保持应用运行。

<img alt="servy-config-recovery" src="https://github.com/user-attachments/assets/84c1efba-096c-4573-88e6-5ca5b4c61a4c" />

有关健康监控与恢复的详细信息，请参阅[健康监控与恢复](./Health-Monitoring-&-Recovery)文档。

### 高级

高级选项卡提供环境变量和服务依赖等附加配置选项。

<img alt="servy-config-advanced" src="https://github.com/user-attachments/assets/75aaf4e2-8b6d-4d9f-a3e3-5534dd466961" />

有关环境变量的详细信息，请参阅[环境变量](./Environment-Variables)文档。

有关服务依赖的详细信息，请参阅[服务依赖](./Service-Dependencies)文档。

### 登录

此选项卡用于配置服务账户，支持本地账户、域账户和 gMSA 账户。

Servy 使用 AES 安全加密存储的密码。更多细节见[安全性](./Security)页面。

<img alt="servy-config-logon" src="https://github.com/user-attachments/assets/e1d8e4cd-dcbb-4126-8928-a0428566a6dc" />

也可以在以下账户下运行服务：

- `NT AUTHORITY\NetworkService`
- `NT AUTHORITY\LocalService`
- 无密码账户

<img alt="servy-config-logon-builtin-accounts" src="https://github.com/user-attachments/assets/5681b397-6f38-4a19-9754-c5999e670fd7" />

> [!IMPORTANT]
> 若服务在非 Local System 账户下运行，必须为该服务账户授予 `%ProgramData%\Servy` 的 **Modify** 权限，并运行强制加固脚本（`Set-ServyExePermissions.ps1 -TargetAccount "domain\user"`），将二进制可执行文件锁定为 **Read & Execute**，以防止未授权篡改二进制文件与本地权限提升。脚本位置与执行说明见[可执行文件权限加固指南](./Security#executable-permission-hardening-mandatory)。

### 启动前

配置可选的启动前程序，在主服务进程启动前运行。可用于准备环境、设置依赖或运行初始化脚本。默认情况下，启动前钩子以同步方式运行并带有超时。若启动前脚本以非零退出码退出或超时，除非启用 **Ignore Failure** 选项，否则服务将无法启动。

将超时设为 0 可在 fire-and-forget 模式下运行启动前钩子。设为 0 时，钩子会启动，服务会立即启动而不等待完成。仅用于不影响服务启动或正常运行的任务。fire-and-forget 模式下不可用 `stdout`/`stderr` 重定向与重试。

孤立的 fire-and-forget 启动前钩子会在服务停止时清理。

<img alt="servy-config-pre-launch" src="https://github.com/user-attachments/assets/74dc7d88-60cf-453d-b070-9bf270a42e77" />

有关启动前钩子的详细信息，请参阅[启动前与启动后操作](./Pre-Launch-&-Post-Launch-Actions)文档。

### 启动后

配置可选的启动后程序，在进程成功启动后运行。

<img alt="servy-config-post-launch" src="https://github.com/user-attachments/assets/8877dca7-c89f-4285-b5f4-b6ccab17e538" />

孤立的启动后钩子会在服务停止时清理。

有关启动后钩子的详细信息，请参阅[启动前与启动后操作](./Pre-Launch-&-Post-Launch-Actions)文档。

### 停止前

配置可选的脚本或可执行文件，在主服务停止前运行。可用于优雅关闭任务，例如通知外部系统或排空资源。停止前进程以同步方式运行，并在运行期间延长服务停止超时。将超时设为 0 可在 fire-and-forget 模式下运行停止前进程。

<img alt="servy-config-pre-stop" src="https://github.com/user-attachments/assets/8b69bf5b-1e74-4d5d-b64f-55635e3cb798" />

有关停止前钩子的详细信息，请参阅[停止前与停止后操作](./Pre-Stop-&-Post-Stop-Actions)文档。

### 停止后

配置可选的脚本或可执行文件，在被包装进程及其所有子进程退出后运行。停止后进程以 fire-and-forget 模式启动，不会阻塞服务关闭。

<img alt="servy-config-post-stop" src="https://github.com/user-attachments/assets/43eb220c-f38a-41f7-ba36-cba9661a064c" />

有关停止后钩子的详细信息，请参阅[停止前与停止后操作](./Pre-Stop-&-Post-Stop-Actions)文档。

## 功能

- **运行任意内容：** 将可执行文件、脚本或批处理文件包装为原生 Windows 服务。
- **智能恢复：** 根据自定义健康监控规则自动重启服务或整台主机。
- **灵活的生命周期钩子：** 在每个阶段（启动前、启动后、停止前、停止后）执行自定义代码。
- **日志轮转：** 按大小或日期轮转日志，并在 Manager 控制台中实时尾随。
- **安全凭据：** 使用行业标准 AES 加密存储服务账户密码。
- **实时监控：** 集成 CPU 与内存性能跟踪。
- **依赖映射：** 可视化依赖树，便于排查启动顺序。

## 可用性

Servy 桌面应用提供多个版本，覆盖现代与较旧系统：

- **.NET 10.0+（推荐）**
  - 自包含安装程序
  - 无需预先安装任何 .NET 运行时

- **.NET Framework 4.8（适用于较旧系统）**
  - 标准安装包
  - 需要 .NET Framework 4.8 Runtime

## 使用方法

1. **启动：** 运行 Servy 桌面应用（`Servy.exe`）。
1. **配置：** 在“Service Details”（Main）选项卡中输入应用路径与服务详细信息。
1. **选项：** 在相应选项卡中自定义环境变量、日志偏好与恢复操作。
1. **安装：** 点击“Install”按钮，向 Windows Service Control Manager（SCM）注册服务。
1. **管理：** 使用 Servy Manager 监控新服务的性能并浏览日志。

## 另请参阅
- [概览](./Overview)
- [使用说明](./Usage)
- [Servy Manager](./Servy-Manager)
- [Servy CLI](./Servy-CLI)
- [Servy PowerShell 模块](./Servy-PowerShell-Module)
