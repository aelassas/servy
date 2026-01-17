[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/build.yml) [![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/test.yml) [![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/net48?label=coverage)](https://app.codecov.io/gh/aelassas/servy/tree/net48)
[![](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)

<!--
[![codecov](https://codecov.io/gh/aelassas/servy/branch/net48/graph/badge.svg?token=26WZX2V4BG)](https://app.codecov.io/gh/aelassas/servy/tree/net48)
[![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/net48?label=coverage)](https://app.codecov.io/gh/aelassas/servy/tree/net48)
[![coveralls](https://coveralls.io/repos/github/aelassas/servy/badge.svg?branch=net48)](https://coveralls.io/github/aelassas/servy?branch=net48)

[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/aelassas/servy/pulls)
[![](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)
-->

<p align="center">
  <img src="https://servy-win.github.io/servy-tiny.png?v=2" alt="Servy">
  ⭐ Don't forget to give us a star on GitHub. It costs nothing but means a lot and helps the project grow!
</p>
<p align="center">
  <a href="https://www.youtube.com/watch?v=JpmzZEJd4f0" target="_blank">
    <img src="https://img.shields.io/badge/Watch%20Demo-0C0C0C?style=for-the-badge&logo=youtube" alt="Watch Demo on YouTube" />
  </a>
</p>

# Servy
 
## .NET Framework 4.8 Version

Servy lets you run any app as a native Windows service with full control over the working directory, startup type, process priority, logging, health checks, environment variables, dependencies, pre-launch and post-launch hooks, and parameters. It's designed to be a full-featured alternative to NSSM, WinSW, and FireDaemon Pro.

This .NET Framework 4.8 version is designed for compatibility with older Windows operating systems, from Windows 7 SP1 to Windows 11 and Windows Server.

Servy is digitally signed using a trusted code-signing certificate provided by the SignPath Foundation. This ensures that all Servy executables and installers are verified and secure, giving you peace of mind when using the tool.

Servy offers a desktop app, a CLI, and a PowerShell module that let you create, configure, and manage Windows services interactively or through scripts and CI/CD pipelines. It also includes a Manager app for easily monitoring and managing all installed services in real time.

If you've ever struggled with the built-in `sc.exe` tool or found NSSM lacking in features or UI, Servy might be exactly what you need. It addresses those limitations, including the ability to set a custom working directory so apps behave exactly as if launched from a shortcut or command prompt.

Servy continuously monitors your app, restarting it automatically if it crashes, hangs, or stops. It is perfect for keeping non-service apps running in the background and ensuring they start automatically at system boot, even before logon, without rewriting them as services. Use it to run Node.js, Python, .NET, Java, Go, Rust, PHP, or Ruby applications; keep web servers, background workers, sync tools, or daemons alive after reboots; and automate task runners, schedulers, or scripts in production with built-in health checks, logging, and restart policies.

## Quick Links
* [Download](https://github.com/aelassas/servy/releases/latest)
* [Installation Guide](https://github.com/aelassas/servy/wiki/Installation-Guide)
* [Overview](https://github.com/aelassas/servy/wiki/Overview)
* [Usage](https://github.com/aelassas/servy/wiki/Usage)
* [FAQ](https://github.com/aelassas/servy/wiki/FAQ)
* [Full Documentation](https://github.com/aelassas/servy/wiki)

## Features

When it comes to features, Servy brings together the best parts of tools like NSSM, WinSW, and FireDaemon Pro, all in one easy-to-use package. It combines the simplicity of open-source tools with the flexibility and power you'd expect from professional service managers. Below is a detailed list of all the features Servy supports.

* Clean, simple UI
* Monitor and manage all installed services with Servy Manager
* Real-time CPU and RAM monitoring with live performance graphs for installed services
* CLI and PowerShell module for full scripting and automated deployments
* Run any executable as a Windows service
* Set service name, description, startup type, priority, working directory, environment variables, dependencies, and parameters
* Environment variable expansion supported in both environment variables and process parameters
* Run services as Local System, local or domain accounts, Active Directory accounts, or gMSAs
* Redirect stdout/stderr to log files with automatic size-based and date-based rotations
* Run pre-launch hook before starting the service, with retries, timeout, logging and failure handling
* Run post-launch hook after the application starts successfully
* Supports `Ctrl+C` for command-line apps, close-window for GUI apps, and force kill if unresponsive
* Prevent orphaned/zombie processes with improved lifecycle management and ensuring resource cleanup
* Health checks and automatic service recovery
* Browse and search logs by level, date, and keyword for faster troubleshooting from Servy Manager
* Export/Import service configurations for easy backups and automation
* Service Event Notification alerts on service failures via Windows notifications and email
* Compatible with Windows 7-11 x64 and Windows Server editions

## Support & Contributing

If this project helped you, saved you time, or inspired you in any way, please consider supporting its future growth and maintenance. You can show your support by starring the repository to show your appreciation and increase visibility, sharing the project with colleagues, communities, or on social media, or by making a donation. Your contributions help keep Servy alive, improving, and accessible to everyone. You can donate through [GitHub Sponsors](https://github.com/sponsors/aelassas) (one-time or monthly), [PayPal](https://www.paypal.me/aelassaspp), or [Buy Me a Coffee](https://www.buymeacoffee.com/aelassas).

Open-source software requires time, effort, and resources to maintain. Every contribution, big or small, makes a difference and motivates continued work on features, bug fixes, and new ideas.

If you have suggestions, issues, or would like to contribute, feel free to [open an issue](https://github.com/aelassas/servy/issues) or [submit a pull request](https://github.com/aelassas/servy/pulls).

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt).
