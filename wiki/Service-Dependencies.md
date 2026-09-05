## 目录

1. [简介](#简介)
1. [图形界面](#图形界面)
1. [CLI 选项：`--deps`](#cli-选项---deps)
1. [CLI 示例](#cli-示例)
1. [PowerShell 选项：`-Deps`](#powershell-选项--deps)
1. [PowerShell 示例](#powershell-示例)
1. [依赖树可视化](#依赖树可视化)

## 简介

Servy 支持可选的服务依赖，使服务仅在一个或多个其他 Windows 服务运行后才启动。

按服务名（不是显示名称）逐行输入本服务所依赖的每个服务，或用分号（`;`）分隔。

这样可确保服务按正确顺序启动，并避免在所需服务尚未运行时出错。

## 图形界面

高级选项卡提供服务依赖等额外配置选项。

<img alt="servy-config-advanced" src="https://github.com/user-attachments/assets/75aaf4e2-8b6d-4d9f-a3e3-5534dd466961" />

## CLI 选项：`--deps`

`--deps` 命令行选项可在通过 CLI 安装服务时指定一个或多个 Windows 服务依赖。
* **语法：** `--deps="Service1; Service2; Service3"`
* Windows 会先启动所列服务（按需启动自动启动类型的依赖）；若任一依赖启动失败或已禁用，本服务将不会启动。
* 多个依赖可用**分号**分隔。
* 尤其适用于依赖数据库、消息代理或其他后台服务的应用。

## CLI 示例
```powershell
servy-cli install `
  --name="MyNodeService" `
  --description="My NodeJS Server" `
  --path="C:\Program Files\nodejs\node.exe" `
  --startupDir="C:\Apps\App" `
  --params="C:\Apps\App\index.js" `
  --startupType="Automatic" `
  --deps="MongoDB; MySQL80"
```

## PowerShell 选项：`-Deps`

`-Deps` 参数可在通过 PowerShell 安装服务时指定一个或多个 Windows 服务依赖。
* **类型：** `string`（分号分隔的列表，可选）
* Windows 会先启动所列服务（按需启动自动启动类型的依赖）；若任一依赖启动失败或已禁用，本服务将不会启动。
* 多个依赖可用**分号**分隔。
* 适用于依赖数据库、消息代理或其他后台服务的服务。

## PowerShell 示例

```powershell
Import-Module "C:\Program Files\Servy\Servy.psm1" -Force

$installParams = @{
    Name         = "MyNodeService"
    Description  = "My NodeJS Server"
    Path         = "C:\Program Files\nodejs\node.exe"
    StartupDir   = "C:\Apps\App"
    Params       = "C:\Apps\App\index.js"
    StartupType  = "Automatic"

    Deps         = "MongoDB; MySQL80"
}

Install-ServyService @installParams
```

## 依赖树可视化

Servy Manager 中的 Dependencies 选项卡提供从服务控制管理器（SCM）检索的服务依赖树可视化。每个依赖会显示当前状态：运行中的服务为绿色，已停止的为红色，循环依赖为橙色。可随时使用 Refresh 按钮或按 **F5** 刷新该树。

此视图特别有助于理解启动与关闭顺序、诊断服务无法启动的原因，以及快速识别可能影响服务可用性的已停止或缺失依赖。

<img alt="servy-manager-dependencies" src="https://github.com/user-attachments/assets/9d69b45c-4059-4dd1-86f8-a9a5f9a35427" />

> [!TIP]
> 可将服务依赖与启动前脚本和健康检查结合使用，以确保服务在复杂环境中可靠启动。
