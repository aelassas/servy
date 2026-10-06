## Why Servy?

I used NSSM for a long time and kept running into the same limitations, so I built Servy to replace it.

NSSM is a lightweight tool that runs ordinary programs as Windows services. It has not been updated in over a decade. It does not reliably stop a program together with all the other programs that it started, and it lacks several features I needed:

* Running scripts before and after a program starts or stops
* Starting a new log file by date
* Monitoring how much processor and memory a program uses
* Choosing which processor cores a program may use
* Email notifications
* Heartbeat ping URLs, which let an external monitoring service confirm that a program is still running
* Advanced options for recovering from failures

Servy adds these features. It is intended as a successor to the discontinued NSSM, with a focus on ease of use and security, and I plan to keep maintaining it.

Servy is open source. Anyone can contribute code, report bugs, or suggest new features.

### NSSM and security

NSSM is also not a secure choice for production use:

* NSSM keeps its settings in the Windows registry, the database where Windows stores its configuration. Settings such as program arguments and environment variables are stored as plain, readable text.
* By default, regular users of the computer can read these settings. A password or key passed to a program through them is therefore visible to every local user.
* NSSM does not keep services separate from each other, and it does not restrict who can read its logs and settings.
* NSSM receives no security updates, so known weaknesses are not fixed.

Servy addresses these issues with the following measures:

* Each service is kept separate from the others, and Servy checks the identity of every program that asks for information.
* Servy automatically restricts who can open its stored settings, its logs, and the channel it uses to communicate between its parts.
* Sensitive data is encrypted with strong, widely used methods, and the encryption key is protected by Windows and tied to the computer.

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
