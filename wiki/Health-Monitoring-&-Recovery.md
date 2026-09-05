## 目录

1. [简介](#introduction)
1. [健康监控设置](#health-monitoring-settings)
1. [CLI 选项](#cli-options)
1. [CLI 示例](#cli-example)
1. [PowerShell 选项](#powershell-options)
1. [PowerShell 示例](#powershell-example)
1. [健康监控工作原理](#how-health-monitoring-works)
   1. [瞬时检测（基于内存）](#transient-detection-memory-based)
   1. [恢复编排（守门人）](#recovery-orchestration-the-gatekeeper)
   1. [稳定性验证（基于持久化）](#stability-verification-persistence-based)
      1. [重启检测（会话持久化）](#reboot-detection-session-persistence)
      1. [自适应稳定窗口](#the-adaptive-stability-window)
   1. [心跳 Ping URL 逻辑](#heartbeat-ping-url-logic)
   1. [逻辑示例与阈值场景](#logic-examples--threshold-scenarios)
   1. [健康监控逻辑流程](#health-monitoring-logic-flow)
   1. [重启检测示例](#reboot-detection-example)
   1. [服务重启检测示例](#service-restart-detection-example)

## Introduction

Servy 内置**健康监控**，用于确保服务正常运行，并在故障时自动恢复。

如果您刚开始使用，以下是 Servy 健康监控器的核心行为：

- **心跳即存活检查：** Servy 默认每 **30 秒**检查一次进程是否正在运行并响应。
- **仅连续失败才恢复：** 仅当服务**连续**失败 $N$ 次时才会触发恢复。一次成功的心跳会将瞬时失败计数器重置为零。
- **“抖动”保护：** Servy 不会立即忘记失败。持久化重启计数器仅在服务稳定（无故障运行）达到特定**试用期**后才会重置为零。
- **重启持久化：** 如果 Servy 重启计算机以修复服务，它会**记住**跨重启已尝试修复的次数。这可防止机器进入无限重启循环。
- **安全停止：** 一旦达到 `MaxRestartAttempts` 限制，Servy 停止尝试，运行可选的 `FailureProgram`，并将服务保持为**已停止**状态，以便人工检查。

## Health Monitoring Settings

桌面应用中的“恢复”选项卡用于配置自动健康监控与故障处理行为。它支持持续的内部存活检查、向外部监控平台（例如 [healthchecks.io](https://healthchecks.io) 或 [Uptime Kuma](https://github.com/louislam/uptime-kuma) Push Monitors）发送可选的带外诊断心跳，以及可自定义的恢复操作，从而在无需人工干预的情况下保持应用可靠运行。

<img alt="servy-config-recovery" src="https://github.com/user-attachments/assets/84c1efba-096c-4573-88e6-5ca5b4c61a4c" />

可用的恢复选项如下：

  - **Heartbeat Interval**：可选，默认 30 秒。Servy 检查服务是否响应的频率。最小为 5 秒。心跳是内部存活检查（进程存在与状态），不是应用层健康探针。
  - **Max Failed Checks**：可选，默认 3。触发恢复操作前允许的连续失败检查次数。最小为 1。
  - **Recovery Action**：可选。定义健康检查失败时的行为。选项：
    - `RestartService`（默认）：重启服务
    - `RestartProcess`：仅重启进程，不进行完整服务重启
    - `RestartComputer`：重启主机
    - `None`：不采取任何操作
  - **Recovery On Clean Exit**：可选。即使子进程以干净退出码（0）优雅退出，也触发已定义的恢复操作。默认情况下，干净退出会有意停止服务且不触发恢复。
  - **Max Restart Attempts**：可选，默认 3。最小为 0；设为 0 表示无限重启尝试。放弃前的最大恢复尝试次数。
  - **Heartbeat URL**：可选字符串。绝对 HTTP/HTTPS URL（例如 `https://hc-ping.com/uuid`），用于向外部监控服务（如 healthchecks.io 或 Uptime Kuma）发送带外诊断心跳。
  - **Heartbeat URL Timeout**：可选整数，默认 10 秒（范围：2–30 秒）。心跳 HTTP GET 请求的超时时间。
  - **Heartbeat URL Flags**：可选开关/布尔值。在服务启动时追加 `/start`，在恢复失败时追加 `/fail` 到 Heartbeat URL（例如将 `https://hc-ping.com/your-uuid` 变为 `https://hc-ping.com/your-uuid/start` 或 `https://hc-ping.com/your-uuid/fail`）。
  - **Failure Program Path**：可选失败程序，在所有恢复尝试失败后运行（以及在健康监控禁用时，当子进程以非零退出码退出时运行）。
  - **Failure Startup Directory**：可选失败程序启动目录。
  - **Failure Program Parameters**：可选失败程序参数。

若禁用健康监控，失败程序会在**子进程以非零退出码退出**时运行。若启用健康监控，失败程序仅在所有恢复操作重试失败后运行。注意：**启动失败（例如可执行路径缺失）当前不会触发失败程序**——请查看 Servy Manager 的 Logs 选项卡，或 Windows 事件日志中 Source 为 `Servy` 的记录。

**环境变量：** 失败程序继承**主服务**的 `EnvironmentVariables`。没有单独的 `Post-Launch / Pre-Stop / Post-Stop / Failure-Program EnvironmentVariables` 设置——仅 `Pre-Launch` 支持按钩子覆盖。

> [!WARNING]
> 将 Max Restart Attempts 设为 0（无限重试）会绕过 FailureProgram 路径，因为“配额已达”条件永远不会满足。

## CLI Options

- `--enableHealth`：启用健康监控。
- `--heartbeatInterval`：心跳间隔（秒）。
- `--maxFailedChecks`：允许的最大失败健康检查次数。
- `--recoveryAction`：失败时的恢复操作。选项：`None`、`RestartService`、`RestartProcess`、`RestartComputer`。
- `--recoveryOnCleanExit`：即使进程以干净退出码（0）退出也启用恢复操作。（*自 Servy 8.4 起可用*）
- `--maxRestartAttempts`：失败时的最大重启尝试次数。
- `--heartbeatUrl`：带外诊断心跳的绝对 URL。
- `--heartbeatUrlTimeoutSeconds`：心跳 URL 请求超时（秒）。
- `--enableHeartbeatUrlFlags`：在服务启动时追加 `/start`，在恢复失败时追加 `/fail` 到 Heartbeat URL。
- `--failureProgramPath`：所有恢复尝试失败时执行的可选程序路径。
- `--failureProgramStartupDir`：失败程序的可选工作目录。
- `--failureProgramParams`：传递给失败程序的可选参数。

> [!NOTE]
> 必须启用健康监控（`--enableHealth`）才能使用 `--heartbeatUrl` 或恢复选项。未启用 `--enableHealth` 时设置恢复参数无效。

## CLI Example

```powershell
servy-cli install `
  --name="MyNodeService" `
  --description="My NodeJS Server" `
  --path="C:\Program Files\nodejs\node.exe" `
  --startupDir="C:\Apps\App" `
  --params="C:\Apps\App\index.js" `
  --startupType="Automatic" `
  --enableHealth `
  --heartbeatInterval="10" `
  --maxFailedChecks="3" `
  --recoveryAction="RestartProcess" `
  --recoveryOnCleanExit `
  --maxRestartAttempts="3" `
  --heartbeatUrl="https://hc-ping.com/your-uuid-here" `
  --heartbeatUrlTimeoutSeconds="5" `
  --enableHeartbeatUrlFlags `
  --failureProgramPath="C:\Apps\FailureHandler.exe" `
  --failureProgramStartupDir="C:\Apps" `
  --failureProgramParams="-log C:\Logs\failure.log"
```

## PowerShell Options

以下参数是在使用 `Install-ServyService` 并配合 splatting 时，与 CLI 健康监控选项对应的 PowerShell 参数。

- `-EnableHealth`：为服务启用健康监控。这是开关参数。
- `-HeartbeatInterval`：心跳间隔（秒）。
- `-MaxFailedChecks`：触发恢复前允许的最大连续失败健康检查次数。
- `-RecoveryAction`：失败时的恢复操作。有效值：`None`、`RestartService`、`RestartProcess`、`RestartComputer`。
- `-RecoveryOnCleanExit`：开关参数，即使进程干净退出（退出码 0）也触发恢复操作。（*自 Servy 8.4 起可用*）
- `-MaxRestartAttempts`：服务停止前的最大恢复尝试次数。
- `-HeartbeatUrl`：字符串，可选。用于心跳的绝对 HTTP/HTTPS URL（例如 `https://hc-ping.com/uuid`）。
- `-HeartbeatUrlTimeoutSeconds`：心跳 URL 请求超时（秒）。
- `-EnableHeartbeatUrlFlags`：开关参数，可选。在服务启动时追加 `/start`，在恢复失败时追加 `/fail` 到 Heartbeat URL。
- `-FailureProgramPath`：所有恢复尝试失败后执行的可选程序路径。
- `-FailureProgramStartupDir`：失败程序的可选工作目录。
- `-FailureProgramParams`：传递给失败程序的可选参数。

> [!NOTE]
> 必须启用健康监控（`-EnableHealth`）才能使用 `-HeartbeatUrl` 或恢复选项。未启用 `-EnableHealth` 时设置恢复参数无效。

## PowerShell Example

```powershell
Import-Module "C:\Program Files\Servy\Servy.psm1" -Force

$installParams = @{
    Name                     = "MyNodeService"
    Description              = "My NodeJS Server"
    Path                     = "C:\Program Files\nodejs\node.exe"
    StartupDir               = "C:\Apps\App"
    Params                   = "C:\Apps\App\index.js"
    StartupType              = "Automatic"

    EnableHealth             = $true
    HeartbeatInterval        = 10
    MaxFailedChecks          = 3
    RecoveryAction           = "RestartService"
    RecoveryOnCleanExit      = $true
    MaxRestartAttempts       = 3

    HeartbeatUrl             = "https://hc-ping.com/your-uuid-here"
    HeartbeatUrlTimeoutSeconds = 5
    EnableHeartbeatUrlFlags  = $true

    FailureProgramPath       = "C:\Apps\FailureHandler.exe"
    FailureProgramStartupDir = "C:\Apps"
    FailureProgramParams     = "-log C:\Logs\failure.log"
}

Install-ServyService @installParams
```

## How Health Monitoring Works

Servy 实现了**分层恢复架构**，旨在最大化正常运行时间，同时保护主机系统免于无限重启循环。逻辑分为三层：**瞬时检测（基于内存）**、**恢复编排（守门人）** 和 **稳定性验证（基于持久化）**。

### Transient Detection (Memory-Based)

服务使用周期性心跳定时器监控子进程，以过滤**噪声**和瞬时故障。

- **失败检查：** 若进程缺失、已崩溃，或在启用 `RecoveryOnCleanExit` 时干净退出，则基于内存的计数器（`_failedChecks`）递增。（默认情况下，退出码 0 的干净退出会有意停止服务。）
- **瞬时重置：** 若进程恢复健康且当前失败计数大于 0，内存计数器立即重置为 `0`。这确保偶尔错过的心跳不会长期累积；仅**连续**失败才会触发恢复。
- **持久化同步：** 当进程健康时，仅当判定 `restartAttempts` 文件**已过期**时，才将其重置为 `0`。若其最后修改时间（最后一次记录的失败）早于计算出的**自适应稳定窗口**，则视为过期。这确保失败历史仅在经过验证的持续正常运行后才清除，而不会被“抖动”服务的一次“幸运”心跳抹掉。
- **阈值：** 仅当连续失败次数达到 `MaxFailedChecks` 时才触发恢复。这可避免因短暂的 OS 抖动或进程响应缓慢而进行不必要的重启。
- **目的：** 这种双重重置策略使看门狗对即时崩溃敏感，同时对间歇性、非关键故障保持宽容。

### Recovery Orchestration (The Gatekeeper)

当达到失败阈值时，系统进入受管的**恢复状态**。

- **守门人模式：** 线程安全标志（`_isRecovering`）会阻止进一步的健康检查，直到恢复操作（重启进程、服务或计算机）完全完成。
- **配额管理：** 执行恢复前，服务检查基于文件的持久化计数器。若已达到 `MaxRestartAttempts`，服务执行 `FailureProgram` 并停止服务，以便人工干预。

失败程序仅在持久化重启计数器达到 `MaxRestartAttempts` 后执行，不会在中间恢复尝试期间执行。

### Stability Verification (Persistence-Based)

这是最关键的一层。它为每个服务管理磁盘上的 `restartAttempts` 文件。与瞬时计数器不同，成功的心跳**不会**立即重置该计数器。

- **稳定性重置：** 持久化计数器仅在服务成功运行超过**自适应稳定窗口**后才重置为 `0`。
- **逻辑：** 若文件的“Last Write Time”早于计算出的阈值，Servy 认为服务**稳定**并清除失败历史。

**专业提示：** 在完成维护后若要手动重置失败计数器，可删除服务数据目录中的 `restartAttempts` 文件（`%ProgramData%\Servy\recovery\ServiceName_shortHash_restartAttempts.dat`）。这会绕过稳定窗口并允许干净启动。

#### Reboot Detection (Session Persistence)

看门狗将上次重启尝试的时间戳与**系统启动时间**进行比较。

- **逻辑：** 若文件在当前 OS 会话之前被修改，则本次评估跳过重置，并将文件时间戳重新锚定到当前会话。失败计数在重启后保留，自适应稳定窗口从启动起重新计时——仅在新会话中服务保持健康达到完整窗口后，计数器才会清除。
- **目的：** 确保若服务触发了 `RestartComputer` 操作，机器重新启动后仍“记住”该次尝试。这可防止利用重启绕过恢复配额。

#### The Adaptive Stability Window

为防止“抖动”（进程启动后很快崩溃），持久化计数器仅在服务存活超过计算出的**试用期**后才重置为 `0`。

看门狗计算将持久化重启计数器重置为零所需的稳定运行时间。该公式在高频“抖动”保护与长期运维合理性之间取得平衡。

$$Threshold =
\begin{cases}
D + PreLaunchTimeout & \text{if } D > 3600 \\
\max(\min(D + \max(D, 30), 3600), D) + PreLaunchTimeout & \text{if } D \le 3600
\end{cases}$$

其中：

- **检测窗口**（$D$）：`HeartbeatInterval` × `MaxFailedChecks`。检测服务故障所需的最短时间。
- **缓冲：** $\max(D, 30s)$。额外安全余量，确保服务在检测范围之外已稳定。
- **$3600$（运维人员理智上限）：** 1 小时限制（3600 秒），确保管理员不必为慢脉搏服务无限等待计数器重置。
- **安全下限**（$D$）：确保最终重置阈值从不短于检测窗口本身，保证服务在清除故障历史前至少存活一个完整的健康周期。
- **预启动超时：** 若启用预启动，阈值会延长 `PreLaunchTimeoutInSeconds`，以防止缓慢的初始化周期消耗稳定性评估预算。

### Heartbeat Ping URL Logic

当配置了 `HeartbeatUrl` 且启用健康监控时，Servy 按以下规则向配置的外部监控端点发送异步 HTTP GET 心跳：

- **服务生命周期启动：** 在启动序列中，若启用了扩展标志（`EnableHeartbeatUrlFlags`），Servy 会向 `HeartbeatUrl` 追加 `/start`（例如 `https://hc-ping.com/your-uuid/start`）。
- **健康检查循环通过（稳态）：** 当健康检查发现进程健康且连续失败计数器已为 `0` 时，Servy 向精确的基础 `HeartbeatUrl` 发送 ping（例如 `https://hc-ping.com/your-uuid`）。
- **健康检查循环通过（已恢复）：** 当健康检查在一次或多次失败检查后发现进程健康时，Servy 也会追加 `/start`，表示服务已恢复。这与启动 ping 使用相同标志，因此外部监控会看到新的运行开始。
- **健康检查循环失败 / 进程崩溃：** 在触发已配置的恢复操作之前、进程崩溃时，或重启尝试配额耗尽时，若启用了扩展标志（`EnableHeartbeatUrlFlags`），Servy 会向 `HeartbeatUrl` 追加 `/fail` 并发送 ping（例如 `https://hc-ping.com/your-uuid/fail`）。

### Logic Examples & Threshold Scenarios

下表展示看门狗如何适配不同的心跳配置：

| Heartbeat Interval | Max Failed Checks | 检测窗口 ($D$) | 缓冲 (max(D, 30s)) | 最终重置阈值 | 应用的策略 |
| --- | --- | --- | --- | --- | --- |
| **5 Seconds** | 3 | 15 Seconds | 30 Seconds | **45 Seconds** | **Buffered：** $D$ + 30s 最小安全余量。 |
| **5 Seconds** | 1 | 5 Seconds | 30 Seconds | **35 Seconds** | **Buffered：** $D$ + 30s 最小安全余量。 |
| **10 Seconds** | 3 | 30 Seconds | 30 Seconds | **60 Seconds** | **Proportional：** $D$ + 30s 最小安全余量。 |
| **1 Minute** | 3 | 3 Minutes ($180s$) | 3 Minutes ($180s$) | **6 Minutes** ($360s$) | **Proportional：** 标准 $2 \times D$ 窗口。 |
| **15 Minutes** | 3 | 45 Minutes ($2700s$) | 45 Minutes ($2700s$) | **60 Minutes** ($3600s$) | **Capped：** 1 小时运维理智上限。 |
| **1 Hour** | 3 | 3 Hours ($10800s$) | 3 Hours ($10800s$) | **3 Hours** ($10800s$) | **Floored（不推荐）：** 检测窗口超过上限；回退为 $D$。 |
| **1 Day** | 3 | 3 Days ($259200s$) | 3 Days ($259200s$) | **3 Days** ($259200s$) | **Floored（不推荐）：** 检测窗口超过上限；回退为 $D$。 |

> [!WARNING]
> **Floored 配置：** 若 `HeartbeatInterval × MaxFailedChecks` 超过 3600 秒（1 小时），检测窗口本身已超过重置上限。Servy 回退为使用 $D$ 作为阈值，并在每次稳定性评估时记录警告。请将 `HeartbeatInterval × MaxFailedChecks` 保持在 3600 秒及以下，以处于推荐的运维范围内。

**预启动注意事项：**

若配置了预启动脚本（设置了 `PreLaunchExecutablePath`），稳定性计时器在进程完成初始化阶段之前实际上不会开始。

例如，若服务的**检测窗口为 1 分钟**（$60\text{s}$），其基础稳定性要求为 **2 分钟**（$120\text{s}$）。若加载数据库依赖需要 **5 分钟**（$300\text{s}$）（`PreLaunchTimeout`），则最终重置阈值为 **7 分钟**（$420\text{s}$）。这种配置架构隔离了执行路径，确保预热延迟不会惩罚稳定性跟踪。

### Health Monitoring Logic Flow

每个心跳周期，Servy 遵循以下决策流程：

1. **心跳检查**
   - Servy 检查子进程的存活状态（进程存在与响应性）。

2. **若健康：**
   - **瞬时重置：** 基于内存的 `_failedChecks` 计数器立即重置为 `0`。
   - **Ping URL：** Servy 向 `HeartbeatUrl` 发送标准 HTTP GET ping（若已配置）。
   - **稳定性检查：** Servy 将当前时间与 `restartAttempts` 文件的“Last Write Time”进行比较。
   - **持久化重置：** 若时长超过**自适应稳定窗口**，基于文件的 `restartAttempts` 重置为 `0`。

3. **若不健康：**
   - **瞬时递增：** 基于内存的 `_failedChecks` 计数器递增。
   - **阈值检查：** 若 `_failedChecks < MaxFailedChecks`，本周期结束（等待下一次心跳）。
   - **守门人检查：** 若达到阈值，Servy 检查是否已有恢复在进行（`_isRecovering`）。若为真，本周期结束，以防止重叠的恢复操作。

4. **恢复编排：**
   - **失败信号：** 若启用 `EnableHeartbeatUrlFlags`，Servy 向 `HeartbeatUrl` 发送 `/fail` HTTP GET ping。
   - **配额检查：** Servy 从磁盘读取持久化的 `restartAttempts`。
   - **失败状态：** 若 `restartAttempts >= MaxRestartAttempts`，已达恢复限制。Servy 执行 `FailureProgram`（若已配置）并**停止服务**，以便人工干预。
   - **操作状态：** 若未达限制：
     - 递增持久化的 `restartAttempts` 并更新文件的 Last Write Time。
     - 设置 `_isRecovering = true`。
     - 执行已配置的 `RecoveryAction`（重启进程、服务或计算机）。
   - **重启持久化：** 若操作为 `RestartComputer`，持久化时间戳保留在磁盘上。系统重启后，“稳定性检查”（步骤 2）会看到最近的时间戳并保留失败计数，从而防止重启循环。

### Reboot Detection Example

考虑如下配置的服务：

- **Heartbeat Interval**：10 秒
- **Max Failed Checks**：3
- **Max Restart Attempts**：3
- **Recovery Action**：`RestartComputer`
- **Failure Program**：`C:\Apps\FailureHandler.exe`

#### 时间线

**会话 1：第一次崩溃序列**

1. **12:00** 服务正常启动。`restartAttempts = 0`
2. **12:05** 服务反复崩溃并超过 `MaxFailedChecks`
   - Servy 触发 `RestartComputer`
   - `restartAttempts` 递增为 1 并写入磁盘
   - `restartAttempts` 在尝试恢复操作之前递增
3. **12:06** 系统重启

**会话 2：第二次崩溃序列**

4. **12:10** Windows 完成重启。Servy 自动启动
   - 加载 `restartAttempts = 1`
   - 文件 Last Write Time 早于当前 OS 启动时间，重启检测阻止重置
5. **12:15** 服务再次崩溃
   - Servy 触发 `RestartComputer`
   - `restartAttempts` 递增为 2
6. **12:16** 系统再次重启

**会话 3：第三次崩溃序列**

7. **12:20** Windows 完成重启。Servy 自动启动
   - 加载 `restartAttempts = 2`
8. **12:25** 服务崩溃
   - Servy 触发 `RestartComputer`
   - `restartAttempts` 递增为 3
9. **12:26** 系统再次重启

**会话 4：第四次崩溃序列**

10. **12:30** Windows 完成重启。Servy 自动启动
    - 加载 `restartAttempts = 3`（已达到 `MaxRestartAttempts`）
11. **12:35** 服务再次崩溃
    - Servy 发现 `restartAttempts` 已达到 `MaxRestartAttempts` 限制
    - 不执行恢复操作
    - 执行已配置的失败程序（`C:\Apps\FailureHandler.exe`）
    - Servy 停止服务以防止无限重启循环

#### 要点

- `restartAttempts` 跨重启持久化。每次重启仅在每次恢复操作时递增一次
- `MaxRestartAttempts` 限制可防止无尽的重启循环
- 达到限制后，Servy 在已配置时运行失败程序并停止服务
- 操作人员可在第四次崩溃后进行人工干预

### Service Restart Detection Example

考虑如下配置的服务：

- **Heartbeat Interval**：10 秒
- **Max Failed Checks**：3
- **Max Restart Attempts**：3
- **Recovery Action**：`RestartService`
- **Failure Program**：`C:\Apps\FailureHandler.exe`

#### 时间线

**会话 1：第一次崩溃序列**

1. **12:00** 服务正常启动。`restartAttempts = 0`
2. **12:05** 服务反复崩溃并超过 `MaxFailedChecks`
   - Servy 触发 `RestartService`
   - `restartAttempts` 递增为 1 并写入磁盘
   - `restartAttempts` 在尝试恢复操作之前递增
3. **12:06** 服务成功重启

**会话 2：第二次崩溃序列**

4. **12:10** 服务再次崩溃
   - Servy 触发 `RestartService`
   - `restartAttempts` 递增为 2
5. **12:11** 服务成功重启

**会话 3：第三次崩溃序列**

6. **12:15** 服务再次崩溃
   - Servy 触发 `RestartService`
   - `restartAttempts` 递增为 3
7. **12:16** 服务成功重启

**会话 4：第四次崩溃序列**

8. **12:20** 服务再次崩溃
   - Servy 发现 `restartAttempts` 已达到 `MaxRestartAttempts` 限制
   - **不**执行恢复操作
   - 执行已配置的失败程序（`C:\Apps\FailureHandler.exe`）
   - Servy 停止服务以防止无尽的重启循环
