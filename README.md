[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/build.yml) 
[![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/test.yml) 
[![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/net48?label=coverage)](https://app.codecov.io/gh/aelassas/servy/tree/net48)
[![](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)

# Servy
 
## .NET Framework 4.8 Version

Servy lets you run any app as a native Windows service with full control over the working directory, startup type, process priority, CPU affinity, logging, health checks, environment variables, dependencies, pre-launch and post-launch hooks, pre-stop and post-stop hooks, and parameters. It's designed to be a full-featured alternative to NSSM, WinSW, and FireDaemon Pro.

This .NET Framework 4.8 version is designed for compatibility with older Windows operating systems, from Windows 7 SP1 to Windows 11 and Windows Server.

Servy is digitally signed using a trusted code-signing certificate provided by the SignPath Foundation. This ensures that all Servy executables and installers are verified and secure, giving you peace of mind when using the tool.

Servy offers a desktop app, a CLI, and a PowerShell module that let you create, configure, and manage Windows services interactively or through scripts and CI/CD pipelines. It also includes a Manager app for easily monitoring and managing all installed services in real time.

Servy continuously monitors your app, restarting it automatically if it crashes, hangs, or stops. It is perfect for keeping non-service apps running in the background and ensuring they start automatically at system boot, even before logon, without rewriting them as services. Use it to run Node.js, Python, .NET, Java, Go, Rust, PHP, or Ruby applications; keep web servers, background workers, sync tools, or daemons alive after reboots; and automate task runners, schedulers, or scripts in production with built-in health checks, logging, and restart policies.

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
* Compatible with Windows 7 SP1 through 11 (x64/ARM64) and Windows Server editions

## Support & Contributing

Servy is free and open-source. If you are using it in a commercial or revenue-generating context, or simply find it valuable, consider supporting the project via [GitHub Sponsors](https://github.com/sponsors/aelassas), [PayPal](https://www.paypal.me/aelassaspp), or [Buy Me a Coffee](https://www.buymeacoffee.com/aelassas).

Open-source software requires time, effort, and resources to maintain. Every contribution, big or small, makes a difference and motivates continued work on features, bug fixes, and new ideas.

If you have suggestions, issues, or would like to contribute, feel free to [open an issue](https://github.com/aelassas/servy/issues) or [submit a pull request](https://github.com/aelassas/servy/pulls).

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt). See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for third-party software notices and licenses.
