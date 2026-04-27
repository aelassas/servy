# Changelog

## [Servy 8.3](https://github.com/aelassas/servy/releases/tag/v8.3)

**Date:** 2026-04-27 | **Tag:** [`v8.3`](https://github.com/aelassas/servy/tree/v8.3)

Servy 8.3 improves the UI experience and includes many fixes. The full changelog is available below.

## Full Changelog
<details>
  <summary>Click to expand release notes!</summary>

* fix(service): Console.CursorVisible causes service crash when running console app as Windows service (#814)
* fix(core): ImportServiceCommandTests - MockXmlValidator/MockJsonValidator ignore isValid parameter (#738)
* fix(core): implement fine-grained per-PID locking and atomic cache pruning in ProcessHelper (#796)
* fix(core): ServiceManager.UninstallServiceAsync - stop-wait loop ignores cancellation, blocks up to ServiceStopTimeoutSeconds (#798)
* fix(core): HandleHelper.cs - regex timeout hardcoded to 1s, bypasses AppConfig.InputRegexTimeout convention (#801)
* fix(core): ServiceValidationRules.cs - Service name not validated against SCM-forbidden characters (\, /) (#806)
* fix(core): ServiceManager.cs - ServiceStopTimeoutSeconds hardcoded as a local const, should live in AppConfig (#809)
* fix(core): ServiceDto.cs - ShouldSerializEnableSizeRotation typo (missing 'e') breaks conditional serialization of EnableSizeRotation (#810)
* fix(core): ServiceManager.GetAllServices - Parallel.ForEach has no per-service timeout, single hung SCM RPC blocks a worker (#819)
* fix(core): ProcessKiller.CriticalSystemProcesses - incomplete safelist; killing dwm/MsMpEng/audiodg/fontdrvhost can destabilize the host (#825)
* fix(core): ProcessHelper.GetProcessTreeMetrics - caps summed CPU at 100%, hides multi-core saturation (#829)
* fix(core): ProcessHelper.EscapeArgument - fails to double backslashes that precede an internal quote (Win32 CommandLineToArgvW corruption) (#827)
* fix(core): Logger.cs - MaxBackupFiles is hardcoded to 0 (unlimited), no admin override path (#838)
* fix(core): SecureData.Decrypt - on crypto failure of marked payload, returns the post-marker payload (not the documented 'original cipherText') (#848)
* fix(core): ProtectedKeyProvider.GetOrGenerate - comment claims 'exponential backoff' but Thread.Sleep math is linear (#852)
* fix(core): ProcessHelper.cs - EscapeProcessArgument is dead code; only the broken EscapeArgument is called (#857)
* fix(core): ProtectedKeyProvider.SaveProtected - WindowsIdentity from GetCurrent() is never disposed (handle leak per save) (#861)
* fix(core): SecurityHelper.CreateSecureDirectory - WindowsIdentity from GetCurrent() never disposed (handle leak per call) (#868)
* fix(core): LogonAsServiceGrant.Ensure - InvalidOperationException loses the underlying SID-resolution failure cause (#874)
* fix(core): ResourceHelper + ServiceExporter - non-atomic file writes can leave truncated/partial files on crash, mid-copy interruption, or disk full (#875)
* fix(core): SecurityHelper.ApplySecurityRules — comparing the current user's SID to BuiltinAdministrators/LocalSystem GROUP SIDs is meaningless (#933)
* fix(core): ProcessKiller.KillProcessTreeAndParents(string) - silent catch-all swallows ALL errors with no logging (#935)
* fix(core): ProcessKiller.KillProcessTree - uses stale 'allProcesses' snapshot during recursion; processes spawned mid-walk survive (#936)
* fix(core): ResourceHelper.TerminateBlockingProcesses - for .exe resources, kills ALL processes matching the filename, including unrelated instances belonging to other services (#938)
* fix(core): Logger.Log - bare 'catch { /* Fail-silent */ }' on the write path swallows every I/O exception with no fallback channel (#940)
* fix(core): ServiceManager.GetAllServices - orphaned PopulateNativeDetails task races with results consumer and uses scmHandle after dispose (#965)
* fix(core): EventLogReader.ReadEvents - materializes the entire result set in memory before MaxResults limit can apply (#970)
* fix(core): EventLogLogger.Info/Warn/Error - EventLog.WriteEntry calls have no try/catch and no length truncation (32766-char limit) (#971)
* fix(core): ServiceControllerWrapper.BuildDependencyTree - bare catch swallows every error with no logging, leaving '(Unavailable)' nodes indistinguishable (#972)
* fix(infra): ServiceRepository.cs - XmlSerializer instantiated per export, leaks dynamic assemblies (#816)
* fix(infra): ServiceRepository.UpsertBatchAsync - IN clause uses default BINARY collation while upsert key is LOWER(Name); ID sync misses case-mismatched rows (#820)
* fix(infra): ServiceRepository.SearchAsync - Name LIKE `@Pattern` is case-sensitive for non-ASCII characters; rest of the repo uses LOWER(...) = LOWER(...) (#881)
* fix(service): ProcessWrapper.cs - CancelOutputRead/CancelErrorRead missing ThrowIfDisposed check (#800)
* fix(service): ServiceHelper.cs - duplicate using Servy.Core.Config directive (#802)
* fix(service): CheckHealth handler never -= from _healthCheckTimer before Dispose, delegate leak on each restart cycle (#803)
* fix(service): fileSemaphore and _healthCheckSemaphore never disposed, kernel handle leak on teardown (#807)
* fix(service): ProcessLauncher.ApplyLanguageFixes - substring match on 'python'/'java' in FileName produces false positives (#834)
* fix(service): ProcessWrapper.WaitUntilRunningAsync - always waits the full timeout, never returns 'running' early (#858)
* fix(service): ProcessLauncher.Start - TimeoutMs silently ignored when OnScmHeartbeat is null (#860)
* fix(service): truncated XML doc tag '/// \<pa' on the unit-test constructor (#889)
* fix(service): ServiceHelper - SensitiveKeyWords list and MaskingRegex pattern are out of sync (CREDENTIAL / CONNECTIONSTRING / CERTIFICATE missing from regex) (#896)
* fix(service): ServiceHelper.MaskSensitiveValue - substring-based keyword match flags non-secret env-var names (e.g. MONKEY, APIPATH, PRIVATELY) (#929)
* fix(service): ProcessLauncher.Start - synchronous launch with TimeoutMs == 0 falls into unbounded WaitForExit() and can hang the service (#952)
* fix(service): ProcessExtensions.GetChildren / GetAllDescendants - bare catch swallows ALL errors with no logging (parallel to #935) (#975)
* fix(service): ProcessLaunchOptions.TimeoutMs - XML doc says 0 = infinite wait but ProcessLauncher.Start throws ArgumentException when TimeoutMs \<= 0 (#976)
* fix(desktop): add None date rotation type (#812)
* fix(desktop,manager): all ViewModels coupled to Application.Current - cannot instantiate in tests (#429)
* fix(desktop,manager): ViewModels directly use Mouse.OverrideCursor - fails without WPF context (#431)
* fix(desktop,manager): FileSystemWatcher event handlers never unsubscribed before Dispose (#804)
* fix(desktop,manager): AsyncCommand.Execute - async void with no try/catch propagates exceptions to the WPF dispatcher and can crash the UI (#866)
* fix(desktop,manager): MainWindow.OnClosed (Servy + Manager) - Process.GetCurrentProcess() handle never disposed (#873)
* fix(manager): ServiceCommands.cs - SearchServicesAsync does not filter nulls returned by ToModelAsync (#797)
* fix(manager): ProcessHelper calls made without _metricsLock held, inconsistent with MainViewModel (#799)
* fix(manager): ConsoleViewModel / PerformanceViewModel - StopMonitoring hides base virtual method instead of overriding it (#808)
* fix(manager): MonitoringViewModelBase.OnTick - async void handler does not wrap OnTickAsync in try/catch; an unhandled exception will crash the WPF dispatcher (#982)
* fix(cli): ExportServiceCommand.cs - new Uri(fullPath) throws UriFormatException on legitimate paths (#815)
* fix(cli,psm1): DateRotationType.None missing from documented enum values (#812)
* fix(cli):  ExportServiceCommand - ReservedPortRegex misses COM0/LPT0 (and Unicode-superscript COM¹/LPT¹/etc.) per current Microsoft naming docs (#900)
* fix(psm1): Servy.psd1 - AliasesToExport declared twice; line 85 empty array overwrites line 76 entries (#811)
* fix(psm1): Assert-Administrator - WindowsIdentity from GetCurrent() never disposed (handle leak per call) (#864)
* fix(psm1): Invoke-ServyCli - finally block calls process.WaitForExit() with no timeout; if Kill() failed, PowerShell hangs forever (#879)
* fix(psm1): Install-ServyService - dead 'if ($paramName -eq "Password") { continue }' guard (no '--password' entry exists in $paramMapping) (#891)
* fix(tests): ProcessHelper and ProcessKiller are static - cannot be mocked (#430)
* fix(tests): multiple test files - Mock-only tests verify Moq dispatch, not production code (#552)
* fix(tests): TestableService - 16 reflection hops into Service private members with silent null-forgiving (#742)
* fix(tests): test fixtures - 13 occurrences of maintainer-specific Python path (C:\Users\aelassas\...) (#753)
* fix(iss): NumericVersion drops patch component, mis-classifies upgrade vs reinstall (#817)
* fix(publish): 8 publish-res scripts are structurally identical (~800 lines) (#406)
* fix(publish): signpath.ps1 - API token stored in plaintext file with no ACL guidance (#583)
* fix(notifications): Get-ServyLastErrors.ps1 - Get-WinEvent -FilterHashtable requires PS 3.0+, contradicts script's "PowerShell 2.0 or later" header (#805)
* fix(notifications): ServyFailureEmail.ps1 --claims PowerShell 2.0 compatibility but dot-sources a script that #Requires -Version 3.0 (#836)
* fix(notifications): ServyFailureEmail.ps1 / ServyFailureNotification.ps1 - masking regex lacks word boundaries, can mangle non-secret content (#837)
* fix(notifications): ServyFailureEmail.ps1 / Get-ServyLastErrors.ps1 - DateTime.Parse on watermark/parameters is culture-sensitive while the file is written invariant ISO 8601 (#863)
* fix(notifications): ServyFailureEmail.ps1 - SmtpClient is never disposed; comment about 'PS 2.0 / .NET 3.5' is incorrect (#884)
* fix(notifications): Get-ServyLastErrors.ps1 - fallback log file is named 'ServyFailureEmail.log' (copy-paste from sibling script) (#926)
* fix(notifications): ServyFailureEmail.ps1 and ServyFailureNotification.ps1 - concurrent watermark re-read uses bare [DateTime]::Parse instead of ParseExact, can silently misinterpret Kind under non-en-US culture (#945)
* fix(bump-runtime): TFM regex 'net\d+\.\d+' lacks word boundary, can match inside larger tokens (#844)
* ci(bump-version): ValidatePattern rejects 3-segment versions but help text claims they are supported (#843)
* ci(bump-version): silently 'succeeds' when regex pattern fails to match (no replacements) (#885)
* ci(scoop): gh CLI authenticates via GH_PAT but gh reads GH_TOKEN; merged-PR duplicate check is a silent no-op (#886)
* ci(choco): retry loop around 'git push' never retries because PowerShell try/catch does not catch non-zero exit from native commands (#887)
* chore(deps): updated dependencies

</details>

### Downloads
* [servy-8.3-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.3/servy-8.3-net48-sbom.xml) - 0.02 MB
* [servy-8.3-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.3/servy-8.3-net48-x64-installer.exe) - 4.07 MB
* [servy-8.3-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.3/servy-8.3-net48-x64-portable.7z) - 1.82 MB
* [servy-8.3-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.3/servy-8.3-sbom.xml) - 0.04 MB
* [servy-8.3-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.3/servy-8.3-x64-installer.exe) - 81.14 MB
* [servy-8.3-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.3/servy-8.3-x64-portable.7z) - 79.01 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v8.3.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v8.3.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v8.2...v8.3

## [Servy 8.2](https://github.com/aelassas/servy/releases/tag/v8.2)

**Date:** 2026-04-24 | **Tag:** [`v8.2`](https://github.com/aelassas/servy/tree/v8.2)

* fix(manager): CPU, RAM and PID resource monitoring shows "N/A" or frozen values for services (#796)
* fix(manager): set PID to N/A when service is uninstalled
* fix(core): ServiceValidationRules.cs - Missing Name/ExecutablePath reported as Warnings instead of Errors (#785)
* fix(core): RotatingStreamWriter.cs - Constructor captures _useLocalTimeForRotation before it is assigned (#791)
* fix(service): TimerAdapter.cs - Disposed-state check bypassed on event/property accessors (#786)
* fix(restarter): AppDbContext created but never disposed (#792)
* ci(setup-dotnet): dotnet-install.ps1 downloaded and executed without signature or hash verification (#787)
* ci(global.json): rollForward: latestPatch weakens build reproducibility (#790)
* ci(publish): CycloneDX CLI, Inno Setup, and 7-Zip installers downloaded and executed without integrity verification (#793)

### Downloads
* [servy-8.2-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.2/servy-8.2-net48-sbom.xml) - 0.02 MB
* [servy-8.2-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.2/servy-8.2-net48-x64-installer.exe) - 4.02 MB
* [servy-8.2-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.2/servy-8.2-net48-x64-portable.7z) - 1.76 MB
* [servy-8.2-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.2/servy-8.2-sbom.xml) - 0.03 MB
* [servy-8.2-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.2/servy-8.2-x64-installer.exe) - 81.04 MB
* [servy-8.2-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.2/servy-8.2-x64-portable.7z) - 78.91 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v8.2.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v8.2.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v8.1...v8.2

## [Servy 8.1](https://github.com/aelassas/servy/releases/tag/v8.1)

**Date:** 2026-04-22 | **Tag:** [`v8.1`](https://github.com/aelassas/servy/tree/v8.1)

Servy 8.1 introduces many fixes across main components. The full release notes are available in the expandable section below.

## Full Changelog
<details>
  <summary>Click to expand release notes!</summary>

* fix(core): Child folder ACLs in %ProgramData%\Servy break multi-account setups (#725)
* fix(core): mixed string empty checks: IsNullOrWhiteSpace vs IsNullOrEmpty used interchangeably (#397)
* fix(core): Multiple files - Process.MainModule!.FileName! pattern repeated in 7 locations (follow-up to #724) (#757)
* fix(core): RotatingStreamWriter.cs - Synchronous Thread.Sleep retries during rotation stall stdout/stderr capture (#761)
* fix(core): Post-launch hook missing EnvironmentVariables / Stdout / Stderr / Timeout / Retry / IgnoreFailure — asymmetric with pre-launch (#762)
* fix(core): Servy Event Log - C# code (1000/2000/3000) and PowerShell scripts (9901/9903) use disjoint Event ID ranges on the same source (#764)
* fix(core): AppConfig.cs - ConfigurationAppPublishDebugPath and ManagerAppPublishDebugPath point to Release folders (#768)
* fix(core): AppConfig.cs - Inconsistent relative-path depth across Debug/Release folder constants (#769)
* fix(core): HandleHelper.cs - Silent catch swallows Kill() failure on handle.exe timeout (#771)
* fix(core): Domain/Service.cs - StartupType, Priority, and DateRotationType lack inline default initializers despite sibling properties having them (#776)
* fix(core): ServiceManager.cs - Null-forgiving operator on scmHandle then null check is self-contradictory (#781)
* fix(infra): DapperExecutor.cs - synchronous Thread.Sleep in SQLite busy-retry blocks thread pool (#759)
* fix(infra): DapperExecutor.cs - SpinWait.SpinUntil(() => false, delay) misuses SpinWait as Thread.Sleep (#779)
* fix(service): Fire-and-forget PRESHUTDOWN registration races OnStart failure (#758)
* fix(service): ProcessLauncher.cs - stdout/stderr log flush has no error handling, buffer lost on disk failure (#770)
* fix(service): EnvironmentVariableHelper.cs - MaxExpansionPasses and MaxStringLength hardcoded, should live in AppConfig (#775)
* fix(service): Partial nginx shutdown leaves orphan process running (#784)
* fix(restarter): database locked on restricted accounts
* fix(desktop): MainViewModel.cs - ConfirmPassword silently overwritten with Password on configuration reload (#782)
* fix(desktop,manager): Export/Import XML vs JSON methods duplicated in both GUI projects (#407)
* fix(desktop,manager): Start/Stop/Restart boilerplate duplicated in both ServiceCommands (#408)
* fix(desktop,manager): MainViewModel.cs - IsManagerAppAvailable snapshotted at ctor, never refreshed (#783)
* fix(desktop,manager,cli): Validation logic triplicated across Servy, Manager, and CLI (#404)
* fix(manager): ConsoleViewModel.cs - LogTailer instances leaked across service switches (no store, no dispose) (#763)
* fix(manager): Strings.resx - Status_StopPending and Status_PausePending display values lack space, inconsistent with sibling statuses (#778)
* fix(psm1): stderr ArrayList capture is unbounded while stdout has a 1 MB cap (asymmetric) (#765)
* fix(notifications): ServyFailureEmail.ps1 - timestamp only persisted on email success causes event storm after SMTP outage (#760)
* fix(tests): ProcessKillerTests.Dispose - cmd cleanup loop has empty body; dead code or missing Kill() (#740)
* fix(tests): LogTailerTests - Hardcoded Task.Delay(300/1000) timing flake risk (#749)
* fix(bump-version): dead else branches in version-format logic (#772)
* fix(publish): docstring example uses -fm but actual parameter is -Tfm (#773)
* fix(bump-runtime): counters use $global: scope, pollute caller session (#774)
* ci(workflows): Multiple workflows - No permissions block, inheriting default read-write token scope (#589)
* ci(scoop): scoop.yml - git config --global injects PAT into runner globally, persists for all subsequent steps (#777)

</details>

### Downloads
* [servy-8.1-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.1/servy-8.1-net48-sbom.xml) - 0.02 MB
* [servy-8.1-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.1/servy-8.1-net48-x64-installer.exe) - 4.02 MB
* [servy-8.1-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.1/servy-8.1-net48-x64-portable.7z) - 1.76 MB
* [servy-8.1-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.1/servy-8.1-sbom.xml) - 0.03 MB
* [servy-8.1-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.1/servy-8.1-x64-installer.exe) - 81.05 MB
* [servy-8.1-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.1/servy-8.1-x64-portable.7z) - 78.91 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v8.1.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v8.1.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v8.0...v8.1

## [Servy 8.0](https://github.com/aelassas/servy/releases/tag/v8.0)

**Date:** 2026-04-20 | **Tag:** [`v8.0`](https://github.com/aelassas/servy/tree/v8.0)

Servy 8.0 introduces many fixes across all components. The full release notes are available in the expandable section below.

## Full Changelog
<details>
  <summary>Click to expand release notes!</summary>

* feat(core): allow MaxRestartAttempts to be set to 0 for unlimited restart attempts (#701)
* feat(desktop): replace process parameters inputs by a resizable textarea (#700)
* fix(core): EnableRotation is ambiguous now that date rotation exists - should be EnableSizeRotation (#381)
* fix(core): MaxRestartAttempts upper bound of 100 is too restrictive for long-running system services (#701)
* fix(core): ProtectedKeyProvider.cs - DPAPI entropy migration failure logs Warn once, stuck state invisible (#723)
* fix(core): Child folder ACLs in %ProgramData%\Servy break multi-account setups (#725)
* fix(core): Central config - Regex ReDoS timeout 200ms duplicated across 6 regexes (#726)
* fix(core): NativeMethods.cs - Inconsistent SafeHandle vs bare IntPtr across SCM/Job P/Invokes (#730)
* fix(core): ProtectedKeyProvider.SaveProtected - No explicit file ACL, non-atomic write (#731)
* fix(core): EventLogReader.cs:44 - Truncated <returns> docstring ending mid-word (#748)
* fix(service): ensure event log availability during pre-shutdown via explicit dependency
* fix(service): ProcessLauncher.cs - Duplicate FireAndForget check is dead code (#707)
* fix(service): EnvironmentVariableHelper.cs - Duplicate <summary> XML doc on ExpandWithDictionary (#708)
* fix(service): ProtectedKeyProvider: stale aes_key.dat from cloned/imaged hosts causes silent 1053, no recovery path (#712)
* fix(service): ProcessLaunchOptions.cs - DefaultWaitChunkMs and DefaultScmAdditionalTimeMs duplicated (#728)
* fix(desktop): MainViewModel.cs - Hardcoded English dialog titles in 4 SaveFile calls (#735)
* fix(desktop): ServiceConfigurationMapper - Mixed AppConfig vs hardcoded defaults within same file (#745)
* fix(desktop,manager): App.xaml.cs initialization duplicated between Servy and Manager (#405)
* fix(desktop,manager): MainWindow.xaml.cs - CreateMainViewModel is 60-line composition root in View code-behind (#649)
* fix(desktop,manager): App.xaml.cs - ContinueWith(async t) drops inner Task, startup faults lost (#714)
* fix(desktop,manager): AppBootstrapper.cs - OnExit disposes SecureData but not DbContext (#715)
* fix(desktop,manager):  14 unused resource keys across Servy and Servy.Manager (#729)
* fix(desktop,manager): ServiceCommands.ValidateFileSize duplicated across Servy and Servy.Manager (#743)
* fix(desktop,manager,cli): misleading error "Max Restart Attempts must be a number greater than or equal to 1" fires for upper-bound violations too (#702)
* fix(desktop,manager,cli): improve validation messages for numeric options (#703)
* fix(manager): MonitoringViewModels - OnTick guard + timer pattern triplicated across 3 ViewModels (#517)
* fix(manager): ViewModels - SearchServicesAsync duplicated across ConsoleViewModel, PerformanceViewModel, DependenciesViewModel (#632)
* fix(manager): MainViewModel.cs - Child ViewModels newed up directly, not injectable (#648)
* fix(manager): MainWindow.xaml.cs - GetDependenciesVm uses 'logsView' as pattern variable (copy-paste bug) (#705)
* fix(manager): MainWindow.xaml.cs - Type pattern variable shadows type name in GetPerformanceVm/GetConsoleVm (#706)
* fix(manager): XAML code-behind - async void event handlers swallow exceptions (#713)
* fix(manager): Monitoring ViewModels - no IDisposable, DispatcherTimer/CTS leak on GC race (#716)
* fix(manager): LogsViewModel - CTS + Cleanup() without IDisposable (#732)
* fix(manager): ServiceConfigurationMapper vs ServiceMapper - Divergent default-handling and enum validation (#733)
* fix(manager): LogTailer.cs - CreationTime rotation detection misses FAT32 tunneling and same-size rotation (#734)
* fix(cli): InstallServiceCommand.cs - Duplicate <summary> XML doc blocks on Execute method (#704)
* fix(cli): ConsoleHelper.cs - Duplicate <summary> XML doc on RunWithLoadingAnimation (#709)
* fix(tests): ServiceRepositoryTests - Brittle reflection-based property verification couples tests to DTO shape (#736)
* fix(tests): RotatingStreamWriterTests - DateTime.UtcNow used twice per test, midnight-boundary flake (#737)
* fix(tests): ServiceRepositoryStub - decrypt parameter ignored; DTOs asymmetric between Get methods (#739)
* fix(tests): ConsoleViewModelTests - [Fact(Skip="TODO needs to be fixed")] with no tracked follow-up (#741)
* fix(tests): tests/ConsoleApp/Program.cs - Developer-specific hardcoded Python path + dead commented code (#746)
* fix(tests): DatabaseValidatorTests - Environment-dependent Assert.Fail masquerading as unit test (#747)
* fix(psm1): [ValidateRange(1, 2147483647)] for -MaxRestartAttempts mismatches CLI limit of 100 (#703)
* fix(psm1): PS 2.0 compat claimed in comments but no #Requires -Version directive (#721)
* fix(notifications): force UTF8 encoding to fix NULL character bug 
* fix(notifications): ensure timestamps are strictly increasing
* fix(notifications): handle non-English OS when querying event log
* fix(publish): publish-sc.ps1 / publish-fd.ps1 - Duplicate hardcoded Inno Setup and 7-Zip paths (#727)
* fix(publish): setup/signpath.ps1 - Install-Module -Force without -RequiredVersion (floating signing-module version) (#750)
* chore(deps): update dependencies
* ci(changelog,sbom): No permissions block (#717)
* ci(scoop): PAT inlined in git clone URL, inconsistent with earlier url.insteadOf pattern (#718)
* ci(tmp): Workflow named "tmp" with no documented purpose (#720)
* ci(setup-dotnet): uses dotnet-install -Channel (floating patch) instead of -Version (pinned) (#751)
* ci(azure-pipelines): orphaned legacy CI alongside GitHub Actions, no tests/coverage/sign (#752)
</details>

### Downloads
* [servy-8.0-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.0/servy-8.0-net48-sbom.xml) - 0.02 MB
* [servy-8.0-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.0/servy-8.0-net48-x64-installer.exe) - 4.02 MB
* [servy-8.0-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.0/servy-8.0-net48-x64-portable.7z) - 1.76 MB
* [servy-8.0-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.0/servy-8.0-sbom.xml) - 0.03 MB
* [servy-8.0-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.0/servy-8.0-x64-installer.exe) - 81.11 MB
* [servy-8.0-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.0/servy-8.0-x64-portable.7z) - 78.99 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v8.0.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v8.0.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v7.9...v8.0

## [Servy 7.9](https://github.com/aelassas/servy/releases/tag/v7.9)

**Date:** 2026-04-16 | **Tag:** [`v7.9`](https://github.com/aelassas/servy/tree/v7.9)

Servy 7.9 introduces a [hardened security infrastructure](https://github.com/aelassas/servy/wiki/Security), significant performance optimizations, and a wealth of new features. It's packed with an extensive list of improvements, and the full release notes are available in the expandable section below.

## Full Changelog
<details>
  <summary>Click to expand release notes!</summary>

* feat(desktop): replace process parameters input by a resizable textarea (#700)
* feat(manager): Get CPU and RAM usage of the whole process tree (#446)
* feat(core): replace WMI with native P/Invoke for improved performance and reliability (#94)
* feat(notifications): move SMTP settings to external XML config
* fix(security): ServiceHelper: debug logging can write passwords and sensitive values to plaintext log files (#161)
* fix(core): Resource leak: StringWriter not disposed in ServiceExporter.ExportXml (#95)
* fix(core): WindowsServiceApi.GetServices: ServiceController instances not disposed after enumeration (#97)
* fix(core): HandleHelper.GetProcessesUsingFile: int.Parse can throw FormatException (#98)
* fix(core): HandleHelper: command injection risk via string-interpolated Arguments (#99)
* fix(core): RotatingStreamWriter.GenerateUniqueFileName: null-forgiving operator on Path.GetDirectoryName (#101)
* fix(core): SecureData.Decrypt: silent fallback to plaintext on cryptographic errors (#103)
* fix(core): Async method naming: Task-returning methods missing Async suffix (#110)
* fix(core): IServiceManager: mixed sync and async methods in same interface (#114)
* fix(core): inconsistent return types for operation success across layers (#116)
* fix(core): ServiceHelper.GetRunningServyServices: verbose list union can be a single LINQ expression (#124)
* fix(core): EnvironmentVariableParser: list allocated before guard clause - use early return (#128)
* fix(core): ResourceHelper: verbose conditional list initialization can be a single ternary (#127)
* fix(core): StringHelper: typo in method name FormatEnvirnomentVariables - missing 'o' (#139)
* fix(core): InstallServiceAsync: SetServiceDescription called with zero handle when CreateService fails (#158)
* fix(core): SecureData: key material retained in memory indefinitely - class lacks IDisposable (#159)
* fix(core): ProtectedKeyProvider: DataProtectionScope.LocalMachine allows any local process to decrypt keys (#160)
* fix(core): ServiceManager.UninstallServiceAsync: Thread.Sleep blocks thread pool thread in async method (#162)
* fix(core): RotatingStreamWriter.Rotate: redundant nested lock - caller already holds _lock (#164)
* fix(core): Logger.Log: null check on _writer outside lock - race condition with Shutdown() (#165)
* fix(core): RotatingStreamWriter: _disposed check outside lock - race with concurrent Dispose() (#168)
* fix(core): Helper.IsValidPath: path traversal check is too broad - blocks legitimate paths (#169)
* fix(core): XXE vulnerability in XmlServiceValidator deserialization (#172)
* fix(core): SecureData missing ObjectDisposedException guard on Encrypt/Decrypt (#177)
* fix(core): PID reuse TOCTOU race condition in ProcessKiller (179)
* fix(core): Race condition between Refresh and Start in ServiceHelper.StartServices (#181)
* fix(core): unbounded loop in RotatingStreamWriter.GenerateUniqueFileName (#183)
* fix(core): Credential validation bypassed when password is empty (#182)
* fix(core): RotatingStreamWriter: uses DateTime.Now instead of UTC for rotation date tracking (#202)
* fix(core): XmlServiceValidator: XXE vulnerability - XmlDocument loaded without disabling DTD processing (#221)
* fix(core): EventLogReader: EventRecord objects may reference disposed reader resources (#223)
* fix(core): EventLogService: XPath injection risk in query filter construction (#224)
* fix(core): HandleHelper: WaitForExit without timeout - caller blocks indefinitely if handle.exe hangs (#251)
* fix(core): EventLogService.SearchAsync: unbounded result list - OOM on large event logs (#253)
* fix(core): ProtectedKeyProvider: no retry on File.ReadAllBytes - AV lock fails service startup (#255)
* fix(core): RotatingStreamWriter.Rotate: File.Move failure silently skipped - log grows unbounded (#256)
* fix(core): EventLogReader.ParseLevel: Critical events (level 1) misclassified as Information (#259)
* fix(core): ProcessHelper.CpuTimesStore: ConcurrentDictionary grows unbounded - memory leak on long-running systems (#264)
* fix(core): ILogger.Prefix is mutable - thread-safety risk when shared across services (#266)
* fix(core): HandleHelper.GetProcessesUsingFile: synchronous ReadToEnd() deadlocks when stderr buffer fills (#277)
* fix(core): ServiceHelper.StopServices: exception wrapping loses inner exception and stack trace (#279)
* fix(core): Negative RotationSize silently produces huge ulong, disabling log rotation (#280)
* fix(core): Inconsistent StartupType default: Manual in Repository vs Automatic in Mapper (#291)
* fix(core): ServiceManager bypasses injected IWin32ErrorProvider in two methods (#295)
* fix(core): Path.GetDirectoryName and FileInfo.Directory null-forgiving on multiple locations (#307)
* fix(core): Unchecked enum casts from database integers can produce undefined enum values (#319)
* fix(core): ResolvePath returns whitespace-only strings as valid paths (#320)
* fix(core): ServiceDependenciesValidator regex rejects valid service names containing dots (#324)
* fix(core): ProcessHelper._lastPruneTime: TOCTOU race allows concurrent prune execution (#332)
* fix(core): ServiceManager.GetAllServices: bare catch silently defaults startup type to Automatic (#337)
* fix(core): ServiceManager.GetServiceStartupType: catch-all returns ambiguous null (#345)
* fix(core): ServiceManager.InstallServiceAsync: UpdateServiceConfig return value explicitly discarded (#348)
* fix(core): Log injection: no newline sanitization on user-controlled strings in log messages (#354)
* fix(core): Hardcoded DPAPI entropy string in ProtectedKeyProvider weakens defense-in-depth (#355)
* fix(core): Local privilege escalation: ProgramData directories created without restrictive ACLs (#357)
* fix(core): SCM and service handles opened with ALL_ACCESS instead of minimum required permissions (#362)
* fix(core): XML/JSON import validators only check Name and Path - numeric fields not range-validated (#366)
* fix(core): Encryption key file path revealed in error log and Event Log (#377)
* fix(core): UserSession property name misleads - actually holds service account name (#380)
* fix(core): Enum default values differ between ServiceMapper and ServiceRepository for same fields (#401)
* fix(core): Dead code: IWindowsServiceProvider and WindowsServiceProvider entirely unused (#409)
* fix(core): DRY: Stop timeout calculation duplicated in 3 locations (#414)
* fix(core): ProtectedKeyProvider: silent retry loop on DPAPI migration failure - no logging (#415)
* fix(core): RotatingStreamWriter: failed rotation causes repeated retry on every subsequent write (#418)
* fix(core): Hidden side-effect: GetCpuUsage mutates global state and prunes dead processes (#432)
* fix(core): Missing comments: magic number 15 (buffer seconds) used in 5+ locations without explanation (#435)
* fix(core): Performance: Regex allocated fresh on every HandleHelper.GetProcessesUsingFile call (#438)
* fix(core): Performance: Logger.WriteLogEntry boxes enum and allocates two strings per log call (#439)
* fix(core): EventLogService.cs - Bracket heuristic for Servy logs matches unrelated events (#463)
* fix(core): ServiceConfiguration.cs - Password and ConfirmPassword serialized in plain text during export (#466)
* fix(core): KillProcessesUsingFile kills by name instead of PID (#472)
* fix(core): HandleHelper.cs - StandardError redirected but never read, deadlock risk (#475)
* fix(core): RotatingStreamWriter.cs - EnforceMaxRotations silently swallows File.Delete exceptions (#476)
* fix(core): ProcessHelper + ProcessKiller - P/Invoke declarations duplicated verbatim (#485)
* fix(core): Logger.cs - Log entry timestamps always local time regardless of rotation setting (#499)
* fix(core): Logger.cs - Initialize tears down old writer without draining in-flight writes (#500)
* fix(core): ServiceManager.cs - Parallel.ForEach parallelism uncapped at ProcessorCount (#504)
* fix(core): ServiceValidator.cs - Error message says 30s-1h but actual bounds are 1s-86400s (#511)
* fix(core): ServiceValidator.cs - ExecutablePath not validated in ValidateDto (#512)
* fix(core): ServiceManager.StartServiceAsync - No differentiation between TimeoutException and other failures (#514)
* fix(core): ServiceManager.cs - GetAllServices: deeply nested Parallel.ForEach with 3 AllocHGlobal blocks (#520)
* fix(core): ServiceExporter.cs - XML export declares UTF-16 but file is written as UTF-8 (#555)
* fix(core): ServiceValidator.cs - Missing upper-bound check on StopTimeout for import flows (#556)
* fix(core): Handle.cs - Struct wrapping IntPtr is freely copyable, risks double-close (#557)
* fix(core): NativeMethods.cs - Regex character class [@!-] parsed as ASCII range, allows unintended characters (#563)
* fix(core): ServiceManager.cs - InstallServiceAsync leaves partial service in SCM on post-create config failure (#564)
* fix(core): SecureData.cs - Decrypt silently returns plaintext when input is not Base64 (#568)
* fix(core): Credential validation triggers real domain logon, can lock out service accounts (#574)
* fix(core):  BuildDependencyTree re-expands shared deps in diamond patterns (#578)
* fix(core): ServiceManager.cs - ArgumentException passes field name as message instead of paramName (#581)
* fix(core): NativeMethods.cs - ValidateCredentials uses LOGON32_LOGON_NETWORK, false negatives for restricted accounts (#582)
* fix(core): ServiceHelper.cs - StopServices throws on first failure, remaining services left running (#590)
* fix(core): ERROR_INVALID_PARAMETER is 7, should be 87 (#657)
* fix(core): PreviousStopTimeout exported in JSON/XML without JsonIgnore (#658)
* fix(core): LogonAsServiceGrant.cs - Opens LSA policy with POLICY_ALL_ACCESS instead of minimum rights (#586)
* fix(core): EnvironmentVariableHelper.cs - Circular reference warning fires falsely on last-iteration convergence (#596)
* fix(core): Validator accepts newlines as delimiters but parser only accepts semicolons (#599)
* fix(core): RotatingStreamWriter.cs - EnforceMaxRotations glob pattern can match and delete unrelated files (#603)
* fix(core): Helper.cs - ParseVersion uses double, minor versions >= 10 sort incorrectly (#629)
* fix(core): EventLogService.cs - No specific handling for Event Log access failure (#634)
* fix(core): EventLogService.cs - XPath query manually escaped, injection possible with crafted source name (#635)
* fix(core): EventLogService.cs - Critical events (level 1) mapped to Error, not separately filterable (#646)
* fix(core): ProcessKiller.cs - Three different WaitForExit timeouts as unnamed magic numbers (#653)
* fix(core): AppConfig.cs - "Configration" typo in public API constant names (#656)
* fix(core): ServyEventLogEntry.cs - No DateTimeKind enforcement on Time property (#659)
* fix(core): EventLogEntry.cs - Class name collides with System.Diagnostics.EventLogEntry (#661)
* fix(core): ServiceStatus.cs - Stopped=0 means uninitialized fields default to Stopped instead of Unknown (#663)
* fix(core): ServiceStartType.cs - AutomaticDelayedStart=5 is not a valid Win32 value, undocumented sentinel (#666)
* fix(core): Domain/Service.cs - Install() wrapperExeDir parameter silently ignored in RELEASE builds (#667)
* fix(core): ServiceDependencyNode.cs - ServiceName has no null guard, nullable contract violation (#669)
* fix(core): Domain/Service.cs - HeartbeatInterval, MaxFailedChecks and other defaults as inline magic numbers (#671)
* fix(core): JsonServiceValidator/XmlServiceValidator - Log injection via crafted service name (#672)
* fix(core): IJsonServiceSerializer vs IXmlServiceSerializer - Nullable parameter asymmetry (#673)
* fix(core): JsonServiceValidator/XmlServiceValidator - No payload size limit before deserialization (#674)
* fix(core): ILogger.cs - Name collides with Microsoft.Extensions.Logging.ILogger (#677)
* fix(core): JsonServiceSerializer/XmlServiceSerializer - Deserialize does not catch deserialization exceptions (#680)
* fix(core,infra): inconsistent ConfigureAwait(false) usage across async methods (#113)
* fix(core,service): RotationSize: integer overflow in MB-to-bytes conversion (2 locations) (#222)
* fix(core,service,desktop,cli): Remove commented-out code blocks across 4 files (#137)
* fix(infra): DummyHelper class is unused - can be removed (#138)
* fix(infra): ServiceRepository.SearchAsync: null keyword silently matches all records (#234)
* fix(infra): DapperExecutor: synchronous connection.Open() in all async methods (#236)
* fix(infra): ServiceRepository: 18 identical Encrypt/Decrypt blocks can use a helper (#245)
* fix(infra): UpsertAsync ON CONFLICT clause misses Pid, PreviousStopTimeout, ActiveStdoutPath, ActiveStderrPath (#290)
* fix(infra): System.Data.SQLite 2.0.3: verify bundled SQLite engine version for CVE-2025-6965 (#379)
* fix(infra): DapperExecutor: no SQLITE_BUSY retry or busy_timeout in connection string (#417)
* fix(infra): Coupling: ServiceRepository contains domain mapping logic (MapToDomain/MapToDto) (#437)
* fix(infra): ServiceRepository.cs - UpdateAsync and Update contain identical 40-line SQL blocks (#484)
* fix(infra): SQLiteDbInitializer.cs - Schema migration via ALTER TABLE with no version tracking (#492)
* fix(infra): ServiceRepository.cs - 50-column SQL repeated 4x with existing divergence (#493)
* fix(infra): DapperExecutor.cs - Retry uses Thread.Sleep with linear backoff (#503)
* fix(infra): UpsertBatchAsync does not sync generated IDs back to DTOs (#585)
* fix(infra): Column migrations not wrapped in transaction, partial failure possible (#588)
* fix(infra): IServiceRepository.UpsertBatchAsync - Documents atomic transaction but implementation has none (#670)
* fix(service): disable recovery when health monitoring is disabled
* fix(service): prevent integer overflow in health check timer
* fix(service): Service.cs: blocking Wait() loop without total timeout cap during service stop (#147)
* fix(service): EnvironmentVariableHelper.ExpandWithDictionary: self-referencing variables cause unbounded string growth (#163)
* fix(service): Service.cs: _pathValidator missing null check in constructors (#166)
* fix(service): Race condition on _childProcess field access in Service.cs (#188)
* fix(service): Process handle leak in fire-and-forget pre-stop hook (#189)
* fix(service): CancellationTokenSource leak on process restart (#190)
* fix(service): Fragile reflection on private ServiceBase fields (#192)
* fix(service): Double-dispose risk and potential exception in ProcessExtensions.GetChildren (#193)
* fix(service): Hook class: Process object never disposed - native handle leak (#225)
* fix(service): Path.GetDirectoryName null flows into WorkingDirectory (3 locations) (#239)
* fix(service): ProcessWrapper.WaitForExit(): parameterless overload can hang service teardown (#252)
* fix(service): post-launch, failure program, and post-stop hooks don't expand custom environment variables (#260)
* fix(service): main process env var assignment missing null coalescing (inconsistent with pre-launch) (#261)
* fix(service): OutputDataReceived race - writer disposed while event handler still in-flight (#262)
* fix(servce); hardcoded timeout constants should be configurable via appsettings (#268)
* fix(service): command injection: service name passed unquoted to Servy.Restarter process arguments (#352)
* fix(service): environment variable override: no blocklist for critical system variables (#353)
* fix(service): EnableDebugLogs writes environment variables (may contain secrets) to log file in cleartext (#376)
* fix(service): ProcessWrapper has duplicate Handle and ProcessHandle properties returning same value (#383)
* fix(service): Hidden side-effect: GetRestartAttempts creates and writes file on read failure (#434)
* fix(service): Performance: enum-to-string-to-enum round-trip in StartOptionsParser (#440)
* fix(service): Tech debt: reflection-based ServiceBase field access fragile across .NET versions (#442)
* fix(service): Abstraction: process launch pattern duplicated across 4 hook methods in Service.cs (#443)
* fix(service): tech debt: synchronous File.ReadAllText/WriteAllText in Service restart-attempts hot path (#444)
* fix(service): Service.StartProcess: Process.Start failure leaves service in broken state (#416)
* fix(service): Dead code: 6 unused Win32 error constants in Errors.cs (#410)
* fix(service):  _isRecovering not volatile, read across threads without synchronization (#451)
* fix(service): Race between process-exit and health timer can trigger double recovery (#452)
* fix(service): OnProcessExited uses blocking semaphore Wait on thread pool (#473)
* fix(service): ProcessExtensions.cs - GetChildren transfers Process ownership without documentation (#481)
* fix(service): Python/Java detection via .py/.java in Arguments string is fragile (#489)
* fix(service): Service.cs - Injection constructor skips Logger.Initialize and SecureData creation (#497)
* fix(service): OnProcessExited early return leaves _isRecovering permanently set (#509)
* fix(service): _fileSemaphore is static but guards per-instance file paths (#513)
* fix(service): Environment variable expand+audit pattern duplicated 5 times (#515)
* fix(service): Service.cs + ProcessLauncher.cs - Python/Java UTF-8 detection duplicated between files (#516)
* fix(service): Magic threshold 20 in OnStart has no named constant (#521)
* fix(service): OnCustomCommand silent return when _serviceHandle is IntPtr.Zero leaves SCM waiting (#527)
* fix(service): CheckHealth holds _healthCheckSemaphore during disk I/O (#528)
* fix(service): LogUnexpandedPlaceholders emits false-positive warnings for legitimate % usage (#532)
* fix(service): EnvironmentVariableHelper.cs - Indirect circular env-var references not detected (#545)
* fix(service): ProcessWrapper.cs - GenerateConsoleCtrlEvent may hit service's own process (#546)
* fix(service): EnsureJavaUTF8Encoding triggers on .java substring in arguments, false positive for non-Java processes (#567)
* fix(service): Password masking regex incomplete for quoted and space-separated values (#571)
* fix(service): Protected-variable blocklist missing runtime-injection variables (#573)
* fix(service): ExecuteTeardown sets _disposed=true even if Cleanup() throws (#576)
* fix(service): OnCustomCommand spin-loops with Task.Wait, ThreadPool exhaustion risk (#579)
* fix(service): StopTree: StopPrivate does not WaitForExit after Kill, grandchild enumeration incomplete (#591)
* fix(service): RestartService does not kill orphan restarter process on timeout (#592)
* fix(service): SendCtrlC broadcasts to entire console group, kills unrelated processes (#593)
* fix(service): _options! null-forgiving in Cleanup() causes NRE if teardown before OnStart completes (#628)
* fix(service): timeout multiplication int * 1000 can overflow for large configured values (#630)
* fix(service): ConditionalResetRestartAttemptsAsync fire-and-forget swallows cancellation silently (#631)
* fix(service): SafeKillProcess accesses faulted task Result, loses inner exception detail (#633)
* fix(service): InsertPid also sets ActiveStdoutPath/ActiveStderrPath, misleading name (#642)
* fix(service): Reflection field-search blocks duplicated for _acceptedCommands and status handle (#650)
* fix(service): reflection-based ServiceBase field access fragile across .NET runtime versions (#652)
* fix(service): Restarter 240-second timeout as unnamed magic number (#654)
* fix(service): PreShutdown waitHint and pulse interval as unnamed magic numbers (#655)
* fix(restarter): ServiceRestarter doesn't handle transitional service states (#191)
* fix(restarter): missing timeout guard in start phase causes ArgumentOutOfRangeException (#321)
* fix(restarter): Servy.Restarter exits with code 0 on failure - caller thinks restart succeeded (#333)
* fix(restarter): Servy.Restarter accepts arbitrary service names without validation (#360)
* fix(restarter): ServiceRestarter.cs - WaitForStatus(Running) called with potentially negative TimeSpan (#547)
* fix(desktop): ServiceCommands.InstallService: int.Parse on raw strings can throw FormatException (#228)
* fix(desktop): MainViewModel: 16 identical Browse methods can be extracted into one helper (#244)
* fix(desktop): ServiceConfigurationMapper.ToDomain silently drops date rotation settings (#292)
* fix(desktop): MainViewModel constructor does not null-guard most parameters (#308)
* fix(desktop): validation logic inconsistency - Servy rejects valid configs when features are disabled (#390)
* fix(desktop): Servy import path skips most validation - only checks env vars and dependencies (#391)
* fix(desktop): Servy vs Manager: IServiceCommands return type divergence - Task vs Task<bool> (#393)
* fix(desktop): Magic numbers for defaults in ServiceRepository vs AppConfig constants elsewhere (#398)
* fix(desktop): Complexity: InstallService method takes 40+ individual parameters (#412)
* fix(desktop): ServiceCommands.cs - InstallService double-maps config to DTO and options (#488)
* fix(desktop,manager): App.xaml.cs: ContinueWith error handler runs without explicit TaskScheduler (#146)
* fix(desktop,manager): ServiceCommands constructors: missing null-guards on injected dependencies (#233)
* fix(desktop,manager): SecureData not disposed in App startup - key material left in memory (#186)
* fix(desktop,manager): Process handle leaks in ServiceCommands.OpenManager and ConfigureServiceAsync (#226)
* fix(desktop,manager): FileDialogService: 6 near-identical dialog methods can share a helper (#246)
* fix(desktop,manager): HelpService.CheckUpdates: HttpClient has no timeout - UI can hang indefinitely (#250)
* fix(desktop,manager): WPF UI: missing AutomationProperties - not accessible to screen readers (#267)
* fix(desktop,manager): Process.Start() return value not checked when launching external apps (#349)
* fix(desktop,manager): Servy vs Manager: success message inconsistency - Msg_ServiceCreated vs Msg_ServiceInstalled (#395)
* fix(desktop,manager): scalability: BulkObservableCollection.TrimToSize is O(n²) due to RemoveAt(0) (#427)
* fix(desktop,manager): App.xaml.cs - Global exception handler exposes raw exception details to UI (#470)
* fix(desktop,manager): HelpService.cs - OpenDocumentation has no exception handling (#482)
* fix(desktop,manager): App.xaml.cs (both) - Duplicate DB/SecureData initialization in App and MainWindow (#494)
* fix(desktop,manager): HelpService.cs - Hardcoded English UI strings instead of localized resources (#496)
* fix(desktop,manager): Both App.xaml.cs - Exception handlers registered inside fire-and-forget InitializeApp, crash window (#606)
* fix(desktop,manager): BulkObservableCollection.cs - _suppressNotification not volatile, cross-thread visibility issue (#622)
* fix(desktop,manager): HelpService.cs - new HttpClient() per call, socket exhaustion anti-pattern (#643)
* fix(desktop,manager): DependenciesView/PerformanceView/ConsoleView - async void Loaded handlers with no try/catch (#664)
* fix(desktop,manager): RelayCommand<T> - Unsafe unboxing cast when parameter is null (#668)
* fix(manager): HistoryResult.cs - Lines property exposes mutable List<LogLine> (#675)
* fix(desktop,manager): MessageBoxService.cs - BeginInvoke returns before dialog is dismissed (#676)
* fix(desktop,manager): AsyncCommand._isExecuting - Not volatile, thread-safety gap (#682)
* fix(desktop,manager): IAsyncCommand.ExecuteAsync - Non-nullable parameter vs AsyncCommand nullable parameter (#683)
* fix(desktop,manager,cli): JSON deserialization should explicitly set TypeNameHandling.None (#175)
* fix(desktop,manager,service): missing constructor null guards in 5 classes (pattern inconsistency) (#241)
* fix(desktop,manager,service): HelpService and ServiceHelper: Process.Start return value not disposed (3 locations) (#242)
* fix(desktop,manager,cli): Process.MainModule!.FileName can be null in single-file deployments and restricted contexts (#306)
* fix(desktop,manager,cli): No pre-flight administrator check before privileged SCM and LSA operations (#361)
* fix(desktop,manager,cli): No file size check before reading import files - potential OOM denial-of-service (#364)
* fix(desktop,manager,cli): no string length bounds on service name, display name, description, and parameters (#367)
* fix(desktop,manager,cli): DatabaseValidator - SQLite version check is advisory only, does not halt startup (#524)
* fix(manager): hardened bulk service operations with dynamic hardware-aware throttling
* fix(manager): MainViewModel: three near-identical bulk service operations can be extracted to one method (#122)
* fix(manager): MainViewModel.UpdateSelectAllState: two All() iterations can be replaced by single Count() (#123)
* fix(manager): ConsoleViewModel: fire-and-forget task without error handling on log tailing (#143)
* fix(manager): PerformanceViewModel, DependenciesViewModel, ConsoleViewModel: missing Cleanup() - timer and CTS resource leaks (#144)
* fix(manager): LogTailer: potential integer overflow in DateTime.AddTicks() for synthetic timestamps (#145)
* fix(manager): ConsoleViewModel.OnTickAsync: bare catch silently hides all errors (#148)
* fix(manager): LogTailer.LoadHistory: no boundary validation on maxLines parameter (#149)
* fix(manager): ViewModels: missing null-conditional on _timer.Stop() and _timer.Start() calls (#150)
* fix(manager): Event handler memory leak in ServiceRowViewModel (#184)
* fix(manager): RequestScroll event handler never unsubscribed in ConsoleView (#185)
* fix(manager): bare catch blocks silently swallow all exceptions in timer handlers (#187)
* fix(manager): LogTailer.RunFromPosition: bare catch blocks swallow exceptions without logging (#199)
* fix(manager): ConsoleViewModel: uses static Logger class instead of injected _logger instance (#200)
* fix(manager): Manager ServiceCommands.RefreshServices: fire-and-forget async callback - exceptions unobserved (#232)
* fix(manager): ConsoleViewModel: NullReferenceException in CollectionView filter (as cast without null check) (#237)
* fix(manager): Manager ServiceCommands.GetServiceDomain: search-based lookup instead of exact match (#229)
* fix(manager): MainWindow.xaml.cs: potential NullReferenceException on _messageBoxService in catch block (#230)
* fix(manager): App.xaml.cs: Path.GetDirectoryName can return null - NRE in Path.Combine (#238)
* fix(manager): PerformanceViewModel: Pid.Value crash due to race between currentSelection and SelectedService (#240)
* fix(manager): Redundant CTS null + IsCancellationRequested check before Cancel() (3 locations) (#247)
* fix(manager): MainViewModel: .Count(predicate) == .Count can be replaced with .All() (#248)
* fix(manager): LogTailer.LoadHistory: tempLines list ignores maxLines cap during ReadLine loop (#254)
* fix(manager): ServiceCommands: concurrent Start/Stop/Restart can corrupt service.Status (#257)
* fix(manager): LogTailer: off-by-one in synthetic timestamp - last line gets lastWrite-1ms instead of lastWrite (#258)
* fix(manager): CancellationTokenSource leaks and race conditions in Manager ViewModels (#282)
* fix(manager): Application.Current can be null during shutdown in background tasks (#284)
* fix(manager): async void SwitchService can crash the app if inner catch throws (#286)
* fix(manager): ServiceMapper.ToModelAsync hardcodes IsInstalled=false and Status=None (#300)
* fix(manager): ServiceCommands.CopyPid: race condition between null check and Clipboard.SetText (#310)
* fix(manager): MainWindow: ServiceCommands is null during ViewModel construction window (#311)
* fix(manager): SetPidText() accesses SelectedService without null guard (#317)
* fix(manager): Empty services collection causes SelectAll checkbox to show as checked (#327)
* fix(manager): MainViewModel: Service model PropertyChanged fires from background threads - collection race (#329)
* fix(manager): MainViewModel: Cleanup/CreateAndStartTimer race - old refresh task gets new uncancelled token (#330)
* fix(manager): MainViewModel.Resfresh() - typo in method name (transposed s and f) (#382)
* fix(manager): DependenciesViewModel and DependencyService: copy-pasted docs reference log tailing (#384)
* fix(manager): inconsistent logging levels - Logger.Error vs _logger.Warn for same operations (#392)
* fix(manager): ServiceCommands depends on concrete ServiceManager instead of IServiceManager (#394)
* fix(manager): Performance: two Process.GetProcessById calls per service per tick (#419)
* fix(manager): Performance: three tab ViewModels each query full DTO just to check PID (#421)
* fix(manager): Performance: 4 new PointCollection allocations per tick in PerformanceViewModel (#422)
* fix(manager): ConsoleViewModel: full log scan on every search keystroke - no debounce (#423)
* fix(manager): memory leak: DispatcherTimer.Tick never unsubscribed in ViewModel Cleanup methods (#424)
* fix(manager): memory leak: RemoveService does not unsubscribe PropertyChanged on removed VM (#425)
* fix(manager): Scalability: N individual SQLite UPSERTs per refresh tick - not batched (#426)
* fix(manager): Scalability: ServicesView.Refresh() full rebuild every tick with large service count (#428)
* fix(manager): hidden side-effect: RefreshServiceInternal writes to database during UI refresh (#433)
* fix(manager): Tech debt: SemaphoreSlim(1,1) serializes ALL service commands behind one lock (#441)
* fix(manager): console view fails to load logs when stdout redirect is missing (#448)
* fix(manager): MainViewModel.cs - _servicesLock not used in SearchServicesAsync (#464)
* fix(manager): ServiceCommands.cs - Service name embedded in process arguments without escaping (#468)
* fix(manager): LogsViewModel.cs - CancellationTokenSource lifecycle issues (#479)
* fix(manager): LogTailer.cs - Rotation detection via CreationTimeUtc unreliable on some file systems (#480)
* fix(manager): PerformanceViewModel.cs - AddPoint calls RemoveAt(0) on List, O(n) per tick (#519)
* fix(manager): MainWindow.xaml.cs - OnClosed does not call Cleanup on child ViewModels (#525)
* fix(manager): ServiceCommands.cs - _serviceLocks semaphores never removed or disposed (#531)
* fix(manager,core): multiple files - Magic numbers without named constants (batch) (#522)
* fix(manager): PerformanceViewModel.cs - Stringly-typed dispatch in AddPoint via propertyName (#523)
* fix(manager): Manager MainViewModel.cs - RemoveService mutates ObservableCollection off UI thread (#529)
* fix(manager): MainWindow.xaml.cs - HandlePerfTabSelected + duplicate StartMonitoring calls (#595)
* fix(manager): DependenciesViewModel.cs - LoadDependencyTree blocks UI thread with synchronous SCM calls (#602)
* fix(manager): ServiceCommands.cs - GetServiceDomain passes null DTO to ToDomain when service not in DB (#613)
* fix(manager): MainViewModel.cs - Dispatcher.Invoke (blocking) inside parallel loop causes thread starvation (#616)
* fix(manager): PerformanceViewModel.cs - valueHistory.Max() O(n) scan on every UI timer tick (#636)
* fix(manager): MainViewModel.cs - Services added one-by-one to ObservableCollection, floods CollectionChanged (#638)
* fix(manager): LogTailer.cs - No IDisposable, event handler references keep ViewModel rooted (#640)
* fix(manager): CpuUsageConverter/RamUsageConverter - ToString() round-trip uses CurrentCulture, comma decimal breaks parse (#644)
* fix(manager): ConsoleViewModel.cs - LINQ sort with anonymous objects on service switch causes GC pressure (#645)
* fix(manager): ServiceCommands.cs - _serviceLocks ConcurrentDictionary grows without bound (#647)
* fix(manager): ConsoleViewModel.cs - SelectedService setter triggers file I/O and timer restart as hidden side effects (#651)
* fix(manager): Service.cs - Name property not change-notified via PropertyChanged (#662)
* fix(manager): ServiceMapper.cs (Manager) - Hard cast (App)Application.Current in static mapper, untestable (#665)
* fix(manager): IServiceConfigurationValidator - Namespace/folder mismatch (#678)
* fix(manager): LogsView.xaml.cs - Missing Unloaded event unsubscribe, memory leak (#679)
* fix(manager,cli): Export functionality writes plaintext service account passwords to XML/JSON files (#375)
* fix(cli): misleading success message for import and export commands
* fix(cli): log exceptions in case unexpected errors
* fix(cli): ServiceInstallValidator: operator precedence bug in timeout/retry validation (#96)
* fix(cli): ImportServiceCommand: no path validation on deserialized service configuration (#100)
* fix(cli): ExportServiceCommand: redundant inner try-catch and misspelled error message (#112)
* fix(cli): CLI Commands: inconsistent constructor null validation - 6 of 8 commands skip checks (#111)
* fix(cli): inconsistent logging - 4 of 8 commands have no log statements (#115)
* fix(cli): CLI Commands: inconsistent input validation approach - validator class vs inline checks (#117)
* fix(cli): Path traversal in CLI export command (#173)
* fix(cli): Path traversal in CLI import command (#174)
* fix(cli): CLI ServiceInstallValidator: EnableSizeRotation bypasses RotationSize validation (#231)
* fix(cli): CLI commands: inconsistent validation approach - injected validator vs inline checks with mixed error messages (#263)
* fix(cli): error messages lack context - no paths, no operation names (#265)
* fix(cli): ExportServiceCommand: path traversal protection bypassable via NTFS junctions (#283)
* fix(cli): Helper.PrintAndReturn vs PrintAndReturnAsync: inconsistent exit code behavior (#293)
* fix(cli): res.ErrorMessage! null-forgiving on potentially null ErrorMessage (#312)
* fix(cli): export command allows UNC paths and Windows device paths - data exfiltration risk (#368)
* fix(cli): Dead code: PrintAndReturn (sync) never called; ExitCode property only consumer (#411)
* fix(cli): RecoveryAction accepted without EnableHealthMonitoring (#594)
* fix(cli): InstallServiceOptions.cs --password CLI flag exposes password in OS process listing (#637)
* fix(cli): ExportServiceCommand.cs - Missing EnsureAdministrator check (#639)
* fix(cli): ImportServiceCommand.cs - Admin check skipped when --install is omitted (#641)
* fix(cli): ConsoleHelper.cs - Console.WindowWidth throws IOException when stdout is redirected (#681)
* fix(cli,psm1): ConsoleHelper and Servy.psm1: string concatenation with + instead of interpolation (#129)
* fix(cli,infra): Mixed case comparison: StringComparison.OrdinalIgnoreCase vs ToLowerInvariant() in same files (#400)
* fix(psm1): misleading error message when Servy CLI is not found (#50)
* fix(psm1): typo in PreStopLogAsError parameter documentation (#60)
* fix(psm1): Test-ServyCliPath: error message shows wrong search path on 64-bit systems (#61)
* fix(psm1): Invoke-ServyCli: potential deadlock when reading stdout and stderr synchronously (#62)
* fix(psm1): servy-module-examples.ps1: relative Import-Module path fails when current directory differs (#64)
* fix(psm1): Test-ServyCliPath: duplicate .SYNOPSIS help block (#66)
* fix(psm1): Install-ServyService: switch parameters bypass Add-Arg helper function (#67)
* fix(psm1): Install-ServyService: numeric parameters typed as [string] instead of [int] (#68)
* fix(psm1): Export-ModuleMember is ignored when module is loaded via manifest (#70)
* fix(psm1): Show-ServyVersion: documentation says --version but command sends version (#71)
* fix(psm1): five service control functions are near-identical copy-paste (#72)
* fix(psm1): Install-ServyService: 56 repetitive Add-Arg calls could be a hashtable loop (#73)
* fix(psm1): Add-Arg: unnecessary unary comma on return statement (#75)
* fix(psm1): same boilerplate as service control functions (#76)
* fix(psm1): function export list maintained in three separate locations (#77)
* fix(psm1): module description duplicated across three files (#78)
* fix(psm1): CLI path validated at module load and again at every function call (#79)
* fix(psm1): servy-module-examples.ps1: help block examples duplicate module-level examples in Servy.psm1 (#80)
* fix(psm1): Invoke-ServyCli: WaitForExit() blocks indefinitely if servy-cli.exe hangs (#81)
* fix(psm1): Invoke-ServyCli: Process.Start() return value ignored (#82)
* fix(psm1): Invoke-ServyCli: no null check on StandardOutput and StandardError before ReadToEnd() (#83)
* fix(psm1): potential null reference in Get-Command pathSearch fallback (#88)
* fix(psm1): Invoke-ServyCli: ReadToEnd() exception loses CLI output context on process crash (#90)
* fix(core): ProcessKiller: bare catch blocks silently swallow all exceptions (#102)
* fix(psm1): Show-ServyHelp: -Command parameter ignored, undefined $argsList passed to CLI (#104)
* fix(psm1): Install-ServyService: PreLaunchRetryAttempts parameter is [string] instead of [int] - no input validation (#106)
* fix(psm1): Install-ServyService: $Env parameter shadows PowerShell $env: automatic variable (#107)
* fix(psm1): Invoke-ServyCli: stderr may be incomplete due to missing async flush after WaitForExit (#108)
* fix(psm1): Invoke-ServyCli: global stderr variable may persist after early exception (#109)
* fix(psm1): Export-ServyServiceConfig: missing [ValidateNotNullOrEmpty()] on $Path parameter (#118)
* fix(psm1): only Invoke-ServyServiceCommand has [CmdletBinding()] - missing on all public functions (#119)
* fix(psm1): inconsistent comment-based help indentation across functions (#120)
* fix(psm1): Invoke-ServyCli: exception error message missing space separator (#121)
* fix(psm1): five identical single-command functions can share a common body (#130)
* fix(psm1): Add-Arg null check on $list is dead code - callers always pass @() (#131)
* fix(psm1): Invoke-ServyCli: verbose argument list construction can be simplified (#132)
* fix(psm1): Invoke-ServyCli: unnecessary pre-initialization of $stdout, $stderr, and $exitCode (#133)
* fix(psm1): Install-ServyService: [int] parameters with default 0 are sent to CLI even when not specified (#134)
* fix(psm1): redundant double check on Get-Command result (#135)
* fix(psm1): stale FIX comment on line 332 can be removed (#140)
* fix(psm1): unnecessary "Assuming" comment about Add-Arg on line 329 (#141)
* fix(psm1): excessive inline comments in Add-Arg repeat what the code already says (#142)
* fix(psm1): Invoke-ServyCli: silent catch on process Kill after timeout - orphaned process possible (#152)
* fix(psm1): Invoke-ServyCli: no TOCTOU check on ServyCliPath before process start (#153)
* fix(psm1): Invoke-ServyCli: $Command joined into argument string without quoting (#154)
* fix(psm1): module-load throw is not PowerShell-idiomatic - prevents ErrorAction handling (#155)
* fix(psm1): Invoke-ServyCli: stderr event scriptblock built via fragile string interpolation (#156)
* fix(psm1): Add-Arg: trailing backslash in paths breaks Windows command-line argument parsing (#157)
* fix(psm1): service account password visible in process command-line arguments (#170)
* fix(psm1): Add-Arg: multiple trailing backslashes still break argument parsing (#171)
* fix(psm1): PowerShell module: Incomplete argument escaping in Add-Arg allows argument injection (#195)
* fix(psm1): Add-Arg escape overwrite bug - line 131 processes $value instead of $escapedValue (#197)
* fix(psm1): Invoke-ServyCli: exit code check after finally block - stderr may be lost (#205)
* fix(psm1): Install-ServyService: PSBoundParameters key matching relies on implicit case-insensitivity (#206)
* fix(psm1): Add-Arg: no type constraint on $list parameter (#207)
* fix(psm1): Add-Arg: array concatenation is O(n²) - consider ArrayList (#208)
* fix(psm1): Add-Arg: add fast-path for values without special characters (#209)
* fix(psm1): ValidateNotNullOrEmpty on $Name parameter (#210)
* fix(psm1): Servy.psm1: formatting inconsistencies across functions (#211)
* fix(psm1): Invoke-ServyCli: stderr race condition after Kill() - async events not flushed (#212)
* fix(psm1): Invoke-ServyCli: event handler registered before global variable exists (#213)
* fix(psm1): Install-ServyService: $EnvVars parameter name mismatches CLI option --env - PSBoundParameters check broken (#214)
* fix(psm1): Invoke-ServyCli: parameterless WaitForExit() on line 280 can hang indefinitely (#215)
* fix(psm1): Invoke-ServyCli: no validation on $script:ServyTimeoutSeconds (#216)
* fix(psm1): Invoke-ServyCli: missing CancelErrorRead() in finally block (#217)
* fix(psm1): Invoke-ServyCli: StandardOutput.ReadToEnd() could exhaust memory on large output (#218)
* fix(psm1): Invoke-ServyCli: Process.Start() failure handling could include Win32 error details (#219)
* fix(psm1): Show-ServyHelp: no explicit --help flag sent when called without -Command (#220)
* fix(psm1): Install-ServyService: .PARAMETER Env help text orphaned after rename to $EnvVars (#269)
* fix(psm1): no guard on total argument string length - cryptic error at Windows 32K limit (#270)
* fix(psm1): race between CancelErrorRead and finally-block ArrayList read (#271)
* fix(psm1): functions document 'Requires Administrator' but never verify elevation (#272)
* fix(psm1): Install-ServyService: 14 path and 3 format parameters lack early validation (#273)
* fix(psm1): CLI stderr containing password can leak into PS error messages and $Error (#274)
* fix(psm1): module-load guard blocks all unit testing on machines without servy-cli.exe (#275)
* fix(psm1): Add-Arg: $key.Trim() called 3 times per invocation - trim once at entry (#276)
* fix(psm1): Add-Arg: -Flag path missing return causes duplicate arguments (#325)
* fix(psm1): Invoke-ServyCli: empty catch on process kill hides failure reason (#346)
* fix(psm1): $PreLaunchEnv lacks pattern validation unlike $EnvVars (#369)
* fix(psm1): $DisplayName and $Password parameters lack basic validation (#372)
* fix(psm1): Export/Import -Path parameters lack existence validation (#373)
* fix(psm1): Show-ServyVersion and Show-ServyHelp use Show- verb but return pipeline output (#388)
* fix(psm1): Password parameter is [string] not [SecureString] (#469)
* fix(psm1): hidden coupling between PS parameter names and CLI flag names (#495)
* fix(psm1): Start-Sleep 50ms instead of proper WaitForExit for stderr flush (#501)
* fix(psm1): TrimStart casing mismatch makes PSBoundParameters.ContainsKey always fail (#510)
* fix(psm1): Error throw uses $_ instead of $_.Exception.Message (#584)
* fix(psd1): add license and help urls
* fix(tests): tests/test.ps1: missing $LASTEXITCODE checks after dotnet test and reportgenerator (#342)
* fix(tests): coverage collection only includes 2 of 8 test assemblies (#553)
* fix(notifications): unnecessary Sort-Object in failure notification scripts (#63)
* fix(notifications): ServyFailureEmail.ps1: hardcoded plaintext SMTP credentials without security warning (#65)
* fix(notifications): ServyFailureEmail.ps1 and ServyFailureNotification.ps1: duplicated event log query and parsing logic (#74)
* fix(notifications): ServyFailureEmail.ps1: Send-MailMessage has no error handling (#84)
* fix(notifications): Get-WinEvent has no error handling (#85)
* fix(notifications): ServyFailureNotification.ps1: WinRT type loading and toast display have no error handling (#86)
* fix(notifications): ServyFailureNotification.ps1: toast notification fails silently in non-interactive sessions (#89)
* fix(notifications): taskschd: "latest error" query misses simultaneous service failures (#91)
* fix(notifications): taskschd: MultipleInstancesPolicy=IgnoreNew silently drops error notifications during burst failures (#92)
* fix(notifications): taskschd: VBS fire-and-forget wrappers create orphaned processes and hide failures (#93)
* fix(notifications): ServyFailureEmail.ps1: hardcoded placeholder SMTP credentials (#201)
* fix(notifications): ServyFailureEmail.ps1: Send-MailMessage -UseSsl requires PowerShell 3.0+ (#204)
* fix(notifications): ServyFailureNotification.ps1: WinRT toast notifications require PowerShell 5.0+ (#203)
* fix(notifications): ServyFailureNotification.ps1: PS 5.0+ syntax breaks declared PS 2.0 compatibility (#235)
* fix(notifications): Get-ServyLastErrors.ps1: $ModuleRoot variable undefined - writes log to wrong location (#313)
* fix(notifications): ServyFailureEmail.ps1: XML config properties accessed without null checks (#314)
* fix(publish): publish-sc.ps1: Resolve-Path leaves variables null when files are missing (#315)
* fix(notifications): ServyFailureNotification.ps1: XML toast nodes can be null after Where-Object (#318)
* fix(notifications): ServyFailureEmail.ps1: HTML injection in notification email body (#370)
* fix(notifications): ServyFailureEmail.ps1: SMTP config From/To not validated for email format (#374)
* fix(notifications): Failure notification scripts forward event log content unfiltered via email and toast (#378)
* fix(notifications): Task scheduler scripts use $ModuleRoot variable name in non-module .ps1 scripts (#389)
* fix(noticiations): PowerShell task scheduler scripts deviate from codebase conventions (#403)
* fix(notifications): ServyFailureEmail.ps1 - System.Net.WebUtility unavailable on .NET 3.5 (PS 2.0 target) (#609)
* fix(notifications): Get-ServyLastErrors.ps1 - Get-WinEvent requires Vista+, not available on XP/Server 2003 (#611)
* fix(notifications): ServyFailureEmail/Notification.ps1 - Sort-Object result not wrapped in @(), scalar on single event (#614)
* fix(notifications): Get-ServyLastErrors.ps1 - exit 1 inside dot-sourced function kills caller's session (#627)
* fix(publish): build scripts use PowerShell 3.0+/5.0+ features despite PS 2.0 compatibility target (#288)
* fix(publish): Servy.Service/publish.ps1 passes parameters that child scripts silently ignore (#298)
* fix(publish): Project publish scripts: dotnet restore/clean/publish and signing missing $LASTEXITCODE checks (#351)
* fix(publish): Setup publish scripts: $Version parameter lacks format validation - Inno Setup injection (#371)
* fix(publish): Setup publish scripts use -Fm parameter while child scripts use -Tfm (#386)
* fix(publish): $selfContained and $serviceProject assigned but never used (#387)
* fix(publish): Publish scripts: inconsistent patterns between Service/Restarter vs CLI/Manager/Servy (#396)
* fix(publish): PowerShell: mixed MSBuild property syntax -p: and /p: in same dotnet publish commands (#402)
* fix(publish): publish-res scripts - Resolve-Path before Test-Path causes build failure on clean checkout (#549)
* fix(publish): publish-fd.ps1 and publish-sc.ps1 - No existence check before Copy-Item for CLI artifacts (#550)
* fix(publish): Servy.Manager/publish.ps1 - Missing exe guard before signing, unlike other publish scripts (#551)
* fix(publish): Servy.Service/publish.ps1 -Runtime passed as bare switch to callee that has no such parameter (#597)
* fix(publish): Error message references wrong binary name (copy-paste error) (#605)
* fix(publish): missing #requires -Version 3.0 where $PSScriptRoot is used (#620)
* fix(publish): All publish-res-*.ps1 - Resolve-Path throws before Test-Path guard is reached (#623)
* ci: add security audit workflow for dependency, static, and secret scanning
* ci(bump-runtime): Get-Item on hardcoded paths throws if files are missing (#316)
* ci(bump-version): $appConfigPath variable silently reused for unrelated file (#385)
* ci(bump-version): WriteAllText may add UTF-8 BOM, unlike bump-runtime.ps1 (#502)
* ci(bump-version): Get-FileEncoding does not detect UTF-16, can corrupt project files (#625)
* ci(actions): actions/checkout@v5, @v6, and upload-artifact@v6 may not exist (#548)
* ci(actions): Third-party actions pinned to floating @master/@main branches (#554)
* ci(sbom): workflow_dispatch input injected directly into PowerShell script string (#577)
* ci(scoop): branch name mismatch causes git reset to wrong ref (#565)
* ci(bump-version): git diff --quiet misses untracked files, version bump commit silently skipped (#566)
* ci(scoop): PAT embedded in git clone URL, stored in .git/config (#580)
* ci(publish): SEVEN_ZIP env var set at workflow level but overridden per-step, confusing default (#600)
* ci(dependabot): GitHub Actions ecosystem not monitored (#608)
* ci(choco): Commit-then-rebase-push pattern is race-prone (#612)
* ci(bump-version): workflow_run trigger runs even when choco workflow fails (#615)
* ci(publish,sbom): publish.yml / sbom.yml - CycloneDX CLI version drift between workflows (#618)
* ci(publish): no timeout-minutes on job, signing steps can hang indefinitely (#621)
* ci(scoop): force push to fork branch without checking if PR already merged (#624)
* ci(loc): loc.yml peaceiris/actions-gh-pages needs contents:write but no permissions declared (#626)
* chore(deps): update dependencies
</details>

### Downloads
* [servy-7.9-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.9/servy-7.9-net48-sbom.xml) - 0.02 MB
* [servy-7.9-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.9/servy-7.9-net48-x64-installer.exe) - 4.02 MB
* [servy-7.9-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.9/servy-7.9-net48-x64-portable.7z) - 1.76 MB
* [servy-7.9-sbom.xml](https://github.com/aelassas/servy/releases/download/v7.9/servy-7.9-sbom.xml) - 0.03 MB
* [servy-7.9-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v7.9/servy-7.9-x64-installer.exe) - 81.02 MB
* [servy-7.9-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v7.9/servy-7.9-x64-portable.7z) - 78.9 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v7.9.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v7.9.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v7.8...v7.9

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
* fix(manager): include missing `DesktopAppPublishPath` configuration in .NET Framework 4.8 build
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

