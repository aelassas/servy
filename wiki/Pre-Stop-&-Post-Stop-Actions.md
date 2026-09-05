## 目录

1. [简介](#简介)
1. [关机钩子生命周期](#关机钩子生命周期)
1. [停止前（Pre-Stop）](#停止前pre-stop)
   1. [停止前设置](#停止前设置)
   1. [图形界面示例](#图形界面示例)
   1. [CLI 示例](#cli-示例)
   1. [PowerShell 示例](#powershell-示例)
1. [停止后（Post-Stop）](#停止后post-stop)
   1. [停止后设置](#停止后设置)
   1. [图形界面示例](#图形界面示例-1)
   1. [CLI 示例](#cli-示例-1)
   1. [PowerShell 示例](#powershell-示例-1)

## 简介

Servy 支持在主服务停止前运行可选脚本或可执行文件。可用于例如：

* 优雅关闭外部依赖
* 刷写日志或指标
* 在关机前通知外部系统

Servy 还支持在服务进程停止后运行可选脚本或可执行文件。可用于例如：

* 清理操作
* 通知或告警
* 关机后自动化

> [!NOTE]
> 若停止前（或停止后）脚本是 PowerShell 脚本（`.ps1`）或任何非可执行文件，**必须**通过可执行文件（如 `powershell.exe` 或 `pwsh.exe`）调用。
>
> 例如：
>
> ```powershell
> --preStopPath="C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
> --preStopParams="-File C:\Scripts\PreStop.ps1 -VaultUrl https://vault.example.com"
> ```
>
> 直接指定 `--preStopPath="C:\Scripts\PreStop.ps1"` **将无法工作**。

## 关机钩子生命周期

| 钩子类型 | 模式 | 触发条件 | 失败影响 | 孤立进程清理 |
| :--- | :--- | :--- | :--- | :--- |
| **Pre-Stop** | **同步** | Timeout > 0 | 记录日志，服务停止 | 否 |
| **Pre-Stop** | **Fire-and-Forget** | Timeout = 0 | 记录日志，服务停止 | 否 |
| **Post-Stop** | **Fire-and-Forget** | 默认 | 记录日志 | 否 |

*启动相关钩子请参见 [启动前与启动后操作](./Pre-Launch-&-Post-Launch-Actions)。*

*Servy 会跟踪启动期间启动的进程，以确保主服务崩溃时，所有设置脚本也会被终止。对于 Stop 钩子，清理已禁用，因为这些进程旨在在服务环境本身被拆解时完成其工作。*

## 停止前（Pre-Stop）

### 停止前设置

* **停止前可执行文件路径**：停止前脚本或可执行文件的完整路径
* **停止前启动目录**：停止前进程的工作目录（可选；默认为主服务的工作目录）
* **停止前参数**：停止前可执行文件的命令行参数
* **停止前超时（秒）**：允许的最大执行时间。默认为 5 秒。设为 0 以 fire-and-forget 模式运行
* **将停止前失败记录为错误**：启用后，停止前失败将作为错误记录

**环境变量：** 此钩子继承**主服务**的 `EnvironmentVariables`。没有单独的 `Post-Launch / Pre-Stop / Post-Stop / Failure-Program EnvironmentVariables` 设置——仅 `Pre-Launch` 支持按钩子覆盖。

停止前脚本默认同步执行。Servy 会等待脚本完成后再继续服务关闭流程。

若脚本失败或超时且启用了 **将停止前失败记录为错误**，失败会作为错误记录，服务继续停止。

将超时设为 0 会以 fire-and-forget 模式运行停止前钩子。此模式下钩子启动后，服务关闭立即继续，不等待完成。仅应用于非关键任务。

Fire-and-forget 停止前钩子作为停止序列的一部分执行，不纳入孤立进程清理跟踪。

### 图形界面示例

<img alt="servy-config-pre-stop" src="https://github.com/user-attachments/assets/8b69bf5b-1e74-4d5d-b64f-55635e3cb798" />

### CLI 示例

```powershell
servy-cli install `
  --name="MyService" `
  --description="Runs app with dynamic config" `
  --path="C:\Apps\App\App.exe" `
  --startupDir="C:\Apps\App" `
  --params="--mode=production" `
  --preStopPath="C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" `
  --preStopStartupDir="C:\Scripts" `
  --preStopParams="-File C:\Scripts\PreStop.ps1 -VaultUrl https://vault.example.com -SecretName AppSecrets" `
  --preStopTimeout="60" `
  --preStopLogAsError
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

    PreStopPath            = "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
    PreStopStartupDir      = "C:\Scripts"
    PreStopParams          = "-File C:\Scripts\PreStop.ps1 -VaultUrl https://vault.example.com -SecretName AppSecrets"
    PreStopTimeout         = 60
    PreStopLogAsError      = $true
}

Install-ServyService @installParams
```

## 停止后（Post-Stop）

### 停止后设置

* **停止后可执行文件路径**：停止后脚本或可执行文件的完整路径
* **停止后启动目录**：停止后进程的工作目录（可选；默认为主服务的工作目录）
* **停止后参数**：停止后可执行文件的命令行参数

**环境变量：** 此钩子继承**主服务**的 `EnvironmentVariables`。没有单独的 `Post-Launch / Pre-Stop / Post-Stop / Failure-Program EnvironmentVariables` 设置——仅 `Pre-Launch` 支持按钩子覆盖。

停止后脚本在服务进程完全停止后以 fire-and-forget 模式执行。

停止后钩子在服务停止后运行，不纳入孤立进程清理跟踪。

### 图形界面示例

<img alt="servy-config-post-stop" src="https://github.com/user-attachments/assets/43eb220c-f38a-41f7-ba36-cba9661a064c" />

### CLI 示例

```powershell
servy-cli install `
  --name="MyService" `
  --description="Runs app with dynamic config" `
  --path="C:\Apps\App\App.exe" `
  --startupDir="C:\Apps\App" `
  --params="--mode=production" `
  --postStopPath="C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" `
  --postStopStartupDir="C:\Scripts" `
  --postStopParams="-File C:\Scripts\Notify.ps1 -VaultUrl https://vault.example.com -SecretName AppSecrets"
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

    PostStopPath            = "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
    PostStopStartupDir      = "C:\Scripts"
    PostStopParams          = "-File C:\Scripts\Notify.ps1 -VaultUrl https://vault.example.com -SecretName AppSecrets"
}

Install-ServyService @installParams
```
