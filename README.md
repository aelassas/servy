[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/build.yml) [![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/test.yml) [![coveralls](https://coveralls.io/repos/github/aelassas/servy/badge.svg?branch=main&v=2)](https://coveralls.io/github/aelassas/servy?branch=main) [![](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/badge.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml) [![](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)

<!--
[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/build.yml) 
[![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/test.yml)
[![Build Status](https://aelassas.visualstudio.com/servy/_apis/build/status%2Faelassas.servy?branchName=main)](https://aelassas.visualstudio.com/servy/_build/latest?definitionId=4&branchName=main) 
[![](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/badge.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml) 
[![codecov](https://codecov.io/gh/aelassas/servy/graph/badge.svg?token=26WZX2V4BG)](https://codecov.io/gh/aelassas/servy)
[![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/main?label=coverage)](https://codecov.io/gh/aelassas/servy)
[![coveralls](https://coveralls.io/repos/github/aelassas/servy/badge.svg?branch=main)](https://coveralls.io/github/aelassas/servy?branch=main)
-->

<p align="center">
  <img src="https://servy-win.github.io/servy.png?v=11" alt="Servy" />
</p>
<p align="center">
  <a href="https://www.youtube.com/watch?v=biHq17j4RbI" target="_blank">
    <img src="https://img.shields.io/badge/Watch%20Demo-FF0033?style=for-the-badge&logo=youtube" alt="Watch Demo on YouTube" />
  </a>
</p>

# Servy

Servy lets you run any app as a native Windows service with full control over working directory, startup type, process priority, logging, health checks, environment variables, dependencies, pre-launch scripts and parameters. An open-source alternative to NSSM and FireDaemon.

Servy offers both a GUI and a Command-Line Interface (CLI), allowing you to create, configure, and manage Windows services either interactively or through scripts and CI/CD pipelines. Additionally, it provides a Manager interface for quickly monitoring and managing all installed services.

If you've ever struggled with the limitations of the built-in `sc` tool or found NSSM lacking in features or UI, Servy might be exactly what you need. It solves a common limitation of Windows services by allowing you to set a custom working directory. The built-in `sc` tool only works with applications specifically designed to run as Windows services and always uses `C:\Windows\System32` with no way to change it. This can break apps that depend on relative paths, configuration files, or local assets. Servy lets you run any app as a service and define the startup directory explicitly, ensuring it behaves exactly as if launched from a shortcut or command prompt.

Servy lets you run an optional script or executable before the main service starts. This is useful for preparing configurations, fetching secrets, or performing other setup tasks. If the pre-launch script fails, the service will not start unless you enable Ignore Failure option.

Servy continuously monitors your app, restarting it automatically if it crashes, hangs, or stops. It is perfect for keeping non-service apps running in the background without having to rewrite them as services. Use it to run Node.js, Python, .NET, Java, Go, Rust, PHP, or Ruby applications; keep web servers, background workers, sync tools, or daemons alive after reboots; and automate task runners, schedulers, or scripts in production with built-in health checks, logging, and restart policies.

## Quick Links
* [Download](https://github.com/aelassas/servy/releases/latest)
* [Installation Guide](https://github.com/aelassas/servy/wiki/Installation-Guide)
* [Service Event Notifications](https://github.com/aelassas/servy/wiki/Service-Event-Notifications)
* [Usage](https://github.com/aelassas/servy/wiki/Usage)
* [Servy Manager](https://github.com/aelassas/servy/wiki/Servy-Manager)
* [Servy CLI](https://github.com/aelassas/servy/wiki/Servy-CLI)
* [Export/Import Services](https://github.com/aelassas/servy/wiki/Export-Import-Services)
* [Security](https://github.com/aelassas/servy/wiki/Security)
* [Architecture](https://github.com/aelassas/servy/wiki/Architecture)
* [Building from Source](https://github.com/aelassas/servy/wiki/Building-from-Source)
* [Troubleshooting](https://github.com/aelassas/servy/wiki/Troubleshooting)
* [FAQ](https://github.com/aelassas/servy/wiki/FAQ)

## Features

* Clean, simple UI
* Quickly monitor and manage all installed services with Servy Manager
* CLI for full scripting and automated deployments
* Run any executable as a Windows service
* Set service name, description, startup type, priority, working directory, environment variables, dependencies, and parameters
* Run services as Local System, local user, or domain account
* Redirect stdout/stderr to log files with automatic size-based rotation
* Run pre-launch script execution before starting the service, with retries, timeout, logging and failure handling
* Prevent orphaned/zombie processes with improved lifecycle management and ensuring resource cleanup
* Health checks and automatic service recovery
* Monitor and manage services in real-time
* Browse and search logs by level, date, and keyword for faster troubleshooting from Servy Manager
* Export/Import service configurations
* Compatible with Windows 7–11 x64 and Windows Server editions

## Roadmap

* [x] Windows Service creation via GUI (Servy Configuration App)
* [x] Logging stdout/stderr with size-based rotation
* [x] Service monitoring and heartbeat checks
* [x] Automatic restart on failure
* [x] CLI for full scripting and automated deployments
* [x] Support environment variables for child processes ([#1](https://github.com/aelassas/servy/issues/1))
* [x] Support environment variable expansion ([#6](https://github.com/aelassas/servy/issues/6))
* [x] Support service dependencies
* [x] Add "Log on as" configuration for Windows service
* [x] Add support for pre-launch script execution before starting the service, with retries, timeout, and failure handling
* [x] Service status query command in CLI
* [x] Export/import service configurations
* [x] Add Help, Documentation, and Check for Updates menus
* [ ] Servy Manager App for managing services installed by Servy
  * [x] Persist service configuration and track installed services in SQLite
  * [x] Provide a "shortcut" to open the Servy Configuration App for full edits
  * [x] Start, stop, restart, and uninstall services
  * [x] Display service status and uptime
  * [x] Add search and filter functionality for services
  * [ ] Provide toast and email notifications for service events (failures)
  * [x] Provide a log viewer
  * [ ] Add advanced scheduling and triggers (start service on event, time, or condition)
  * [ ] Support automatic recovery actions beyond simple restart (e.g., run scripts)
  * [x] Support service dependency management (start/stop order)
  * [x] Add bulk service operations (start/stop/restart multiple services at once)
  * [ ] Enable remote management of Servy services on other machines *(long-term)*
  * [ ] Add a health monitoring dashboard *(long-term)*

## Support & Contributing

If this project helped you, saved you time, or inspired you in any way, please consider supporting its future growth and maintenance. You can show your support by starring the repository (it helps increase visibility and shows your appreciation), sharing the project (recommend it to colleagues, communities, or on social media), or making a donation (if you'd like to financially support the development) via [GitHub Sponsors](https://github.com/sponsors/aelassas) (one-time or monthly), [PayPal](https://www.paypal.me/aelassaspp), or [Buy Me a Coffee](https://www.buymeacoffee.com/aelassas). 

Open-source software requires time, effort, and resources to maintain. Your support helps keep this project alive, up-to-date, and accessible to everyone. Every contribution, big or small, makes a difference and motivates continued work on features, bug fixes, and new ideas.

If you have suggestions, issues, or want to contribute, feel free to [open an issue](https://github.com/aelassas/servy/issues) or [submit pull request](https://github.com/aelassas/servy/pulls).

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt).

