[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/build.yml)
[![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/test.yml)
[![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/main?label=coverage)](https://codecov.io/gh/aelassas/servy)
[![release](https://github.com/aelassas/servy/actions/workflows/release.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/release.yml)


<!--
[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/build.yml) 
[![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/test.yml)
[![Build Status](https://aelassas.visualstudio.com/servy/_apis/build/status%2Faelassas.servy?branchName=main)](https://aelassas.visualstudio.com/servy/_build/latest?definitionId=4&branchName=main) 
[![](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/badge.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml) 
[![codecov](https://codecov.io/gh/aelassas/servy/graph/badge.svg?token=26WZX2V4BG)](https://codecov.io/gh/aelassas/servy)
[![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/main?label=coverage)](https://codecov.io/gh/aelassas/servy)
[![coveralls](https://coveralls.io/repos/github/aelassas/servy/badge.svg?branch=main)](https://coveralls.io/github/aelassas/servy?branch=main)

[![scoop](https://github.com/aelassas/servy/actions/workflows/scoop.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/scoop.yml)
[![](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)

[![sonar](https://img.shields.io/sonar/quality_gate/aelassas_servy?server=https%3A%2F%2Fsonarcloud.io&label=sonar)](https://sonarcloud.io/summary/new_code?id=aelassas_servy)
[![winget](https://github.com/aelassas/servy/actions/workflows/winget.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/winget.yml)
[![choco](https://github.com/aelassas/servy/actions/workflows/choco.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/choco.yml)
[![bump-version](https://github.com/aelassas/servy/actions/workflows/bump-version.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/bump-version.yml)
[![release](https://github.com/aelassas/servy/actions/workflows/release.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/release.yml)

[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/aelassas/servy/total)](https://github.com/aelassas/servy/releases)
[![GitHub Release](https://img.shields.io/github/v/release/aelassas/servy)](https://github.com/aelassas/servy/releases/latest)
[![License](https://img.shields.io/github/license/aelassas/servy)](https://github.com/aelassas/servy/blob/main/LICENSE.txt)

[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/aelassas/servy/pulls)
-->


<p align="center">
  <img src="https://servy-win.github.io/servy-tiny.png?v=1" alt="Servy">
  ⭐ Don't forget to give us a star on GitHub. It costs nothing but means a lot and helps the project grow!
</p>
<p align="center">
  <a href="https://www.youtube.com/watch?v=biHq17j4RbI" target="_blank">
    <img src="https://img.shields.io/badge/Watch%20Demo-0C0C0C?style=for-the-badge&logo=youtube" alt="Watch Demo on YouTube">
  </a>
</p>

# Servy
<!--
[![](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/badge.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml) [![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/aelassas/servy/total)](https://github.com/aelassas/servy/releases)
-->

<!--
[![](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/badge.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml) [![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/aelassas/servy/total)](https://github.com/aelassas/servy/releases) [![](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki) [![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/aelassas/servy/pulls)
-->

Servy lets you run any app as a native Windows service with full control over the working directory, startup type, process priority, logging, health checks, environment variables, dependencies, pre-launch and post-launch hooks, and parameters. It's designed to be a full-featured alternative to NSSM, WinSW, and FireDaemon Pro.

Servy offers a desktop app, a CLI, and a PowerShell module that let you create, configure, and manage Windows services interactively or through scripts and CI/CD pipelines. It also includes a Manager app for easily monitoring and managing all installed services in real time.

If you've ever struggled with the built-in `sc.exe` tool or found NSSM lacking in features or UI, Servy might be exactly what you need. It addresses those limitations, including the ability to set a custom working directory so apps behave exactly as if launched from a shortcut or command prompt.

Servy continuously monitors your app, restarting it automatically if it crashes, hangs, or stops. It is perfect for keeping non-service apps running in the background and ensuring they start automatically at system boot, even before logon, without rewriting them as services. Use it to run Node.js, Python, .NET, Java, Go, Rust, PHP, or Ruby applications; keep web servers, background workers, sync tools, or daemons alive after reboots; and automate task runners, schedulers, or scripts in production with built-in health checks, logging, and restart policies.

## Why?

Curious about the philosophy behind Servy? Read the [project notes](NOTES.md).

## Getting Started
You have two options to install Servy. Download and [install manually](https://github.com/aelassas/servy/wiki/Installation-Guide#manual-download-and-install) or use a package manager such as WinGet, Chocolatey, or Scoop.

Make sure you have [WinGet](https://learn.microsoft.com/en-us/windows/package-manager/winget/), [Chocolatey](https://chocolatey.org/install), or [Scoop](https://scoop.sh/) installed.

Run one of the following commands as administrator from Command Prompt or PowerShell:

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

<!--
> Servy has been reviewed by Microsoft Security Intelligence and is confirmed safe. It performs only standard installation tasks and does not contain malware, adware, or unwanted software. Servy passes VirusTotal scans and is published in the Windows Package Manager (WinGet), Chocolatey, and Scoop. You can safely install it from GitHub, WinGet, Chocolatey, or Scoop.
-->

## Quick Links
* [Download](https://github.com/aelassas/servy/releases/latest)
* [Overview](https://github.com/aelassas/servy/wiki/Overview)
* [Installation Guide](https://github.com/aelassas/servy/wiki/Installation-Guide)
* [Usage](https://github.com/aelassas/servy/wiki/Usage)
* [Servy Manager](https://github.com/aelassas/servy/wiki/Servy-Manager)
* [Servy CLI](https://github.com/aelassas/servy/wiki/Servy-CLI)
* [Examples & Recipes](https://github.com/aelassas/servy/wiki/Examples-&-Recipes)
* [FAQ](https://github.com/aelassas/servy/wiki/FAQ)
* [Full Documentation](https://github.com/aelassas/servy/wiki)

## Features

When it comes to features, Servy brings together the best parts of tools like NSSM, WinSW, and FireDaemon Pro, all in one easy-to-use package. It combines the simplicity of open-source tools with the flexibility and power you'd expect from professional service managers. Below is a detailed list of all the features Servy supports.

* Clean, simple UI
* Monitor and manage all installed services with Servy Manager
* Real-time CPU and RAM usage tracking for installed services
* CLI and PowerShell module for full scripting and automated deployments
* Run any executable as a Windows service
* Set service name, description, startup type, priority, working directory, environment variables, dependencies, and parameters
* Environment variable expansion supported in both environment variables and process parameters
* Run services as Local System, local or domain accounts, Active Directory accounts, or gMSAs
* Redirect stdout/stderr to log files with automatic size-based rotation
* Run pre-launch hook before starting the service, with retries, timeout, logging and failure handling
* Run post-launch hook after the application starts successfully
* Supports `Ctrl+C` for command-line apps, close-window for GUI apps, and force kill if unresponsive
* Prevent orphaned/zombie processes with improved lifecycle management and ensuring resource cleanup
* Health checks and automatic service recovery
* Browse and search logs by level, date, and keyword for faster troubleshooting from Servy Manager
* Export/Import service configurations
* Service Event Notification alerts on service failures via Windows notifications and email
* Compatible with Windows 7-11 x64 and Windows Server editions

## Roadmap

See the [project roadmap](ROADMAP.md).

## Support & Contributing

If this project helped you, saved you time, or inspired you in any way, please consider supporting its future growth and maintenance. You can show your support by starring the repository to show your appreciation and increase visibility, sharing the project with colleagues, communities, or on social media, or by making a donation. Your contributions help keep Servy alive, improving, and accessible to everyone. You can donate through [GitHub Sponsors](https://github.com/sponsors/aelassas) (one-time or monthly), [PayPal](https://www.paypal.me/aelassaspp), or [Buy Me a Coffee](https://www.buymeacoffee.com/aelassas).

Open-source software requires time, effort, and resources to maintain. Every contribution, big or small, makes a difference and motivates continued work on features, bug fixes, and new ideas.

If you have suggestions, issues, or would like to contribute, feel free to [open an issue](https://github.com/aelassas/servy/issues) or [submit a pull request](https://github.com/aelassas/servy/pulls).

## Stats for Nerds

[![Lines Of Code](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/badge.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/aelassas/servy/total)](https://servy-win.github.io/stats)

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt).

## Acknowledgments

A huge thanks to [JetBrains](https://www.jetbrains.com/) for providing an [open-source license](https://www.jetbrains.com/community/opensource/) for their tools. Their software made it much easier to profile, debug, and optimize Servy, helping improve its performance and stability. Having access to these professional tools really made a difference during development and saved a lot of time.

<a href="https://www.jetbrains.com/">
  <img alt="JetBrains" src="https://aelassas.github.io/content/jetbrains.svg?v=3" width="52" height="52">
</a>


I'd also like to thank everyone who tested Servy, reported issues, and suggested improvements on GitHub and Reddit. Your feedback and contributions helped shape the project and made it better with every release.

