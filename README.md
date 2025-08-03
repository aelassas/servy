# Servy

**Servy** is a Windows application that allows you to run any executable as a Windows service, using a simple graphical interface.

Built with **WPF (.NET Framework 4.8)**, it provides a reliable and compatible solution for automating app startup, monitoring, and background execution across a wide range of Windows versions — from Windows 7 SP1 to Windows 11.

## Requirements

- Windows 7 SP1 / 8 / 10 / 11 (x64)
- [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net48-web-installer)
- Administrator privileges (required for service installation)

## Download
* [Download Latest Release](https://github.com/aelassas/servy/releases/latest)

## Features

- Run any executable as a Windows service
- Easy-to-use WPF interface
- Supports:
  - Service name & description
  - Startup type (Automatic, Manual, Disabled)
  - Custom working directory & parameters

## How It Works

1. **Install** the application using the provided installer.
2. **Launch Servy** as administrator.
3. Fill in the service details:
   - `Service Name`
   - `Service Description`
   - `Startup Type`
   - `Process Path` (path to the executable you want to run)
   - `Startup Directory` (optional)
   - `Process Parameters` (optional)
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

## Author

**Akram El Assas** [GitHub](https://github.com/aelassas)

## Feedback

If you have suggestions, issues, or want to contribute, feel free to open an issue or pull request.

