## Table of Contents

### [简介](#introduction-1)

### [总体概览](#general-overview-1)

1. [Servy 的使用场景是什么？](#whats-the-use-case-for-servy)
1. [应用应该作为服务运行还是托盘应用？](#should-i-run-an-app-as-a-service-or-a-tray-app)
1. [“该做成服务的东西本来就该是服务”？](#things-that-should-be-a-service-should-already-be-a-service)
1. [为什么选择 Windows 服务而不是脱离 Windows 生态？](#why-choose-a-windows-service-instead-of-building-outside-the-windows-ecosystem)

### [对比](#comparisons-1)

1. [Servy 和 Windows 任务计划程序是一回事吗？](#is-servy-the-same-as-windows-task-scheduler)
1. [Servy 与 sc.exe 有何不同？](#how-does-servy-compare-to-scexe)
1. [Servy 与 NSSM 有何不同？](#how-does-servy-compare-to-nssm)
1. [Servy 与 WinSW 有何不同？](#how-does-servy-compare-to-winsw)
1. [Servy 与 SrvAny 有何不同？](#how-does-servy-compare-to-srvany)
1. [Servy 与 Docker Desktop 有何不同？](#how-does-servy-compare-to-docker-desktop)
1. [Servy 与 Tanuki wrapper 有何不同？是否兼容 Waratek RASP？](#how-does-servy-compare-to-tanukis-wrapper-and-is-it-compatible-with-waratek-rasp)
1. [Servy 能否在 Windows 上替代 PM2 运行 Node.js？](#can-servy-replace-pm2-for-nodejs-on-windows)
1. [为什么不用 Microsoft.Extensions.Hosting.WindowsServices？](#why-not-microsoftextensionshostingwindowsservices)

### [技术配置](#technical-configuration-1)

1. [Servy 可以把哪些类型的应用作为服务运行？](#what-types-of-applications-can-servy-run-as-a-service)
1. [如何用 Servy 运行批处理或 PowerShell 等脚本？](#how-do-i-run-scripts-like-batch-or-powershell-with-servy)
1. [Servy 是否同时支持控制台应用和 GUI 应用？](#does-servy-support-both-console-apps-and-gui-apps)
1. [能否设置自定义工作目录？](#can-i-set-a-custom-working-directory)
1. [Servy 是否支持以自定义用户账户运行服务？](#does-servy-support-running-services-under-custom-user-accounts)
1. [如何更新服务配置？](#how-do-i-update-a-service-configuration)
1. [能否在自动化部署（CI/CD）中使用 Servy？](#can-i-use-servy-in-automated-deployments-cicd)

### [服务生命周期与钩子](#service-lifecycle--hooks-1)

1. [Servy 是否支持自动重启？](#does-servy-support-automatic-restarts)
1. [被监控的进程崩溃时会发生什么？](#what-happens-if-the-monitored-process-crashes)
1. [Servy 是否支持在服务失败时运行脚本？](#does-servy-support-running-a-script-on-service-failure)
1. [Servy 中的 pre-launch 钩子是什么？](#what-is-the-pre-launch-hook-in-servy)
1. [pre-launch 钩子失败时会发生什么？](#what-happens-if-the-pre-launch-hook-fails)
1. [能否向 pre-launch 进程传递参数和环境变量？](#can-i-pass-arguments-and-environment-variables-to-the-pre-launch-process)
1. [pre-launch 钩子是同步还是异步？](#is-the-pre-launch-hook-synchronous-or-asynchronous)
1. [Servy 中的 post-launch 钩子是什么？](#what-is-the-post-launch-hook-in-servy)
1. [post-launch 进程何时执行？](#when-is-the-post-launch-process-executed)
1. [若在等待 post-launch 时服务停止会怎样？](#what-happens-if-the-service-stops-while-the-post-launch-process-is-waiting)
1. [能否向 post-launch 进程传递参数？](#can-i-pass-arguments-to-the-post-launch-process)
1. [能否用 post-launch 运行长时间脚本？](#can-i-use-post-launch-for-long-running-scripts)
1. [Pre-Stop 与 Post-Stop 钩子有何区别？](#what-is-the-difference-between-a-pre-stop-and-a-post-stop-hook)
1. [超时为 0 时，Pre-Stop 钩子还会运行吗？](#if-the-timeout-is-0-does-the-pre-stop-hook-still-run)
1. [Pre-Stop 钩子设为 0 秒超时有何风险？](#what-is-the-danger-of-a-0-second-timeout-on-a-pre-stop-hook)
1. [更改配置后服务会被重新创建吗？](#is-the-service-recreated-after-config-change)

### [日志与监控](#logging--monitoring-1)

1. [能否捕获 stdout 和 stderr 日志？](#can-i-capture-stdout-and-stderr-logs)
1. [有没有办法实时查看服务日志？](#is-there-a-way-to-view-service-logs-in-real-time)
1. [Servy 能否在服务失败或重启时发送通知或邮件？](#can-servy-send-notifications-or-emails-on-service-failures-or-restarts)
1. [Servy 是否提供 CPU 和内存监控？](#does-servy-provide-cpu-and-ram-monitoring)
1. [如何使用 Heartbeat URL 监控服务？](#how-do-i-monitor-my-service-using-a-heartbeat-url)
1. [如何将失败告警发送到 Slack、Microsoft Teams、电话或 WhatsApp？](#how-can-i-get-failure-alerts-sent-to-slack-microsoft-teams-phone-call-or-whatsapp)

### [关闭与进程处理](#shutdown--process-handling-1)

1. [Servy 如何停止应用？](#how-does-servy-stop-the-app)
1. [Servy 如何防止僵尸进程？](#how-does-servy-prevent-zombie-processes)
1. [用 Servy 运行 Java 应用时，服务关闭会触发 Java shutdown hooks 吗？](#will-java-shutdown-hooks-run-when-a-servy-service-running-a-java-app-shuts-down)
1. [Servy 会向 SCM 申请额外的服务关闭时间吗？](#does-servy-make-requests-for-additional-service-shutdown-time)
1. [Pre-Shutdown 信号具体何时发生？](#when-exactly-does-the-pre-shutdown-signal-occur)
1. [操作系统关机或重启时会发生什么？](#what-happens-if-the-os-shuts-down-or-reboots)

### [环境与兼容性](#environment--compatibility-1)

1. [为什么需要管理员权限？](#why-do-i-need-admin-privileges)
1. [Servy 是否兼容较旧的 Windows 版本？](#is-servy-compatible-with-older-windows-versions)
1. [Servy 能在离线服务器上工作吗？](#does-servy-work-on-offline-servers)
1. [能否把 Servy 集成到安装程序中且无需额外依赖？](#can-servy-be-used-as-part-of-an-installer-without-requiring-extra-dependencies)
1. [卸载 Servy 后会发生什么？](#what-happens-if-i-uninstall-servy)

### [高级与特定场景](#advanced--specific-scenarios-1)

1. [Servy 是否支持为被包装进程设置环境变量？](#does-servy-support-environment-variables-for-the-wrapped-process)
1. [Servy 是否支持服务依赖？](#does-servy-support-service-dependencies)
1. [Servy 是否支持 gMSA 与 AD 登录？](#does-servy-support-gmsa-and-ad-log-on)
1. [为什么应用作为 Windows 服务运行时 Excel COM 自动化会失败？](#why-does-excel-com-automation-fail-when-my-application-runs-as-a-windows-service)
1. [能否用 Azure 登录通过 Servy 将 OneDrive 作为服务运行？](#can-servy-run-onedrive-as-a-service-using-azure-login)
1. [Servy 是否支持类似 ClickOnce 的更新行为？](#does-servy-support-clickonce-like-update-behavior)
1. [能否用 PowerShell 脚本通过 Servy 以服务方式运行 Windows Update？](#can-servy-run-windows-update-from-a-powershell-script-as-a-service)
1. [如何以及为何应在 Servy 中使用 CPU 亲和性？](#how-and-why-should-i-use-cpu-affinity-with-servy)

## Introduction

欢迎阅读 Servy 常见问题解答。Servy 是一款 Windows 服务包装器，用于将非服务型应用（包括用 Node.js、Python、Go、Java 或 .NET 编写的应用）作为可靠、一等公民的 Windows 服务运行。

许多后台应用最初只是简单的控制台进程。但在生产环境中，它们往往需要结构化的生命周期管理，例如自动重启、优雅关闭与持续监控。Servy 在可移植的应用代码与 Windows 服务控制管理器（Service Control Manager，SCM）之间架起桥梁，集中管理日志、环境变量、进程健康状态与恢复行为，而无需修改应用本身。

本 FAQ 涵盖从初始安装与配置，到高级生命周期钩子与进程树管理等主题。

## General Overview

### What's the use case for Servy?

主要用途是以可靠、可观测的方式，将非服务型应用作为正式的 Windows 服务运行。

当你有用 Node.js、Python、Go 或 .NET 编写的 Web 服务器、Worker、调度器或长时间运行的工具等后台应用时，这一点很有用。例如：内部 REST API、后台任务处理器、同步数据的文件监视器、消息队列消费者、本地构建代理、监控或指标采集器，以及需要持续在后台运行的自动化工具。

这类应用通常需要开机自动启动、在崩溃或挂起时重启、以特定的本地或域账户运行、提供便于排查的日志，并在重启或部署时干净关闭，不留下孤儿进程。

Servy 面向开发者、IT 管理员和高级用户，希望在日志、生命周期钩子、健康检查与监控等方面，获得比基础服务包装器更多的控制与可见性。

### Should I run an app as a service or a tray app?

托盘应用适合与交互式用户会话绑定的场景，而 Windows 服务满足另一类需求。当应用必须独立于已登录用户运行时，服务非常有用。它们在系统启动时启动，并在无人登录时继续运行，这对后台工作负载或通过远程会话访问的机器很重要。

服务还与 SCM（Service Control Manager）集成，提供可预期的启动与关闭行为、失败时自动重启，以及在系统更新或重启时一致的处理方式。托盘应用常在用户注销或会话断开时停止，而服务会不间断运行。因此，服务更适合本地 API、后台 Worker、调度器或监控代理等长时间运行的进程。

安全性也是重要因素。服务可以在权限受限的专用账户下运行，这对用户会话中的应用往往不现实。实践中，需要交互的面向用户工具适合托盘应用；需要无论谁登录都能稳定运行的基础设施类工作负载，则更适合服务。

### Things that should be a service should already be a service?

理想情况下，任何打算在服务器上长期运行的程序，从一开始就应实现为正式的 Windows 服务。

但现实中，包装器与服务管理器之所以存在，是因为环境并不总是理想。大量最终运行在 Windows Server 上的软件最初并非按服务编写，包括遗留内部工具、无法获得源码的第三方软件、设计为控制台进程的跨平台应用，以及假定 Linux 式监管而非 Windows Service Control Manager 的厂商工具。

这并不限于某一种语言。Node.js、Python、Java、Go 以及部分遗留应用常以控制台应用起步，因为这是最可移植、最简单的执行模型。将它们重写为原生 Windows 服务并不总是可行或划算，尤其是当应用已经可靠运行，只需要可预期的启动、关闭与恢复行为时。

### Why choose a Windows service instead of building outside the Windows ecosystem?

在企业、受监管以及本地部署等仍以 Windows 为标准的环境中，Windows 服务依然相关。许多生产工作负载并非云原生，也无法迁移到容器或 Linux。

Servy 并不是在推广 Windows 优于其他平台，而是为已经运行在 Windows 上、需要可靠后台进程以及正确生命周期管理、日志与恢复的团队，解决一个真实且反复出现的问题。对这些环境而言，Windows 服务仍是正确且受支持的方案。

## Comparisons

### Is Servy the same as Windows Task Scheduler?

不是。虽然理论上可以用任务计划程序拼出类似方案，但你需要组合多种工具与脚本来完成进程监管、日志轮转与性能监控。

Servy 提供统一平台，用于实时安装、配置、管理与监控 Windows 服务。无需在多个工具间切换，Servy 开箱即用即可处理进程监管、自动重启、实时 CPU/内存占用跟踪、stdout/stderr 日志流以及失败告警。

### How does Servy compare to sc.exe?

`sc.exe` 并不会把任意应用包装成服务；它只管理已经专门编写、能与 Windows Service API 通信的二进制。若强行把普通可执行文件塞进去，服务通常会因不知道如何与 Service Control Manager（SCM）通信而超时失败。Servy 正是这座必要的桥梁，让你无需改一行代码即可将任意应用作为服务运行。

除了保持存活，Servy 还用单一仪表板替代多种工具。不必在任务管理器看内存、事件查看器看系统错误、原始文本文件看日志之间跳转，你可以在同一视图中获得实时遥测与可搜索的 stdout/stderr。它还处理清理脚本、post-stop 钩子、依赖检查或 pre-launch 钩子等复杂生命周期逻辑——这些都不是 `sc.exe` 的设计目标。

### How does Servy compare to NSSM?

Servy 与 NSSM 都能让你将任意应用作为原生 Windows 服务运行，但它们解决的问题相关却略有不同。

Servy 的差异在于可见性与日常运维。通过 Servy，你可以实时看到服务在做什么，包括 CPU 与内存占用、实时 stdout 与 stderr、依赖树以及可搜索日志，全部集中在一处。排查问题时无需在事件查看器、日志文件与任务管理器之间跳转。

Servy 还将服务生命周期视为一等概念，支持带正确日志、超时与失败处理的 pre-launch、post-launch、pre-stop 与 post-stop 操作。

### How does Servy compare to WinSW?

WinSW 是可靠的工具，但灵活性不足。它没有图形界面，主要是基于 XML 配置的包装器。虽然 WinSW 支持设置工作目录，但其健康检查与自动恢复能力相对 Servy 有限。WinSW 的重启策略主要依赖退出码，不提供主动健康检查（如心跳监控），也不支持在反复失败后重启子进程或整个系统等高级恢复选项。

### How does Servy compare to SrvAny?

在非常简单的场景下，若目标只是把进程作为服务启动，SrvAny 可能够用。取舍往往出现在服务生命周期与日常运维上。一旦需要干净的启动与关闭、恢复操作以及一致的日志，SrvAny 的局限就会显现。Servy 提供了 SrvAny 所缺乏的运维控制与可见性。

### How does Servy compare to Docker Desktop?

正如 Docker Desktop 把容器这个黑盒变成日志、资源占用与生命周期管理的可视化仪表板，Servy 对 Windows Service Control Manager 做了类似的事。它实质上现代化了原生后台任务的管理方式，让你不必在事件查看器或 `services.msc` 等系统工具里翻找，才能确认应用是否正常。

关键区别在于：Docker 提供隔离环境（容器），而 Servy 为原生进程提供可观测性与控制。当你希望获得类似 Docker 的实时 stdout/stderr 与资源遥测可见性，但又需要应用直接在主机操作系统上运行并完整访问 Windows 环境时，它很合适。它把现代、容器式的管理流程带到了传统 Windows 基础设施上。

### How does Servy compare to Tanuki's wrapper and is it compatible with Waratek RASP?

Tanuki 的 wrapper 紧密聚焦于 JVM 与 Java 进程生命周期，常注入原生库。Servy 与语言无关，将应用视为外部可执行文件，停留在 JVM 边界之外。

由于 Servy 不会插桩或修改 JVM，它避免了与 Waratek RASP 等工具的许多兼容性问题。只要 JVM 以正确选项启动，Servy 就会管理服务生命周期，而不干扰 Java 内部操作。

### Can Servy replace PM2 for Node.js on Windows?

可以。Servy 可通过直接将 `node.exe` 作为原生 Windows 服务运行，在 Windows 上替代 PM2。你的 Node.js 后端由 Windows Service Control Manager（SCM）管理，而不是维持 PM2 存活，这在 Windows 上通常更可靠。

Servy 最有帮助的地方是稳定性与生命周期管理：原生启动与关闭、可预期的重启、日志、恢复、钩子、环境变量、服务依赖与资源监控。这避免了 PM2 用户在 Windows 上常遇到的许多可靠性问题。

若目标是在 Windows 服务器上可靠运行 Node.js 后端，用 Servy 替换 PM2 是直接可行的方案。

### Why not Microsoft.Extensions.Hosting.WindowsServices?

`Microsoft.Extensions.Hosting.WindowsServices` 面向从一开始就设计为 Windows 服务的应用。

Servy 面向相反情况：你已有应用，且无法或不想重写。你把 Servy 指向一个 exe，它就会在不改动应用代码的情况下，加入日志、重启策略与生命周期钩子等实用运维能力。

## Technical Configuration

### What types of applications can Servy run as a service?

Servy 可以运行任意可执行文件，包括 Node.js、Python、.NET 应用、批处理脚本与 PowerShell 脚本。

### How do I run scripts like batch or PowerShell with Servy?

通过其解释器（`cmd.exe` 或 `powershell.exe`）运行：将解释器指定为可执行文件，将脚本路径作为参数。

### Does Servy support both console apps and GUI apps?

支持。虽然服务通常运行后台任务，但在需要时 Servy 也可将 GUI 应用作为服务托管；不过在服务上下文（Session 0）中 GUI 交互会受限。微软一般不建议这样做，仅应在遗留或过渡场景中使用。

### Can I set a custom working directory?

可以。Servy 允许指定启动目录，以避免 Windows 服务常见的路径问题。

### Does Servy support running services under custom user accounts?

可以。你可在 LocalSystem、域账户、AD、gMSA 或任何具备所需权限的自定义用户账户下运行服务。

### How do I update a service configuration?

若服务已安装，可通过 Servy Manager 更新配置。打开该服务的配置，进行修改，然后点击 **Install** 应用更改。最后重启服务，以确保所有更改生效。

### Can I use Servy in automated deployments (CI/CD)?

完全可以。Servy 提供 CLI 与 PowerShell 模块，便于将服务安装与配置编写为自动化工作流的一部分。

因此它适合 Azure DevOps 代理、自托管 runner 或内部构建 Worker 等工具。

## Service Lifecycle & Hooks

### Does Servy support automatic restarts?

支持。它包含健康检查以及可配置的自动恢复与重启策略。

### What happens if the monitored process crashes?

Servy 会执行已配置的恢复操作（重启服务、重启进程、重启计算机，或不做任何操作）。此外可独立配置可选的失败程序，在所有恢复尝试均失败后运行。

### Does Servy support running a script on service failure?

支持。Servy 提供可选的失败程序，在所有恢复尝试失败后运行（以及在禁用健康监控时，子进程以非零退出码退出时运行）。

### What is the pre-launch hook in Servy?

pre-launch 钩子是在主服务进程启动前运行的可选脚本或可执行文件。可用于准备环境或验证依赖。

### What happens if the pre-launch hook fails?

若 pre-launch 钩子以错误退出，Servy 会停止服务启动，以防止在无效状态下运行主进程，除非启用了 **Ignore Failure** 选项。

### Can I pass arguments and environment variables to the pre-launch process?

可以。你可为 pre-launch 进程配置参数与环境变量。Servy 在运行进程前会展开环境变量。

### Is the pre-launch hook synchronous or asynchronous?

pre-launch 钩子默认同步运行。若 pre-launch 超时设为 `0`，则以 fire-and-forget 模式（异步）启动。

### What is the post-launch hook in Servy?

post-launch 钩子是在主服务进程成功启动并通过启动健康检查后运行的可选脚本或可执行文件。

### When is the post-launch process executed?

Servy 会等待服务的 **Start Timeout**（默认 10 秒，可通过 Start Timeout 设置、CLI 中的 `--startTimeout`，或 PowerShell 模块中的 `-StartTimeout` 配置），以确保主进程不会过早退出。若该时间窗口后进程仍存活，则异步执行 post-launch 操作。

### What happens if the service stops while the post-launch process is waiting?

若服务在 post-launch 进程运行前停止，等待会被取消，post-launch 进程不会运行。

### Can I pass arguments to the post-launch process?

可以。你可为 post-launch 进程配置参数与工作目录。

### Can I use post-launch for long-running scripts?

可以。post-launch 进程独立于主服务进程运行。

### What is the difference between a Pre-Stop and a Post-Stop hook?
**Pre-Stop 钩子**在向进程发送主停止信号（`Ctrl+C` 控制台控制事件，`CTRL_C_EVENT`）*之前*执行。用于从负载均衡器移除节点或完成当前工作等任务。**Post-Stop 钩子**在进程完全退出*之后*运行，通常用于清理临时文件、释放锁或发送最终日志。

### If the timeout is 0, does the Pre-Stop hook still run?
会触发钩子，但编排器不会阻塞或等待其完成。它会立即向主进程发送停止信号（先 `Ctrl+C`，在停止超时后回退到 `TerminateProcess`）。这对只需记录事件、无需在进程退出前清理的“仅通知”钩子很有用。

### What is the danger of a 0-second timeout on a Pre-Stop hook?
主要风险是竞态条件。若 Pre-Stop 钩子旨在“排空”流量或保存状态，而主进程在 10 毫秒后退出，钩子很可能在执行中途被终止。仅对非关键副作用（例如向 Slack 频道发送“再见”消息）使用 0 超时。

### Is the service recreated after config change?

不会。Servy 作为服务包装器运行，注册到 Windows 的服务可执行文件是 `Servy.Service.exe`。Servy 将你的应用路径、参数、工作目录与运行时选项存储在内部配置中，并在更改时就地更新。

由于 Service Control Manager 条目是就地修改而非重建，服务名称保持不变。Windows 直接从服务名称派生 Service SID，因此授予 `NT SERVICE\YourService` 对文件、文件夹或系统资源的权限在更新后完全保持稳定。

仅在以下情况 Service SID 会改变：

* 删除并重新安装（先卸载再安装）服务
* 以不同名称重新创建服务
* 显式更改服务名称

这些情况下，Windows 会生成新的 SID，分配给旧 SID 的权限不会延续。

## Logging & Monitoring

### Can I capture stdout and stderr logs?

可以。Servy 可将 `stdout/stderr` 重定向到日志文件，并按文件大小或日期自动轮转。日志以文件形式存储，独立于 Windows 事件日志。可通过 Servy Manager 进行实时跟踪查看。

### Is there a way to view service logs in real time?

可以。Servy Manager 内置带过滤与搜索的日志查看器，无需离开应用即可快速诊断问题。

### Can Servy send notifications or emails on service failures or restarts?

可以。Servy 可在发生错误时生成通知并发送邮件，帮助你了解服务健康状况。详见 [Service Event Notifications](./Service-Event-Notifications)。

### Does Servy provide CPU and RAM monitoring?

可以。Servy Manager 提供 CPU 与内存监控，可通过两种视图访问：
 * **Services Tab（网格视图）：** 实时概览各已配置服务的资源占用。
 * **Performance Tab（实时图表）：** 实时显示 CPU 与内存占用，便于详细监控。

指标细节包括：

 * **CPU：** 占用按整机总容量的百分比报告，与 Windows 任务管理器 CPU 列的行为一致。
 * **RAM：** Servy 报告进程的**已提交私有内存**（服务请求的未共享内存总量，包括当前已换出到磁盘的内存）。
 * **进程树聚合：** Servy 动态汇总整个进程树的指标。这意味着报告的性能数据包含被包装的核心服务进程，以及由其产生的所有活动子进程与后代进程。

**关于 RAM 数值的说明：** 将 Servy 与任务管理器对比的运维人员会注意到，Servy 的 RAM 数值通常高于任务管理器默认的“内存”列。这是有意为之。任务管理器默认显示 *Private Working Set*（当前位于物理 RAM 中的内存）。由于 Windows 在压力下会积极将后台服务内存换出到磁盘，Working Set 对监控后台服务并不可靠。Servy 使用 Commit Size，确保你看到服务的真实内存占用，从而更容易发现内存泄漏与实际资源消耗。

### How do I monitor my service using a Heartbeat URL?

Servy 内置向 [healthchecks.io](https://healthchecks.io/) 或 Uptime Kuma 等外部监控平台发送 HTTP GET 心跳探测的支持。

在服务设置中配置以下字段：

* **Heartbeat URL：** 设置监控端点（例如 `https://hc-ping.com/your-uuid`）。
* **Heartbeat URL Timeout：** 将 HTTP 请求超时设为 2 到 30 秒（默认 10 秒）。
* **Heartbeat URL Flags：** 启用后，服务启动时追加 `/start`，进程恢复失败时追加 `/fail`。

配置后，Servy 会自动处理探测：
* **启动：** 向 `https://hc-ping.com/your-uuid/start` 发送 GET 请求（若启用 flags）。
* **健康运行：** 在稳态健康检查通过时向 `https://hc-ping.com/your-uuid` 发送 GET；若该次通过前曾有一次或多次失败检查，则发往 `.../start`（若启用 flags）。
* **失败或崩溃：** 在发生崩溃或恢复尝试耗尽时，向 `https://hc-ping.com/your-uuid/fail` 发送 GET（若启用 flags）。

### How can I get failure alerts sent to Slack, Microsoft Teams, Phone Call, or WhatsApp?

Servy 依赖带外监控服务投递实时事故告警。将 Servy 的 **Heartbeat URL** 指向 [healthchecks.io](https://healthchecks.io/) 等探测提供商，后者可原生连接下游通知平台。

1. 在监控服务（例如 `healthchecks.io`）中创建检查项，并复制其唯一探测 URL。
2. 将 URL 填入 Servy 的 **Heartbeat URL** 设置，并启用 **Heartbeat URL Flags**。
3. 在监控服务仪表板中，附加你偏好的集成渠道。

当 Servy 发送 `/fail` 信号，或因主机宕机停止探测时，监控平台会向已配置渠道触发告警，包括：

* **聊天与协作：** Slack、Microsoft Teams、Discord、Telegram、WhatsApp、Signal、Google Chat、Matrix、Mattermost、Rocket.Chat、Zulip。
* **事故管理与 Webhooks：** PagerDuty、Opsgenie、Splunk On-Call、Spike.sh、PagerTree、自定义 Webhooks。
* **直接通知：** Email、SMS、Phone Call、Pushbullet、Pushover、ntfy、Gotify。
* **问题与事件跟踪：** GitHub Issues、Trello、Prometheus。

> [!TIP]
> 若需本地或隔离告警、不依赖外部探测服务，也可使用 Servy 的 **Failure Program Path**，在所有恢复尝试失败后执行本地脚本（例如调用 Slack Webhook 的 PowerShell 脚本）。

## Shutdown & Process Handling

### How does Servy stop the app?

若子进程是控制台应用，Servy 会发送 `Ctrl+C` 信号以优雅停止。它会等待若干秒（可配置）让进程退出；若未退出，则强制终止。该过程会对每个子进程及其后代递归重复。

### How does Servy prevent zombie processes?

Servy 跟踪并显式管理其创建的完整进程树。当服务停止或重启时，Servy 首先尝试向主进程及其所有后代传播 `Ctrl+C` 信号以优雅关闭主进程，让应用干净退出。若进程在配置的超时内未退出，则强制终止。

该关闭流程会递归应用于所有子进程及其后代，确保不留下孤儿进程。服务停止时，任何 pre-launch 或 post-launch 进程也会连同其整个进程树一并终止。

因此，Servy 可防止服务停止后仍继续消耗 CPU 或内存的僵尸或失控进程。

### Will Java shutdown hooks run when a Servy service running a Java app shuts down?

会。当服务收到停止请求时，Servy 向 Java 进程发送 `Ctrl+C` 控制台控制事件。JVM 将其解释为中断信号，触发正常关闭序列并执行所有已注册的 hooks。

### Does Servy make requests for additional service shutdown time?

会。当配置的启动/停止超时超过阈值（可配置）时，Servy 会通过标准的 `RequestAdditionalTime` 机制显式向 SCM 申请额外时间。

### When exactly does the Pre-Shutdown signal occur?

当 Windows Service Control Manager（SCM）检测到操作系统正在关机或重启时发生。它在标准 “Stop” 命令之前发送。Windows 会等待已注册 Pre-Shutdown 的服务完成，然后才开始标准的服务关闭阶段。检测到系统关机或重启时，Servy 会执行专门的拆除工作流，以确保数据完整性与优雅退出。

### What happens if the OS shuts down or reboots?

当发起系统级关机或重启时，Servy 拦截 Windows Pre-Shutdown 信号，触发优先保证数据完整性的专门拆除工作流。Servy 会静默健康监控以防冗余恢复尝试，并利用 “Wait Hints” 给予子进程更长窗口来刷新缓冲区与提交事务。该高优先级序列确保即便在重启期间，服务也会编排优雅退出，而非强制终止。

## Environment & Compatibility

### Why do I need admin privileges?

创建与控制 Windows 服务需要提升权限。

### Is Servy compatible with older Windows versions?

现代构建支持 **Windows 10 (1809+)**、**Windows 11** 与 **Windows Server 2016+**。Windows 7 SP1 与 Server 2008 R2 等遗留系统也可通过专用的 **.NET Framework 4.8 构建** 获得支持。请参阅 [Installation Guide](./Installation-Guide#version-comparison) 以为你的操作系统选择正确版本。

### Does Servy work on offline servers?

可以。Servy 可完全离线工作：安装不需要互联网访问，也无需下载外部依赖。

### Can Servy be used as part of an installer without requiring extra dependencies?

可以。便携版 `servy-cli.exe` 是单个自包含可执行文件，无需额外框架。

### What happens if I uninstall Servy?

用 Servy 安装的服务在 Windows 中仍保持注册并继续工作，直到你显式卸载它们。可通过 Servy Manager 或 CLI 安全移除。服务能继续运行，是因为它是标准的 SCM 注册服务，不依赖 Servy UI 或 CLI。

## Advanced & Specific Scenarios

### Does Servy support environment variables for the wrapped process?

支持。Servy 完整支持环境变量。可通过 Servy Desktop App 的 Advanced 选项卡、Servy CLI 的 `--envVars` 选项，或 PowerShell 模块中的 `-EnvVars` 参数进行配置。此外，Servy 允许在进程路径、工作目录与参数中展开环境变量，确保配置在不同环境中保持动态与灵活。

### Does Servy support service dependencies?

支持。可在生态中管理服务依赖。可通过 Servy Desktop App 的 Advanced 选项卡、CLI 的 `--deps` 选项，或 PowerShell 模块中的 `-Deps` 参数定义依赖。为便于监督，Servy Manager 可可视化这些关系并配合实时状态指示：绿色表示服务正在运行，红色表示已停止，橙色警告存在依赖环。

### Does Servy support gMSA and AD log on?

支持。Servy 支持 Group Managed Service Accounts（gMSA）与 Active Directory（AD）登录，以及标准的域与本地账户。它也兼容无密码账户以及 NetworkService、LocalService 等内置 Windows 服务标识。可在 Servy Desktop App 的 Log On 选项卡中配置这些凭据，或使用 CLI 的 `--user` 与 `--password` 选项，或 PowerShell 模块的 `-User` 与 `-Password` 参数。对于 gMSA 与无密码账户，密码字段应留空，由 Servy 自动与域控制器完成身份验证握手。

### Why does Excel COM automation fail when my application runs as a Windows service?

这是预期行为。Microsoft Excel 需要交互式用户会话。应用作为服务运行时，在 Session 0 中执行，没有交互式桌面。建议使用不依赖 COM 的库，如 Open XML SDK 或 EPPlus。

### Can Servy run OneDrive as a Service using Azure Login?

OneDrive 通常需要交互式登录，服务无法完成。不过，你可以用 Servy 运行处理同步的 PowerShell/Graph API 脚本，作为后台服务。

### Does Servy support ClickOnce-like update behavior?

支持，且以对服务安全的方式实现。推荐做法是使用 **pre-launch 钩子**，在服务启动前检查更新。这样可确保更新在没有文件锁定或竞态条件的情况下进行。

### Can Servy run Windows Update from a PowerShell script as a service?

技术上可行，但必须确保脚本具备所需管理权限（在 LocalSystem 下运行），并干净地处理重启与幂等性。

### How and why should I use CPU affinity with Servy?

**为何使用 CPU 亲和性？**

默认情况下，Windows 操作系统调度器会将进程分布到所有可用的逻辑 CPU 核心。设置 **CPU Affinity** 会将被包装进程绑定到特定的 CPU 核心子集上运行。这尤其适用于：

* **资源隔离：** 防止高 CPU 或计算密集型后台服务饿死关键系统进程或其他同机服务。
* **性能优化：** 将缓存敏感或单线程工作负载固定到专用核心，以减少上下文切换与 L1/L2/L3 缓存颠簸。
* **许可合规：** 将遗留或专有软件限制在固定数量的已授权核心上。

**如何在 Servy 中设置 CPU 亲和性**

Servy 接受以 **核心范围**、**逗号分隔列表** 或 **十六进制位掩码** 指定的 CPU 亲和性：

* **核心列表 / 范围：** `"0-3,8"`（核心 0、1、2、3 与 8）或 `"0,2,4"`（核心 0、2 与 4）。
* **十六进制位掩码：** `"0xFF00"`（将进程固定到核心 8 到 15）。
* **通用 CPU 0：** `"0"` 或 `"0x1"`（在任意机器上将进程固定到核心 0）。

#### 示例配置

##### 1. Servy Desktop App（GUI）
在 **Servy Desktop App** 中添加或编辑服务时：
1. 转到 **Main** 选项卡。
2. 找到 **CPU Affinity** 字段。
3. 输入所需的 CPU 掩码或核心列表（例如 `0-3,8`、`0,2,4` 或 `0xFF00`）。
4. 点击 **Install**，然后 **Restart**。

##### 2. CLI 用法
```cmd
servy-cli install --name="MyService" --path="C:\Apps\Worker.exe" --cpuAffinity="0-3,8"
```

##### 3. PowerShell 自动化脚本

```powershell
Import-Module "C:\Program Files\Servy\Servy.psm1" -Force

$installParams = @{
    Name        = "MyService"
    Path        = "C:\Apps\Worker.exe"
    CpuAffinity = "0,2,4" # Binds process specifically to Cores 0, 2, and 4
}

Install-ServyService @installParams
```

##### 4. MyService.json 配置文件

```json
{
  "Name": "MyService",
  "ExecutablePath": "C:\\Apps\\Worker.exe",
  "CpuAffinity": "0xFF00"
}
```

##### 5. MyService.xml 配置文件

```xml
<ServiceDto>
  <Name>MyService</Name>
  <ExecutablePath>C:\Apps\Worker.exe</ExecutablePath>
  <CpuAffinity>0-3,8</CpuAffinity>
</ServiceDto>
```

#### 十六进制位掩码如何工作

以下说明十六进制位掩码 `"0xFF00"` 如何映射到**核心 8 到 15**，以及背后的逐步计算。

##### 1. 将十六进制转换为二进制

在 Windows CPU 亲和性位掩码中，**每个比特位直接对应一个 CPU 核心索引**：

* **Bit 0**（最右边的位）→ 核心 0
* **Bit 1** → 核心 1
* ...
* **Bit** *N* → 核心 $N$

要查看启用了哪些核心，将十六进制值 `0xFF00` 展开为 16 位二进制表示（每个十六进制数字代表 4 位）：

| Hex Digit | `F` | `F` | `0` | `0` |
| --- | --- | --- | --- | --- |
| **Binary** | `1111` | `1111` | `0000` | `0000` |

合成为 16 位数：

$$\text{Binary: } 1111\ 1111\ 0000\ 0000_2$$

##### 2. 将二进制位映射到处理器核心

从右到左将 16 位与从零开始的核心索引对齐：

```text
Bit Position (Core):  15 14 13 12 11 10  9  8 |  7  6  5  4  3  2  1  0
Binary Bit Value:      1  1  1  1  1  1  1  1 |  0  0  0  0  0  0  0  0
                      |_____________________|   |_____________________|
                         Cores 8 to 15 ON           Cores 0 to 7 OFF
```

* **Bits 0 through 7** 设为 `0` $\rightarrow$ 核心 0–7 **禁用**。
* **Bits 8 through 15** 设为 `1` $\rightarrow$ 核心 8–15 **启用**。

由于 bits 8 到 15 均为 `1`，配置此亲和性掩码的任何进程都会被固定为仅在**核心 8、9、10、11、12、13、14 与 15**上执行。

##### 3. 数学计算

要以编程方式计算位掩码值，对每个已启用核心索引 $k$ 求二的幂（$2^k$）之和：

$$\text{AffinityMask} = \sum_{k=8}^{15} 2^k$$

```math
\begin{aligned}
\text{AffinityMask} &= 2^8 + 2^9 + 2^{10} + 2^{11} + 2^{12} + 2^{13} + 2^{14} + 2^{15} \\
                    &= 256 + 512 + 1024 + 2048 + 4096 + 8192 + 16384 + 32768 \\
                    &= 65,280_{10}
\end{aligned}
```

将十进制 $65,280$ 转换为十六进制得到 `0xFF00`：

* $65,280 / 4096 = \mathbf{15} \rightarrow \mathbf{F}$
* $(65,280 \pmod{4096}) / 256 = \mathbf{15} \rightarrow \mathbf{F}$
* 剩余余数 $= 0 \rightarrow \mathbf{00}$

结果：**`0xFF00`**
