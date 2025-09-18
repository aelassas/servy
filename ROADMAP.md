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
* [ ] Add support for post-launch script execution after starting the service, with retries, timeout, and failure handling
* [x] Service status query command in CLI
* [x] Export/import service configurations
* [x] Add Help, Documentation, and Check for Updates menus
* [x] Add package manager support (WinGet, Chocolatey) ([#9](https://github.com/aelassas/servy/issues/9))
* [ ] Servy Manager App for managing services installed by Servy
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
  * [ ] Enable remote management of Servy services on other machines *(not planned – too dangerous)*
  * [ ] Add a health monitoring dashboard *(long-term)*
