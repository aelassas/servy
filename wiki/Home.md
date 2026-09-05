# Servy

Servy 是一款 Windows 工具，可将任意可执行文件运行为 Windows 服务，并通过清晰的图形界面、CLI 或 PowerShell，全面掌控配置、监控、管理和恢复。

现代版本支持 **Windows 10 (1809+)**、**Windows 11** 和 **Windows Server 2016+**。Windows 7 SP1、Windows Server 2008 R2 等旧版系统也可通过专用的 **.NET Framework 4.8 版本** 获得支持。请参阅[安装指南](./Installation-Guide#version-comparison)，按操作系统选择合适版本。

> [!TIP]
> 使用侧边栏浏览安装指南、配置选项等内容。

## 主要优势

* **将任意可执行文件运行为 Windows 服务：** 适用于 Node.js、Python、.NET、Java、Go、Rust、PHP 和 Ruby 应用。
* **在一处管理服务：** 通过桌面应用、CLI 或 PowerShell 进行监控与控制。
* **控制启动行为：** 可设置工作目录、依赖项、环境变量和启动参数。
* **自动恢复：** 通过心跳检查、重启策略和服务健康监控保持应用运行。
* **添加生命周期钩子：** 需要时可运行启动前、启动后、停止前和停止后任务。
* **跟踪运行时健康与日志：** 查看 CPU/内存占用、实时控制台输出和轮转日志文件。
* **灵活的服务身份：** 可在 Local System、本地/域账户、Active Directory 账户或 gMSA 下运行。
* **支持现代与旧版 Windows：** 现代版本原生支持 x64 与 ARM64，.NET Framework 4.8 版本支持 x64。
