## 目录

1. [概览](#概览)
   1. [服务](#服务)
   1. [性能](#性能)
   1. [控制台](#控制台)
   1. [依赖](#依赖)
   1. [日志](#日志)
1. [功能](#功能)
1. [可用性](#可用性)
1. [使用方法](#使用方法)
1. [键盘快捷键](#键盘快捷键)
1. [另请参阅](#另请参阅)

> [!NOTE]
> 若在远程管理工具（如 MeshCentral、TeamViewer 或 AnyDesk）中出现空白屏幕，请从管理员命令提示符使用以下命令运行 Manager 应用：`Servy.Manager.exe --force-sr`

## 概览

Servy Manager 是用于管理和监控由 Servy 控制的服务的集中管理应用。它提供进程健康、CPU 与内存利用率、实时控制台输出流、依赖树以及系统日志文件的实时可见性；全部直接与 Servy 的本地数据库和运行时状态交互。

### 服务

Manager 提供集中位置，用于查看所有已安装服务、其实时状态、CPU 与内存占用，以及快捷操作（启动、停止、重启、安装、卸载、移除、编辑、复制 PID）。

<img alt="servy-manager-services" src="https://github.com/user-attachments/assets/bed472ea-35b5-4b78-9f19-3377f2a73342" />

### 性能

在 Performance 选项卡中通过实时图表监控 CPU 与内存占用。

<img alt="servy-manager-performance" src="https://github.com/user-attachments/assets/82ad2034-eb74-49fd-aabd-f68e9bac7394" />

### 控制台

在统一控制台中实时监控服务的 `stdout` 与 `stderr` 输出。

Console 视图会自动加载近期日志历史、继续尾随实时输出，并按时间戳排序两个流，便于准确排查问题。

你可以即时过滤日志，在选择或复制文本时暂停更新，并在不丢失上下文的情况下恢复实时流。

<img alt="servy-manager-console" src="https://github.com/user-attachments/assets/8734acde-b59c-478c-b4af-797ea99d5884" />

### 依赖

Dependencies 选项卡提供从 Service Control Manager（SCM）检索的服务依赖树的可视化表示。每个依赖会显示当前状态：运行中为绿色，已停止为红色，循环为橙色。可随时使用 Refresh 按钮或按 **F5** 刷新树。

此视图特别有助于理解启动与关闭顺序、诊断服务无法启动的原因，以及快速识别可能影响服务可用性的已停止或缺失依赖。

<img alt="servy-manager-dependencies" src="https://github.com/user-attachments/assets/9d69b45c-4059-4dd1-86f8-a9a5f9a35427" />

### 日志

Servy 会将日志写入 Windows 事件日志以及 `%ProgramData%\Servy\logs` 中的日志文件，内置日志查看器可让你直接在 GUI 中实时检查这些日志。

<img alt="servy-manager-logs" src="https://github.com/user-attachments/assets/53eba82a-a879-4aa6-8af8-68928fa5aa5a" />

## 功能

- **服务列表：** 查看在 Servy 中安装或导入的所有服务。
- **服务控制：** 启动、停止和重启服务。
- **服务安装：** 从配置文件安装服务，卸载/移除服务。
- **导出：** 将服务配置保存为 XML 或 JSON。
- **导入：** 将配置导入 Servy 数据库而不立即安装——稍后可从 UI 安装。
- **配置编辑器：** 打开并编辑服务配置。
- **搜索：** 快速查找特定服务。
- **监控：** 跟踪服务的实时 CPU 与内存占用。
- **实时日志尾随：** 实时流式输出标准输出（`stdout`）与标准错误（`stderr`）日志。
- **依赖树：** 预览并检查服务依赖。
- **日志：** 按日志级别、日期和关键字快速搜索日志。

## 可用性

Servy Manager 提供多个版本，覆盖现代与较旧系统：

- **.NET 10.0+（推荐）**
  - 自包含安装程序
  - 无需预先安装任何 .NET 运行时

- **.NET Framework 4.8（适用于较旧系统）**
  - 标准安装包
  - 需要 .NET Framework 4.8 Runtime

## 使用方法

1. 启动 Servy Manager（`Servy.Manager.exe`）。
1. 浏览已安装或已导入的服务列表。
1. 使用上下文菜单：
   - 启动/停止/重启服务
   - 打开/编辑配置
   - 将配置导出为 XML/JSON
   - 将配置导入数据库
   - 复制 PID
1. 要安装新服务，先导入其配置，再通过 UI 安装；或使用 Servy 桌面应用进行交互式设置。
1. 选择服务以检查其详细管理选项卡：
   - **Performance：** 查看实时 CPU 与内存占用图表。
   - **Console：** 实时流式输出 `stdout` 与 `stderr` 日志。
   - **Dependencies：** 检查服务依赖树与关系。
   - **Logs：** 搜索并过滤 Servy 事件日志条目与诊断历史。

## 键盘快捷键

| 快捷键 | 位置 | 作用 |
| --- | --- | --- |
| `F5` | Services | 刷新完整服务列表。状态也会自动刷新。 |
| `F5` | Logs | 重新执行当前日志搜索。 |
| `F5` | Dependencies | 刷新依赖树。 |
| `Ctrl` + `A` | Services | 选中列表中的每个服务。即使搜索框有焦点也有效，此时会替代通常的全选文本行为。 |

`F5` 对 Console 选项卡无效，该选项卡会持续流式输出。

## 另请参阅

- [概览](./Overview)
- [使用说明](./Usage)
- [Servy 桌面应用](./Servy-Desktop-App)
- [Servy CLI](./Servy-CLI)
- [Servy PowerShell 模块](./Servy-PowerShell-Module)
