## 目录

1. [简介](#简介)
   1. [兼容性与运行时要求](#兼容性与运行时要求)
1. [安装](#安装)
1. [用法示例](#用法示例)
   1. [为何使用参数溅射？](#为何使用参数溅射)
   1. [安装新服务](#安装新服务)
   1. [导出服务配置](#导出服务配置)
   1. [导入服务配置](#导入服务配置)
   1. [启动、停止、重启与查看状态](#启动停止重启与查看状态)
   1. [卸载服务](#卸载服务)
1. [Cmdlet 参考](#cmdlet-参考)
   1. [Install-ServyService 参数参考](#install-servyservice-参数参考)
1. [故障排除](#故障排除)
1. [另见](#另见)
1. [参考](#参考)

> [!IMPORTANT]
> **控制台 UI 兼容性**
> 若应用尝试在视觉上更新命令提示符（如清屏或移动光标），在作为后台服务运行时会崩溃，因为服务没有可见窗口。无需修改代码即可修复：在安装服务时于服务配置中启用 `-EnableConsoleUI` 选项。

## 简介

Servy PowerShell 模块提供轻量、可脚本化的后台服务管理接口。设计高度可移植，便于系统管理员在多样化的 Windows 环境中自动化部署。

### 兼容性与运行时要求

PowerShell 模块（`Servy.psm1`）编写为兼容 **PowerShell 2.0 及更高版本**。但其在旧操作系统上的运行能力取决于存在哪个版本的 Servy CLI：

* **Windows 10 / 11 / Server 2016+**：使用 **现代 CLI（.NET 10.0+）**。该版本性能最佳，并利用最新 Windows 安全功能。
* **Windows 7 SP1 / 8 / Server 2008 R2**：使用 **旧版 CLI（.NET Framework 4.8）**。与此构建搭配时，PowerShell 模块在旧发行版上功能完整。

> [!NOTE]
> `taskschd/` 下附带的任务计划程序钩子
> （`ServyFailureEmail.ps1`、`ServyFailureNotification.ps1`、`Get-ServyLastErrors.ps1`、
> `Servy-Watermark.psm1`）有更严格的要求：
> - 基于 `Get-WinEvent` 的脚本需要 **PowerShell 5.1+**（Windows 7 SP1 / Server 2008 R2 SP1+）。
> - Toast 通知需要 **PowerShell 5.1+**（Windows 10 1607+）。
> 仅 `Servy.psm1` 本身完全兼容 PS 2.0。

## 安装

在 PowerShell 会话中导入模块：

```powershell
Import-Module "C:\Program Files\Servy\Servy.psm1" -Force
```

显示版本：

```powershell
Get-ServyVersion
```

显示帮助：

```powershell
# 查看模块常规帮助与可用命令
Get-ServyHelp

# 获取其他受支持命令的帮助
Get-ServyHelp -Command "install"
Get-ServyHelp -Command "start"
Get-ServyHelp -Command "stop"
```

## 用法示例

**专业提示：** 在 PowerShell 中，开关参数（如 `-Quiet`、`-Install` 或 `-EnableHealth`）是切换标志。
**内联**调用命令时，不要传递 `$true` 或 `$false`；包含该标志即启用，省略则保持禁用。使用**参数溅射**时，在溅射哈希表中将开关参数设为 `$true` 是正确且惯用的做法。

### 为何使用参数溅射？

参数溅射使 PowerShell 命令更易阅读、维护与扩展。

不必使用带有许多参数的长命令行，而是将选项归组到单个哈希表中，清晰展示意图与默认值。这对 Servy 命令尤其有用，因其支持许多可选参数以及钩子、日志与恢复配置等高级场景。

溅射还可：

* 避免行续接反引号
* 更易添加或移除参数
* 在 API 演进时保持示例可读

### 安装新服务

```powershell
# 正确：开关标志通过出现启用，或在溅射哈希表中设为 $true
$installParams = @{
    Name              = "WexflowServer"
    Description       = "Wexflow Workflow Engine"
    Path              = "C:\Program Files\dotnet\dotnet.exe"
    StartupDir        = "C:\Program Files\Wexflow Server\Wexflow.Server"
    Params            = "Wexflow.Server.dll"
    StartupType       = "Automatic"
    EnableHealth      = $true
    RecoveryAction    = "RestartService"
    HeartbeatInterval = 30
    MaxFailedChecks   = 3
}

Install-ServyService @installParams

# 错误：不要对内联开关这样做
# Install-ServyService -Quiet $true -EnableHealth $true

# 正确：使用不带值的内联开关，或使用溅射
# 内联：Install-ServyService -Quiet -EnableHealth
# 溅射：
# $params = @{ Quiet = $true; EnableHealth = $true }
# Install-ServyService @params

```

### 导出服务配置

```powershell
$exportXmlParams = @{
    Name           = "WexflowServer"
    ConfigFileType = "xml"
    Path           = "C:\WexflowServer.xml"
}

Export-ServyServiceConfig @exportXmlParams

$exportJsonParams = @{
    Name           = "WexflowServer"
    ConfigFileType = "json"
    Path           = "C:\WexflowServer.json"
}

Export-ServyServiceConfig @exportJsonParams
```

### 导入服务配置

```powershell
# 正确：开关标志通过出现启用，或在溅射哈希表中设为 $true
$importXmlParams = @{
    ConfigFileType = "xml"
    Path           = "C:\WexflowServer.xml"
    Install        = $true
}

Import-ServyServiceConfig @importXmlParams

$importJsonParams = @{
    ConfigFileType = "json"
    Path           = "C:\WexflowServer.json"
}

Import-ServyServiceConfig @importJsonParams
```

### 启动、停止、重启与查看状态

Servy 安装的是**标准 Windows 服务**，已完整注册到服务控制管理器（SCM）。安装后，可使用**任何常规 Windows 服务控制机制**管理该服务，例如 `Start-Service`、`sc.exe`、`services.msc` 或第三方工具。

下方所示的 Servy PowerShell cmdlet 作为**便捷封装**提供，以保持脚本一致性。启动或停止服务**并不要求**使用它们。

```powershell
$serviceParams = @{
    Name  = "WexflowServer"
}

Start-ServyService @serviceParams
Get-ServyServiceStatus @serviceParams
Stop-ServyService @serviceParams
Restart-ServyService @serviceParams
```

#### 使用标准 Windows 服务命令

同一服务可用内置 PowerShell 与 Windows 工具控制：

```powershell
Start-Service -Name WexflowServer
Get-Service   -Name WexflowServer
Stop-Service  -Name WexflowServer
```

或从**提升权限**的命令提示符：

```cmd
sc.exe start WexflowServer
sc.exe stop  WexflowServer
```

**注意：** 使用 PowerShell 时，请从**提升权限的 PowerShell 会话**中显式调用 `sc.exe`（例如 `sc.exe start ServiceName`）。PowerShell 将 `sc` 定义为别名，省略 `.exe` 可能导致意外行为。这是标准 PowerShell 行为，与 Servy 无关。

安装后，该服务的行为与任何其他原生 Windows 服务相同，运行时不依赖 Servy 特定命令。

### 卸载服务

```powershell
$uninstallParams = @{
    Name  = "WexflowServer"
}

Uninstall-ServyService @uninstallParams
```

## Cmdlet 参考

| Cmdlet | 参数 | 说明 |
| --- | --- | --- |
| `Set-ServyConfig` | `-TimeoutSeconds`（int，可选，默认：600）<br>`-MaxBufferChars`（int，可选，默认：1048576） | **配置 Servy CLI 的模块级执行设置。**<br>更新内部模块变量，如执行超时与输出缓冲区限制。适用于为资源受限环境或异常长时间运行的操作调优模块。<br><br>**示例：**<br>- `Set-ServyConfig -TimeoutSeconds 1200 -MaxBufferChars 2097152` |
| `Install-ServyService` | `-Name`（string，**必需**）<br>`-Path`（string，**必需**）<br>*（完整参数列表见 [Install-ServyService 参数](#install-servyservice-参数参考)）* | **以高级配置安装新 Windows 服务。**<br><br>封装 Servy CLI `install` 命令，将任意可执行文件变为托管 Windows 服务。支持复杂生命周期管理、日志与自愈功能。<br><br>**主要功能：**<br>- **日志：** 将输出重定向到文件，并按大小或日期轮转。<br>- **健康：** 基于失败心跳的自动恢复操作（例如 `RestartService`）。<br>- **生命周期：** 在启动*前*（`PreLaunch`）或*后*（`PostLaunch`）执行任务。<br><br>**示例：**<br>- `Install-ServyService -Name "MyApp" -Path "C:\App\app.exe"`<br>- `Install-ServyService -Name "MyApp" -DisplayName "My App" -Path "C:\App\app.exe"`<br>- `Install-ServyService -Name "LogApp" -Path "C:\App\app.exe" -Stdout "C:\App\stdout.log" -EnableSizeRotation -RotationSize 10`<br>- `Install-ServyService -Name "SecureApp" -Path "C:\App\app.exe" -EnvVars "API_KEY=12345;DB_PORT=5432"` |
| `Uninstall-ServyService` | `-Name`（string，**必需**）<br>`-Quiet`（switch，可选） | **按名称卸载 Windows 服务。**<br>从 Windows 服务控制管理器（SCM）与 Servy 内部数据库中完全移除服务条目。<br><br>**示例：**<br>- `Uninstall-ServyService -Name "MyApp" -Quiet` |
| `Start-ServyService` | `-Name`（string，**必需**）<br>`-Quiet`（switch，可选） | **启动 Windows 服务。**<br>触发服务启动信号。若安装时定义了任何 `PreLaunch` 设置，将执行启动前进程，且必须在主服务启动前成功（除非使用了 `PreLaunchIgnoreFailure`）。<br><br>**示例：**<br>- `Start-ServyService -Name "MyApp"` |
| `Stop-ServyService` | `-Name`（string，**必需**）<br>`-Quiet`（switch，可选） | **停止 Windows 服务。**<br>向服务进程发送终止信号。遵守安装时设置的 `StopTimeout` 值，允许应用在强制终止前优雅关闭。<br><br>**示例：**<br>- `Stop-ServyService -Name "MyApp" -Quiet` |
| `Restart-ServyService` | `-Name`（string，**必需**）<br>`-Quiet`（switch，可选） | **重启 Windows 服务。**<br>执行完整的停止操作，随后启动。这是导入后应用配置更改的推荐方式。<br><br>**示例：**<br>- `Restart-ServyService -Name "MyApp"` |
| `Get-ServyServiceStatus` | `-Name`（string，**必需**）<br>`-Quiet`（switch，可选） | **检索服务的当前状态。**<br>向 SCM 查询进程的实时状态。<br><br>**可能结果：** `NotInstalled`、`Stopped`、`StartPending`、`StopPending`、`Running`、`ContinuePending`、`PausePending`、`Paused`、`Unknown`。<br><br>**示例：**<br>- `Get-ServyServiceStatus -Name "MyApp"` |
| `Export-ServyServiceConfig` | `-Name`（string，**必需**）<br>`-ConfigFileType`（string，**必需**：`xml`、`json`）<br>`-Path`（string，**必需**）<br>`-Quiet`（switch，可选） | **将服务配置导出到文件。**<br>将所有元数据（路径、超时、健康检查等）保存到外部文件，用于备份或模板创建。<br><br>**示例：**<br>- `Export-ServyServiceConfig -Name "MyApp" -ConfigFileType "json" -Path "C:\Backups\MyApp.json"`<br>- `Export-ServyServiceConfig -Name "MyApp" -ConfigFileType "xml" -Path "C:\Backups\MyApp.xml"` |
| `Import-ServyServiceConfig` | `-ConfigFileType`（string，**必需**：`xml`、`json`）<br>`-Path`（string，**必需**）<br>`-Install`（switch，可选）<br>`-Quiet`（switch，可选） | **从文件导入配置。**<br>将先前导出的文件中的设置加载到 Servy 数据库。使用 `-Install` 开关可在导入后立即向 Windows 注册服务。<br><br>**示例：**<br>- `Import-ServyServiceConfig -ConfigFileType "json" -Path "C:\Configs\NewApp.json" -Install`<br>- `Import-ServyServiceConfig -ConfigFileType "xml" -Path "C:\Configs\NewApp.xml"` |
| `Get-ServyHelp` | `-Command`（string，可选）<br>`-Quiet`（switch，可选） | **显示 Servy CLI 帮助手册。**<br>提供全局用法说明，或在请求时提供特定命令的详细参数解释。<br><br>**示例：**<br>- `Get-ServyHelp`<br>- `Get-ServyHelp -Command "install"` |
| `Get-ServyVersion` | `-Quiet`（switch，可选） | **显示 Servy 二进制版本。**<br>输出模块所使用的 `servy-cli.exe` 文件的版本字符串。<br><br>**示例：**<br>- `Get-ServyVersion -Quiet` |

### Install-ServyService 参数参考

#### 核心配置

| 参数 | 类型 | 必需 | 值 / 范围 / 说明 |
| --- | --- | --- | --- |
| `-Name` | string | **是** | 服务唯一标识名称 |
| `-Path` | string | **是** | 可执行进程的路径 |
| `-DisplayName` | string | 否 | Windows 服务中的显示名称（`services.msc`） |
| `-Description` | string | 否 | 关于服务的描述文本 |
| `-StartupDir` | string | 否 | 服务进程的工作目录 |
| `-Params` | string | 否 | 传递给可执行文件的附加参数 |
| `-StartupType` | string | 否 | 选项：`Automatic`、`AutomaticDelayedStart`、`Manual`、`Disabled` |
| `-Priority` | string | 否 | 选项：`Idle`、`BelowNormal`、`Normal`、`AboveNormal`、`High`、`RealTime` |
| `-CpuAffinity` | string | 否 | 允许的逻辑 CPU（例如 `'0-3,8'` 或 `'0xFF00'`） |
| `-User` | string | 否 | 服务账户用户名（`.\username` 或 `DOMAIN\username`） |
| `-Password` | SecureString | 否 | 服务账户密码 |
| `-EnvVars` | string | 否 | 环境变量（`Name=Value;Name=Value`） |
| `-Deps` | string | 否 | Windows 服务依赖（按服务名） |
| `-StartTimeout` | int | 否 | 等待成功启动的超时（范围：`1`-`86400` 秒） |
| `-StopTimeout` | int | 否 | 等待进程退出的超时（范围：`1`-`86400` 秒） |
| `-EnableConsoleUI` | switch | 否 | 启用控制台 UI（禁用 stdout/stderr 重定向） |
| `-Quiet` | switch | 否 | 抑制旋转指示器并以非交互方式运行 |

> [!IMPORTANT]
> 若服务在 Local System 以外的账户下运行，必须为该服务账户授予对 `%ProgramData%\Servy` 的 **Modify** 权限，并运行强制加固脚本（`Set-ServyExePermissions.ps1 -TargetAccount "domain\user"`），将二进制可执行文件锁定为 **Read & Execute**，以防止无特权二进制篡改与本地权限提升。脚本位置与执行说明见 [可执行文件权限加固指南](./Security#executable-permission-hardening-mandatory)。

#### 日志

| 参数 | 类型 | 必需 | 值 / 范围 / 说明 |
| --- | --- | --- | --- |
| `-Stdout` | string | 可选 | 捕获 stdout 的日志文件路径 |
| `-Stderr` | string | 可选 | 捕获 stderr 的日志文件路径 |
| `-EnableRotation` | switch | 可选 | *已弃用*：请使用 `-EnableSizeRotation` |
| `-EnableSizeRotation` | switch | 可选 | 启用基于大小的日志轮转 |
| `-RotationSize` | int | 可选 | 轮转前的最大日志文件大小（范围：`1`-`10240` MB） |
| `-EnableDateRotation` | switch | 可选 | 启用基于日期的日志轮转 |
| `-DateRotationType` | string | 可选 | 选项：`Daily`、`Weekly`、`Monthly`、`None` |
| `-MaxRotations` | int | 可选 | 保留的已轮转日志数（范围：`0`-`10000`；`0` = 无限制） |
| `-UseLocalTimeForRotation` | switch | 可选 | 使用本地服务器时间而非 UTC 计算轮转 |
| `-EnableDebugLogs` | switch | 可选 | 启用调试日志到 `Servy.Service.log` |

#### 健康监控与自愈

| 参数 | 类型 | 必需 | 值 / 范围 / 说明 |
| --- | --- | --- | --- |
| `-EnableHealth` | switch | 可选 | 启用自动健康监控 |
| `-HeartbeatInterval` | int | 可选 | 心跳间隔（秒）（范围：`5`-`86400` 秒） |
| `-MaxFailedChecks` | int | 可选 | 触发恢复前的失败检查次数（范围：`1`-`100000`） |
| `-RecoveryAction` | string | 可选 | 选项：`None`、`RestartService`、`RestartProcess`、`RestartComputer` |
| `-RecoveryOnCleanExit` | switch | 可选 | 即使进程以代码 0 退出也运行恢复操作 |
| `-MaxRestartAttempts` | int | 可选 | 最大重启尝试次数（范围：`0`-`100000`；`0` = 无限制） |
| `-HeartbeatUrl` | string | 可选 | 带外诊断探测 URL（例如 healthchecks.io） |
| `-HeartbeatUrlTimeoutSeconds` | int | 可选 | 探测响应超时（范围：`2`-`30` 秒） |
| `-EnableHeartbeatUrlFlags` | switch | 可选 | 包含心跳 URL 标志（`/start`、`/fail`） |
| `-FailureProgramPath` | string | 可选 | 服务失败时运行的程序/脚本路径 |
| `-FailureProgramStartupDir` | string | 可选 | 失败程序的工作目录 |
| `-FailureProgramParams` | string | 可选 | 失败程序的参数 |

#### 生命周期钩子（启动前/后与停止前/后）

| 参数 | 类型 | 必需 | 值 / 范围 / 说明 |
| --- | --- | --- | --- |
| `-PreLaunchPath` | string | 可选 | 服务启动前运行的可执行文件/脚本 |
| `-PreLaunchStartupDir` | string | 可选 | PreLaunch 脚本的工作目录 |
| `-PreLaunchParams` | string | 可选 | PreLaunch 可执行文件的附加参数 |
| `-PreLaunchEnv` | string | 可选 | PreLaunch 可执行文件的环境变量 |
| `-PreLaunchStdout` | string | 可选 | PreLaunch stdout 日志文件路径 |
| `-PreLaunchStderr` | string | 可选 | PreLaunch stderr 日志文件路径 |
| `-PreLaunchTimeout` | int | 可选 | PreLaunch 超时（范围：`0`-`86400` 秒；`0` = fire-and-forget） |
| `-PreLaunchRetryAttempts` | int | 可选 | PreLaunch 可执行文件的重试次数（范围：`0`-`100000`） |
| `-PreLaunchIgnoreFailure` | switch | 可选 | 即使 PreLaunch 失败也继续启动服务 |
| `-PostLaunchPath` | string | 可选 | 服务启动后运行的可执行文件/脚本（fire-and-forget） |
| `-PostLaunchStartupDir` | string | 可选 | PostLaunch 脚本的工作目录 |
| `-PostLaunchParams` | string | 可选 | PostLaunch 可执行文件的附加参数 |
| `-PreStopPath` | string | 可选 | 服务停止前运行的可执行文件/脚本 |
| `-PreStopStartupDir` | string | 可选 | PreStop 脚本的工作目录 |
| `-PreStopParams` | string | 可选 | PreStop 可执行文件的附加参数 |
| `-PreStopTimeout` | int | 可选 | PreStop 超时（范围：`0`-`86400` 秒；`0` = fire-and-forget） |
| `-PreStopLogAsError` | switch | 可选 | 将 PreStop 失败视为错误 |
| `-PostStopPath` | string | 可选 | 服务停止后运行的可执行文件/脚本 |
| `-PostStopStartupDir` | string | 可选 | PostStop 脚本的工作目录 |
| `-PostStopParams` | string | 可选 | PostStop 可执行文件的附加参数 |

有关 CPU 亲和性的更多详情，见 [常见问题](./FAQ#how-and-why-should-i-use-cpu-affinity-with-servy)。

## 故障排除
1. 传递 `$true` 或 `$false` 时安装失败

    PowerShell 中的常见错误是尝试向开关参数传递布尔值（例如 `-Quiet $true`）。
    * 症状：命令以 “Parameter cannot be found” 错误失败，或更常见的是 PowerShell 将 `$true` 解释为下一个位置参数。在 `Install-ServyService` 中，这常导致 `$true` 被错误地赋给 `-Path` 或 `-Name` 参数，从而使底层 CLI 调用失败。
    * 修复：移除 `$true` 或 `$false` 引用。使用 `-Quiet` 开启，省略则保持关闭。
1. “Access Denied” 错误

   大多数 Servy 操作（安装、卸载、启动、停止）直接与 Windows 服务控制管理器交互。
   * 解决方案：确保 PowerShell 会话以管理员权限运行。若使用 VS Code 等 IDE，请以管理员身份重启。
1. 服务无法启动

   若 `Start-ServyService` 返回成功消息但服务状态仍为 Stopped，问题可能出在应用程序可执行文件或 PreLaunch 配置中。
   * 解决方案：若安装时配置了 `stdout` 与 `stderr` 日志，请检查它们。
   * 验证：在命令提示符中手动运行 `-Path` 与 `-Params` 中定义的命令，查看是否立即崩溃。
1. 找不到 CLI 可执行文件

   在便携模式下，模块期望 `servy-cli.exe` 与 `Servy.psm1` 位于同一文件夹。
   * 解决方案：确认文件未被分开。若使用已安装版本，确保 `%ProgramFiles%\Servy\` 在系统 **PATH** 中，或该目录中存在这些文件。
1. 自动化环境中的问题（Ansible/CI/CD）

   若进程尝试绘制交互式进度条或旋转指示器，自动化运行器常会挂起。
   * 解决方案：在非交互脚本中始终使用 `-Quiet` 开关。这会强制模块输出纯文本日志，而非交互式 UI 元素。
1. 环境变量格式

   `-EnvVars` 与 `-PreLaunchEnv` 参数需要特定字符串格式。
   * 要求：使用 `Key=Value` 格式，以分号分隔。
   * 示例：`-EnvVars "NODE_ENV=production;PORT=3000"`

## 另见
* [导出/导入服务](./Export-Import-Services)

## 参考

* [Servy.psm1](https://github.com/aelassas/servy/blob/main/src/Servy.CLI/Servy.psm1)
* [servy-module-examples.ps1](https://github.com/aelassas/servy/blob/main/src/Servy.CLI/servy-module-examples.ps1)
