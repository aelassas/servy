## Table of Contents
1. [简介](#introduction)
1. [快速开始（任意应用）](#quick-start-any-app)
   1. [示例：运行简单的 HTTP 服务器](#example-run-a-simple-http-server)
1. [Servy 如何运行你的应用](#how-servy-runs-your-app)
1. [服务账户与权限](#service-account--permissions)
   1. [示例：使用专用服务账户安装](#example-install-using-a-dedicated-service-account)
1. [验证服务](#verifying-the-service)
1. [常见问题与修复](#common-problems--fixes)
   1. [服务启动后立即停止](#service-starts-then-stops-immediately)
   1. [在终端中正常，作为服务却不行](#works-in-terminal-but-not-as-a-service)
   1. [没有产生日志](#no-logs-are-produced)
1. [提示与说明](#tips--notes)
1. [另见](#see-also)

### [JavaScript 与 TypeScript 运行时](#javascript--typescript-runtimes-1)
1. [Node.js / Next.js / Express](#run-a-nodejs--nextjs--express-app-as-a-service)
1. [Deno](#run-a-deno-app-as-a-service)
1. [Bun](#run-a-bun-app-as-a-service)

### [容器与基础设施](#containers--infrastructure-1)
1. [Docker](#run-a-docker-container-as-a-service)
1. [Docker Compose](#run-a-docker-compose-stack-as-a-service)
1. [Nginx](#run-an-nginx-web-server-as-a-service)
1. [Redis](#run-a-redis-server-as-a-service)
1. [PocketBase](#run-a-pocketbase-instance-as-a-service)

### [AI 与现代工具](#ai--modern-tools-1)
1. [Ollama AI 实例](#run-an-ollama-ai-instance-as-a-service)
1. [Ghost CMS](#run-a-ghost-cms-instance-as-a-service)

### [编译型语言](#compiled-languages-1)
1. [Go](#run-a-go-app-as-a-service)
1. [Rust](#run-a-rust-app-as-a-service)
1. [C / C++](#run-a-c--c-compiled-app-as-a-service)
1. [Zig](#run-a-zig-app-as-a-service)
1. [Pascal / Delphi](#run-a-pascal--delphi-app-as-a-service)
1. [Fortran](#run-a-fortran-app-as-a-service)

### [托管运行时](#managed-runtimes-1)
1. [.NET](#run-a-net-app-as-a-service)
1. [Java](#run-a-java-jar-as-a-service)
1. [Elixir](#run-an-elixir-script-as-a-service)
1. [Erlang](#run-an-erlang-script-as-a-service)

### [脚本与自动化](#scripting--automation-1)
1. [PowerShell](#run-a-powershell-script-as-a-service)
1. [Batch](#run-a-batch-file-as-a-service)
1. [Python](#run-a-python-script-as-a-service)
1. [PHP](#run-a-php-app-as-a-service)
1. [Laravel Queue Worker](#run-a-laravel-queue-worker-as-a-service)
1. [Ruby](#run-a-ruby-app-as-a-service)
1. [VBScript](#run-a-vbscript-as-a-service)
1. [AutoHotkey](#run-an-autohotkey-script-as-a-service)
1. [WSL Bash](#run-a-wsl-bash-script-as-a-service)

### [数据科学与分析](#data-science--analytics-1)
1. [Julia](#run-a-julia-script-as-a-service)
1. [R](#run-an-r-script-as-a-service)

### [其他语言与工具](#other-languages--tools-1)
1. [Haskell](#run-a-haskell-app-as-a-service)
1. [Dart](#run-a-dart-server-or-script-as-a-service)
1. [Lua](#run-a-lua-script-as-a-service)
1. [Perl](#run-a-perl-script-as-a-service)
1. [OCaml](#run-an-ocaml-script-or-app-as-a-service)
1. [Kopia](#run-kopia-as-a-service)

## Introduction

本文档说明如何使用 Servy CLI 将几乎任何应用作为原生 Windows 服务运行。涵盖多种语言、运行时与基础设施工具的实用示例。使用目录可直接跳转到所需的运行时或配置。

Servy 可将任意应用作为原生 Windows 服务运行。本页为最常用的语言与框架提供可立即作为后台服务运行的示例。

服务可通过 Servy Desktop App 或 PowerShell 模块安装与配置，但本页聚焦于使用 Servy CLI 的真实示例，便于自动化、脚本编写与 CI/CD 流水线。

安装后，Servy 目录会自动加入系统 **PATH** 环境变量。因此你可以在任意提升权限的命令提示符或 PowerShell 会话中直接运行 `servy-cli`。

## Quick Start (Any App)

将任意应用作为 Windows 服务运行的基本模式是：

```powershell
servy-cli install `
  --name="MyService" `
  --path="C:\path\to\app.exe" `
  --params="optional arguments" `
  --startupDir="C:\path\to" `
  --startupType="Automatic"
```

若应用能在终端中正确运行，使用此模式作为服务也会正确运行。

### Example: Run a simple HTTP server

```powershell
servy-cli install `
  --name="HelloServer" `
  --path="C:\Program Files\nodejs\node.exe" `
  --params="server.js" `
  --startupDir="C:\apps\hello" `
  --startupType="Automatic"
```

若 `node server.js` 在终端中可用，此服务也会可用。

## How Servy Runs Your App

Servy 通过向 Windows Service Control Manager（SCM）注册，将你的应用作为**原生 Windows 服务**运行。

你的应用**不需要**实现 Windows Service API，也不需要具备服务感知能力。服务启动时 Servy 启动你的可执行文件或命令；若启用恢复则会进行监控；运行 pre-launch 与 post-launch 钩子；服务停止时停止应用，包括 pre-stop 与 post-stop 钩子。

关键特性：

* 你的应用像所有 Windows 服务一样运行在 **Session 0**。
* 标准输入/输出**不可交互**。
* 环境变量、工作目录与参数在安装时显式定义。
* 服务生命周期（启动、停止、重启）完全由 SCM 管理。

若应用无法无人值守运行、需要桌面会话，或期望用户交互，则不适合作为 Windows 服务运行。

## Service Account & Permissions

默认情况下，服务安装后以 **LocalSystem** 账户运行。

请注意：
* 网络访问可能与你的用户账户不同
* 映射驱动器不可用
* 环境变量必须是系统范围的

若应用需要访问网络共享、数据库或受限文件夹，应配置**专用服务账户**，而不是在默认的 **LocalSystem** 下运行。

安装服务时使用 `--user` 选项，并通过 `SERVY_PASSWORD` 环境变量提供密码（见下方示例），并确保该账户对以下位置具有 **Modify** 权限：

* `%ProgramData%\Servy`
* 应用的启动目录
* 应用依赖的任何其他文件、文件夹或网络资源

并且已对 `%ProgramData%\Servy` 运行强制加固脚本。参见 [Executable Permission Hardening](./Security#executable-permission-hardening-mandatory)。

### Example: Install using a dedicated service account

当未提供 `--password` 时，`servy-cli` 从 `SERVY_PASSWORD` 读取账户密码，从而避免出现在进程列表与 shell 历史中。

```powershell
# Set the password in the current process environment first
$env:SERVY_PASSWORD = "your_secret_password"

servy-cli install `
  --name="MySecureService" `
  --path="C:\apps\secure\app.exe" `
  --startupDir="C:\apps\secure" `
  --user="DOMAIN\svc-myapp" `
  --startupType="Automatic"

# Clear sensitive variables from memory immediately after use
Remove-Item Env:SERVY_PASSWORD
```

> [!IMPORTANT]
> **安全：** 避免在命令行使用 `--password`——它会出现在操作系统进程列表与 shell 历史中。详见 [Security](./Security#6-sensitive-command-line-arguments--service-account-credentials) 页面。

有关服务账户、信任边界与安全注意事项的更多信息，请参阅 [Security Model](./Security) 文档。

## Verifying the Service

安装后：

```powershell
sc.exe query MyService
sc.exe start MyService
sc.exe stop MyService
```

或使用 Servy CLI：

```powershell
servy-cli status --name="MyService"
servy-cli start --name="MyService"
servy-cli stop --name="MyService"
```

或打开 **Servy Manager** 以：

* 启动 / 停止服务
* 查看日志
* 调整重启与失败策略
* 查看 CPU / 内存实时性能图表
* 预览服务 `stdout`/`stderr`
* 预览服务依赖

## JavaScript & TypeScript Runtimes

### Run a Node.js / Next.js / Express App as a Service
```powershell
servy-cli install `
  --name="MyNodeApp" `
  --description="Node.js Express API" `
  --path="C:\Program Files\nodejs\node.exe" `
  --params="server.js" `
  --startupDir="C:\apps\myapp" `
  --startupType="Automatic"
```

将 npm 脚本（`npm start`）作为服务运行：
```powershell
servy-cli install `
  --name="MyNodeApp" `
  --description="Node.js App via npm" `
  --path="C:\Program Files\nodejs\npm.cmd" `
  --params="start" `
  --startupDir="C:\apps\myapp" `
  --startupType="Automatic"
```

将 Next.js 生产应用作为服务运行：
```powershell
servy-cli install `
  --name="MyNextApp" `
  --description="Next.js App" `
  --path="C:\Program Files\nodejs\npm.cmd" `
  --params="start" `
  --startupDir="C:\apps\myapp" `
  --startupType="Automatic"
```
这将运行：
```text
npm start -> next start
```
这是在生产环境中运行 Next.js 的正确方式。

### Run a Deno App as a Service

```powershell
servy-cli install `
  --name="MyDenoService" `
  --description="Deno background script" `
  --path="C:\tools\deno\deno.exe" `
  --params="run --allow-net worker.ts" `
  --startupDir="C:\apps\deno" `
  --startupType="Automatic"
```

**说明：**

* `--allow-net` 标志仅为示例；按需添加其他权限（`--allow-read`、`--allow-write` 等）。
* 适用于脚本与 Deno HTTP 服务器。

### Run a Bun App as a Service

```powershell
servy-cli install `
  --name="MyBunService" `
  --description="Bun backend service" `
  --path="C:\tools\bun\bun.exe" `
  --params="C:\apps\bun\server.ts" `
  --startupDir="C:\apps\bun" `
  --startupType="Automatic"
```

**说明：**

* Bun 会自动检测文件是脚本还是服务器。
* 如需可在 `--params` 中传递 `--port 3000` 等参数。

## Containers & Infrastructure

### Run a Docker Container as a Service

```powershell
servy-cli install `
  --name="MyDockerService" `
  --description="Docker container service" `
  --path="C:\Program Files\Docker\Docker\resources\bin\docker.exe" `
  --params="run --rm --name myapp -p 8080:80 myimage:latest" `
  --startupDir="C:\Program Files\Docker\Docker\resources\bin" `
  --startupType="Automatic"
```

**说明：**

* `--rm` 确保容器停止时被清理。
* 按需调整 `-p` 与 `myimage:latest`。

### Run a Docker Compose Stack as a Service

当你希望整个容器堆栈在开机时自动启动，而不依赖 Docker Desktop 的自动启动行为时，这很有用。

```powershell
servy-cli install `
  --name="MyDockerComposeService" `
  --description="Docker Compose stack service" `
  --path="C:\Program Files\Docker\Docker\resources\bin\docker-compose.exe" `
  --params="-f C:\apps\mycompose\docker-compose.yml up" `
  --startupDir="C:\apps\mycompose" `
  --startupType="Automatic"
```

**说明：**

* 这将整个 `docker-compose.yml` 堆栈作为后台服务运行。
* 不要添加 `--detach`：它会使 `docker-compose` 在启动容器后立即退出，服务会马上停止（参见下方 *Service starts then stops immediately*），且停止服务时也不会再关闭堆栈。前台模式下服务状态跟踪堆栈，停止服务会干净地关闭容器。

### Run an Nginx Web Server as a Service

将 Nginx 的 Windows 端口作为服务运行，可确保 Web 服务器在重启后持续存在。

```powershell
servy-cli install `
  --name="Nginx" `
  --description="Nginx Web Server" `
  --path="C:\nginx\nginx.exe" `
  --params='-g "daemon off;"' `
  --startupDir="C:\nginx" `
  --startupType="Automatic"
```

为确保日志输出一致为 UTF-8（尤其在投递日志或使用非 ASCII 字符时），用 cmd.exe 包装 Nginx 并强制代码页 65001：

```powershell
servy-cli install `
  --name="Nginx" `
  --description="Nginx Web Server" `
  --path="C:\Windows\System32\cmd.exe" `
  --params='/c "C:\nginx\start-nginx.cmd"' `
  --startupDir="C:\nginx" `
  --startupType="Automatic"
```

其中 `start-nginx.cmd` 如下：
```cmd
@echo off
chcp 65001 >nul
cd /d C:\nginx
nginx.exe -g "daemon off;"
```

Nginx 日志位于：`C:\nginx\logs\`

### Run a Redis Server as a Service

```powershell
servy-cli install `
  --name="Redis" `
  --description="Redis In-Memory Data Store" `
  --path="C:\redis\redis-server.exe" `
  --params="redis.windows.conf" `
  --startupDir="C:\redis" `
  --startupType="Automatic"
```

> [!NOTE]
> Redis for Windows 未获 Redis Labs 官方支持。生产环境建议在 Docker 或 WSL 中运行 Redis。

### Run a PocketBase Instance as a Service

PocketBase 是单文件后端，作为服务运行非常高效。

```powershell
servy-cli install `
  --name="PocketBase" `
  --description="PocketBase backend" `
  --path="C:\apps\pocketbase\pocketbase.exe" `
  --params="serve --http=0.0.0.0:8090" `
  --startupDir="C:\apps\pocketbase" `
  --startupType="Automatic"
```

## AI & Modern Tools

### Run an Ollama AI Instance as a Service

让本地 LLM API 在后台保持可用。

```powershell
servy-cli install `
  --name="Ollama" `
  --description="Ollama Local AI API" `
  --path="C:\Users\Admin\AppData\Local\Programs\Ollama\ollama.exe" `
  --params="serve" `
  --startupType="Automatic"
```

### Run a Ghost CMS Instance as a Service

```powershell
servy-cli install `
  --name="GhostCMS" `
  --path="C:\Program Files\nodejs\node.exe" `
  --params="current\index.js" `
  --startupDir="C:\var\www\ghost" `
  --startupType="Automatic"
```

## Compiled Languages

### Run a Go App as a Service

```powershell
servy-cli install `
  --name="MyGoService" `
  --description="Go background service" `
  --path="C:\apps\my-go-app\my-go-app.exe" `
  --params="--port=8080 --mode=worker" `
  --startupDir="C:\apps\my-go-app" `
  --startupType="Automatic"
```

### Run a Rust App as a Service

```powershell
servy-cli install `
  --name="MyRustService" `
  --description="Rust background service" `
  --path="C:\apps\rustsvc\rust_svc.exe" `
  --startupDir="C:\apps\rustsvc" `
  --startupType="Automatic"
```

### Run a C / C++ Compiled App as a Service

```powershell
servy-cli install `
  --name="MyCppService" `
  --description="C++ Application" `
  --path="C:\apps\cpp-service\service.exe" `
  --startupDir="C:\apps\cpp-service" `
  --startupType="Automatic"
```

### Run a Zig App as a Service
```powershell
servy-cli install `
  --name="MyZigService" `
  --description="Zig background worker" `
  --path="C:\apps\zig\myapp.exe" `
  --startupDir="C:\apps\zig" `
  --startupType="Automatic"
```

### Run a Pascal / Delphi App as a Service
```powershell
servy-cli install `
  --name="MyPascalService" `
  --description="Delphi background app" `
  --path="C:\apps\pascal\worker.exe" `
  --startupDir="C:\apps\pascal" `
  --startupType="Automatic"
```

### Run a Fortran App as a Service
```powershell
servy-cli install `
  --name="MyFortranService" `
  --description="Fortran computational service" `
  --path="C:\apps\fortran\worker.exe" `
  --startupDir="C:\apps\fortran" `
  --startupType="Automatic"
```

## Managed Runtimes

### Run a .NET App as a Service

```powershell
servy-cli install `
  --name="MyDotNetApp" `
  --description=".NET Worker Service" `
  --path="C:\apps\dotnetapp\MyApp.exe" `
  --startupDir="C:\apps\dotnetapp" `
  --startupType="Automatic"
```

或者，若使用带 DLL 的 dotnet 运行时：

```powershell
servy-cli install `
  --name="MyDotNetApp" `
  --description=".NET Worker Service" `
  --path="C:\Program Files\dotnet\dotnet.exe" `
  --params="C:\apps\dotnetapp\app.dll" `
  --startupDir="C:\apps\dotnetapp" `
  --startupType="Automatic"
```

### Run a Java JAR as a Service
```powershell
servy-cli install `
  --name="MyJavaService" `
  --description="Java Spring Boot App" `
  --path="%JAVA_HOME%\bin\java.exe" `
  --params="-jar C:\apps\springboot\app.jar" `
  --startupDir="C:\apps\springboot" `
  --startupType="Automatic"
```

通过使用 `%JAVA_HOME%` 系统环境变量，Servy 在运行时解析正确的 Java 安装路径，而不依赖硬编码版本。这确保服务在 Java 更新后仍可工作，并使配置在不同环境间可移植。

### Run an Elixir Script as a Service

```powershell
servy-cli install `
  --name="MyElixirService" `
  --description="Elixir background worker" `
  --path="C:\Windows\System32\cmd.exe" `
  --params='/c "C:\Program Files\Elixir\bin\elixir.bat C:\apps\elixir\worker.exs"' `
  --startupDir="C:\apps\elixir" `
  --startupType="Automatic"
```
对于 Elixir，可将 `--params` 指向任意 `.exs` 脚本或 mix 任务。

### Run an Erlang Script as a Service

```powershell
servy-cli install `
  --name="MyErlangService" `
  --description="Erlang background worker" `
  --path="C:\Program Files\erl-25.3\bin\erl.exe" `
  --params="-noshell -s my_app start -s init stop" `
  --startupDir="C:\apps\erlang" `
  --startupType="Automatic"
```
对于 Erlang，`-s` 参数启动所需的模块与函数。请按你的 OTP 应用调整。

## Scripting & Automation

### Run a PowerShell script as a Service

```powershell
servy-cli install `
  --name="MyPowerShellScript" `
  --description="PowerShell automation job" `
  --path="C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" `
  --params='-File "C:\scripts\script\my script.ps1"' `
  --startupDir="C:\scripts\script" `
  --startupType="Automatic"
```

### Run a Batch File as a Service

```powershell
servy-cli install `
  --name="MyBatchScript" `
  --description="Batch automation job" `
  --path="C:\Windows\System32\cmd.exe" `
  --params="/c C:\scripts\backup-job.bat" `
  --startupDir="C:\scripts" `
  --startupType="Automatic"
```

### Run a Python Script as a Service
```powershell
servy-cli install `
  --name="MyPythonJob" `
  --description="Python background job" `
  --path="C:\Python311\python.exe" `
  --params="C:\apps\scripts\job.py" `
  --startupDir="C:\apps\scripts" `
  --startupType="Automatic"
```

#### Why `--enableConsoleUI` Is Required for Some Python Applications

许多 Python 库与编排框架（例如 **Prefect**、**Rich**、**Colorama**）会尝试初始化颜色、进度条或高级日志等交互式终端功能。作为标准 Windows 服务在 **Session 0** 中运行时，这些应用通常因找不到有效终端句柄（stdin/stdout/stderr）而无法启动。

设置 `--enableConsoleUI` 选项会指示 Servy：

* 在 Session 0 内为被包装进程**分配真实的控制台缓冲区**。
* **提供有效的终端句柄**，满足应用对控制台环境的要求。
* **确保稳定性**，避免脚本在尝试访问控制台时因 `Illegal operation on a terminal` 或 `No such file or directory` 错误而立即退出。

### Run a PHP App as a Service

```powershell
servy-cli install `
  --name="MyPHPWorker" `
  --description="PHP queue worker" `
  --path="C:\php\php.exe" `
  --params="C:\apps\worker\queue-worker.php" `
  --startupDir="C:\apps\worker" `
  --startupType="Automatic"
```

### Run a Laravel Queue Worker as a Service

适合处理 PHP/Laravel 应用中的后台任务。

```powershell
servy-cli install `
  --name="LaravelWorker" `
  --description="Laravel Queue Worker" `
  --path="C:\php\php.exe" `
  --params="artisan queue:work --tries=3" `
  --startupDir="C:\inetpub\wwwroot\myapp" `
  --startupType="Automatic"
```

### Run a Ruby App as a Service

```powershell
servy-cli install `
  --name="MyRubyApp" `
  --description="Ruby background app" `
  --path="C:\Ruby32\bin\ruby.exe" `
  --params="C:\apps\rubyapp\app.rb" `
  --startupDir="C:\apps\rubyapp" `
  --startupType="Automatic"
```

### Run a VBScript as a Service

```powershell
servy-cli install `
  --name="MyVBScript" `
  --description="VBScript automation job" `
  --path="C:\Windows\System32\cscript.exe" `
  --params="C:\scripts\tasks\job.vbs" `
  --startupDir="C:\scripts\tasks" `
  --startupType="Automatic"
```

若你更喜欢 wscript.exe（带窗口，但仍可作为服务工作）：

```powershell
servy-cli install `
  --name="MyVBScript" `
  --description="VBScript automation job" `
  --path="C:\Windows\System32\wscript.exe" `
  --params="C:\scripts\tasks\job.vbs" `
  --startupDir="C:\scripts\tasks" `
  --startupType="Automatic"
```

### Run an AutoHotkey script as a Service

将 AutoHotkey（AHK）作为服务运行，可让脚本在用户登录前执行。

> [!WARNING]
> **桌面交互：** Windows 服务运行在 Session 0。这意味着需要 GUI 交互、鼠标移动或向活动窗口发送按键的脚本无法正确工作。服务模式适用于后台文件监控或仅逻辑脚本。

```powershell
servy-cli install `
  --name="MyAutoHotkeyService" `
  --description="AutoHotkey background script" `
  --path="C:\Program Files\AutoHotkey\v2\AutoHotkey.exe" `
  --params="C:\scripts\service.ahk" `
  --startupDir="C:\scripts" `
  --startupType="Automatic"
```

### Run a WSL Bash Script as a Service

```powershell
servy-cli install `
  --name="MyWSLScript" `
  --description="WSL Bash script service" `
  --path="C:\Windows\System32\wsl.exe" `
  --params="bash /home/user/scripts/run.sh" `
  --startupDir="C:\Windows\System32" `
  --startupType="Automatic"
```

若需要特定发行版：

```powershell
servy-cli install `
  --name="MyUbuntuWSLService" `
  --description="WSL Ubuntu service job" `
  --path="C:\Windows\System32\wsl.exe" `
  --params="-d Ubuntu bash /home/user/app/start.sh" `
  --startupDir="C:\Windows\System32" `
  --startupType="Automatic"
```

> [!WARNING]
> **按用户注册的发行版：** WSL 发行版按用户账户注册。在默认的 **LocalSystem** 账户下，`wsl.exe` 找不到发行版，服务会立即退出（`WSL_E_DISTRO_NOT_FOUND`）。请使用拥有该发行版的账户通过 `--user` 安装服务（参见 *Service Account & Permissions*），并注意 WSL VM 生命周期随后会遵循该用户的会话策略。

## Data Science & Analytics

### Run a Julia Script as a Service

*（用于 ML 任务、分析、长时间运行的计算 Worker）*

```powershell
servy-cli install `
  --name="MyJuliaService" `
  --description="Julia analytics worker" `
  --path="C:\Julia-1.10\bin\julia.exe" `
  --params="C:\apps\julia\worker.jl" `
  --startupDir="C:\apps\julia" `
  --startupType="Automatic"
```

### Run an R Script as a Service
```powershell
servy-cli install `
  --name="MyRService" `
  --description="R background job" `
  --path="C:\Program Files\R\R-4.4.1\bin\Rscript.exe" `
  --params="C:\apps\r\job.R" `
  --startupDir="C:\apps\r" `
  --startupType="Automatic"
```

## Other Languages & Tools

### Run a Haskell App as a Service
```powershell
servy-cli install `
  --name="MyHaskellService" `
  --description="Haskell background worker" `
  --path="C:\apps\haskell\myapp.exe" `
  --startupDir="C:\apps\haskell" `
  --startupType="Automatic"
```

### Run a Dart Server or Script as a Service

*（Shelf Web 服务、后台 Worker、API 服务器等）*

```powershell
servy-cli install `
  --name="MyDartService" `
  --description="Dart backend service" `
  --path="C:\tools\dart-sdk\bin\dart.exe" `
  --params="C:\apps\dart\server.dart" `
  --startupDir="C:\apps\dart" `
  --startupType="Automatic"
```

### Run a Lua Script as a Service
```powershell
servy-cli install `
  --name="MyLuaService" `
  --description="Lua automation script" `
  --path="C:\Lua\5.4\lua.exe" `
  --params="C:\apps\lua\script.lua" `
  --startupDir="C:\apps\lua" `
  --startupType="Automatic"
```

### Run a Perl Script as a Service

```powershell
servy-cli install `
  --name="MyPerlService" `
  --description="Perl Script" `
  --path="C:\Perl64\bin\perl.exe" `
  --params="C:\apps\perl-task\task.pl" `
  --startupDir="C:\apps\perl-task" `
  --startupType="Automatic"
```

### Run an OCaml Script or App as a Service
```powershell
servy-cli install `
  --name="MyOCamlService" `
  --description="OCaml background worker" `
  --path="C:\OCaml\bin\ocaml.exe" `
  --params="C:\apps\ocaml\worker.ml" `
  --startupDir="C:\apps\ocaml" `
  --startupType="Automatic"
```

若已将 OCaml 代码编译为原生可执行文件（例如 `worker.exe`），可跳过 `ocaml.exe`，直接将 `--path` 设为该可执行文件：
```powershell
servy-cli install `
  --name="MyOCamlService" `
  --description="OCaml compiled worker" `
  --path="C:\apps\ocaml\worker.exe" `
  --startupDir="C:\apps\ocaml" `
  --startupType="Automatic"
```

这样即可使用 Servy 将脚本或已编译的 OCaml 应用作为 Windows 服务运行。

### Run Kopia as a Service
将 Kopia 设为 Windows 服务是明智之举。它确保备份在后台运行而无需用户登录，使用 Servy 可使过程直截了当。

为避免备份服务器凭据暴露在命令行历史、日志或进程枚举工具中，请使用 `SERVY_PROCESS_PARAMETERS` 环境变量安全传递启动参数（自 Servy v8.5 起）：

```powershell
# Set the sensitive arguments securely inside your current session memory
$env:SERVY_PROCESS_PARAMETERS = 'server start --insecure --address=127.0.0.1:51515 --server-username=admin --server-password=somepwd'

# Install the service without passing secrets over the command line
servy-cli install `
  --name="KopiaService" `
  --description="Kopia Service" `
  --path="C:\Program Files\KopiaUI\resources\server\kopia.exe" `
  --startupDir="C:\Program Files\KopiaUI\resources\server" `
  --startupType="Automatic" `
  --stdout="C:\Program Files\KopiaUI\resources\server\stdout.log" `
  --stderr="C:\Program Files\KopiaUI\resources\server\stderr.log" `
  --enableSizeRotation `
  --rotationSize="10"

# Clear the session variable after deployment
Remove-Item Env:SERVY_PROCESS_PARAMETERS
```

> [!WARNING]
> **生产安全警告：** 通过原始 CLI 参数（`--params`）直接传递敏感字段，可能将密码暴露给任何系统监控工具或有进程列表权限的用户。部署真实生产密码时，务必使用上方所示的 `SERVY_PROCESS_PARAMETERS` 传递模式。更多详情请参阅 [Security Guidance](./Security#6-sensitive-command-line-arguments--service-account-credentials)。

* `server start`：使 Kopia 作为本地服务器在后台运行。
* `--insecure`：由于其仅监听 127.0.0.1（localhost），对本地设置通常可以接受，但如有需要可配置 TLS。
* `--address`：定义 Kopia UI/API 所在的端口。

按需调整 Kopia 命令参数。参见[官方文档](https://kopia.io/docs/reference/command-line/common/server-start/)。

Kopia 作为服务（服务器模式）运行后，通过 Web UI（最简单）或命令行使用 Kopia 的内部 Policy 系统来安排快照。

## Common Problems & Fixes

### Service starts then stops immediately
* 可执行文件立即退出
* `--params` 中缺少参数
* 工作目录不正确

**修复：** 在终端中手动运行相同命令。

### Works in terminal but not as a service
* 应用依赖用户特定的环境变量
* 使用了映射的网络驱动器
* NTFS 权限不足

**修复：** 使用专用服务账户与系统范围的环境变量。

### No logs are produced
* 应用向相对路径写日志
* 启动目录不正确

**修复：** 显式设置 `--startupDir` 并验证日志路径。

## Tips & Notes

若应用可从命令行启动，Servy 即可将其作为 Windows 服务运行。

若不能，Servy 不会掩盖问题。它会通过日志与退出码清楚地暴露问题。

* **环境变量：** 若应用依赖特定环境变量（如 `JAVA_HOME`），请确保将其设为系统变量，因为服务默认在 SYSTEM 账户下运行。
* **日志：** 使用 Servy Manager 配置日志轮转，并捕获 `stdout`/`stderr` 以调试后台服务。
* **权限：** 确保服务账户对应用的启动目录与 `%ProgramData%\Servy` 具有 NTFS **Modify** 权限，并且已对 `%ProgramData%\Servy` 运行强制加固脚本。参见 [Executable Permission Hardening](./Security#executable-permission-hardening-mandatory)。

## See Also
* [Installation Guide](./Installation-Guide)
* [Overview](./Overview)
* [Servy CLI](./Servy-CLI)
* [Servy PowerShell Module](./Servy-PowerShell-Module)
* [Servy Automation & CI/CD](./Servy-Automation-&-CI-CD)
