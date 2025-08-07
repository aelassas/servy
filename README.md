[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/build.yml) [![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/test.yml)

<p align="center">
  <a href="https://servy-win.github.io/">
    <img src="https://servy-win.github.io/servy.png?d=7" width="480">
  </a>
</p>

# Servy

## .NET Framework 4.8 Version

**Servy** is a Windows application that allows you to run any executable as a Windows service, using a simple graphical interface. This .NET Framework 4.8 version is designed for compatibility with older Windows operating systems, from Windows 7 SP1 to Windows 11 and Windows Server.

It provides a reliable and compatible solution for automating app startup, monitoring, and background execution across a wide range of Windows versions — from Windows 7 SP1 to Windows 11 and Windows Server.

Servy solves a common limitation of Windows services by allowing you to set a custom working directory. When you create a service with `sc`, the default working directory is always `C:\Windows\System32`, and there's no built-in way to change that. This breaks many applications that rely on relative paths, configuration files, or assets located in their own folders. Servy lets you explicitly set the startup directory so that your application runs in the right environment, just like it would if launched from a shortcut or command prompt. This ensures your application runs exactly as expected. It is a fully managed, open-source alternative to NSSM, built entirely in C# for simplicity, transparency, and ease of integration.

## Requirements

This version is for older systems that require .NET Framework support.
* Windows 7, 8, 10, 11, or Windows Server (x64)
* [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net48-web-installer)

**Administrator privileges are required** to install and manage Windows services.

## Download
* [Download Latest Release](https://github.com/aelassas/servy/releases/latest)

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
5. Start or stop the service directly from Windows Services (services.msc) or any management tool.

## Architecture

- `Servy.exe`: WPF frontend application
  Handles user input, service configuration, and manages the lifecycle of the Windows service.

- `Servy.Service.exe`: Windows Service that runs in the background  
  Responsible for launching and monitoring the target process based on the configured settings (e.g., heartbeat, recovery actions).

- `Servy.Restarter.exe`: Lightweight utility used to restart a Windows service
  Invoked as part of the *Restart Service* recovery action when a failure is detected.

Together, these components provide a complete solution for wrapping any executable as a monitored Windows service with optional health checks and automatic recovery behavior.

## Installation

Servy ships with an installer created using [Inno Setup](https://jrsoftware.org/isinfo.php).

The app will request elevation (UAC prompt) to install and manage services.

## Permissions

Servy requires:
- Administrator privileges to create, start, or stop services
- Access to write in the installation folder and service registry entries

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt).

## Feedback

If you have suggestions, issues, or want to contribute, feel free to open an issue or pull request.


