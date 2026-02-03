## Roadmap

* [x] Windows Service creation via GUI
* [x] Logging stdout/stderr with size-based rotation
* [x] Logging stdout/stderr with date-based rotation ([#27](https://github.com/aelassas/servy/issues/27))
* [x] Add max rotations option to specify the maximum number of rotated log files to keep ([#26](https://github.com/aelassas/servy/issues/26))
* [x] Allow logging of stdout/stderr to the same file with size-based rotation ([#14](https://github.com/aelassas/servy/issues/14))
* [x] Service monitoring and heartbeat checks
* [x] Automatic restart on failure
* [x] CLI and PowerShell module for full scripting and automated deployments
* [x] Support environment variables for the wrapped process ([#1](https://github.com/aelassas/servy/issues/1))
* [x] Support environment variable expansion in environment variables and process parameters ([#6](https://github.com/aelassas/servy/issues/6))
* [x] Support environment variable expansion in process paths ([#35](https://github.com/aelassas/servy/issues/35))
* [x] Support environment variable expansion in startup directories
* [x] Support service dependencies
* [x] Add "Log on as" configuration for Windows service
* [x] Add support for DOMAIN\gMSA$ Group Managed Service Accounts
* [x] Add support for pre-launch script execution before starting the service, with retries, timeout, and failure handling
* [x] Add support for fire-and-forget pre-launch hooks when timeout is set to 0
* [x] Add support for post-launch script execution after the process starts successfully
* [x] Add support for pre-stop and post-stop hooks ([#36](https://github.com/aelassas/servy/issues/36))
* [x] Service status query command in CLI
* [x] Export/import service configurations
* [x] Add Help, Documentation, and Check for Updates menus
* [x] Add package manager support (WinGet, Chocolatey, Scoop) ([#9](https://github.com/aelassas/servy/issues/9))
* [x] Add support for script or executable to run when the process fails to start
* [x] Add Event ID to Info, Warning, and Error service logs
* [x] Support using the same file for stdout and stderr ([#14](https://github.com/aelassas/servy/issues/14))
* [x] Support Ctrl+C for command-line apps ([#20](https://github.com/aelassas/servy/issues/20))
* [x] Add support for automatic delayed-start service startup type
* [x] Upgrade to .NET 10 LTS
* [x] Keep SCM responsive while stopping the main wrapped process and its process tree
* [ ] Add a REST API for management-only API for status, health metrics, and lifecycle control (start/stop/restart)
* [ ] Add a web dashboard for remote service control and real-time performance graphs
* [ ] Add Servy Agent mode to manage multiple remote servers from a single instance
* [ ] Add resource-based restart policies (e.g., trigger restart on RAM/CPU usage thresholds)
* [ ] Add scheduled uptime windows and maintenance mode at specific times (start/stop services and pause health checks)
* [x] Servy Manager App for managing services installed by Servy
  * [x] Persist service configuration and track installed services in SQLite
  * [x] Provide a "shortcut" to open the Servy Configuration App for full edits
  * [x] Start, stop, restart, and uninstall services
  * [x] Display service status and uptime
  * [x] Add search and filter functionality for services
  * [x] Provide Windows toast and email notifications for service events (failures)
  * [x] Provide a log viewer
  * [x] Support automatic recovery actions beyond simple restart (e.g., run scripts)
  * [ ] Add advanced scheduling and triggers (start service on event, time, or condition)
  * [x] Support service dependency management (start/stop order)
  * [x] Add bulk service operations (start/stop/restart multiple services at once)
  * [ ] Add a health monitoring dashboard *(long-term)*
  * [x] Add PID column and copy PID action to services
  * [x] Add real-time CPU and RAM monitoring with live performance graphs for services
  * [x] Add a live Console tab for real-time stdout and stderr streaming
  * [ ] Add `Security/Permissions` tab to view service ACLs and account privileges
  * [ ] Add `Certificates` tab to manage service-specific certificates
  * [x] Add `Dependencies` tab for service dependency tree visualization
