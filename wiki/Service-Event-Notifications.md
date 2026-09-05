## 目录

1. [简介](#introduction)
1. [要求](#requirements)
   1. [通用](#common)
   1. [Toast 通知（`ServyFailureNotification.ps1`）](#for-toast-notifications-servyfailurenotificationps1)
   1. [电子邮件通知（`ServyFailureEmail.ps1`）](#for-email-notifications-servyfailureemailps1)
   1. [心跳 Ping URL 通知](#for-heartbeat-ping-url-notifications)
1. [通过任务计划程序设置 Toast 通知](#setup-toast-notifications-via-task-scheduler)
1. [手动通知（可选）](#manual-notification-optional)
1. [电子邮件通知](#email-notifications)
   1. [以 SYSTEM 身份生成凭据](#generating-credentials-as-system)
1. [心跳 Ping URL 通知](#heartbeat-ping-url-notifications)
1. [通过心跳 URL 的外部告警（Slack、Teams、电话等）](#external-alerts-via-heartbeat-urls-slack-teams-phone-call-etc)
   1. [如何将失败告警发送到 Slack、Microsoft Teams、电话或 WhatsApp？](#how-can-i-get-failure-alerts-sent-to-slack-microsoft-teams-phone-call-or-whatsapp)
1. [限制](#limitations)
1. [提示](#tips)
1. [故障排除](#troubleshooting)

## Introduction

Servy 可在托管服务失败时通过交互式 Windows toast 通知、发送电子邮件，或向外部监控服务发起带外 HTTP 心跳，通知用户。这使管理员无需持续监视应用界面即可立即察觉服务崩溃或健康检查失败。

## Requirements

### Common
* 必须已安装并运行 Servy。
* 能够访问 Windows 应用程序事件日志。

### For Toast notifications (`ServyFailureNotification.ps1`)
* Windows 10 或 11，Windows Server 2016+
* PowerShell 5.1+（Windows Runtime toast API）

### For Email notifications (`ServyFailureEmail.ps1`)
* Windows 7 SP1+ / Windows Server 2008 R2+
* PowerShell 5.1+（`Get-WinEvent`）
* 主机可访问的 SMTP 中继

### For Heartbeat Ping URL Notifications
* 到指定监控端点（例如 [healthchecks.io](https://healthchecks.io/)、Uptime Kuma、Pingdom）的直接或经代理路由的 HTTP/HTTPS 出站连接。

## Setup Toast Notifications via Task Scheduler

1. 照常安装 Servy。
2. 确保通知脚本位于：
    ```text
    %ProgramFiles%\Servy\taskschd\ServyFailureNotification.ps1
    ```
3. 导入计划任务：
      * 打开 **任务计划程序**。
      * 从右侧操作窗格点击 **导入任务…**。
      * 导航并选择：
        ```text
        %ProgramFiles%\Servy\taskschd\ServyFailureNotification.xml
        ```
      * 在 **常规** 选项卡中：
          * 确保选中 **“只在用户登录时运行”**（若用户未登录到活动桌面会话，Toast 无法呈现）。
          * 勾选 **“使用最高权限运行”**，以确保脚本有权限读取事件日志。

配置完成后，每当 Servy 向应用程序日志写入新的 Error 事件时，Windows 会自动触发此任务。

> [!IMPORTANT]
> 若使用 Servy 的便携版，必须编辑 `ServyFailureNotification.xml`，并将 `{SERVY_INSTALL_PATH}` 替换为包含 Servy 可执行文件的目录的绝对路径。

## Manual Notification (Optional)

您可以在 PowerShell 窗口中手动测试或运行通知脚本：
```powershell
& "C:\Program Files\Servy\taskschd\ServyFailureNotification.ps1"
```

脚本会自动解析事件日志中最新的 Servy 错误，并显示包含服务名称和具体错误消息的 toast。

> [!NOTE]
> 计划任务不受计算机的 PowerShell 执行策略影响——它们通过 `ServyFailureNotification.vbs` 运行，后者以 `-ExecutionPolicy Bypass` 调用 PowerShell。仅此手动调用受执行策略约束。若被阻止，请按任务相同的方式运行脚本，而不是更改计算机策略：
> ```powershell
> powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Program Files\Servy\taskschd\ServyFailureNotification.ps1"
> ```

## Email Notifications

对于用户可能没有活动桌面会话的服务器环境，可将 Servy 配置为发送电子邮件通知。

1. 照常安装 Servy。

2. 通过编辑 `%ProgramFiles%\Servy\taskschd\smtp-config.xml` 配置 SMTP 设置：

    ```xml
    <?xml version="1.0" encoding="UTF-8"?>
    <SmtpConfig>
      <Server>smtp.example.com</Server>
      <Port>587</Port>
      <UseSsl>true</UseSsl>
      <TimeoutMs>30000</TimeoutMs>
      <From>servy.notifications@example.com</From>
      <To>admin1@example.com;admin2@example.com</To>
    </SmtpConfig>
    ```

3. 为避免在明文中硬编码密码，电子邮件脚本需要加密的 XML 凭据文件。以管理员身份打开 PowerShell 并运行以下代码块：

    ```powershell
    $targetDir = "C:\Program Files\Servy\taskschd"
    $cred = Get-Credential
    $cred | Export-Clixml -Path (Join-Path $targetDir "smtp-cred.xml")
    ```

> [!IMPORTANT]
> **关键安全说明：** Windows 使用数据保护 API（DPAPI）加密此 XML 文件。这意味着该文件**只能由创建它的确切用户账户解密**。您必须在以将运行计划任务的同一用户账户登录时运行 `Get-Credential` 命令。若任务以 `SYSTEM` 运行，则必须以 `SYSTEM` 身份生成凭据。

### Generating credentials as SYSTEM

若您已明确将计划任务改为以 `NT AUTHORITY\SYSTEM` 运行（通过“更改用户或组…”——导入的任务默认使用导入它的账户），则必须从 SYSTEM 上下文的 PowerShell 会话生成 `smtp-cred.xml`。推荐方式是通过 [PsExec](https://learn.microsoft.com/sysinternals/downloads/psexec)：

```powershell
# From an elevated admin prompt, after downloading PsExec:
psexec.exe -i -s powershell.exe

# In the new SYSTEM-context window (verify with: whoami → "nt authority\system"):
$targetDir = "C:\Program Files\Servy\taskschd"
$cred = Get-Credential
$cred | Export-Clixml -Path (Join-Path $targetDir "smtp-cred.xml")
```

若无法使用 PsExec，请改为让计划任务在专用服务账户下运行——该账户随后可从其自身的登录会话生成 `smtp-cred.xml`。

4. 导入计划任务：

      * 打开 **任务计划程序**。
      * 点击 **导入任务…**
      * 选择：
        ```text
        %ProgramFiles%\Servy\taskschd\ServyFailureEmail.xml
        ```
      * 在 **常规** 选项卡中：
          * 选择 **“不管用户是否登录都要运行”**。
          * 勾选 **“使用最高权限运行”**。
          * 确保安全选项中指定的用户与步骤 3 中生成 `smtp-cred.xml` 的用户一致。

> [!IMPORTANT]
> 若使用 Servy 的便携版，必须编辑 `ServyFailureEmail.xml`，并将 `{SERVY_INSTALL_PATH}` 替换为包含 Servy 可执行文件的目录的绝对路径。

## Heartbeat Ping URL Notifications

除本地 toast 和电子邮件外，Servy 还支持向外部正常运行时间与健康监控服务（例如 [healthchecks.io](https://healthchecks.io/)、Uptime Kuma 或 Pingdom）直接发送实时带外通知 ping。

Servy 根据运行时生命周期状态执行异步 HTTP GET 调用：

* **启动通知（`/start`）：** 当启用 `--enableHeartbeatUrlFlags` 时，Servy 在服务启动时向 `https://hc-ping.com/your-uuid/start` 发出 ping，通知监控平台进程已初始化。
* **周期性健康检查 Ping：** 在稳态发现进程健康的健康检查会 ping 精确的基础 URL（`https://hc-ping.com/your-uuid`）以刷新正常运行时间计时器。在一次或多次失败检查之后的健康检查（当启用 `--enableHeartbeatUrlFlags` 时）会改为重新发送 `/start`，表示服务已恢复——完整 ping 规则见 [健康监控与恢复](./Health-Monitoring-&-Recovery)。
* **失败告警 Ping（`/fail`）：** 当子进程崩溃、健康检查重试失败或重启配额超出时，Servy 立即向 `https://hc-ping.com/your-uuid/fail` 发出 ping（当启用 `--enableHeartbeatUrlFlags` 时），以在已连接平台（Slack、PagerDuty、Teams、SMS）上触发即时告警。

```powershell
.\servy-cli install `
  --name="MyWorkerService" `
  --path="C:\Services\Worker.exe" `
  --enableHealth `
  --heartbeatInterval="60" `
  --maxFailedChecks="3" `
  --recoveryAction="RestartService" `
  --heartbeatUrl="https://hc-ping.com/your-uuid" `
  --heartbeatUrlTimeoutSeconds="10" `
  --enableHeartbeatUrlFlags
```

有关配置带外 ping 间隔和超时限制的完整详情，请参阅 [与监控工具集成](./Integration-with-Monitoring-Tools)。

## External Alerts via Heartbeat URLs (Slack, Teams, Phone Call, etc.)

### How can I get failure alerts sent to Slack, Microsoft Teams, Phone Call, or WhatsApp?

Servy 依赖带外监控服务传递实时事件告警。将 Servy 的 **Heartbeat URL** 指向如 [healthchecks.io](https://healthchecks.io/) 等 ping 提供商，后者可原生连接到下游通知平台。

1. 在监控服务（例如 `healthchecks.io`）中创建检查并复制其唯一 ping URL。
2. 将 URL 输入 Servy 的 **Heartbeat URL** 设置并启用 **Heartbeat URL Flags**。
3. 在监控服务仪表板中附加您偏好的集成通道。

当 Servy 发送 `/fail` 信号，或因主机宕机而停止 ping 时，监控平台会向您配置的通道触发告警，包括：

* **聊天与协作：** Slack、Microsoft Teams、Discord、Telegram、WhatsApp、Signal、Google Chat、Matrix、Mattermost、Rocket.Chat、Zulip。
* **事件管理与 Webhooks：** PagerDuty、Opsgenie、Splunk On-Call、Spike.sh、PagerTree、自定义 Webhooks。
* **直接通知：** 电子邮件、SMS、电话、Pushbullet、Pushover、ntfy、Gotify。
* **问题与事件跟踪：** GitHub Issues、Trello、Prometheus。

> [!TIP]
> 对于无需外部 ping 服务的本地或隔离告警，也可使用 Servy 的 **Failure Program Path**，在所有恢复尝试失败后执行本地脚本（例如调用 Slack Webhook 的 PowerShell 脚本）。

## Limitations

* **需要交互式会话：** Toast 通知严格在用户上下文（会话 1+）中运行。若任务以 `SYSTEM`（会话 0）运行，或用户完全注销，则不会显示。
* **依赖事件日志：** Toast 和电子邮件脚本的任务计划程序触发绑定到 Windows 事件日志。通知仅针对由 “Servy” 源记录且级别为 Error 的事件触发。
* **凭据可移植性：** `smtp-cred.xml` 文件无法复制到另一台机器或由另一用户账户使用，除非重新生成。
* **心跳 Ping 的网络连接：** Heartbeat URL ping 需要出站 HTTP/HTTPS 通信。没有互联网或代理路由的高安全性离线环境会丢弃带外 ping 尝试。

## Tips

* **状态跟踪：** PowerShell 脚本在 `taskschd` 目录中维护 `.dat` 时间戳文件（例如 `last-processed-toast.dat`），以确保不会对同一错误发送重复告警。若需测试通知管道，请删除此文件。**注意：** 下次运行时仅处理最近的单条错误；较旧的未读错误会被丢弃以防告警洪泛。若要重放历史，请将文件内容设为早于您想查看的事件的时间戳（ISO 8601 `o` 格式，例如 `2026-01-01T00:00:00.0000000Z`）。
* **自定义：** 您可以安全地编辑 `.ps1` 脚本，以自定义 toast 通知标题、图标或电子邮件 HTML 正文，以符合组织的监控标准。
* **纵深防御：** 组合使用 Toast、电子邮件和心跳 Ping URL。Toast 在本地通知开发人员，电子邮件通过收件箱通知系统管理员，心跳 Ping URL 则集成到自动化事件响应工具（PagerDuty、Slack）。

## Troubleshooting

* **症状：** 电子邮件从未发送，回退日志（`ServyFailureEmail.log`）显示 `"Key not valid for use in specified state"` 或类似的 `CryptographicException`。
* **原因：** 这表明 `smtp-cred.xml` 凭据文件由与计划任务“运行身份”不同的 Windows 账户加密。
* **修复：** 参阅 [以 SYSTEM 身份生成凭据](#generating-credentials-as-system) 一节，使用任务中配置的确切用户上下文重新生成凭据文件。
