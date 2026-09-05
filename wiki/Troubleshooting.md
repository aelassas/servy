## Table of Contents

1. [简介](#introduction)
1. [远程管理工具出现空白屏幕](#blank-screen-on-remote-management-tools)
1. [服务无法启动](#service-wont-start)
1. [“Access Denied” 错误](#access-denied-errors)
1. [日志未出现](#logs-not-appearing)
1. [健康检查过早失败](#health-checks-failing-too-soon)
1. [进程启动后立即退出](#process-starts-but-exits-immediately)
1. [服务已安装但在 Windows 服务中不可见](#service-installed-but-not-visible-in-windows-services)
1. [服务无法卸载](#service-wont-uninstall)
1. [CPU 或内存占用过高](#high-cpu-or-memory-usage)
1. [更新配置后更改未生效](#changes-not-applying-after-updating-configuration)
1. [使用域账户的服务在重启后无法启动](#service-using-a-domain-account-does-not-start-after-reboot)
1. [Windows 7 上 Servy「Check for Updates...」无效](#servy-check-for-updates-not-working-on-windows-7)

## Introduction

本页列出使用 Servy 时最常见的问题及实用修复方法。当出现异常时请先从这里开始；解决方案可能已在下方覆盖。

大多数问题属于以下几类：权限、启动配置、健康检查、日志捕获或 Windows 策略设置。在进行更改前，使用下方各节快速缩小问题范围。

## Blank Screen on Remote Management Tools

若在远程管理工具（如 MeshCentral、TeamViewer 或 AnyDesk）中出现空白屏幕，请从管理员命令提示符使用以下命令运行桌面应用：
```cmd
Servy.exe --force-sr
```

以及使用以下命令运行 Manager 应用：

```cmd
Servy.Manager.exe --force-sr
```

## Service Won't Start

将应用包装为 Windows 服务时，这是最常见的问题之一。

可能原因与修复：

* **Access Denied / 权限问题：** 若服务在 **LocalSystem** 以外的账户下运行，请为运行服务的账户授予对 `%ProgramData%\Servy` 的 **Modify** 访问权限，并运行强制加固脚本（`Set-ServyExePermissions.ps1 -TargetAccount "domain\user"`）。参见 [Executable Permission Hardening](./Security#executable-permission-hardening-mandatory)。
* **缺少工作目录：**
  许多应用（Node.js、Python、.NET、Java…）依赖相对路径。
  *真实案例：* 某用户有一个读取 `./config.json` 的 Node 应用。手动运行正常，但作为服务失败，因为 Servy 从 `C:\Program Files\nodejs\` 启动它。设置工作目录后解决。
* **找不到文件或缺少依赖：**
  确保所有 DLL、配置或运行时存在于应用期望的位置。
  *示例：* 缺少运行时依赖的 .NET 应用会静默失败。
* **应用立即崩溃：**
  打开 Servy Manager：Logs，检查轮转日志文件，或查看 `%ProgramData%\Servy\logs\Servy.Service.log`。通常可在那里看到异常。
* **应用因 Console/UI 句柄错误崩溃：**
  若应用尝试可视化更新命令提示符（如清屏或移动光标），在作为后台服务运行时会崩溃，因为服务没有可见窗口。无需改代码即可修复：安装服务时在配置中启用 `--enableConsoleUI` 选项。
* **缺少环境变量：**
  若应用依赖 `PATH`、`NODE_ENV`、`ASPNETCORE_ENVIRONMENT` 等变量，请在服务配置中定义它们。

## "Access Denied" Errors

这些问题通常发生在 Windows 阻止某项操作时。

可能原因与修复：

* **Servy 未以管理员身份运行：** 某些操作（安装服务、绑定低于 1024 的端口、写入受保护位置）需要提升权限。
* **日志或工作目录位于受保护文件夹：**
  *真实案例：* 将日志写入 `C:\Program Files\MyApp\logs` 导致访问问题。
  修复：使用可写路径如 `C:\ServyData\logs`，或调整权限。
* **被包装的应用本身需要管理员权限：** 例如管理防火墙规则或与驱动程序交互的应用。

## Logs Not Appearing

若 Servy 正在运行但 `stdout`/`stderr` 日志为空，说明应用的输出未被捕获。

可能原因与修复：

* **日志目录只读或被杀毒软件阻止：**
  *真实案例：* Windows Defender 阻止了看起来“可疑”的 EXE 的文件写入。添加排除项后解决。
* **未缓冲的 stdout/stderr 输出：**
  某些应用会缓冲日志直到进程结束。
  修复：启用无缓冲输出或添加显式 flush 调用。
* **应用直接写入自己的日志文件：** 检查应用内部日志设置，确保输出写入 `stdout`/`stderr`。
* **已启用控制台 UI 模式（`--enableConsoleUI`）：** 启用 Console UI 时会有意禁用 `stdout`/`stderr` 重定向。若需要 Servy 将应用输出捕获到已配置的日志文件，请禁用 `--enableConsoleUI`。

## Health Checks Failing Too Soon

当应用启动较慢或执行繁重初始化时，Servy 可能认为应用不健康。

可能原因与修复：

* **被包装进程在启动时退出：** Servy 的心跳检查的是被包装进程是否仍在运行，而非 HTTP 健康端点。若心跳触发，几乎总是意味着进程崩溃或已退出。检查 `stdout`/`stderr` 日志以获取实际错误。
* **启动时间超过初始化窗口：** 默认在恢复触发前为 `30s × 3 = 90s`。若应用需要更长时间，请增大 `HeartbeatInterval` 或 `MaxFailedChecks`。
* **预热或初始化缓慢：** 若应用因繁重初始化步骤（加载 ML 模型、打开 SQL 连接池等）未能在心跳预算内就绪，请将该步骤移到 Pre-Launch 钩子，使其在主进程被视为已启动*之前*完成。

## Process Starts but Exits Immediately

可能原因与修复：

* **应用期望用户交互：** GUI 应用或会打开控制台窗口的应用在无界面运行时会立即退出。
* **缺少依赖或运行时错误：** 检查日志中是否有缺失的 DLL、配置错误或运行时不匹配。
* **错误的命令行参数：** 不正确的标志或环境相关路径可导致立即终止。

## Service Installed but Not Visible in Windows Services

可能原因与修复：

* **提升上下文不匹配：** 在一个用户会话中以管理员安装服务，然后在受限用户下查看 Services，可能什么都看不到。
* **视图过时：** 刷新列表或重新打开 `services.msc`。
* **名称冲突：** 确保服务名称不与现有 Windows 服务冲突。

## Service Won't Uninstall

可能原因与修复：

* **活动进程锁定：** 另一工具（任务管理器、监控代理、杀毒软件）可能仍持有进程句柄。请先手动停止服务。
* **权限不足：** 以管理员模式运行 Servy Manager。
* **服务已标记为删除：** 检查 Windows 是否已将服务标记为删除；若是，需重启后才能重新安装。

## High CPU or Memory Usage

可能原因与修复：

* **意外的后台行为：** 应用作为后台服务时行为可能不同（例如，相对路径可能导致它加载或写入巨大的数据文件）。
* **缺少工作目录设置：** 使用正确的工作目录，防止在系统文件夹中意外创建文件。

## Changes Not Applying After Updating Configuration

可能原因与修复：

* **待处理的服务重启：** 配置更改需重启 Windows 服务后才会生效。
* **缓存的应用配置：** 某些应用仅在启动时读取配置。
* **语法或验证错误：** 确保配置文件中没有尾随逗号、损坏的 JSON 或重复字段。

## Service Using a Domain Account Does Not Start After Reboot

若配置为在域账户下运行的服务在安装后可成功启动，但在服务器重启后无法启动，最常见原因是该账户失去了「作为服务登录」（Log on as a service）权限。

安装服务时，Servy 会在本地为指定账户授予此权限。但是，若域组策略定义了「作为服务登录」设置，它会在启动或组策略刷新期间覆盖本地配置。重启后，域策略替换本地分配，账户不再具有所需权限，Windows 阻止服务启动。

要解决此问题，请在域级别将服务账户添加到「作为服务登录」策略。运行 `gpmc.msc` 打开组策略管理控制台，编辑适用于目标服务器的相应组策略对象，然后依次转到「计算机配置」、「Windows 设置」、「安全设置」、「本地策略」、「用户权限分配」，选择「作为服务登录」。将域服务账户添加到列表并应用更改。在服务器上运行 `gpupdate /force` 或等待下一个策略刷新周期，必要时重启。

若服务器未加入域，可使用 `secpol.msc` 在本地配置该设置，并导航到相同的「用户权限分配」位置。

出现此问题时，Windows 会在事件查看器（`eventvwr.msc`）的「Windows 日志」→「系统」中记录失败，来源为 Service Control Manager。日志条目通常说明服务无法登录，因为用户在该计算机上不具备所请求的登录类型。这确认问题与 Windows 安全策略有关，而非 Servy 本身。

另外，请确保域账户对 `%ProgramData%\Servy` 具有 **Modify** 访问权限，并且已为其运行强制加固脚本。参见 [Executable Permission Hardening](./Security#executable-permission-hardening-mandatory)。

## Servy "Check for Updates..." Not Working on Windows 7

Windows 7（尤其是未更新时）默认未启用 TLS 1.2。这是根本原因。

要修复，请创建名为 `enable-tls12-win7.reg` 的文件并以管理员身份运行：

```text
Windows Registry Editor Version 5.00

; WinHTTP TLS 1.2
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp]
"DefaultSecureProtocols"=dword:00000a00

[HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp]
"DefaultSecureProtocols"=dword:00000a00

; SCHANNEL TLS 1.2 client
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client]
"Enabled"=dword:00000001
"DisabledByDefault"=dword:00000000

; .NET strong crypto
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\.NETFramework\v4.0.30319]
"SchUseStrongCrypto"=dword:00000001
"SystemDefaultTlsVersions"=dword:00000001

[HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v4.0.30319]
"SchUseStrongCrypto"=dword:00000001
"SystemDefaultTlsVersions"=dword:00000001
```

然后重启计算机。
