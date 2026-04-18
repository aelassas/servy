# Changelog

## [Servy 7.8](https://github.com/aelassas/servy/releases/tag/v7.8)

**Date:** 2026-04-04 | **Tag:** [`v7.8`](https://github.com/aelassas/servy/tree/v7.8)

* feat(logger): add [`LogRollingInterval`](https://github.com/aelassas/servy/wiki/Advanced-Configuration#log-rolling-interval-logrollinginterval) config for daily, weekly, or monthly log rotation
* fix(logger): defer file creation until first write (lazy init)
* fix(core): replace WMI with SCM/Registry for resource refresh service queries (#48)
* fix(cli): `--version` output goes to `stderr` instead of `stdout` (#49)
* fix(cli): only inject the default verb if the user didn't provide a recognized verb
* fix(psm1): misleading error message when Servy CLI is not found (#50)
* fix(psm1): Invoke-ServyCli: non-zero exit code caught by its own try/catch block (#51)
* fix(psm1): Install-ServyService: Pre-stop and Post-stop parameters missing `[string]` type declaration (#52)
* fix(psm1): missing module manifest (.psd1) for Servy PowerShell module (#53)
* fix(psm1): Add-Arg does not quote values containing spaces (#54)
* fix(psm1): use `System.Diagnostics.Process` instead of `&` in Invoke-ServyCli (#54)
* fix(psm1): Uninstall-ServyService: incorrect function name in .EXAMPLE (#55)
* fix(psm1): Show-ServyVersion: inconsistent invocation pattern compared to other functions (#56)
* fix(psm1): Show-ServyHelp: .EXAMPLE does not demonstrate -Command parameter (#57)
* fix(psm1): Install-ServyService: PostLaunch missing parameters that PreLaunch has (#58)
* fix(psm1): Import-ServyServiceConfig: missing -Name parameter unlike Export-ServyServiceConfig (#59)
* perf(core): accelerate startup by replacing WMI with fast SCM/Registry queries (#48)

### Downloads
* [servy-7.8-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.8/servy-7.8-net48-sbom.xml) - 0.02 MB
* [servy-7.8-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.8/servy-7.8-net48-x64-installer.exe) - 3.98 MB
* [servy-7.8-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.8/servy-7.8-net48-x64-portable.7z) - 1.71 MB
* [servy-7.8-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.8/servy-7.8-sbom.xml) - 0.03 MB
* [servy-7.8-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.8/servy-7.8-x64-installer.exe) - 81.85 MB
* [servy-7.8-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.8/servy-7.8-x64-portable.7z) - 79.72 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v7.8.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v7.8.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v7.7...v7.8

## [Servy 7.7](https://github.com/aelassas/servy/releases/tag/v7.7)

**Date:** 2026-04-03 | **Tag:** [`v7.7`](https://github.com/aelassas/servy/tree/v7.7)

* feat(logger): add [`EnableEventLog`](https://github.com/aelassas/servy/wiki/Advanced-Configuration#general-logging-settings) option to allow disabling Windows Event Log via configuration
* refactor(core): general code cleanup and refactoring
* docs(wiki): improve [Advanced Configuration](https://github.com/aelassas/servy/wiki/Advanced-Configuration) documentation
* chore(announcement): launch new [Servy website](https://servy-win.github.io/) with refreshed branding, security audits, and 100% code coverage

### Downloads
* [servy-7.7-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.7/servy-7.7-net48-sbom.xml) - 0.02 MB
* [servy-7.7-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.7/servy-7.7-net48-x64-installer.exe) - 3.97 MB
* [servy-7.7-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.7/servy-7.7-net48-x64-portable.7z) - 1.71 MB
* [servy-7.7-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.7/servy-7.7-sbom.xml) - 0.03 MB
* [servy-7.7-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.7/servy-7.7-x64-installer.exe) - 81.76 MB
* [servy-7.7-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.7/servy-7.7-x64-portable.7z) - 79.7 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v7.7.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v7.7.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v7.6...v7.7

## [Servy 7.6](https://github.com/aelassas/servy/releases/tag/v7.6)

**Date:** 2026-04-02 | **Tag:** [`v7.6`](https://github.com/aelassas/servy/tree/v7.6)

* feat(service): read entire service configuration from database instead of service parameters
* feat(logger): implement dual-channel LogLevel support for the Windows Event Log and local files
* feat(logger): add [`LogRotationSizeMB`](https://github.com/aelassas/servy/wiki/Advanced-Configuration#settings-detail) configuration option (default: 10MB)
* feat(logger): add `NONE` [Log Level](https://github.com/aelassas/servy/wiki/Advanced-Configuration#logging-levels) to disable logging
* fix(logger): implement IDisposable in EventLogLogger for clean teardown
* fix(logger): prevent duplicate exception text in logs
* fix(service): use synchronous resource extraction to ensure thread safety
* fix(service): move resource refresh to service constructor to ensure it's done before any operations that rely on it
* fix(service): move logger disposal to service teardown
* fix(service): include `LogLevel` setting in .NET Framework 4.8 build
* fix(service): move event source creation to constructor for self-healing initialization
* fix(service): refactor InstallService to use InstallServiceOptions class for improved maintainability
* fix(restarter): ensure logger is disposed on exit
* fix(manager): improve stability and configuration consistency
* fix(manager): ensure EventRecords are disposed to prevent memory leaks
* fix(manager): include missing `ConfigurationAppPublishPath` configuration in .NET Framework 4.8 build
* fix(manager): remove deprecated `EnableDebugLogs` setting from .NET 10.0 build
* fix(manager): optimize log search threading by removing redundant `Task.Run` and `Dispatcher` nesting
* fix(net48): rename `Servy.Restarter.exe` to `Servy.Restarter.Net48.exe` to avoid conflict with the .NET 10.0 build
* ci(publish): update publish workflow to handle the new `Servy.Restarter.Net48.exe` filename for the .NET Framework 4.8 build

### Downloads
* [servy-7.6-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.6/servy-7.6-net48-sbom.xml) - 0.02 MB
* [servy-7.6-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.6/servy-7.6-net48-x64-installer.exe) - 3.97 MB
* [servy-7.6-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.6/servy-7.6-net48-x64-portable.7z) - 1.71 MB
* [servy-7.6-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.6/servy-7.6-sbom.xml) - 0.03 MB
* [servy-7.6-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.6/servy-7.6-x64-installer.exe) - 81.84 MB
* [servy-7.6-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.6/servy-7.6-x64-portable.7z) - 79.71 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v7.6.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v7.6.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v7.5...v7.6

## [Servy 7.5](https://github.com/aelassas/servy/releases/tag/v7.5)

**Date:** 2026-03-31 | **Tag:** [`v7.5`](https://github.com/aelassas/servy/tree/v7.5)

* feat(logger): expand observability across Desktop, CLI, Manager, Service, and Restarter [logs](https://github.com/aelassas/servy/wiki/Logging-&-Log-Rotation#internal-servy-logs)
* feat(logger): add `DEBUG` level for more verbose output during troubleshooting
* feat(logger): add [LogLevel](https://github.com/aelassas/servy/wiki/Advanced-Configuration) setting to dynamically adjust log verbosity at runtime
* fix(logger): prevent null entries in logs after abrupt termination
* fix(core): improve embedded resource extraction and refresh reliability after installation
* fix(core): prevent redundant resource refreshes using 20-minute timestamp delta
* fix(core): ensure reliable resource extraction on the first run after installation
* fix(cli): ensure logger is initialized before use in CLI

### Downloads
* [servy-7.5-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.5/servy-7.5-net48-sbom.xml) - 0.02 MB
* [servy-7.5-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.5/servy-7.5-net48-x64-installer.exe) - 3.97 MB
* [servy-7.5-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.5/servy-7.5-net48-x64-portable.7z) - 1.71 MB
* [servy-7.5-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.5/servy-7.5-sbom.xml) - 0.03 MB
* [servy-7.5-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.5/servy-7.5-x64-installer.exe) - 81.89 MB
* [servy-7.5-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.5/servy-7.5-x64-portable.7z) - 79.77 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v7.5.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v7.5.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v7.4...v7.5

## [Servy 7.4](https://github.com/aelassas/servy/releases/tag/v7.4)

**Date:** 2026-03-30 | **Tag:** [`v7.4`](https://github.com/aelassas/servy/tree/v7.4)

* feat(logger): expand observability across Desktop, CLI, and Manager apps
* fix(logger): ensure recovery logic coverage and prevent zombie handles
* fix(logger): restore correct encoding after log rotation to prevent garbled text
* fix(logger): enable 10MB default size rotation for Desktop, CLI and Manager logs
* fix(logger): include year boundary check in weekly log rotation
* fix(core): verify process StartTime before termination to prevent PID reuse kills
* fix(core): prevent recursive process termination of the current process tree
* fix(core): await Dapper tasks to prevent premature connection disposal
* fix(db): resolve TOCTOU race condition via atomic upsert in ServiceRepository
* fix(db): migrate service name index to UNIQUE to support ON CONFLICT logic
* fix(service): respect Windows service model forcing sync OnStart/OnStop
* fix(service): allow direct timeout checks for fire-and-forget pre-launch tasks
* fix(service): release managed handles for detached processes
* fix(service): add null-checks and error logging for Process.Start robustness
* fix(manager): implement high-performance log tailing via batch trimming
* fix(manager): allow root dependency node to expand and collapse
* fix(manager): move performance metrics off the UI thread
* ci(publish): migrate 7zip download to GitHub releases and fix install path

### Downloads
* [servy-7.4-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.4/servy-7.4-net48-sbom.xml) - 0.02 MB
* [servy-7.4-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.4/servy-7.4-net48-x64-installer.exe) - 3.97 MB
* [servy-7.4-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.4/servy-7.4-net48-x64-portable.7z) - 1.71 MB
* [servy-7.4-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.4/servy-7.4-sbom.xml) - 0.03 MB
* [servy-7.4-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.4/servy-7.4-x64-installer.exe) - 81.88 MB
* [servy-7.4-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.4/servy-7.4-x64-portable.7z) - 79.78 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v7.4.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v7.4.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v7.3...v7.4

## [Servy 7.3](https://github.com/aelassas/servy/releases/tag/v7.3)

**Date:** 2026-03-26 | **Tag:** [`v7.3`](https://github.com/aelassas/servy/tree/v7.3)

* fix(core): use local time instead of UTC for log rotation (#47)
* fix(core): correct log cleanup logic (#47)

### Downloads
* [servy-7.3-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.3/servy-7.3-net48-sbom.xml) - 0.02 MB
* [servy-7.3-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.3/servy-7.3-net48-x64-installer.exe) - 3.97 MB
* [servy-7.3-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.3/servy-7.3-net48-x64-portable.7z) - 1.71 MB
* [servy-7.3-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.3/servy-7.3-sbom.xml) - 0.03 MB
* [servy-7.3-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.3/servy-7.3-x64-installer.exe) - 81.87 MB
* [servy-7.3-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.3/servy-7.3-x64-portable.7z) - 79.76 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v7.3.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v7.3.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v7.2...v7.3

## [Servy 7.2](https://github.com/aelassas/servy/releases/tag/v7.2)

**Date:** 2026-03-26 | **Tag:** [`v7.2`](https://github.com/aelassas/servy/tree/v7.2)

* feat(core): change log rotation naming to insert timestamp before extension (#47)
* feat(core): use local time instead of UTC for log rotation (#47)
* feat(core): update log cleanup logic (#47)
* ci(publish): fix VirusTotal 502 timeouts

### Downloads
* [servy-7.2-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.2/servy-7.2-net48-sbom.xml) - 0.02 MB
* [servy-7.2-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.2/servy-7.2-net48-x64-installer.exe) - 3.97 MB
* [servy-7.2-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.2/servy-7.2-net48-x64-portable.7z) - 1.71 MB
* [servy-7.2-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.2/servy-7.2-sbom.xml) - 0.03 MB
* [servy-7.2-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.2/servy-7.2-x64-installer.exe) - 81.87 MB
* [servy-7.2-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.2/servy-7.2-x64-portable.7z) - 79.76 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v7.2.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v7.2.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v7.1...v7.2

## [Servy 7.1](https://github.com/aelassas/servy/releases/tag/v7.1)

**Date:** 2026-03-25 | **Tag:** [`v7.1`](https://github.com/aelassas/servy/tree/v7.1)

* fix(cli): typo in import validation message (#45)
* fix(cli): install after import not registered with `Servy.Service.CLI.exe` (#46)
* ci(publish): wrong Inno Setup download URL
* ci(publish): upgrade artifact upload to actions/upload-artifact@v6
* ci(publish): fix SBOM schema version
* ci(publish): fix VirusTotal 502 timeouts
* chore(deps): update dependencies

### Downloads
* [servy-7.1-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.1/servy-7.1-net48-sbom.xml) - 0.02 MB
* [servy-7.1-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.1/servy-7.1-net48-x64-installer.exe) - 3.97 MB
* [servy-7.1-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.1/servy-7.1-net48-x64-portable.7z) - 1.71 MB
* [servy-7.1-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.1/servy-7.1-sbom.xml) - 0.03 MB
* [servy-7.1-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.1/servy-7.1-x64-installer.exe) - 81.85 MB
* [servy-7.1-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.1/servy-7.1-x64-portable.7z) - 79.74 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v7.1.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v7.1.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v7.0...v7.1

## [Servy 7.0](https://github.com/aelassas/servy/releases/tag/v7.0)

**Date:** 2026-03-14 | **Tag:** [`v7.0`](https://github.com/aelassas/servy/tree/v7.0)

* fix(desktop,manager): add `--force-sr` flag to resolve blank UI issues on MeshCentral (#44)
* fix(desktop): fix manager app detection when launched from CLI
* fix(manager): fix desktop app detection when launched from CLI

### Downloads
* [servy-7.0-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.0/servy-7.0-net48-sbom.xml) - 0.02 MB
* [servy-7.0-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.0/servy-7.0-net48-x64-installer.exe) - 3.97 MB
* [servy-7.0-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.0/servy-7.0-net48-x64-portable.7z) - 1.71 MB
* [servy-7.0-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.0/servy-7.0-sbom.xml) - 0.03 MB
* [servy-7.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.0/servy-7.0-x64-installer.exe) - 81.86 MB
* [servy-7.0-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.0/servy-7.0-x64-portable.7z) - 79.74 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v7.0.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v7.0.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v6.9...v7.0

## [Servy 6.9](https://github.com/aelassas/servy/releases/tag/v6.9)

**Date:** 2026-03-13 | **Tag:** [`v6.9`](https://github.com/aelassas/servy/tree/v6.9)

* fix(desktop,manager): blank UI on some machines (#44)
* fix(desktop,manager): add logger with software-rendering diagnostics (#44)
* fix(desktop): pre-stop and post-stop file dialogs not working
* fix(installer): remove Servy Manager launcher from post-install window
* chore(deps): update dependencies

### Downloads
* [servy-6.9-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.9/servy-6.9-net48-sbom.xml) - 0.02 MB
* [servy-6.9-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.9/servy-6.9-net48-x64-installer.exe) - 3.96 MB
* [servy-6.9-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.9/servy-6.9-net48-x64-portable.7z) - 1.71 MB
* [servy-6.9-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.9/servy-6.9-sbom.xml) - 0.03 MB
* [servy-6.9-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.9/servy-6.9-x64-installer.exe) - 81.79 MB
* [servy-6.9-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.9/servy-6.9-x64-portable.7z) - 79.74 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v6.9.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v6.9.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v6.8...v6.9

## [Servy 6.8](https://github.com/aelassas/servy/releases/tag/v6.8)

**Date:** 2026-02-26 | **Tag:** [`v6.8`](https://github.com/aelassas/servy/tree/v6.8)

* perf(core): optimize encryption with modern high-performance crypto APIs
* perf(manager): optimize Services tab performance
* perf(manager): move console log sorting off the UI thread
* fix(desktop): improve layout sizing on small displays
* fix(desktop): update pre-launch help text and clarify pre-launch and pre-stop timeouts
* fix(manager): set minimum width for Select All column
* fix(dev): correct wrapper service path resolution in Debug mode
* chore(deps): update dependencies
* docs(wiki): add Kopia service sample to [Examples & Recipes docs](https://github.com/aelassas/servy/wiki/Examples-&-Recipes#run-kopia-as-a-service) (#41)

### Downloads
* [servy-6.8-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.8/servy-6.8-net48-sbom.xml) - 0.02 MB
* [servy-6.8-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.8/servy-6.8-net48-x64-installer.exe) - 3.95 MB
* [servy-6.8-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.8/servy-6.8-net48-x64-portable.7z) - 1.7 MB
* [servy-6.8-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.8/servy-6.8-sbom.xml) - 0.03 MB
* [servy-6.8-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.8/servy-6.8-x64-installer.exe) - 81.85 MB
* [servy-6.8-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.8/servy-6.8-x64-portable.7z) - 79.72 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v6.8.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v6.8.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v6.7...v6.8

## [Servy 6.7](https://github.com/aelassas/servy/releases/tag/v6.7)

**Date:** 2026-02-15 | **Tag:** [`v6.7`](https://github.com/aelassas/servy/tree/v6.7)

* fix(manager): restore decryption on refresh to fix field values
* fix(ci): resolve coverlet runtime issue on `net48` branch

### Downloads
* [servy-6.7-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.7/servy-6.7-net48-sbom.xml) - 0.02 MB
* [servy-6.7-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.7/servy-6.7-net48-x64-installer.exe) - 3.96 MB
* [servy-6.7-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.7/servy-6.7-net48-x64-portable.7z) - 1.7 MB
* [servy-6.7-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.7/servy-6.7-sbom.xml) - 0.03 MB
* [servy-6.7-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.7/servy-6.7-x64-installer.exe) - 81.84 MB
* [servy-6.7-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.7/servy-6.7-x64-portable.7z) - 79.73 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v6.7.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v6.7.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v6.6...v6.7

## [Servy 6.6](https://github.com/aelassas/servy/releases/tag/v6.6)

**Date:** 2026-02-14 | **Tag:** [`v6.6`](https://github.com/aelassas/servy/tree/v6.6)

> [!IMPORTANT]
> This version contains a critical bug in Servy Manager. 
> It is not recommended for production use. Please use a different version instead. 
> For maximum stability and security, always use the latest available version of Servy.

* feat(manager): optimize background timer performance
* fix(manager): remove unnecessary decryptions across all tabs
* fix(manager): correct wrapper service path in Debug mode
* fix(apps): ensure full application shutdown on main window close
* docs(wiki): update CLI and PowerShell docs

### Downloads
* [servy-6.6-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.6/servy-6.6-net48-sbom.xml) - 0.02 MB
* [servy-6.6-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.6/servy-6.6-net48-x64-installer.exe) - 3.96 MB
* [servy-6.6-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.6/servy-6.6-net48-x64-portable.7z) - 1.7 MB
* [servy-6.6-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.6/servy-6.6-sbom.xml) - 0.03 MB
* [servy-6.6-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.6/servy-6.6-x64-installer.exe) - 81.85 MB
* [servy-6.6-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.6/servy-6.6-x64-portable.7z) - 79.73 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v6.6.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v6.6.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v6.5...v6.6

## [Servy 6.5](https://github.com/aelassas/servy/releases/tag/v6.5)

**Date:** 2026-02-13 | **Tag:** [`v6.5`](https://github.com/aelassas/servy/tree/v6.5)

* feat(security): implement authenticated encryption using AES-CBC with HMAC-SHA256
* perf(core): improve performance and overall stability
* fix(service): remove duplicate recovery flag reset
* fix(net48): ensure SQLite assemblies are deployed at runtime
* chore(deps): update dependencies
* docs(wiki): update documentation

### Downloads
* [servy-6.5-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.5/servy-6.5-net48-sbom.xml) - 0.02 MB
* [servy-6.5-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.5/servy-6.5-net48-x64-installer.exe) - 3.96 MB
* [servy-6.5-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.5/servy-6.5-net48-x64-portable.7z) - 1.7 MB
* [servy-6.5-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.5/servy-6.5-sbom.xml) - 0.03 MB
* [servy-6.5-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.5/servy-6.5-x64-installer.exe) - 81.85 MB
* [servy-6.5-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.5/servy-6.5-x64-portable.7z) - 79.73 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v6.5.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v6.5.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v6.4...v6.5

## [Servy 6.4](https://github.com/aelassas/servy/releases/tag/v6.4)

**Date:** 2026-02-09 | **Tag:** [`v6.4`](https://github.com/aelassas/servy/tree/v6.4)

* fix(cli): auto-disable spinner when no console is attached (#39)
* chore(setup): normalize the publish scripts and CI workflow
* docs(wiki): update and enhance the [documentation](https://github.com/aelassas/servy/wiki)

### Downloads
* [servy-6.4-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.4/servy-6.4-net48-sbom.xml) - 0.01 MB
* [servy-6.4-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.4/servy-6.4-net48-x64-installer.exe) - 3.94 MB
* [servy-6.4-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.4/servy-6.4-net48-x64-portable.7z) - 1.7 MB
* [servy-6.4-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.4/servy-6.4-sbom.xml) - 0.03 MB
* [servy-6.4-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.4/servy-6.4-x64-installer.exe) - 81.85 MB
* [servy-6.4-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.4/servy-6.4-x64-portable.7z) - 79.72 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v6.4.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v6.4.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v6.3...v6.4

## [Servy 6.3](https://github.com/aelassas/servy/releases/tag/v6.3)

**Date:** 2026-02-06 | **Tag:** [`v6.3`](https://github.com/aelassas/servy/tree/v6.3)

* fix(service): register `PRESHUTDOWN` support in `OnStart` (#37)
* fix(service): ignore `PRESHUTDOWN` signal during computer restart recovery action
* fix(service): prevent infinite crash loops with stability-based counter reset
* fix(service): implement proportional stability threshold for monitoring
* fix(service): prevent restart counter reset during computer restart recovery action
* fix(service): decouple health detection from recovery execution
* fix(service): improve performance and stability of [health monitoring](https://github.com/aelassas/servy/wiki/Health-Monitoring-&-Recovery#reboot-detection-example)
* fix(service): resolve race conditions in process health checks
* fix(service): ignore recovery when teardown starts
* fix(service): synchronize health monitor with teardown state
* fix(service): implement thread-safe access to restart attempts file
* feat(setup): add "Launch Servy Manager" option after setup completes

### Downloads
* [servy-6.3-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.3/servy-6.3-net48-sbom.xml) - 0.01 MB
* [servy-6.3-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.3/servy-6.3-net48-x64-installer.exe) - 3.96 MB
* [servy-6.3-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.3/servy-6.3-net48-x64-portable.7z) - 1.7 MB
* [servy-6.3-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.3/servy-6.3-sbom.xml) - 0.03 MB
* [servy-6.3-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.3/servy-6.3-x64-installer.exe) - 81.81 MB
* [servy-6.3-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.3/servy-6.3-x64-portable.7z) - 79.69 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v6.3.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v6.3.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v6.2...v6.3

## [Servy 6.2](https://github.com/aelassas/servy/releases/tag/v6.2)

**Date:** 2026-02-04 | **Tag:** [`v6.2`](https://github.com/aelassas/servy/tree/v6.2)

* fix(service): explicitly handle OS shutdown with SCM wait pulses (#37)
* fix(core): ensure service start respects configured pre-launch timeout
* fix(core): ensure service stop and restart respect configured pre-stop timeout
* feat(manager): add visual detection of circular dependencies in service tree
* feat(manager): add dynamic tooltips for service status and cycle warnings
* feat(manager): sort service tree nodes alphabetically by display name

### Downloads
* [servy-6.2-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.2/servy-6.2-net48-sbom.xml) - 0.01 MB
* [servy-6.2-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.2/servy-6.2-net48-x64-installer.exe) - 3.96 MB
* [servy-6.2-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.2/servy-6.2-net48-x64-portable.7z) - 1.7 MB
* [servy-6.2-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.2/servy-6.2-sbom.xml) - 0.03 MB
* [servy-6.2-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.2/servy-6.2-x64-installer.exe) - 81.83 MB
* [servy-6.2-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.2/servy-6.2-x64-portable.7z) - 79.71 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v6.2.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v6.2.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v6.1...v6.2

## [Servy 6.1](https://github.com/aelassas/servy/releases/tag/v6.1)

**Date:** 2026-02-03 | **Tag:** [`v6.1`](https://github.com/aelassas/servy/tree/v6.1)

* feat(manager): add [Dependencies tab](https://github.com/aelassas/servy/wiki/Servy-Manager#dependencies) to show service dependency tree with status indicators
* refactor(manager): extract service list into a reusable control
* refactor(manager): move UI constants to Servy.UI for reuse
* chore(psm1): replace backticks with splatting in PowerShell samples

### Downloads
* [servy-6.1-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.1/servy-6.1-net48-sbom.xml) - 0.01 MB
* [servy-6.1-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.1/servy-6.1-net48-x64-installer.exe) - 3.96 MB
* [servy-6.1-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.1/servy-6.1-net48-x64-portable.7z) - 1.7 MB
* [servy-6.1-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.1/servy-6.1-sbom.xml) - 0.03 MB
* [servy-6.1-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.1/servy-6.1-x64-installer.exe) - 81.85 MB
* [servy-6.1-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.1/servy-6.1-x64-portable.7z) - 79.73 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v6.1.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v6.1.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v6.0...v6.1

## [Servy 6.0](https://github.com/aelassas/servy/releases/tag/v6.0)

**Date:** 2026-02-01 | **Tag:** [`v6.0`](https://github.com/aelassas/servy/tree/v6.0)

* feat(core): support fire-and-forget pre-launch hooks when timeout is set to 0
* fix(service): clean up orphaned pre-launch and post-launch hook processes on service stop
* fix(service): remove post-launch, pre-stop, and post-stop arguments from logs for security
* fix(desktop): reduce window height on small resolutions
* fix(cli): typo in `--preStopTimeout` option documentation for `install` command
* chore(ci): add LoC badges for prod, tests, and total code
* docs(wiki): add Pre‐Stop & Post‐Stop Actions docs

### Downloads
* [servy-6.0-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.0/servy-6.0-net48-sbom.xml) - 0.01 MB
* [servy-6.0-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.0/servy-6.0-net48-x64-installer.exe) - 3.95 MB
* [servy-6.0-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.0/servy-6.0-net48-x64-portable.7z) - 1.69 MB
* [servy-6.0-sbom.xml](https://github.com/aelassas/servy/releases/download/v6.0/servy-6.0-sbom.xml) - 0.03 MB
* [servy-6.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v6.0/servy-6.0-x64-installer.exe) - 81.83 MB
* [servy-6.0-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v6.0/servy-6.0-x64-portable.7z) - 79.72 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v6.0.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v6.0.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v5.9...v6.0

## [Servy 5.9](https://github.com/aelassas/servy/releases/tag/v5.9)

**Date:** 2026-01-30 | **Tag:** [`v5.9`](https://github.com/aelassas/servy/tree/v5.9)

* feat(manager): add [Console tab](https://github.com/aelassas/servy/wiki/Overview#console) to display real-time service stdout and stderr output
* feat(core): add pre-stop and post-stop hooks (#36)
* fix(service): request SCM additional time in pulses while pre-launch hook is running
* fix(tests): correct test script issues
* chore(tests): upgrade to xUnit v3
* chore(tests): remove deprecated xUnit packages

### Downloads
* [servy-5.9-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.9/servy-5.9-net48-sbom.xml) - 0.01 MB
* [servy-5.9-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.9/servy-5.9-net48-x64-installer.exe) - 3.95 MB
* [servy-5.9-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.9/servy-5.9-net48-x64-portable.7z) - 1.69 MB
* [servy-5.9-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.9/servy-5.9-sbom.xml) - 0.03 MB
* [servy-5.9-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.9/servy-5.9-x64-installer.exe) - 81.86 MB
* [servy-5.9-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.9/servy-5.9-x64-portable.7z) - 79.73 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v5.9.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v5.9.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v5.8...v5.9

## [Servy 5.8](https://github.com/aelassas/servy/releases/tag/v5.8)

**Date:** 2026-01-25 | **Tag:** [`v5.8`](https://github.com/aelassas/servy/tree/v5.8)

* fix(service): implement resilient recursive process tree termination
* fix(service): prevent orphaned child processes when parent is force-killed

### Downloads
* [servy-5.8-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.8/servy-5.8-net48-sbom.xml) - 0.01 MB
* [servy-5.8-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.8/servy-5.8-net48-x64-installer.exe) - 3.93 MB
* [servy-5.8-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.8/servy-5.8-net48-x64-portable.7z) - 1.68 MB
* [servy-5.8-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.8/servy-5.8-sbom.xml) - 0.03 MB
* [servy-5.8-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.8/servy-5.8-x64-installer.exe) - 81.76 MB
* [servy-5.8-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.8/servy-5.8-x64-portable.7z) - 79.67 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v5.8.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v5.8.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v5.7...v5.8

## [Servy 5.7](https://github.com/aelassas/servy/releases/tag/v5.7)

**Date:** 2026-01-24 | **Tag:** [`v5.7`](https://github.com/aelassas/servy/tree/v5.7)

fix(service): ensure cleanup of descendant processes on shutdown
fix(service): propagate `Ctrl+C` signal to descendant processes during stop
fix(service): use pulsed shutdown to allow full process tree cleanup
fix(service): keep SCM responsive during long-running process termination
fix(service): improve process stop logic for complex process trees
fix(service): align restart recovery with configured stop timeout

### Downloads
* [servy-5.7-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.7/servy-5.7-net48-sbom.xml) - 0.01 MB
* [servy-5.7-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.7/servy-5.7-net48-x64-installer.exe) - 3.93 MB
* [servy-5.7-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.7/servy-5.7-net48-x64-portable.7z) - 1.68 MB
* [servy-5.7-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.7/servy-5.7-sbom.xml) - 0.03 MB
* [servy-5.7-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.7/servy-5.7-x64-installer.exe) - 81.77 MB
* [servy-5.7-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.7/servy-5.7-x64-portable.7z) - 79.69 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v5.7.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v5.7.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v5.6...v5.7

## [Servy 5.6](https://github.com/aelassas/servy/releases/tag/v5.6)

**Date:** 2026-01-22 | **Tag:** [`v5.6`](https://github.com/aelassas/servy/tree/v5.6)

* feat(core): allow environment variable expansion in process paths (#35)
* feat(core): allow environment variable expansion in startup directories
* fix(service): keep SCM responsive by requesting additional time in short pulses during stop
* fix(service): improve startup options validation for process paths and startup directories

### Downloads
* [servy-5.6-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.6/servy-5.6-net48-sbom.xml) - 0.01 MB
* [servy-5.6-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.6/servy-5.6-net48-x64-installer.exe) - 3.93 MB
* [servy-5.6-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.6/servy-5.6-net48-x64-portable.7z) - 1.68 MB
* [servy-5.6-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.6/servy-5.6-sbom.xml) - 0.03 MB
* [servy-5.6-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.6/servy-5.6-x64-installer.exe) - 81.77 MB
* [servy-5.6-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.6/servy-5.6-x64-portable.7z) - 79.69 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v5.6.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v5.6.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v5.5...v5.6

## [Servy 5.5](https://github.com/aelassas/servy/releases/tag/v5.5)

**Date:** 2026-01-21 | **Tag:** [`v5.5`](https://github.com/aelassas/servy/tree/v5.5)

* fix(core): request additional SCM start and stop time when configured timeout approaches limit
* fix(core): ensure service is in database before performing start, stop and restart actions
* fix(core): align start and stop timeouts with service timeouts from SCM and database while restarting services
* fix(core): use previous stop timeout while calculating total stop time during restart
* docs(wiki): update CLI commands and examples in multiple wiki pages
* docs(wiki): expand FAQ with more questions and answers

### Downloads
* [servy-5.5-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.5/servy-5.5-net48-sbom.xml) - 0.01 MB
* [servy-5.5-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.5/servy-5.5-net48-x64-installer.exe) - 3.93 MB
* [servy-5.5-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.5/servy-5.5-net48-x64-portable.7z) - 1.68 MB
* [servy-5.5-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.5/servy-5.5-sbom.xml) - 0.03 MB
* [servy-5.5-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.5/servy-5.5-x64-installer.exe) - 81.98 MB
* [servy-5.5-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.5/servy-5.5-x64-portable.7z) - 79.87 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v5.5.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v5.5.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v5.4...v5.5

## [Servy 5.4](https://github.com/aelassas/servy/releases/tag/v5.4)

**Date:** 2026-01-20 | **Tag:** [`v5.4`](https://github.com/aelassas/servy/tree/v5.4)

* feat(psm1): improve CLI discovery for installed and portable setups
* fix(manager): handle long user session values with proper width and trimming
* fix(tests): eliminate race condition from fire-and-forget async work in ServiceCommands
* chore(psm1): update PowerShell module samples
* chore(deps): update dependencies
* docs(psm1): update PowerShell module docs
* docs(wiki): expand FAQ with more questions and answers

### Downloads
* [servy-5.4-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.4/servy-5.4-net48-sbom.xml) - 0.01 MB
* [servy-5.4-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.4/servy-5.4-net48-x64-installer.exe) - 3.92 MB
* [servy-5.4-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.4/servy-5.4-net48-x64-portable.7z) - 1.67 MB
* [servy-5.4-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.4/servy-5.4-sbom.xml) - 0.03 MB
* [servy-5.4-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.4/servy-5.4-x64-installer.exe) - 81.93 MB
* [servy-5.4-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.4/servy-5.4-x64-portable.7z) - 79.82 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v5.4.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v5.4.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v5.3...v5.4

## [Servy 5.3](https://github.com/aelassas/servy/releases/tag/v5.3)

**Date:** 2026-01-14 | **Tag:** [`v5.3`](https://github.com/aelassas/servy/tree/v5.3)

* feat(psm1): make Servy PowerShell module fully compatible with PowerShell 2.0+
* feat(psm1): ensure Servy PowerShell module compatibility with Windows 7+ and Windows Server 2008+
* fix(psm1): resolve `servy-cli.exe` path relative to module for portable and SCCM (#31)
* fix(psm1): correct validation for `-StartupType` and `-DateRotationType` parameters for `Install-ServyService` function
* fix(psm1): replace exit statements with throw for proper PowerShell error handling
* fix(psm1): throw error when cli exits with non-zero exit code
* fix(psm1): add validation to `help` command
* fix(cli): return exit code 0 for `help` and `version` commands instead of 1
* fix(manager): increase performance graph grid thickness for pixel-perfect visibility
* refactor(psm1): dry up code and improve error handling
* chore(deps): update dependencies
* chore(core): remove unused dependency `System.Diagnostics.PerformanceCounter`
* docs(psm1): clarify module usage for installed and portable Servy versions (#31)
* docs(wiki): update Export/Import and PowerShell docs

### Downloads
* [servy-5.3-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.3/servy-5.3-net48-sbom.xml) - 0.01 MB
* [servy-5.3-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.3/servy-5.3-net48-x64-installer.exe) - 3.93 MB
* [servy-5.3-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.3/servy-5.3-net48-x64-portable.7z) - 1.67 MB
* [servy-5.3-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.3/servy-5.3-sbom.xml) - 0.03 MB
* [servy-5.3-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.3/servy-5.3-x64-installer.exe) - 81.93 MB
* [servy-5.3-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.3/servy-5.3-x64-portable.7z) - 79.82 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v5.3.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v5.3.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v5.2...v5.3

## [Servy 5.2](https://github.com/aelassas/servy/releases/tag/v5.2)

**Date:** 2026-01-13 | **Tag:** [`v5.2`](https://github.com/aelassas/servy/tree/v5.2)

* feat(manager): optimize CPU and RAM graph rendering performance
* fix(manager): disable hit testing on CPU and RAM graphs
* fix(manager): ensure character ellipsis triggers in service Name and Description columns
* fix(manager): set minimum window width to 940px to prevent layout clipping
* fix(manager): correct PID badge padding and layout in Performance tab

### Downloads
* [servy-5.2-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.2/servy-5.2-net48-sbom.xml) - 0.01 MB
* [servy-5.2-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.2/servy-5.2-net48-x64-installer.exe) - 3.93 MB
* [servy-5.2-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.2/servy-5.2-net48-x64-portable.7z) - 1.67 MB
* [servy-5.2-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.2/servy-5.2-sbom.xml) - 0.04 MB
* [servy-5.2-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.2/servy-5.2-x64-installer.exe) - 82.23 MB
* [servy-5.2-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.2/servy-5.2-x64-portable.7z) - 80.12 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v5.2.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v5.2.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v5.1...v5.2

## [Servy 5.1](https://github.com/aelassas/servy/releases/tag/v5.1)

**Date:** 2026-01-12 | **Tag:** [`v5.1`](https://github.com/aelassas/servy/tree/v5.1)

* feat(manager): add copy PID button to Performance tab
* feat(manager): theme PID label as a badge using performance color palette
* fix(manager): prevent content clipping of PID badge in Performance tab
* fix(manager): center search text and cursor vertically in Services and Logs tabs
* fix(manager): prevent search box auto-focus when changing between tabs
* fix(manager): remove focus style from focused elements
* docs(manager): finalize graph interaction model to match Windows Task Manager standards
* chore(installer): fix false positive virus scan detections (Windows Defender, Deep Instinct, ClamAV)

### Downloads
* [servy-5.1-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.1/servy-5.1-net48-sbom.xml) - 0.01 MB
* [servy-5.1-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.1/servy-5.1-net48-x64-installer.exe) - 3.93 MB
* [servy-5.1-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.1/servy-5.1-net48-x64-portable.7z) - 1.67 MB
* [servy-5.1-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.1/servy-5.1-sbom.xml) - 0.04 MB
* [servy-5.1-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.1/servy-5.1-x64-installer.exe) - 82.23 MB
* [servy-5.1-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.1/servy-5.1-x64-portable.7z) - 80.12 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v5.1.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v5.1.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v5.0...v5.1

## [Servy 5.0](https://github.com/aelassas/servy/releases/tag/v5.0)

**Date:** 2026-01-11 | **Tag:** [`v5.0`](https://github.com/aelassas/servy/tree/v5.0)

* feat(manager): refine CPU and RAM graph point collection and rendering
* feat(manager): optimize background refresh with batch fetching, throttling, and atomic flags
* feat(manager): optimize overall performance and responsiveness of Services and Performance tabs
* fix(manager): stop CPU and RAM graphs from blocking mouse clicks
* fix(manager): prevent multiple service selection in Performance tab
* fix(manager): correct hover background color for logs grid
* chore(installer): fix false positive virus scan detections (Windows Defender, ClamAV)

### Downloads
* [servy-5.0-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.0/servy-5.0-net48-sbom.xml) - 0.01 MB
* [servy-5.0-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.0/servy-5.0-net48-x64-installer.exe) - 3.92 MB
* [servy-5.0-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.0/servy-5.0-net48-x64-portable.7z) - 1.67 MB
* [servy-5.0-sbom.xml](https://github.com/aelassas/servy/releases/download/v5.0/servy-5.0-sbom.xml) - 0.04 MB
* [servy-5.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v5.0/servy-5.0-x64-installer.exe) - 82.23 MB
* [servy-5.0-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v5.0/servy-5.0-x64-portable.7z) - 80.12 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v5.0.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v5.0.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v4.9...v5.0

## [Servy 4.9](https://github.com/aelassas/servy/releases/tag/v4.9)

**Date:** 2026-01-10 | **Tag:** [`v4.9`](https://github.com/aelassas/servy/tree/v4.9)

* feat(cli): add `--install` option to import command to install service after import
* feat(powershell): add `-Install` switch to `Import-ServyServiceConfig` cmdlet to install service after import
* feat(manager): optimize real-time CPU and RAM metric collection in Services tab
* feat(manager): apply modern look to services list in Performance tab
* feat(manager): allow sorting services by name in Performance tab
* feat(manager): add hover background to services and logs grids
* fix(manager): stabilize CPU and RAM graph polling in Performance tab
* fix(manager): keep the UI thread smooth during CPU and RAM graph updates
* fix(manager): prevent timer re-entry and zombie restarts during monitoring

### Downloads
* [servy-4.9-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.9/servy-4.9-net48-sbom.xml) - 0.01 MB
* [servy-4.9-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.9/servy-4.9-net48-x64-installer.exe) - 3.93 MB
* [servy-4.9-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.9/servy-4.9-net48-x64-portable.7z) - 1.67 MB
* [servy-4.9-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.9/servy-4.9-sbom.xml) - 0.04 MB
* [servy-4.9-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.9/servy-4.9-x64-installer.exe) - 82.23 MB
* [servy-4.9-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.9/servy-4.9-x64-portable.7z) - 80.11 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v4.9.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v4.9.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v4.8...v4.9

## [Servy 4.8](https://github.com/aelassas/servy/releases/tag/v4.8)

**Date:** 2026-01-08 | **Tag:** [`v4.8`](https://github.com/aelassas/servy/tree/v4.8)

* feat(core): add start and stop timeout options to desktop app, CLI and PowerShell module
* feat(manager): optimize real-time CPU and RAM metric collection in Services tab
* docs(wiki): update FAQ, CLI, PowerShell and Export/Import docs

### Downloads
* [servy-4.8-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.8/servy-4.8-net48-sbom.xml) - 0.01 MB
* [servy-4.8-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.8/servy-4.8-net48-x64-installer.exe) - 3.9 MB
* [servy-4.8-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.8/servy-4.8-net48-x64-portable.7z) - 1.67 MB
* [servy-4.8-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.8/servy-4.8-sbom.xml) - 0.04 MB
* [servy-4.8-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.8/servy-4.8-x64-installer.exe) - 82.22 MB
* [servy-4.8-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.8/servy-4.8-x64-portable.7z) - 80.12 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v4.8.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v4.8.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v4.7...v4.8

## [Servy 4.7](https://github.com/aelassas/servy/releases/tag/v4.7)

**Date:** 2026-01-07 | **Tag:** [`v4.7`](https://github.com/aelassas/servy/tree/v4.7)

* feat(manager): optimize CPU and RAM graphs rendering and responsiveness
* fix(manager): handle service restarts by resetting CPU and RAM graphs on PID change
* fix(manager): correct visual synchronization of CPU and RAM graphs data
* fix(manager): resolve fencepost error for perfect graph-grid alignment
* fix(manager): prevent redundant CPU and RAM graphs resets and improve state tracking
* fix(manager): prevent race condition in async search
* fix(manager): optimize performance tab search
* fix(manager): add logging to performance tab in case of search failure
* refactor(manager): decouple CPU and RAM graphs grid logic from View Model to View

### Downloads
* [servy-4.7-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.7/servy-4.7-net48-sbom.xml) - 0.01 MB
* [servy-4.7-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.7/servy-4.7-net48-x64-installer.exe) - 3.91 MB
* [servy-4.7-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.7/servy-4.7-net48-x64-portable.7z) - 1.67 MB
* [servy-4.7-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.7/servy-4.7-sbom.xml) - 0.04 MB
* [servy-4.7-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.7/servy-4.7-x64-installer.exe) - 82.23 MB
* [servy-4.7-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.7/servy-4.7-x64-portable.7z) - 80.11 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v4.7.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v4.7.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v4.6...v4.7

## [Servy 4.6](https://github.com/aelassas/servy/releases/tag/v4.6)

**Date:** 2026-01-06 | **Tag:** [`v4.6`](https://github.com/aelassas/servy/tree/v4.6)

* feat(manager): apply modern look to CPU and RAM performance graphs
* feat(manager): use raw CPU and RAM values in performance graphs without averaging
* feat(manager): add padding to services list for better readability in performance tab
* fix(tests): prevent multiple WPF Application instances across STA unit tests

### Downloads
* [servy-4.6-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.6/servy-4.6-net48-sbom.xml) - 0.01 MB
* [servy-4.6-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.6/servy-4.6-net48-x64-installer.exe) - 3.91 MB
* [servy-4.6-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.6/servy-4.6-net48-x64-portable.7z) - 1.67 MB
* [servy-4.6-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.6/servy-4.6-sbom.xml) - 0.04 MB
* [servy-4.6-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.6/servy-4.6-x64-installer.exe) - 82.24 MB
* [servy-4.6-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.6/servy-4.6-x64-portable.7z) - 80.13 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v4.6.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v4.6.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v4.5...v4.6

## [Servy 4.5](https://github.com/aelassas/servy/releases/tag/v4.5)

**Date:** 2026-01-05 | **Tag:** [`v4.5`](https://github.com/aelassas/servy/tree/v4.5)

* feat(manager): add performance tab with real-time CPU and RAM monitoring graphs
* chore(setup): add dark mode support to installers
* chore(setup): add Uninstall shortcut to Start Menu
* chore(setup): add .NET Framework 4.8 install check in net48 installer
* fix(net48): correct desktop app and manager paths in app config
* ci(winget): fix WinGet workflow
* docs(wiki): expand Troubleshooting section with additional use cases
* docs(wiki): expand FAQ with more frequently asked questions

### Downloads
* [servy-4.5-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.5/servy-4.5-net48-sbom.xml) - 0.01 MB
* [servy-4.5-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.5/servy-4.5-net48-x64-installer.exe) - 3.91 MB
* [servy-4.5-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.5/servy-4.5-net48-x64-portable.7z) - 1.67 MB
* [servy-4.5-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.5/servy-4.5-sbom.xml) - 0.04 MB
* [servy-4.5-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.5/servy-4.5-x64-installer.exe) - 82.24 MB
* [servy-4.5-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.5/servy-4.5-x64-portable.7z) - 80.13 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v4.5.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v4.5.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v4.4...v4.5

## [Servy 4.4](https://github.com/aelassas/servy/releases/tag/v4.4)

**Date:** 2026-01-01 | **Tag:** [`v4.4`](https://github.com/aelassas/servy/tree/v4.4)

* chore(desktopapp): improve service logon information text
* chore(desktopapp): remove unused `System.ServiceProcess.ServiceController` reference
* chore(cli): improve service user install command help text
* test(tests): add missing unit tests to achieve 100% code coverage
* ci(net48): fix publish and test workflows
* docs(wiki): update Usage, CLI and Troubleshooting documentation
* docs(wiki): expand FAQ with more frequently asked questions

### Downloads
* [servy-4.4-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.4/servy-4.4-net48-sbom.xml) - 0.01 MB
* [servy-4.4-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.4/servy-4.4-net48-x64-installer.exe) - 3.68 MB
* [servy-4.4-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.4/servy-4.4-net48-x64-portable.7z) - 1.66 MB
* [servy-4.4-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.4/servy-4.4-sbom.xml) - 0.04 MB
* [servy-4.4-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.4/servy-4.4-x64-installer.exe) - 82 MB
* [servy-4.4-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.4/servy-4.4-x64-portable.7z) - 80.12 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v4.4.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v4.4.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v4.3...v4.4

## [Servy 4.3](https://github.com/aelassas/servy/releases/tag/v4.3)

**Date:** 2025-12-19 | **Tag:** [`v4.3`](https://github.com/aelassas/servy/tree/v4.3)

* fix(core): upgrade to `System.Data.SQLite` 2.0.2 to ensure database stability and security
* fix(sbom): exclude test projects from SBOMs
* fix(sbom): add Servy version to SBOMs

### Downloads
* [servy-4.3-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.3/servy-4.3-net48-sbom.xml) - 0.01 MB
* [servy-4.3-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.3/servy-4.3-net48-x64-installer.exe) - 3.69 MB
* [servy-4.3-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.3/servy-4.3-net48-x64-portable.7z) - 1.66 MB
* [servy-4.3-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.3/servy-4.3-sbom.xml) - 0.04 MB
* [servy-4.3-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.3/servy-4.3-x64-installer.exe) - 81.98 MB
* [servy-4.3-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.3/servy-4.3-x64-portable.7z) - 80.12 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v4.3.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v4.3.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v4.2...v4.3

## [Servy 4.2](https://github.com/aelassas/servy/releases/tag/v4.2)

**Date:** 2025-12-17 | **Tag:** [`v4.2`](https://github.com/aelassas/servy/tree/v4.2)

* fix(core): encrypt process parameters for maximum security
* fix(core): move process parameters retrieval from binary path to database
* chore: include SBOMs in release artifacts for provenance

### Downloads
* [servy-4.2-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.2/servy-4.2-net48-sbom.xml) - 0.03 MB
* [servy-4.2-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.2/servy-4.2-net48-x64-installer.exe) - 4.48 MB
* [servy-4.2-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.2/servy-4.2-net48-x64-portable.7z) - 2.38 MB
* [servy-4.2-sbom.xml](https://github.com/aelassas/servy/releases/download/v4.2/servy-4.2-sbom.xml) - 0.05 MB
* [servy-4.2-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.2/servy-4.2-x64-installer.exe) - 82.1 MB
* [servy-4.2-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.2/servy-4.2-x64-portable.7z) - 80.25 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v4.2.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v4.2.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v4.1...v4.2

## [Servy 4.1](https://github.com/aelassas/servy/releases/tag/v4.1)

**Date:** 2025-12-16 | **Tag:** [`v4.1`](https://github.com/aelassas/servy/tree/v4.1)

* fix(core): resolve SCM limit for extremely large environment variables (#29)
* fix(core): encrypt environment variables for maximum security

### Downloads
* [servy-4.1-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.1/servy-4.1-net48-x64-installer.exe) - 4.48 MB
* [servy-4.1-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.1/servy-4.1-net48-x64-portable.7z) - 2.38 MB
* [servy-4.1-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.1/servy-4.1-x64-installer.exe) - 82.1 MB
* [servy-4.1-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.1/servy-4.1-x64-portable.7z) - 80.24 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v4.1.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v4.1.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v4.0...v4.1

## [Servy 4.0](https://github.com/aelassas/servy/releases/tag/v4.0)

**Date:** 2025-12-15 | **Tag:** [`v4.0`](https://github.com/aelassas/servy/tree/v4.0)

* chore: officially signed all executables and installers with a trusted SignPath certificate for maximum trust and security
* chore: fix multiple false-positive detections from AV engines (SecureAge, DeepInstinct, and others)
* chore(deps): update dependencies
* feat(core): add max rotations option (#26)
* feat(core): add date-based log rotation (#27)
* feat(desktop): add `Logging` tab for stdout/stderr and logging configuration
* feat(setup): add `Add Servy to PATH` installation option
* feat(setup): add custom installation options for advanced users
* fix(core): update `stdout`/`stderr` rotated file naming to include rotation index as extension
* fix(security): isolate recovery configuration files (#25)
* fix(desktop): add validation to start, stop, and restart service commands
* fix(desktop): improve validation of number input fields
* fix(setup): grant write access to `%ProgramData%\Servy` for Local Service and Network Service accounts
* ci: automate code signing with SignPath and GitHub Actions

### Downloads
* [servy-4.0-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.0/servy-4.0-net48-x64-installer.exe) - 4.48 MB
* [servy-4.0-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.0/servy-4.0-net48-x64-portable.7z) - 2.38 MB
* [servy-4.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v4.0/servy-4.0-x64-installer.exe) - 82.05 MB
* [servy-4.0-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v4.0/servy-4.0-x64-portable.7z) - 80.19 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v4.0.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v4.0.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v3.9...v4.0

## [Servy 3.9](https://github.com/aelassas/servy/releases/tag/v3.9)

**Date:** 2025-11-27 | **Tag:** [`v3.9`](https://github.com/aelassas/servy/tree/v3.9)

* fix: significantly reduce executable and installer sizes (#24)
* ci(choco): update Chocolatey workflow to use the new API
* chore(setup): update build script to keep console window open after failure

### Downloads
* [servy-3.9-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.9/servy-3.9-net48-x64-installer.exe) - 4.46 MB
* [servy-3.9-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v3.9/servy-3.9-net48-x64-portable.7z) - 2.37 MB
* [servy-3.9-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.9/servy-3.9-x64-installer.exe) - 81.98 MB
* [servy-3.9-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v3.9/servy-3.9-x64-portable.7z) - 80.13 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v3.9.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v3.9.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v3.8...v3.9

## [Servy 3.8](https://github.com/aelassas/servy/releases/tag/v3.8)

**Date:** 2025-11-26 | **Tag:** [`v3.8`](https://github.com/aelassas/servy/tree/v3.8)

* fix: reduce executable sizes by optimizing build configurations (#24)
* fix(service): stdout/stderr redirection issues for pre-launch process
* fix(notifications): missing details in email notifications
* fix(setup): correct resource and publish steps in build scripts
* refactor(core): general code improvements and optimizations
* chore(setup): refactor build scripts for consistency and maintainability

### Downloads
* [servy-3.8-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.8/servy-3.8-net48-x64-installer.exe) - 4.46 MB
* [servy-3.8-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v3.8/servy-3.8-net48-x64-portable.7z) - 2.37 MB
* [servy-3.8-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.8/servy-3.8-x64-installer.exe) - 116.47 MB
* [servy-3.8-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v3.8/servy-3.8-x64-portable.7z) - 79.14 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v3.8.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v3.8.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v3.7...v3.8

## [Servy 3.7](https://github.com/aelassas/servy/releases/tag/v3.7)

**Date:** 2025-11-19 | **Tag:** [`v3.7`](https://github.com/aelassas/servy/tree/v3.7)

* fix(core): properly grant "Log on as a service" right for local and Active Directory accounts
* chore(desktop): update service display name info text
* refactor(core): general code improvements and optimizations

### Downloads
* [servy-3.7-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.7/servy-3.7-net48-x64-installer.exe) - 4.48 MB
* [servy-3.7-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v3.7/servy-3.7-net48-x64-portable.7z) - 2.38 MB
* [servy-3.7-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.7/servy-3.7-x64-installer.exe) - 145.98 MB
* [servy-3.7-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v3.7/servy-3.7-x64-portable.7z) - 143.67 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v3.7.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v3.7.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v3.6...v3.7

## [Servy 3.6](https://github.com/aelassas/servy/releases/tag/v3.6)

**Date:** 2025-11-18 | **Tag:** [`v3.6`](https://github.com/aelassas/servy/tree/v3.6)

* feat(core): add service display name
* fix(core): ensure user account has the "Log on as a service" right
* fix(core): ensure event source exists in the desktop app, the cli and the manager app
* fix(about): make .NET runtime and year retrieval automatic
* chore(about): refine about info text for Servy and Servy Manager
* chore(psm): correct `Add-Arg` function description

### Downloads
* [servy-3.6-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.6/servy-3.6-net48-x64-installer.exe) - 4.47 MB
* [servy-3.6-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v3.6/servy-3.6-net48-x64-portable.7z) - 2.38 MB
* [servy-3.6-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.6/servy-3.6-x64-installer.exe) - 146.01 MB
* [servy-3.6-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v3.6/servy-3.6-x64-portable.7z) - 143.67 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v3.6.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v3.6.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v3.5...v3.6

## [Servy 3.5](https://github.com/aelassas/servy/releases/tag/v3.5)

**Date:** 2025-11-16 | **Tag:** [`v3.5`](https://github.com/aelassas/servy/tree/v3.5)

* chore(setup): significantly reduce portable archive sizes for better distribution
* chore(deps): update dependencies
* docs(wiki): add more samples to [examples & recipes](https://github.com/aelassas/servy/wiki/Examples-&-Recipes)

### Downloads
* [servy-3.5-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.5/servy-3.5-net48-x64-installer.exe) - 4.47 MB
* [servy-3.5-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v3.5/servy-3.5-net48-x64-portable.7z) - 2.38 MB
* [servy-3.5-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.5/servy-3.5-x64-installer.exe) - 145.94 MB
* [servy-3.5-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v3.5/servy-3.5-x64-portable.7z) - 143.66 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v3.5.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v3.5.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v3.4...v3.5

## [Servy 3.4](https://github.com/aelassas/servy/releases/tag/v3.4)

**Date:** 2025-11-13 | **Tag:** [`v3.4`](https://github.com/aelassas/servy/tree/v3.4)

* fix(psm): improve argument parsing and handling of optional parameters
* docs(wiki): add more samples to [examples & recipes](https://github.com/aelassas/servy/wiki/Examples-&-Recipes)

### Downloads
* [servy-3.4-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.4/servy-3.4-net48-x64-installer.exe) - 17.15 MB
* [servy-3.4-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v3.4/servy-3.4-net48-x64-portable.zip) - 10.53 MB
* [servy-3.4-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.4/servy-3.4-x64-installer.exe) - 145.96 MB
* [servy-3.4-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v3.4/servy-3.4-x64-portable.zip) - 362.22 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v3.4.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v3.4.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v3.3...v3.4

## [Servy 3.3](https://github.com/aelassas/servy/releases/tag/v3.3)

**Date:** 2025-11-12 | **Tag:** [`v3.3`](https://github.com/aelassas/servy/tree/v3.3)

* chore: upgrade to .NET 10 LTS
* chore(deps): update dependencies
* fix(restarter): increase service stop and start timeouts to 120 seconds
* fix(service): improve service restart wait handling and logging
* docs(wiki): update documentation

### Downloads
* [servy-3.3-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.3/servy-3.3-net48-x64-installer.exe) - 17.15 MB
* [servy-3.3-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v3.3/servy-3.3-net48-x64-portable.zip) - 10.52 MB
* [servy-3.3-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.3/servy-3.3-x64-installer.exe) - 145.93 MB
* [servy-3.3-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v3.3/servy-3.3-x64-portable.zip) - 362.22 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v3.3.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v3.3.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v3.2...v3.3

## [Servy 3.2](https://github.com/aelassas/servy/releases/tag/v3.2)

**Date:** 2025-11-10 | **Tag:** [`v3.2`](https://github.com/aelassas/servy/tree/v3.2)

* fix(restarter): add detailed error logs for service restart failures (#23)
* chore: update info message about service account permissions (#23)
* chore: add info message about recovery permissions (#23)

### Downloads
* [servy-3.2-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.2/servy-3.2-net48-x64-installer.exe) - 17.15 MB
* [servy-3.2-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v3.2/servy-3.2-net48-x64-portable.zip) - 10.52 MB
* [servy-3.2-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.2/servy-3.2-x64-installer.exe) - 135.87 MB
* [servy-3.2-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v3.2/servy-3.2-x64-portable.zip) - 340.59 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v3.2.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v3.2.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v3.1...v3.2

## [Servy 3.1](https://github.com/aelassas/servy/releases/tag/v3.1)

**Date:** 2025-11-09 | **Tag:** [`v3.1`](https://github.com/aelassas/servy/tree/v3.1)

* fix(core): support NetworkService, LocalService, and passwordless accounts (#23)

### Downloads
* [servy-3.1-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.1/servy-3.1-net48-x64-installer.exe) - 17.15 MB
* [servy-3.1-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v3.1/servy-3.1-net48-x64-portable.zip) - 10.52 MB
* [servy-3.1-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.1/servy-3.1-x64-installer.exe) - 137.21 MB
* [servy-3.1-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v3.1/servy-3.1-x64-portable.zip) - 338.11 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v3.1.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v3.1.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v3.0...v3.1

## [Servy 3.0](https://github.com/aelassas/servy/releases/tag/v3.0)

**Date:** 2025-11-08 | **Tag:** [`v3.0`](https://github.com/aelassas/servy/tree/v3.0)

* feat(core): add support for automatic (delayed) service startup type
* fix(core): properly escape special characters in process parameters (#22)
* fix(core): switch to network logon since some domain accounts don't allow interactive logon
* fix(core): allow valid characters in DOMAIN\Username logon
* fix(core): improve logon validation for gMSA accounts
* fix(manager): validate credentials when importing a service
* chore: add [project manifest](https://github.com/aelassas/servy/blob/main/MANIFEST.md)

### Downloads
* [servy-3.0-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.0/servy-3.0-net48-x64-installer.exe) - 17.15 MB
* [servy-3.0-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v3.0/servy-3.0-net48-x64-portable.zip) - 10.52 MB
* [servy-3.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v3.0/servy-3.0-x64-installer.exe) - 137.2 MB
* [servy-3.0-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v3.0/servy-3.0-x64-portable.zip) - 338.1 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v3.0.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v3.0.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v2.9...v3.0

## [Servy 2.9](https://github.com/aelassas/servy/releases/tag/v2.9)

**Date:** 2025-10-30 | **Tag:** [`v2.9`](https://github.com/aelassas/servy/tree/v2.9)

* fix(service): stdout/stderr pipes lost after sending Ctrl+C signal to child process (#20)

### Downloads
* [servy-2.9-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.9/servy-2.9-net48-x64-installer.exe) - 17.15 MB
* [servy-2.9-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.9/servy-2.9-net48-x64-portable.zip) - 10.52 MB
* [servy-2.9-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.9/servy-2.9-x64-installer.exe) - 137.21 MB
* [servy-2.9-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.9/servy-2.9-x64-portable.zip) - 338.1 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v2.9.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v2.9.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v2.8...v2.9

## [Servy 2.8](https://github.com/aelassas/servy/releases/tag/v2.8)

**Date:** 2025-10-27 | **Tag:** [`v2.8`](https://github.com/aelassas/servy/tree/v2.8)

* fix(core): escape environment variables correctly when formatting after import
* fix(restarter): publish script command fix

### Downloads
* [servy-2.8-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.8/servy-2.8-net48-x64-installer.exe) - 17.15 MB
* [servy-2.8-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.8/servy-2.8-net48-x64-portable.zip) - 10.52 MB
* [servy-2.8-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.8/servy-2.8-x64-installer.exe) - 137.19 MB
* [servy-2.8-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.8/servy-2.8-x64-portable.zip) - 338.1 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v2.8.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v2.8.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v2.7...v2.8

## [Servy 2.7](https://github.com/aelassas/servy/releases/tag/v2.7)

**Date:** 2025-10-26 | **Tag:** [`v2.7`](https://github.com/aelassas/servy/tree/v2.7)

* chore(winget,chocolatey): update manifests

### Downloads
* [servy-2.7-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.7/servy-2.7-net48-x64-installer.exe) - 17.15 MB
* [servy-2.7-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.7/servy-2.7-net48-x64-portable.zip) - 10.52 MB
* [servy-2.7-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.7/servy-2.7-x64-installer.exe) - 137.18 MB
* [servy-2.7-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.7/servy-2.7-x64-portable.zip) - 338.1 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v2.7.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v2.7.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v2.6...v2.7

## [Servy 2.6](https://github.com/aelassas/servy/releases/tag/v2.6)

**Date:** 2025-10-25 | **Tag:** [`v2.6`](https://github.com/aelassas/servy/tree/v2.6)

* fix(service): resolve stdout/stderr UTF-8 encoding issues (#20)
* fix(manager): ensure service list is sorted correctly
* fix(service): remove unnecessary logs

### Downloads
* [servy-2.6-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.6/servy-2.6-net48-x64-installer.exe) - 17.15 MB
* [servy-2.6-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.6/servy-2.6-net48-x64-portable.zip) - 10.52 MB
* [servy-2.6-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.6/servy-2.6-x64-installer.exe) - 137.19 MB
* [servy-2.6-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.6/servy-2.6-x64-portable.zip) - 338.1 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v2.6.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v2.6.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v2.5...v2.6

## [Servy 2.5](https://github.com/aelassas/servy/releases/tag/v2.5)

**Date:** 2025-10-23 | **Tag:** [`v2.5`](https://github.com/aelassas/servy/tree/v2.5)

* fix(service): correctly display non-ASCII characters in `stdout/stderr` (#20)
* fix(recovery): allow graceful restart

### Downloads
* [servy-2.5-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.5/servy-2.5-net48-x64-installer.exe) - 17.14 MB
* [servy-2.5-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.5/servy-2.5-net48-x64-portable.zip) - 10.52 MB
* [servy-2.5-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.5/servy-2.5-x64-installer.exe) - 137.18 MB
* [servy-2.5-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.5/servy-2.5-x64-portable.zip) - 338.1 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v2.5.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v2.5.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v2.4...v2.5

## [Servy 2.4](https://github.com/aelassas/servy/releases/tag/v2.4)

**Date:** 2025-10-21 | **Tag:** [`v2.4`](https://github.com/aelassas/servy/tree/v2.4)

* fix(service): incorrect process termination (#20)
* fix(logging): add [Event ID](https://github.com/aelassas/servy/wiki/Logging-&-Log-Rotation#event-ids) to Info, Warning, and Error service logs
* fix(core): ensure PID is preserved after importing a running service
* fix(core): exclude service Id and PID from XML and JSON exports
* ci(sonar): add SonarCloud analysis workflow
* ci(release): add [release](https://github.com/aelassas/servy/blob/main/.github/workflows/release.yml) workflow
* refactor: general refactoring and code cleanup

### Downloads
* [servy-2.4-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.4/servy-2.4-net48-x64-installer.exe) - 17.13 MB
* [servy-2.4-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.4/servy-2.4-net48-x64-portable.zip) - 10.51 MB
* [servy-2.4-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.4/servy-2.4-x64-installer.exe) - 137.2 MB
* [servy-2.4-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.4/servy-2.4-x64-portable.zip) - 338.09 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v2.4.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v2.4.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v2.3...v2.4

## [Servy 2.3](https://github.com/aelassas/servy/releases/tag/v2.3)

**Date:** 2025-10-13 | **Tag:** [`v2.3`](https://github.com/aelassas/servy/tree/v2.3)

* fix(security): add debug logging option and prevent sensitive data exposure (#19)
* fix(psm1): handle parameters correctly in PowerShell module to avoid argument misparsing (#18)
* fix(cli): correct help text for environment variables
* fix(packaging): include PowerShell module in portable ZIP
* docs(psm1): update documentation and examples for PowerShell module
* chore: resolve false positives in SecureAge and Gridinsoft

### Downloads
* [servy-2.3-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.3/servy-2.3-net48-x64-installer.exe) - 17.21 MB
* [servy-2.3-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.3/servy-2.3-net48-x64-portable.zip) - 10.53 MB
* [servy-2.3-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.3/servy-2.3-x64-installer.exe) - 137.15 MB
* [servy-2.3-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.3/servy-2.3-x64-portable.zip) - 338.08 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v2.3.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v2.3.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v2.2...v2.3

## [Servy 2.2](https://github.com/aelassas/servy/releases/tag/v2.2)

**Date:** 2025-10-09 | **Tag:** [`v2.2`](https://github.com/aelassas/servy/tree/v2.2)

* feat(manager): enhance DataGrid virtualization and performance
* fix(manager): prevent resizing DataGrid rows
* fix(service): service install not working when Windows is installed on a drive letter other than `C:` #16
* fix(service): reorganize and clean up startup parameters log

### Downloads
* [servy-2.2-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.2/servy-2.2-net48-x64-installer.exe) - 17.22 MB
* [servy-2.2-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.2/servy-2.2-net48-x64-portable.zip) - 10.52 MB
* [servy-2.2-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.2/servy-2.2-x64-installer.exe) - 137.21 MB
* [servy-2.2-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.2/servy-2.2-x64-portable.zip) - 338.07 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v2.2.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v2.2.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v2.1...v2.2

## [Servy 2.1](https://github.com/aelassas/servy/releases/tag/v2.1)

**Date:** 2025-10-09 | **Tag:** [`v2.1`](https://github.com/aelassas/servy/tree/v2.1)

* fix(clients): `Servy.Service.exe` not copied when Windows is installed on a drive letter other than `C:` #16
* fix(clients): load `appsettings.json` from project root in debug and exe directory in release #16
* fix(configurator): export XML and JSON not working when password is supplied

### Downloads
* [servy-2.1-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.1/servy-2.1-net48-x64-installer.exe) - 17.22 MB
* [servy-2.1-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.1/servy-2.1-net48-x64-portable.zip) - 10.52 MB
* [servy-2.1-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.1/servy-2.1-x64-installer.exe) - 137.21 MB
* [servy-2.1-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.1/servy-2.1-x64-portable.zip) - 338.07 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v2.1.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v2.1.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v2.0...v2.1

## [Servy 2.0](https://github.com/aelassas/servy/releases/tag/v2.0)

**Date:** 2025-10-07 | **Tag:** [`v2.0`](https://github.com/aelassas/servy/tree/v2.0)

* fix(clients): prevent loading stray `appsettings.json` at runtime #16
* fix(core): robust and accurate CPU usage calculation
* fix(service): avoid stopping services when copying `Servy.Restarter.exe` from resources
* fix(service): add missing log for post-launch options

### Downloads
* [servy-2.0-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.0/servy-2.0-net48-x64-installer.exe) - 17.22 MB
* [servy-2.0-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.0/servy-2.0-net48-x64-portable.zip) - 10.52 MB
* [servy-2.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v2.0/servy-2.0-x64-installer.exe) - 137.21 MB
* [servy-2.0-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v2.0/servy-2.0-x64-portable.zip) - 338.07 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v2.0.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v2.0.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v1.9...v2.0

## [Servy 1.9](https://github.com/aelassas/servy/releases/tag/v1.9)

**Date:** 2025-10-04 | **Tag:** [`v1.9`](https://github.com/aelassas/servy/tree/v1.9)

* fix(manager): adjust service list column widths when maximizing window
* chore(release): add generic installer
* chore: update dependencies

### Downloads
* [servy-1.9-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.9/servy-1.9-net48-x64-installer.exe) - 17.22 MB
* [servy-1.9-net48-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v1.9/servy-1.9-net48-x64-portable.zip) - 10.52 MB
* [servy-1.9-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.9/servy-1.9-x64-installer.exe) - 137.15 MB
* [servy-1.9-x64-portable.zip](https://github.com/aelassas/servy/releases/download/v1.9/servy-1.9-x64-portable.zip) - 338.07 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v1.9.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v1.9.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v1.8...v1.9

## [Servy 1.8](https://github.com/aelassas/servy/releases/tag/v1.8)

**Date:** 2025-10-01 | **Tag:** [`v1.8`](https://github.com/aelassas/servy/tree/v1.8)

* fix(core): using the same file for `stdout` and `stderr` prevents log rotation #14
* fix(installer): manager and cli apps not killed on uninstall
* fix(core): use `ConcurrentDictionary` for thread-safe CPU usage tracking of services
* fix(core): prevent `NullReferenceException` in CPU usage retrieval
* fix(core): optimize resource copying for better performance
* fix(manager): escape `\`, `%` and `_` in services search to match literals
* fix(manager): lower services refresh interval to 4 seconds
* fix(manager): enforce blue background and white text for selected DataGrid rows
* chore(installer): fix false positives in VirusTotal, Microsoft Defender, SecureAge, and Zillya
* chore(installer): add Servy to Scoop `extras` bucket

### Downloads
* [servy-1.8-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.8/servy-1.8-net48-x64-installer.exe) - 17.22 MB
* [servy-1.8-net8.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.8/servy-1.8-net8.0-x64-installer.exe) - 137.16 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v1.8.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v1.8.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v1.7...v1.8

## [Servy 1.7](https://github.com/aelassas/servy/releases/tag/v1.7)

**Date:** 2025-09-29 | **Tag:** [`v1.7`](https://github.com/aelassas/servy/tree/v1.7)

* feat(manager): add real-time CPU & RAM monitoring for services
* feat(manager): add PID column and Copy PID action to services
* fix(core): optimize resource copying for better performance
* fix(core): reliably update service without checking Win32 error
* fix(manager): performance issues on UI thread when loading and updating services
* fix(manager): adjust actions column width in services
* fix(manager): load configuration from appsettings.manager.json
* fix(configurator): load configuration from appsettings.json
* fix(cli): load configuration from appsettings.cli.json
* fix(cli): correct help text for `--preLaunchEnv` argument in `install` command
* chore: update dependencies

### Downloads
* [servy-1.7-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.7/servy-1.7-net48-x64-installer.exe) - 4.44 MB
* [servy-1.7-net8.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.7/servy-1.7-net8.0-x64-installer.exe) - 137.21 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v1.7.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v1.7.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v1.6...v1.7

## [Servy 1.6](https://github.com/aelassas/servy/releases/tag/v1.6)

**Date:** 2025-09-23 | **Tag:** [`v1.6`](https://github.com/aelassas/servy/tree/v1.6)

* feat(core): add support for [post-launch actions](https://github.com/aelassas/servy/wiki/Pre‐Launch-&-Post‐Launch-Actions#post-launch)
* feat(installer): add Servy to [Scoop package manager](https://github.com/aelassas/servy/wiki/Installation-Guide#quick-install)
* fix(configurator): increase tab height for better visibility
* fix(installer): adjust LZMA dictionary size to 64MB to reduce memory usage during install

### Downloads
* [servy-1.6-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.6/servy-1.6-net48-x64-installer.exe) - 4.18 MB
* [servy-1.6-net8.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.6/servy-1.6-net8.0-x64-installer.exe) - 137.3 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v1.6.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v1.6.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v1.5...v1.6

## [Servy 1.5](https://github.com/aelassas/servy/releases/tag/v1.5)

**Date:** 2025-09-20 | **Tag:** [`v1.5`](https://github.com/aelassas/servy/tree/v1.5)

* fix(installer): resolve false positives from VirusTotal and Microsoft Defender
* fix(installer): increase LZMA dictionary size to 128MB

### Downloads
* [servy-1.5-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.5/servy-1.5-net48-x64-installer.exe) - 4.18 MB
* [servy-1.5-net8.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.5/servy-1.5-net8.0-x64-installer.exe) - 104.58 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v1.5.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v1.5.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v1.4...v1.5

## [Servy 1.4](https://github.com/aelassas/servy/releases/tag/v1.4)

**Date:** 2025-09-18 | **Tag:** [`v1.4`](https://github.com/aelassas/servy/tree/v1.4)

* fix(cli,powershell): add `--quiet` and `-q` options to CLI and `-Quiet` switch to PowerShell module #11
* fix(cli): rotation size calculation
* fix(configurator): make failure program path optional instead of required

### Downloads
* [servy-1.4-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.4/servy-1.4-net48-x64-installer.exe) - 4.18 MB
* [servy-1.4-net8.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.4/servy-1.4-net8.0-x64-installer.exe) - 137.32 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v1.4.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v1.4.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v1.3...v1.4

## [Servy 1.3](https://github.com/aelassas/servy/releases/tag/v1.3)

**Date:** 2025-09-18 | **Tag:** [`v1.3`](https://github.com/aelassas/servy/tree/v1.3)

* feat(core): add failure program recovery action
* feat(core): add support for gMSA accounts
* feat(cli): add [Servy PowerShell module](https://github.com/aelassas/servy/wiki/Servy-PowerShell-Module)
* feat(configurator,cli): change rotation size unit from bytes to megabytes (MB) for improved readability [#10](https://github.com/aelassas/servy/issues/10)
* fix(configurator,cli): ensure rotation and health check default values are persisted correctly
* fix(configurator): correct confirm password validation
* chore: update dependencies

### Downloads
* [servy-1.3-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.3/servy-1.3-net48-x64-installer.exe) - 4.18 MB
* [servy-1.3-net8.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.3/servy-1.3-net8.0-x64-installer.exe) - 137.29 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v1.3.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v1.3.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v1.2...v1.3

## [Servy 1.2](https://github.com/aelassas/servy/releases/tag/v1.2)

**Date:** 2025-09-15 | **Tag:** [`v1.2`](https://github.com/aelassas/servy/tree/v1.2)

* fix(cli): support both `version` and `--version` arguments
* fix(cli): update `--rotationSize` help text for install command
* fix(pre-launch): add missing info text and row in configurator
* fix(manager): refine column alignment and layout in services and logs grids
* fix(manager): freeze widths of checkbox and context menu columns
* fix(manager): prevent moving columns while allowing resize
* fix(manager): enable sorting for Name and Description columns in services grid
* fix(manager): prevent columns from shrinking below min width in services grid
* fix(installer): missing icons

### Downloads
* [servy-1.2-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.2/servy-1.2-net48-x64-installer.exe) - 4.05 MB
* [servy-1.2-net8.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.2/servy-1.2-net8.0-x64-installer.exe) - 145.2 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v1.2.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v1.2.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v1.1...v1.2

## [Servy 1.1](https://github.com/aelassas/servy/releases/tag/v1.1)

**Date:** 2025-09-11 | **Tag:** [`v1.1`](https://github.com/aelassas/servy/tree/v1.1)

* fix(cli): bump `Microsoft.Extensions.Configuration.Json` from 9.0.8 to 8.0.1
* fix(core): remove unused `EntityFramework` package
* fix(installer): missing icons on silent install
* chore: update dependencies

### Downloads
* [servy-1.1-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.1/servy-1.1-net48-x64-installer.exe) - 4.05 MB
* [servy-1.1-net8.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.1/servy-1.1-net8.0-x64-installer.exe) - 137.28 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v1.1.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v1.1.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v1.0...v1.1

## [Servy 1.0](https://github.com/aelassas/servy/releases/tag/v1.0)

**Date:** 2025-09-06 | **Tag:** [`v1.0`](https://github.com/aelassas/servy/tree/v1.0)

The first official release of **Servy**!

Servy is a free, open-source Windows tool (GUI + CLI) that lets you run any executable as a Windows service with powerful configuration and management options.

### Features
* Clean, simple UI
* Quickly monitor and manage all installed services with Servy Manager
* CLI for full scripting and automated deployments
* Run any executable as a Windows service
* Set service name, description, startup type, priority, working directory, environment variables, dependencies, and parameters
* Environment variable expansion supported in both environment variables and process parameters
* Run services as Local System, local user, or domain account
* Redirect stdout/stderr to log files with automatic size-based rotation
* Run pre-launch script execution before starting the service, with retries, timeout, logging and failure handling
* Prevent orphaned/zombie processes with improved lifecycle management and ensuring resource cleanup
* Health checks and automatic service recovery
* Monitor and manage services in real-time
* Browse and search logs by level, date, and keyword for faster troubleshooting from Servy Manager
* Export/Import service configurations
* Service Event Notification alerts on service failures via Windows notifications and email
* Compatible with Windows 7–11 x64 and Windows Server editions

### Requirements

#### .NET 8.0 Version
- Recommended for modern systems and includes the latest features and performance enhancements.
- Published as a self-contained executable and does not require installing the .NET 8.0 Desktop Runtime separately.
- Supported OS: Windows 10, 11, or Windows Server (x64).

#### .NET Framework 4.8 Version
- For older systems that require .NET Framework support.
- Supported OS: Windows 7, 8, 10, 11, or Windows Server (x64).
- Requires installation of the [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net48-web-installer).

### Quick Install
You have two options to install Servy. Download and [install manually](https://github.com/aelassas/servy/wiki/Installation-Guide#manual-download-and-install) or use a package manager such as WinGet or Chocolatey.

Make sure you have [WinGet](https://learn.microsoft.com/en-us/windows/package-manager/winget/) or [Chocolatey](https://chocolatey.org/install) installed.

Run one of the following commands as administrator from Command Prompt or PowerShell:

**WinGet**
```powershell
winget install servy
```

**Chocolatey**
```powershell
choco install -y servy
```

### Downloads
* [servy-1.0-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.0/servy-1.0-net48-x64-installer.exe) - 3.99 MB
* [servy-1.0-net8.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v1.0/servy-1.0-net8.0-x64-installer.exe) - 138.14 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v1.0.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v1.0.tar.gz)

