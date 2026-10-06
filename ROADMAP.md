## Roadmap

Servy's direction: keep the service runtime self-contained and portable, strengthen local supervision first, then layer optional remote management on top.

### Design principles

* **Local operation remains complete.** Creating and running a service never requires internet access, cloud registration, or a central management server.
* **Remote features are opt-in.** Network listeners are disabled by default or bound to localhost until explicitly configured.
* **One source of truth for behavior.** GUI, CLI, PowerShell, and future REST/web interfaces operate on the same configuration and service-management primitives.
* **Portable deployment stays easy.** Copy Servy and a service definition to another Windows machine and get operational quickly.
* **Security is designed in.** Secrets, authentication, authorization, TLS, and auditability get first-class treatment when remote management arrives.

### Delivered

#### Service creation and configuration

* [x] Windows Service creation via GUI
* [x] CLI and PowerShell module for full scripting and automated deployments
* [x] Support environment variables for the wrapped process ([#1](https://github.com/aelassas/servy/issues/1))
* [x] Support environment variable expansion in environment variables and process parameters ([#6](https://github.com/aelassas/servy/issues/6))
* [x] Support environment variable expansion in process paths ([#35](https://github.com/aelassas/servy/issues/35))
* [x] Support environment variable expansion in startup directories
* [x] Support service dependencies
* [x] Add "Log on as" configuration for Windows service
* [x] Add support for DOMAIN\gMSA$ Group Managed Service Accounts
* [x] Add support for automatic delayed-start service startup type
* [x] Allow configuring CPU affinity in service definitions ([#4436](https://github.com/aelassas/servy/issues/4436))
* [x] Export/import service configurations
* [x] Add dump and restore scripts for migration and VM cloning
* [x] Add `show` CLI command to display service configuration in a human-readable format ([#7018](https://github.com/aelassas/servy/issues/7018))
* [x] Service status query command in CLI

#### Logging

* [x] Logging stdout/stderr with size-based rotation
* [x] Logging stdout/stderr with date-based rotation ([#27](https://github.com/aelassas/servy/issues/27))
* [x] Add max rotations option to specify the maximum number of rotated log files to keep ([#26](https://github.com/aelassas/servy/issues/26))
* [x] Allow logging of stdout/stderr to the same file with size-based rotation ([#14](https://github.com/aelassas/servy/issues/14))
* [x] Add Event ID to Info, Warning, and Error service logs

#### Reliability, hooks, and lifecycle

* [x] Service monitoring and heartbeat checks
* [x] Automatic restart on failure
* [x] External Heartbeat Ping URL ([#2700](https://github.com/aelassas/servy/issues/2700))
* [x] Add support for pre-launch script execution before starting the service, with retries, timeout, and failure handling
* [x] Add support for fire-and-forget pre-launch hooks when timeout is set to 0
* [x] Add support for post-launch script execution after the process starts successfully
* [x] Add support for pre-stop and post-stop hooks ([#36](https://github.com/aelassas/servy/issues/36))
* [x] Add support for script or executable to run when the process fails to start
* [x] Support Ctrl+C for command-line apps ([#20](https://github.com/aelassas/servy/issues/20))
* [x] Keep SCM responsive while stopping the main wrapped process and its process tree

#### Platform and distribution

* [x] Add Help, Documentation, and Check for Updates menus
* [x] Add package manager support (WinGet, Chocolatey, Scoop) ([#9](https://github.com/aelassas/servy/issues/9))
* [x] Upgrade to .NET 10 LTS
* [x] Provide ARM64 binaries ([#2243](https://github.com/aelassas/servy/issues/2243))

#### Servy Manager App

* [x] Servy Manager App for managing services installed by Servy
* [x] Persist service configuration and track installed services in SQLite
* [x] Provide a "shortcut" to open the Servy Desktop App for full edits
* [x] Start, stop, restart, and uninstall services
* [x] Display service status and uptime
* [x] Add search and filter functionality for services
* [x] Provide Windows toast and email notifications for service events (failures)
* [x] Provide a log viewer
* [x] Support automatic recovery actions beyond simple restart (e.g., run scripts)
* [x] Support service dependency management (start/stop order)
* [x] Add bulk service operations (start/stop/restart multiple services at once)
* [x] Add PID column and copy PID action to services
* [x] Add real-time CPU and RAM monitoring with live performance graphs for services
* [x] Add a live Console tab for real-time stdout and stderr streaming
* [x] Add `Dependencies` tab for service dependency tree visualization

### Phase 1: Strengthen the local supervisor

#### 1. Resource-based recovery policies

* [ ] Add declarative resource-based restart policies (e.g., trigger restart on RAM/CPU usage thresholds)
  * [ ] Support RAM, CPU, and memory-growth-rate conditions (e.g., RAM > 2 GB for 5 minutes, memory growth > 500 MB/hour)
  * [ ] Support configurable threshold, duration, and violation count
  * [ ] Support cooldown between recovery actions
  * [ ] Support recovery actions: restart process, restart service, run failure hook
  * [ ] Support scope selection: root process or complete process tree

#### 2. Scheduling, maintenance windows, and event triggers

* [ ] Add scheduled uptime windows (e.g., start at 06:00, stop at 22:00)
* [ ] Add scheduled operations such as periodic or weekly restarts (e.g., stop, run maintenance hook, start)
* [ ] Add maintenance mode to pause health checks and suppress recovery during a given time range
* [ ] Add condition-based startup and triggers
  * [ ] Start when the network is available
  * [ ] Start when another service is running
  * [ ] Start when a file exists
  * [ ] Start when a TCP endpoint becomes reachable
  * [ ] Start on a Windows Event Log condition
* [ ] Add advanced scheduling and triggers in Servy Manager (start service on event, time, or condition)

#### 3. Tags, groups, and bulk operations

* [ ] Add service tags and groups to the service definition
* [ ] Add tag-based bulk operations in the CLI (e.g., `servy-cli start --tag backend`, `restart --tag production`, `stop --tag customer-a`)
* [ ] Add tag/group filtering in Servy Manager
* [ ] Add group actions in Servy Manager (restart selected services, stop a group, export selected definitions, enable maintenance mode across a group)

#### 4. Historical performance, health, and uptime

* [ ] Persist local telemetry in SQLite: CPU, RAM, restart events, crashes, health-check failures, uptime, and downtime
* [ ] Keep detailed samples for a short period and aggregate older data to keep retention lightweight
* [ ] Provide history views for last hour / 24 hours / 7 days / 30 days
  * [ ] CPU average and peak
  * [ ] RAM average and peak
  * [ ] Restart count
  * [ ] Health-check failures
  * [ ] Service uptime and total downtime
* [ ] Add a health monitoring dashboard in Servy Manager *(long-term)*

#### 5. Configuration templates and runtime profiles

* [ ] Add first-class, editable, and exportable profiles/templates for repeated service creation
* [ ] Ship built-in profiles for PowerShell, Node.js, Python, Java, and ASP.NET
* [ ] Allow profiles to define startup type, executable, logging, recovery, and shutdown behavior

#### 6. Deployment-friendly secret injection

* [ ] Support secret sources in exported service definitions without embedding credentials (e.g., `"passwordSource": "env:SERVY_PASSWORD"`)
* [ ] Resolve secrets from the deployment environment at import/install time, with no mandatory central secrets platform

#### 7. Servy Manager security and certificates

* [ ] Add `Security/Permissions` tab to view service ACLs and account privileges
* [ ] Add `Certificates` tab to manage service-specific certificates

### Phase 2: Optional management platform layer

Each feature in this phase must remain optional, and the local runtime must keep operating independently.

#### 8. Management REST API

* [ ] Add a management-only REST API for status, health metrics, and lifecycle control (start/stop/restart)
  * [ ] `GET /services`, `GET /services/{name}`
  * [ ] `GET /services/{name}/metrics`, `GET /services/{name}/logs`
  * [ ] `POST /services/{name}/start`, `/stop`, `/restart`
* [ ] Disabled by default and bound to localhost until explicitly enabled
* [ ] Support TLS, authentication, authorization, and audit logging

#### 9. Embedded local web dashboard

* [ ] Add a web dashboard for remote service control and real-time performance graphs
* [ ] Embed it in the Servy package (no IIS, SQL Server, or cloud service required), bound to localhost by default
* [ ] Build it on top of the management REST API
* [ ] Expose logs, service state, metrics, recovery history, and actions
* [ ] Support optional HTTPS remote access

#### 10. Multi-host management

* [ ] Add Servy Agent mode to manage multiple remote servers from a single instance
* [ ] Start only after the local API model is stable

#### 11. Structured events and webhooks

* [ ] Add a generic event model (e.g., `service.recovery.failed` with service, PID, attempts, CPU, and memory) to unify notifications, heartbeat URLs, and failure hooks
* [ ] Support targets: webhook POST, executable hook, Windows Event Log, email, and desktop notification
* [ ] Use the common event model across local and remote operations

### Documentation

* [ ] Periodically verify the competitor comparison against competitor activity (e.g., review the description of WinSW as unmaintained against its current repository activity)
