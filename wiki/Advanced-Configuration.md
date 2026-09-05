## 目录

1. [简介](#简介)
1. [文件位置与先决条件](#文件位置与先决条件)
   1. [先决条件](#先决条件)
   1. [设置文件位置](#设置文件位置)
1. [日志配置](#日志配置)
   1. [日志级别（`LogLevel`）](#日志级别loglevel)
   1. [日志滚动间隔（`LogRollingInterval`）](#日志滚动间隔logrollinginterval)
1. [设置参考](#设置参考)
   1. [共享核心设置](#共享核心设置)
   1. [常规日志](#常规日志)
   1. [Servy Windows 服务](#servy-windows-服务)
   1. [Restarter 设置](#restarter-设置)
   1. [桌面应用设置](#桌面应用设置)
   1. [Manager 应用设置](#manager-应用设置)
   1. [CLI 设置](#cli-设置)
1. [构建配置示例](#构建配置示例)
   1. [现代构建（.NET 10.0+）](#现代构建net-100)
   1. [旧版构建（.NET Framework 4.8）](#旧版构建net-framework-48)
1. [另见](#另见)

## 简介

本指南说明如何微调 Servy 生态系统，涵盖应用刷新频率到高级日志轮转策略。

## 文件位置与先决条件

### 先决条件
* **管理员权限**：必须以**管理员**身份运行文本编辑器（如 VS Code、Notepad++），才能保存对 `%ProgramFiles%` 或 `%ProgramData%` 的更改。
* **需要重启**：对配置文件的更改不会动态生效。必须重启关联应用（Desktop/Manager）或 Windows 服务（通过 SCM 或 CLI）后更改才会应用。

### 设置文件位置

| 组件 | 现代构建（.NET 10.0+） | 旧版构建（.NET 4.8） |
| :--- | :--- | :--- |
| **Windows 服务** | `%ProgramData%\Servy\appsettings.service.json` | `...\Servy.Service.Net48.exe.config` |
| **Windows 服务（CLI）** | `%ProgramData%\Servy\appsettings.service.json` | `...\Servy.Service.CLI.Net48.exe.config` |
| **Restarter** | `%ProgramData%\Servy\appsettings.restarter.json` | `...\Servy.Restarter.Net48.exe.config` |
| **桌面应用** | `%ProgramFiles%\Servy\appsettings.desktop.json` | `...\Servy.exe.config` |
| **Manager 应用** | `%ProgramFiles%\Servy\appsettings.manager.json` | `...\Servy.Manager.exe.config` |
| **CLI** | `%ProgramFiles%\Servy\appsettings.cli.json` | `...\servy-cli.exe.config` |

> [!NOTE]
> 对于 Servy v8.7 及更早版本，桌面应用与服务的设置文件名为 `appsettings.json`。Servy v8.8+ 请参见上表。

## 日志配置

Servy 使用双通道日志引擎，同时写入 **Windows 事件日志**（用于高层监控）和**本地平面文件**（用于详细诊断）。

**日志目录：** `%ProgramData%\Servy\logs\`

### 日志级别（`LogLevel`）
决定输出详细程度。值不区分大小写。

| 级别 | 目标 | 说明 |
| :--- | :--- | :--- |
| **DEBUG** | **仅文件** | 最详细；记录内部状态与心跳。不建议用于生产。 |
| **INFO** | **两者** | **（默认）** 记录主要里程碑（启动、停止、配置更改）。 |
| **WARN** | **两者** | 记录非致命问题，如关闭缓慢或配置拼写错误。 |
| **ERROR** | **两者** | 记录严重失败、崩溃或访问被拒绝错误。 |
| **NONE** | **无** | 完全禁用日志引擎。 |

### 日志滚动间隔（`LogRollingInterval`）
决定新日志文件的创建频率。自 Servy v7.8+ 起可用。

> [!IMPORTANT]
> **大小轮转优先**：`LogRotationSizeMB` 限制始终优先。若日志在时间间隔（如 `Monthly`）之前达到大小限制（如 10MB），将立即轮转以防止磁盘溢出。

* `Daily`：在每个新日历日开始时轮转。**默认为 UTC**；设置
    `UseLocalTimeForRotation: true` 以在服务器本地时间午夜轮转。
* `Weekly`：每个日历周轮转一次（ISO 8601、FirstFourDayWeek、周一为
    第一天）。**默认为 UTC**；设置 `UseLocalTimeForRotation: true` 以使用本地时间。
* `Monthly`：在每个日历月的第一天轮转。**默认为 UTC**；
    设置 `UseLocalTimeForRotation: true` 以使用本地时间。
* `None`：**（默认）** 文件仅在达到大小限制时轮转。

## 设置参考

以下设置适用于 Windows 服务包装器、restarter、桌面应用、manager 应用与 CLI。需要微调特定组件的日志、刷新行为或运行限制时使用。

### 共享核心设置

这些设置配置整个 Servy 生态系统的数据存储与加密位置。每个 Servy 组件都会读取这些键。

> [!CAUTION]
> **跨组件一致与路径格式**
>
> * **需要绝对路径：** 在 JSON 配置文件中，连接字符串中的环境变量（如 `%ProgramData%`）不会自动展开。始终指定完整绝对路径（例如 `C:\\ProgramData\\Servy\\db\\Servy.db`），并转义反斜杠（`\\\\`）。
> * **一致性：** 若覆盖这些设置中的任一项，**必须在所有组件中应用完全相同的值**（`appsettings.service.json`、`appsettings.restarter.json`、`appsettings.desktop.json`、`appsettings.manager.json` 与 `appsettings.cli.json`）。

| 设置 | 默认示例 | 说明 |
| :--- | :--- | :--- |
| `ConnectionStrings:DefaultConnection` | `Data Source=C:\ProgramData\Servy\db\Servy.db;Busy Timeout=5000;Journal Mode=WAL;Pooling=True;` | SQLite 连接字符串。配置数据库位置、连接池与 WAL 模式参数。 |
| `Security:AESKeyFilePath` | `C:\ProgramData\Servy\security\aes_key.dat` | 用于保护敏感服务凭据的 AES 加密密钥文件的绝对路径。 |
| `Security:AESIVFilePath` | `C:\ProgramData\Servy\security\aes_iv.dat` | **（旧版）** v1 密码格式（Servy < 6.5）使用的静态 AES 初始化向量（IV）文件绝对路径。当前构建中未使用（`AllowLegacyV1Decryption = false`）。 |

以下是共享核心设置示例文件 `appsettings.service.json`：
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=C:\\ProgramData\\Servy\\db\\Servy.db;Busy Timeout=5000;Journal Mode=WAL;Pooling=True;"
  },
  "Security": {
    "AESKeyFilePath": "C:\\ProgramData\\Servy\\security\\aes_key.dat"
  }
}
```

以下是共享核心设置示例文件 `Servy.Service.Net48.exe.config`：
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <appSettings>
    <add key="DefaultConnection" value="Data Source=C:\ProgramData\Servy\db\Servy.db;Busy Timeout=5000;Journal Mode=WAL;Pooling=True;" />
    <add key="Security:AESKeyFilePath" value="C:\ProgramData\Servy\security\aes_key.dat" />
  </appSettings>
</configuration>
```

### 常规日志

| 设置 | 默认 | 说明 |
| :--- | :--- | :--- |
| `LogLevel` | `INFO` | 输出详细程度。 |
| `EnableSizeRotation` | `true` | 为内部日志文件启用基于大小的日志轮转。禁用时，`LogRotationSizeMB` 无效（自 v9.4+ 起可用）。 |
| `LogRotationSizeMB` | `10` | 归档前的最大日志文件大小（MB）。需要 `EnableSizeRotation` 为 `true`。必须大于 `0`；`0` 或负值会静默替换为默认 `10` MB。要禁用基于大小的轮转，请将 `EnableSizeRotation` 设为 `false`。 |
| `MaxBackupLogFiles` | `10` | 保留的归档日志数量。设为 `0` 表示无限制。 |
| `EnableEventLog` | `true` | 控制 Servy 与 Windows 服务基础设施是否写入 Windows 事件日志。 |
| `UseLocalTimeForRotation` | `false` | 确定基于日期的日志轮转边界时使用本地系统时间而非 UTC。 |
| `LogRollingInterval` | `None` | 基于日期的轮转间隔：`Daily`、`Weekly`、`Monthly` 或 `None`。参见上文日志滚动间隔一节。自 Servy v7.8+ 起可用。 |

> [!NOTE]
> `EnableEventLog` 设置主要由 **Servy Windows 服务**包装器与 **Servy Windows 服务 Restarter** 实用程序遵守。在 **CLI**、**桌面应用**或 **Manager 应用**配置中设置此项无效。

> [!WARNING]
> **`EnableEventLog: false` 对服务失败诊断的副作用**
>
> 将 `EnableEventLog` 设为 `false` 会同时禁用 Servy 的事件记录器与框架的自动服务状态日志（`AutoLog = false`）。
>
> 设为 `false` 时，启动或关闭失败（如 `SR.StartFailed`、`SR.StopFailed` 或 `SR.ShutdownFailed`）将**不会**在 Windows 事件查看器中生成条目日志。此状态下，关键诊断条目仅捕获在 `%ProgramData%\Servy\logs\` 下的本地文件日志中。

### Servy Windows 服务

| 设置 | 默认 | 说明 |
| :--- | :--- | :--- |
| `Timing:WaitChunkMs` | `5000` | 运行同步 **pre-launch / pre-stop 钩子**时等待循环的粒度（毫秒）。控制多久切片一次以检查取消或超时，不控制健康检查频率。 |
| `Timing:ScmAdditionalTimeMs` | `15000` | 添加到服务控制管理器（SCM）操作的额外缓冲时间（毫秒），防止过早超时。 |

### Restarter 设置

| 设置 | 默认 | 说明 |
| :--- | :--- | :--- |
| `RestartTimeoutSeconds` | `120` | Restarter 等待服务停止并再次启动的最长时间（秒），超时后放弃。范围为 `[1, 86400]`。**超过 240 的值实际上不可达：** Servy 宿主服务在 240 秒（4 分钟）后强制终止 `Servy.Restarter.exe`，当配置值超过该值时 Restarter 会在启动时记录警告。 |
| `General Logging` | - | 使用上文**常规日志**一节中的设置。 |

### 桌面应用设置

| 设置 | 默认 | 说明 |
| :--- | :--- | :--- |
| `ManagerAppPublishPath` | `.\Servy.Manager.exe` | 从桌面应用启动的 Servy Manager 应用路径。若非绝对路径，则相对于桌面应用的基目录解析。 |
| `General Logging` | - | 使用上文**常规日志**一节中的设置。 |

### Manager 应用设置

| 设置 | 默认 | 说明 |
| :--- | :--- | :--- |
| `RefreshIntervalInSeconds` | `4` | 主服务列表的刷新间隔（秒）。范围为 `[1, 3600]`。 |
| `PerformanceRefreshIntervalInMs` | `800` | CPU/RAM 图表更新频率（毫秒）。范围为 `[100, 300000]`。 |
| `ConsoleRefreshIntervalInMs` | `800` | Console 选项卡中 `stdout`/`stderr` 更新的轮询速率（毫秒）。范围为 `[100, 300000]`。 |
| `ConsoleMaxLines` | `20000` | Console 选项卡缓冲区中保留的最大 `stdout`/`stderr` 行数。范围为 `[100, 40000]`。 |
| `DependenciesRefreshIntervalInMs` | `800` | Dependencies 选项卡的轮询速率（毫秒）。范围为 `[100, 300000]`。 |
| `MaxBulkOperationParallelism` | `8` | 批量任务期间最大并发 SCM 操作数。范围为 `[1, 64]`。 |
| `SearchDebounceDelayMs` | `300` | Console 选项卡中搜索框重新运行过滤前的防抖窗口（毫秒）。范围为 `[100, 2000]`。 |
| `LogsWindowDays` | `3` | Logs 选项卡中显示的 Windows 事件日志历史天数。范围为 `[1, 30]`。 |
| `DesktopAppPublishPath` | `.\Servy.exe` | 从 Manager 启动的 Servy 桌面应用路径。若非绝对路径，则相对于 Manager 的基目录解析。 |
| `General Logging` | - | 使用上文**常规日志**一节中的设置。 |

> [!NOTE]
> 为防止 SCM 争用，实际批量操作并行度上限为：`Math.Max(1, Math.Min(Environment.ProcessorCount * 2, MaxBulkOperationParallelism))`。

### CLI 设置

| 设置 | 默认 | 说明 |
| :--- | :--- | :--- |
| `General Logging` | - | 使用上文**常规日志**一节中的设置。 |

## 构建配置示例

### 现代构建（.NET 10.0+）
**文件：** `%ProgramData%\Servy\appsettings.service.json`（Servy Windows 服务包装器）
```json
{
  "Timing": {
    "WaitChunkMs": 5000,
    "ScmAdditionalTimeMs": 15000
  },
  "LogLevel": "INFO",
  "EnableSizeRotation": true,
  "LogRotationSizeMB": 10,
  "LogRollingInterval": "Daily",
  "MaxBackupLogFiles": 10,
  "UseLocalTimeForRotation": true,
  "EnableEventLog": true
}
```

> [!TIP]
> 若只想禁用写入 Windows 事件日志，同时为所有 Servy 服务保留本地文件日志，可创建内容如下的 `%ProgramData%\Servy\appsettings.service.json` 文件：
> ```json
> {
>   "EnableEventLog": false
> }
> ```

### 旧版构建（.NET Framework 4.8）
**文件：** `%ProgramData%\Servy\Servy.Service.Net48.exe.config`（Servy Windows 服务包装器）
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <appSettings>
    <add key="Timing:WaitChunkMs" value="5000" />
    <add key="Timing:ScmAdditionalTimeMs" value="15000" />
    <add key="LogLevel" value="INFO" />
    <add key="EnableSizeRotation" value="true" />
    <add key="LogRotationSizeMB" value="10" />
    <add key="LogRollingInterval" value="Daily" />
    <add key="MaxBackupLogFiles" value="10" />
    <add key="UseLocalTimeForRotation" value="true" />
    <add key="EnableEventLog" value="true" />
  </appSettings>
</configuration>
```

## 另见
* [日志与日志轮转](./Logging-&-Log-Rotation)
* [Servy Manager](./Servy-Manager)
* [安装指南](./Installation-Guide)
