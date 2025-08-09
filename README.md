[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/build.yml) [![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/test.yml) [![](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)

<p align="center">
  <img src="https://servy-win.github.io/servy.png?d=7" width="480" alt="Servy" />
</p>
<p align="center">
  <a href="https://www.youtube.com/watch?v=JpmzZEJd4f0" target="_blank">
    <img src="https://img.shields.io/badge/Watch%20Demo-FF0033?style=for-the-badge&logo=youtube" alt="Watch Demo on YouTube" />
  </a>
</p>

# Servy

## .NET Framework 4.8 Version

Servy lets you run any app as a Windows service with full control over working directory, startup type, logging, health checks, and parameters. A fully managed alternative to NSSM.

This .NET Framework 4.8 version is designed for compatibility with older Windows operating systems, from Windows 7 SP1 to Windows 11 and Windows Server.

Servy solves a common limitation of Windows services by allowing you to set a custom working directory. The built-in `sc` tool only works with applications specifically designed to run as Windows services and always uses `C:\Windows\System32` with no way to change it. This can break apps that depend on relative paths, configuration files, or local assets. Servy lets you run any app as a service and define the startup directory explicitly, ensuring it behaves exactly as if launched from a shortcut or command prompt.

Servy is perfect for keeping non-service apps running in the background without rewriting them as services. Use it to run Node.js, Python, or .NET apps; keep web servers, sync tools, or daemons alive after reboots; and automate task runners or scripts in production with built-in health checks, restart policies, and a modern UI.

## Requirements

This version is for older systems that require .NET Framework support.
* Supported OS: Windows 7, 8, 10, 11, or Windows Server (x64)
* Requires [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net48-web-installer)

**Administrator privileges are required** to install and manage Windows services.

## Quick Links
* [Download](https://github.com/aelassas/servy/releases/latest)
* [Installation Guide](https://github.com/aelassas/servy/wiki/Installation-Guide)
* [Usage](https://github.com/aelassas/servy/wiki/Usage)
* [Architecture](https://github.com/aelassas/servy/wiki/Architecture)
* [Troubleshooting](https://github.com/aelassas/servy/wiki/Troubleshooting)
* [FAQ](https://github.com/aelassas/servy/wiki/FAQ)

## Features

* Clean, simple UI
* Run any executable as a Windows service
* Set service name, description, startup type, priority, working directory, and parameters
* Redirect stdout/stderr to log files with automatic size-based rotation
* Prevent orphaned/zombie processes with improved lifecycle management and ensuring resource cleanup
* Health checks and automatic service recovery
* Monitor and manage services in real-time
* Compatible with Windows 7–11 x64 and Windows Server editions


## How It Works

1. **Install** the application using the provided installer.
2. **Launch Servy** as administrator.
3. Fill in the service details:
   - `Service Name` (required)
   - `Service Description` (optional)
   - `Startup Type` (optional)
   - `Process Path` (required - path to the executable you want to run)
   - `Startup Directory` (optional - Working directory for the process. Defaults to the executable's directory if not specified.)
   - `Process Parameters` (optional)
   - `Process Priority` (optional)
   - `Stdout File Path` (optional)
   - `Stderr File Path` (optional)
   - `Rotation Size` (optional - in bytes, minimum value is 1 MB (1,048,576 bytes), default value is 10MB)
   - `Heartbeat Interval` (optional - Interval between health checks of the child process, default value is 30 seconds)
   - `Max Failed Checks` (optional - Number of consecutive failed health checks before triggering the recovery action, default value is 3 attempts)
   - `Recovery Action` (optional - Action to take when the max failed checks is reached. Options: Restart Service, Restart Process, Restart Computer, None)
   - `Max Restart Attempts` (optional - Maximum number of recovery attempts (whether restarting the service or process) before stopping further recovery, default value is 3 attempts)
4. Click **Install** to register the service.
5. Start or stop the service directly from Service Control Manager `services.msc` or any management tool.

## Architecture

- `Servy.exe`: WPF frontend application using the MVVM design pattern <br>
  Handles user input, service configuration, and manages the lifecycle of the Windows service.

- `Servy.Service.exe`: Windows Service that runs in the background <br>
  Responsible for launching and monitoring the target process based on the configured settings (e.g., heartbeat, recovery actions).

- `Servy.Restarter.exe`: Lightweight utility used to restart a Windows service <br>
  Invoked as part of the *Restart Service* recovery action when a failure is detected.

Together, these components provide a complete solution for wrapping any executable as a monitored Windows service with optional health checks and automatic recovery behavior.

You can find detailed architecture overview in the [wiki](https://github.com/aelassas/servy/wiki/Architecture).

## Support & Contributing

If this project helped you, saved you time, or inspired you in any way, please consider supporting its future growth and maintenance. You can show your support by starring the repository (it helps increase visibility and shows your appreciation), sharing the project (recommend it to colleagues, communities, or on social media), or making a donation (if you'd like to financially support the development) via [GitHub Sponsors](https://github.com/sponsors/aelassas) (one-time or monthly), [PayPal](https://www.paypal.me/aelassaspp), or [Buy Me a Coffee](https://www.buymeacoffee.com/aelassas). Open-source software requires time, effort, and resources to maintain—your support helps keep this project alive, up-to-date, and accessible to everyone. Every contribution, big or small, makes a difference and motivates continued work on features, bug fixes, and new ideas.

If you have suggestions, issues, or want to contribute, feel free to [open an issue](https://github.com/aelassas/servy/issues) or [submit pull request](https://github.com/aelassas/servy/pulls).

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt).

