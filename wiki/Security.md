## 目录

1. [安全概述](#security-overview)
   1. [双重锁定系统](#the-double-lock-system)
1. [Servy 如何保护您的数据](#how-servy-protects-your-data)
   1. [自动目录加固（ACL）](#1-automatic-directory-hardening-acls)
   1. [机器唯一加密（动态熵）](#2-machine-unique-encryption-dynamic-entropy)
   1. [密码学密钥派生（HKDF）](#3-cryptographic-key-derivation-hkdf)
   1. [认证加密（v6.5+）](#4-authenticated-encryption-v65)
   1. [内存防御（内存清零）](#5-in-memory-defense-memory-zeroing)
   1. [敏感命令行参数与服务账户凭据](#6-sensitive-command-line-arguments--service-account-credentials)
1. [渗透防护：本地导入强制](#infiltration-guard-local-import-enforcement)
   1. [缓解：纵深防御流水线](#mitigation-defense-in-depth-pipeline)
1. [供应链与信任](#supply-chain-and-trust)
1. [Servy 信任边界](#the-servy-trust-boundary)
   1. [架构设计与运行时权限](#architectural-design--runtime-permissions)
   1. [可执行文件权限加固（强制）](#executable-permission-hardening-mandatory)
      1. [脚本可用性](#script-availability)
      1. [加固工具用法](#hardening-utility-usage)
   1. [这对您的部署意味着什么](#what-this-means-for-your-deployment)
   1. [自动权限表（v7.9+）](#automatic-permissions-table-v79)
1. [文件位置与恢复](#file-locations-and-recovery)
1. [关键警告：机器迁移](#critical-warning-machine-migration)
1. [最佳实践](#best-practices)
1. [故障排查](#troubleshooting)

## Security Overview

Servy 充当 Windows 服务配置的安全保险库。Servy 使用业界标准的 AES-256 加密敏感数据，包括密码、环境变量以及全部执行参数（`Parameters`、`Password`、`EnvironmentVariables`、`FailureProgramParameters`、`PreLaunchParameters`、`PreLaunchEnvironmentVariables`、`PostLaunchParameters`、`PreStopParameters` 与 `PostStopParameters`）。即便数据库被攻破，您的真实机密仍是不可读的文本。

### The Double-Lock System

自版本 7.9 起，Servy 采用双层防御策略保护您的机密：

  * **上锁的房间（ACL）：** Servy 自动限制谁可以查看硬盘上的 Servy 文件夹。
  * **机器唯一密钥（动态熵）：** Servy 将加密密钥与您特定计算机的唯一机器身份绑定。若文件被移到另一台 PC，在没有源系统注册表中存储的机器特定熵的情况下无法解密。

您可以通过检查数据目录的访问控制列表（ACL）来验证“上锁的房间”是否生效。在提升权限的 PowerShell 窗口中运行以下命令：

```powershell
(Get-Acl "$env:ProgramData\Servy").Access | Select-Object IdentityReference, IsInherited, AccessControlType, FileSystemRights
```

**输出中应关注：**

* **IdentityReference：** 通常应看到三个主体：`NT AUTHORITY\SYSTEM`、`BUILTIN\Administrators`，以及安装 Servy 的特定**用户账户**（或手动添加的自定义服务运行账户）。
* *Note:* 安装程序与 `SecurityHelper` 类会为当前用户授予完全控制作为“手动密钥”，以确保运维连续性。若安装严格由已以 `SYSTEM` 运行的进程执行，则仅会出现前两者。自定义非管理员服务账户在被授予 `Modify` 权限后会出现在此。

* **IsInherited：** 所有条目必须为 **False**。Servy 明确断开来自 `%ProgramData%` 根的继承，以防止其他应用或标准用户的“横向”访问。
* **AccessControlType：** 应为 **Allow**。不应存在针对 `Everyone`、`Users` 或 `Authenticated Users` 等宽泛身份组的**任何条目**，这些会在加固阶段被精确清除。
* **可执行文件加固状态：** 检查 `%ProgramData%\Servy` 下的各个核心二进制文件（例如 `Servy.Service.exe`）时，运行 `Set-ServyExePermissions.ps1` 可确保自定义服务运行账户显示 `ReadAndExecute` 权限，而非目录继承的 `Modify` 或 `FullControl` 权限。详见此[章节](./Security#executable-permission-hardening-mandatory)。

#### 审计人员实现上下文
为与源代码对照，审计人员可参考以下逻辑门控：
* **Inno Setup（`servy.iss`）：** `ShouldAddCurrentUser` 检查确保交互式安装程序的账户被加入文件夹 ACL。
* **运行时（`SecurityHelper.cs`）：** `ApplySecurityRules` 方法**仅当当前用户既不是 LocalSystem 账户、也不是 Administrators 组成员时**，为当前用户添加显式完全控制 ACE（管理员与 SYSTEM 已由强制的 Administrators/SYSTEM ACE 覆盖）。交互式安装程序账户的 ACE 由 Inno Setup 的 `ShouldAddCurrentUser` 步骤另行添加，并由 `ApplySecurityRules` 保留。

#### 子目录继承说明
虽然根保险库（`%ProgramData%\Servy`）已明确断开继承，但所有内部子文件夹（如 `recovery`、`db` 与 `security`）在创建时相对保险库根启用继承。这确保三主体“上锁房间”安全模型在整个数据结构中一致维持，而无需冗余的 ACL 写入。

## How Servy Protects Your Data

Servy 已将其安全模型从被动响应改为主动防御。

### 1. Automatic Directory Hardening (ACLs)

在先前版本中，Servy 对 `%ProgramData%\Servy` 文件夹依赖 Windows 默认权限。自 v7.9+ 起，Servy 接管控制。在安装或启动时，应用会自动执行以下操作：

* **断开继承：** Servy 将该文件夹与父驱动器的开放权限断开。
* **显式清除：** Servy 移除 `Users`、`Authenticated Users` 与 `Everyone` 组的访问。
* **受限准入：** 仅移除宽泛、无特权的组 — `Users`、`Authenticated Users` 与 `Everyone`。向 `SYSTEM` 与 `Administrators` 授予完全控制，保留安装用户（见下表），并保留您已为命名账户授予的显式 `Allow` 规则。这可防止本地权限提升：标准用户篡改服务以获取管理员权限的风险。

* **向下继承：** `%ProgramData%\Servy` 内的所有子文件夹与文件自动继承这些严格的父 ACL，确保新的服务目录、配置与日志默认保持锁定。
* **自定义权限保留（有附带条件）：** 您为*命名身份*添加的显式 `Allow` ACE 会保留。针对宽泛组 `Users`、`Authenticated Users` 或 `Everyone` 的任何 **`Allow`** 规则会在每次运行时移除。针对这些宽泛组的 **`Deny`** 规则会保留；仅针对 `Administrators`、`LocalSystem` 或安装用户的 `Deny` 规则会作为反抢占措施被移除。

### 2. Machine-Unique Encryption (Dynamic Entropy)

Servy 使用 Windows 数据保护 API（DPAPI），并增加一层安全。为防止二进制分析（有人阅读源代码以查找机密），Servy 从您计算机唯一的 `MachineGuid` 派生加密熵。

* **为何安全：** 加密熵在运行时从 Windows 注册表派生，而非硬编码在应用二进制中。
* **不可移植：** 因为每台计算机有不同的 ID，您的 `aes_key.dat` 文件复制到另一台机器后无效。

若 `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography\MachineGuid` 缺失、不可读或受注册表权限限制，Servy 会记录关键安全降级错误，并回退为从主机的 `Environment.MachineName` 派生熵。在此回退模式下，重命名主机计算机会改变熵计算，并永久破坏所有受保护配置字段的解密。

> [!CAUTION]
> **动态熵依赖与注册表敏感性**
>
> Servy 将其 DPAPI 主密钥（`%ProgramData%\Servy\security\aes_key.dat`）绑定到系统的 `MachineGuid`。若在活动安装上修改、删除、损坏 `MachineGuid`，或被注册表权限阻止，Servy 将无法解除保护其密钥材料；导致 Servy Manager、`servy-cli` 与后台服务立即解密失败并停止操作。
>
> 若在初始密钥生成期间 `MachineGuid` 缺失或不可读，Servy 会回退使用机器的主机名（`Environment.MachineName`）作为动态熵源，并记录关键安全降级事件。若主机随后在此回退状态下被重命名，DPAPI 解密同样会失败。
>
> 在无需重新导入服务配置的情况下从解密失败中恢复：
> * 在 `HKLM\SOFTWARE\Microsoft\Cryptography` 中恢复原始 `MachineGuid` 字符串。
> * 若权限限制阻止 Servy 读取 `MachineGuid`，修复注册表访问控制列表（ACL）。
> * 若在回退状态下运行，还原主机名。
> * 否则，若有有效的既有转储归档，清除过期保险库（`%ProgramData%\Servy\db` 与 `%ProgramData%\Servy\security`），并使用 `Servy-Restore.ps1` 恢复配置（参见 [Backup/Restore & VM Cloning](./Backup-Restore-&-VM-Cloning)）。

### 3. Cryptographic Key Derivation (HKDF)

为遵循严格的密码学最佳实践，Servy 使用 **HKDF（RFC 5869）** 从主密钥派生独立子密钥。不同的 `info` 上下文字符串（`V2_AES_ENCRYPTION` 与 `V2_HMAC_AUTHENTICATION`）为加密与认证子密钥提供域隔离，确保二者无法互相派生或替代。

### 4. Authenticated Encryption (v6.5+)

Servy 使用 DPAPI 派生的密钥材料，结合机器特定的注册表熵来保护敏感数据。认证加密（HMAC-SHA256 + AES-256-CBC）同时确保机密性与篡改检测，通过拒绝解密任何被修改的载荷来防止位翻转攻击。

### 5. In-Memory Defense (Memory Zeroing)

安全不止于硬盘。为防御高级内存刮取攻击，Servy 在 RAM 中安全处理机密。`SecureData` 类实现 `IDisposable`，并利用 `CryptographicOperations.ZeroMemory()` 在不再需要时立即擦除每个敏感缓冲区：

* **瞬态数据：** 明文与密文缓冲区在每次加密/解密调用后立即清零。
* **初始化材料：** 构造期间传入的主密钥克隆在子密钥派生后立即擦除。
* **密钥材料：** 对象释放时，所有活动敏感缓冲区被安全清零。这包括现代 AES 加密与 HMAC 认证所需的两个 HKDF 派生 V2 子密钥。（注：用于主密钥克隆与静态 IV 保留的两个旧版 V1 缓冲区在已发布生产构建中保持未分配，因为 `AllowLegacyV1Decryption` 在编译时永久禁用。）

与标准数组清除方法不同，该方法确保内存擦除不会被 JIT 编译器的发布优化省略，显著缩短攻击者从内存转储中提取密钥材料的机会窗口。

### 6. Sensitive Command-Line Arguments & Service Account Credentials

虽然 Servy 支持用于配置便利的 CLI 标志（例如 `--password`、`--envVars`、`--params`），但通过命令行参数传递敏感数据是**不安全的**。命令行参数对任何能够枚举进程列表的用户或进程可见（例如 `Get-Process`、`pslist` 或 Windows 事件跟踪），并且常被记录在 shell 历史文件与系统审计日志中。

#### 推荐方法

自 **v8.5** 起，Servy 提供两种处理敏感数据的安全替代方案：

1. **环境变量（推荐用于按服务的机密）：** 对每个敏感字段，Servy 支持关联的环境变量。Servy 在安装时透明读取这些变量，确保机密永不触及进程参数字符串。
2. **导入配置（推荐用于复杂部署）：** 使用 `import` 命令配合 XML 或 JSON 配置文件。这使敏感值完全离开命令行，并支持结构化、可版本控制的配置管理。

#### 敏感字段参考

以下参数被视为敏感，应通过环境变量（v8.5+）或配置文件提供：

| 参数 | 环境变量 | 说明 |
| --- | --- | --- |
| `--password` | `SERVY_PASSWORD` | Windows 服务账户密码。 |
| `--params` | `SERVY_PROCESS_PARAMETERS` | 服务进程的命令行参数。 |
| `--envVars` | `SERVY_ENVIRONMENT_VARIABLES` | 服务进程的环境变量。 |
| `--failureProgramParams` | `SERVY_FAILURE_PROGRAM_PARAMETERS` | 失败恢复程序的参数。 |
| `--preLaunchParams` | `SERVY_PRE_LAUNCH_PARAMETERS` | 启动前可执行文件的参数。 |
| `--preLaunchEnv` | `SERVY_PRE_LAUNCH_ENVIRONMENT_VARIABLES` | 启动前可执行文件的环境变量。 |
| `--postLaunchParams` | `SERVY_POST_LAUNCH_PARAMETERS` | 启动后可执行文件的参数。 |
| `--preStopParams` | `SERVY_PRE_STOP_PARAMETERS` | 停止前可执行文件的参数。 |
| `--postStopParams` | `SERVY_POST_STOP_PARAMETERS` | 停止后可执行文件的参数。 |

##### PowerShell 示例：

```powershell
# Set the secrets in the process-level environment
$env:SERVY_PASSWORD = 'p@ssw0rd_123!'
$env:SERVY_ENVIRONMENT_VARIABLES = 'API_KEY=secret_key_123;DB_URL=...'

# Install the service (omit sensitive flags)
servy-cli install --name="MySecureService" --path="C:\App\app.exe" --user="DOMAIN\svc_account"

# Clear variables immediately after use
Remove-Item Env:SERVY_PASSWORD
Remove-Item Env:SERVY_ENVIRONMENT_VARIABLES
```

## Infiltration Guard: Local Import Enforcement

从通用命名约定（UNC）路径或通过重定向路径（例如符号链接或联接点）导入服务配置会带来严重安全风险。这些技术旨在绕过系统信任边界，并将应用执行层暴露于恶意配置注入。

为维护系统完整性，导入流水线明确阻止非本地路径。此强制措施缓解的主要攻击向量包括：

* **攻击者控制的配置注入：** UNC 目标（例如 `\\attacker\share\evil.xml`）允许远程对手在其直接控制的基础设施节点上托管恶意配置文件。一旦被摄取，攻击者可注入任意可执行路径、未授权参数或未验证的环境变量，从而劫持服务的运行时行为。
* **路径重定向攻击：** 通过利用文件系统符号链接、目录联接点或专用 Win32 重解析点，攻击者可操纵路径解析机制。这可诱使引擎从与预期完全不同的后端目标读取，暴露敏感文件或从意外的网络位置拉取参数。
* **权限提升与系统完整性：** 因为引擎会执行管理提权校验检查以安全运行，它拥有对本机的高权限访问。若导入任务被操纵为从受保护的操作系统目录（例如 `Windows` 或 `System32` 命名空间）处理文件，则可促成非预期的系统级文件访问或操纵。
* **基于网络的映射绕过：** 虚拟本地路径 — 包括映射的网络驱动器（例如 `Z:\config.json`）或本地 DOS 设备替换（`subst`）— 常常掩盖底层远程存储卷。这些目标本质上缺乏本地存储硬件的严格安全边界，引入网络级拦截向量。

### Mitigation: Defense-in-Depth Pipeline

为对抗这些向量，引擎在 CLI 或 GUI 界面发生任何文件访问之前，将所有配置路径通过严格、顺序的校验链：

1. **显式 UNC 检查：** 拒绝任何以标准网络前缀（`\\`）开头或解析为远程 URI 的原始路径字符串。
2. **驱动器接口查询：** 通过 `DriveInfo` 评估目标卷，主动阻止网络支持的逻辑驱动器盘符。
3. **重解析点祖先遍历：** 递归追踪完整目录树，验证没有父路径或兄弟路径使用符号链接或目录联接点。
4. **文件级符号链接评估：** 直接检查文件系统属性，确认目标文件是物理的、非符号实体。
5. **保留设备阻止：** 防止使用旧版系统设备名称（例如 `CON`、`PRN`、`AUX`）的欺骗尝试。
6. **受保护系统目录围栏：** 防止从主要管理操作系统路径加载配置。
7. **Win32 内核句柄最终化：** 打开临时受限读取句柄，并通过 `GetFinalPathNameByHandle` 解析目标的最终规范路径，确保联接点、subst 映射与虚拟设备无法掩盖 UNC 目标。

## Supply Chain and Trust

安全需要透明。您不应猜测 Servy 是否安全。

  * **数字签名：** 所有可执行文件与安装程序均由 SignPath 签名。这证明代码自离开构建服务器以来未被篡改。
  * **SBOM（软件物料清单）：** Servy 发布包含 CycloneDX 格式的每个组件与依赖的完整清单。
  * **漏洞扫描：** 每当已发布公告与某一依赖匹配时，GitHub Dependabot 会发出告警。
  * **已扫描的发布：** 发布二进制在 VirusTotal 上扫描，并向 Microsoft Security Intelligence 及相关杀毒厂商提交误报报告。

## The Servy Trust Boundary

Servy 在**单一信任边界**安全模型下运行。Servy 管理的所有服务在共享根保险库目录 `%ProgramData%\Servy` 内执行并持久化运行时数据。

### Architectural Design & Runtime Permissions

因为 `Servy.Service.exe` 直接以您配置的服务账户身份执行，该账户需要对 `%ProgramData%\Servy` 目录树拥有 **Modify** 权限，以执行必要的运行时操作：

* **数据库操作：** 向 `%ProgramData%\Servy\db\Servy.db` 写入服务状态更新，需要对包含数据库的目录具备 POSIX/Win32 文件锁定权限（`-wal` 与 `-shm` 预写日志）。
* **辅助程序提取：** 在生命周期恢复例程期间提取运行时辅助二进制（例如 `Servy.Restarter.exe`）。
* **日志与恢复：** 向 `%ProgramData%\Servy\logs\Servy.Service.log` 写入日志，并序列化进程恢复元数据（`%ProgramData%\Servy\recovery\`）。

### Executable Permission Hardening (Mandatory)

虽然非管理员运行账户需要在目录级别（`%ProgramData%\Servy`）拥有 **Modify** 访问以写入日志流、数据库锁与状态文件，但将核心二进制可执行文件与已加载程序集限制为 **读取与执行**（`RX`）对**任何运行 Servy 服务的自定义账户都是严格强制的**。

> [!CAUTION]
> **强制脚本执行与权限加固**
>
> 若自定义账户对 `%ProgramData%\Servy` 根保险库拥有 **Modify** 权限，且加固脚本**从未**执行过，则为该账户（或包含它的组）运行 `Set-ServyExePermissions.ps1` 是**强制加固步骤**：否则该账户会保留对核心二进制的继承 `Modify` 权限，并可替换或篡改由 `SYSTEM` 与 `Administrators` 执行的代码。服务在不运行脚本的情况下仍可启动，这正是为何加固步骤绝不可跳过。
>
> **加固后的行为：**
> 运行 `Set-ServyExePermissions.ps1` 会断开 Servy 核心二进制上的 ACL 继承，并限制显式执行权限。一旦在保险库上执行过此加固脚本，之后添加的任何自定义服务账户将**不会**从 `%ProgramData%\Servy` 父目录继承权限。若新的自定义账户被授予保险库根权限，但**未**为该账户重新运行 `Set-ServyExePermissions.ps1`，Windows 服务控制管理器（SCM）将对 `Servy.Service.exe` 被拒绝执行权限（`ERROR_ACCESS_DENIED`），导致服务启动失败。为运维连续性，必须为每个新的自定义账户重新运行加固脚本。

运行 `Set-ServyExePermissions.ps1` 可保护您的部署免受：
* **无特权二进制替换与篡改：** 防止被攻陷的服务进程或运行账户覆盖核心二进制（`Servy.Service.exe`、`Servy.Restarter.exe` 等），从而在提升的管理上下文中执行任意代码。
* **DLL 劫持：** 阻止恶意或被攻陷的非管理员身份在应用保险库目录中植入或替换共享 `.dll` 依赖。
* **本地权限提升（LPE）：** 确保服务运行权限无法被利用以获得对由 `SYSTEM` 或 `Administrators` 执行的可执行组件的未授权写访问。

您可以使用 `Set-ServyExePermissions.ps1` 工具对核心 Servy 二进制（`Servy.Service.exe`、`Servy.Service.CLI.exe` 与 `Servy.Restarter.exe`，或其 `.Net48.exe` 与 `*.dll` 对应文件）强制执行此加固。

> [!NOTE]
> 自 v9.7 起，Servy 会在 `%ProgramData%\Servy` 中位于原子文件更新期间自动捕获并保留 `*.exe` 与 `*.dll` 文件上的既有显式访问控制列表（ACL）。一旦配置完成，您的 **读取与执行** 权限边界会在应用更新与嵌入资源重新提取之间自动保持。

> [!IMPORTANT]
> 在 v9.7 之前，`%ProgramData%\Servy` 内 `*.exe` 与 `*.dll` 文件的 ACL 在原子更新之间**不会保留**。在旧版本上升级或重新提取二进制会使新文件继承默认目录权限（`Modify`），需要在每次更新后重新运行 `Set-ServyExePermissions.ps1` 以恢复 **读取与执行** 加固。

#### Script Availability

* **Servy v9.7+：** 安装后 `Set-ServyExePermissions.ps1` 直接位于 `%ProgramFiles%\Servy`。对于便携版构建，它包含在便携包根目录中。
* **v9.7 之前的版本：** 直接从仓库下载脚本：
  * [现代构建（.NET 10.0+）](https://raw.githubusercontent.com/aelassas/servy/refs/heads/main/setup/Set-ServyExePermissions.ps1)
  * [.NET Framework 4.8 构建](https://raw.githubusercontent.com/aelassas/servy/refs/heads/net48/setup/Set-ServyExePermissions.ps1)

#### Hardening Utility Usage

从**提升（管理员）** PowerShell 会话运行脚本，并指定您的服务运行账户或目标账户：

```powershell
# Local account / relative notation
.\Set-ServyExePermissions.ps1 -TargetAccount ".\user_svc"

# Active Directory domain user or group
.\Set-ServyExePermissions.ps1 -TargetAccount "MYDOMAIN\svc-servy"

# Group Managed Service Account (gMSA)
.\Set-ServyExePermissions.ps1 -TargetAccount "CORP\app-gmsa$"
```

脚本将目录继承的权限转换为显式规则，清除指定账户在二进制上先前的 `Modify` 特权，将其锁定为 `Read & Execute`，并使用与语言无关的知名 SID 为 `NT AUTHORITY\SYSTEM` 与 `BUILTIN\Administrators` 保留 `Full Control`。

> [!NOTE]
> **既有权限的保留：** 重新运行 `Set-ServyExePermissions.ps1` 以配置新用户或组时，会保留所有先前分配的自定义权限。先前脚本执行期间建立的显式 ACE 保持完好，使您可以增量添加运行账户而不撤销既有权限。

> [!TIP]
> **管理多个服务账户：** 若计划在单台服务器上管理数十个自定义运行账户，可考虑创建专用的本地或域安全组（例如 `Servy-ServiceRunners`）。您可以运行一次 `Set-ServyExePermissions.ps1 -TargetAccount "Servy-ServiceRunners"`，然后将任何新的自定义服务账户（`.\servy_svc`、`LocalService` 等）加入该组。

> [!IMPORTANT]
> **关于管理服务账户的重要安全说明：** 确保提供给 `Set-ServyExePermissions.ps1` 的目标服务账户**不是**本地 `BUILTIN\Administrators` 组的成员。因为 Windows 会评估访问令牌上附加的所有组 SID，且 `Allow` ACE 不会削减权限，`BUILTIN\Administrators` 中的任何账户都会通过组成员身份固有地继承对已加固二进制的 `FullControl`。为强制执行 Servy 的单一信任边界模型，并将运行访问限制为严格的 `Read & Execute` 或 `Read` 权限，请使用专用的、无特权的服务账户。

### What This Means for Your Deployment

* **共享信任层：** 被授予对 `%ProgramData%\Servy` 的 `Modify` 访问的所有服务账户共享同一安全边界。自定义服务账户可读取共享 SQLite 配置数据库、检查其他服务的日志，或与保险库中的文件交互。
* **跨服务影响：** 因为 `Modify` 访问授予整个保险库根，被攻陷的服务账户可能更改共享数据库记录、检查文件，或篡改同机服务的日志。虽然运行 `Set-ServyExePermissions.ps1` 可加固保险库中的核心二进制可执行文件与 DLL 以防二进制篡改，但它不会在同机服务之间隔离运行时数据库访问或日志文件。
* **预期使用环境：** Servy 面向专用应用服务器、CI/CD 运行环境以及所有已配置服务账户属于单一、相互信任管理层级的工作站。
* **零信任隔离：** 若您的安全架构需要严格的多租户隔离（服务 A 必须与服务 B 在密码学与权限上隔离），服务应部署在不同的虚拟机、隔离的操作系统安装或 Windows 容器中。

### Automatic Permissions Table (v7.9+)

| 身份 | 访问级别 | 由谁管理 | 说明 |
| :--- | :--- | :--- | :--- |
| **SYSTEM** | Full Control | Servy（自动） | 本地系统服务宿主管理所需。 |
| **Administrators** | Full Control | Servy（自动） | 管理配置所需。 |
| **安装用户** | Full Control | Servy（自动） | 在非提升安装时保留，以保障运维连续性。 |
| **自定义服务账户** | Modify | 用户（手动） | 对以非 SYSTEM 身份运行的服务必须手动授予。 |
| **标准用户** | 无 | Servy（自动） | 在启动时显式清除，以防止标准用户篡改。 |

> [!NOTE]
> 在 Servy 的 ACL 加固上下文中，**标准用户**指包括 `Everyone`、`Users` 或 `Authenticated Users` 在内的宽泛身份组。

> [!NOTE]
> 安装用户 ACE 仅在当前进程身份**既非 SYSTEM 也非管理员**时由 `SecurityHelper.ApplySecurityRules` 添加；对于提升（管理员）安装，该账户改为通过安装程序添加的 ACE 出现。

> [!IMPORTANT]
> 若您配置服务以自定义本地或域服务账户（或 gMSA）运行，您**必须手动向该账户授予对 `%ProgramData%\Servy` 的 `Modify` 权限**（并允许继承到子文件夹）。没有 `Modify` 权限，服务运行程序将无法初始化数据库写锁、日志或提取恢复辅助程序。随后必须使用 `Set-ServyExePermissions.ps1` 在 `.exe` 文件上独立加固可执行权限。详见 [Executable Permission Hardening (Mandatory)](./Security#executable-permission-hardening-mandatory) 章节。

## File Locations and Recovery

您的主加密密钥存储在此处：

  * **数据库：** `%ProgramData%\Servy\db\Servy.db`
  * **密钥：** `%ProgramData%\Servy\security\aes_key.dat`
  * **IV：** `%ProgramData%\Servy\security\aes_iv.dat`

`aes_iv.dat` 文件保存旧版 v1 密码格式（Servy < 6.5）使用的静态 IV。Servy 6.5+ 使用嵌入密文中的每消息随机 IV，因此当前构建中静态 IV 不再用于加密或解密任何内容 — v1 解密永久禁用（`AllowLegacyV1Decryption = false`）以缓解降级攻击。在禁用状态下，该文件在服务启动时**不会**被读取，也**不会**加载到内存；整个加载路径通过 `AllowLegacyV1Decryption` 常量编译排除，因此运行时在全新安装上不再创建 `aes_iv.dat`；该文件仅存在于最初由较旧（门控前）构建设置的机器上，应予保留 — **请勿删除。** 要迁移由 6.5 之前构建写入的记录，请用兼容 v1 的 Servy 版本导出，再将所得文件导入当前版本；它们将重新加密为 v2。

## Critical Warning: Machine Migration

因为加密与您特定的 Windows 安装绑定，您不能将 `.dat` 文件复制到新服务器。

要将 Servy 移到新 PC：
1. 在旧机器上导出服务。导出是未加密的，因此要像对待物理钥匙一样对待它：`Parameters`、`EnvironmentVariables` 等可能以明文写入。
1. 将导出文件移到新机器。
1. 导入服务。因为 Servy 从不将 LogOn 账户或密码持久化到导出中，**导入的服务默认将以 LocalSystem 运行**。
1. 对任何不应以 **LocalSystem** 运行的服务，在 Servy Manager（或通过 `servy-cli install`）中**手动重新输入服务账户凭据**。

若您的服务以域账户、gMSA 或本地账户运行，第 4 步是强制的。

## Best Practices

  * **备份整个 Servy 数据文件夹：** 在执行 Windows 重置或刷新之前，备份整个 `%ProgramData%\Servy\` 树。密钥（`security\*.dat`）与加密配置数据库（`db\Servy.db`）必须一起恢复 — 仅有密钥无法解密任何内容，仅有数据库在没有匹配密钥时也无法解密。
  * **使用托管账户：** 尽可能以组托管服务账户（gMSA）运行服务，以获得安全性与易用性的最佳平衡。
  * **审计访问：** 定期检查 `%ProgramData%\Servy` 文件夹的“安全”选项卡，确保没有未授权用户被手动添加。

## Troubleshooting

  * **启动时访问被拒绝：** 这通常意味着运行服务的账户对 `%ProgramData%\Servy` 文件夹没有权限。请参阅上方的权限表。
  * **解密错误：** 当 `.dat` 文件从另一台计算机移动而来，或 Windows `MachineGuid` 被更改时会发生。您需要重新输入每个加密字段 — `Password`、`Parameters`、`EnvironmentVariables`、`PreLaunchEnvironmentVariables`，以及对应的钩子 `Parameters`（`PreLaunchParameters`、`PostLaunchParameters`、`PreStopParameters`、`PostStopParameters`、`FailureProgramParameters`）— 因为它们全部存储在同一台机器绑定的 AES 密钥下。若您有来自原机器的导出，请立即导入（按 *Critical Warning: Machine Migration*），而不是手动重新配置每个服务。

Servy v7.9+ 自动化了 Windows 安全的常规部分 — 访问控制列表（ACL）与机器绑定密钥派生 — 使锁定配置成为默认，而非手动检查清单。

有问题？请查看完整的 [Troubleshooting Guide](./Troubleshooting)，或打开 [GitHub Issue](https://github.com/aelassas/servy/issues) 以获得社区与开发者的帮助。
