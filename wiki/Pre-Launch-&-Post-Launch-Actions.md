## 目录

1. [简介](#简介)
1. [启动钩子生命周期](#启动钩子生命周期)
1. [启动前（Pre-Launch）](#启动前pre-launch)
   1. [启动前设置](#启动前设置)
   1. [图形界面示例](#图形界面示例)
   1. [CLI 示例](#cli-示例)
   1. [PowerShell 示例](#powershell-示例)
1. [启动后（Post-Launch）](#启动后post-launch)
   1. [启动后设置](#启动后设置)
   1. [图形界面示例](#图形界面示例-1)
   1. [CLI 示例](#cli-示例-1)
   1. [PowerShell 示例](#powershell-示例-1)

## 简介

Servy 支持在主服务启动前运行可选脚本或可执行文件。可用于例如：

* 准备或生成配置文件
* 动态获取密钥或凭据
* 运行主进程启动前所需的任何设置或初始化

此外，Servy 还支持在进程成功启动后运行可选脚本或可执行文件。可用于例如：

* 初始化依赖服务或进程
* 运行数据库迁移或设置脚本
* 发送通知或记录启动事件
* 执行特定于环境的配置

> [!NOTE]
> 若启动前（或启动后）脚本是 PowerShell 脚本（`.ps1`）或任何非可执行文件，**必须**通过可执行文件（如 `powershell.exe` 或 `pwsh.exe`）调用。
>
> 例如：
>
> ```powershell
> --preLaunchPath="C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
> --preLaunchParams="-File C:\Scripts\GenerateConfig.ps1 -VaultUrl https://vault.example.com"
> ```
>
> 直接指定 `--preLaunchPath="C:\Scripts\GenerateConfig.ps1"` **将无法工作**。

## 启动钩子生命周期

| 钩子类型 | 模式 | 触发条件 | 失败影响 | 孤立进程清理 |
| :--- | :--- | :--- | :--- | :--- |
| **Pre-Launch** | **同步** | Timeout > 0 | **服务不会启动** | 是 |
| **Pre-Launch** | **Fire-and-Forget** | Timeout = 0 | 记录日志，服务启动 | 是 |
| **Post-Launch** | **Fire-and-Forget** | 默认 | 记录日志，服务保持运行 | 是 |

*关机相关钩子请参见 [停止前与停止后操作](./Pre-Stop-&-Post-Stop-Actions)。*

*Servy 会跟踪启动期间启动的进程，以确保主服务崩溃时，所有设置脚本也会被终止。*

## 启动前（Pre-Launch）
### 启动前设置

* **启动前可执行文件路径** - 启动前脚本或可执行文件的完整路径
* **启动前启动目录** - 启动前进程的工作目录（可选；默认为主服务的工作目录）
* **启动前参数** - 启动前可执行文件的命令行参数
* **启动前环境变量** - 启动前进程的可选环境变量（格式与主进程相同）
* **启动前 Stdout/Stderr 文件路径** - 用于捕获输出与错误的日志文件
* **启动前超时（秒）** - 每次尝试允许的最长时间，设为 **0** 以 fire-and-forget 模式运行（默认：30 秒）
* **启动前重试次数** - 失败时的重试次数（默认：0）
* **忽略失败** - 启用后，即使启动前脚本失败，服务仍继续启动

默认情况下，启动前钩子带超时同步运行。若启动前脚本以非零退出码退出或超时，除非启用了 **忽略失败** 选项，否则服务将无法启动。

将超时设为 0 会以 fire-and-forget 模式运行启动前钩子。设为 0 时，钩子启动后立即启动服务，不等待完成。仅用于不影响服务启动或正常运行能力的任务。fire-and-forget 模式下不可用 `stdout`/`stderr` 重定向与重试。

Fire-and-forget 启动前钩子作为启动序列的一部分执行，并在服务停止时纳入孤立进程清理跟踪。

### 图形界面示例

<img alt="servy-config-pre-launch" src="https://github.com/user-attachments/assets/74dc7d88-60cf-453d-b070-9bf270a42e77" />

### CLI 示例

```powershell
servy-cli install `
  --name="MyService" `
  --description="Runs app with dynamic config" `
  --path="C:\Apps\App\App.exe" `
  --startupDir="C:\Apps\App" `
  --params="--mode=production" `
  --preLaunchPath="C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" `
  --preLaunchStartupDir="C:\Scripts" `
  --preLaunchParams="-File C:\Scripts\GenerateConfig.ps1 -VaultUrl https://vault.example.com" `
  --preLaunchEnv="ENV=production;API_KEY=abcdef123" `
  --preLaunchStdout="C:\Logs\prelaunch_stdout.log" `
  --preLaunchStderr="C:\Logs\prelaunch_stderr.log" `
  --preLaunchTimeout="60" `
  --preLaunchRetryAttempts="2" `
  --preLaunchIgnoreFailure
```

### PowerShell 示例

```powershell
Import-Module "C:\Program Files\Servy\Servy.psm1" -Force

$installParams = @{
    Name                    = "MyService"
    Description             = "Runs app with dynamic config"
    Path                    = "C:\Apps\App\App.exe"
    StartupDir              = "C:\Apps\App"
    Params                  = "--mode=production"

    PreLaunchPath            = "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
    PreLaunchStartupDir      = "C:\Scripts"
    PreLaunchParams          = "-File C:\Scripts\GenerateConfig.ps1 -VaultUrl https://vault.example.com"
    PreLaunchEnv             = "ENV=production;API_KEY=abcdef123"
    PreLaunchStdout          = "C:\Logs\prelaunch_stdout.log"
    PreLaunchStderr          = "C:\Logs\prelaunch_stderr.log"
    PreLaunchTimeout         = 60
    PreLaunchRetryAttempts   = 2
    PreLaunchIgnoreFailure   = $true
}

Install-ServyService @installParams
```

## 启动后（Post-Launch）
### 启动后设置

* **启动后可执行文件路径** - 启动后脚本或可执行文件的完整路径
* **启动后启动目录** - 启动后进程的工作目录（可选；默认为主服务的工作目录）
* **启动后参数** - 启动后可执行文件的命令行参数

**环境变量：** 此钩子继承**主服务**的 `EnvironmentVariables`。没有单独的 `Post-Launch / Pre-Stop / Post-Stop / Failure-Program EnvironmentVariables` 设置——仅 `Pre-Launch` 支持按钩子覆盖。

启动后脚本在服务进程完全启动后以 fire-and-forget 模式执行。

启动后钩子在服务启动后运行，并在服务停止时纳入孤立进程清理跟踪。

### 图形界面示例

<img alt="servy-config-post-launch" src="https://github.com/user-attachments/assets/8877dca7-c89f-4285-b5f4-b6ccab17e538" />

### CLI 示例

```powershell
servy-cli install `
  --name="MyService" `
  --description="Runs app with dynamic config" `
  --path="C:\Apps\App\App.exe" `
  --startupDir="C:\Apps\App" `
  --params="--mode=production" `
  --postLaunchPath="C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" `
  --postLaunchStartupDir="C:\Scripts" `
  --postLaunchParams="-File C:\Scripts\Notify.ps1 -VaultUrl https://vault.example.com"
```

### PowerShell 示例

```powershell
Import-Module "C:\Program Files\Servy\Servy.psm1" -Force

$installParams = @{
    Name                   = "MyService"
    Description            = "Runs app with dynamic config"
    Path                   = "C:\Apps\App\App.exe"
    StartupDir             = "C:\Apps\App"
    Params                 = "--mode=production"

    PostLaunchPath         = "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
    PostLaunchStartupDir   = "C:\Scripts"
    PostLaunchParams       = "-File C:\Scripts\Notify.ps1 -VaultUrl https://vault.example.com"
}

Install-ServyService @installParams
```
