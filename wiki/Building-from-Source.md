## 目录

1. [源代码](#源代码)
1. [构建说明](#构建说明)
   1. [要求](#要求)
   1. [步骤](#步骤)
   1. [运行应用程序](#运行应用程序)
   1. [运行测试](#运行测试)

## 源代码

Servy 提供两个版本，分别维护在不同分支：

* **.NET 10.0+ 版本：** 位于 [`main`](https://github.com/aelassas/servy/tree/main) 分支
* **.NET Framework 4.8 版本：** 位于 [`net48`](https://github.com/aelassas/servy/tree/net48) 分支

## 构建说明

### 要求
要在本地构建并运行任一版本，你需要：

* Visual Studio 2026 或更高版本
* 与目标版本匹配的 .NET SDK：
  * `main` 分支需要 [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)。所需的精确 SDK 版本在仓库根目录的 `global.json`（`sdk.version`）中指定。由于 `rollForward` 设置为 `disable`，必须从 [.NET 发布存档](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) 安装该精确 SDK 版本。
  * `net48` 分支需要 .NET Framework 4.8 Developer Pack（随 Visual Studio 提供）。（不受 `global.json` 约束）。

> [!NOTE]
> 需要以**管理员**身份运行 Visual Studio，才能管理 Windows 服务。

### 步骤
1. 克隆仓库：
    ```bash
    git clone https://github.com/aelassas/servy.git
    ```
2. 检出所需分支：
    ```bash
    # 适用于 .NET 10.0+ 版本
    git checkout main

    # 适用于 .NET Framework 4.8 版本
    git checkout net48
    ```
3. 在 Visual Studio 2026 中打开 `Servy.sln` 文件
4. 还原 NuGet 包
5. 构建解决方案

> [!IMPORTANT]
> 在 `main` 上，可执行项目（`Servy`、`Servy.CLI`、`Servy.Manager`、`Servy.Restarter`、`Servy.Service`）声明了 `<RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>`；共享库（`Servy.Core`、`Servy.Infrastructure`、`Servy.UI`）不固定 RID。发布时请选择所需的 RID（例如 `win-x64`）。
>
> 在 `net48` 上，**x64** 为必需，因为 `SourceGear.sqlite3` 不提供 **Any CPU** 构建。

### 运行应用程序
* 运行桌面应用：将启动项目设为 **Servy** 并运行
* 运行 Manager 应用：将启动项目设为 **Servy.Manager** 并运行
* 运行 CLI 应用：将启动项目设为 **Servy.CLI** 并运行

### 运行测试
在 `main` 上，测试运行器按 `global.json` 配置使用 Microsoft.Testing.Platform（MTP）。

* 运行完整测试套件（单元测试与集成测试）并生成代码覆盖率：
    ```powershell
    .\tests\test.ps1
    ```
* 通过标准 .NET CLI 运行单元测试：
    ```powershell
    dotnet test
    ```
* 或者，直接运行单个测试可执行文件：
    ```powershell
    dotnet run --project tests\Servy.Core.UnitTests\Servy.Core.UnitTests.csproj
    ```
