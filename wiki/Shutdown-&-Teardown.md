## 目录

1. [操作系统关机处理（v6.2+）](#操作系统关机处理v62)
1. [关机序列](#关机序列)
1. [主要特性](#主要特性)
1. [必须重新安装](#必须重新安装)
1. [延长关机时间窗口](#延长关机时间窗口)
1. [说明](#说明)

## 操作系统关机处理（v6.2+）

Servy v6.2+ 在操作系统关机与重启时引入高可靠性的服务拆解流程，符合 Windows 服务包装器所期望的优雅关闭行为。

## 关机序列

检测到系统关机或重启时，Servy 会执行专门的拆解工作流，以确保数据完整性与优雅退出。序列如下：

1. **关机前事件：** Servy 立即捕获来自 Windows 服务控制管理器（SCM）的 `SERVICE_CONTROL_PRESHUTDOWN` 信号。
2. **Pre-Stop 钩子：** 若已配置 Pre-Stop 钩子，则执行。默认同步运行以允许清理。但若 **Pre-Stop Timeout** 设为 **0**，钩子以 **fire-and-forget** 模式运行，Servy 立即进入下一步。
3. **进程终止：** Servy 停止受管子进程。先尝试优雅关闭（发送 `WM_CLOSE` 或 `CTRL_C_EVENT`），若进程未在配置的超时内退出，再强制终止。
4. **Post-Stop 钩子：** 若已配置 Post-Stop 钩子，则以 **fire-and-forget** 模式执行。该钩子在子进程退出后运行，但不阻塞服务完成自身关闭。
5. **日志与收尾：** Servy 将预关机序列成功完成记录到 Windows 事件日志，并正式转换到 `STOPPED` 状态。

## 主要特性

* **预关机注册：** 注册 `SERVICE_CONTROL_PRESHUTDOWN`。使 Servy 在操作系统发起关机或重启时立即开始清理，早于标准服务停止命令。
* **SCM 进度报告：** 通过等待提示与检查点定期向 Windows 服务控制管理器（SCM）报告进度，防止长时间清理任务被过早终止。
* **同步拆解：** 在并发收到 `STOP` 与 `PRESHUTDOWN` 控制码时防止竞态条件。

## 必须重新安装

> [!IMPORTANT]
> **兼容性：** 必须使用 Servy v6.2+ 重新安装服务。使用更早版本安装的服务在 SCM 数据库中缺少 `SERVICE_ACCEPT_PRESHUTDOWN` 标志，会忽略这些高优先级通知。

## 延长关机时间窗口

默认情况下，Servy 服务（v6.2+，已注册预关机通知）在 SCM 继续之前会获得全局 `PreshutdownTimeout` 窗口（标准 Windows 配置下为 180 秒）。Servy 动态发送等待提示与检查点以报告关机进度，防止操作系统在主动清理期间过早终止包装器。

### 方法 1：按服务超时覆盖（推荐）

**无需**编辑注册表或重启系统，即可为 Servy 管理的服务提供更长的关机窗口。Servy 通过 `ChangeServiceConfig2` 使用 `SERVICE_CONFIG_PRESHUTDOWN_INFO`，直接在 Windows 服务控制管理器（SCM）上自动配置按服务的预关机超时。

按服务的预关机超时在每次安装或更新服务时动态计算：

$$\text{Preshutdown Timeout} = \text{Baseline} + \text{PreStopTimeout} + \text{SCM Buffer}$$

其中：

* **Baseline：** 以下三者中的最大值：你配置的 `StopTimeout`、此前记录的历史停止耗时（本身上限为 86400 秒），以及下限 **5 秒**（`DefaultStopTimeout`）。低于下限的 `StopTimeout` 不会缩短该窗口。
* **PreStopTimeout：** 分配给 Pre-Stop 可执行钩子的最长时间；未配置 Pre-Stop 可执行文件时为 0。
* **SCM Buffer：** 强制增加的 15 秒安全余量（`ScmTimeoutBufferSeconds`），用于防止操作系统时序竞态。

例如，`-StopTimeout 60` 且 `-PreStopTimeout 30`、从未记录过更慢停止的服务，会注册为 `60 + 30 + 15 = 105` 秒的预关机超时。两者均保持默认时为 `5 + 5 + 15 = 25` 秒。

要为特定服务授予更长的关机窗口：

1. 通过 CLI、PowerShell cmdlet 或 Servy Manager UI 增大该服务的 `-StopTimeout`（若使用 Pre-Stop 钩子，也增大 `-PreStopTimeout`）。
2. 更新服务配置并重新安装。

SCM 会立即对该特定服务采用自定义预关机超时——**无需编辑注册表、无需系统重启，也不影响本机上的其他服务。**

> [!NOTE]
> 参数说明与用法示例请参见：
>
> * [Servy CLI 参考](./Servy-CLI)（`--stopTimeout`、`--preStopTimeout`）
> * [Servy PowerShell 模块参考](./Servy-PowerShell-Module)（`-StopTimeout`、`-PreStopTimeout`）
> * [停止前与停止后操作](./Pre-Stop-&-Post-Stop-Actions)

### 方法 2：全局系统注册表回退（整机）

若需提高接收预关机通知的非 Servy 服务的硬上限，或延长后续 `WaitToKillServiceTimeout` 阶段（该阶段管辖接收 `SERVICE_CONTROL_STOP` 的标准服务，在当前 Windows 版本上默认仅 **5 秒**），可修改全局注册表项。

由于服务控制管理器在系统启动时读取这些全局控制注册表项，应用更改后需要完整重启。

以下 PowerShell 脚本延长全局关机超时。以 **管理员**身份运行，将所需超时设为毫秒，然后重启系统。

```powershell
# 注册表路径
$registryPath = "HKLM:\SYSTEM\CurrentControlSet\Control"

# 超时（毫秒）（示例：80 秒）
$timeoutValue = 80000

# 1. 设置 PreshutdownTimeout（DWORD，毫秒）
if (Get-ItemProperty -Path $registryPath -Name "PreshutdownTimeout" -ErrorAction SilentlyContinue) {
    Set-ItemProperty -Path $registryPath -Name "PreshutdownTimeout" -Value $timeoutValue
} else {
    New-ItemProperty -Path $registryPath -Name "PreshutdownTimeout" -Value $timeoutValue -PropertyType DWord -Force
}

# 2. 设置 WaitToKillServiceTimeout（REG_SZ，毫秒）
# 注意：在 Windows 注册表中以字符串（REG_SZ）存储
Set-ItemProperty -Path $registryPath -Name "WaitToKillServiceTimeout" -Value "$timeoutValue"

Write-Host "Shutdown timeouts updated to $timeoutValue ms."
Write-Host "A system reboot is required for these changes to take effect."
```

## 说明

* **按服务 Preshutdown Timeout：** 通过 Servy 的 `StopTimeout` / `PreStopTimeout` 设置配置。使用 `SERVICE_CONFIG_PRESHUTDOWN_INFO` 直接向 SCM 注册。立即生效，无需重启。
* **PreshutdownTimeout（注册表）：** 控制 Windows 授予尚未设置显式按服务预关机超时的预关机处理服务（`SERVICE_CONTROL_PRESHUTDOWN`）的全局回退超时（毫秒）。
* **WaitToKillServiceTimeout（注册表）：** 控制 Windows 在强制终止无响应进程之前授予标准服务（`SERVICE_CONTROL_STOP`）的全局回退超时（毫秒）。
* **配置提示：** 将服务的 `StopTimeout` 设为超过最长运行受管进程及其相关关机钩子的最大预期耗时，以免 Windows 过早强制终止。
