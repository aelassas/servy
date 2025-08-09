[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/build.yml) [![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/test.yml) [![](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)

<p align="center">
  <a href="https://servy-win.github.io/">
    <img src="https://servy-win.github.io/servy.png?d=7" width="480">
  </a>
</p>
<p align="center">
  <a href="https://www.youtube.com/watch?v=JpmzZEJd4f0" target="_blank">
    <img src="https://img.shields.io/badge/Watch%20Demo-FF0033?style=for-the-badge&logo=youtube" alt="Watch Demo on YouTube" />
  </a>
</p>

# Servy

## .NET Framework 4.8 Version

**Servy** is a Windows application that allows you to run any executable as a Windows service, using a simple graphical interface. This .NET Framework 4.8 version is designed for compatibility with older Windows operating systems, from Windows 7 SP1 to Windows 11 and Windows Server.

It provides a reliable and compatible solution for automating app startup, monitoring, and background execution across a wide range of Windows versions — from Windows 7 SP1 to Windows 11 and Windows Server.

Servy solves a common limitation of Windows services by allowing you to set a custom working directory. When you create a service with `sc`, the default working directory is always `C:\Windows\System32`, and there's no built-in way to change that. This breaks many applications that rely on relative paths, configuration files, or assets located in their own folders. Servy lets you explicitly set the startup directory so that your application runs in the right environment, just like it would if launched from a shortcut or command prompt.

Servy is ideal for scenarios where you need to keep non-service applications running reliably in the background. It's especially useful for developers, sysadmins, and IT professionals who want to deploy background processes without rewriting them as Windows services. Typical use cases include running Node.js, Python, or .NET console apps as services; keeping web servers, database sync tools, or custom daemons alive after reboots; and automating task runners or batch scripts in production environments. With built-in health checks, restart policies, and a modern UI, Servy is a powerful alternative to tools like NSSM—without the need for manual configuration or registry edits.

## Why Servy?

Windows services are great but have limitations like fixed working directories and no built-in health checks. Servy fills this gap by providing an easy way to run any app as a service with full control over its environment and recovery options — no coding required.

## Requirements

This version is for older systems that require .NET Framework support.
* Supported OS: Windows 7, 8, 10, 11, or Windows Server (x64)
* Requirements: [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net48-web-installer)

**Administrator privileges are required** to install and manage Windows services.

## Quick Links
* [Download Latest Release](https://github.com/aelassas/servy/releases/latest)
* [Documentation](https://github.com/aelassas/servy/wiki)

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

