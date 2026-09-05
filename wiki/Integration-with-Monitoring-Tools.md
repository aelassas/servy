## 目录

1. [简介](#introduction)
1. [支持的集成](#supported-integrations)
1. [CLI 示例：与 CI/CD 流水线集成](#cli-example-integrating-with-cicd-pipeline)
1. [心跳 URL 与带外 Ping](#heartbeat-url--out-of-band-pings)
   1. [配置参数](#configuration-parameters)
   1. [运行时执行规则](#operational-execution-rules)
   1. [与 Healthchecks.io 集成](#integrating-with-healthchecksio)

## Introduction

Servy 可与外部监控和自动化工具集成，以简化服务管理、告警和 CI/CD 工作流。这使您能够在企业环境中跟踪服务健康、日志和生命周期事件。

## Supported Integrations

- **Healthchecks.io 与正常运行时间监控服务（Uptime Kuma、Pingdom、Better Stack）**
  - 通过带外通道直接向您的监控端点发送异步 HTTP GET ping。
  - 使用扩展生命周期标志（`/start`、`/fail`）自动报告服务启动、健康的周期性检查以及失败/恢复信号。

- **Jenkins、TeamCity、Azure DevOps、GitHub Actions**
  - 使用 **Servy CLI** 作为 CI/CD 流水线的一部分安装、启动、停止或卸载服务。
  - 使用预启动脚本、环境变量和依赖项自动化部署服务。
  - 参见完整指南：[Servy 自动化与 CI/CD](./Servy-Automation-&-CI-CD)

- **Prometheus、Grafana 与日志转发器**
  - 健康检查事件、进程生命周期转换和诊断条目会写入 **Windows 事件查看器**（在 **Servy** 事件日志下）以及 Servy 的中央日志文件：
    ```text
    %ProgramData%\Servy\logs\Servy.Service.log
    ```
  - 示例日志条目：
    ```text
    [MyApp] Health monitoring started.
    [MyApp] Health check failed (1/3).
    [MyApp] Health check failed (2/3).
    [MyApp] Health check failed (3/3). Initiating recovery.
    [MyApp] Started child process with PID: 17852
    ```
  - **监控与自动化触发：**
    - **日志转发：** 如 **Promtail**、**Vector** 或 **Logstash** 等工具可跟踪 `Servy.Service.log`，以流式传输指标、触发告警并构建 Grafana 仪表板（跟踪正常运行时间、重启次数和失败率）。
    - **事件查看器抓取：** 原生 Windows 日志转发器或 Prometheus `windows_exporter` 可直接从 **Servy** 日志抓取事件日志条目。
    - **失败操作：** 在重复的健康检查失败时，Servy 可自动触发恢复操作——**Restart Service**（默认）、**Restart Process**、**Restart Computer** 或 **None**——执行外部脚本（`FailureProgramPath`），或 ping webhook（`HeartbeatUrl`），在启用 `EnableHeartbeatUrlFlags` 时追加 `/fail`。

- **告警工具**
  - Servy 支持 [服务事件通知](./Service-Event-Notifications)，在服务失败时提供 Windows  toast 通知和电子邮件告警。
  - 这确保用户或管理员无需持续监视服务即可立即收到通知。
  - 对于自定义告警，您可以触发与 Slack、Teams 或其他消息平台集成的脚本。

- **Windows 事件查看器 / SIEM**
  - 所有重要服务事件都记录在 Windows 事件查看器中。
  - 与 SIEM 工具（Splunk、ELK、Graylog）集成，实现集中式日志聚合与告警。

## CLI Example: Integrating with CI/CD Pipeline

```powershell
.\servy-cli install `
  --name="MyNodeApp" `
  --description="Node.js API Service" `
  --path="C:\Program Files\nodejs\node.exe" `
  --startupDir="C:\Apps\MyNodeApp" `
  --params="C:\Apps\MyNodeApp\server.js" `
  --startupType="Automatic" `
  --enableHealth `
  --heartbeatInterval="10" `
  --maxFailedChecks="3" `
  --recoveryAction="RestartService" `
  --stdout="C:\Logs\MyNodeApp_stdout.log" `
  --stderr="C:\Logs\MyNodeApp_stderr.log" `
  --enableSizeRotation `
  --rotationSize="10"
```

> [!TIP]
> 在 CI/CD 脚本中使用 Servy CLI，可可靠地部署服务、监控其健康状况，并与企业监控和告警工具集成。

有关告警，请参阅 [服务事件通知](./Service-Event-Notifications)。

## Heartbeat URL & Out-of-Band Pings

Servy 允许服务通过 HTTP/HTTPS GET 请求向外部正常运行时间与健康监控平台（例如 [healthchecks.io](https://healthchecks.io/)、Uptime Kuma 或 Pingdom）发送带外诊断心跳。

### Configuration Parameters

- **Heartbeat URL**：可选字符串。用于向外部监控服务发送带外诊断心跳的绝对 HTTP/HTTPS URL（例如 `https://hc-ping.com/your-uuid`）。
- **Heartbeat URL Timeout**：可选整数，默认为 `10` 秒（范围：`2`-`30` 秒）。心跳 HTTP GET 请求的超时秒数。
- **Heartbeat URL Flags**：可选开关/布尔值。在服务启动时追加 `/start`，在恢复失败时追加 `/fail` 到 Heartbeat URL。

### Operational Execution Rules

当配置了 `HeartbeatUrl` 且启用健康监控（`--enableHealth`）时，Servy 在服务启动（`/start`）、每次稳态成功健康检查（基础 URL）、失败后的首次成功检查（`/start`，表示恢复）以及进程失败或恢复触发（`/fail`）时，向外部端点发送异步 HTTP GET ping。

有关 ping 条件、超时和状态管理的完整详情，请参阅规范文档 [心跳 Ping URL 逻辑](./Health-Monitoring-&-Recovery#heartbeat-ping-url-logic)。

### Integrating with Healthchecks.io

要将 Servy 管理的 Windows 服务与 [healthchecks.io](https://healthchecks.io/) 集成：

1. **在 Healthchecks.io 中创建检查：**
   - 将检查间隔设置为与 Servy 的 `--heartbeatInterval` 匹配或略长（例如每 60 秒，宽限期 20 秒）。
   - 复制检查的唯一 ping URL（例如 `https://hc-ping.com/5f8c8d82-3d2d-4b82-9f0a-123456789abc`）。

2. **使用 CLI 标志配置 Servy：**
   ```powershell
   .\servy-cli install `
     --name="MyWorkerService" `
     --path="C:\Services\MyWorker.exe" `
     --enableHealth `
     --heartbeatInterval="60" `
     --maxFailedChecks="3" `
     --recoveryAction="RestartService" `
     --heartbeatUrl="https://hc-ping.com/5f8c8d82-3d2d-4b82-9f0a-123456789abc" `
     --heartbeatUrlTimeoutSeconds="10" `
     --enableHeartbeatUrlFlags
   ```
