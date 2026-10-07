[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/aelassas/servy/actions/workflows/build.yml)
[![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg?branch=main)](https://github.com/aelassas/servy/actions/workflows/test.yml)
[![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/main?label=coverage&t=9)](https://codecov.io/gh/aelassas/servy)
[![docs](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)

# Servy

Servy lets you run any app as a native Windows service with full control over the working directory, startup type, process priority, CPU affinity, logging, health checks, environment variables, dependencies, pre-launch and post-launch hooks, pre-stop and post-stop hooks, and parameters.

Starting from v10.2, Servy provides enterprise-grade per-service isolation between custom accounts, DPAPI, HKDF & AES-256 HMAC authenticated encryption, and automated vault ACL hardening. All releases feature signed binaries, SBOMs, and continuous vulnerability scanning. See [Security.md](https://github.com/aelassas/servy/wiki/Security) and [Architecture.md](https://github.com/aelassas/servy/wiki/Architecture) for details.

Servy offers a desktop app, a CLI, and a PowerShell module that let you create, configure, and manage Windows services interactively or through scripts and CI/CD pipelines. It also includes a Manager app for monitoring and managing all installed services in real time.

Servy continuously monitors your app, restarting it automatically if it crashes, hangs, or stops. It allows non-service apps to run in the background and start automatically at system boot, even before logon, without rewriting them as services. Use it to run Node.js, Python, .NET, Java, Go, Rust, PHP, or Ruby applications; keep web servers, LLMs, background workers, sync tools, or daemons alive after reboots; and automate task runners, schedulers, or scripts in production with built-in health checks, logging, and restart policies.

## Why?

See [NOTES.md](NOTES.md).

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

**Legacy OS Support**

Package managers carry the self-contained modern build. For older platforms (Windows 7 SP1 / 8.x / Server 2008 R2), download `servy-x.x-net48-x64-installer.exe` or `servy-x.x-net48-x64-portable.7z` directly from [GitHub Releases](https://github.com/aelassas/servy/releases/latest) (requires .NET Framework 4.8).

## Quick Example

You can manage services using the [desktop app](https://github.com/aelassas/servy/wiki/Servy-Desktop-App), [CLI](https://github.com/aelassas/servy/wiki/Servy-CLI), or [PowerShell](https://github.com/aelassas/servy/wiki/Servy-PowerShell-Module).

Here's a minimal example using the CLI to run a Node.js server as a Windows service. Run it from an elevated PowerShell prompt: installing, starting and stopping a Windows service all require administrator privileges.

```powershell
servy-cli install `
  --name="MyService" `
  --path="C:\Program Files\nodejs\node.exe" `
  --startupDir="C:\MyServer" `
  --params="server.js" `
  --enableHealth
```

This installs `MyService` to run a Node.js server in the background with auto-startup and [health monitoring](https://github.com/aelassas/servy/wiki/Health-Monitoring-&-Recovery).

See additional [examples and recipes](https://github.com/aelassas/servy/wiki/Examples-&-Recipes) for Python, Java, Go, and other runtimes.

## Features

**Service configuration**

* Set the name, display name, description, startup type, process priority, CPU affinity, working directory, parameters, environment variables, and dependencies of each service.
* Use environment variables in parameters, process paths, and startup directories.
* Run services as Local System, a local or domain account, or a group managed service account (gMSA).
* Run pre-launch, post-launch, pre-stop, and post-stop hooks, with retries, timeouts, and failure handling.

**Reliability**

* Restart the process automatically when it crashes, hangs, or stops, based on health checks.
* Send heartbeat pings to an external URL such as [healthchecks.io](https://healthchecks.io/).
* Stop processes cleanly: `Ctrl+C` for console apps, a close request for GUI apps, `Ctrl+C` propagation to child processes, and force termination when a process does not respond.
* Terminate the whole process tree on stop so that no orphaned processes remain.
* Send notifications on service events through OS notifications, email, WhatsApp, webhooks and other channels.

**Logging and monitoring**

* Capture stdout and stderr to log files, with size-based or date-based rotation.
* View CPU and RAM usage as live graphs.
* View, tail, and search stdout and stderr in real time.
* View service dependencies as a tree with the status of each service.

**Management tools**

* Manage services with the desktop app, the Manager app, the CLI (`servy-cli`), or the PowerShell module (`Servy.psm1`).
* Script deployments and use Servy in CI/CD pipelines.
* Export and import service configurations for backup and migration.

**Security**

* Isolate each service through Windows named pipes, with kernel-level PID validation and DACL access checks.
* Restrict access to vault data, logs, and IPC endpoints with automated ACL hardening.
* Protect sensitive data with DPAPI, HKDF, and AES-256 with HMAC authenticated encryption.

**Supported platforms**

* Modern build (default, self-contained): Windows 10 (1809 or later), Windows 11, and Windows Server 2016 or later, on x64 and ARM64.
* Legacy build (`net48`, requires .NET Framework 4.8): Windows 7 SP1, Windows 8.x, and Windows Server 2008 R2 or later, on x64 only. See the [version comparison](https://github.com/aelassas/servy/wiki/Installation-Guide#version-comparison).

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## Roadmap

See [ROADMAP.md](ROADMAP.md).

## Support & Contributing

Servy is free and open source. If it has helped you, saved you time, or powered your production workflows, consider supporting its development and maintenance.

Maintaining the project, publishing signed releases, and shipping security updates takes real time and resources. You can help keep Servy active, independent, and free for everyone by becoming a sponsor on [GitHub Sponsors](https://github.com/sponsors/aelassas), making a contribution via [PayPal](https://www.paypal.me/aelassaspp), [Liberapay](https://liberapay.com/aelassas/), or [Buy Me a Coffee](https://www.buymeacoffee.com/aelassas), or simply starring and sharing the repository.

If you have suggestions, feature requests, or bug reports, feel free to [open an issue](https://github.com/aelassas/servy/issues) or [submit a pull request](https://github.com/aelassas/servy/pulls).

## Stats for Nerds

[![LoC - Prod](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/loc-prod.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![LoC - Tests](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/loc-tests.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![LoC - Total](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/loc-total.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![GitHub Downloads](https://img.shields.io/github/downloads/aelassas/servy/total)](https://servy-win.github.io/downloads)

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt). See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for third-party software notices and licenses.

## Acknowledgments

Special thanks to [Christophe Rogiers](https://github.com/Christophe-Rogiers), who helped make Servy more robust, more secure, and reliable through his countless contributions, insightful advice, and dedication to code quality. Thanks Christophe for being a key part of the project.

Thanks to [SignPath](https://signpath.io/?utm_source=foundation&utm_medium=github&utm_campaign=servy) for providing a free code signing service, and to the [SignPath Foundation](https://signpath.org/?utm_source=foundation&utm_medium=github&utm_campaign=servy) for supplying a free code signing certificate.

Thanks to [JetBrains](https://www.jetbrains.com/) for providing an [open-source license](https://www.jetbrains.com/community/opensource/) for their tools. Their software made it much easier to profile, debug, and optimize Servy, helping improve its performance and stability. Having access to these professional tools really made a difference during development and saved a lot of time.

Thanks to everyone who tested Servy, reported issues, and suggested improvements on GitHub and Reddit. Your feedback and contributions have shaped the project and made it better with every release.

<p>
  <a href="https://signpath.org/?utm_source=foundation&utm_medium=github&utm_campaign=servy">
    <img alt="SignPath Foundation" src="https://aelassas.github.io/content/signpath.png?v=2" width="54" height="51">
  </a>
  &nbsp;
  <a href="https://www.jetbrains.com/community/opensource/">
    <img alt="JetBrains Open Source" src="https://aelassas.github.io/content/jetbrains.svg?v=3" width="54" height="51">
  </a>
</p>
