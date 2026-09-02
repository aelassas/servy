[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/aelassas/servy/actions/workflows/build.yml)
[![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg?branch=main)](https://github.com/aelassas/servy/actions/workflows/test.yml)
[![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/main?label=coverage&t=8)](https://codecov.io/gh/aelassas/servy)
[![](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)

# Servy

Servy lets you run any app as a native Windows service with full control over the working directory, startup type, process priority, CPU affinity, logging, health checks, environment variables, dependencies, pre-launch and post-launch hooks, pre-stop and post-stop hooks, and parameters.

Servy is digitally signed with a code-signing certificate provided by the SignPath Foundation. The signature verifies the publisher, enables Windows to detect any modification of the released binaries and installers, and prevents SmartScreen warnings.

Servy offers a desktop app, a CLI, and a PowerShell module that let you create, configure, and manage Windows services interactively or through scripts and CI/CD pipelines. It also includes a Manager app for monitoring and managing all installed services in real time.

Servy continuously monitors your app, restarting it automatically if it crashes, hangs, or stops. It is well suited to keeping non-service apps running in the background and ensuring they start automatically at system boot, even before logon, without rewriting them as services. Use it to run Node.js, Python, .NET, Java, Go, Rust, PHP, or Ruby applications; keep web servers, background workers, sync tools, or daemons alive after reboots; and automate task runners, schedulers, or scripts in production with built-in health checks, logging, and restart policies.

## Demo Video

This video demonstrates Servy 1.0. While Servy has evolved significantly since then with many features like real-time CPU/RAM monitoring, stdout/stderr streaming, heartbeat pings, notifications, and CPU affinity, the core concept remains the same.

[![Servy Demo Video](https://github.com/user-attachments/assets/183a48eb-0763-46b5-aba2-7db01857c942)](https://www.youtube.com/watch?v=biHq17j4RbI)

## Why?

See [NOTES.md](NOTES.md) for details.

## Getting Started

Download the latest release from [GitHub](https://github.com/aelassas/servy/releases/latest) or install via a package manager:

**WinGet**

```powershell
winget install servy
```

**Chocolatey**

```powershell
choco install -y servy
```

**Scoop**

```powershell
scoop bucket add extras
scoop install servy
```

**Patch My PC**

Servy is available in the official [Patch My PC catalog](https://patchmypc.com/supported-products/) for enterprise automated deployment and updates via Microsoft Intune and ConfigMgr (SCCM).

> [!NOTE]
> **Legacy OS Support (Windows 7 SP1 / 8.x / Server 2008 R2):** Package managers carry the self-contained modern build. For older platforms, download `servy-x.x-net48-x64-installer.exe` or `servy-x.x-net48-x64-portable.7z` directly from [GitHub Releases](https://github.com/aelassas/servy/releases/latest) (requires .NET Framework 4.8).

## Quick Example

You can manage services using the [desktop app (GUI)](https://github.com/aelassas/servy/wiki/Servy-Desktop-App), the [CLI](https://github.com/aelassas/servy/wiki/Servy-CLI), or [PowerShell](https://github.com/aelassas/servy/wiki/Servy-PowerShell-Module).

Here's a minimal example using the CLI to run a Node.js app as a Windows service:

```powershell
servy-cli install `
  --name="MyService" `
  --path="C:\Program Files\nodejs\node.exe" `
  --startupDir="C:\MyServer" `
  --params="server.js" `
  --enableHealth
```

This creates a service named `MyService` that runs your Node.js server in the background, starts automatically with Windows, and has [health monitoring](https://github.com/aelassas/servy/wiki/Health-Monitoring-&-Recovery) enabled.

Then start the service:

```cmd
servy-cli start --name="MyService"
```

Or from an **elevated** Command Prompt:

```cmd
sc.exe start MyService
```

Explore more [examples and recipes](https://github.com/aelassas/servy/wiki/Examples-&-Recipes) for Python, Java, Go, and other popular frameworks.

## Quick Links

* [Download](https://github.com/aelassas/servy/releases/latest)
* [Installation Guide](https://github.com/aelassas/servy/wiki/Installation-Guide)
* [Overview](https://github.com/aelassas/servy/wiki/Overview)
* [Usage](https://github.com/aelassas/servy/wiki/Usage)
* [FAQ](https://github.com/aelassas/servy/wiki/FAQ)
* [Full Documentation](https://github.com/aelassas/servy/wiki)

## Features

* Clean, simple UI
* Monitor and manage all installed services with Servy Manager
* Real-time CPU and RAM monitoring with live performance graphs for installed services
* Real-time service stdout and stderr output preview in Servy Console
* Service dependency tree visualization with status indicators
* CLI and PowerShell module for full scripting and automated deployments
* Run any executable as a Windows service
* Set service name, description, startup type, priority, CPU affinity, working directory, environment variables, and dependencies
* Environment variable expansion supported in parameters, process paths, and startup directories
* Run services as Local System, local or domain accounts, Active Directory accounts, or gMSAs
* Redirect stdout/stderr to log files with automatic size-based and date-based rotations
* Run pre-launch hooks before starting the service, with retries, timeout, logging and failure handling
* Run post-launch hooks after the application starts successfully
* Run pre-stop and post-stop hooks before and after the application stops
* Supports `Ctrl+C` for command-line apps, close-window for GUI apps, and force kill if unresponsive
* Supports `Ctrl+C` propagation to descendant processes of the wrapped process
* Prevent orphaned/zombie processes with improved lifecycle management while ensuring resource cleanup
* Health checks and automatic service recovery
* Browse and search logs by level, date, and keyword for faster troubleshooting from Servy Manager
* Export/Import service configurations for easy backups and automation
* Service Event Notification alerts on service failures via Windows notifications and email
* Modern build (default, self-contained): Windows 10 (1809+), Windows 11 and Windows Server 2016+, on x64 and ARM64
* Legacy build (`net48`, requires .NET Framework 4.8): Windows 7 SP1, 8.x and Windows Server 2008 R2+, x64 only - see the [version comparison](https://github.com/aelassas/servy/wiki/Installation-Guide#version-comparison)

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## Roadmap

See [ROADMAP.md](ROADMAP.md).

## Support & Contributing

Servy is free and open-source. If you use it in a commercial or revenue-generating context, or find it valuable, consider supporting the project via [GitHub Sponsors](https://github.com/sponsors/aelassas), [PayPal](https://www.paypal.me/aelassaspp), or [Buy Me a Coffee](https://www.buymeacoffee.com/aelassas).

Open-source software requires time, effort, and resources to maintain. Every contribution, big or small, makes a difference and motivates continued work on features, bug fixes, and new ideas.

If you have suggestions or issues or would like to contribute, feel free to [open an issue](https://github.com/aelassas/servy/issues) or [submit a pull request](https://github.com/aelassas/servy/pulls).

## Stats for Nerds

[![LoC - Prod](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/loc-prod.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![LoC - Tests](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/loc-tests.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![LoC - Total](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/loc-total.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![GitHub Downloads](https://img.shields.io/github/downloads/aelassas/servy/total)](https://servy-win.github.io/downloads)

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt). See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for third-party software notices and licenses.

## Acknowledgments

Thanks to [SignPath](https://signpath.io/?utm_source=foundation&utm_medium=github&utm_campaign=servy) for providing a free code signing service, and to the [SignPath Foundation](https://signpath.org/?utm_source=foundation&utm_medium=github&utm_campaign=servy) for supplying a free code signing certificate.

Thanks to [JetBrains](https://www.jetbrains.com/) for providing an [open-source license](https://www.jetbrains.com/community/opensource/) for their tools. Their software made it much easier to profile, debug, and optimize Servy, helping improve its performance and stability. Having access to these professional tools really made a difference during development and saved a lot of time.

Special thanks to everyone who tested Servy, reported issues, and suggested improvements on GitHub and Reddit. Your feedback and contributions have shaped the project and made it better with every release.

<p>
  <a href="https://signpath.org/?utm_source=foundation&utm_medium=github&utm_campaign=servy">
    <img alt="SignPath Foundation" src="https://aelassas.github.io/content/signpath.png?v=2" width="54" height="51">
  </a>
  &nbsp;
  <a href="https://www.jetbrains.com/community/opensource/">
    <img alt="JetBrains Open Source" src="https://aelassas.github.io/content/jetbrains.svg?v=3" width="54" height="51">
  </a>
</p>
