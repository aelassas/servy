## 目录

1. [开始使用](#开始使用)
1. [快速示例](#快速示例)
1. [桌面应用](#桌面应用)
   1. [服务详细信息](#服务详细信息)
   1. [日志](#日志)
   1. [恢复](#恢复)
   1. [高级](#高级)
   1. [登录](#登录)
   1. [启动前](#启动前)
   1. [启动后](#启动后)
   1. [停止前](#停止前)
   1. [停止后](#停止后)
1. [Manager 应用](#manager-应用)
   1. [服务](#服务)
   1. [性能](#性能)
   1. [控制台](#控制台)
   1. [依赖](#依赖)
   1. [日志](#日志-1)
1. [CLI / PowerShell](#cli--powershell)
   1. [CLI](#cli)
   1. [PowerShell](#powershell)
1. [另请参阅](#另请参阅)

> [!IMPORTANT]
> **控制台 UI 兼容性**
> 如果应用试图以视觉方式更新命令提示符（例如清屏或移动光标），作为后台服务运行时会崩溃，因为服务没有可见窗口。无需改代码即可修复：在安装服务时，在服务配置中启用 **Enable Console UI** 选项。

## 开始使用

Servy 可将任意应用运行为原生 Windows 服务，并全面掌控启动、环境、日志与生命周期管理。

你可以通过桌面应用（GUI）、CLI（`servy-cli`）或 PowerShell 管理服务。

> [!IMPORTANT]
> **桌面交互与 Session 0 隔离**
>
> Servy 可将任意可执行文件包装为原生 Windows 服务，包括不需要交互式桌面会话的 GUI 应用。与所有 Windows 服务包装器（包括 NSSM 和 WinSW）一样，Servy 进程在 **Session 0** 中执行，该会话处于隔离环境，无法访问交互式桌面。
>
> 若尝试将需要交互式桌面的 GUI 应用作为服务运行，它将无法启动或在运行时崩溃，常抛出 Access Violation（`0xC0000005`）或空指针异常等错误。若应用需要活动的桌面会话，请改用其他自动化工具（例如配置为仅在用户登录时运行的 **Windows Task Scheduler**）。

> [!NOTE]
> **开始之前**
>
> 安装和管理 Windows 服务需要管理员权限。若使用现代 Windows 工作站或服务器，请选择 .NET 10.0+ 版本。若需支持 Windows 7 SP1 或 Windows Server 2008 R2 等旧平台，请改用 .NET Framework 4.8 版本。

要开始使用，请从 [GitHub](https://github.com/aelassas/servy/releases/latest) 下载最新版本，或通过包管理器安装：

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

默认安装后，`servy-cli` 会加入系统 **PATH**——相关选项与便携包说明见[安装指南](./Installation-Guide#add-servy-to-path)。

## 快速示例

你可以使用[桌面应用（GUI）](./Servy-Desktop-App)、[CLI](./Servy-CLI) 或 [PowerShell](./Servy-PowerShell-Module) 管理服务。

以下是用 CLI 将 Node.js 应用运行为 Windows 服务的最小示例。请在提升权限的 PowerShell 中运行：安装、启动和停止 Windows 服务均需要管理员权限。

```powershell
servy-cli install `
  --name="MyService" `
  --path="C:\Program Files\nodejs\node.exe" `
  --startupDir="C:\MyServer" `
  --params="server.js" `
  --enableHealth
```

这将创建一个名为 `MyService` 的服务，在后台运行你的 Node.js 服务器，随 Windows 自动启动，并启用[健康监控](./Health-Monitoring-&-Recovery)。

然后启动服务：

```powershell
servy-cli start --name="MyService"
```

或从**提升权限**的命令提示符：

```cmd
sc.exe start MyService
```

查看更多 Python、Java、Go 及其他流行框架的[示例与配方](./Examples-&-Recipes)。

## 桌面应用

[Servy 桌面应用](./Servy-Desktop-App) 提供直观的图形界面，用于配置、安装和管理单个 Windows 服务。

### 服务详细信息

配置核心服务属性，包括名称、描述、可执行文件路径、参数、工作目录和启动类型。参见 [Servy 桌面应用](./Servy-Desktop-App#service-details)。

<img alt="servy-config-main" src="https://github.com/user-attachments/assets/ada585ff-1f6a-487f-ac37-4779137dd6b4" />

有关 CPU 亲和性的更多说明，见[常见问题](./FAQ#how-and-why-should-i-use-cpu-affinity-with-servy)。

### 日志

配置 `stdout` 与 `stderr` 日志文件重定向、按大小与按日期的日志轮转，以及保留策略。参见[日志与日志轮转](./Logging-&-Log-Rotation)。

<img alt="servy-config-logging" src="https://github.com/user-attachments/assets/b79978ec-1740-464d-b9c7-57b2151cc853" />

> [!NOTE]
> 启用调试选项会将敏感信息写入本地日志文件 `%ProgramData%\Servy\logs\Servy.Service.log`。仅在启用本地日志（默认开启）时发生。为保障安全，敏感数据绝不会写入 Windows 事件日志，也不会在 CLI 与 PowerShell 模块中显示。

有关日志的详细信息，请参阅[日志与日志轮转](./Logging-&-Log-Rotation)文档。

### 恢复

配置自动存活检查、外部诊断心跳 ping，以及自动重启行为。参见[健康监控与恢复](./Health-Monitoring-&-Recovery)。

<img alt="servy-config-recovery" src="https://github.com/user-attachments/assets/84c1efba-096c-4573-88e6-5ca5b4c61a4c" />

有关健康监控与恢复的详细信息，请参阅[健康监控与恢复](./Health-Monitoring-&-Recovery)文档。

### 高级

设置自定义进程环境变量与 Windows 服务依赖。参见[环境变量](./Environment-Variables)和[服务依赖](./Service-Dependencies)。

<img alt="servy-config-advanced" src="https://github.com/user-attachments/assets/75aaf4e2-8b6d-4d9f-a3e3-5534dd466961" />

有关环境变量的详细信息，请参阅[环境变量](./Environment-Variables)文档。

有关服务依赖的详细信息，请参阅[服务依赖](./Service-Dependencies)文档。

### 登录

配置服务运行账户，包括 `LocalSystem`、内置服务账户、域账户和 gMSA。参见[安全性](./Security)。

<img alt="servy-config-logon" src="https://github.com/user-attachments/assets/e1d8e4cd-dcbb-4126-8928-a0428566a6dc" />

也可以在以下账户下运行服务：

* `NT AUTHORITY\NetworkService`
* `NT AUTHORITY\LocalService`
* 无密码账户

<img alt="servy-config-logon-builtin-accounts" src="https://github.com/user-attachments/assets/5681b397-6f38-4a19-9754-c5999e670fd7" />

> [!IMPORTANT]
> 若服务在非 Local System 账户下运行，必须为该服务账户授予 `%ProgramData%\Servy` 的 **Modify** 权限，并运行强制加固脚本（`Set-ServyExePermissions.ps1 -TargetAccount "domain\user"`），将二进制可执行文件锁定为 **Read & Execute**，以防止未授权篡改二进制文件与本地权限提升。脚本位置与执行说明见[可执行文件权限加固指南](./Security#executable-permission-hardening-mandatory)。

### 启动前

在主服务进程启动前执行自定义初始化脚本或可执行文件。参见[启动前与启动后操作](./Pre-Launch-&-Post-Launch-Actions)。

<img alt="servy-config-pre-launch" src="https://github.com/user-attachments/assets/74dc7d88-60cf-453d-b070-9bf270a42e77" />

有关启动前钩子的详细信息，请参阅[启动前与启动后操作](./Pre-Launch-&-Post-Launch-Actions)文档。

### 启动后

主进程成功启动后立即运行辅助任务。参见[启动前与启动后操作](./Pre-Launch-&-Post-Launch-Actions)。

<img alt="servy-config-post-launch" src="https://github.com/user-attachments/assets/8877dca7-c89f-4285-b5f4-b6ccab17e538" />

有关启动后钩子的详细信息，请参阅[启动前与启动后操作](./Pre-Launch-&-Post-Launch-Actions)文档。

### 停止前

在停止主服务之前运行优雅关闭任务或资源排空脚本。参见[停止前与停止后操作](./Pre-Stop-&-Post-Stop-Actions)。

<img alt="servy-config-pre-stop" src="https://github.com/user-attachments/assets/8b69bf5b-1e74-4d5d-b64f-55635e3cb798" />

有关停止前钩子的详细信息，请参阅[停止前与停止后操作](./Pre-Stop-&-Post-Stop-Actions)文档。

### 停止后

在主服务及所有子进程完全退出后执行事后清理任务。参见[停止前与停止后操作](./Pre-Stop-&-Post-Stop-Actions)。

<img alt="servy-config-post-stop" src="https://github.com/user-attachments/assets/43eb220c-f38a-41f7-ba36-cba9661a064c" />

有关停止后钩子的详细信息，请参阅[停止前与停止后操作](./Pre-Stop-&-Post-Stop-Actions)文档。

## Manager 应用

[Servy Manager](./Servy-Manager) 是用于管理系统上所有已安装服务的集中管理界面，支持管理、监控与检查。

### 服务

查看所有已注册服务，检查实时 CPU/内存指标，并触发生命周期操作。参见 [Servy Manager](./Servy-Manager#services)。

<img alt="servy-manager-services" src="https://github.com/user-attachments/assets/bed472ea-35b5-4b78-9f19-3377f2a73342" />

### 性能

通过实时可视化性能图表跟踪 CPU 与内存利用率。参见 [Servy Manager](./Servy-Manager#performance)。

<img alt="servy-manager-performance" src="https://github.com/user-attachments/assets/82ad2034-eb74-49fd-aabd-f68e9bac7394" />

### 控制台

实时流式输出统一的 `stdout` 与 `stderr` 控制台内容，并支持实时过滤与尾随（tail）控制。参见 [Servy Manager](./Servy-Manager#console)。

<img alt="servy-manager-console" src="https://github.com/user-attachments/assets/8734acde-b59c-478c-b4af-797ea99d5884" />

### 依赖

检查完整的 Windows Service Control Manager（SCM）依赖树，并以颜色标识状态。参见 [Servy Manager](./Servy-Manager#dependencies)。

<img alt="servy-manager-dependencies" src="https://github.com/user-attachments/assets/9d69b45c-4059-4dd1-86f8-a9a5f9a35427" />

### 日志

直接在应用查看器中检查 Windows 事件日志条目。参见 [Servy Manager](./Servy-Manager#logs)。

<img alt="servy-manager-logs" src="https://github.com/user-attachments/assets/53eba82a-a879-4aa6-8af8-68928fa5aa5a" />

## CLI / PowerShell

Servy 包含命令行接口（`servy-cli`）和 [PowerShell 模块](./Servy-PowerShell-Module)，支持完整自动化与 CI/CD 集成。

### CLI

通过命令行参数或配置文件直接执行服务操作。参见 [Servy CLI](./Servy-CLI)。

<img alt="servy-cli" src="https://github.com/user-attachments/assets/b47127dd-8e83-4920-acbf-d5bc64ca6f12" />

### PowerShell

使用 PowerShell cmdlet 与管道对象以原生方式管理服务。参见 [Servy PowerShell 模块](./Servy-PowerShell-Module)。

<img alt="servy-powershell" src="https://github.com/user-attachments/assets/7c3684ee-7337-4967-bf70-d3056a54d8c7" />

## 另请参阅
* [安装指南](./Installation-Guide)
* [使用说明](./Usage)
* [Servy 桌面应用](./Servy-Desktop-App)
* [Servy Manager](./Servy-Manager)
* [Servy CLI](./Servy-CLI)
* [Servy PowerShell 模块](./Servy-PowerShell-Module)
* [示例与配方](./Examples-&-Recipes)
* [导出/导入服务](./Export-Import-Services)
* [服务事件通知](./Service-Event-Notifications)
