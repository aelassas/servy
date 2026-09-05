## 目录

1. [简介](#简介)
1. [基本用法](#基本用法)
1. [命令帮助](#命令帮助)
1. [Install 命令](#install-命令)
1. [其他命令](#其他命令)
1. [提示](#提示)
1. [另见](#另见)

> [!IMPORTANT]
> **控制台 UI 兼容性**
> 若应用尝试在视觉上更新命令提示符（如清屏或移动光标），在作为后台服务运行时会崩溃，因为服务没有可见窗口。无需修改代码即可修复：在安装服务时于服务配置中启用 `--enableConsoleUI` 选项。

## 简介

Servy 包含专为完整脚本编写、自动化部署以及与 CI/CD 流水线无缝集成而设计的命令行界面（CLI）。

该 CLI 提供轻量、便于脚本的桌面应用替代方案，专注于自动化与无界面场景，同时复用与桌面应用相同的核心服务管理逻辑。

默认安装后，`servy-cli` 会位于系统 **PATH** 中——控制此项的选项以及便携包说明见 [安装指南](./Installation-Guide#add-servy-to-path)。

> [!NOTE]
> 在 servy-cli 中，使用单字符快捷方式（如 `-n` 或 `-p`）时不支持等号（`=`）。

## 基本用法
首先，打开提升权限的命令提示符或 PowerShell 窗口并运行：
```text
PS> servy-cli help
Servy.CLI <version>+<commit>
Copyright © 2026 Akram El Assas. All rights reserved.

  install      Install a Windows service.

  uninstall    Uninstall a Windows service.

  start        Start a Windows service.

  stop         Stop a Windows service.

  status       Get the current status of a Windows service. Possible results:
               NotInstalled, Stopped, StartPending, StopPending, Running,
               ContinuePending, PausePending, Paused, Unknown.

  restart      Restart a Windows service.

  export       Export a Servy Windows service configuration to a configuration
               file.

  import       Import a Windows service configuration into the Servy database and
               optionally install the service.

  help         Display more information on a specific command.

  version      Display version information.
```

## 命令帮助
要查看任意命令的详细帮助，在命令名后追加 `--help`。例如，获取 `start` 命令的帮助：
```text
PS> servy-cli start --help
Servy.CLI <version>+<commit>
Copyright © 2026 Akram El Assas. All rights reserved.

  -n, --name     Required. Name of the service to start.

  -q, --quiet    Suppress spinner and run in non-interactive mode.

  --help         Display this help screen.

  --version      Display version information.
```

## Install 命令
安装或更新 Windows 服务的主要命令是 `install`。

**快速示例**

以下命令将 `MyApp.exe` 安装为名为 `MyService` 的 Windows 服务：

```cmd
servy-cli install --name="MyService" --path="C:\path\to\MyApp.exe"
```

**详细用法**

以下是其详细用法：

```text
PS> servy-cli install --help
Servy.CLI <version>+<commit>
Copyright © 2026 Akram El Assas. All rights reserved.

  -n, --name                      Required. Unique service name to install.

  --displayName                   The human-readable name shown in the Windows Services
                                  console (services.msc). If left empty, the service
                                  name will be used instead.

  -d, --description               Description of the service.

  -p, --path                      Required. Path to the executable process. Supports
                                  environment variable expansion, example:
                                  %JAVA_HOME%\bin\java.exe

  --startupDir                    Startup directory for the process. Supports
                                  environment variable expansion, example:
                                  %PROGRAMDATA%\MyApp

  --params                        Additional parameters for the process. Supports
                                  environment variable expansion, example:
                                  --params="--data %ProgramData%\MyApp --bin
                                  %MY_VAR%\bin". SECURITY WARNING: Use the
                                  SERVY_PROCESS_PARAMETERS environment variable instead
                                  to avoid exposing sensitive parameters in OS process
                                  listings.

  --startupType                   Service startup type. Options: Automatic,
                                  AutomaticDelayedStart, Manual, Disabled. Defaults to
                                  Automatic.

  --priority                      Process priority level. Options: Idle, BelowNormal,
                                  Normal, AboveNormal, High, RealTime. Defaults to
                                  Normal.

  -a, --cpuAffinity               Logical CPUs the process may run on (e.g., '0-3,8' or
                                  '0xFF00').

  --startTimeout                  Timeout in seconds to wait for the process to start
                                  successfully before considering the startup as failed.
                                  Must be between 1 and 86400 seconds. Defaults to 10
                                  seconds.

  --stopTimeout                   Timeout in seconds to wait for the process to exit.
                                  Must be between 1 and 86400 seconds. Defaults to 5
                                  seconds.

  --enableConsoleUI               Enable console user interface for the service. When
                                  enabled, stdout/stderr redirection is disabled.

  --stdout                        Path to stdout log file.

  --stderr                        Path to stderr log file.

  --enableRotation                Deprecated. Enable size-based log rotation. This
                                  option is kept only for backward compatibility. Use
                                  --enableSizeRotation instead.

  --enableSizeRotation            Enable size-based log rotation.

  --rotationSize                  Log rotation size in Megabytes (MB). Must be between 1
                                  and 10240 MB.

  --enableDateRotation            Enable date-based log rotation based on the date
                                  interval specified by --dateRotationType. When both
                                  size-based and date-based rotation are enabled, size
                                  rotation takes precedence.

  --dateRotationType              Date rotation type. Options: Daily, Weekly, Monthly,
                                  None (None disables date-based rotation; use when only
                                  size rotation is desired).

  --maxRotations                  Maximum rotated log files to keep. Must be between 0
                                  and 10000. Set to 0 or leave empty for unlimited.

  --useLocalTimeForRotation       Use local server time for log rotation instead of UTC.
                                  Default is false.

  --debug                         Whether debug logs are enabled. When enabled,
                                  environment variables and process parameters are
                                  recorded in the Servy.Service.log file. Not
                                  recommended for production environments, as these logs
                                  may contain sensitive information.

  --enableHealth                  Enable health monitoring.

  --heartbeatInterval             Heartbeat interval in seconds. Must be between 5 and
                                  86400 seconds.

  --maxFailedChecks               Maximum allowed failed health checks. Must be between
                                  1 and 100000.

  --recoveryAction                Recovery action on failure. Options: None,
                                  RestartService, RestartProcess, RestartComputer.
                                  Restart service and restart computer actions are not
                                  available if the service runs under NT
                                  AUTHORITY\NetworkService, NT AUTHORITY\LocalService,
                                  or a user account without the required privileges.
                                  Only the restart process action will be available for
                                  these accounts.

  --recoveryOnCleanExit           Enable running recovery action even if the process
                                  exits successfully. Default is false.

  --maxRestartAttempts            Maximum restart attempts on failure. Must be between 0
                                  and 100000. Set to 0 for unlimited restart attempts.

  --heartbeatUrl                  Absolute URL for out-of-band diagnostic heartbeat
                                  pings. Only used when health monitoring is enabled.

  --heartbeatUrlTimeoutSeconds    Timeout in seconds for external heartbeat URL
                                  requests. Must be between 2 and 30 seconds. Defaults
                                  to 10 seconds.

  --enableHeartbeatUrlFlags       Append /start and /fail to the heartbeat URL on
                                  service start and failure.

  --failureProgramPath            The failure program path. Configure a script or
                                  executable to run when the wrapped process exits with
                                  a non-zero exit code (recovery disabled) or after all
                                  recovery action retries have failed (recovery
                                  enabled). It is not run when the process fails to
                                  start; that path stops the service. Supports
                                  environment variable expansion, example:
                                  %JAVA_HOME%\bin\java.exe

  --failureProgramStartupDir      Specifies the directory in which the failure program
                                  will start. If not set, defaults to the service
                                  working directory. Supports environment variable
                                  expansion, example: %PROGRAMDATA%\MyApp

  --failureProgramParams          Additional parameters for the failure program.
                                  SECURITY WARNING: Use the
                                  SERVY_FAILURE_PROGRAM_PARAMETERS environment variable
                                  instead to avoid exposing sensitive parameters in OS
                                  process listings.

  --envVars                       Environment variables for the process. Enter variables
                                  in the format varName=varValue separated by semicolons
                                  (;). Use \= to escape '=', \" to escape '"', \; to
                                  escape ';', \\ to escape '\', and %% to escape '%'
                                  (collapses to a single '%'). Supports environment
                                  variable expansion, example: VAR1=%ProgramData%\MyApp;
                                  VAR2=%VAR1%\bin. SECURITY WARNING: Use the
                                  SERVY_ENVIRONMENT_VARIABLES environment variable
                                  instead to avoid exposing sensitive parameters in OS
                                  process listings.

  --deps                          Specify one or more Windows service names (not display
                                  names) that this service depends on separated with
                                  semicolons (;). Each service name must contain only
                                  letters, digits, hyphens, underscores, periods,
                                  spaces, and dollar signs ($), optionally preceded by
                                  '+' to reference a load-order group, and must not
                                  exceed 256 characters. Windows starts stopped
                                  dependencies automatically when this service starts;
                                  if a dependency is disabled or fails to start, this
                                  service will not start.

  --user                          The service account username (e.g., .\username,
                                  DOMAIN\username, DOMAIN\gMSA$, or a built-in identity
                                  such as NT AUTHORITY\LocalService, NT
                                  AUTHORITY\NetworkService, NT SERVICE\MyService or IIS
                                  APPPOOL\MyPool). Leave the --password option or
                                  SERVY_PASSWORD environment variable empty for
                                  built-in, virtual and gMSA accounts. If this option is
                                  not set, the service runs under Local System. If the
                                  service runs under an account other than Local System,
                                  you must grant Modify access to %ProgramData%\Servy
                                  for the account running the service and execute the
                                  mandatory hardening script:
                                  Set-ServyExePermissions.ps1 -TargetAccount
                                  "domain\user" to prevent unprivileged binary tampering
                                  and local privilege escalation. Learn script location
                                  and execution usage at:
                                  https://github.com/aelassas/servy/wiki/Security#execut
                                  able-permission-hardening-mandatory

  --password                      The service account password. SECURITY WARNING: Use
                                  the SERVY_PASSWORD environment variable instead to
                                  avoid exposing credentials in OS process listings.

  --preLaunchPath                 The pre-launch executable path. Configure an optional
                                  script or executable to run before the main service
                                  starts. This is useful for preparing configurations,
                                  fetching secrets, or other setup tasks. If the
                                  pre-launch script fails, the service will not start
                                  unless you enable --preLaunchIgnoreFailure. Supports
                                  environment variable expansion, example:
                                  %JAVA_HOME%\bin\java.exe

  --preLaunchStartupDir           Specifies the directory in which the pre-launch
                                  executable will start. If not set, defaults to the
                                  service working directory. Supports environment
                                  variable expansion, example: %PROGRAMDATA%\MyApp

  --preLaunchParams               Additional parameters for the pre-launch executable.
                                  SECURITY WARNING: Use the SERVY_PRE_LAUNCH_PARAMETERS
                                  environment variable instead to avoid exposing
                                  sensitive parameters in OS process listings.

  --preLaunchEnv                  Environment variables for the pre-launch executable.
                                  Enter variables in the format varName=varValue
                                  separated by semicolons (;). Use \= to escape '=', \"
                                  to escape '"', \; to escape ';', \\ to escape '\', and
                                  %% to escape '%' (collapses to a single '%'). Supports
                                  environment variable expansion, example:
                                  VAR1=%ProgramData%\MyApp; VAR2=%VAR1%\bin. SECURITY
                                  WARNING: Use the
                                  SERVY_PRE_LAUNCH_ENVIRONMENT_VARIABLES environment
                                  variable instead to avoid exposing sensitive
                                  parameters in OS process listings.

  --preLaunchStdout               Path to stdout log file of the pre-launch executable.

  --preLaunchStderr               Path to stderr log file of the pre-launch executable.

  --preLaunchTimeout              Timeout for the pre-launch executable. Must be between
                                  0 and 86400 seconds. Set the timeout to 0 to run the
                                  pre-launch hook in fire-and-forget mode. When set to
                                  0, the hook is started and the service is launched
                                  immediately without waiting for completion. Use this
                                  only for tasks that do not affect the service's
                                  ability to start or run correctly. Stdout/Stderr
                                  redirection and retries are not available in
                                  fire-and-forget mode.

  --preLaunchRetryAttempts        Number of retry attempts for the pre-launch executable
                                  if it fails. Must be between 0 and 100000.

  --preLaunchIgnoreFailure        Ignore failure and start service even if pre-launch
                                  executable fails.

  --postLaunchPath                The post-launch executable path. Configure an optional
                                  script or executable to run after the process starts
                                  successfully. Supports environment variable expansion,
                                  example: %JAVA_HOME%\bin\java.exe

  --postLaunchStartupDir          Specifies the directory in which the post-launch
                                  executable will start. If not set, defaults to the
                                  service working directory. Supports environment
                                  variable expansion, example: %PROGRAMDATA%\MyApp

  --postLaunchParams              Additional parameters for the post-launch executable.
                                  SECURITY WARNING: Use the SERVY_POST_LAUNCH_PARAMETERS
                                  environment variable instead to avoid exposing
                                  sensitive parameters in OS process listings.

  --preStopPath                   The pre-stop executable path. Configure an optional
                                  script or executable to run before the main service
                                  stops. This can be used for graceful shutdown tasks
                                  such as notifying external systems or draining
                                  resources. The pre-stop process runs synchronously and
                                  extends the service stop timeout while it is running.
                                  Set the timeout to 0 to run the pre-stop process in
                                  fire-and-forget mode. Supports environment variable
                                  expansion, example: %JAVA_HOME%\bin\java.exe

  --preStopStartupDir             Specifies the directory in which the pre-stop
                                  executable will start. If not set, defaults to the
                                  service working directory. Supports environment
                                  variable expansion, example: %PROGRAMDATA%\MyApp

  --preStopParams                 Additional parameters for the pre-stop executable.
                                  SECURITY WARNING: Use the SERVY_PRE_STOP_PARAMETERS
                                  environment variable instead to avoid exposing
                                  sensitive parameters in OS process listings.

  --preStopTimeout                Timeout for the pre-stop executable. Set the timeout
                                  to 0 to run the pre-stop process in fire-and-forget
                                  mode. Must be between 0 and 86400 seconds.

  --preStopLogAsError             Log pre-stop failure as error.

  --postStopPath                  The post-stop executable path. Configure an optional
                                  script or executable to run after the wrapped process
                                  and all of its child processes have exited. The
                                  post-stop process is started in fire-and-forget mode
                                  and does not block service shutdown. Supports
                                  environment variable expansion, example:
                                  %JAVA_HOME%\bin\java.exe

  --postStopStartupDir            Specifies the directory in which the post-stop
                                  executable will start. If not set, defaults to the
                                  service working directory. Supports environment
                                  variable expansion, example: %PROGRAMDATA%\MyApp

  --postStopParams                Additional parameters for the post-stop executable.
                                  SECURITY WARNING: Use the SERVY_POST_STOP_PARAMETERS
                                  environment variable instead to avoid exposing
                                  sensitive parameters in OS process listings.

  -q, --quiet                     Suppress spinner and run in non-interactive mode.

  --help                          Display this help screen.

  --version                       Display version information.
```

> [!NOTE]
> 若 `install` 命令中 `--params` 的值包含以 `--` 开头的参数，请使用等号（=）以避免解析问题。示例：`--params="--mode=production --port=7008"`。若没有等号，CLI 可能会将 `--mode` 或 `--port` 解释为自己的选项，而不是服务参数的一部分。

> [!IMPORTANT]
> 若服务在 Local System 以外的账户下运行，必须为该服务账户授予对 `%ProgramData%\Servy` 的 **Modify** 权限，并运行强制加固脚本（`Set-ServyExePermissions.ps1 -TargetAccount "domain\user"`），将二进制可执行文件锁定为 **Read & Execute**，以防止无特权二进制篡改与本地权限提升。脚本位置与执行说明见 [可执行文件权限加固指南](./Security#executable-permission-hardening-mandatory)。

> [!IMPORTANT]
> **安全最佳实践：** 避免在生产环境或脚本中使用敏感标志（例如 `--password`、`--params`、`--envVars`、`--preLaunchEnv`）。将这些值作为命令行参数传递会使其对能够访问 Windows 进程列表或 shell 历史文件的任何用户或进程可见。应改为在运行 install 命令前设置对应的环境变量（例如 `SERVY_PASSWORD`、`SERVY_PROCESS_PARAMETERS`、`SERVY_ENVIRONMENT_VARIABLES`）。更多信息见 [安全](./Security#6-sensitive-command-line-arguments--service-account-credentials) 页面。

以下是使用安全环境变量回退模式安装服务的**推荐**方式：

```powershell
# 1. 在当前进程环境中设置敏感值
$env:SERVY_PASSWORD              = "your_secret_password"
$env:SERVY_PROCESS_PARAMETERS    = "C:\Apps\App\index.js"
$env:SERVY_ENVIRONMENT_VARIABLES = "ENV_VAR1=VAL1; ENV_VAR2=VAL2;"

# 2. 运行不带敏感 CLI 标志的 install 命令
servy-cli install `
  --name="My NodeJS Service" `
  --description="My NodeJS Server" `
  --path="C:\Program Files\nodejs\node.exe" `
  --startupDir="C:\Apps\App" `
  --startupType="Automatic" `
  --priority="Normal" `
  --stdout="C:\Apps\App\stdout.log" `
  --stderr="C:\Apps\App\stderr.log" `
  --enableSizeRotation `
  --rotationSize=10 `
  --enableHealth `
  --heartbeatInterval=10 `
  --maxFailedChecks=3 `
  --recoveryAction="RestartService" `
  --maxRestartAttempts=5 `
  --deps="MongoDB; MySQL80" `
  --user=".\serviceuser"

# 3. 使用后立即从内存中清除敏感变量
Remove-Item Env:SERVY_PASSWORD
Remove-Item Env:SERVY_PROCESS_PARAMETERS
Remove-Item Env:SERVY_ENVIRONMENT_VARIABLES
```

以下是 `install` 命令的用法示例：
```powershell
servy-cli install `
  --name="My NodeJS Service" `
  --description="My NodeJS Server" `
  --path="C:\Program Files\nodejs\node.exe" `
  --startupDir="C:\Apps\App" `
  --params="C:\Apps\App\index.js" `
  --startupType="Automatic" `
  --priority="Normal" `
  --stdout="C:\Apps\App\stdout.log" `
  --stderr="C:\Apps\App\stderr.log" `
  --enableSizeRotation `
  --rotationSize=10 `
  --enableHealth `
  --heartbeatInterval=10 `
  --maxFailedChecks=3 `
  --recoveryAction="RestartService" `
  --maxRestartAttempts=5 `
  --envVars="ENV_VAR1=VAL1; ENV_VAR2=VAL2;" `
  --deps="MongoDB; MySQL80"
```

以下是带启动前脚本的 `install` 命令用法示例：
```powershell
servy-cli install `
  --name="MyLegacyService" `
  --description="Runs legacy app with dynamic config" `
  --path="C:\Apps\LegacyApp\LegacyApp.exe" `
  --startupDir="C:\Apps\LegacyApp" `
  --params="--mode=production" `
  --preLaunchPath="C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" `
  --preLaunchStartupDir="C:\Scripts" `
  --preLaunchParams="-File C:\Scripts\GenerateConfig.ps1 -VaultUrl https://vault.example.com -SecretName AppSecrets" `
  --preLaunchEnv="ENV=production;API_KEY=abcdef123" `
  --preLaunchStdout="C:\Logs\prelaunch_stdout.log" `
  --preLaunchStderr="C:\Logs\prelaunch_stderr.log" `
  --preLaunchTimeout=60 `
  --preLaunchRetryAttempts=2 `
  --preLaunchIgnoreFailure `
  --enableHealth `
  --heartbeatInterval=30 `
  --maxFailedChecks=3 `
  --recoveryAction="RestartService" `
  --maxRestartAttempts=5 `
  --stdout="C:\Logs\service_stdout.log" `
  --stderr="C:\Logs\service_stderr.log" `
  --enableSizeRotation `
  --rotationSize=10
```

有关 CPU 亲和性的更多详情，见 [常见问题](./FAQ#how-and-why-should-i-use-cpu-affinity-with-servy)。

## 其他命令
* `uninstall`：按名称卸载现有服务。
* `start`：按名称启动 Windows 服务。
* `stop`：按名称停止 Windows 服务。
* `restart`：按名称重启 Windows 服务。
* `status`：按名称获取 Windows 服务状态。
* `export`：将 Servy Windows 服务配置导出到配置文件。
* `import`：将 Windows 服务配置导入 Servy 数据库，并可选择安装。
* `--version`：显示 CLI 版本（全局标志）。

## 提示
* 执行会修改 Windows 服务的命令时，始终以管理员权限运行 CLI。
* 对任意命令使用 `--help` 标志可显示详细用法与可用选项。
* 在脚本或 CI/CD 流水线中自动化时，依赖 CLI 的退出码：`0` 表示成功，其他值表示失败。
* 确保日志文件路径对服务运行所用的用户账户可写，以避免权限问题。

## 另见
* [PowerShell 模块](./Servy-PowerShell-Module)
* [Servy 自动化与 CI/CD](./Servy-Automation-&-CI-CD)
* [示例与配方](./Examples-&-Recipes)
* [导出/导入服务](./Export-Import-Services)
