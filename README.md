[![Build Status](https://aelassas.visualstudio.com/servy/_apis/build/status%2Faelassas.servy?branchName=main)](https://aelassas.visualstudio.com/servy/_build/latest?definitionId=4&branchName=main) [![](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/badge.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml) 

<p align="center">
  <a href="https://servy-win.github.io/">
    <img src="https://servy-win.github.io/servy.png?d=5">
  </a>
</p>

# Servy

**Servy** is a Windows application that allows you to run any executable as a Windows service, using a simple graphical interface.

It provides a reliable and compatible solution for automating app startup, monitoring, and background execution across a wide range of Windows versions — from Windows 7 SP1 to Windows 11 and Windows Server.

Servy solves a common limitation of Windows services by allowing you to set a custom working directory. When you create a service with `sc`, the default working directory is always `C:\Windows\System32`, and there's no built-in way to change that. This breaks many applications that rely on relative paths, configuration files, or assets located in their own folders. Servy lets you explicitly set the startup directory so that your application runs in the right environment, just like it would if launched from a shortcut or command prompt. This ensures your application runs exactly as expected. It is a fully managed, open-source alternative to NSSM, built entirely in C# for simplicity, transparency, and ease of integration.

## Requirements

- Windows 7 SP1 / 8 / 10 / 11 (x64) / Windows Server
- [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net48-web-installer)
- Administrator privileges (required for service installation)

## Download
* [Download Latest Release](https://github.com/aelassas/servy/releases/latest)

## Features

- Run any executable as a Windows service
- Easy-to-use WPF interface
- Supports:
  - Service name & description
  - Startup type (Automatic (default), Manual, Disabled)
  - Process Priority (Idle, Below Normal, Normal (default), Above Normal, High, Real Time (use with caution))
  - Custom working directory & parameters
  - stdout/stderr redirection to files with size-based rotation
  - Improved process management by handling orphaned/zombie processes and ensuring resource cleanup

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
4. Click **Install** to register the service.
5. Start or stop the service directly from Windows Services or any management tool.

## Architecture

- `Servy.exe`: WPF frontend application (.NET Framework 4.8)
- `Servy.Service.exe`: Companion Windows Service used to wrap and run the target process

The WPF app handles user input and creates/manages the Windows service. The background service launches the specified process with the given settings.

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

