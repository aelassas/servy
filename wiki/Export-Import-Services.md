## 目录

1. [简介](#introduction)
1. [导出](#export)
   1. [GUI](#gui)
   1. [CLI](#cli)
   1. [PowerShell](#powershell)
1. [导入](#import)
   1. [安全说明](#security-note)
   1. [格式](#format)
   1. [XML 示例](#xml-sample)
   1. [JSON 示例](#json-sample)
   1. [GUI](#gui-1)
   1. [CLI](#cli-1)
   1. [PowerShell](#powershell-1)

## Introduction
Servy 支持通过 GUI、CLI 和 PowerShell 模块导出与导入服务配置。

所有通过 Servy 创建的服务（无论通过 GUI、CLI 还是 PowerShell）都存储在专用的 SQLite 数据库中。

GUI 可以导出已注册和未注册的服务。

CLI 和 PowerShell 模块只能导出已在 Servy 数据库中注册的服务。

数据库位于：
```text
%ProgramData%\Servy\db\Servy.db
```

密码使用 AES 安全加密，密钥通过 Windows DPAPI 保护。更多详情请参阅 [安全](./Security) 页面。

> [!IMPORTANT]
> 导出中不包含密码，但其他敏感字段（如 Parameters 和 EnvironmentVariables）仍可能存在。请始终安全地处理导出文件。不要公开分享、提交到源代码控制，或放在未受保护的位置。考虑加密这些文件或限制访问，以确保其保密性。

## Export

Password 和 UserAccount 字段不会导出；出于安全原因，请在导入后重新输入。

### GUI
* 打开 Servy
* 填写服务配置
* 点击左上角的 **Export** 菜单
* 选择导出格式（XML 或 JSON）
* 指定导出文件路径并确认

### CLI
* 运行以下命令以 XML 格式导出：
  ```cmd
  servy-cli export --name="MyRegisteredService" --config="xml" --path="C:\MyRegisteredService.xml"
  ```
* 运行以下命令以 JSON 格式导出：
  ```cmd
  servy-cli export --name="MyRegisteredService" --config="json" --path="C:\MyRegisteredService.json"
  ```

若在 Servy 数据库中找不到指定服务，CLI 将以退出码 1 终止。

### PowerShell
* 导入 Servy PowerShell 模块：
  ```powershell
  Import-Module "C:\Program Files\Servy\Servy.psm1" -Force
  ```

* 运行以下脚本以 XML 格式导出：
  ```powershell
  $exportParamsXml = @{
      Name           = "MyRegisteredService"
      ConfigFileType = "Xml"
      Path           = "C:\MyRegisteredService.xml"
  }

  Export-ServyServiceConfig @exportParamsXml
  ```
* 运行以下脚本以 JSON 格式导出：
  ```powershell
  $exportParamsJson = @{
      Name           = "MyRegisteredService"
      ConfigFileType = "Json"
      Path           = "C:\MyRegisteredService.json"
  }

  Export-ServyServiceConfig @exportParamsJson
  ```

若在 Servy 数据库中找不到指定服务，PowerShell cmdlet 将**抛出错误**。您可以使用 `try/catch` 处理失败，类似于 CLI 以退出码 1 退出。

## Import
> [!NOTE]
> 若需要密码和用户，必须在导入后重新输入。

### Security Note

> [!CAUTION]
> **渗透防护：本地导入强制**
>
> 从通用命名约定（UNC）路径或通过重定向路径（例如符号链接、联接点或映射的网络驱动器）导入服务配置会带来严重安全风险，包括攻击者控制的配置注入、路径重定向利用以及权限提升。
>
> 为维护系统完整性，导入管道通过多层验证链显式阻止非本地路径：检查显式 UNC 前缀、查询驱动器接口、遍历重解析点祖先、评估文件级符号链接、阻止保留设备名、强制受保护目录边界，并通过 `GetFinalPathNameByHandle` 验证规范句柄。
>
> 有关这些攻击向量与防御验证管道的完整技术细节，请参阅安全页面上的 [渗透防护：本地导入强制](./Security#infiltration-guard-local-import-enforcement)。

### Format
您可以按 XML 或 JSON 格式导入服务配置。

仅 **Name** 和 **ExecutablePath** 字段为必填；其余均为可选。

#### 字段

| 字段 | 类型 | 必填 | 说明 |
|-------|------|----------|-------------|
| Name | string | **是** | 服务的唯一名称。 |
| DisplayName | string? | 否 | 在 Windows 服务控制台中显示的易读名称。若为空，则使用服务名称。 |
| Description | string? | 否 | 服务的可选描述。 |
| ExecutablePath | string | **是** | 服务可执行文件的路径。 |
| StartupDirectory | string? | 否 | 服务可执行文件的可选启动目录。 |
| Parameters | string? | 否 | 传递给服务可执行文件的可选参数。 |
| StartupType | int? | 否 | 服务的启动类型（来自 [StartupType](#startuptype) 表的整数值）。 |
| Priority | int? | 否 | 服务的进程优先级（来自 [Priority](#priority) 表的整数值）。 |
| CpuAffinity | string? | 否 | 进程可运行的逻辑 CPU，为核心列表/范围或十六进制掩码（例如 `0-3,8` 或 `0xFF00`）。 |
| StdoutPath | string? | 否 | 标准输出日志的可选路径。 |
| StderrPath | string? | 否 | 标准错误日志的可选路径。 |
| StartTimeout | int? | 否 | 等待进程成功启动的超时秒数，超时后视为启动失败（范围：`1`-`86400`）。 |
| StopTimeout | int? | 否 | 等待进程退出的超时秒数（范围：`1`-`86400`）。 |
| EnableConsoleUI | bool? | 否 | 为 true 时，被包装进程以可见控制台窗口运行；stdout/stderr 重定向被禁用。默认为 false。 |
| EnableSizeRotation | bool? | 否 | 是否启用基于大小的日志轮转。 |
| RotationSize | int? | 否 | 轮转前日志文件的最大大小（兆字节 MB）（范围：`1`-`10240`）。 |
| EnableDateRotation | bool? | 否 | 是否启用基于日期的日志轮转。 |
| DateRotationType | int? | 否 | 日期轮转类型。（来自 [DateRotationType](#daterotationtype) 表的整数值）。 |
| UseLocalTimeForRotation | bool? | 否 | 是否使用本地服务器时间进行日志轮转而非 UTC。默认为 false（UTC）。 |
| MaxRotations | int? | 否 | 保留的最大日志文件数（范围：`0`-`10000`；`0` = 无限制）。 |
| EnableDebugLogs | bool? | 否 | 是否在本地日志文件中启用调试日志。不建议在生产环境使用，因为这些日志可能包含敏感数据。 |
| EnableHealthMonitoring | bool? | 否 | 是否启用健康监控。 |
| HeartbeatInterval | int? | 否 | 健康监控的心跳间隔秒数（范围：`5`-`86400`）。 |
| MaxFailedChecks | int? | 否 | 触发恢复前允许的最大连续失败健康检查次数（范围：`1`-`100000`）。 |
| RecoveryAction | int? | 否 | 恢复操作（来自 [RecoveryAction](#recoveryaction) 表的整数值）。 |
| RecoveryOnCleanExit | bool? | 否 | 是否在被包装进程以干净（零）退出码退出时也触发恢复操作。默认为 `false`。（自 8.4 起可用） |
| MaxRestartAttempts | int? | 否 | 服务失败时的最大重启尝试次数（范围：`0`-`100000`；`0` = 无限制，会绕过失败程序——参见 [健康监控与恢复](./Health-Monitoring-&-Recovery)）。 |
| HeartbeatUrl | string? | 否 | 带外诊断心跳的绝对 URL。 |
| HeartbeatUrlTimeoutSeconds | int? | 否 | 心跳 URL 请求的超时秒数（范围：`2`-`30`）。 |
| EnableHeartbeatUrlFlags | bool? | 否 | 为心跳 URL ping 追加 /start 和 /fail 生命周期后缀。 |
| FailureProgramPath | string? | 否 | 失败时运行的可选进程路径。 |
| FailureProgramStartupDirectory | string? | 否 | 失败程序的可选工作目录。 |
| FailureProgramParameters | string? | 否 | 失败程序的可选命令行参数。 |
| EnvironmentVariables | string? | 否 | 可选环境变量，`key=value` 格式，以分号分隔。 |
| ServiceDependencies | string? | 否 | 此服务所依赖的可选服务名称，以分号分隔。 |
| PreLaunchExecutablePath | string? | 否 | 服务启动前运行的可选可执行文件路径。 |
| PreLaunchStartupDirectory | string? | 否 | 预启动可执行文件的可选启动目录。 |
| PreLaunchParameters | string? | 否 | 预启动可执行文件的可选参数。 |
| PreLaunchEnvironmentVariables | string? | 否 | 预启动可执行文件的可选环境变量，`key=value` 格式。 |
| PreLaunchStdoutPath | string? | 否 | 预启动可执行文件标准输出日志的可选路径。 |
| PreLaunchStderrPath | string? | 否 | 预启动可执行文件标准错误日志的可选路径。 |
| PreLaunchTimeoutSeconds | int? | 否 | 等待预启动可执行文件完成的最大秒数（范围：`0`-`86400`；`0` = 即发即忘）。 |
| PreLaunchRetryAttempts | int? | 否 | 预启动可执行文件的最大重试次数（范围：`0`-`100000`）。 |
| PreLaunchIgnoreFailure | bool? | 否 | 是否忽略预启动可执行文件的失败。 |
| PostLaunchExecutablePath | string? | 否 | 进程成功启动后运行的可选可执行文件路径。 |
| PostLaunchStartupDirectory | string? | 否 | 后启动可执行文件的可选启动目录。 |
| PostLaunchParameters | string? | 否 | 后启动可执行文件的可选参数。 |
| PreStopExecutablePath | string? | 否 | 服务停止前运行的可选可执行文件路径。 |
| PreStopStartupDirectory | string? | 否 | 预停止可执行文件的可选启动目录。 |
| PreStopParameters | string? | 否 | 预停止可执行文件的可选参数。 |
| PreStopTimeoutSeconds | int? | 否 | 等待预停止可执行文件完成的最大秒数（范围：`0`-`86400`；`0` = 即发即忘）。 |
| PreStopLogAsError | bool? | 否 | 是否将预停止失败记录为错误。 |
| PostStopExecutablePath | string? | 否 | 服务停止后运行的可选可执行文件路径。 |
| PostStopStartupDirectory | string? | 否 | 后停止可执行文件的可选启动目录。 |
| PostStopParameters | string? | 否 | 后停止可执行文件的可选参数。 |

> [!IMPORTANT]
> `UserAccount`、`Password` 和 `RunAsLocalSystem` 永不导出。导入时，文件中的任何自定义标识都会被**丢弃**（服务重置为无密码的 **LocalSystem** 基线）并记录警告；请在导入后手动重新输入账户和密码。`Pid`、`PreviousStopTimeout`、`ActiveStdoutPath` 和 `ActiveStderrPath` 会被静默忽略，无法通过导入文件设置。

#### StartupType

| 值 | 名称      | 说明 |
|-------|-----------|-------------|
| 2     | Automatic | 服务在系统启动期间由服务控制管理器自动启动。 |
| 3     | Manual    | 服务必须由用户或应用程序手动启动。 |
| 4     | Disabled  | 服务已禁用，无法启动。 |
| 5     | AutomaticDelayedStart | 服务自动启动，但在其他自动启动服务之后延迟启动。 |

#### Priority

| 值 | 名称        | 说明 |
|-------|-------------|-------------|
| 0     | Idle        | 进程仅在系统空闲且其他进程未使用 CPU 时运行。这是最低优先级。 |
| 1     | BelowNormal | 进程优先级低于正常，但高于空闲。 |
| 2     | Normal      | 进程具有正常优先级，这是进程的默认优先级。 |
| 3     | AboveNormal | 进程优先级高于正常，但低于高。 |
| 4     | High        | 进程具有高优先级，相比正常优先级获得更多 CPU 时间。 |
| 5     | RealTime    | 进程具有实时优先级，最高优先级。请谨慎使用，因为它可能独占 CPU 资源并饿死其他进程。 |

#### DateRotationType

| 值 | 名称    | 说明 |
|-------|---------|-------------|
| 0     | Daily   | 每个日历日轮转一次日志文件（默认 UTC；若 `UseLocalTimeForRotation` 为 true 则使用本地时间）。 |
| 1     | Weekly  | 每个日历周轮转一次日志文件（默认 UTC；若 `UseLocalTimeForRotation` 为 true 则使用本地时间；ISO FirstFourDayWeek，周一为一周第一天）。 |
| 2     | Monthly | 每个日历月轮转一次日志文件（默认 UTC；若 `UseLocalTimeForRotation` 为 true 则使用本地时间）。 |
| 3     | None    | 禁用基于日期的轮转。仅需基于大小的轮转时使用此项。 |

#### RecoveryAction

| 值 | 名称             | 说明 |
|-------|------------------|-------------|
| 0     | None             | 不采取任何操作。 |
| 1     | RestartService   | 重启服务。 |
| 2     | RestartProcess   | 重启进程。 |
| 3     | RestartComputer  | 重启计算机。 |

### XML Sample

```xml
<?xml version="1.0" encoding="utf-8"?>
<ServiceDto>
  <Name>MyTestService</Name>
  <DisplayName>My Test Service</DisplayName>
  <Description>Sample service for testing import</Description>
  <ExecutablePath>C:\Program Files\TestService\TestService.exe</ExecutablePath>
  <StartupDirectory>C:\Program Files\TestService</StartupDirectory>
  <Parameters>-arg1 -arg2</Parameters>
  <StartupType>2</StartupType>
  <Priority>1</Priority>
  <CpuAffinity>0-3,8</CpuAffinity>
  <StdoutPath>C:\Logs\TestService_out.log</StdoutPath>
  <StderrPath>C:\Logs\TestService_err.log</StderrPath>
  <StartTimeout>10</StartTimeout>
  <StopTimeout>5</StopTimeout>
  <EnableSizeRotation>true</EnableSizeRotation>
  <RotationSize>10</RotationSize>
  <EnableDateRotation>false</EnableDateRotation>
  <DateRotationType>0</DateRotationType>
  <MaxRotations>0</MaxRotations>
  <UseLocalTimeForRotation>false</UseLocalTimeForRotation>
  <EnableConsoleUI>false</EnableConsoleUI>
  <EnableDebugLogs>false</EnableDebugLogs>
  <EnableHealthMonitoring>true</EnableHealthMonitoring>
  <HeartbeatInterval>30</HeartbeatInterval>
  <MaxFailedChecks>3</MaxFailedChecks>
  <RecoveryAction>1</RecoveryAction>
  <MaxRestartAttempts>5</MaxRestartAttempts>
  <HeartbeatUrl>https://hc-ping.com/your-uuid</HeartbeatUrl>
  <HeartbeatUrlTimeoutSeconds>5</HeartbeatUrlTimeoutSeconds>
  <EnableHeartbeatUrlFlags>true</EnableHeartbeatUrlFlags>
  <RecoveryOnCleanExit>false</RecoveryOnCleanExit>
  <FailureProgramPath>C:\Program Files\nodejs\node.exe</FailureProgramPath>
  <FailureProgramStartupDirectory>C:\Apps\Notify</FailureProgramStartupDirectory>
  <FailureProgramParameters>C:\Apps\Notify\index.js</FailureProgramParameters>
  <EnvironmentVariables>APP_ENV=production;APP_CONFIG=C:\Apps\App\config.json</EnvironmentVariables>
  <ServiceDependencies>ServiceA;ServiceB</ServiceDependencies>
  <PreLaunchExecutablePath>C:\Program Files\TestService\PreLaunch.exe</PreLaunchExecutablePath>
  <PreLaunchStartupDirectory>C:\Program Files\TestService</PreLaunchStartupDirectory>
  <PreLaunchParameters>-preArg1 -preArg2</PreLaunchParameters>
  <PreLaunchEnvironmentVariables>CONFIG=C:\Config;LOGS=C:\Logs</PreLaunchEnvironmentVariables>
  <PreLaunchStdoutPath>C:\Logs\PreLaunch_out.log</PreLaunchStdoutPath>
  <PreLaunchStderrPath>C:\Logs\PreLaunch_err.log</PreLaunchStderrPath>
  <PreLaunchTimeoutSeconds>60</PreLaunchTimeoutSeconds>
  <PreLaunchRetryAttempts>2</PreLaunchRetryAttempts>
  <PreLaunchIgnoreFailure>true</PreLaunchIgnoreFailure>
  <PostLaunchExecutablePath>C:\Program Files\TestService\PostLaunch.exe</PostLaunchExecutablePath>
  <PostLaunchStartupDirectory>C:\Program Files\TestService</PostLaunchStartupDirectory>
  <PostLaunchParameters>-postArg1 -postArg2</PostLaunchParameters>
  <PreStopExecutablePath>C:\Program Files\TestService\PreStop.exe</PreStopExecutablePath>
  <PreStopStartupDirectory>C:\Program Files\TestService</PreStopStartupDirectory>
  <PreStopParameters>-stopArg1 -stopArg2</PreStopParameters>
  <PreStopTimeoutSeconds>30</PreStopTimeoutSeconds>
  <PreStopLogAsError>true</PreStopLogAsError>
  <PostStopExecutablePath>C:\Program Files\TestService\PostStop.exe</PostStopExecutablePath>
  <PostStopStartupDirectory>C:\Program Files\TestService</PostStopStartupDirectory>
  <PostStopParameters>-stopArg1 -stopArg2</PostStopParameters>
</ServiceDto>
```

### JSON Sample

```json
{
  "Name": "MyTestService",
  "DisplayName": "My Test Service",
  "Description": "Sample service for testing import",
  "ExecutablePath": "C:\\Program Files\\TestService\\TestService.exe",
  "StartupDirectory": "C:\\Program Files\\TestService",
  "Parameters": "-arg1 -arg2",
  "StartupType": 2,
  "Priority": 1,
  "CpuAffinity": "0-3,8",
  "StdoutPath": "C:\\Logs\\TestService_out.log",
  "StderrPath": "C:\\Logs\\TestService_err.log",
  "StartTimeout": 10,
  "StopTimeout": 5,
  "EnableSizeRotation": true,
  "RotationSize": 10,
  "EnableDateRotation": false,
  "DateRotationType": 0,
  "MaxRotations": 0,
  "UseLocalTimeForRotation": false,
  "EnableConsoleUI": false,
  "EnableDebugLogs": false,
  "EnableHealthMonitoring": true,
  "HeartbeatInterval": 30,
  "MaxFailedChecks": 3,
  "RecoveryAction": 1,
  "MaxRestartAttempts": 5,
  "HeartbeatUrl": "https://hc-ping.com/your-uuid",
  "HeartbeatUrlTimeoutSeconds": 5,
  "EnableHeartbeatUrlFlags": true,
  "RecoveryOnCleanExit": false,
  "FailureProgramPath": "C:\\Program Files\\nodejs\\node.exe",
  "FailureProgramStartupDirectory": "C:\\Apps\\Notify",
  "FailureProgramParameters": "C:\\Apps\\Notify\\index.js",
  "EnvironmentVariables": "APP_ENV=production;APP_CONFIG=C:\\Apps\\App\\config.json",
  "ServiceDependencies": "ServiceA;ServiceB",
  "PreLaunchExecutablePath": "C:\\Program Files\\TestService\\PreLaunch.exe",
  "PreLaunchStartupDirectory": "C:\\Program Files\\TestService",
  "PreLaunchParameters": "-preArg1 -preArg2",
  "PreLaunchEnvironmentVariables": "CONFIG=C:\\Config;LOGS=C:\\Logs",
  "PreLaunchStdoutPath": "C:\\Logs\\PreLaunch_out.log",
  "PreLaunchStderrPath": "C:\\Logs\\PreLaunch_err.log",
  "PreLaunchTimeoutSeconds": 60,
  "PreLaunchRetryAttempts": 2,
  "PreLaunchIgnoreFailure": true,
  "PostLaunchExecutablePath": "C:\\Program Files\\TestService\\PostLaunch.exe",
  "PostLaunchStartupDirectory": "C:\\Program Files\\TestService",
  "PostLaunchParameters": "-postArg1 -postArg2",
  "PreStopExecutablePath": "C:\\Program Files\\TestService\\PreStop.exe",
  "PreStopStartupDirectory": "C:\\Program Files\\TestService",
  "PreStopParameters": "-stopArg1 -stopArg2",
  "PreStopTimeoutSeconds": 30,
  "PreStopLogAsError": true,
  "PostStopExecutablePath": "C:\\Program Files\\TestService\\PostStop.exe",
  "PostStopStartupDirectory": "C:\\Program Files\\TestService",
  "PostStopParameters": "-stopArg1 -stopArg2"
}
```

### GUI
* 打开 Servy
* 点击左上角的 **Import** 菜单
* 选择导入格式（XML 或 JSON）
* 选择要导入的文件

若有效，配置会显示在 UI 中。若服务已安装，配置会持久化到数据库。

若文件被拒绝，错误对话框会说明原因，且不会向数据库写入任何内容。

### CLI
* 运行以下命令以 XML 格式导入：
  ```cmd
  servy-cli import --config="xml" --path="C:\MyRegisteredService.xml"
  ```
* 运行以下命令以 JSON 格式导入：
  ```cmd
  servy-cli import --config="json" --path="C:\MyRegisteredService.json"
  ```
* 运行以下命令导入 XML 并安装：
  ```cmd
  servy-cli import --config="xml" --path="C:\MyRegisteredService.xml" --install
  ```
* 运行以下命令导入 JSON 并安装：
  ```cmd
  servy-cli import --config="json" --path="C:\MyRegisteredService.json" --install
  ```

若有效，配置会保存到数据库；若提供了 `--install` 选项，则安装服务。

若文件缺失、格式错误、被本地路径防护阻止，或包含无效字段，则不会创建服务，CLI 以退出码 1 终止。

### PowerShell
* 导入 Servy PowerShell 模块：
  ```powershell
  Import-Module "C:\Program Files\Servy\Servy.psm1" -Force
  ```
* 从 XML 导入服务配置：
  ```powershell
  Import-ServyServiceConfig -ConfigFileType Xml -Path "C:\MyRegisteredService.xml"
  ```

* 从 JSON 导入服务配置：
  ```powershell
  Import-ServyServiceConfig -ConfigFileType Json -Path "C:\MyRegisteredService.json"
  ```

* 从 XML 导入服务配置并安装服务：
  ```powershell
  Import-ServyServiceConfig -ConfigFileType Xml -Path "C:\MyRegisteredService.xml" -Install
  ```

* 从 JSON 导入服务配置并安装服务：
  ```powershell
  Import-ServyServiceConfig -ConfigFileType Json -Path "C:\MyRegisteredService.json" -Install
  ```

若配置有效，会保存到 Servy 数据库；若提供了 `-Install` 开关，则安装服务。

在相同条件下 cmdlet 会抛出异常；请像处理 `Export-ServyServiceConfig` 一样用 `try/catch` 包装调用。
