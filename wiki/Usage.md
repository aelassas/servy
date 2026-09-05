## 目录

1. [简介](#简介)
1. [快速开始](#快速开始)
1. [服务配置](#服务配置)
1. [日志与日志轮转](#日志与日志轮转)
1. [健康监控与恢复](#健康监控与恢复)
1. [高级配置](#高级配置)
1. [登录与安全](#登录与安全)
1. [启动与停止钩子](#启动与停止钩子)
1. [管理服务](#管理服务)
1. [另请参阅](#另请参阅)

## 简介

本页全面介绍如何使用 Servy 配置、安装和管理服务。无论使用 Servy（GUI）、CLI 还是 PowerShell，底层原则相同。

Servy 是服务包装器，可将任意可执行文件、脚本或运行时（Node.js、Python、Java 等）转换为受管 Windows 服务。它提供自动恢复、日志轮转、CPU 与内存监控以及生命周期钩子等企业级功能，这些是原生 Windows 服务所不具备的。

## 快速开始

> [!IMPORTANT]
> 安装服务前，请确保以管理员权限运行，且目标账户对 `%ProgramData%\Servy` 以及应用所需的任何文件夹具有所需访问权限。

1. **启动** `Servy.exe`（Servy GUI）。
2. **命名服务**：在 **Service Name** 字段中输入唯一的服务名称。
3. **选择可执行文件**：在 **Process Path** 中提供二进制文件的完整路径。
4. **配置可选设置**：按需设置 **Process Parameters**、**Startup Directory** 或 **Description**。
5. **安装**：点击 **Install** 按钮，向 Windows Service Control Manager（SCM）注册服务。
6. **启动**：点击 **Start**，立即上线服务。

## 服务配置

> [!IMPORTANT]
> 若服务在非 Local System 账户下运行，必须为该服务账户授予 `%ProgramData%\Servy` 的 **Modify** 权限，并运行强制加固脚本（`Set-ServyExePermissions.ps1 -TargetAccount "domain\user"`），将二进制可执行文件锁定为 **Read & Execute**，以防止未授权篡改二进制文件与本地权限提升。脚本位置与执行说明见[可执行文件权限加固指南](./Security#executable-permission-hardening-mandatory)。

### 主要设置
* **Service Name（必填）**：Windows 中唯一的服务名称。
* **Display Name**：在 `services.msc` 中显示的可读名称。
* **Description**：服务用途的简要说明。
* **Process Path（必填）**：可执行文件的完整路径（例如 `C:\Program Files\nodejs\node.exe`）。支持环境变量展开。
* **Startup Directory**：进程的工作目录。默认可执行文件所在文件夹。
* **Process Parameters**：传递给可执行文件的命令行参数。
* **Startup Type**：在 `Automatic (Default)`、`Automatic (Delayed start)`、`Manual` 或 `Disabled` 中选择。
* **Process Priority**：在 `Idle`、`Below Normal`、`Normal (Default)`、`Above Normal`、`High` 或 `Real Time (Use with caution)` 中选择。
* **CPU Affinity**：进程可运行的逻辑 CPU（例如 `0-3,8` 或 `0xFF00`）。更多说明见此[常见问题](./FAQ#how-and-why-should-i-use-cpu-affinity-with-servy)。
* **超时**：
    * **Start Timeout**：等待成功启动的秒数（默认：`10s`）。
    * **Stop Timeout**：在强制终止前等待进程优雅退出的秒数（默认：`5s`）。
* **Enable Console UI**：用于交互式控制台应用；禁用 stdout/stderr 重定向。

## 日志与日志轮转

Servy 捕获 `stdout`（标准输出）与 `stderr`（错误输出）并写入文件。

* **按大小轮转**：日志达到指定大小（例如 10MB）时自动滚动。
* **按日期轮转**：按 `Daily`、`Weekly` 或 `Monthly` 间隔滚动日志。
* **保留**：设置 **Max Rotations** 以限制磁盘上保留的旧日志文件数量。

高级日志配置见[日志与日志轮转](./Logging-&-Log-Rotation)。

## 健康监控与恢复

Servy 可监控应用健康状况，并在失败时自动采取措施。

* **心跳**：Servy 按设定间隔检查进程是否仍存活。
* **恢复操作**：失败时可选择 **Restart Process**、**Restart Service** 或 **Restart Computer**。
* **心跳 URL**：绝对 HTTP/HTTPS URL（例如 `https://hc-ping.com/uuid`），用于向外部监控服务（如 [healthchecks.io](https://healthchecks.io/) 或 Uptime Kuma）发送带外诊断心跳 ping。
* **Failure Program**：指定在检测到失败时专门运行的外部脚本或应用。

详细设置见[健康监控与恢复](./Health-Monitoring-&-Recovery)。

## 高级配置

### 环境变量
定义进程专用环境变量，而不污染系统级环境。
* **格式**：`KEY=VALUE`（每行一个，或以 `;` 分隔）。
* **展开**：支持引用其他变量，例如 `PATH=%PATH%;C:\CustomBin`。
* **转义**：字面量分号使用 `\;`，字面量等号使用 `\=`。

完整解析顺序见[环境变量](./Environment-Variables)。

### 服务依赖
确保服务仅在其他必需服务（如 `MSSQLSERVER` 或 `Docker`）就绪后启动。请使用内部 **Service Name**，而非 Display Name。

参见[服务依赖](./Service-Dependencies)。

## 登录与安全

Servy 允许配置服务身份，支持：
* **本地账户**：`.\username`
* **域账户**：`DOMAIN\username`
* **托管服务账户**：`DOMAIN\gMSA$`

**安全说明：** 存储的密码使用 **AES-256** 加密。有关 Servy 如何处理凭据的技术细节，见[安全性](./Security)模型。

## 启动与停止钩子

Servy 提供四个不同的钩子点来管理服务生命周期：

| 钩子 | 典型用途 |
| :--- | :--- |
| **Pre-Launch** | 获取密钥、生成配置文件。 |
| **Post-Launch** | 发送“已启动”通知、初始化数据库迁移。 |
| **Pre-Stop** | 优雅排空连接、刷新缓冲区。 |
| **Post-Stop** | 清理临时文件、事后日志记录。 |

* 启动详细指南：[启动前与启动后操作](./Pre-Launch-&-Post-Launch-Actions)
* 关闭详细指南：[停止前与停止后操作](./Pre-Stop-&-Post-Stop-Actions)

## 管理服务

* **Servy Manager**：使用 GUI 进行实时状态监控、日志查看与配置更新。参见 [Servy Manager](./Servy-Manager)。
* **Servy CLI**：适合自动化与远程管理。参见 [Servy CLI](./Servy-CLI)。
* **PowerShell**：使用原生模块进行 CI/CD 集成。参见 [Servy PowerShell 模块](./Servy-PowerShell-Module)。
* **可移植性**：使用 **Export/Import** 功能在服务器之间迁移服务配置。参见[导出/导入服务](./Export-Import-Services)。

**下一步：** 准备部署？查看[示例与配方](./Examples-&-Recipes)，获取 Node.js、Python 和 Java 应用的预配置方案。

## 另请参阅
* [Servy 桌面应用](./Servy-Desktop-App)
* [Servy Manager](./Servy-Manager)
* [Servy CLI](./Servy-CLI)
* [PowerShell 模块](./Servy-PowerShell-Module)
* [示例与配方](./Examples-&-Recipes)
* [关闭与拆卸](./Shutdown-&-Teardown)
* [服务事件通知](./Service-Event-Notifications)
* [与替代方案对比](./Comparison-with-Alternatives)
* [故障排除](./Troubleshooting)
* [常见问题](./FAQ)
