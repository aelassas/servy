[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/build.yml) [![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/test.yml) [![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/net48?label=coverage)](https://app.codecov.io/gh/aelassas/servy/tree/net48)
<!--
[![codecov](https://codecov.io/gh/aelassas/servy/branch/net48/graph/badge.svg?token=26WZX2V4BG)](https://app.codecov.io/gh/aelassas/servy/tree/net48)
[![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/net48?label=coverage)](https://app.codecov.io/gh/aelassas/servy/tree/net48)
[![coveralls](https://coveralls.io/repos/github/aelassas/servy/badge.svg?branch=net48)](https://coveralls.io/github/aelassas/servy?branch=net48)

[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/aelassas/servy/pulls)
-->

<p align="center">
  <img src="https://servy-win.github.io/servy.png?v=13" alt="Servy" />
</p>
<p align="center">
  <a href="https://www.youtube.com/watch?v=JpmzZEJd4f0" target="_blank">
    <img src="https://img.shields.io/badge/Watch%20Demo-FF0033?style=for-the-badge&logo=youtube" alt="Watch Demo on YouTube" />
  </a>
</p>

# Servy
[![](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki) [![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/aelassas/servy/pulls)
 
## .NET Framework 4.8 Version

Servy lets you run any app as a native Windows service with full control over working directory, startup type, process priority, logging, health checks, environment variables, dependencies, pre-launch scripts and parameters. A modern open-source alternative to NSSM, WinSW and FireDaemon.

This .NET Framework 4.8 version is designed for compatibility with older Windows operating systems, from Windows 7 SP1 to Windows 11 and Windows Server.

Servy offers a GUI, a CLI, and a PowerShell module, allowing you to create, configure, and manage Windows services either interactively or through scripts and CI/CD pipelines. Additionally, it provides a Manager interface for quickly monitoring and managing all installed services in real-time.

If you've ever struggled with the limitations of the built-in `sc` tool or found NSSM lacking in features or UI, Servy might be exactly what you need. It solves a common limitation of Windows services by allowing you to set a custom working directory. The built-in `sc` tool only works with applications specifically designed to run as Windows services and always uses `C:\Windows\System32` with no way to change it. This can break apps that depend on relative paths, configuration files, or local assets. Servy lets you run any app as a service and define the startup directory explicitly, ensuring it behaves exactly as if launched from a shortcut or command prompt.

Servy lets you run an optional script or executable before the main service starts. This is useful for preparing configurations, fetching secrets, or performing other setup tasks.

Servy continuously monitors your app, restarting it automatically if it crashes, hangs, or stops. It is perfect for keeping non-service apps running in the background without having to rewrite them as services. Use it to run Node.js, Python, .NET, Java, Go, Rust, PHP, or Ruby applications; keep web servers, background workers, sync tools, or daemons alive after reboots; and automate task runners, schedulers, or scripts in production with built-in health checks, logging, and restart policies.

## Quick Links
* [Download](https://github.com/aelassas/servy/releases/latest)
* [Overview](https://github.com/aelassas/servy/wiki/Overview)
* [Installation Guide](https://github.com/aelassas/servy/wiki/Installation-Guide)
* [Usage](https://github.com/aelassas/servy/wiki/Usage)
* [Servy Manager](https://github.com/aelassas/servy/wiki/Servy-Manager)
* [Servy CLI](https://github.com/aelassas/servy/wiki/Servy-CLI)
* [Servy PowerShell Module](https://github.com/aelassas/servy/wiki/Servy-PowerShell-Module)
* [Servy Automation & CI/CD](https://github.com/aelassas/servy/wiki/Servy-Automation-&-CI-CD)
* [Integration with Monitoring Tools](https://github.com/aelassas/servy/wiki/Integration-with-Monitoring-Tools)
* [Service Event Notifications](https://github.com/aelassas/servy/wiki/Service-Event-Notifications)
* [Export/Import Services](https://github.com/aelassas/servy/wiki/Export-Import-Services)
* [Pre‐Launch & Post‐Launch Actions](https://github.com/aelassas/servy/wiki/Pre‐Launch-&-Post‐Launch-Actions)
* [Service Dependencies](https://github.com/aelassas/servy/wiki/Service-Dependencies)
* [Monitoring & Health Checks](https://github.com/aelassas/servy/wiki/Monitoring-&-Health-Checks)
* [Logging & Log Rotation](https://github.com/aelassas/servy/wiki/Logging-&-Log-Rotation)
* [Examples & Recipes](https://github.com/aelassas/servy/wiki/Examples-&-Recipes)
* [Comparison with Alternatives](https://github.com/aelassas/servy/wiki/Comparison-with-Alternatives)
* [Servy vs FireDaemon](https://github.com/aelassas/servy/wiki/Servy-vs-FireDaemon)
* [Security](https://github.com/aelassas/servy/wiki/Security)
* [Architecture](https://github.com/aelassas/servy/wiki/Architecture)
* [Building from Source](https://github.com/aelassas/servy/wiki/Building-from-Source)
* [Troubleshooting](https://github.com/aelassas/servy/wiki/Troubleshooting)
* [FAQ](https://github.com/aelassas/servy/wiki/FAQ)

## Features

* Clean, simple UI
* Quickly monitor and manage all installed services in real-time with Servy Manager
* CLI and PowerShell module for full scripting and automated deployments
* Run any executable as a Windows service
* Set service name, description, startup type, priority, working directory, environment variables, dependencies, and parameters
* Environment variable expansion supported in both environment variables and process parameters
* Run services as Local System, local user, domain account, or DOMAIN\gMSA$ for Group Managed Service Accounts
* Redirect stdout/stderr to log files with automatic size-based rotation
* Run pre-launch script execution before starting the service, with retries, timeout, logging and failure handling
* Run post-launch script execution automatically after the application starts successfully
* Prevent orphaned/zombie processes with improved lifecycle management and ensuring resource cleanup
* Health checks and automatic service recovery
* Browse and search logs by level, date, and keyword for faster troubleshooting from Servy Manager
* Export/Import service configurations
* Service Event Notification alerts on service failures via Windows notifications and email
* Compatible with Windows 7–11 x64 and Windows Server editions

## Support & Contributing

If this project helped you, saved you time, or inspired you in any way, please consider supporting its future growth and maintenance. You can show your support by starring the repository to show your appreciation and increase visibility, sharing the project with colleagues, communities, or on social media, or by making a donation. Your contributions help keep Servy alive, improving, and accessible to everyone. You can donate through [GitHub Sponsors](https://github.com/sponsors/aelassas) (one-time or monthly), [PayPal](https://www.paypal.me/aelassaspp), or [Buy Me a Coffee](https://www.buymeacoffee.com/aelassas).

Open-source software requires time, effort, and resources to maintain. Every contribution, big or small, makes a difference and motivates continued work on features, bug fixes, and new ideas.

If you have suggestions, issues, or would like to contribute, feel free to [open an issue](https://github.com/aelassas/servy/issues) or [submit a pull request](https://github.com/aelassas/servy/pulls).

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt).

