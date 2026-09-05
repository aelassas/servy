## 目录

1. [简介](#简介)
1. [环境变量展开](#环境变量展开)
1. [图形界面](#图形界面)
1. [CLI 选项：`--envVars`](#cli-选项---envvars)
1. [CLI 示例](#cli-示例)
1. [PowerShell 选项：`-EnvVars`](#powershell-选项--envvars)
1. [PowerShell 示例](#powershell-示例)
1. [提示](#提示)

## 简介

Servy 允许为服务进程定义**环境变量**，从而对运行时上下文进行细粒度控制。

**环境变量展开**在 Servy 配置的多个区域受支持：

- 服务**二进制路径**（`--path` / `-Path`）
- **启动目录**（`--startupDir` / `-StartupDir`）
- **进程参数**（`--params` / `-Params`）
- **环境变量**（`--envVars` / `-EnvVars`）

这使得可以在配置中动态引用现有系统变量、用户定义变量或路径。

> [!NOTE]
> 除 `Pre-Launch` 钩子外，所有钩子继承**主服务**的 `EnvironmentVariables`。没有单独的 `Post-Launch / Pre-Stop / Post-Stop / Failure-Program EnvironmentVariables` 设置——仅 `Pre-Launch` 支持按钩子覆盖。

## 环境变量展开

Servy 使用可让变量相互引用的展开引擎。解析顺序如下：

1. **系统环境：** 首先加载当前所有系统与进程环境变量。
2. **自定义覆盖：** 接着添加在**环境变量**字段（或 `--envVars`）中定义的变量。若自定义变量与系统变量同名，**自定义值优先**。
3. **交叉引用展开：** 最后扫描所有变量中的 `%VAR%` 占位符。这些占位符使用最终合并后的变量集解析。

### 受保护变量

出于安全原因，Servy 拒绝让 `--envVars` / `-EnvVars` 覆盖一份硬编码变量列表；这些变量若被覆盖可能导致权限提升或运行时注入（DLL/JIT 分析器劫持、调试器探测、搜索路径攻击等）。任何覆盖尝试会在**每次服务启动时**（环境变量展开期间）被静默忽略，并在 `%ProgramData%\Servy\logs\Servy.Service.log` 中记录警告：

```text
Security: Blocked an attempt to override protected variable 'PATH'. Custom values for this variable are ignored to prevent privilege escalation.
```

当前列表包括：

| 类别 | 环境变量 | 安全 / 完整性用途 |
| --- | --- | --- |
| **系统完整性** | `PATH`, `COMSPEC`, `SYSTEMROOT`, `WINDIR`, `SYSTEMDRIVE`, `TEMP`, `TMP`, `PATHEXT`, `PROGRAMFILES`, `PROGRAMFILES(X86)`, `PROGRAMW6432`, `COMMONPROGRAMFILES`, `COMMONPROGRAMFILES(X86)`, `COMMONPROGRAMW6432` | 保护核心操作系统路径、shell 执行二进制、可执行扩展解析规则，以及系统级临时布局存储区域，防止被重定向或篡改。 |
| **配置文件与身份重定向** | `APPDATA`, `LOCALAPPDATA`, `PUBLIC`, `HOMEDRIVE`, `HOMEPATH`, `HOME`, `USERDOMAIN`, `USERDOMAIN_ROAMINGPROFILE`, `LOGONSERVER` | 防止恶意重定向活动用户配置位置、本地缓存仓库、主路径范围或域网络登录，以免导致凭据收割或状态投毒。 |
| **用户与配置完整性** | `USERNAME`, `USERPROFILE`, `ALLUSERSPROFILE`, `PROGRAMDATA`, `PSMODULEPATH` | 保护系统配置与初始化边界、跟踪上下文，以及默认 PowerShell 实用模块路径发现范围，免受任意干扰。 |
| **.NET 运行时注入与诊断** | `COR_ENABLE_PROFILING`, `COR_PROFILER`, `COR_PROFILER_PATH`, `CORECLR_ENABLE_PROFILING`, `CORECLR_PROFILER`, `CORECLR_PROFILER_PATH`, `DOTNET_STARTUP_HOOKS`, `DOTNET_ROOT`, `DOTNET_ROOT(x86)`, `DOTNET_HOST_PATH`, `DOTNET_BUNDLE_EXTRACT_BASE_DIR`, `DOTNET_ADDITIONAL_DEPS`, `DOTNET_SHARED_STORE`, `DOTNET_DiagnosticPorts`, `COMPlus_DiagnosticPorts`, `DOTNET_EnableDiagnostics`, `COMPlus_EnableDiagnostics`, `DOTNET_EnableDiagnostics_IPC`, `COMPlus_EnableDiagnostics_IPC`, `DOTNET_EnableDiagnostics_Profiler`, `COMPlus_EnableDiagnostics_Profiler`, `DOTNET_EnableEventPipe`, `COMPlus_EnableEventPipe`, `DOTNET_GCName`, `COMPlus_GCName`, `DOTNET_GCPath`, `COMPlus_GCPath`, `DOTNET_LegacyHostPolicy`, `COMPlus_LegacyHostPolicy`, `DOTNET_LegacyTransform`, `COMPlus_LegacyTransform`, `DOTNET_PerfMapEnabled`, `COMPlus_PerfMapEnabled`, `DOTNET_ZapDisable`, `COMPlus_ZapDisable`, `DOTNET_DbgEnableMiniDump`, `COMPlus_DbgEnableMiniDump`, `DOTNET_DbgMiniDumpName`, `COMPlus_DbgMiniDumpName`, `DOTNET_DbgMiniDumpType`, `COMPlus_DbgMiniDumpType` | 切断针对旧版、现代与交叉编译 `.NET`/`CoreCLR` 运行时的执行劫持与进程附加路径。阻止未经授权的远程诊断套接字端点暴露（`DiagnosticPorts`）、运行时分析器/程序集 DLL 注入、自定义垃圾回收器（`GCPath`）劫持，以及将敏感进程内存转储（`DbgMiniDumpName`）重定向到不安全磁盘区域。 |
| **Java 注入** | `JAVA_TOOL_OPTIONS`, `_JAVA_OPTIONS`, `JDK_JAVA_OPTIONS`, `JAVA_OPTS`, `JAVA_OPTIONS`, `CATALINA_OPTS`, `CATALINA_JAVA_OPTS`, `MAVEN_OPTS`, `M2_OPTS`, `GRADLE_OPTS`, `ANT_OPTS`, `JBOSS_JAVA_OPTS`, `WILDFLY_OPTS`, `CLASSPATH`, `JAVA_HOME`, `JRE_HOME`, `JDK_HOME` | 阻止通过原生 JVM 诊断（`java.exe` 启动器标志）或企业应用框架（如 Tomcat、JBoss、Maven、Gradle）中常见的 shell 包装脚本加载的执行利用（例如恶意 `-javaagent` 参数）。 |
| **Node.js 与 NPM 注入** | `NODE_OPTIONS`, `NODE_PATH`, `NODE_EXTRA_CA_CERTS`, `NPM_CONFIG_PREFIX`, `NPM_CONFIG_USERCONFIG`, `NPM_CONFIG_GLOBALCONFIG` | 防御 Node.js 运行时实例遭受预加载选项注入、本地依赖解析调整、恶意证书颁发机构添加（防止中间人流量解密），以及 npm 注册表配置篡改。 |
| **TLS 信任存储与 OpenSSL 配置** | `OPENSSL_CONF`, `OPENSSL_MODULES`, `SSL_CERT_FILE`, `SSL_CERT_DIR`, `REQUESTS_CA_BUNDLE`, `CURL_CA_BUNDLE` | 防止通过 OpenSSL 动态引擎/提供程序模块加载（`OPENSSL_CONF`、`OPENSSL_MODULES`）实现任意代码执行，并通过阻止恶意覆盖或替换 OpenSSL、Python `requests` 与 `curl` 消费者的受信任 CA 证书包，阻止静默中间人（MITM）流量解密。 |
| **Python 注入** | `PYTHONSTARTUP`, `PYTHONPATH`, `PYTHONHOME`, `PYTHONUSERBASE`, `PYTHONEXECUTABLE` | 防止通过专用系统初始化钩子、站点库布局目录或自定义解释器路径重定向劫持任意脚本解析或站点包源位置。 |
| **Ruby 与 Perl 注入** | `RUBYOPT`, `RUBYLIB`, `PERL5OPT`, `PERL5LIB`, `PERLLIB` | 禁止在本地 Ruby 与 Perl 执行环境中进行选项映射开关与内部代码包含路径操纵。 |
| **PHP 注入** | `PHPRC`, `PHP_INI_SCAN_DIR` | 阻止攻击者通过修改初始化文件搜索路径提供自定义配置指令，或动态扫描任意目录中的恶意扩展模块。 |
| **全局 / Unix 回退** | `LD_PRELOAD`, `LD_LIBRARY_PATH`, `LD_AUDIT` | 控制 Unix 兼容框架、MinGW 子系统、WSL 边界或 Cygwin 沙箱中的动态跟踪或链接编辑器行为，以阻止任意二进制插桩。 |
| **Windows AppCompat** | `__COMPAT_LAYER`, `SHIM_FILE_LOG`, `SHIM_DEBUG_LEVEL`, `_NT_SYMBOL_PATH`, `_NT_ALT_SYMBOL_PATH`, `_NT_SOURCE_PATH`, `MICROSOFT_TELEMETRY_ENV_OVERRIDE` | 加固执行树，抵御调试诊断向量、系统符号仓库重定向，以及应用程序兼容性 shim 注入层技术。 |
| **PowerShell 加固绕过** | `__PSLockDownPolicy`, `PSExecutionPolicyPreference` | 通过阻止针对 Windows 执行限制或系统 LanguageMode 的程序性覆盖，缓解进程初始化时未经授权的管理策略更改。 |

若需为服务进程扩展 `PATH`，请修改*系统* `PATH`，或使用自定义变量名并在应用程序配置中引用。

> [!NOTE]
> 对于 Python 可执行文件，当 `PYTHONLEGACYWINDOWSSTDIO`、`PYTHONIOENCODING`、`PYTHONUTF8` 与 `PYTHONUNBUFFERED` 尚未定义时，Servy 会设置它们；参见 `ApplyLanguageFixes`。

### 展开逻辑示例
若系统有 `TEMP=C:\Windows\Temp`，且你定义：

- `MY_ROOT=C:\ServyApp`
- `MY_LOGS=%MY_ROOT%\logs`
- `APP_TEMP=%TEMP%`

进程最终看到的环境将是：

- `MY_ROOT`：`C:\ServyApp`
- `MY_LOGS`：`C:\ServyApp\logs`
- `APP_TEMP`：`C:\Windows\Temp`

> [!NOTE]
> **变量顺序与循环引用：**
> 变量使用多遍不动点算法解析，因此**定义顺序无关紧要**——即使 `MY_ROOT` 在列表中稍后定义，`MY_LOGS=%MY_ROOT%\logs` 也能正确解析。展开引擎最多运行 5 遍；更深的链会留下未解析的 `%VAR%` 占位符，Servy 会记录警告（"Environment variable expansion reached maximum pass limit"）。
>
> **避免循环引用：**
> - **自引用**（`A=%A%`）：展开引擎一旦遇到包含自身 `%NAME%` 标记的值即捕获。记录为 `Direct cycle detected for variable 'A'; leaving literal placeholder.`，占位符保持字面量（除非操作系统已为 `A` 导出值，此时会代入继承值以模拟 Windows `PATH` 追加语义）。
> - **多变量循环**（`A=%B%`、`B=%A%`，或更长链如 `A=%B%`、`B=%C%`、`C=%A%`）：没有专门检测。不动点循环允许最多 5 遍（`AppConfig.MaxEnvVarExpansionPasses`）；若达到限制时值仍在变化，循环退出，Servy 记录 `Environment variable expansion reached maximum pass limit. Indirect circular reference detected (e.g., A=%B%, B=%A%).`。双变量循环通常在循环内退化为自引用，因此也会在后续遍次中显示为 `Direct cycle detected`。

## 图形界面

Servy 的高级选项卡可为服务进程设置环境变量：

<img alt="servy-config-advanced" src="https://github.com/user-attachments/assets/75aaf4e2-8b6d-4d9f-a3e3-5534dd466961" />

## CLI 选项：`--envVars`

`--envVars` 命令行选项可为服务进程指定环境变量。
- **语法：** `--envVars="VAR1=value1; VAR2=value2"`
- 多个变量用**分号（;）**分隔 - 特殊字符可转义：
  - `\=` 转义 `=`
  - `\"` 转义 `"`
  - `\;` 转义 `;`
  - `\\` 转义 `\`
  - `%%` 转义 `%`（折叠为单个 `%`，与 `cmd.exe` 行为一致）（自 v8.5+ 起）
- 支持环境变量展开。示例：
  `--envVars="VAR1=%ProgramData%\MyApp; VAR2=%VAR1%\bin; CHANCE=100%%"`
- 适用于在不更改系统级环境变量的情况下设置运行时上下文。

## CLI 示例

```powershell
servy-cli install `
  --name="MyNodeService" `
  --description="My NodeJS Server" `
  --path="%ProgramFiles%\nodejs\node.exe" `
  --startupDir="C:\Apps\App" `
  --params="C:\Apps\App\index.js" `
  --startupType="Automatic" `
  --envVars="NODE_ENV=production; APP_CONFIG=C:\Apps\App\config.json"
```

## PowerShell 选项：`-EnvVars`

`-EnvVars` 参数可在通过 PowerShell 安装服务时定义环境变量。

- **类型：** `string`（分号分隔的列表，可选）
- 转义规则与 CLI 相同。
- 多个变量用**分号（;）**分隔
- 变量**仅应用于服务进程**，而非系统范围。

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

    EnvVars      = "NODE_ENV=production; APP_CONFIG=C:\Apps\App\config.json"
}

Install-ServyService @installParams
```

## 提示

- **大小写不敏感：** 环境变量名不区分大小写。定义 `node_env` 会正确覆盖现有的 `NODE_ENV`。
- **展开顺序：** 可同时引用现有系统变量（如 `%ProgramData%`）以及同一列表中定义的其他自定义变量。
- **安全的百分号转义：** 要安全地向环境传入字面百分号字符并防止其被当作展开块处理，请使用双百分号（`%%`）。例如，定义 `ALERT_MSG=Battery at 100%%` 会在服务进程内正确展开为 `Battery at 100%`。
- **验证：** 若不确定变量是否正确应用，可运行以下命令将环境转储到文件以排查（PowerShell 管理员）：
```powershell
servy-cli install --name="EnvTest" --startupType="Manual" --path="C:\Windows\System32\cmd.exe" --params="/c set > C:\servy_env.txt && timeout /t 3600 /nobreak > nul" --envVars="MY_ROOT=C:\ServyApp; MY_LOGS=%MY_ROOT%\logs"
servy-cli restart --name="EnvTest"
Start-Sleep -Seconds 3
Get-Content C:\servy_env.txt | Select-String "MY_LOGS"

# 完成后清理
servy-cli uninstall --name="EnvTest"
```
