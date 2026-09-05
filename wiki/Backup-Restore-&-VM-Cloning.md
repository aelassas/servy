## 目录

1. [简介](#introduction)
1. [备份与还原实用脚本](#backup--restore-utility-scripts)
   * [Servy-Dump.ps1](#servy-dumpps1)
   * [Servy-Restore.ps1](#servy-restoreps1)
1. [安全警告与凭据处理](#security-warnings--credential-handling)
   * [哪些内容无法在 dump/restore 周期中保留？](#what-does-not-survive-a-dumprestore-cycle)
1. [虚拟机克隆与迁移（VMware / Hyper-V）](#virtual-machine-cloning--migration-vmware--hyper-v)
   * [1. 克隆 VM 或从模板部署会破坏熵与加密吗？](#1-will-cloning-vms-or-deploying-from-templates-break-entropy-and-encryption)
   * [2. 更改虚拟 CPU 或 RAM 配置会破坏加密吗？](#2-will-changing-virtual-cpu-or-ram-configurations-break-encryption)
   * [3. 更改虚拟 NIC MAC 地址会破坏加密吗？](#3-will-changing-virtual-nic-mac-addresses-break-encryption)
   * [4. 密钥存储与动态熵有何不同？](#4-how-does-key-storage-differ-from-dynamic-entropy)
1. [推荐的 VMware / 黄金镜像部署工作流](#recommended-vmware--golden-image-deployment-workflow)
   * [自动化步骤](#automation-steps)
1. [另请参阅](#see-also)

## Introduction

本文档介绍使用官方 PowerShell 实用脚本（`Servy-Dump.ps1` 和 `Servy-Restore.ps1`）备份与恢复 Servy 服务配置的流程，以及机器迁移、Windows 重装、虚拟机克隆、镜像和模板迁移（VMware、Hyper-V、Azure、AWS）的架构指南。

Servy 自动加密存储在其配置数据库中的敏感字段，包括进程参数、环境变量、密码、API 密钥以及预/后生命周期钩子；使用通过 Windows 数据保护 API（DPAPI）和系统的 `MachineGuid`（`HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography\MachineGuid`）绑定到本地 OS 安装的主 AES 密钥。由于此加密保险库严格绑定到特定的 Windows 安装，执行会改变机器标识的操作——例如将数据库文件复制到另一主机、重装 Windows、还原 OS 磁盘镜像，或克隆虚拟机（VMware/Hyper-V）——会永久破坏这些受保护字段的解密能力。

`Servy-Dump.ps1` 和 `Servy-Restore.ps1` 脚本用于弥合这一差距：它们在源机器上将服务定义（不含凭据）导出为 XML 归档，并在目标上重新导入，还原期间会初始化全新的、绑定到机器的加密保险库。

## Backup & Restore Utility Scripts

从 **v10.0+** 起，Servy 在 `%ProgramFiles%\Servy\`（或便携发行版根目录）中直接包含两个管理用 PowerShell 脚本，以简化环境迁移、备份例程和基于模板的预配。对于 v10.0 之前的版本，可直接从官方仓库下载脚本：
* [`Servy-Dump.ps1`](https://github.com/aelassas/servy/blob/main/src/Servy.CLI/Servy-Dump.ps1)（`main` 分支）
* [`Servy-Restore.ps1`](https://github.com/aelassas/servy/blob/main/src/Servy.CLI/Servy-Restore.ps1)（`main` 分支）

对于 .NET Framework 4.8 构建，可直接从 `net48` 分支下载脚本：

* [`Servy-Dump.ps1`](https://github.com/aelassas/servy/blob/net48/src/Servy.CLI/Servy-Dump.ps1)（`net48` 分支）
* [`Servy-Restore.ps1`](https://github.com/aelassas/servy/blob/net48/src/Servy.CLI/Servy-Restore.ps1)（`net48` 分支）

> [!IMPORTANT]
> **系统要求与 OS 下限**
>
> 两个脚本都需要管理员权限（若未以管理员运行则返回 `exit code 1`）。
> * **`main` 分支（.NET 10.0+ / 现代构建）：**
>   * **`Servy-Dump.ps1`**：需要 **Windows 10 / Windows Server 2016 或更高版本** 以及 **PowerShell 5.1+**。它使用 OS 原生的 `%SystemRoot%\System32\winsqlite3.dll` 并以原生 UTF-16 封送查询 `Servy.db`。
>   * **`Servy-Restore.ps1`**：需要 **Windows 10 / Windows Server 2016 或更高版本** 以及 **PowerShell 5.1+**。
>
> * **`net48` 分支（.NET Framework 4.8 / 旧版构建）：**
>   * **`Servy-Dump.ps1`**：支持 **Windows 7 SP1 / Windows Server 2008 R2 或更高版本** 以及 **PowerShell 2.0+**。使用多层数据库检查层（优先使用 Servy 安装目录中的 `System.Data.SQLite.dll` 或 `e_sqlite3.dll`，并动态回退到 `winsqlite3.dll` / `sqlite3.dll`）。
>   * **`Servy-Restore.ps1`**：支持 **Windows 7 SP1 / Windows Server 2008 R2 或更高版本** 以及 **PowerShell 2.0+**。当 `Expand-Archive` 或 `.NET ZipFile` 不可用时，使用原生 COM `Shell.Application` 解压作为回退。
>
> * **跨版本还原兼容性：**
>
> `Servy-Restore.ps1` 没有直接的 SQLite 互操作依赖；它通过 `Import-ServyServiceConfig` 操作。在现代机器上生成的 dump 归档可使用 `net48` 分支版本的 `Servy-Restore.ps1` 直接还原到旧版 Windows 7 / Server 2008 R2 目标。
>

### Servy-Dump.ps1

`Servy-Dump.ps1` 检查本地 Servy SQLite 数据库（`%ProgramData%\Servy\db\Servy.db`），枚举所有已注册的服务定义，并使用 `Export-ServyServiceConfig` 将每个服务的配置导出为单独的 XML 文件。配置文件格式和完整字段参考见 [导出与导入服务](./Export-Import-Services)。所有 XML 定义随后与用于完整性验证的 `.sha256` sidecar 文件一起压缩到单个合并的 `.zip` 归档中。

#### 脚本功能

* **原生与多层互操作：** 在 `main` 分支上，使用 Windows 原生的 `%SystemRoot%\System32\winsqlite3.dll` 查询 `Servy.db`，无需安装外部 DLL。在 `net48` 分支上，使用托管的 `System.Data.SQLite.dll` / `e_sqlite3.dll`，并动态回退到 `kernel32`。
* **按服务错误隔离：** 个别导出失败不会删除已成功导出的文件或中止整个过程。若至少有一个服务导出成功，则生成 zip 归档并返回退出码 `7`。
* **净化文件名与编码安全：** 自动净化包含非法文件系统字符的服务名称，并使用原生 UTF-16 封送安全处理 Unicode 服务名称（例如 `Café-Svc` 或 `服务`）。
* **安全检查：** 需要提升的管理员权限，并在未获明确许可时阻止意外覆盖。

#### 语法与用法

```powershell
# Basic usage (fails with exit code 3 if destination archive already exists)
.\Servy-Dump.ps1 -DestinationArchivePath "C:\Backups\Servy_Dump.zip"

# Force overwrite of existing dump archive
.\Servy-Dump.ps1 -DestinationArchivePath "C:\Backups\Servy_Dump.zip" -Overwrite

# Force overwrite of existing dump archive, uninstall each successfully exported service from the Windows SCM, and remove it from the Servy database
.\Servy-Dump.ps1 -DestinationArchivePath "C:\Backups\Servy_Dump.zip" -Overwrite -Uninstall
```

#### 参数

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `-DestinationArchivePath` | `String` | **是** | 目标 zip 归档目标文件（例如 `C:\Backups\Servy_Dump.zip`）。若提供目录路径或尾部分隔符，则写入 `$DestinationArchivePath\Servy_Dump.zip`；若未指定文件扩展名，则追加 `.zip`。使用 `-Overwrite` 替换现有归档。 |
| `-Overwrite` | `Switch` | 否 | 若目标 dump 归档已存在则覆盖。 |
| `-Uninstall` | `Switch` | 否 | 存在时，从 Windows SCM 卸载每个成功导出的服务，并将其从 Servy 数据库中移除。除非指定 `-Confirm:$false`，否则在卸载前通过 `ShouldProcess` 提示确认。 |

#### 退出码

| 代码 | 含义 |
| --- | --- |
| `0` | 成功。所有已注册服务配置均已成功导出并归档。（注意：当不存在数据库或未注册任何服务时也返回此码，此时**不会写入归档**）。 |
| `1` | 未以管理员权限运行。 |
| `2` | 无法定位或导入 Servy PowerShell 模块（`Servy.psm1`）。 |
| `3` | 目标归档已存在且未指定 `-Overwrite`。 |
| `4` | I/O 与检查失败。无法读取数据库、目标路径无效或不可写、在 `-Overwrite` 下无法替换现有 SHA-256 sidecar、归档压缩或 ACL 加固失败，或发生意外运行时错误。 |
| `5` | 设置编译失败。无法编译原生 SQLite 动态 P/Invoke 程序集绑定。 |
| `6` | 完全导出失败。无法导出任何服务配置；未生成输出归档。 |
| `7` | dump 归档已成功创建，但有一个或多个服务导出或卸载失败。 |
| `8` | 归档暂存不匹配。暂存的配置数量与导出数量不符；dump 已中止。 |

> [!WARNING]
> 在自动化备份作业中，除检查退出码外，还应检查归档文件是否存在且非空。仅退出码 `0` 并不能保证已生成归档。

> [!WARNING]
> **安全与文件权限通知**
>
> `Servy-Dump.ps1` 创建的 dump 归档（`.zip` dump 和 `.sha256` sidecar）包含未加密的明文服务配置。不会导出任何凭据（每次导出都省略 `UserAccount`、`Password` 和 `RunAsLocalSystem`），但执行参数、API 密钥、命令行参数、环境变量以及预/后钩子等敏感数据会以明文写入。
>
> 为防止凭据和配置暴露，`Servy-Dump.ps1` 自动对所有生成的输出文件强制严格的 Windows 访问控制列表（ACL）：
> * 断开来自父目录的权限继承。
> * 剥离所有宽泛组权限（`Users`、`Authenticated Users`、`Everyone`）以及自定义用户 ACE。
> * 访问仅限于 **内置 Administrators**（`S-1-5-32-544`）和 **Local SYSTEM**（`S-1-5-18`）。
>
> 若次要服务运行账户需要读取 dump 归档，管理员必须在 dump 完成后显式授予这些权限。

> [!TIP]
> `Servy-Dump.ps1` 在卸载服务前提示确认。要在自动化工作流中抑制提示，请使用 `-Confirm:$false`：
> ```powershell
> .\Servy-Dump.ps1 -DestinationArchivePath "C:\Backups\Servy_Dump.zip" -Overwrite -Uninstall -Confirm:$false
> ```

### Servy-Restore.ps1

`Servy-Restore.ps1` 摄取由 `Servy-Dump.ps1` 生成的合并 `.zip` dump 归档，对照随附的 `.sha256` sidecar 文件验证其完整性（sidecar 必须与 dump 文件位于同一目录），将各个服务 XML 文件解压到安全暂存位置，并通过 `Import-ServyServiceConfig` 将每个配置导入本地 Servy 实例。

#### 语法与用法

```powershell
# Restore service configurations (imports definitions into the Servy database)
.\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip"

# Restore service configurations AND install them into Windows SCM
.\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip" -Install

# Restore service configurations AND install them into Windows SCM without SHA-256 sidecar integrity verification
.\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip" -Install -SkipIntegrityCheck
```

> [!WARNING]
> **还原会覆盖现有服务**
>
> `Servy-Restore.ps1` 无条件导入归档中的每个配置。若目标机器上已存在同名服务，其存储的配置将被**替换**；若使用 `-Install` 选项，其 Windows SCM 注册也会被重写。
> 没有确认提示，也没有撤销能力：自 dump 以来在目标上所做的任何配置更改都会被覆盖。
>
> 此行为在下方黄金镜像克隆工作流中是有意设计的，该工作流在导入前会清除陈旧的继承配置。但在对正在使用的机器执行还原时请务必谨慎。
>
> **重要加密前提：**
>
> 仅当目标系统的本地机器标识和 DPAPI 密钥完好时，才能创建还原前的安全 dump。若目标 OS 已经过 Sysprep、Windows 重装或机器 SID 重新生成，其现有数据库无法被 `Servy-Dump.ps1` 解密；必须改为清除陈旧保险库（`%ProgramData%\Servy\db` 和 `%ProgramData%\Servy\security`）。不要删除整个 `%ProgramData%\Servy` 目录，因为 `%ProgramData%\Servy\logs` 和 `%ProgramData%\Servy\recovery` 包含应保留的有价值运行时日志和服务历史。
>
> 若目标机器的 DPAPI 范围完好且有活动服务在运行，请在覆盖还原**之前**进行新的安全 dump：
>
> ```powershell
> # Take a safety backup on an active, healthy host prior to running a restore
> .\Servy-Dump.ps1 -DestinationArchivePath "C:\Backups\Servy_PreRestore.zip" -Overwrite
> ```

#### 参数

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `-DumpArchivePath` | `String` | **是** | 指定要还原的目标 `.zip` dump 归档的路径。 |
| `-Install` | `Switch` | 否 | 自动将每个导入的服务注册到 Windows 服务控制管理器（SCM）。 |
| `-SkipIntegrityCheck` | `Switch` | 否 | 完全跳过 SHA-256 sidecar 验证：无论 `.sha256` sidecar 缺失、过时还是不匹配，都会在无完整性检查的情况下还原归档。 |
| `-MaxAllowedEntries` | `Int32` | 否 | 解压期间允许的归档最大条目数，以防止 zip 炸弹攻击（默认为 `1000`，范围：`1`-`100,000`）。 |
| `-MaxUncompressedBytes` | `Int64` | 否 | 解压期间允许的最大未压缩总字节数（默认为 `104857600` 字节 / 100 MB，范围：`1`-`10737418240` 字节 / 10 GB）。 |

#### 退出码

| 代码 | 含义 |
| --- | --- |
| `0` | 成功。当归档不包含 XML 配置文件时也返回此码，此时**不会导入任何内容**。 |
| `1` | 未以管理员权限运行。 |
| `2` | 无法定位或导入 Servy PowerShell 模块（`Servy.psm1`）。 |
| `3` | 指定的 dump 归档不存在。 |
| `4` | I/O 与解压失败。归档路径无效、无法解压归档、ACL 加固失败、检测到畸形条目、超出 -MaxAllowedEntries 或 -MaxUncompressedBytes 安全限制，或发生意外运行时错误。 |
| `5` | 校验和验证失败。`.sha256` sidecar 缺失（且未使用 `-SkipIntegrityCheck`）或检测到哈希不匹配。 |
| `6` | 完全导入失败。无法从归档导入任何服务配置。 |
| `7` | 部分导入警告。还原已完成，但有一个或多个服务导入失败。 |

## Security Warnings & Credential Handling

> [!CAUTION]
> **关键安全警告：未加密的配置**
>
> `Servy-Dump.ps1` 生成的 dump 归档包含**未加密的
> 明文 XML 文件**。不会导出任何凭据（每次导出都省略 `UserAccount`、
> `Password` 和 `RunAsLocalSystem`），但执行参数、API 密钥、命令行参数、环境
> 变量以及预/后钩子等敏感数据会以明文写入。请将生成的 dump 归档的访问限制为仅授权的管理人员。
> 完整字段参考见 [导出与导入服务](./Export-Import-Services)。
>
> 归档还包含服务配置引用的每个**可执行路径**：
> 被包装的程序及其启动目录、失败程序，以及预启动、后启动、预停止和后停止钩子可执行文件及其
> 参数和环境。
>
> 由于还原的服务默认为 `LocalSystem`（见下方提示），任何能**修改** dump 归档的人都能控制从该归档还原的每台机器上以 `LocalSystem` 运行的内容。请像限制读访问一样严格限制**写**访问，并将 dump 归档存储在 ACL 仅授予 Administrators 和 SYSTEM 访问权限的目录中，这与安装程序应用于 `%ProgramData%\Servy` 的保护相同：
>
> ```powershell
> icacls "C:\Staging" /inheritance:r /grant:r "*S-1-5-32-544:(OI)(CI)F" "*S-1-5-18:(OI)(CI)F"
> ```

> [!IMPORTANT]
> **还原时凭据重置为 LOCALSYSTEM**
>
> 出于安全原因，Servy 不导出 Windows 服务账户凭据（用户名和密码）。通过 `Servy-Restore.ps1`、`servy-cli` 或 Servy Manager 还原配置会自动将所有服务登录标识重置为 `LocalSystem`。
>
> **还原后需要采取的操作：**
>
> 若任何还原的服务在自定义账户下运行
> （`.\svc_account`、`DOMAIN\svc_account` 或 gMSA），您必须通过 [Servy Manager](./Servy-Manager)、[`servy-cli`](./Servy-CLI)
> 或 [PowerShell 模块](./Servy-PowerShell-Module) 手动重新输入登录
> **用户名和密码**，并重新运行可执行文件加固
> （通过 `Set-ServyExePermissions.ps1`，见 [安全](./Security)）。

### What does not survive a dump/restore cycle?

并非服务定义中的每个字段都会在导出中保留。在依赖还原的克隆与模板完全一致之前，请针对这些情况做好规划：

| 字段 | 导入时的行为 | 克隆后为何重要 |
| --- | --- | --- |
| `UserAccount` | 不导出；重置为 `LocalSystem` | 对每个使用自定义标识的服务重新输入 |
| `Password` | 不导出；重置为空 | 与 `UserAccount` 一起重新输入 |
| `RunAsLocalSystem` | 不导出；强制为 `LocalSystem` 基线 | 正是此字段导致上述重置 |
| `Pid` | 静默忽略 | 运行时状态；符合预期 |
| `PreviousStopTimeout` | 静默忽略 | 恢复调优不会被携带 |
| `ActiveStdoutPath` | 静默忽略 | 解析后的日志目标在克隆上重新派生 |
| `ActiveStderrPath` | 静默忽略 | 同上 |

导入时丢弃自定义标识会向日志写入警告。四个静默忽略的字段完全不产生警告，因此若模板依赖它们，请显式检查。

完整字段参考见 [导出与导入服务](./Export-Import-Services)。

## Virtual Machine Cloning & Migration (VMware / Hyper-V)

在管理虚拟化基础架构（VMware vSphere、Hyper-V、Azure VM、AWS EC2）时，理解 Servy 如何处理加密机器标识，对于基于模板的预配和镜像克隆至关重要。

### 1. Will cloning VMs or deploying from templates break entropy and encryption?

**取决于克隆是否经过通用化（generalized）。**

Servy 将其主 AES 密钥绑定到两个机器特定输入：Windows DPAPI
LocalMachine 主密钥，以及从
`HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography\MachineGuid` 读取的注册表熵。

* **Sysprep / 来宾自定义（`sysprep /generalize`、VMware Guest OS
  Customization、Azure 和 AWS 镜像部署）：是，解密会中断。**
  通用化会重新生成机器 SID 和 `MachineGuid`，并重置
  DPAPI 机器密钥。克隆实例无法解密 `%ProgramData%\Servy\db\Servy.db` 中的 `Password`、
  `Parameters`、`EnvironmentVariables` 或任何其他受保护字段。请遵循下方工作流。

* **未经通用化的原始克隆（无自定义的 VMware “Clone”、
  Hyper-V 导出/导入、还原的磁盘镜像）：否，解密继续
  工作。** 注册表配置单元和 DPAPI 机器密钥都是复制磁盘上的文件，因此与源相同，现有保险库仍可读。无需清除或重新导入。

* **源机器 / 模板主控** 在任一情况下都不受影响。

> [!WARNING]
> 原始克隆会保留源机器的密钥，这意味着每个克隆共享它们。若这在您的环境中不可接受，请通用化镜像并遵循下方工作流，使每个实例派生自己的密钥。

### 2. Will changing Virtual CPU or RAM configurations break encryption?

**否。**

Servy **不会**将其密钥材料绑定到 CPU ID、RAM 容量、BIOS UUID 或主板序列号等硬件指标。它仅绑定到 Windows DPAPI 主密钥和 OS 存储的注册表值（`MachineGuid`）。在 VMware 中修改 vCPU 或 RAM 分配对加密解密没有影响。

### 3. Will changing Virtual NIC MAC addresses break encryption?

**否。**

Servy 不会检查或将密钥材料绑定到网络适配器、IP 地址或 MAC 地址。更换虚拟 NIC、重新配置网络或升级 VMware Tools 不会使现有解密密钥失效。

### 4. How does key storage differ from dynamic entropy?

* **密钥存储：** 加密密钥材料（`aes_key.dat`）存储在磁盘上的 `%ProgramData%\Servy\security\` 中。
* **动态熵：** 额外的运行时熵直接从操作系统注册表（`MachineGuid`）派生。

若在初始密钥生成期间 `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography\MachineGuid` 缺失、不可读或受注册表权限限制，Servy 会回退为使用主机的 `Environment.MachineName` 作为动态熵源，并记录关键安全降级事件。主机名可预测且并非每个 OS 安装唯一；共享主机名的两个克隆会派生相同的熵。此外，若在此回退状态下随后重命名系统，熵计算会改变并破坏所有受保护字段的解密。

> [!WARNING]
> **动态熵依赖与注册表敏感性**
>
> Servy 将其 DPAPI 主密钥（`%ProgramData%\Servy\security\aes_key.dat`）绑定到系统的 `MachineGuid`。若在活动安装上修改、删除、损坏 `MachineGuid`，或被注册表权限阻止，Servy 将无法解除对其密钥材料的保护；导致 Servy Manager、`servy-cli` 和后台服务立即解密失败并停止操作。
> 若在现有安装上遇到解密失败：
> * 在 `HKLM\SOFTWARE\Microsoft\Cryptography` 中还原原始 `MachineGuid` 字符串。
> * 若权限限制阻止 Servy 读取 `MachineGuid`，请修复注册表访问控制列表（ACL）。
> * 若在回退状态下运行，请还原主机名。
> * 否则，若有有效的预先存在的 dump 归档，请清除陈旧保险库（`%ProgramData%\Servy\db` 和 `%ProgramData%\Servy\security`）并使用 `Servy-Restore.ps1` 还原配置。

> [!NOTE]
> 由 Servy 7.8 或更早版本写入的密钥材料不带熵。Servy 通过兼容路径读取它，记录 `SECURITY DEGRADATION WARNING`，并在首次成功读取时透明地以受熵保护的格式重新保存。在升级期间每个文件看到该警告一次是预期行为。

当机器标识被改变时（例如在通用化克隆、Windows 重装或 OS 迁移上）：

* **Windows DPAPI 主机密钥改变：** OS 重置其本地加密主密钥。
* **注册表 `MachineGuid` 改变：** OS 自定义过程为安装生成新的唯一标识符。

这两种改变都会使从原机器复制过来的 DPAPI 主密钥（`%ProgramData%\Servy\security\aes_key.dat`）的解密能力失效。

## Recommended VMware / Golden Image Deployment Workflow

为避免跨克隆虚拟机的 DPAPI 解密失败，**不要**尝试在 OS 实例之间复制 `%ProgramData%\Servy\security\aes_key.dat`。应在自动化的克隆后自定义流水线中利用 `Servy-Dump.ps1` 和 `Servy-Restore.ps1`：

```text
[ Golden Image / Template ]
         │
         ├── 1. Install Servy (%ProgramFiles%\Servy)
         ├── 2. Run Servy-Dump.ps1 (if pre-configured services exist)
         │      └─> .\Servy-Dump.ps1 -DestinationArchivePath "C:\Sysprep\Servy_Base_Dump.zip" -Overwrite
         │
         ▼ (VMware Clone / Sysprep Deployment)
[ New Cloned VM Instance ]
         │
         ├── 3. Execute Sysprep / Guest Customization (New IP, Hostname, MachineGuid)
         ├── 4. Purge the stale vault: delete %ProgramData%\Servy\db and %ProgramData%\Servy\security
         ├── 5. Run Servy-Restore.ps1 -DumpArchivePath "C:\Sysprep\Servy_Base_Dump.zip" -Install
         ├── 6. Re-enter Service Account Passwords & Run Set-ServyExePermissions.ps1
         └── 7. Remove Staged Dump Archive (C:\Sysprep\Servy_Base_Dump.zip)
```

### Automation Steps

1. **准备黄金模板：** 在主镜像上安装 Servy。
2. **导出基础配置：** 若模板包含标准基础服务定义，运行：
    ```powershell
    .\Servy-Dump.ps1 -DestinationArchivePath "C:\Sysprep\Servy_Base_Dump.zip" -Overwrite
    ```
    使用 `-Uninstall` 开关从 Windows 服务控制管理器（SCM）和 Servy 数据库中移除每个成功导出的服务：
    ```powershell
    .\Servy-Dump.ps1 -DestinationArchivePath "C:\Sysprep\Servy_Base_Dump.zip" -Overwrite -Uninstall
    ```
3. **部署克隆：** 在 VMware 中克隆 VM 并执行标准 Guest OS Customization / Sysprep。
4. **清除陈旧保险库：** 在克隆的 VM 上，删除从模板继承的数据库和密钥材料。Servy 会在下一次 CLI 操作时使用其加固的 ACL 重新创建这两个文件夹。
    ```powershell
    Remove-Item -LiteralPath "$env:ProgramData\Servy\db" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath "$env:ProgramData\Servy\security" -Recurse -Force -ErrorAction SilentlyContinue
    ```
    只需从保险库 `%ProgramData%\Servy` 中移除 `db\` 和 `security\` 文件夹。删除整个 `%ProgramData%\Servy` 也会丢弃 `logs\` 和 `recovery\`，这没有必要，且会使您失去模板中的服务历史。
5. **还原配置：** 在新克隆的 VM 首次启动时，运行：
    ```powershell
    .\Servy-Restore.ps1 -DumpArchivePath "C:\Sysprep\Servy_Base_Dump.zip" -Install
    ```
    Servy 会自动初始化绑定到新 VM 唯一 `MachineGuid` 和 DPAPI 范围的全新 `aes_key.dat`。
6. **重新应用登录凭据与二进制加固：** 重新分配自定义服务运行账户凭据（例如通过 Servy Manager、`servy-cli` 或 Servy PowerShell 模块）并强制强制二进制权限：
    ```powershell
    .\Set-ServyExePermissions.ps1 -TargetAccount "DOMAIN\svc-runner"
    ```
7. **移除暂存的 dump 归档：** 归档是明文的（见上方安全警告），且暂存在黄金镜像中时会复制到从该模板部署的每个 VM。在来宾自定义的最后一步删除它：
    ```powershell
    Remove-Item -LiteralPath "C:\Sysprep\Servy_Base_Dump.zip" -Force -ErrorAction SilentlyContinue
    ```

> [!CAUTION]
> 仅在模板上删除暂存的 dump 归档不够。因为每个克隆都会从镜像获得自己的副本，必须在还原完成后在每个已部署的 VM 上执行移除。

## See Also

* [导出与导入服务](./Export-Import-Services) - 配置文件格式和字段参考
* [安全](./Security) - 保险库、密钥材料和可执行文件加固
* [Servy PowerShell 模块](./Servy-PowerShell-Module) - `Export-ServyServiceConfig` 和 `Import-ServyServiceConfig`
* [故障排除](./Troubleshooting) - 还原后的 DPAPI 解密失败
