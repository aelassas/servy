## Why Servy?

When I discovered NSSM years ago, I liked the concept and found it very interesting. One day, I decided to write a simple tool to do the same thing, and that's how Servy was born. I decided to focus on simplicity and security, and put it on GitHub so others could contribute code, report bugs, and suggest improvements.

NSSM is a lightweight tool that runs programs as Windows services, but it has not been updated in over a decade. It struggles to reliably stop complex process trees, often leaving orphaned child processes. Additionally, it lacks critical modern features such as pre and post lifecycle execution scripts, date-based log rotation, real-time CPU and memory monitoring, CPU affinity, email notifications, heartbeat ping health checks, and advanced failure recovery options.

Servy adds these features. It is intended as a successor to the discontinued NSSM, with a focus on ease of use and security, and I plan to keep maintaining it providing bug fixes, security updates, and new features.

Servy is open source. Anyone can contribute code, report bugs, or suggest new features.

### NSSM and security

NSSM is not a secure choice for production environments:

* NSSM stores its configurations in the Windows Registry under `HKLM\SYSTEM\CurrentControlSet\Services\<ServiceName>\Parameters`.
* Configuration settings, including executable paths, command-line parameters, arguments, and environment variables, **are saved as plain, readable text**.
* Any user can read these Registry entries, meaning sensitive data such as database passwords, API keys, and connection strings are exposed to anyone with access to the system.
* NSSM does not isolate service permissions, leaving log files and parameters completely unrestricted across local accounts.
* NSSM receives no security updates, leaving long-standing vulnerabilities unpatched.

Servy protects service configurations, sensitive data, and runtime states with a zero-trust model:

* All configurations and sensitive data are stored in a secure vault protected by DPAPI, HKDF key derivation, and AES-256 HMAC encryption.
* Servy automatically enforces strict Access Control Lists (ACLs) on stored configurations, log files, and IPC communication channels to block unauthorized access.
* Each service context is fully isolated, and Servy authenticates the identity of every process requesting configuration data.

In other words, Servy ensures that sensitive information is never exposed to unauthorized users and that each service operates within a secure, isolated environment.

See the [Security Model](https://github.com/aelassas/servy/wiki/Security) for details.

## Points of interest

### Architecture

Servy is made of a few separate parts that work together:

* **Desktop app:** a window where you create and configure services.
* **Manager app:** a window where you monitor and control the services that Servy manages, including their status, resource usage, and logs.
* **Command-line tool and PowerShell module:** text-based tools for scripts and automated deployments.
* **Background service:** a service named `Servy` that starts with Windows. It is the only part that reads the stored settings of all services and the encryption key.
* **Service runner:** a small program that Windows starts for each service. It launches your program, watches it, restarts it when needed, and writes its logs.
* **Restart helper:** a small tool that restarts a service safely from outside the service itself.

Settings are stored in a local database on the computer. When a service starts, its runner asks the background service for its own settings and receives nothing else. The background service answers only after checking who is asking. Every service that Servy creates depends on the background service, so Windows always starts it first. The desktop app, the Manager app, and the command-line tool install and start it automatically when needed.

This design means that a service running under its own Windows account never opens the settings database or the encryption key directly. It also keeps the parts of Servy that deal with the screen, the settings, and the running services independent of each other, which makes Servy easier to test and to maintain.

Servy is available in two builds:

* A modern build for Windows 10, Windows 11, and Windows Server 2016 or later. It includes everything it needs and does not require a separate installation of .NET.
* A legacy build for older systems, from Windows 7 SP1 and Windows Server 2008 R2. It requires .NET Framework 4.8.

See the [Architecture](https://github.com/aelassas/servy/wiki/Architecture) page for more details.

### Security

Starting from v10.2, Servy applies the following protections automatically, without any manual step:

* **Separation between services:** a service can access only its own settings and its own log folder. It cannot read the settings of other services, the encryption key, or the logs of services that run under other accounts.
* **Encrypted settings:** passwords, environment variables, and program arguments are stored in encrypted form. The encryption key is protected by Windows and tied to the computer, so copying the files to another computer does not expose them.
* **Restricted folders:** only administrators and the system can open Servy's data folder. Each service account receives only the access that its own services need, and Servy removes that access when the service is removed.
* **Protected communication channel:** the background service communicates with the service runners through a local channel that only the accounts of managed services can use. It cannot be reached from another computer. Each side also verifies the identity of the other, so a rogue program cannot impersonate either one.
* **Safe handling of secrets:** passwords and other sensitive values can be provided through environment variables or configuration files instead of typed on the command line, where other programs on the computer could read them.
* **Safe imports:** configuration files can be imported only from local locations. Network shares, shortcuts that redirect elsewhere, and protected system folders are rejected.
* **Trusted releases:** all programs and installers are digitally signed, each release includes a list of all the components it contains, dependencies are monitored for known vulnerabilities, and release files are scanned for malware.

See the [Security](https://github.com/aelassas/servy/wiki/Security) page for more details.

### Working with Windows internals

Servy talks directly to Windows for tasks such as starting and stopping programs, installing services, checking their state, and setting permissions. Working at this level required a detailed understanding of how Windows starts, monitors, and stops services.

### Stopping a service cleanly

The hardest bug to fix was related to stopping a service. When Servy asked a program to stop by sending it the same signal as pressing `Ctrl+C`, the program's output was lost, and the service could no longer receive messages from it. Finding the cause required careful debugging and a closer look at how Windows handles stop signals and communication between programs. The fix made service shutdown more reliable.

### Community feedback

Publishing Servy on GitHub and sharing it on Reddit brought bug reports, feature requests, and feedback from real usage. Most bugs were easy to reproduce and fix. Several features came directly from user requests, such as the use of environment variables in settings.

The GitHub contributors who helped design the security model in Servy v7.9 and later deserve special thanks. See the [Security Model](https://github.com/aelassas/servy/wiki/Security) for details.

### Automated releases

I use scripts to automate building, testing, packaging, and publishing new versions.

GitHub Actions runs these scripts when a new release is published. The automated process publishes the new version to the WinGet, Chocolatey, and Scoop package managers, and then increases the version number for the next release. Setting this up took several attempts, but releases now require almost no manual work.

### Code signing

Programs and installers are digitally signed through SignPath and GitHub Actions. This required building a dedicated automated process. A signature allows users to verify who published a release and that it has not been changed. The automated processes are available here:

* [Modern build](https://github.com/aelassas/servy/blob/main/.github/workflows/publish.yml)
* [Legacy build](https://github.com/aelassas/servy/blob/net48/.github/workflows/publish.yml)

See the [Acknowledgments](README.md#acknowledgments) in the README.

Feedback and contributions are welcome.
