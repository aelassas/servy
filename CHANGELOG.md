# Changelog

## [Servy 8.6](https://github.com/aelassas/servy/releases/tag/v8.6)

**Date:** 2026-07-12 | **Tag:** [`v8.6`](https://github.com/aelassas/servy/tree/v8.6)

* fix(core): separate atomic and progressive creation paths to preserve inheritance when breakInheritance is false (#3896)
* fix(service): wrap masking regex value patterns in atomic groups to eliminate ReDoS timeouts
* fix(manager): prevent tab switching from prematurely freezing search commands
* chore(deps): update dependencies

### Downloads
* [servy-8.6-arm64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.6/servy-8.6-arm64-installer.exe) - 76.02 MB
* [servy-8.6-arm64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.6/servy-8.6-arm64-portable.7z) - 77.4 MB
* [servy-8.6-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.6/servy-8.6-net48-sbom.xml) - 0.03 MB
* [servy-8.6-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.6/servy-8.6-net48-x64-installer.exe) - 4.32 MB
* [servy-8.6-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.6/servy-8.6-net48-x64-portable.7z) - 2.06 MB
* [servy-8.6-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.6/servy-8.6-sbom.xml) - 0.04 MB
* [servy-8.6-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.6/servy-8.6-x64-installer.exe) - 82.02 MB
* [servy-8.6-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.6/servy-8.6-x64-portable.7z) - 79.91 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v8.6.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v8.6.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v8.5...v8.6

## [Servy 8.5](https://github.com/aelassas/servy/releases/tag/v8.5)

**Date:** 2026-06-28 | **Tag:** [`v8.5`](https://github.com/aelassas/servy/tree/v8.5)

Servy 8.5 focuses on stability, security, and maintainability, with a large number of fixes across all components and full native support for ARM64. The full changelog is available below.

## Full Changelog
<details>
  <summary>Click to expand release notes!</summary>

* feat: provide ARM64 binaries (#2243)
* fix: Wrapper executable not found (#4134)
* fix: Cannot remove service with status "Not Installed" - Uninstall button greyed out (#2904)
* fix(core): XmlServiceValidator.TryValidate / JsonServiceValidator.TryValidate - Compares string.Length (chars) against MaxConfigFileSizeBytes (bytes), allowing 2-3x oversized payloads with multibyte UTF-8 content (#1441)
* fix(core): AppConfig.cs - Doc comment references non-existent constant 'MaxBulkOperationParallelism' (actual is 'DefaultMaxBulkOperationParallelism') (#1445)
* fix(core): AppConfig.cs - MaxPreLaunchRetryAttempts = int.MaxValue is inconsistent with bounded MaxMaxRestartAttempts (100_000); allows unbounded retry loops (#1446)
* fix(core): AppConfig.cs - Confusingly similar names: DefaultStopTimeout (5s) vs DefaultServiceStopTimeoutSeconds (60s) (#1448)
* fix(core): ProcessKiller.cs - parentProcess.WaitForExit return value ignored; inconsistent with KillProcessTree/WalkAndKillChildren which log on timeout (#1449)
* fix(core): ResourceHelper.cs - ShouldCopyResource XML doc lists 'targetFileName' as out parameter, but the actual signature has no such parameter (#1453)
* fix(core): Helper.IsServiceNameValid - missing length check inconsistent with ServiceValidationRules.Validate (allows long names through start/stop/uninstall paths) (#1457)
* fix(core): Logger.FormatException - 'truncation to LoggerMaxFormattedExceptionLength' overshoots by 14+ chars (writes '... [truncated]' AFTER setting Length) (#1458)
* fix(core): AppConfig.cs - Static constructor invariant check between two const ints is dead code (compile-time folded) (#1460)
* fix(core): ServiceHelper.CalculateStopTimeout - Math.Max(total, floor) is always 'total' (dead floor guard) (#1462)
* fix(core): Logger.Log() - log entry timestamps reuse '_useLocalTimeForRotation' flag, conflating rotation policy with log formatting (#1464)
* fix(core): Helper.Quote() - whitespace-only inputs are coerced to empty quotes; literal-whitespace arguments cannot be passed (#1465)
* fix(core): SecureData.Encrypt - plainBytes allocated outside try, not zeroed on early failure (Aes.Create / aes.Key throws) (#1488)
* fix(core): StringHelper.cs - unused 'using System.Text.RegularExpressions' import (#1490)
* fix(core): ProtectedKeyProvider.cs - defensive 'encrypted is null' branch throws FileNotFoundException with a 'failed to read' message (#1501)
* fix(core): ServiceHelper.StartServices - Math.Max(startTimeout, DefaultServiceStartTimeoutSeconds) is dead floor (same family as #1462) (#1502)
* fix(core): SecureData.Decrypt - 'Returning original input' log message contradicts the immediately-thrown SecureDataIntegrityException (#1509)
* fix(core): ProtectedKeyProvider.GetOrGenerate - inconsistent retry backoff between IOException (exponential) and UnauthorizedAccessException (fixed 100ms) (#1512)
* fix(core): AppConfig.AllowLegacyV1Decryption - documented as a runtime migration toggle but is a static-readonly hard-coded false (no runtime override) (#1517)
* fix(core): AppConfig.ResolveExe - XML doc claims it throws FileNotFoundException but the implementation never throws (#1518)
* fix(core): Helper.WriteFileAtomic / WriteFileAtomicCore - duplicated 'retries = 3' and '100ms backoff' magic numbers across sync and async (#1522)
* fix(core): AppConfig.cs - TargetFramework field throws unconditionally even though it is only consumed inside #if DEBUG blocks (#1525)
* fix(core): SecureData.Decrypt - legacy fallback path emits two redundant 'returning as plaintext' warnings on a single decrypt failure (#1526)
* fix(core): ProcessKiller.KillParentProcesses - parent Process handle opened twice (once for StartTime, then re-opened to Kill) (#1527)
* fix(core): ProcessKiller.WalkAndKillChildren / KillParentProcesses - generic Logger.Warn drops the Exception (no stack), inconsistent with Logger.Error sites in the same file (#1528)
* fix(core): StringHelper.cs - XML doc cref points to wrong namespace 'Servy.Core.Security.EnvironmentVariableParser' (#1534)
* fix(core): ServiceValidationRules.Validate - service-dependency errors are joined into one Errors entry while every other check adds individual strings (#1547)
* fix(core): RotatingStreamWriter.EnforceMaxRotations - 'consecutiveDeletionFailures >= 10' threshold duplicated as a magic literal across the enumerate-failure and delete-failure paths (#1555)
* fix(core): RotatingStreamWriter.TripCircuitBreaker - 'CooldownMs / 60000' inline magic number for ms->minutes conversion in error message (same family as #1549) (#1556)
* fix(core): Logger.cs fallback log entries - inconsistent timestamp format vs. main log (no TZ marker) (#1559)
* fix(core): NativeMethods.ValidateCredentials - 'if (username.Contains("\\"))' is dead-conditional after regex pre-check, plus dead initialisations of 'domain'/'user' (#1563)
* fix(core): NativeMethods.ValidateCredentials - generic catch wraps Exception in SecurityException without preserving inner exception (#1575)
* fix(core): ProcessHelper.ResolvePath - throws InvalidOperationException but IProcessHelper XML doc declares ArgumentException (#1583)
* fix(core): Servy.Core Helper.IsValidPath / Helper.CreateParentDirectory - bare 'catch { return false; }' silently swallows IO/Access errors with no telemetry (same family as #1519) (#1586)
* fix(core): ServiceDependenciesValidator.Validate - error message '[ServiceDependencies] Invalid service name...' hardcoded in English; inconsistent with localized Strings.Msg_* in sibling validators (#1592)
* fix(core): XmlServiceValidator vs JsonServiceValidator - asymmetric parse-failure logging and inconsistent error wording across symmetric API pair (#1595)
* fix(core): ServiceDto.cs - ShouldSerialize methods redundant for properties already marked [JsonIgnore] + [XmlIgnore] (#1597)
* fix(core): SecureData ctor - second HKDF.DeriveKey throw leaves _v2EncryptionKey unzeroed in half-constructed instance (#1599)
* fix(core): ServiceValidationRules.Validate - Description-too-long uses Warnings while every other length check uses Errors (#1600)
* fix(core): ServiceValidationRules.Validate - paramFields length error doesn't identify which of 6 parameters exceeded the limit (#1601)
* fix(core): AppConfig.RecoverySchedulingDelay - missing 'Ms' suffix on a milliseconds constant, inconsistent with every other Ms-valued constant (#1603)
* fix(core): ServiceManager.InstallServiceAsync - Math.Max(total, floor) dead floor on totalWaitTime (same family as #1462) (#1604)
* fix(core): ServiceManager.StartServiceAsync - Math.Max(timeout, floor) dead floor (same family as #1462) (#1605)
* fix(core): ServiceManager.InstallServiceAsync - DeleteService rollback return value ignored on pre-shutdown/delayed-autostart failure (#1606)
* fix(core): ServiceManager.UninstallServiceAsync - ControlService(STOP) return value ignored, masking SCM stop-command failures (#1607)
* fix(core): EventLogLogger.SetIsEventLogEnabled - non-atomic _eventLog.Dispose()+null sequence racing with concurrent WriteEntry causes ObjectDisposedException (#1615)
* fix(core): ResourceHelper.CopyEmbeddedResource - returns success even when StartServices throws in finally; caller cannot detect stopped-and-not-restarted services (#1616)
* fix(core): Servy.psm1 / AppConfig.cs - 'SERVY_PASSWORD' env var name duplicated as a string literal in PowerShell and C# (same family as #1396) (#1631)
* fix(core): HandleHelper.GetProcessesUsingFile - unbounded WaitForExit() after a failed Kill can hang the caller indefinitely (#1634)
* fix(core): EventLogReader.MapToDto - garbled XML doc comment 'as as local on this OS' (duplicate 'as') (#1635)
* fix(core): EventLogReader.ParseLevel - Critical (1) silently collapsed to Error with no diagnostic (#1636)
* fix(core): NativeMethods.AtomicSecureMove - ERROR_NOT_SAME_DEVICE (0x11) inline magic number; Errors.cs is the central location (#1637)
* fix(core): NativeMethods.GetFileIdentity - 4KB prefix-digest false-negatives on files with identical headers (e.g. rotated logs with same log prologue) (#1638)
* fix(core): ProcessKiller.KillChildren - bypasses ancestor protection; descendants matching the Servy process chain can be killed (#1639)
* fix(core): ProcessKiller - '.exe' stripping logic duplicated three times with subtle skip in KillProcessesUsingFile (#1640)
* fix(core): EnvironmentVariableParser / EnvironmentVariablesValidator - delimiter array { ';', '\r', '\n' } duplicated between parser and validator (#1642)
* fix(core): ServiceControllerWrapper.BuildDependencyTree - ServicesDependedOn[] disposed twice on happy path (loop + finally) (#1643)
* fix(core): mlServiceValidator.TryValidate / JsonServiceValidator.TryValidate - 90% identical logic; only parser differs (#1645)
* fix(core): ProtectedKeyProvider - 'try { EventLog.WriteEntry... } catch (Exception) { Logger.Debug... }' block duplicated 3 times (#1646)
* fix(core): LoggerConfigurator.ConfigureFromAppSettings - inconsistent warning logging: LogLevel parse failure warns, every other field silently falls back (#1650)
* fix(core): Helper.IsRunningUnderXunit - public static method with zero callers in src/ and tests/ (dead code) (#1655)
* fix(core): ProcessKiller.KillParentProcesses - hardcoded '2' for PID-reuse tolerance instead of AppConfig.PidReuseToleranceSeconds (inconsistent with line 122 in same file) (#1659)
* fix(core): ResourceHelper.CopyEmbeddedResource - currentResourceName / currentTargetPath locals assigned but never read (dead variables) (#1660)
* fix(core): HandleHelper.GetProcessesUsingFile - inline magic 2000ms drain timeout inconsistent with AppConfig.HandleExeTimeoutMs/HandleExeRegexTimeout used in the same method (#1662)
* fix(core): ServiceDtoHelper.ApplyDefaults - name and XML doc hide that RunAsLocalSystem/UserAccount/Password are unconditionally overwritten (hidden side-effect) (#1663)
* fix(core): ValidationResult.IsValid - contradicts its own XML doc: returns false on any Warning even though docs state warnings 'may not block the operation' (#1664)
* fix(core): Logger.cs - dead null-conditional access on message after earlier null-empty guard (#1665)
* fix(core): ProtectedKeyProvider.cs - Migration warning logs use inconsistent EventIds across file log and Windows Event Log (#1666)
* fix(core): AppConfig.cs - MaxConfigFileSizeBytes recomputes the MB-to-bytes factor instead of using BytesInMegabyte (#1667)
* fix(core): AppConfig.cs - leftover '// MaxConsoleMaxLines already exists in AppConfig' placeholder comment (#1668)
* fix(core): ServiceManager.cs - delayed auto-start rollback doesn't check DeleteService return value (inconsistent with pre-shutdown rollback) (#1685)
* fix(core): Helper.cs - WriteFileAtomicCore async retry loop missing CancellationToken check that the sync version has (#1695)
* fix(core): ProtectedKeyProvider.cs - multi-process race during first run can produce divergent keys (#1701)
* fix(core): ProcessKiller.cs - KillProcessTreeAndParents(string) aborts all kills if any Process.ProcessName access throws Win32Exception (#1705)
* fix(core): ProcessKiller.cs - KillChildren skips PID-reuse identity check on first-level children (parentStartTime = DateTime.MinValue) (#1706)
* fix(core): NativeMethods.cs - class-wide [ExcludeFromCodeCoverage] hides coverage for ValidateCredentials/AtomicSecureMove/GetFileIdentity logic (#1710)
* fix(core): Helper.cs - EscapeArgs and EscapeBackslashes are ~90% duplicate; extract a shared core (#1712)
* fix(core): Logger.cs - FormatException truncation skips closing brackets, leaving unmatched [Inner -> markers (#1716)
* fix(core): Logger.cs - _initFallbackWriteCount / _logFallbackWriteCount never reset; long-running services go silent forever after 10 lifetime failures (#1717)
* fix(core): SecureData.cs - DecryptV2 HMAC failures bypass Logger.Error in catch filter (#1723)
* fix(core): HandleHelper.cs - GetProcessesUsingFile ignores handle.exe ExitCode; non-zero exits silently yield an empty list (#1726)
* fix(core): AppConfig.cs - hardcoded Version string '8.5' duplicates Servy.Core.csproj Version/AssemblyVersion and will drift after release bumps (#1732)
* fix(core): Logger.cs - log timestamp format 'yyyy-MM-dd HH:mm:ss' lacks milliseconds; events within same second cannot be ordered (#1736)
* fix(core): ServiceValidationRules.cs - duplicate Name-length check is unreachable dead code (#1737)
* fix(core): AppConfig.cs - static-ctor invariant check throws TypeInitializationException; better expressed as a unit test (#1739)
* fix(core): Helper.cs - NormalizePath trims drive-root backslash ('C:\' → 'C:'), changing semantics, and ignores AltDirectorySeparatorChar (#1744)
* fix(core): HandleHelper.cs - RegexMatchTimeoutException is swallowed, returning silent partial process list (#1745)
* fix(core): Helpers/ServiceHelper.cs - GetRunningServyServices enumerates SCM and registry twice (#1751)
* fix(core): ProcessKiller.cs - KillProcessTreeAndParents(string) duplicates work by calling Process.GetProcesses() after already building a Toolhelp32 snapshot (#1753)
* fix(core): EventLogService.cs / EventLogReader.cs - Error filter silently includes Critical events while comment claims they are collapsed (#1754)
* fix(core): [wiki] Security - Subdirectory list mentions a 'recovery' folder that the code never creates (#1755)
* fix(core): ResourceHelper.cs - GetHostProcessLastWriteTimeUTC logs Debug on dual probe failure, silently disabling resource re-extraction (#1759)
* fix(core): ServiceHelper.cs - StartServices timeout formula is inconsistent with CalculateStopTimeout; should be extracted into a symmetric helper (#1763)
* fix(core): ProcessHelper.cs - GetProcessTree has no parent-PID start-time check; metrics can include unrelated descendants of recycled PIDs (#1770)
* fix(core): EventLogLogger.cs - SourceExists matches ALL logs; existing 'Servy' source in a different log silently redirects events away from Application (#1773)
* fix(core): ServiceDtoImportValidator.cs - TException=Exception in both JSON/XML subclasses makes the catch-all unreachable and erases the intended parser-vs-unexpected error split (#1784)
* fix(core): Helper.cs / SecurityHelper.cs - duplicate, divergent reserved Windows device-name lists (#1798)
* fix(core): Helper.cs - HasAncestorReparsePoint throws NullReferenceException when passed a root-drive path (#1799)
* fix(core): NativeMethodsHelpers.cs - PrefixDigest embeds fs.Length, causing false-positive rotation detection on every poll for actively growing logs on FAT32/SMB (#1806)
* fix(core): ProtectedKeyProvider.cs - AbandonedMutexException is not caught; service fails to start after any process is killed while holding the key-generation mutex (#1808)
* fix(core): ProcessHelper.cs - GetProcessTree leaks Win32Exception from Process.StartTime, crashing metrics collection on permission-denied process queries (#1813)
* fix(core): ResourceHelper.cs - CopyEmbeddedResource logs 'successfully copied' in the restart-failure branch even when the copy itself was aborted by TerminateBlockingProcesses (#1817)
* fix(core): Service.cs - Stop is the only virtual public method; Start/Restart/Install/Uninstall asymmetrically sealed (#1818)
* fix(core): Helper.cs - IsServiceNameValid uses Msg_ServiceNameContainsTrailingWhitespace for both leading AND trailing whitespace (#1819)
* fix(core): ServiceManager.cs - InstallServiceAsync calls GetByNameAsync with decrypt:true but only reads Pid and PreviousStopTimeout; credentials are never used (preserveExistingCredentials:false) (#1820)
* fix(core): Helper.cs - WriteFileAtomic / WriteFileAtomicCore swallow retry exceptions silently; root cause is lost when retries are exhausted (#1821)
* fix(core): ServiceManager.cs - InstallServiceAsync leaves a partially-installed SCM service if SetServiceDescription or UpsertAsync throws after CreateService succeeds (no rollback) (#1823)
* fix(core): InstallServiceOptions.cs - three booleans typed as bool? (EnableConsoleUI, RecoveryOnCleanExit, PreStopLogAsError) while the other six are bool, with no documented reason (#1824)
* fix(core): IServiceRepository.cs - UpsertBatchAsync xmldoc claims 'NOT executed within an atomic transaction', but implementation wraps the batch in BeginTransaction/Commit (#1826)
* fix(core): ServiceControllerWrapper.cs - '(Unavailable)' / '(Access Denied)' display-name suffixes are hardcoded English, shown directly in the Manager dependency tree (#1828)
* fix(core): Helper.cs - HasAncestorReparsePoint silently returns false when target's immediate parent doesn't exist, bypassing reparse-point security guard (#1829)
* fix(core): ImportServiceCommand.cs - \?\UNC\ resolved-path prefix bypasses the post-handle UNC guard (#1837)
* fix(core): ExportServiceCommand.cs - UNC exfiltration guards do not cover subst / DefineDosDevice mappings to remote shares (#1838)
* fix(core): ServiceHelper.cs + ServiceManager.cs - three batch-loop call sites pay for unnecessary GetByNameAsync decryption (#1839)
* fix(core): Helper.cs - IsServiceNameValid reserved-name check is bypassed when service name starts with '.' (#1840)
* fix(core): StringHelper.cs - FormatServiceDependencies returns null while sibling FormatEnvironmentVariables always returns non-null, forcing every caller to defend with '?? string.Empty' (#1842)
* fix(core): Helper.cs - IsValidPath uses Path.IsPathRooted, accepts drive-relative paths like '\foo\log.txt' as 'absolute' (#1849)
* fix(core): Helper.cs - GetUniqueTempPath appends 37 chars; near-MAX_PATH destinations throw PathTooLongException on first write (#1850)
* fix(core): ResourceHelper.cs - TerminateBlockingProcesses kills locking processes (and stops Servy services) BEFORE checking the embedded resource exists (#1851)
* fix(core): ProtectedKeyProvider.cs - Global\ Mutex created without explicit DACL; SYSTEM service and user Manager fall into separate Local\ mutexes and can race key generation (#1854)
* fix(core): NativeMethodsHelpers.cs - ValidateCredentials rejects valid service accounts via LOGON32_LOGON_NETWORK when network logon is policy-denied (#1855)
* fix(core): EventLogLogger.cs - ScopedEventLogLogger.CreateScoped loses intermediate scope's prefix when scopes are nested (#1859)
* fix(core): RotatingStreamWriter.cs - disabled-rotation oversize warning is emitted on EVERY write while the circuit breaker is tripped (#1865)
* fix(core): LogTailer.cs - ReadLineAsync called without CancellationToken; tailer can't be cancelled while a slow-network/locked-file read is in flight (#1867)
* fix(core): ProcessKiller.cs - KillParentProcesses recurses UP before verifying the parent's identity; a PID-recycled immediate parent leaks the walk into unrelated ancestors (#1870)
* fix(core): Helper.cs - EscapeArgs xmldoc claims null-or-whitespace input returns empty string, but code only checks IsNullOrEmpty (#1872)
* fix(core): AppConfig.cs / HelpService.cs - Version uses ToString(2), causes false 'update available' prompt when assembly has a Build component (#1874)
* fix(core): ProcessKiller.cs - KillProcessTree reads process.StartTime unguarded; the entire tree kill is silently aborted on access-denied (#1877)
* fix(core): ServiceManager.cs - UpdateServiceConfig always returns true or throws; bool return type hides the contract, caller's '!updated' branch is dead code (#1880)
* fix(core): SecureData.cs - Decrypt xmldoc says 'returns the original cipherText' on failure, but actually throws SecureDataIntegrityException for marked payloads and raw-Base64 with legacy disabled (#1882)
* fix(core): EventLogReader.cs - ParseLevel(0) logs a spurious 'Unknown event log level' warning; level 0 is the documented LogAlways severity (#1890)
* fix(core): IProcessKiller.cs - KillProcessesUsingFile xmldoc states a hardcoded 'C:\Program Files\Sysinternals\handle64.exe' path that no longer matches the implementation (#1891)
* fix(core): EventLogService.cs - provider heuristic filter is dead in wildcard mode; redundant in normal mode (#1893)
* fix(core): ProcessMetrics.cs - RamUsage xmldoc claims 'Private Working Set' but ProcessHelper reads PrivateMemorySize64 (private bytes / commit) (#1894)
* fix(core): ProtectedKeyProvider.cs - SaveProtected compares the current user's SID against BuiltinAdministratorsSid (a group SID), so the guard never matches and the user ACL is always added (#1897)
* fix(core): ResourceHelper.cs - GetHostProcessLastWriteTimeUTC silently returns DateTime.MinValue when MainModule is null (no exception), skipping AppDomain fallback (#1903)
* fix(core): StringHelper.cs - FormatEnvironmentVariables does not escape literal CR/LF in values, breaking round-trip through Parse (#1902)
* fix(core): rotectedKeyProvider.cs - 'Exponential backoff: 100ms, 200ms, 400ms' comment is misleading; the 400ms sleep never executes (#1904)
* fix(core): NativeMethodsHelpers.cs - ValidateCredentials trims username but not domain; whitespace in DOMAIN\User splits leak into LogonUser (#1905)
* fix(core): ProcessHelper.cs - GetProcessMetrics evicts CPU sample on ArgumentException but not on InvalidOperationException (process-exited mid-call), causing stale baseline after PID reuse (#1914)
* fix(core): Servy.Core/Helpers/StringHelper.cs vs EscapedTokenizer.cs - Escape emits letter sequences \r/\n, but Unescape only recognizes literal CR/LF bytes; CR/LF in env-var values is lost on round-trip (#1920)
* fix(core): RotatingStreamWriter.cs - CloseWriter abandons _writer/_baseStream if Flush throws, leaking handles and wedging future writes/Dispose (#1928)
* fix(core): HandleHelper.cs - 'No matching handles found' English string match is fragile; locale or future Sysinternals rewording silently fails the operation (#1931)
* fix(core): ServiceExporter.cs - ExportJson(file) buffers the full JSON to a string before writing, while ExportXml(file) streams directly (#1932)
* fix(core): StringHelper.cs - NormalizeString silently merges adjacent env vars when a value ends with an unescaped backslash (#1934)
* fix(core): ResourceHelper.cs - CopyEmbeddedResource leaks the embedded resource Stream if the pre-using service-enumeration step throws (#1935)
* fix(core): Helper.cs - IsServiceNameValid calls segment.ToUpperInvariant() before lookup in a case-insensitive HashSet (#1938)
* fix(core): ProcessKiller.cs - WalkAndKillChildren reads child.StartTime without SafeStartTime guard; access-denied throws abort the whole descendant subtree (#1945)
* fix(core): ServiceDto.cs - ShouldSerializeDisplayName missing; null DisplayName appears in exports while sibling Description is suppressed (#1949)
* fix(core): HandleHelper.cs - HandleOutputRegex.IsMatch is not wrapped in try/catch for RegexMatchTimeoutException, while Matches() is (#1953)
* fix(core): AppFoldersHelper.cs - EnsureFolders self-skip compares normalized folder against raw AppConfig.ProgramDataPath (#1955)
* fix(core): EventLogReader.cs - ParseLevel xmldoc claims it 'prevents information loss' but body folds Critical (level 1) into Error (#1956)
* fix(core): ServiceHelper.cs - malformed 'bypasses prevents' xmldoc + asymmetric catch-all error wording between StartServices/StopServices (#1957)
* fix(core): Logger.cs - FormatException leaves unbalanced brackets on truncation (#1959)
* fix(core): RotatingStreamWriter.cs - EnforceMaxRotations orders by local time, ignoring useLocalTimeForRotation (#1960)
* fix(core): RotatingStreamWriter.cs - _consecutiveDeletionFailures incremented/reset outside the lock (#1961)
* fix(core): SecureData.cs - HKDF info parameters allocated per constructor call (#1965)
* fix(core): ProtectedKeyProvider.cs - GenerateRandomBytes can use static RandomNumberGenerator.Fill (#1966)
* fix(core): EscapedTokenizer.cs - O(n^2) backward scan on every delimiter check (#1970)
* fix(core): StringHelper.NormalizeString - round-trip data loss for env-var values ending in backslash or containing newlines (#1972)
* fix(core): ImportGuard.ValidatePathSecurity - TOCTOU between validation handle-open and caller's File.ReadAllTextAsync (#1976)
* fix(core): Helper.WriteFileAtomic - synchronous retry path uses Thread.Sleep that ignores the CancellationToken (#1980)
* fix(core): AppConfig.cs - misleading remarks claim 'static readonly' but fields are 'const'; runtime invariant check is non-existent (#1981)
* fix(core): Helper.cs - Canonicalise and NormalizePath diverge on drive-root semantics, risking comparison bugs (#1983)
* fix(core): ProcessKiller.cs - '.exe' suffix stripping is duplicated in three places (#1984)
* fix(core): ServiceManager.cs - GetServiceDescription and GetServiceUser silently return null on Win32 errors other than ERROR_INSUFFICIENT_BUFFER (#1985)
* fix(core): RotatingStreamWriter.cs - Monitor.Wait inside WriteInternal has no timeout; a stuck rotation hangs all writers indefinitely (#1988)
* fix(core): Magic Win32 error codes 31, 233, 1326 used inline instead of Errors.cs constants (#1990)
* fix(core): Logger.cs - FormatException sb.Length truncation can split UTF-16 surrogate pairs and corrupt log output (#1993)	
* fix(core): Logger.cs - second Initialize() overload silently drops settings if called after Shutdown() (#1999)
* fix(core): EventLogLogger.cs - nested CreateScoped produces double-bracketed prefix (e.g. '[[A] [B]] message') (#2000)
* fix(core): ImportGuard.ValidatePathSecurity - Reserved-device-name check rejects valid filenames where 'CON'/'NUL'/etc. appears as a non-leading segment (e.g. 'prefix.NUL.json') (#2006)
* fix(core): Helper.cs - WriteFileAtomicAsync never propagates the CancellationToken into the writer delegate (#2011)
* fix(core): SecurityHelper.ApplySecurityRules - PurgeAccessRules also removes Deny rules for Users/Authenticated Users/Everyone (#2012)
* fix(core): ProcessKiller.cs - PID-reuse temporal check is silently bypassed when StartTime cannot be queried (#2013)
* fix(core): ConfigParser.ParseEnum - Enum.IsDefined rejects valid combined values, silently breaking any future [Flags] enum (#2014)
* fix(core): ServiceManager.InstallServiceAsync - existing-service update path returns Failure on EnablePreShutdown but the description, start-type, and delayed-auto-start have already been mutated (#2054)
* fix(core): Logger - RotatingStreamWriter - re-entrant rotation deadlock: Logger.Warn inside PerformPhysicalRotation re-enters the same writer and blocks forever on Monitor.Wait (#2055)
* fix(core): AppFoldersHelper.EnsureFolders - XML doc lists 'logs' subfolder but code secures 'recovery' instead (#2060)
* fix(core): ProcessHelper.GetProcessTree - strict child-start-time check has no PidReuseToleranceSeconds, inconsistent with ProcessKiller/ProcessExtensions (#2065)
* fix(core): Helper.WriteFileAtomic / WriteFileAtomicCore - Flush() does not force a physical disk write, comment overstates the durability guarantee (#2066)
* fix(core): ServiceManager.InstallServiceAsync - OperationCanceledException logged as Error, inconsistent with Uninstall/Start/Stop which log it as Info (#2071)
* fix(core): ServiceManager.GetAllServices - LogOnAs defaults to LocalSystem before PopulateNativeDetails runs, so timed-out / faulted queries silently report the wrong account (#2072)
* fix(core): Helper.WriteFileAtomic / WriteFileAtomicCore - PrepareDestinationForMove runs outside the retry loop, so AV/EDR-induced AccessDenied on the destination file bypasses the documented retry policy (#2073)
* fix(core): HandleHelper.GetProcessesUsingFile - RegexMatchTimeoutException escapes the try/catch because MatchCollection enumerates lazily (#2079)
* fix(core): ProcessHelper.ResolvePath - legitimate paths containing literal '%X%' segments are rejected as 'unexpanded variable' when the file does not yet exist on disk (#2082)
* fix(core): NativeMethodsHelpers.ValidateCredentials - username regex allows newline / tab / NBSP via \s, producing misleading downstream errors (#2089)
* fix(core): ServiceHelper.CalculateStopTimeout - uncapped previousStopTimeout lets one abnormal stop poison every subsequent stop (#2091)
* fix(core): Helper.IsValidPath - uses Path.GetInvalidPathChars(), accepting filename-invalid chars like '<', '>', '|', '*', '?' that cause downstream open failures (#2106)
* fix(core): ServiceHelper.StopServices - no InvalidOperationException fallback like StartServices, so a benign race (service already stopped by another caller) is reported as an error (#2111)
* fix(core): ServiceManager.UninstallServiceAsync - pre-uninstall ChangeServiceConfig(DEMAND_START) is not rolled back when stop/delete fails, leaving the service silently switched to Manual (#2115)
* fix(core): ServiceManager.InstallServiceAsync - CREATE path applies PreShutdown then DelayedStart; UPDATE path applies them in opposite order (#2121)
* fix(core): NativeMethodsHelpers.GetFileIdentity - PrefixDigest never incorporates fs.Length, contradicting the inline comment; two rotated logs with identical 4096-byte prefixes hash to the same digest and rotation is missed (#2130)
* fix(core): EventLogService.SearchAsync - startDate/endDate with DateTimeKind.Unspecified silently shifts the filter window by the local UTC offset (#2131)
* fix(core): Logger.cs Log - sanitized message keeps '[' and ']', allowing log-line forgery via service name / process output (CWE-117) (#2132)
* fix(core): ImportGuard / ExportServiceCommand - reserved-device check misses trailing-space variants (sibling of #2069) (#2141)
* fix(core): ProtectedKeyProvider.SaveProtected - File.Move lacks AV/EDR retry that Helper.WriteFileAtomic has (#2152)
* fix(core): ServiceControllerWrapper.BuildDependencyTree - currentPath cycle check is O(n) per node → O(n^2) for deep dependency chains (#2163)
* fix(core): EventLogReader.MapToDto - ArgumentOutOfRangeException from 'new DateTimeOffset(evt.TimeCreated.Value)' is not caught; the in-code comment claims it is, but the fallback only fires when TimeCreated has NO value (#2170)
* fix(core): Helper.IsServiceNameValid - char.IsControl misses Unicode format category (ZWSP, RLO, LRM), allowing invisible/bidi chars in service names (#2171)
* fix(core): Logger.Log - single-line sanitization does not strip Unicode line separators (U+2028, U+2029), partially defeating the scannability guarantee (#2172)
* fix(core): IServiceManager - IsServiceInstalled and GetDependencies lack CancellationToken, asymmetric with sibling interface methods (#2176)
* fix(core): ScopedEventLogLogger.SetIsEventLogEnabled(true) silently no-ops if parent's _eventLog was never initialized (#2182)
* fix(core): Logger.FormatException - only walks AggregateException.InnerException (first); siblings via InnerExceptions are silently dropped (#2183)
* fix(core): ServiceDtoHelper.ApplyDefaultsAndResetIdentity silently discards UserAccount/Password from XML/JSON imports with no operator-visible warning (#2184)
* fix(core): ProcessKiller.CriticalSystemProcesses - missing Windows 10+ pseudo-system processes (Registry, MemCompression) (#2187)
* fix(core): SecureData - v1 legacy keying material (_v1MasterKey, _v1StaticIv) is loaded, cloned, and zeroed on every instance even though AllowLegacyV1Decryption is hard-coded false  (#2193)
* fix(core): ImportGuard.ValidatePathSecurity - handle-resolution validation block is silently skipped when GetFinalPathNameByHandle returns 0, bypassing the UNC re-check (#2196)
* fix(core): LogonAsServiceGrant.GrantLogonAsService - inconsistent handling of LsaClose return value vs HasLogonAsService (#2020)
* fix(core): ServiceHelper.StartServices - StopPending/PausePending services skip Start()/Continue() and then time out polling for Running (#2025)
* fix(core): WindowsServiceInfo.cs - XML doc cref references non-existent IWindowsServiceProvider; should be IWindowsServiceApi (#2030)
* fix(core): HandleHelper.GetProcessesUsingFile - timeout path's TimeoutException reports possibly-truncated stderr because async readers are not drained before throwing (#2043)
* fix(core): ServiceManager.MapStartupType - log message says 'Falling back to Manual' but the code returns ServiceStartType.Unknown (#2050)
* fix(core): AppConfig.cs - FindRepoRoot XML doc says 'only available in DEBUG builds' but the method is compiled and called in Release tests (#2084)
* fix(core): AppFoldersHelper.EnsureFolders - XML doc promises InvalidOperationException for malformed connection strings, but DbConnectionStringBuilder throws ArgumentException (#2090)
* fix(core): ServiceManager.RestartServiceAsync - Stop failure reason discarded; caller sees generic 'Failed to restart' instead of the real cause (#2122)
* fix(core): ResourceHelper.CopyEmbeddedResource - restart failure after successful copy returns false, so caller logs 'failed to extract embedded resource' for a copy that actually succeeded (#2124)
* fix(core): InstallServiceOptions - five nullable int?/bool? fields are asymmetric with non-nullable siblings; null leaks downstream because two of three call sites forget to apply '?? Default' (#2126)
* fix(core): ServiceHelper.cs StartServices - polling loop ignores Stopped state, mis-reports crashed-during-start as 'timed out waiting' (#2133)
* fix(core): ProcessHelper.GetProcessTree - inline parentToChildren snapshot duplicates Toolhelp32Snapshot.BuildSnapshotAndChildMap (#2146)
* fix(core): ServiceAccounts / GetLogOnAsDisplayName - LocalSystem has a 4-alias set but LocalService and NetworkService only match the canonical 'NT AUTHORITY\…' form (#2147)
* fix(core): ProcessKiller.KillProcessesUsingFile - invokes KillProcessTreeAndParents with default killParents=true, terminating unrelated parent processes that are not holding the file lock (#2048)
* fix(core): ServiceManager.InstallServiceAsync - cancellation between CreateService and the try/catch rollback block leaves an orphan SCM entry with no DB record (#2049)
* fix(core): EventLogReader.MapToDto - only FormatDescription is guarded; Id/Level/ProviderName/TimeCreated can throw EventLogException and abort the entire enumeration (#2061)
* fix(core): EventLogReader.ReadEvents - for-loop fetches one extra EventRecord past maxReadCount; a throw on that read aborts the whole batch the caller had already received (#2139)
* fix(core): StringHelper.FormatServiceDependencies - XML doc and nullable return type both promise null, but method never returns null (#2157)
* fix(core): StringHelper.Escape / EscapedTokenizer.Unescape - round-trip is broken for \r and \n in env-var values (#2158)
* fix(core): EnvironmentVariableParser.Parse - value trim-then-unescape is asymmetric with key unescape-then-trim, drops escaped trailing whitespace in values (#2159)
* fix(core): EventLogService.SearchAsync - IndexOf bracket check uses StringComparison.OrdinalIgnoreCase which is meaningless for '[' and ']' (#2160)
* fix(core): EventLogService.SearchAsync - comment claims 'records is a fully-materialized DTO sequence' but ReadEvents is a lazy yield-based iterator (#2161)
* fix(core): ConfigParser.ParseBool - only accepts 'True'/'False', rejects common config-file boolean variants ('1', '0', 'yes', 'no', 'on', 'off') (#2162)
* fix(core): EnvironmentVariablesValidator.Validate - short-circuits on first error, asymmetric with sibling ServiceDependenciesValidator which collects all errors (#2164)
* fix(core):  ProcessHelper.cs - UnexpandedEnvVarRegex static field declared but never referenced (dead code) (#2165)
* fix(core): EventLogLogger.ScopedEventLogLogger.CreateScoped - nested-prefix bracket-balancing trick ("{Prefix}] [{prefix}") silently breaks when a prefix contains '[' or ']' (#2166)
* fix(core): Domain/Service.cs - XML doc <returns> on Start/Stop/Restart/Install/Uninstall describe a bool result but methods return OperationResult (#2168)
* fix(core): Logger.FormatException - truncation path emits one unclosed '[' bracket when the size limit triggers on inner exception N (off-by-one) (#2169)
* fix(core): Domain/Service.cs - IsInstalled() and GetServiceStartupType() do not accept CancellationToken, breaking propagation from callers (#2177)
* fix(core): Logger.cs - now/tzMarker timestamp formatting duplicated across three sites (DRY) (#2185)
* fix(core): NativeMethods.PROCESS_BASIC_INFORMATION has 3 Reserved2_* fields where Win32 defines only Reserved2[2]; layout is 1 IntPtr larger than the OS struct, so UniqueProcessId would read garbage if NtQueryInformationProcess is ever wired up (#2198)
* fix(core): ServiceStartType.cs - XML doc cref references nonexistent 'AutoStart' member (should be 'Automatic') (#2200)
* fix(core): nstallServiceOptions.cs - XML doc / HelpText for Pre-Stop / Post-Launch / Post-Stop StartupDir contradict the actual fallback in Service.cs (#2208)
* fix(core): ServiceHelper.CalculateStartTimeout - does not account for PreLaunchRetryAttempts, causing 'Timed out waiting to start' for services with configured pre-launch retries (#2211)
* fix(core): ProtectedKeyProvider.SaveProtected - crash between WriteAllBytes and File.Move leaves encrypted .tmp files orphaned with elevated ACLs; subsequent runs never clean them up (#2213)
* fix(core): ProcessKiller.KillParentProcesses - recursive call passes stale parentStartTime (possibly MinValue) instead of verified exactStartTime, silently truncating the upward walk one level (#2214)
* fix(core): ImportGuard.ValidatePathSecurity - ReadToEnd has no size guard and runs before every caller's size check, so a giant import file OOMs the process before MaxConfigFileSizeMB rejects it (#2220)
* fix(core): SecurityHelper.CreateSecureDirectory - race-window comment is misleading; ACE purge does not mitigate handle-retention or squat attacks during the TOCTOU window (#2224)
* fix(core): XmlServiceSerializer vs ServiceExporter - divergent XmlSerializer caching and BOM encoding for identical ServiceDto XML pipeline (#2227)
* fix(core): ServiceDto.EnableDebugLogs XML doc claims data goes to Windows Event Log; actual destination is the local Servy.Service.log file (#2228)
* fix(core): DateRotationType.cs - XML doc claims 'per calendar day/week/month (local)' but RotatingStreamWriter defaults to UTC (useLocalTimeForRotation=false) (#2231)
* fix(core): SecurityHelper.ApplySecurityRules - DACL purge only removes Allow ACEs for Users/AuthenticatedUsers/Everyone; pre-squatted directory keeps attacker-added explicit Allow ACE (local privilege escalation) (#2235)
* fix(core): EnvironmentVariablesValidator.Validate accepts env-var values that EnvironmentVariableParser.Parse later rejects with FormatException (missing newline-validation parity) (#2238)
* fix(core): ReservedNames.cs - COM0/LPT0 are not Windows reserved device names; over-restriction rejects valid names and XML doc 'ports (0-9)' is wrong (#2240)
* fix(core): ServiceControllerWrapper.BuildDependencyTree - fullyExpanded memoization reuses path-dependent cycle placeholders, mislabeling shared/diamond dependencies as cyclic (#2241)
* fix(core): SecureData.cs DecryptV2 - corrupted v2 Base64 throws FormatException, not the documented SecureDataIntegrityException (#2242)
* fix(core): EventLogLevel.Verbose is preserved by ParseLevel but excluded from the Logs filter - asymmetric with Critical (folded into Error) (#2246)
* fix(core): ServiceDependenciesParser.Parse - nullable return type string? but the method never returns null (#2254)
* fix(core): SecurityHelper.CreateSecureDirectory - redundant SetAccessRuleProtection(true, false) call; ApplySecurityRules already sets protection unconditionally (#2256)
* fix(core): mportGuard.ValidatePathSecurityAndSize - 'content' out-param XML doc claims it outputs the validated path, but it returns the file contents (#2258)
* fix(core): EventLogReader.ParseLevel - XML doc says 'Verbose is preserved' but Verbose (level 5) is folded into Information (#2259)
* fix(core): NativeMethodsHelpers.GetFileIdentity - comment says '4-byte-len' but BitConverter.GetBytes(fs.Length) emits an 8-byte long (#2261)
* fix(core): RotatingStreamWriter.WriteInternal - lock-timeout log says 'drop current line' but the line is actually written (#2269)
* fix(core): ProtectedKeyProvider.GetCachedOrGenerate - XML doc promises 'a clone' but the generate path returns the original array (only the fast path clones) (#2270)
* fix(core): SecurityHelper.ApplySecurityRules - XML doc claims current user is skipped when in 'System or Admin groups' but code only excludes LocalSystem (#2271)
* fix(core): ServiceManager.GetServiceStartupType - inlines delayed-auto-start query that IsDelayedStart already encapsulates (DRY) (#2275)
* fix(core): ProtectedKeyProvider.SaveProtected - fixed staging file assumes mutex protection, but the v7.8 migration re-save runs outside the mutex (#2276)
* fix(core): ProcessKiller.cs - reserved system-PID threshold '4' hardcoded inline at four guard sites instead of a named constant (#2277)
* fix(core): Servy.Core ServiceHelper.CalculateStartTimeout - preLaunchRetryAttempts parameter is undocumented (missing <param> tag) (#2281)
* fix(core): ConfigParser.ParseEnum(string) - Flags parity check rejects valid canonical comma-separated combinations (asymmetric with int overload) (#2282)
* fix(core): ServiceManager.cs - AutomaticDelayedStart→Automatic start-type coercion duplicated in CreateService and UpdateServiceConfig (#2288)
* fix(core): EventLogService.cs - SearchAsync truncates to MaxResults before ordering; with default oldest-first reads the most recent events are never returned (#2295)
* fix(core): ResourceHelper.ShouldCopyResource - 'embeddedResourceTime' variable and debug log actually report the host executable's timestamp, not the embedded resource's (#2301)
* fix(core): Logger.cs - FormatException emits an unmatched trailing ']' for every formatted exception (off-by-one in structural-bracket balancing) (#2307)
* fix(core): EventLogReader.ReadEvents - the (maxReadCount+1)th EventRecord is read by the for-loop increment but never disposed, leaking a native handle (#2310)
* fix(core): ServiceDependenciesValidator.cs - XML doc summary omits the dollar sign '$' that the regex and inline comment allow (#2317)
* fix(core): IServiceManager.cs - GetDependencies XML doc claims reverse dependencies are returned, but implementation only resolves ServicesDependedOn (#2327)
* fix(core): IServiceManager.cs / ServiceManager.cs - InstallServiceAsync documents ArgumentNullException but actually throws ArgumentException for missing names/paths (#2328)
* fix(core): InstallServiceOptions.cs - EnableDateRotation doc uses <paramref name="dateRotationType"> referencing a nonexistent parameter (#2329)
* fix(core): ServiceManager.cs - StartServiceAsync/StopServiceAsync repository-null guard throws a misleading "Cannot install service" message (copy-paste) (#2330)
* fix(core): ServiceManager.cs - IsServiceInstalled/GetServiceStartupType throw ArgumentNullException for non-null whitespace names (inconsistent with sibling methods) (#2331)
* fix(core): XmlServiceSerializer.cs / XmlServiceValidator.cs - duplicated XXE-hardening XmlReaderSettings can drift between the two XML entry points (#2334)
* fix(core,service): PID-reuse start-time tolerance differs: ProcessKiller uses AddSeconds(-2), ProcessExtensions uses AddSeconds(-1) (#1571)
* fix(core,service): ProcessExtensions.BuildParentChildMapNative / ProcessKiller.BuildSnapshotAndChildMapNative - Toolhelp32 process-map building duplicated across Servy.Service and Servy.Core (#1641)
* fix(core,service): EnableDebugLogs XML doc (Domain/Service.cs, StartOptions.cs) wrongly claims env vars/params go to the Windows Event Log - they go to the local log file only (#2272)
* fix(infra): ServiceRepository.PatchRuntimeStateAsync/PatchRuntimeState - sync and async siblings duplicated verbatim (drift risk) (#1608)
* fix(infra): ServiceRepository.DecryptDto - mid-loop decryption failure leaves DTO half-decrypted, mixed plaintext + ciphertext returned to caller (#1609)
* fix(infra): ServiceRepository.UpsertBatchAsync - encrypted upsert + plaintext ID-resolution SELECT not wrapped in transaction; concurrent inserts/deletes can yield mismatched/missing IDs (#1610)
* fix(infra): SQLiteDbInitializer ApplyVersion2/3/5 - 'add column if missing' migration boilerplate duplicated three times with copy-paste try/catch/log (#1611)
* fix(infra): SQLiteDbInitializer.ReconcileSchema - 'self-healing' only adds missing columns; cannot detect renamed/removed/type-changed columns, silently masking real drift (#1612)
* fix(infra): DapperExecutor.CalculateBackoff - maxBackoffMs default 5000 is an inline magic number; sibling DB tuning lives in AppConfig (same family as #1556) (#1654)
* fix(infra): SQLiteDbInitializer.ReconcileSchema - XML doc summary and <param> blocks duplicated verbatim (two identical doc blocks above one method) (#1661)
* fix(infra): DapperExecutor.cs - WrappedDbTransaction.Dispose leaks Connection if Transaction.Dispose throws (#1687)
* fix(infra): SQLiteDbInitializer.cs - ApplyVersion4 silently drops orphan columns, masking missed rename migrations (#1708)
* fix(infra): ServiceRepository.cs - UpsertBatchAsync ID sync uses C# ToLowerInvariant against SQLite ASCII LOWER, missing non-ASCII service names (#1714)
* fix(infra): ServiceRepository.cs - ApplyRuntimeState preserves only 3 fields; importing a config silently wipes Password/UserAccount/RunAsLocalSystem/PreviousStopTimeout (#1765)
* fix(infra): SQLiteDbInitializer.cs - first-init schema migration is not atomic across processes; second Servy starter crashes with 'table Services already exists' (#1788)
* fix(infra): SQLiteDbInitializer.cs - AddColumnIfMissing/RenameColumnIfExists logs 'successfully migrated' even when no schema change occurred (#1797)
* fix(infra): DapperExecutor.cs - BeginTransaction leaks the connection if Open() or BeginTransaction() throws (#1805)
* fix(infra): SQLiteDbInitializer.cs - ReconcileSchema type-mismatch check assumes [SqlColumn] never contains anything other than 'TYPE NOT NULL', causing false positives if a DEFAULT/CHECK/COLLATE clause is ever added (#1810)
* fix(infra): SQLiteDbInitializer.cs - ApplyVersion3/5 hardcode column SQL types, decoupling migration path from [SqlColumn] SSoT on ServiceDto (#1835)
* fix(infra): DapperExecutor.cs - QueryFirstOrDefaultAsync is the only async query without a CommandDefinition overload; duplicates retry/rebind logic (#1907)
* fix(infra): SQLiteDbInitializer.cs - UpgradeLegacyDatabaseToVersion1 creates UNIQUE index without dedup, breaks migration if legacy DB has duplicate LOWER(Name) rows (#1944)
* fix(infra): SQLiteDbInitializer.cs - DELETE uses string interpolation for ID list instead of parameters (#1964)
* fix(infra): SQLiteDbInitializer.cs - UpgradeLegacyDatabaseToVersion1 relies on non-deterministic GROUP_CONCAT order to pick which duplicate to keep (#1973)
* fix(infra): ServiceRepository.cs - One corrupt service breaks GetAllAsync/SearchAsync for every other service (#1975)
* fix(infra): SQLiteDbInitializer.cs - ApplyVersion4 only logs orphan columns before dropping the table, silently losing their data (#1979)
* fix(infra): DapperExecutor.cs - Duplicate 'Retry loop exited' guard with identical message in sync and async paths (#2010)
* fix(infra): ServiceRepository.UpsertBatchAsync - does not preserve runtime state, clobbers Pid/ActiveStdoutPath/ActiveStderrPath/Password on batch import (#2051)
* fix(infra): DapperExecutor - no BeginTransactionAsync; ServiceRepository.UpsertBatchAsync blocks on synchronous connection.Open inside an async path (#2125)
* fix(infra): IServiceRepository.UpsertAsync - return-value contract says 'affected records' but impl returns service ID, making consumer 'affected <= 0' checks dead code (#2154)
* fix(infra): SQLiteDbInitializer.ReconcileSchema - detects type/nullability mismatches but only warns; 'Self-Healing' claim only covers missing columns (#2180)
* fix(infra): SQLiteDbInitializer.ApplyVersion4 - table rebuild silently loses every index/trigger/view on Services except idx_services_name_lower (#2181)
* fix(infra): DapperExecutor - sync methods recompute Unwrap(transaction) inside the retry lambda; async methods correctly hoist it outside (#2233)
* fix(infra): ServiceRepository.HandleCorruptServiceDecryption - duplicated word 'payload payload' in user-facing description text (#2244)
* fix(infra): ServiceRepository - GetByIdAsync/GetByNameAsync/GetByName decrypt without the HandleCorruptServiceDecryption guard used by GetAllAsync/SearchAsync (#2245)
* fix(infra): ServiceRepository.UpsertBatchAsync - PatchRuntimeState reads run outside the batch transaction (separate connection), breaking the documented snapshot isolation (#2257)
* fix(infra): ServiceRepository.cs - decrypt-then-handle-corruption block duplicated across 5 read methods (DRY) (#2293)
* fix(infra): SQLiteDbInitializer.cs - AddColumnIfMissing / UpgradeLegacyDatabaseToVersion1 emit ALTER ADD COLUMN without the NOT-NULL/DEFAULT guard that ReconcileSchema already has (#2297)
* fix(infra): ServiceRepository.cs - UpsertBatchAsync ID-sync chunk size (900) is a hardcoded magic number; SQLite parameter cap lives in AppConfig elsewhere (#2324)
* fix(service): ServySecurity.ps1 / Servy.Service ServiceHelper.cs - SensitiveKeyWords list duplicated across PowerShell and C# (must be hand-synced) (#1396)
* fix(service): ProcessLauncher.cs - ApplyLanguageFixes .jar branch is dead; UseShellExecute=false cannot directly launch .jar (#1444)
* fix(service): Service.cs - LogIssue local functions drop Exception parameter when failures are ignored (Pre-Launch and Pre-Stop) (#1484)
* fix(service): Service.cs OnCustomCommand fallback - logs 'Pre-Shutdown handling complete' BEFORE ExecuteTeardown actually runs (#1510)
* fix(service): Service.cs HandleStopResult - XML doc and 'canceled' wording mismatch the underlying TryStopGracefullyOrKill semantics (#1514)
* fix(service): ConsoleAppDetector.cs - generic 'catch { return false }' silently swallows IO/Access errors with no telemetry (#1519)
* fix(service): Service.cs - dead health check timer disposal in Cleanup() finally block (already disposed in ExecuteTeardown) (#1523)
* fix(service): ProcessLauncher.Start - subscribes to UnderlyingProcess events directly, bypassing IProcessWrapper abstraction (#1530)
* fix(service): ProcessLauncher.Start - drain timeout branch is unreachable; warning at line 271 is misleading dead code (#1539)
* fix(service): ServiceHelper.RestartService - magic numbers '60_000' and '1000' inline for ms->minutes/seconds display conversion (#1549)
* fix(service): Service.cs ConditionalResetRestartAttemptsAsync - try/catch around Environment.TickCount64 arithmetic is dead error-handling (#1598)
* fix(service): ServiceHelper.LogStartupArguments - inconsistent 'InSeconds' suffix for timeout fields (#1602)
* fix(service): Service.cs _cancellationSource may leak if Cancel() throws an exception other than ObjectDisposedException (#1672)
* fix(service): Service.cs - post-launch Task.Run captures _childProcess by closure; NullReferenceException if Cleanup nulls it during the wait (#1679)
* fix(service): Service.cs - RunSynchronousPreLaunch leaks IProcessWrapper on every retry (#1680)
* fix(service): Service.cs - background ConditionalReset task uses CancellationToken.None, can race with semaphore disposal at teardown (#1681)
* fix(service): Service.cs - SafeKillProcess timeout path logs misleading 'already exited' message (#1684)
* fix(service): ServiceHelper.cs - RestartProcess skips startProcess if stop throws, silently aborting recovery (#1702)
* fix(service): Service.cs - SafeKillProcess IsFaulted/IsCanceled branches are unreachable; faults propagate through Wait() and reach outer catch instead (#1704)
* fix(service): Service.cs - OnCustomCommand IsFaulted branch unreachable; AggregateException from Wait() escapes the SCM handler (#1713)
* fix(service): ProcessWrapper.cs - WaitAndCheckStillRunningAsync uses DateTime.UtcNow for timeout, vulnerable to clock changes (#1718)
* fix(service): Service.cs - HandleStopResult XML doc contradicts implementation for true/false/null cases (#1722)
* fix(service): ServiceHelper.cs - RestartProcess never disposes the old IProcessWrapper; every RestartProcess recovery leaks a Process handle (#1725)
* fix(service): ProcessLauncher.cs - ApplyLanguageFixes Java branch misses .jar despite docs promising it (#1740)
* fix(service): Service.cs - OnShutdown / OnCustomCommand reboot bypass skips FlushAndShutdownLogger, losing recent logs (#1741)
* fix(service): Service.cs - ConditionalReset background task reads _preLaunchEnabled before StartPreLaunchProcess sets it (#1743)
* fix(service): ServiceHelper.cs - RestartProcess skips descendant cleanup when parent has already exited (the common case) (#1746)
* fix(service): ProcessLauncher.cs - ApplyLanguageFixes misses py.exe (Python launcher) and python2; services lose UTF-8 fixes (#1747)
* fix(service): ProcessLauncher.cs - Helper.EnsureDirectoryExists is outside the lazy-init try/catch; persistent directory failures cause unbounded log spam (#1769)
* fix(service): StartOptionsParser.cs - EnvironmentVariableParser.Parse FormatException aborts every service start when stored env vars are malformed; no graceful fallback (#1782)
* fix(service): ServiceHelper.cs / IServiceHelper.cs - LogStartupArguments 'string[] args' parameter is dead; never referenced in the body (#1792)
* fix(service): EnvironmentVariableHelper.cs - ProtectedVariables list omits APPDATA/LOCALAPPDATA/HOMEPATH and other profile-redirection vectors (#1802)
* fix(service): Service.cs - OnOutputDataReceived/OnErrorDataReceived have no try/catch; writer exceptions can crash the service (#1807)
* fix(service): ProcessLauncher.cs - ApplyLanguageFixes lets RegexMatchTimeoutException propagate, crashing every Java service start when input regex times out (#1809)
* fix(service): ProcessLauncher.cs - if stdoutWriter.Dispose() throws in finally, orphaned child process is never killed (skips the !returnedOwnership cleanup branch) (#1816)
* fix(service): ervice.cs - HandleLogWriters compares StdOutPath/StdErrPath with raw OrdinalIgnoreCase, missing same-file detection for normalised variants (#1833)
* fix(service): Service.cs - OnOutputDataReceived/OnErrorDataReceived race with Cleanup can call WriteLine on disposed RotatingStreamWriter (#1834)
* fix(service): EnvironmentVariableHelper.cs - ProtectedVariables omits PROGRAMFILES / PROGRAMFILES(x86) / COMMONPROGRAMFILES; user-supplied env vars can redirect %ProgramFiles% to a malicious path (#1843)
* fix(service): ProcessLauncher.cs - ApplyLanguageFixes Python detection uses StartsWith and matches third-party tools (pythonista, python_wrapper, etc.) (#1844)
* fix(service): Service.cs - OnErrorDataReceived xmldoc says 'logs errors' but body only writes to stderr writer (#1847)
* fix(service): Service.cs - CleanupTrackedHooks re-acquires _trackedHooks lock its only caller is already holding (#1848)
* fix(service): ProcessWrapper.cs - CloseMainWindow Win32Exception escapes TryStopGracefullyOrKill catch, crashing graceful-stop attempts (#1852)
* fix(service): Service.cs - _isRebooting flag is never reset on RestartComputer failure, leaving the service permanently deaf to health checks and exits (#1863)
* fix(service): Service.cs - StartProcess briefly nulls _cancellationSource between Interlocked.Exchange and reassignment, breaking shutdown cancellation for concurrent readers (#1878)
* fix(service):  Service.cs - three 'if (process == null)' branches after ProcessLauncher.Start are unreachable; Start always returns non-null or throws (#1883)
* fix(service): EnvironmentVariableHelper.cs - inline-expansion overflow warning logs first 40 chars of raw env-var value, potentially leaking secrets to disk (#1884)
* fix(service): Service.cs - PersistProcessState calls GetByName with default decrypt:true but only mutates runtime-state fields (#1885)
* fix(service): IServiceHelper.cs - ValidateAndLog and EnsureValidWorkingDirectory xmldocs are out of sync with the actual signatures (#1895)
* fix(service): ProcessExtensions.cs - GetChildren duplicates the Toolhelp32 snapshot walk that GetAllDescendants already centralizes via Toolhelp32Snapshot.BuildSnapshotAndChildMap (#1911)
* fix(service): Servy.Service/Service.cs - ConditionalResetRestartAttemptsAsync adds PreLaunchTimeoutInSeconds AFTER the 1-hour cap, silently breaking the stated ceiling (#1916)
* fix(service): Servy.Service/Service.cs - OnStart's background restart-attempts reset task captures CancellationToken.None because _cancellationSource is still null at that point (#1917)
* fix(service): Servy.Service/Service.cs - production constructor xmldoc is missing <param> entries for processHelper and processKiller (#1918)
* fix(service): Service.cs - SafeKillProcess's totalTimeoutMs underestimates actual stop budget; each ProcessWrapper.Stop can consume up to 2*timeoutMs (graceful wait + post-kill wait) (#1919)
* fix(service): Service.cs - RunSynchronousPreLaunch retries with no delay/backoff between attempts (#1921)
* fix(service): Service.cs - FlushAndShutdownLogger leaves an orphan Task that mutates _logger after the 1.5s timeout returns (#1922)
* fix(service): Service.cs - StartProcess logs unexpanded placeholders in args twice (ExpandAndAudit + explicit call) (#1930)
* fix(service): Service.cs - RunSynchronousPreLaunch hardcodes waitChunkMs=250, ignoring the configurable _waitChunkMs field (#1936)
* fix(service): Service.cs - ConditionalResetRestartAttemptsAsync hardcodes 3600s cap instead of an AppConfig constant (#1937)
* fix(service): Service.cs - SafeKillProcess reports already-exited child as 'stopped gracefully' instead of 'already exited' (mainExitedGracefully default is true, not null) (#1947)
* fix(service): Service.cs - StartPreLaunchProcess asymmetric fallback: args expanded with main env vars, but process gets none (#1952)
* fix(service): Service.cs - RunFailureProgram's ExpandAndAudit call omits the context prefix used by every sibling hook (#1954)
* fix(service): EnvironmentVariableHelper.cs - ExpandWithDictionary uses both 'IndexOf > -1' and 'IndexOf >= 0' six lines apart (#1958)
* fix(service): Service.cs - Unnecessary (Func<Task?>) cast on Task.Run lambda (#1962)
* fix(service): Service.cs - Dead null-conditional on readonly semaphore fields (#1963)
* fix(service): ProcessLauncher.cs - Finally block calls process.HasExited even when Start() never succeeded (#1968)
* fix(service): ProcessWrapper.cs - Stop(timeoutMs) uses the same value for graceful timeout and post-kill wait (#1971)
* fix(service): ProcessLauncher.cs - Unused 'extension' local in ApplyLanguageFixes (#1974)
* fix(service): Service.cs - Dead File.Exists check in ConditionalResetRestartAttemptsAsync (#1982)
* fix(service): EnvironmentVariableHelper - self-referential custom variables silently lose their declared value (#1987)
* fix(service): ProcessLauncher.ApplyLanguageFixes - Inconsistent regex-timeout handling between Python (silent) and Java (logged) (#2015)
* fix(service): ServiceHelper.RestartComputer - relies on PATH resolution for 'shutdown' executable (#2016)
* fix(service): Environment-Variables.md - direct circular reference (A=%B%, B=%A%) is silently kept, not detected or logged (#2024)
* fix(service): Service.cs RunSynchronousPreLaunch - retry back-off Thread.Sleep loop ignores teardown/cancellation, blocking SCM Stop during pre-launch (#2035)
* fix(service): EnvironmentVariableHelper.ProtectedVariables - missing DOTNET_DiagnosticPorts and other CLR/.NET diagnostic injection vectors (#2037)
* fix(service): Service.cs HandleStopResult - XML doc claims null means 'timed out' but timeout no longer routes through HandleStopResult (#2046)
* fix(service): Service.cs StartProcess - assigns caller-supplied environmentVariables to _environmentVariables field without null guard, can NRE on RestartProcess (#2047)
* fix(service): Service.cs OnCustomCommand - fallback path calls Environment.Exit without flushing the logger, losing the diagnostic messages it just wrote (#2052)
* fix(service): Service.cs hook framework - only Pre-Launch supports dedicated environment variables; Pre-Stop / Post-Launch / Post-Stop / Failure Program reuse the main service envs (#2053)
* fix(service): Service.cs HandleLogWriters - stderr path is passed to Helper.Canonicalise before _pathValidator.IsValidPath runs, so a malformed StdErrPath throws and crashes OnStart (#2068)
* fix(service): Service.cs MakeFilenameSafe - does not strip trailing spaces/periods, allowing two distinct service names to collide on the same restartAttempts file (#2069)
* fix(service): Service.cs PersistProcessState - read-modify-write of full DTO with preserveExistingCredentials:false overwrites concurrent password/parameter updates from Servy.Manager (#2070)
* fix(service): ProcessLauncher.Start - return value of process.Start() is ignored, masking 'no new process started' for pre-launch / post-launch / pre-stop / post-stop / failure hooks (#2074)
* fix(service): Environment-Variables.md - Tips section warns against wrapping a literal '%' in another '%' but never documents how to actually escape a percent sign (#2075)
* fix(service): Service.cs MakeFilenameSafe - multi-extension reserved-device names (e.g. CON.log.gz) bypass the guard and collide with the actual DOS device (#2080)
* fix(service): Service.cs InitiateRecoveryAsync - _healthCheckSemaphore.WaitAsync uses no CancellationToken, throws ObjectDisposedException if teardown disposes the semaphore mid-wait (#2097)
* fix(service): Service.cs CleanupTrackedHooks - single try/catch wraps the whole foreach, so one hook.Dispose() throw leaks every remaining hook (#2098)
* fix(service): Service.cs ExecuteRecoveryAction - XML doc summary describes InitiateRecoveryAsync's responsibilities, not its own (#2099)
* fix(service): Service.cs StartPreLaunchProcess - XML doc references unresolved cref MinPreLaunchTimeoutSeconds (#2100)
* fix(service): IServiceHelper / ServiceHelper.ValidateAndLog - fullArgs parameter declared on interface and implementation but never used (#2101)
* fix(service): IServiceHelper.RestartProcess - XML doc references nonexistent terminateJobObject parameter; doc step 2 claims behavior that no longer exists (#2102)
* fix(service): Service.cs Cleanup - stdout/stderr writer Dispose exception leaks the other writer and leaves fields non-null (#2104)
* fix(service): Service.cs OnProcessExited - unconditional Warn 'Child process exit detected' fires even on configured clean exits (#2105)
* fix(service): Service.cs MakeFilenameSafe - reserved-name '_' prefix causes collision with user-supplied '_<NAME>' service (#2118)	
* fix(service): Service.cs ConditionalResetRestartAttemptsAsync - Math.Max/Min clamp silently bypasses the 1-hour cap when detection window exceeds it (#2119)
* fix(service): Service.cs OnCustomCommand - ExecuteTeardown return value discarded; SERVICE_STOPPED reported to SCM even when cleanup actually failed (#2120)
* fix(service): ProcessWrapper.TryStopGracefullyOrKill - CloseMainWindow exception returns null (interpreted as 'already exited'), skipping the force-kill path even when the process is still running (#2129)
* fix(service): EnvironmentVariableHelper.ProtectedVariables - missing Windows AppCompat / debugger injection vectors (__COMPAT_LAYER, SHIM_FILE_LOG, _NT_SYMBOL_PATH, …) (#2134)
* fix(service): EnvironmentVariableHelper.ProtectedVariables - missing PowerShell language-mode / execution-policy bypass vectors (__PSLockDownPolicy, PSExecutionPolicyPreference) (#2135)
* fix(service): Service.cs OnProcessExited / CheckHealth - _healthCheckSemaphore.WaitAsync uses no CancellationToken (siblings of #2097) (#2140)
* fix(service): ServiceHelper.RestartComputer - Process.Start null return is silently swallowed; failed reboot leaves no log entry (#2148)
* fix(service): Service.cs ExecuteTeardown - finally-block disposal cascade is not exception-safe (sibling of #2109/#2136) (#2151)
* fix(service): ProcessLauncher.ApplyLanguageFixes - Windows Python Launcher (py.exe / pyw.exe) not detected, UTF-8 / unbuffered env vars are not applied (#2153)
* fix(service): EnvironmentVariableHelper.ProtectedVariables - missing JAVA_OPTS / MAVEN_OPTS / CATALINA_OPTS and other JVM-launcher injection vectors (#2174)
* fix(service): Service.cs MakeFilenameSafe - XML doc block is duplicated (two identical /// summary/param/returns blocks back-to-back) (#2179)
* fix(service): StartOptionsParser.SafeResolvePath - only catches InvalidOperationException/ArgumentException; SecurityException, NotSupportedException, PathTooLongException from Path.GetFullPath propagate and crash OnStart (#2188)
* fix(service): Service.cs MakeFilenameSafe - Contains(seg, StringComparer.OrdinalIgnoreCase) degrades ImmutableHashSet O(1) lookup to O(N) LINQ scan (#2190)
* fix(service): Service.cs hook launchers - ExpandAndAudit args/env then ProcessLauncher.Start expands them a second time (double expansion, env audit work wasted) (#2191)
* fix(service): Service.cs InitiateRecoveryAsync - gate stays closed forever if RestartService/RestartComputer throws, silently dropping all subsequent recovery (#2062)
* fix(service): Service.cs ScheduleRecoveryAsync - SAFETY RESET writes _isRecovering outside _healthCheckSemaphore, racing with health-check evaluations (#2067)
* fix(service): ProcessWrapper.SendCtrlC - GenerateConsoleCtrlEvent return value discarded; method reports success on signal failure (#2212)
* fix(service): Service.cs StartProcess - Task.Run lambda reads capturedCts.Token after Interlocked.Exchange disposes it on rapid recovery cycles (#2222)
* fix(service): ProcessExtensions.Format - asymmetric fallback strings between InvalidOperationException and Win32Exception catches (#2230)
* fix(service): EnvironmentVariableHelper.cs - %% escape token decoded too early; literal %VAR% in custom values gets OS-re-expanded by the string overload (#2239)
* fix(service): ServySecurity.ps1 vs ServiceHelper.cs - MaskingRegex Branch B diverges (multi-word in PS vs single-word in C#), breaking the 'strict parity' contract (#2218)
* fix(service): EnvironmentVariableHelper.cs - memory-guard Substring truncation can split the PercentEscapeToken, leaking _SERVY_ESC_PERCENT_ fragments into the final env-var value (#2255)
* fix(service): Service.cs - Redundant nested 'if (!recoveryTriggered)' in ScheduleRecoveryAsync (dead guard) (#2263)
* fix(service): Service.cs - ExecuteRecoveryAction swallows all exceptions, making InitiateRecoveryAsync's failure handling dead code (recovery gate never reopens on failed terminal action) (#2265)
* fix(service): Service.cs OnStart - health-monitoring fields (_maxFailedChecks, _recoveryAction, _heartbeatIntervalSeconds) are initialized in SetupHealthMonitoring AFTER the child process and its Exited handler are wired up, creating a startup race (#2266)
* fix(service): EnvironmentVariableHelper.ExpandWithDictionary - inline length-guard truncation can split a PercentEscapeToken, leaving  marker fragments in the final expanded value (#2267)
* fix(service): EnvironmentVariableHelper.ExpandEnvironmentVariables - outer truncation guard corrupts an INTACT PercentEscapeToken at the boundary (distinct from #2267) (#2273)
* fix(service): Service.cs ExecuteRecoveryAction - attempt counter logged as '(0/unlimited)' on every recovery in unlimited mode (#2274)
* fix(service): ProcessLauncher.Start - process wrapper not disposed when process.Start() throws/returns false (dispose gated on processStarted) (#2278)
* fix(service): Service.cs RunFailureProgram - XML doc claims it runs 'when the main child process fails to start', but start failures never invoke it (#2280)
* fix(service): Service.cs - health-monitoring enable predicate duplicated in OnStart (_recoveryActionEnabled) and SetupHealthMonitoring (#2287)
* fix(service): StartOptionsParser.cs - SafeResolvePath XML summary claims it falls back to 'empty values' but actually returns the original raw path (#2299)
* fix(service): EnvironmentVariableHelper.cs - dictionary-builder OS expansion lacks the injected-% protection the string overload uses, re-expanding literal percent content (#2300)
* fix(service): Service.cs - RunFailureProgram has a truncated comment "// 1. Prepare Environment and" (#2302)
* fix(service): Service.cs - PRESHUTDOWN failure path sets ExitCode=1066 but UpdateServiceStatus hardcodes dwWin32ExitCode/dwServiceSpecificExitCode to 0, so SCM records a clean stop (#2305)
* fix(service): Service.cs - Dispose(bool) has two '// 2.' numbered comment steps (base-class call should be '// 3.') (#2306)
* fix(service): IServiceHelper.cs - RequestAdditionalTime XML doc uses unresolvable <see cref> to OnStart/OnStop/OnPause/OnContinue (#2340)
* fix(service): Service.cs - StartPostLaunchProcess / RunFailureProgram / StartPostStopProcess are near-duplicate fire-and-forget hook launchers (DRY) (#2341)
* fix(service): IServiceHelper.cs - RestartService XML doc omits the 'serviceName' parameter (#2349)
* fix(service): IProcessWrapper.cs - Format() has an empty <returns></returns> doc tag (#2350)
* fix(service): IStreamWriter.cs - Write(string text) is missing its <param> doc (#2351)
* fix(service): StreamWriterFactory.cs - Create return type drops the nullable annotation declared on IStreamWriterFactory (#2352)
* fix(service): Service.cs - OnCustomCommand hardcodes pre-shutdown pulse interval (2000) instead of an AppConfig constant (#2359)
* fix(restarter): Program.Main - 'reloadOnChange: true' is wasted on a one-shot console process; spins up a FileSystemWatcher that never reloads (#1565)
* fix(restarter): Program.Main - resource disposal order does not match reverse-of-construction (dbContext disposed AFTER scopedLogger/rootLogger) - same family as #1498 (#1589)
* fix(restarter): Program.cs - DefaultRestartTimeoutSeconds (120) defined as public const in Program.cs instead of AppConfig (#1567)
* fix(restarter): Program.cs - early returns skip Logger.Shutdown() and may lose recent log entries (#1750)
* fix(restarter): ServiceRestarter.cs - HandleTransitionalError can sleep past the budget; remaining-time check happens before WaitForStatus but not before Thread.Sleep (#1822)
* fix(restarter): Program.cs - GetByName decrypts 8 sensitive fields just to check service existence (#1836)
* fix(restarter): Servy.Restarter/Program.cs - duplicates EnableEventLog parsing + SetIsEventLogEnabled that LoggerConfigurator already performed three lines above (#1915)
* fix(restarter): ServiceRestarter.cs - RestartService's pending-state settle loop also sleeps past the budget (follow-up to #1822) (#1942)
* fix(restarter): ServiceRestarter.RestartService - throws System.TimeoutException internally but lets System.ServiceProcess.TimeoutException propagate from WaitForStatus, two unrelated exception types for the same logical timeout (#2083)
* fix(restarter): Program.cs Main - finally block disposal cascade is not exception-safe; one Dispose() throw skips Logger.Shutdown() and remaining cleanup (#2109)
* fix(restarter): ServiceRestarter.RestartService - pending-state settle loop is not protected against InvalidOperationException/Win32Exception from a deleted-mid-flight service, inconsistent with Stop/Start phases (#2110)
* fix(restarter): ServiceRestarter.RestartService - start-phase controller.Status read (line 90) is unguarded, unlike the settle phase, so a service uninstalled between stop and start throws an unhandled exception (#2298)
* fix(restarter): Program.cs - step '3. Configure the GLOBAL logging' comment sits above the restart-timeout parse, not the logging setup (#2347)
* fix(restarter): IServiceRestarter.cs - RestartService XML doc omits the 'timeout' parameter (impl uses inheritdoc, so the gap propagates) (#2348)
* fix(restarter): Servy.Restarter/publish.ps1 - comment-based help documents a -Version parameter that the param() block does not declare (#2358)
* fix(ui): AppBootstrapper.cs - resource disposal order does not match reverse-of-construction (same family as #1202) (#1498)
* fix(ui): AppBootstrapper.OnExit - 'TryDispose' helper is misused for a Cancel() call (misleading name) (#1505)
* fix(ui): AppBootstrapper.InitializeAppAsync - splash visibility threshold (1000ms) and minimum-display delay (500ms) are inline magic numbers (#1562)
* fix(ui): BulkObservableCollection - duplicated reset-notification block in AddRange and TrimToSize (#1568)
* fix(ui): AppBootstrapper.InitializeAppAsync - bool.TryParse(out showSplash) resets default 'true' to 'false' on malformed first arg (#1573)
* fix(ui): AppBootstrapper.OnStartup - 'FATAL: AppDomain Unhandled Exception.Process is terminating' missing space between sentences (#1574)
* fix(ui): Servy.UI HelpService - duplicated Process.Start+dispose 'open external URL' block in OpenDocumentation and CheckUpdates (#1585)
* fix(ui): WpfUiDispatcher.InvokeAsync(Action, DispatcherPriority) - 'if (_dispatcher == null) return;' is dead code; field is set in ctor via 'Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher' (#1588)
* fix(ui): ICursorService.SetWaitCursor returns IDisposable that two callers discard, then manually call ResetCursor in finally (API misuse / contract drift) (#1593)
* fix(ui): AppBootstrapper.cs - direct CLI invocation 'Servy.exe MyService' silently drops the service name when first positional arg isn't 'true'/'false' (#1719)
* fix(ui): App.xaml.cs - ServiceProvider singletons never disposed on OnExit (#1749)
* fix(ui): AppBootstrapper.cs -DispatcherUnhandledException handler shows MessageBox unconditionally; recurring background-tick faults cause modal-dialog spam (#1762)
* fix(ui): ServiceConfigurationValidator.cs (Servy & Servy.Manager) - XML doc references warnings, but ValidationResult has no Warnings and the code only shows errors (#1775)
* fix(ui): AppBootstrapper.cs - InitializeAppAsync catch block calls app.Shutdown() with no exit code; critical startup failures exit 0 and look green to CI/service wrappers (#1787)
* fix(ui): ImportGuard.cs - ValidateFileSizeAsync silently returns false when Path.GetFullPath throws, leaving the user with no feedback (#1803)
* fix(ui): AppBootstrapper.cs - FileSystemWatcher.Error handler logs a warning without capturing the underlying exception (#1841)
* fix(ui): AppBootstrapper.cs - InitializeAppAsync discards CopyEmbeddedResource's success bool; UI starts normally even when Servy.Service.exe / handle.exe extraction failed (#1858)
* fix(ui): ServiceCommands.cs - GUI ImportConfigAsync lacks UNC/reparse/protected-folder guards that CLI ImportServiceCommand enforces (#1861)
* fix(ui): AppBootstrapper.cs - hardcoded English MessageBox for missing handle.exe, while sibling failures use Strings.resx (#1923)
* fix(ui,core): App.xaml.cs (Servy, Servy.Manager) and ProcessHelper.cs - three more sites use Path.IsPathRooted as an 'is absolute' check (follow-up to #1849) (#1866)
* fix(ui): ImportGuard.cs and ExportServiceCommand.cs - same Contains("\UNC\") false-positive as #1875, but in two more files (#1879)
* fix(ui): AppBootstrapper.cs - OnExit calls CleanupAvailabilityWatcher() outside TryRun, breaking deterministic cleanup if Dispose throws (#1886)
* fix(ui): AppBootstrapper.cs - OnStartup re-checks _options for null even though the readonly field cannot be null after construction (#1943)
* fix(ui): ImportGuard.cs - reserved-device-name guard only checks first filename segment (mirror of #1939 in the UI security pipeline) (#1951)
* fix(ui): ServiceCommands.cs - Synchronous IsServiceInstalled and GetServiceStartupType run on the UI thread inside async methods (#1978)
* fix(ui): AppBootstrapper.cs - Dead null-conditional on readonly _appLifetimeCts field (#2007)
* fix(ui): AppBootstrapper.cs - Stale comment claims 'async void monitor' but method is async Task (#2008)
* fix(ui): AppBootstrapper.cs - FileSystemWatcher.Error handler is anonymous and never detached on cleanup (#2009)
* fix(ui): MainWindow.OnClosed calls Logger.Shutdown() before App.OnExit, silently dropping disposal warnings (#2027)
* fix(ui): AppBootstrapper.cs - FileSystemWatcher.Error in availability monitor only logs; does not refresh state, leaving UI stale after a buffer overflow (#2036)
* fix(ui): HelpService.CheckUpdates - HttpResponseMessage from GetAsync is never disposed, leaking the response object and pinning pooled connections longer than necessary (#2045)
* fix(ui): DesignTimeMocks.cs - DesignTimeServiceRepository declares duplicate GetByName overload that isn't on IServiceRepository (#2085)
* fix(ui): AsyncCommand.Execute - OperationCanceledException logged as Error, inconsistent with ServiceManager and noisy on user cancel (#2094)
* fix(ui): BulkObservableCollection.AddRange - partial mutation when source enumeration throws midway, leaves UI bindings out of sync (#2095)
* fix(ui): HelpService.CheckUpdates - non-numeric latest tag (e.g. 'v1.0.0-rc.1') silently parses to 0.0.0.0 and tells users 'no updates available' (#2128)
* fix(ui): DesignTimeMocks.cs DesignTimeHelpService - three orphan method overloads not on IHelpService (sibling of #2085) (#2145)
* fix(ui): InverseBooleanConverter.cs - ConvertBack XML doc claims 'otherwise true' but the method returns Binding.DoNothing (#2236)
* fix(ui): IHelpService.cs - OpenDocumentation 'caption' param doc copy-pasted from CheckUpdates (says 'during the update check') (#2342)
* fix(ui): BulkObservableCollection.cs - TrimToSize XML doc references ".NET Framework 4.8" but the project targets net10.0-windows (#2251)
* fix(ui): DesignTimeMocks.cs (Servy.UI) - DesignTimeCursorService.SetWaitCursor doc says 'Returns a dummy disposable' but the method returns void (#2354)
* fix(ui): MessageBoxService.cs - Application.Current.Dispatcher dereferenced without null guard (sibling WpfUiDispatcher guards it) (#2361)
* fix(desktop,manager): SplashWindow.xaml.cs (Servy + Servy.Manager) - leftover French XML doc comment 'Logique d'interaction pour…' inconsistent with English docs (#2339)
* fix(desktop): ServiceCommands.cs - rotationSizeValue ternary has dead null-coalescing branch (dto.RotationSize already non-null when > 0 is true) (#1478)
* fix(desktop): ServiceCommands.cs - XmlServiceSerializer/JsonServiceSerializer instantiated per-call instead of injected (#1491)
* fix(desktop): MainViewModel.cs - StopTimeout XML doc says 'start timeout' (copy-paste error from StartTimeout) (#1493)
* fix(desktop): MainViewModel.cs - BrowseFailureProgramPath XML doc references nonexistent property 'FailureProgramExecutablePath' (#1494)
* fix(desktop): MainViewModel.cs - inconsistent IsBusy reset (ResetIsBusy() helper vs. inline 'IsBusy = false') (#1495)
* fix(desktop): MainViewModel.cs - ResetToDefaults hardcodes RunAsLocalSystem/PreStopLogAsError instead of using AppConfig.Default* constants (#1709)
* fix(desktop): ServiceCommands.cs - Start/Stop/RestartService show generic 'Unexpected error' on UnauthorizedAccessException, unlike Install/Uninstall which show 'Admin rights required' (#1772)
* fix(desktop): ServiceCommands.cs - OperationResult.ErrorMessage is discarded on failure; users always see generic 'Unexpected error' even when the SCM returned a specific reason (#1815)
* fix(desktop): MainViewModel.cs - 50 property setters fire PropertyChanged unconditionally; only ServiceName/ServiceDisplayName check for actual change (#1950)
* fix(desktop): MainViewModel.cs - Two identical '#region Public Methods' regions in same class (#1995)
* fix(desktop): ServiceCommands.cs - same synchronous IsServiceInstalled/GetServiceStartupType freeze as #1978, this time in the desktop app (#1996)
* fix(desktop): ServiceCommands.cs - OpenManager swallows Process.Start exceptions and gives the user no feedback on failure (#1997)
* fix(desktop): ServiceConfigurationValidator depends on concrete ServiceValidationRules while Servy.Manager/Validators uses IServiceValidationRules - inconsistent coupling (#2044)
* fix(desktop): DateRotationTypeItem.cs - class summary copy-pasted from StartupTypeItem (says 'startup type' instead of date rotation type) (#2355)
* fix(desktop): MainViewModel.cs - SelectedDateRotationType / DateRotationTypes XML docs say 'startup type' (copy-pasted from startup-type members) (#2356)
* fix(desktop): ServiceCommands.cs - InstallService/UninstallService call IsServiceInstalled outside try/catch (asymmetric with Start/Stop/Restart) (#2360)
* fix(desktop): MainViewModel.cs - ServiceName XML doc claims it updates a 'ServiceControllerWrapper' instance that does not exist (#2363)
* fix(manager): ConsoleViewModel.cs - unused 'using Newtonsoft.Json.Linq' import (#1472)
* fix(manager): PerformanceViewModel.cs - garbled XML doc: 'buffdouble stepX ers' (corrupted 'buffers') (#1482)
* fix(manager): ConsoleViewModel.cs - 'SECURITY CHECK' label on session-id race guard is misleading (#1500)
* fix(manager): Mappers/ServiceMapper.cs - namespace 'Servy.Manager' doesn't match folder, inconsistent with Servy.Core.Mappers convention (#1537)
* fix(manager): LogTailer.RunFromPosition - GetFileIdentity called without IServyLogger; rotation/probe failures are silently swallowed (#1542)
* fix(manager): CopyPidCommand declared as { get; set; } in three ViewModels; all other IAsyncCommand properties (incl. ServiceRowViewModel.CopyPidCommand) use { get; } (#1543)
* fix(manager): erformanceViewModel - CopyPidCommand uses nameof(CopyPidAsync) (method) for AsyncCommand name; sibling ViewModels use nameof(CopyPidCommand) (property) (#1544)
* fix(manager): App.xaml.cs - Min/Max bounds for config values hardcoded as magic numbers; AppConfig defines Default* but no Min*/Max* siblings (#1545)
* fix(manager): DependenciesViewModel.LoadDependencyTreeAsync - redundant OnPropertyChanged(nameof(DependencyTree)) after ObservableCollection mutation (#1550)
* fix(manager): ServiceCommands.CopyPid - 'maxRetries = 5' hardcoded as local const while the matching delay lives in AppConfig.ClipboardComRetryDelayMs (#1552)
* fix(manager): MainWindow.xaml.cs - inconsistent parameter casing across HandleXxxTabSelected helpers ('consoleVM' / 'dependenciesVM' vs 'perfVm' / 'logsVm') (#1561)
* fix(manager): MainViewModel.cs - 'if (ServiceCommands == null) throw' guard duplicated across 6 command handlers; ServiceCommands is already null-guarded in ctor (#1576)
* fix(manager): MainViewModel.StopRefreshTimer - dead 'if (_appConfig != null)' check; _appConfig is set via constructor null-throw (#1577)
* fix(manager): App.xaml.cs - malformed LogLevel config value falls back silently; inconsistent with GetConfigInt which logs a warning (#1578)
* fix(manager): PerformanceViewModel.OnTickAsync - clears 'SelectedService.Pid' instead of snapshotted 'currentSelection.Pid'; can wipe PID of a different service if user changes selection mid-tick (#1579)
* fix(manager): PerformanceViewModel.CopyPidAsync - dead 'ServiceCommands == null' guard; field is set in ctor via '?? throw' (#1580)
* fix(manager): LogsViewModel.Cleanup - aliased to Dispose() makes the VM single-use; subsequent tab-switches leak CancellationTokenSource instances (#1581)
* fix(manager): DependenciesViewModel.OnTickAsync - same stale-selection PID bug as PerformanceViewModel (#1579) (#1582)
* fix(manager): CpuUsageConverter.Convert - XML doc says <param>The PID.</param> but the parameter is a double CPU usage value (#1584)
* fix(manager): Servy.Manager Service.CpuUsage - XML doc says 'Values can exceed 100.0 if the tree utilizes multiple cores' but producer ProcessHelper.GetProcessTreeMetrics clamps to 100.0 (#1587)
* fix(manager): MonitoringViewModelBase.OnTick - 'Interlocked.CompareExchange(ref _isMonitoringFlag, 1, 1)' used purely as a read; should be Volatile.Read (#1590)
* fix(manager): Servy.Manager LogEntryModel.LevelIcon - stale '// LOG: ... using .NET 4.8 switch syntax' comment; project targets modern .NET (#1591)
* fix(manager): ServiceSearchViewModelBase ctor - missing null checks for cursorService and uiDispatcher; inconsistent with rest of codebase (#1594)
* fix(manager): LogTailer.Dispose - non-atomic _isDisposed bool check; concurrent Dispose calls can double-cancel and double-dispose _disposeCts (#1614)
* fix(manager): MainViewModel.cs - Dispose leaks PerformanceVM/ConsoleVM/DependenciesVM and disposes ServiceCommands while child VMs still reference it (#1711)
* fix(manager): LogTailer.cs - batch.Clear() races with async consumer; high-volume batches arrive empty to UI (#1721)
* fix(manager): MainWindow.xaml.cs - RunAsync xmldoc claims async void but method is async Task (#1724)
* fix(manager): ServiceCommands.cs - ImportConfigAsync bypasses ExecuteLockedAsync, can race with Start/Stop/Install/Uninstall/Remove on the same service (#1727)
* fix(manager): LogsViewModel.cs - uses ObservableCollection.Add in a loop; should use BulkObservableCollection for large result sets (#1730)
* fix(manager): LogTailer.cs - unhandled exceptions trigger 1-second retry loop without circuit-breaker, spamming Logger.Error indefinitely (#1731)
* fix(manager): CpuUsageConverter.cs / RamUsageConverter.cs - design-mode check 'GetIsInDesignMode(new DependencyObject())' never returns true; designer crashes when opening MainWindow.xaml (#1733)
* fix(manager): ViewModels - OnTickAsync error logging is inconsistent across Console/Performance/Dependencies VMs (#1735)
* fix(manager): DependenciesViewModel.cs / ConsoleViewModel.cs - copy-pasted 'so we don't clear the zeros we just added' comment refers to graph zeros that don't exist here (#1761)
* fix(manager): ServiceCommands.cs - CopyPid silently fails after retries; user sees confirmation on success but nothing on failure (#1764)
* fix(manager): ViewModels/ServiceSearchViewModelBase.cs - Services collection uses plain ObservableCollection.Add in a loop; should use BulkObservableCollection like MainViewModel (#1776)
* fix(manager): MainViewModel.cs - Dispose doesn't drain _services row VMs; final snapshot leaks N ServiceRowViewModels with attached PropertyChanged handlers (#1777)
* fix(manager): LogEntryModel.cs - LevelIcon switch has no case for EventLogLevel.Critical (or Verbose); Critical events get the Info icon in LogsView (#1793)
* fix(manager): ServiceSearchViewModelBase.cs - SearchServicesAsync mutates Services without re-checking the token after await; an older search can overwrite a newer one's results (#1796)
* fix(manager): ConsoleView.xaml.cs - CopyMenuItem_Click uses raw Clipboard.SetText with no retry or COMException handling; transient clipboard locks crash the Manager UI (#1804)
* fix(manager): ServiceMapper.cs - GetLogOnAsDisplayName only handles LocalSystem; LocalService and NetworkService leak raw 'NT AUTHORITY\…' strings into the UI (#1825)
* fix(manager): ServiceCommands.cs - Install/Uninstall/Start/Stop discard res.ErrorMessage, always show generic Msg_UnexpectedError (#1832)
* fix(manager): ConsoleViewModel.cs - OnTickAsync mutates stale 'currentSelection' after await, can update the previous service when SelectedService changes during the DB call (#1856)
* fix(manager): PerformanceViewModel.cs - OnTickAsync graph data from previous service leaks into current view when selection changes during await (#1857)
* fix(manager): MainViewModel.cs - ExecuteBulkOperationAsync does not await the MessageBox; IsBusy/cursor reset before the user closes the dialog (#1864)
* fix(manager): LogTailer.cs - LoadHistory inline comment says '1 tick less' but code spaces synthetic timestamps by 1 millisecond (#1871)
* fix(manager): DependenciesViewModel.cs - OnTickAsync mutates stale 'currentSelection' after GetServicePidAsync; previous service's PID can overwrite the current view (#1892)
* fix(manager): LogLine.cs - Timestamp property xmldoc claims 'local time' but the value is always stored in UTC (#1898)
* fix(manager): LogTailer.cs - RunFromPosition's metadata-fallback rotation branch is dead code (covered by outer condition) (#1906)
* fix(manager): LogsViewModel.cs - FromDate/ToDate setters fire PropertyChanged twice for ToDateMinDate/FromDateMaxDate (#1908)
* fix(manager): LogsViewModel.cs - GetLogLevels excludes Critical/Verbose but xmldoc claims 'all available log levels' (#1909)
* fix(manager): MonitoringViewModelBase.cs - StartMonitoring is virtual but StopMonitoring is sealed; asymmetric override surface (mirror of #1818) (#1910)
* fix(manager): ConsoleViewModel.cs - ResetConsole's 'resetLabels' parameter is dead; every caller passes true (#1948)
* fix(manager): ServiceCommands.cs - Accidental unused 'using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window' (#1969)
* fix(manager): ServiceCommands.cs - SemaphoreSlim leak when ConcurrentDictionary.GetOrAdd factory races (#1989)
* fix(manager): MainViewModel.cs - DTO startup type clobbers freshly-fetched OS value on first refresh (#1994)
* fix(manager): ConsoleViewModel.cs - RequestScroll and RequestStatePreservation event delegates never unsubscribed on Dispose, retaining the View (#1998)
* fix(manager): ConsoleViewModel.cs - RequestStatePreservation event raised but never subscribed by any view; the 'preserve UI selection during log updates' contract is silently broken (#2001)
* fix(manager): ServiceMapper.cs - Stale 'future-proofing' comment promises checks for LocalService/NetworkService that are already implemented two lines above (#2004)
* fix(manager): ServiceCommands.RemoveServiceAsync - wraps async DeleteAsync in Task.Run, dropping the outer CancellationToken (#2017)
* fix(manager): ServiceCommands.Dispose - disposes SemaphoreSlim instances without guarding against concurrent in-flight operations (#2018)
* fix(manager): ServiceCommands.GetServiceDomain - drops CancellationToken on the repository fetch, blocking cancel during DB I/O (#2028)
* fix(manager): LogTailer.cs - Comment claims 'EXPONENTIAL BACKOFF' but the formula is linear (baseDelay * attempts) (#2029)
* fix(manager): IServiceCommands.cs - <param name="showMessageBox""> has a stray double-quote in three places (#2034)
* fix(manager): DependenciesViewModel.LoadDependencyTreeAsync - DependencyTree.Clear() runs before SelectedService null-check, causing UI flash when no service is selected (#2041)
* fix(manager): MainViewModel / LogsViewModel / DependenciesViewModel - Interlocked CTS-swap pattern races between Dispose and newCts.Token read, can throw ObjectDisposedException (#2042)
* fix(manager): LogTailer.RunFromPosition - position tracker uses reader.CurrentEncoding.GetByteCount, mis-counts on non-UTF8 BOM-less files and can drop bytes on transient error (#2064)
* fix(manager): PerformanceViewModel.cs - Task.Run drops CancellationToken in OnTickAsync, asymmetric with Servy.Manager ServiceMapper.ToModelAsync (#2081)
* fix(manager): ServiceMapper.GetLogOnAsDisplayName - does not recognize NT AUTHORITY\SYSTEM / .\LocalSystem aliases for the LocalSystem account, leaks raw SCM string into the UI (#2087)
* fix(manager): MainWindow.xaml.cs RunAsync / MainTabControl_SelectionChangedAsync - OperationCanceledException logged as Error on every tab switch / window close (#2096)
* fix(manager): LogsViewModel.Search - oldCts.Cancel() / Dispose() lack try/catch and finally; inconsistent with Cleanup() and leaks the CTS if Cancel throws (#2103)
* fix(manager): MonitoringViewModelBase / PerformanceViewModel / ConsoleViewModel - same Interlocked CTS-swap race as #2042 in shared base + two un-listed ViewModels (#2114)
* fix(manager): ServiceManager.GetAllServices - silent partial results when cancellation fires mid-Parallel.ForEach (#2123)
* fix(manager): ServiceSearchViewModelBase - same Interlocked CTS-swap race as #2042/#2114; concurrent SearchCommand vs. Dispose leaks the new CTS (#2127)
* fix(manager): oldCts.Cancel()/Dispose() lack try/catch in 11 additional ViewModel sites (sibling of #2103) (#2137)
* fix(manager): ServiceRowViewModel - StartCommand is enabled in StartPending/StopPending/Paused/None states, causing SCM errors on click (#2138)
* fix(manager): ConsoleViewModel.SwitchServiceAsync - List.Sort by Timestamp is unstable, so stdout/stderr lines with tied synthetic timestamps interleave non-deterministically in history (#2143)
* fix(manager): CpuUsageConverter and RamUsageConverter - near-identical scaffolding can be extracted into a shared base or generic (#2178)
* fix(manager): LogTailer.Dispose - _disposeCts.Cancel() / Dispose() lack try/catch; CTS leaks if Cancel throws (sibling of #2103 / #2137) (#2205)
* fix(manager): ConsoleView.xaml.cs CopyMenuItem_Click - Thread.Sleep retry loop runs on the WPF UI thread; freezes UI on clipboard contention (asymmetric with ServiceCommands.CopyPid which uses async Task.Delay) (#2229)
* fix(manager): ServiceSearchViewModelBase.SearchServicesAsync - stale superseded search clobbers the newer search's IsBusy/cursor/button state in finally (#2247)
* fix(manager): DependenciesViewModel.LoadDependencyTreeAsync - superseded load's finally resets IsBusy while a newer load is still running (#2248)
* fix(manager): LogsViewModel.Search - superseded search's finally resets IsBusy/cursor/SearchButtonText while a newer search is still running (#2249)
* fix(manager): LogsViewModel.cs - ToDate setter raises redundant/spurious OnPropertyChanged(nameof(FromDateMaxDate)) (#2264)
* fix(manager): LogTailer.RunFromPosition - per-line byte-offset estimation (incl. unused 'charCount' and per-line GetByteCount) is dead; lastPosition is always overwritten by fs.Position at EOF (#2283)
* fix(manager): DesignTimeAppConfig.cs - SearchDebounceDelayMs hardcodes literal 300 instead of referencing AppConfig.DefaultSearchDebounceDelayMs (#2325)
* fix(manager): App.xaml.cs (Manager) - LogLevel uses <inheritdoc/> but IAppConfiguration has no LogLevel member, so docs resolve to nothing (#2333)
* fix(manager): DesignTimeAppConfig.cs - MaxBulkOperationParallelism declared public while all other IAppConfiguration members use explicit interface implementation (#2335)
* fix(manager): IServiceCommands.cs / ServiceCommands.cs - ExportServiceToXml/JsonAsync lack CancellationToken (asymmetric with Import and every other async command) (#2336)
* fix(manager): LogsViewModel.cs - Search() XML doc references nonexistent <see cref="Logs"/> member (#2337)
* fix(manager): MainViewModel.cs - SearchServicesAsync finally resets IsBusy/cursor/button even when superseded by a newer search (#2338)
* fix(manager): IServiceCommands.cs / ServiceCommands.cs - CopyPid returns Task but lacks the Async suffix (inconsistent with every other async member) (#2343)
* fix(manager): IServiceCommands.cs - CopyPid <param> doc says 'The service to configure' (copy-pasted from ConfigureServiceAsync) (#2344)
* fix(manager): IServiceConfigurationValidator.cs - Validate returns Task<bool> but lacks the Async suffix (#2345)
* fix(manager): DependenciesView.xaml.cs - class summary copy-pasted from ConsoleView (says 'live-monitoring stdout/stderr') (#2346)
* fix(manager): MainWindow.xaml.cs - HandleMainTabSelected XML doc omits the consoleVm parameter (#2357)
* fix(manager): Cannot remove service with status "Not Installed" - Uninstall button greyed out (#2904)
* fix(manager,core): ServiceMapper.cs / ServiceAccounts.cs - XML doc/comment claims service-account strings come from WMI, but Servy uses no WMI (#2294)
* fix(cli): BaseCommand.ExecuteWithHandling - sync path missing OperationCanceledException catch present in async sibling (#1473)
* fix(cli): ImportServiceCommand.ProcessImportInternalAsync - same content is deserialized twice (once for validation, again inside repoImporter) (#1617)
* fix(cli): Program.cs - 'using System.Diagnostics' under #if !DEBUG is dead; nothing in the file references the namespace in Release (#1649)
* fix(cli): InstallServiceOptions.cs - XML doc 'Possible values' lists out of sync with [Option] HelpText (missing AutomaticDelayedStart, missing None) (#1656)
* fix(cli): Servy.CLI/Program.cs - Run() extracts embedded resources on every invocation, including --help and --version (#1760)
* fix(cli): ImportServiceCommand -> ServiceValidationRules - validator calls NativeMethods.ValidateCredentials (LogonUser) on imported user/password, but the deserializer immediately resets them to LocalSystem; failed imports can lock out the target account for nothing (#1781)
* fix(cli): ImportServiceCommand.cs - input path lacks the UNC block and junction/symlink resolution that ExportServiceCommand applies; admin imports can silently pull from \attacker\share via a junction (#1783)
* fix(cli): ImportServiceCommand.cs / ExportServiceCommand.cs - UNC infiltration/exfiltration guard bypassed via NTFS junction; check runs on un-resolved path only (#1786)
* fix(cli): ImportServiceCommand.cs / ExportServiceCommand.cs - file-level symlink bypasses UNC infiltration/exfiltration guard added in #1786; only directory junctions are resolved (#1790)
* fix(cli): ImportServiceCommand.cs - repoImporter return value discarded; CLI import reports success even when UpsertAsync persists 0 rows (#1794)
* fix(cli): ImportServiceCommand.cs - Reparse-point guard only inspects immediate parent directory; intermediate ancestor junctions bypass the check (#1795)
* fix(cli): Import/ExportServiceCommand - duplicate ConfigFileType parsing logic (TryParseFileType vs inline parse) (#1800)
* fix(cli): ExportServiceCommand.cs - typeLabel uses culture-dependent ToUpper() instead of ToUpperInvariant() (#1801)
* fix(cli): InstallServiceOptions.cs - broken XML doc cref 'Constants.DefaultUseLocalTimeForRotation' (no such symbol); actual default lives on AppConfig (#1814)
* fix(cli): BaseCommand.cs - HandleException builds error messages with hardcoded English ('Failed to {action}', 'Suggestion:'), while other failure paths localize via Strings.resx (#1827)
* fix(cli): Program.cs - EnsureEventSourceExists runs before argument parsing, forcing admin rights for --help / --version on first run (#1830)
* fix(cli): ImportServiceCommand.cs - UNC import guard bypassed by mapped network drive (DriveType.Network not checked) (#1831)
* fix(cli): InstallServiceCommand.cs - duplicate ParseEnumOption shadows ConfigParser.ParseEnum and silently swallows invalid enum values (#1845)
* fix(cli): ImportServiceCommand.cs - UNC normalization check uses Contains("\UNC\"), false-positives on local paths with a folder literally named UNC (#1875)
* fix(cli): ExportServiceCommand.cs - SaveFile writes content BEFORE the GetFinalPathNameByHandle UNC verification; data leaves the box before the security check fires (#1876)
* fix(cli): ExportServiceCommand.cs - SaveFile's outer catch re-wraps the deliberate UNC SecurityException, hiding the specific reason (#1887)
* fix(cli): ImportServiceCommand.cs - reserved-device-name guard only checks the FIRST filename segment; siblings (e.g. service.CON.json) slip through (#1939)
* fix(cli): ImportServiceCommand.cs - fileInfo.Length size check uses cached state from before the security probes, allowing files that grow past MaxConfigFileSizeBytes to be loaded fully into memory (#1946)
* fix(cli): ExportServiceCommand.SaveFile - OpenOrCreate without SetLength leaves trailing bytes from previous export when overwriting a longer file (#2019)
* fix(cli): ImportServiceCommand - Inlines existence and size check instead of using ImportGuard.ValidatePathAndSize (#2022)
* fix(cli): ExportServiceCommand.SaveFile - reserved-device-name check rejects valid filenames where 'CON'/'NUL'/etc. appears as a non-leading segment (#2038)
* fix(cli): ExportServiceCommand.SaveFile - generic catch-block File.Delete can erase pre-existing user data on transient I/O failure (#2039)
* fix(cli): ServiceInstallValidator.MapEnum - Enum.IsDefined rejects valid combined values, silently breaking any future [Flags] enum (same root cause as #2014) (#2040)
* fix(cli): RestartServiceCommand.cs - GetServiceStartupType drops CancellationToken, inconsistent with StartServiceCommand (#2076)
* fix(cli): UninstallServiceCommand.cs - repository DeleteAsync drops CancellationToken after SCM uninstall succeeds (#2077)
* fix(cli): ExportServiceCommand.SaveFile - protected-folder check evaluates input path, not resolved path; symlink can redirect write into a system directory (#2113)
* fix(cli): Program.cs Main - finally-block disposal cascade is not exception-safe (same pattern as #2109) (#2136)
* fix(cli): Program.cs Run - embedded ServyServiceCLI.exe copy failure only prints to Console; CLI continues with degraded/missing service binary while Servy.UI throws on the same failure (#2149)
* fix(cli): ServiceInstallValidator.MapInt - culture-sensitive int.TryParse diverges from ConfigParser.ParseInt (InvariantCulture) (#2156)
* fix(cli): ExportServiceCommand.SaveFile - SecurityException catch does not delete the stub file created by FileStream.OpenOrCreate, leaving 0-byte artifacts on disk (#2167)
* fix(cli): ExportServiceCommand.SaveFile - handle-resolution validation block is silently skipped when GetFinalPathNameByHandle returns 0, bypassing UNC and protected-folder re-checks (#2195)
* fix(cli): ExportServiceCommand.SaveFile - generic catch reports ordinary write failures (disk full, sharing violation, ACL denial) as 'Security Guard Failure: Target file handle validation rejected' (#2237)
* fix(cli): service-control commands - Start/Stop/Restart/Uninstall share a near-identical Execute skeleton (#2532)
* fix(psm1): Servy.psm1 - Format-SecureLogMessage hardcodes sensitive field list, decoupled from CLI options (#722)
* fix(psm1): misleading 'CLI moved or deleted' message also fires when CLI was never present at module load (#1496)
* fix(psm1): Invoke-ServyCli - 'null check' on $exitCode is dead (Process.ExitCode is non-nullable int) (#1497)
* fix(psm1): Marshal.PtrToStringAuto used with BSTR; PtrToStringBSTR is the canonical pair to SecureStringToBSTR (#1504)
* fix(psm1): PreLaunchTimeout PS ValidateRange (0..86400) does not match C# Max (0..1000) (#1516)
* fix(psm1): Install-ServyService - session-level $env:SERVY_PASSWORD is unconditionally Remove-Item'd in finally, breaking batch use (#1632)
* fix(psm1): Invoke-ServyCli - magic numbers 50ms (poll interval) and 5000ms (kill timeout) repeated three times inline (#1633)
* fix(psm1): Set-ServyConfig accepts zero or negative MaxBufferChars/TimeoutSeconds without validation (#1670)
* fix(psm1): -Deps ValidatePattern rejects valid service names containing dots (#1694)
* fix(psm1): -User ValidatePattern rejects gMSA accounts and UPN-style usernames (#1707)
* fix(psm1): Invoke-ServyCli omits StandardOutputEncoding/StandardErrorEncoding, mangles non-ASCII CLI output (#1715)
* fix(psm1): PreLaunchTimeout ValidateRange caps at 1000s while siblings allow 86400s (#1742)
* fix(psm1): Format-SecureLogMessage regex breaks on values containing escaped quotes, leaking the tail of sensitive arguments (#1860)
* fix(psm1): Format-SecureLogMessage uses [regex]::Replace with a script block, but PowerShell 2.0 cannot auto-convert script blocks to MatchEvaluator delegates (#1873)
* fix(psm1): Assert-Administrator calls $identity.Dispose() but WindowsIdentity does not implement IDisposable until .NET 4.5; throws on PS 2.0 / .NET 3.5 (#1924)
* fix(psm1): Install-ServyService silently drops password when PtrToStringBSTR yields empty string (#1967)
* fix(psm1): Invoke-ServyCli timeout calls Process.Kill() which orphans CLI child processes (#1977)
* fix(psm1): --envVars and --params values are exposed on the CLI process command line, defeating the log-scrubbing in Format-SecureLogMessage (#1986)
* fix(psm1): Format-SecureLogMessage unquoted-value pattern only masks first whitespace-delimited token, leaving multi-word values exposed (#1992)
* fix(psm1): Install-ServyService docstring EXAMPLE uses the deprecated -EnableRotation switch that the same module warns about at runtime (#2005)
* fix(psm1): Install-ServyService - DEVELOPER NOTE describes auto-derived 1:1 param mapping, but code uses an explicit $paramMapping hashtable (#2260)
* fix(psm1): Stdout/Stderr/PreLaunch* path validators reject bare relative filenames (empty-parent), inconsistent with Export Path validator (#2289)
* fix(psm1): stale comment: EnvVars 28,000-char cap is justified by 'CLI argument' limit, but EnvVars is now passed via environment variable, not the command line (#2303)
* fix(psm1): Install-ServyService -Deps ValidatePattern rejects '$', blocking dependencies on SQL Server named-instance services (MSSQL$INSTANCE) (#2312)
* fix(psm1): Params/PreLaunchParams/PostLaunchParams/PreStopParams/PostStopParams/FailureProgramParams lack the [ValidateLength] cap that EnvVars/PreLaunchEnv have, despite all being injected via environment variables (#2322)
* fix(test): tests/test.ps1 - Second '--' separator before Exclude filter makes the XAML/g.cs runsettings exclusion never reach vstest (#2023)
* fix(notifications): ServySecurity.ps1 Protect-SensitiveString - declares ValueFromPipeline=$true but has no process { } block, so pipeline input only processes the last value (#1513)
* fix(notifications): ServyFailureEmail.ps1 - ConvertTo-HtmlSafe doc claims 'PowerShell 2.0' compatibility, but script #Requires -Version 3.0 (#1503)
* fix(notifications): Servy-Watermark.psm1 - Update-Watermark failures create a feedback loop in the notification pipeline (no '^Failed to update timestamp file' filter) (#1533)
* fix(notifications): ServyFailureNotification.ps1 - stale comment references nonexistent EventIds.cs constant 'ScriptDependencyMissing' (#1536)
* fix(notifications): ServyFailureNotification.ps1 / EventIds.cs - event ID 3104 duplicated between PowerShell ('EVENT_ID_DEPENDENCY_ERROR') and C# ('ScheduledTaskScriptDependencyError') (same family as #1396) (#1618)
* fix(notifications): Write-ServyLog.ps1 - rotated log files never deleted; unbounded disk growth over time (#1621)
* fix(notifications): Write-ServyLog.ps1 - same-second rotation overwrites previously rotated log with Rename-Item -Force (#1622)
* fix(notifications): ServyFailureEmail.ps1 Send-NotificationEmail - ValueFromPipeline=$true without a process { } block (same family as #1513) (#1623)
* fix(notifications): Servy-Watermark.psm1 Write-FallbackError - Write-Host with -ForegroundColor produces nothing in Task Scheduler context (#1624)
* fix(notifications): ServyFailureNotification.ps1 - magic numbers for toast tag length (64), expiration (5 min) and inter-toast delay (500ms) (#1625)
* fix(notifications): ServyFailureEmail.ps1 - Port reader missing whitespace tolerance that UseSsl / TimeoutMs readers have (#1626)
* fix(notifications): ServyFailureEmail.ps1 - smtpServer/From/To validated with IsNullOrEmpty; whitespace-only values pass the gate (#1627)
* fix(notifications): Get-ServyLastErrors.ps1 - broken indentation around fallback dependency check (lines 87-101) (#1628)
* fix(notifications): Write-ServyLog.ps1 - rotation cleanup pattern resolves to '*.log' because $baseName_ is parsed as one variable (#1674)
* fix(notifications): Servy-Watermark.psm1 - Update-Watermark has a TOCTOU lost-update race between concurrent script instances (#1676)
* fix(notifications): Write-ServyLog.ps1 - collision-suffix rotation produces files ending in '.N' that the cleanup glob never matches (#1682)
* fix(notifications): Servy-Watermark.psm1 - Update-Watermark leaks StreamReader/StreamWriter and writes a UTF-8 BOM on every truncate (#1748)
* fix(notifications): Servy-Watermark.psm1 - ConvertFrom-ServyEventMessage regex fails on multiline event messages; service name shows as 'Unknown Service' whenever an exception is logged (#1774)
* fix(notifications): ServyFailureEmail.ps1 - Protect-SensitiveString regex timeout crashes the script with the watermark un-advanced, producing an infinite restart loop on the same event (#1780)
* fix(notifications): ServyFailureNotification.ps1 - Protect-SensitiveString regex timeout in Show-Notification halts the queue and never advances the watermark, producing an infinite retry loop on the poison event (#1811)
* fix(notifications): Write-ServyLog.ps1 - collision-retry loop's 'while (Test-Path $target -and $attempt -lt 100)' is parsed as a Test-Path command and throws ParameterBindingException (#1899)
* fix(notifications): ServyFailureNotification.ps1 - Show-Notification returns $true immediately after notifier.Show() (async); watermark advances even when the Failed event later signals delivery failure (#1925)
* fix(notifications): Servy-Watermark.psm1 - AddTicks(1) + strict > filter can silently drop events whose TimeCreated equals prev+1 tick (#2003)
* fix(notifications): ServySecurity.ps1 - 'keep in sync' comment incorrectly lists Servy.psm1's Format-SecureLogMessage (#2026)
* fix(notifications): ServyFailureNotification.ps1 - $ServiceName not masked in toast title, leaks raw event data (#2032)
* fix(notifications): ServySecurity.ps1 Protect-SensitiveString - space-separator branch only masks first token, leaking multi-word values (same root cause as #1992 but in this PS1 script) (#2056)
* fix(notifications): Write-ServyLog.ps1 - comment says 'no BOM' but Encoding.UTF8 emits a BOM; first write to a new log file is prefixed with U+FEFF bytes (#2057)
* fix(notifications): ServyFailureEmail.ps1 - ServicePointManager.SecurityProtocol not pinned, EnableSsl can negotiate down to SSL3/TLS1.0 on older Windows Server hosts (#2078)
* fix(notifications): Write-ServyLog.ps1 - rotation TOCTOU and unlocked AppendAllText race when multiple processes share a log file (#2088)
* fix(notifications): ServyFailureEmail.ps1 Send-NotificationEmail - generic catch classifies permanent errors (e.g. CRLF in Subject, MailMessage validation) as TransientFailure, freezing the email queue forever (#2093)
* fix(notifications): ServyFailureEmail.ps1 - 'To' validation regex rejects multi-recipient configurations even though MailMessage.To.Add supports them (#2107)
* fix(notifications): ServyFailureNotification.ps1 Show-Notification - 750ms add_Failed wait misses async toast delivery failures (Focus Assist, disabled notifications, OS suppression); watermark advances and error is lost forever (#2108)
* fix(notifications): Servy-Watermark.psm1 Update-Watermark - duplicate notifications when lock contention exceeds 1s budget (#2194)
* fix(notifications): ServyFailureEmail.xml - RunOnlyIfNetworkAvailable is false, so the task fires on every Servy error event even when no network is reachable (#2209)
* fix(notifications): ServyFailureEmail.vbs / ServyFailureNotification.vbs - shell.Run exit code discarded, so Task Scheduler always records 0x0 even when the PowerShell child fails (#2210)
* fix(notifixations): ServyFailureEmail.ps1 Send-NotificationEmail - '-bor Tls12/Tls13' does not pin TLS; SSL3/TLS 1.0 stay enabled where the runtime default included them (#2225)
* fix(notifications): setup/taskschd/ServyFailureNotification.xml + ServyFailureEmail.xml - DisallowStartIfOnBatteries=true combined with StartWhenAvailable=false silently drops failure alerts on battery-powered hosts (#2234)
* fix(notifications): Write-ServyLog.ps1 - SHA256 instance created per call is never disposed (handle/memory churn under frequent logging) (#2252)
* fix(notifications): Write-ServyLog.ps1 - AbandonedMutexException silently drops the log line and re-abandons the mutex, permanently disabling file logging until process restart (#2253)
* fix(notifications): ServyFailureEmail.ps1 / ServyFailureNotification.ps1 - dangling, nonsensical comment inside the dependency-missing Write-EventLog block (#2284)
* fix(notifications): ServyFailureNotification.ps1 - if CreateToastNotifier throws, $notifier stays null and $notifier.Show() NPEs; the 'fall back gracefully to standard delivery' comment is not implemented (#2285)
* fix(notifications): ServySecurity.ps1 - header says masker parity is with 'Servy.Core' but the inline sync comment correctly points to Servy.Service/Helpers/ServiceHelper.cs (#2290)
* fix(notifications): ServyFailureEmail.ps1 - multi-recipient validation block (lines 209-224) is dedented to column 0, breaking Send-NotificationEmail indentation (#2291)
* fix(notifications): ServyFailureNotification.ps1 - no 'permanent failure advances watermark' path (asymmetric with ServyFailureEmail.ps1); a permanently-undeliverable toast stalls the queue forever (#2292)
* fix(notifications): ServyFailureNotification.ps1 - no-op 'continue' in delivery-status switch with a nonsensical comment; loop break is actually handled by the if below (#2296)
* fix(notifications): Servy-Watermark.psm1 - Read-Watermark does not Trim() file content before ParseExact, asymmetric with Update-Watermark which does (#2308)
* fix(notifications): ServyFailureNotification.ps1 - Show-Notification's add_Failed handler calls Write-FallbackError from a WinRT callback thread that has no PowerShell runspace (#2309)
* fix(notifications): ServyFailureNotification.ps1 - notification-suppressed branch logs "Skipping watermark advance" but returns 'PermanentFailure', which advances the watermark and permanently drops those failures (#2311)
* fix(notifications): Write-ServyLog.ps1 - Global\ mutex creation fails for non-elevated standard users, silently dropping all fallback file logging (#2314)
* fix(notifications): Get-ServyLastErrors.ps1 - first-run query is unbounded (no -MaxEvents), loads every historical Servy error just to use the most recent one (#2315)
* fix(notifications): ServyFailureNotification.ps1 - Notification-suppression probe returns 'TransientFailure' but both comments say permanent / 'save the watermark', causing head-of-line blocking (#2316)
* fix(notifications): ServyFailureEmail.ps1 - SmtpException classification treats GeneralFailure (-1) as transient, stalling the queue forever on permanent auth/config errors (#2318)
* fix(notifications): ServyFailureNotification.ps1 - Unreachable catch [RegexMatchTimeoutException] around Protect-SensitiveString (#2320)
* fix(notifications): Get-ServyLastErrors.ps1 - first-run -MaxEvents 1 defeats the feedback-loop pre-filter, masking a genuine crash (#2321)
* fix(bump-runtime): per-file try/catch is broken by ErrorActionPreference=Stop; one bad file aborts the whole runtime bump (#1657)
* fix(bump-runtime): doesn't update global.json SDK version; CI breaks after runtime bump (#1698)
* fix(bump-runtime): global.json version regex only updates major.minor, leaves stale build number that may not exist as an SDK (#2063)
* fix(bump-runtime): Get-FileEncoding.ps1 - UTF-32 Big Endian BOM (00 00 FE FF) is not detected; UTF-32 BE files are misclassified as UTF-8 (#2201)
* fix(bump-version): Update-FileContent - Write-Error + ErrorActionPreference='Stop' makes 'return' dead code; one missing pattern aborts whole bump mid-flight (#1521)
* fix(bump-version): no per-file isolation; one bad file aborts the entire version bump (related to #1657) (#1658)
* fix(bump-version): DESCRIPTION lists publish.ps1/publish-sc.ps1/publish-fd.ps1 as updated targets, but the script never touches them (#1756)
* fix(bump-version): Update-FileContent silently returns and exits 0 when regex pattern stops matching, allowing stale versions to ship (#1789)
* fix(bump-version): version-tag regex does not support <Tag Condition="..."> form, silently skipping conditional version metadata (#2086)
* fix(publish): publish.ps1 / publish-sc.ps1 / publish-fd.ps1 - Tfm + Version defaults duplicated across three scripts (drift risk on bumps) (#1570)
* fix(publish): tools-config.ps1 - Resolve-Tool reads undefined $envPath; SERVY_TOOL_* env var lookup is dead code (#1620)
* fix(publish): signpath.ps1 - 'Write-Error … return' pattern at six sites swallows fatal errors when called inside try/catch (#1629)
* fix(publish): publish-common.ps1 Build-Installer - magic numbers maxRetry=3 and Start-Sleep -Seconds 2 should be parameters/constants (#1630)
* fix(publish): signpath.ps1 - inline comments treated as part of config value, breaking SIGN flag check (#1688)
* fix(publish): signpath.ps1 - BuildData.Url constructed with no validation that GitHub env vars are set (#1689)
* fix(publish): publish-common.ps1 - Build-Installer retry loop catches all exceptions, wasting time on non-transient errors (#1692)
* fix(publish): publish.ps1 - $LASTEXITCODE check after PowerShell child scripts is unreliable (#1700)
* fix(publish): publish-sc.ps1 - Step 3 packaging catch block writes an error but never sets a non-zero exit code; failure leaves the workflow green (#1757)
* fix(publish): publish.ps1 - outer catch swallows failures and never sets a non-zero exit code; CI sees green on red builds (#1768)
* fix(publish): tools-config.ps1 - Resolve-Tool's Get-Command lookup omits -CommandType Application; an alias/function/script with the tool's name shadows the real executable (#1779)
* fix(publish): publish-res.ps1 - local Assert-LastExitCode duplicates common-helpers.ps1 but is missing the 'exit $LASTEXITCODE' line (#1791)
* fix(publish): publish-common.ps1 - New-PortablePackage passes paths via Start-Process -ArgumentList; breaks the 7z command if the package path contains a space (#1926)
* fix(publish): publish-common.ps1 Build-Installer - AV-lock retry regex never matches; only retries on the rare LASTEXITCODE=-1 (#2033)
* fix(publish): signpath.ps1 - failed signing leaves orphan $Path.signed on disk; a later run silently moves the stale artifact over the real binary (#2092)
* fix(publish): publish-res-{debug,release}.ps1 (×8 files) - builds a self-referential path '..\<SelfDir>\Resources' instead of 'Resources' (#2186)
* fix(publish): signpath.ps1 - inline-comment regex strips '\s+#.*' from inside quoted values, dropping the closing quote and corrupting the value (#2199)
* fix(publish): signpath.ps1 - 'elif' is not a PowerShell keyword; single-quoted and unquoted config values are never parsed (signing silently mis-skipped) (#2313)
* fix(publish): publish-res.ps1 - two large commented-out artifact-copy blocks left in the script (#2530)
* fix(publish): setup/publish.ps1 - comment-based help still documents a framework-dependent build and a -IncludeFrameworkDependent switch the script no longer implements (#2563)
* ci: Multiple workflows (sonar/scoop/choco/changelog/bump-version/dotnet-reflection/build/sbom/wiki/release) - Third-party actions pinned to mutable tags (same family as #607, #1304, #1309, #1342) (#1360)
* ci(dotnet): .github/actions/setup-dotnet/action.yml - Authenticode SignerCertificate.Subject regex check uses unanchored substring match, allowing any cert whose O= field contains 'Microsoft Corporation' as a substring (#2204)
* ci(dotnet): setup-dotnet/action.yml - comment claims 'Using -Version instead of -Channel' but the script passes -JsonFile (#2560)
* ci(build): build.yml - dotnet restore loop ignores $LASTEXITCODE; later --no-restore builds fail with misleading error (#1734)
* ci(test): test.yml - Third-party actions pinned to mutable tags, supply chain risk (same as #607, different workflow) (#1304)
* ci(test): test.yml - $args shadowed in test loop; PowerShell automatic variable should not be reassigned (#1699)
* ci(test): 'dotnet tool install dotnet-reportgenerator-globaltool --version 5.*' uses a floating major version, breaking build reproducibility (#1940)
* ci(security): security.yml - Third-party actions pinned to mutable tags, supply chain risk in the security-audit workflow itself (same as #607, #1304) (#1309)
* ci(sonar): sonar.yml SONAR_TOKEN secret interpolated directly into shell command instead of using env var (#1673)
* ci(publish): publish.yml - Third-party actions pinned to mutable tags, supply chain risk (#607)
* ci(publish): version extraction reads stale parameter default from publish.ps1, always yields empty string (#1690)
* ci(publish): publish.yml - section comment says 'Extract Version from setup/publish.ps1' but the step reads setup/build-config.ps1 (#2559)
* ci(bump-version): minor-version rollover at 9 prevents 10+ minor versions; never publishes 8.10, 9.11, etc. (#1648)
* ci(bump-version): leftover commented-out tag-resolution block in 'Set version vars' step (#1669)
* ci(bump-version): unchecked 'git push' calls on main and net48 branches; same failure mode as #1729 and #1869 (#1881)
* ci(bump-version): ./bump-version.ps1 invoked without Invoke-Git/exit-code check; script failure is silently treated as 'no changes to commit' (#2203)
* ci(winget): winget.yml - winget-releaser action pinned by SHA with no version comment, unlike every other pinned action in the repo (#2529)
* ci(choco,scoop,bump-version): choco.yml, scoop.yml & bump-version.yml - identical 'Set version vars' tag-resolution block repeated in three workflows (#2527)
* ci(choco): chocolateyuninstall.ps1 - Test-Path fails on UninstallString with quoted path or embedded args, silently skips uninstall (#1691)
* ci(choco): choco.yml - choco pack / choco push native exit codes are not checked; a failed pack still triggers a push that can upload a stale nupkg or report green on a broken package (#1812)
* ci(choco): chocolateyuninstall.ps1 - $extraArgs is parsed from the registry UninstallString but then silently discarded; any registered uninstall flags are ignored (#2197)
* ci(choco): choco.yml - retry loop never aborts a paused rebase; one git pull conflict deadlocks all 5 attempts (#2216)
* ci(choco): choco.yml - 'nothing to commit' check uses '$(git status --porcelain) -eq $null', deviating from the '-ne '''' idiom in scoop.yml/bump-version.yml (#2561)
* ci(scoop): scoop.yml - Extras PR step uses 'git push --force' instead of '--force-with-lease', risking overwrite of concurrent updates (#1693)
* ci(scoop): scoop.yml - 'if (git show-ref --verify --quiet ...)' always evaluates to false in PowerShell (#1683)
* ci(scoop): scoop.yml - Write-ScoopManifest doubles every leading-space block, producing 8-space JSON indentation under pwsh 7 (#1697)
* ci(scoop): multiple unchecked 'git pull' and 'git push' calls in the manifest-update job; same failure mode as #1729 in a different file (#1869)
* ci(sonar): 'dotnet build' failure is masked by the trailing 'dotnet-sonarscanner end' step exit code; Sonar workflow reports green on broken builds (#1771)
* ci(sonar): 'dotnet tool install dotnet-sonarscanner' has no version pin, compromising build reproducibility (#1941)
* ci(sonar): sonar.yml - SonarCloud scanner cache key omits SONAR_SCANNER_VERSION, so bumping the version constant has no effect after the first cache hit (#2202)
* ci(security): vulnerable-package detection uses fragile English string match on dotnet list output; non-en locale or SDK rewording silently passes (#1913)
* ci(sbom): sbom.yml - adds entire $env:USERPROFILE to PATH and skips integrity pinning for dotnet-CycloneDX tool (#1728)
* ci(sbom): sbom.yml - 'dotnet tool install --add-source' does not restrict the install to the SHA256-verified local nupkg; nuget.org can still satisfy the request (#1912)
* ci(sbom): sbom.yml - '--ignore-failed-sources' on CycloneDX tool install silently bypasses the SHA-256 pinning when the local source fails (#2217)
* ci(sbom): sbom.yml - SHA256 pinning only covers the CycloneDX top-level .nupkg; transitive dependencies are fetched unverified from NuGet.org (#2226)
* ci(release): release.yml - workflow list ['winget','choco','scoop','bump-version','changelog'] hard-coded twice (trigger + script) (#1647)
* ci(loc): loc.yml - peaceiris/actions-gh-pages@v3 pinned to mutable tag, supply chain risk (same family as #607, #1304, #1309) (#1342)
* ci(loc): loc.yml - GHA-LoC-Badge action pinned by SHA with no version comment, unlike other pinned actions (#2558)
* ci(changelog): git pull --rebase failure is not checked; git push runs regardless and can fail or push stale data (#1729)
* ci(changelog): 'gh api releases' failure (rate limit / 5xx) is unchecked; workflow reports 'No releases found' and exits 0, silently skipping the changelog update (#1868)
* ci(changelog): changelog.yml - Duplicate dead-code empty-releases check; second if block is unreachable (#2206)
* chore(deps): update dependencies
* other fixes: check commits for more details

**Full Changelog**: https://github.com/aelassas/servy/compare/v8.4...v8.5
</details>

### Downloads
* [servy-8.5-arm64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.5/servy-8.5-arm64-installer.exe) - 76.04 MB
* [servy-8.5-arm64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.5/servy-8.5-arm64-portable.7z) - 77.43 MB
* [servy-8.5-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.5/servy-8.5-net48-sbom.xml) - 0.03 MB
* [servy-8.5-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.5/servy-8.5-net48-x64-installer.exe) - 4.29 MB
* [servy-8.5-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.5/servy-8.5-net48-x64-portable.7z) - 2.03 MB
* [servy-8.5-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.5/servy-8.5-sbom.xml) - 0.04 MB
* [servy-8.5-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.5/servy-8.5-x64-installer.exe) - 81.96 MB
* [servy-8.5-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.5/servy-8.5-x64-portable.7z) - 79.84 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v8.5.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v8.5.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v8.4...v8.5

## [Servy 8.4](https://github.com/aelassas/servy/releases/tag/v8.4)

**Date:** 2026-05-11 | **Tag:** [`v8.4`](https://github.com/aelassas/servy/tree/v8.4)

Servy 8.4 introduces enhanced recovery orchestration, improved security, significant performance optimizations, and bug fixes across the entire service ecosystem. The full changelog is available below.

### Full Changelog
<details>
  <summary>Click to expand release notes!</summary>

* feat(core): recovery for when the service process exits cleanly (#1311)
* fix(core): Timing/retry magic numbers scattered across LogTailer, ServiceHelper, DapperExecutor, ProcessHelper, RotatingStreamWriter - consolidate into AppConfig (#818)
* fix(core): ProcessKiller.KillProcessTree - GetParentProcessId called O(N×depth) times during recursion (#826)
* fix(core): Helper.IsRunningInUnitTest - only detects xUnit; NUnit/MSTest assemblies fall through to production code paths (#830)
* fix(core): ServiceManager constructor - missing ArgumentNullException guards (inconsistent with rest of codebase) (#839)
* fix(core): Logger.cs - Log timestamp lacks UTC/local indicator, ambiguous when UseLocalTimeForRotation is enabled (#842)
* fix(core): DefaultRotationSize is duplicated between AppConfig and Logger (DRY) (#845)
* fix(core): DapperExecutor.cs - unreachable 'return default' after for-loops with const retry counts (#850)
* fix(core): NativeMethods.cs - duplicate Win32 status struct (ServiceStatus and SERVICE_STATUS) - name also collides with the public ServiceStatus enum (#865)
* fix(core): ServiceManager.cs - Win32 access-right and service-type constants are re-declared shadowing the same names in NativeMethods.cs (#867)
* fix(core): IServiceManager - async lifecycle methods (Start/Stop/Restart/Install) lack CancellationToken while Uninstall and read methods accept one (#871)
* fix(core): Helper.EscapeArgs and ProcessHelper.EscapeProcessArgument implement the same Win32 algorithm in two different files (#872)
* fix(core): ProcessHelper.ResolvePath - XML doc and inline comment claim 'SERVICE’s environment' but the call expands the CALLER's environment (#878)
* fix(core): ServiceMapper.ToDomain - RecoveryAction default hardcoded as 'RecoveryAction.RestartService' instead of using 'AppConfig.DefaultRecoveryAction' (#892)
* fix(core): Service.cs (Domain) - RecoveryAction property has no default initializer; falls back to enum 0 (None) instead of AppConfig.DefaultRecoveryAction (RestartService) (#893)
* fix(core): Two parallel ServiceDto validators with different rule sets - XML/JSON imports skip upper-bound checks that CLI install enforces (#898)
* fix(core): EnvironmentVariablesValidator and EnvironmentVariableParser - duplicated 'SplitByUnescapedDelimiters' and 'IndexOfUnescapedChar' implementations (DRY) (#901)
* fix(core): ServiceDependenciesValidator - XML doc, inline comment, and error message all say 'letters/digits/hyphens/underscores' but the regex also allows '.' (#902)
* fix(core): Servy.Core Helper.GetBuiltWithFramework - naive 'net' prefix strip mangles 'netstandard*' TFMs into '.NET standard*' (#917)
* fix(core): Helper.EnsureEventSourceExists & Servy.Restarter Program.cs - Logger.Error duplicates ex.Message in formatted text and again in the exception parameter (#918)
* fix(core): AppFoldersHelper.EnsureFolders - hand-rolled connection-string parser fails on quoted paths and paths containing semicolons (#922)
* fix(core): Logger.Initialize - default parameter value '10' for logRotationSizeMB hardcoded instead of DefaultLogRotationSizeMB constant (#923)
* fix(core): ServiceDtoHelper.ApplyDefaults silently clobbers RunAsLocalSystem/UserAccount/Password - XML/JSON imports always force LocalSystem (#930)
* fix(core): ProcessHelper.MaintainCache - races with GetLockForPid users; cleanup can hand out a NEW lock object for the same PID, defeating per-PID serialization (#934)
* fix(core): ServiceHelper.GetRunningServices - naive ImagePath parser splits on first space when no quotes, mangles legacy unquoted paths under 'Program Files' (#937)
* fix(core): AppConfig - DefaultStopTimeout (5s) and DefaultServiceStopTimeoutSeconds (60s) are two parallel 'stop timeout' defaults with no documented relationship; produces 12x asymmetry between Manager and Service (#942)
* fix(core): ServiceValidationRules.Validate - service-name / display-name / description length checks emit Warnings (non-blocking) for hard SCM limits, allowing invalid configs to pass validation (#943)
* fix(core): ServiceValidationRules.Validate - calls Helper.CreateParentDirectory side-effect for stdout/stderr paths during what should be a read-only validation (#944)
* fix(core): AppConfig - MaxConfigFileSizeMB (10MB) and MaxImportPayloadSizeChars (~2MB) reject the same import inconsistently (#960)
* fix(core): ServiceManager.InstallServiceAsync - EnablePreShutdown only refreshes timeout on initial create, not on existing-service update (#962)
* fix(core): ServiceManager.InstallServiceAsync - gMSA detection via EndsWith("$") misclassifies regular accounts whose name happens to end in $ (#966)
* fix(core): EventLogService.SearchAsync - sourceName constructor parameter is silently overridden by hardcoded AppConfig.EventSource filter at result-time (#969)
* fix(core): EventLogLogger.CreateScoped - every scoped logger allocates a fresh EventLog handle (resource leak across scopes) (#973)
* fix(core): ServiceManager.UninstallServiceAsync - ChangeServiceConfig called on a handle opened without SERVICE_CHANGE_CONFIG (silent ERROR_ACCESS_DENIED) (#985)
* fix(core): ServiceManager.GetAllServices - trackedTasks ConcurrentBag is declared and joined but never populated (dead safety gate) (#986)
* fix(core): ProcessKiller.KillProcessTreeAndParents(string) - root process name is not checked against CriticalSystemProcesses safelist (#990)
* fix(core): ProtectedKeyProvider.GetMachineEntropy - uses Registry.LocalMachine which silently falls back to MachineName entropy when run as 32-bit (WoW64 redirection) (#993)
* fix(core): HandleHelper.GetProcessesUsingFile - synchronous StandardOutput.ReadToEnd defeats HandleExeTimeoutMs (handle.exe hang would block forever) (#996)
* fix(core): ResourceHelper.TerminateBlockingProcesses - extension and targetFileName parameters are unused (kept for 'signature compatibility') (#999)
* fix(core): ServiceExporter.ExportJson and JsonServiceSerializer.Serialize use different JsonSerializerSettings - asymmetric JSON output (#1000)
* fix(core): SecureData.Dispose - _disposed flag set after ZeroMemory; concurrent Dispose calls can race through the guard (#1004)
* fix(core): ProcessHelper.GetProcessTreeMetrics - comment claims sum can exceed 100% but the per-process formula is normalized to whole-machine capacity (#1005)
* fix(core): NativeMethods.AtomicSecureMove - name promises atomicity but MoveFileEx falls back to copy+delete across volumes (#1014)
* fix(core): NativeMethods.GetFileIdentity - two empty catch blocks with stale 'Fallback or log failure here if necessary' TODO comments (#1015)
* fix(core): NativeMethods.ValidateCredentials - silently passes for non-gMSA accounts when password is null/empty (function name promises validation) (#1016)
* fix(core): EnvironmentVariableParser.Parse - surrounding quotes are unconditionally stripped, no way to set an env var whose literal value starts and ends with double quotes (#1074)
* fix(core): AppConfig - TFM 'net10.0-windows' hardcoded into three path constants (silently stale on TFM upgrade) (#1027)
* fix(core): AppConfig - three near-identical Get*ServicePath / GetHandleExePath methods (DRY) (#1028)
* fix(core): EventIds.ScriptInfo (1100) and EventIds.ScriptWarning (2100) constants are defined but never referenced anywhere in the codebase (#1039)
* fix(core): ServiceManager.UninstallServiceAsync - bypasses _win32ErrorProvider with direct Marshal.GetLastWin32Error() in 2 places (test-seam violation) (#1041)
* fix(core): ServiceManager - MapStartupType returns Manual for ServiceStartMode.Boot/System but GetServiceStartupType returns null (silent data drift in batch list) (#1042)
* fix(core): ProcessKiller - Process.GetCurrentProcess() handle leaked in 2 places (lines 216, 286), inconsistent with line 78 (#1045)
* fix(core): ProcessHelper.ResolvePath - Regex.Match called inline (uncompiled) on every path validation (#1046)
* fix(core): ResourceHelper - ResourceStalenessThresholdMinutes (20 min) hardcoded as private const, should live in AppConfig (#1047)
* fix(core): Servy.Core ServiceMapper.ToDto is dead code in src/ (only tests call it) (#1049)
* fix(core): Logger.cs hardcodes 'logs' subdirectory in 3 places (lines 91, 120, 349) (#1051)
* fix(core): ProcessKiller.KillProcessTreeAndParents - calls Toolhelp32 snapshot twice per invocation (BuildProcessSnapshotNative + BuildParentChildMapNative) (#1059)
* fix(core): Helper.WriteFileAtomic and Helper.WriteFileAtomicAsync are ~95% duplicated (DRY) (#1060)
* fix(core): RotatingStreamWriter - Thread.Sleep called while holding _lock blocks all writers for up to 100ms during rotation retries (#1066)
* fix(core): RotatingStreamWriter._rotationDisabled is one-way: a single non-IO exception silently disables rotation forever, file grows unbounded (#1067)
* fix(core): RotatingStreamWriter.EnforceMaxRotations regex misses double-collision filenames produced by GenerateUniqueFileName, those rotated logs accumulate forever (#1068)
* fix(core): ProtectedKeyProvider.GetKey/GetIV - no in-memory caching, full DPAPI roundtrip + 3-retry file read on every call #1069
* fix(core): ProtectedKeyProvider - [ExcludeFromCodeCoverage] on entire security class hides DPAPI/ACL regressions from coverage tooling (#1070)
* fix(core): ProtectedKeyProvider - three bare catch blocks (lines 256, 274, 310) swallow exceptions without binding the variable (#1071)
* fix(core): SecureData.Decrypt - tampered/truncated v2 payloads silently return the original ciphertext as 'plaintext', callers can't distinguish success from integrity failure (#1072)
* fix(core): SecureData.Decrypt - v1 marker path always accepts unauthenticated ciphertext, enabling silent v2->v1 downgrade by any writer of stored credentials (#1073)
* fix(core): HandleHelper.GetProcessesUsingFile - StringBuilder accessed unsynchronized after Kill on timeout, race with still-firing OutputDataReceived/ErrorDataReceived handlers (#1075)
* fix(core): ServiceMapper.ToDto omits Pid, ActiveStdoutPath, ActiveStderrPath; Domain->DTO->Domain round-trip silently drops runtime state (#1076)
* fix(core): Logger.Log - exception logged via $"{message}\nException: {ex}" then newline-sanitized to literal \r\n, stack traces become unreadable single-line escaped blobs (#1077)
* fix(core): ServiceValidationRules - service name only checks for '/' and '\', misses other SCM-rejected inputs (leading/trailing whitespace, control chars, '"', '|', etc.) (#1078)
* fix(core): ResourceHelper.GetEmbeddedResourceLastWriteTimeUTC name and XML doc claim 'embedded resource' time, actually returns the exe's own File.GetLastWriteTimeUtc (#1079)
* fix(core): LogonAsServiceGrant.cs - [ExcludeFromCodeCoverage] on entire LSA-privilege class hides regressions in security-sensitive code (#1092)
* fix(core): ServiceManager.cs - three SCM-tuning consts hardcoded (ServiceStartTimeoutSeconds, ScmPollIntervalMs, MaxParallelScmQueries) and inconsistent 250ms poll in Start/Stop (#1095)
* fix(core): ServiceExporter.cs - private nested Utf8StringWriter duplicates the public Servy.Core.IO.Utf8StringWriter class (#1104)
* fix(core): EscapedTokenizer - three methods duplicate the 'count preceding backslashes' loop (DRY) (#1105)
* fix(core): EnvironmentVariablesValidator.Validate - XML doc says 'exactly one unescaped equals' but code only requires >=1, and CountUnescapedChar pass is redundant before IndexOfUnescapedChar (#1113)
* fix(core): EscapedTokenizer.Unescape - escaped newline/semicolon split asymmetry: split treats \\r and \\n as escaped delimiters but Unescape never strips the backslash, leaving a stray '\' in the value (#1114)
* fix(core): OperationResult.Failure - XML doc claims ArgumentException is thrown only on null, but code throws on whitespace too (#1115)
* fix(core): RotatingStreamWriter.ShouldRotateByDate Weekly mode misses year-on-year rotation when both samples land in 'week 1' under ISO calendar year mismatch (#1116)
* fix(core): Logger.Log error fallback writes to logs/LoggerWriteErrors.log without first ensuring the directory exists, unlike InternalInitialize which calls SecurityHelper.CreateSecureDirectory (#1117)
* fix(core): EventLogLogger.SetIsEventLogEnabled - when InitializeEventLog fails, _isEventLogEnabled is overwritten back to the requested 'true' state, leaving _eventLog null but flag pretending logging is on (#1118)
* fix(core): EventLogReader.ReadEvents - cutoff EventRecord at maxReadCount yields break before using-block, leaking the just-fetched native handle (#1119)
* fix(core): ServiceControllerWrapper.BuildDependencyTree - currentPath retains stale entries when an exception is thrown after Add but before RemoveAt, corrupting cycle detection for siblings (#1120)
* fix(core): JsonServiceSerializer.Deserialize logs first 100 chars of malformed JSON to error log on JsonException - can leak Password if it appears early in the payload (#1121)
* fix(core): XmlServiceSerializer.Deserialize/Serialize allocate XmlSerializer per call instead of caching it statically (parallel impl of #1121 + perf hit) (#1122)
* fix(core): ServiceManager.GetAllServices - LogOnAs initialized to magic string "LocalSystem" instead of LocalSystemAccount const defined in same file (#1131)
* fix(core): ServiceManager.UninstallServiceAsync - stop wait loop uses DefaultServiceStopTimeoutSeconds instead of per-service StopTimeout (asymmetric with StopServiceAsync) (#1132)
* fix(core): ServiceManager.GetDependencies - null return is overloaded (means both "service not installed" and "unexpected error") (#1133)
* fix(core): SecurityHelper.CreateSecureDirectory - silently swallows UnauthorizedAccessException without logging, leaves no audit trail when ACL hardening fails (#1137)
* fix(core): XmlServiceValidator/JsonServiceValidator - both treat ValidationResult.Warnings as blocking failures, defeating the purpose of separate Errors/Warnings collections (#1139)
* fix(core): WindowsServiceApi - [ExcludeFromCodeCoverage] on the entire class hides regressions in the non-trivial GetServices() loop (parallel to #1070 and #1092) (#1138)
* fix(core): EventIds.cs declares only base ranges (1000/2000/3000) but specific IDs (3001/3002/3003/3103/3104) are hardcoded magic numbers in ProtectedKeyProvider.cs and the taskschd scripts (#1144)
* fix(core): Servy.Core DTOs/ServiceInfo.cs constructor - LogOnAs hardcoded to 'LocalSystem' magic string instead of ServiceAccounts.LocalSystem const (parallel to #1131) (#1160)
* fix(core): Servy.Core Native/Handle.cs - process-handle wrapper class is named 'Handle' while the three siblings (SafeScmHandle / SafeServiceHandle / SafeJobObjectHandle) all follow the SafeXxxHandle BCL convention (#1161)
* fix(core): EventLogReader.MapToDto - 'evt.TimeCreated ?? DateTime.MinValue' converts implicitly to DateTimeOffset and throws ArgumentOutOfRangeException in non-UTC time zones when TimeCreated is null (#1167)
* fix(core): ProcessHelper.cs - [ExcludeFromCodeCoverage] on entire class hides regressions in non-trivial logic (BFS process-tree walker, CPU-delta cache pruning, RAM formatter, ResolvePath regex flow); parallel to #1070 / #1092 / #1138 (#1169)
* fix(core): EventLogService.cs - MaxResults (10000) and LogName ('Application') hardcoded as private consts; LogName is also duplicated in EventLogLogger and Helper.EnsureEventSourceExists (#1170)
* fix(core): ResourceHelper.CopyEmbeddedResource - outer catch misattributes restart failures (StartServices in finally) as 'Failed to copy embedded resource' when the copy itself succeeded (#1171)
* fix(core): ServiceManager.cs - Nullable IServiceRepository? parameter contradicts ArgumentNullException guard (#1176)
* fix(core): ServiceManager.GetAllServices - ServiceController handles leak on cancellation (#1183)
* fix(core): Servy.Core.Helpers.ServiceHelper constructor silently accepts null repository, NREs at runtime (#1187)
* fix(core): Multiple files - Hardcoded 1024*1024 bytes-per-MB literal bypasses AppConfig.BytesInMegabyte / AppConfig.ToBytes (#1188)
* fix(core): AppConfig.cs - MaxMaxFailedChecks / MaxMaxRestartAttempts / MaxPreLaunchRetryAttempts set to int.MaxValue, inconsistent with other Max* constants and creates overflow risk in counters (#1193)
* fix(core): EventLogLogger.cs - SafeWriteToEventLog reads _eventLog without volatile/lock; SetIsEventLogEnabled can null it concurrently (#1205)
* fix(core): EventLogLogger.cs - EventLogMessageMaxChars (31000) hardcoded; should live in AppConfig with the other Event-Log constants (#1206)
* fix(core): Logging-&-Log-Rotation.md - Event ID 3002 is documented in the 'Error' range table but the entry itself describes a Warning, contradicting the actual EventLogEntryType.Warning emitted by the code (#1210)
* fix(core): EnvironmentVariableHelper.cs - ProtectedVariables list omits DOTNET_ROOT, DOTNET_HOST_PATH, DOTNET_BUNDLE_EXTRACT_BASE_DIR - .NET runtime hijacking via env var override (#1212)
* fix(core): ProtectedKeyProvider.cs - GetCachedOrGenerate fast-path reads cacheField without synchronization; race with InvalidateCache can NRE on Clone() (#1213)
* fix(core): SecureData.cs - IDisposable class holding raw AES/HMAC keys has no finalizer; forgotten Dispose leaves key material unwiped until GC (#1214)
* fix(core): JsonServiceValidator.cs - Copy-paste bug: warning log says 'XML Import succeeded with warnings' inside the JSON validator (#1215)
* fix(core): Logger.cs / RotatingStreamWriter.cs - Date format strings use thread-default culture; produce non-Gregorian years on Thai locale (Buddhist calendar) (#1216)
* fix(core): ProtectedKeyProvider.cs - IDisposable holding decrypted AES key/IV has no finalizer; forgotten Dispose leaves plaintext key material in managed heap until GC (#1220)
* fix(core): ProcessKiller.cs - WaitForExit timeout floor only applied to KillParentWaitMs (Math.Max 1000); KillChildWaitMs/KillTreeWaitMs honor smaller values without floor (#1222)
* fix(core): ProcessKiller.cs - KillProcessTreeAndParents(int) lacks the top-level try/catch present on the string overload, leaks exceptions to callers (#1223)
* fix(core): EventLogService.cs - EventLogMaxResults caps raw events before filtering; '[' bracket heuristic and provider-name substring filter silently drop matches, so user sees fewer hits than configured (#1228)
* fix(core): ProcessKiller.cs - KillParentProcesses recursive call passes DateTime.MinValue, disabling PID-recycling identity check for grandparents and beyond (#1231)
* fix(core): Logger.cs - _currentLogLevel read on hot logging path without volatile or lock; updates may go unobserved (#1236)
* fix(core): ServiceHelper.cs (Core) - StopServices ignores PreStopTimeoutSeconds, stop wait too short when pre-stop hook configured (#1241)
* fix(core): ServiceHelper.cs (Core) - StartServices ignores PreLaunchTimeoutSeconds, start wait too short when pre-launch hook configured (#1242)
* fix(core): ServiceHelper.cs - Hardcoded 'defaultTimeoutInSeconds = 30' should reference AppConfig.DefaultServiceStartTimeoutSeconds (#1243)
* fix(core): ServiceManager.cs - UninstallServiceAsync GetByNameAsync call drops cancellationToken (#1244)
* fix(core): ProcessKiller.cs - WalkAndKillChildren recursive walk has no cycle/visited-set protection (DFS)  (#1247)
* fix(core): ResourceHelper.cs - GetHostProcessLastWriteTimeUTC fallback to UtcNow forces unconditional re-extraction every startup (#1248)
* fix(core): ResourceHelper.cs - Constructor instantiates concrete ServiceHelper instead of accepting an abstraction; tightly couples and blocks unit testing (#1249)
* fix(core): ProcessHelper.cs - ValidatePath swallows InvalidOperationException, losing diagnostic context for unexpanded environment variables (#1250)
* fix(core): ResourceHelper.cs - 'targetFileName' out parameter on ShouldCopyResource is set but never consumed by either caller (#1256)
* fix(core): ResourceHelper.cs - CopyEmbeddedResource (async) and CopyEmbeddedResourceSync are asymmetric: async stops/restarts services, sync silently doesn't (#1257)
* fix(core): Helper.cs - WriteFileAtomic (sync) calls async core via .GetAwaiter().GetResult(); deadlock risk on captured SynchronizationContext (#1259)
* fix(core): Logger.cs - FormatException recurses on InnerException with no depth limit; pathological chains can blow the stack (#1260)
* fix(core): AppConfig.cs - DefaultDesktopAppPublishPath / DefaultManagerAppPublishPath use './X.exe' which is CWD-dependent, not BaseDirectory-anchored (#1266)
* fix(core): AppConfig.cs - Five-level '..\..\..\..\..' relative paths to sibling project bin folders are fragile and break under publish/single-file/custom output (#1268)
* fix(core): ServiceExporter.cs - ExportXml string vs file overloads diverge in null handling and UTF-8 BOM behavior (#1269)
* fix(core): Helper.cs - IsServiceNameValid does not enforce the Windows SCM 256-character maximum service name length (#1279)
* fix(core): ProcessHelper.cs - MaintainCache catches only ArgumentException; Win32Exception/InvalidOperationException from Process.HasExited terminates the prune loop early (#1281)
* fix(core): XmlServiceValidator.cs / JsonServiceValidator.cs - Redundant ValidatePath call after _serviceValidationRules.Validate (#1285)
* fix(core): NativeMethods.cs - ValidateCredentials .\AccountName forms throw SecurityException because Translate runs before built-in bypass (#1286)
* fix(core): NativeMethods.cs - FILE_IDENTITY.IsDifferentFrom does asymmetric comparison when handle-info presence differs between the two probes (#1287)
* fix(core): ServiceDto.cs - Clone() omits EnableConsoleUI; cloned DTOs silently lose console-UI flag (#1290)
* fix(core): ServiceDtoHelper.cs - ApplyDefaults skips EnableConsoleUI while filling all other nullable bool defaults (#1292)
* fix(core): ServiceControllerProvider.cs - Constructor accepts factory without null guard, breaks sibling-component convention (#1293)
* fix(core): ServiceDependenciesValidator.cs - Error message and class comment omit 'periods', contradicting the regex that allows them (#1294)
* fix(core): EventLogReader.cs - MapToDto's FormatDescription() call has no try/catch; one event with a missing provider aborts the entire enumeration (#1295)
* fix(core): EnvironmentVariableParser.cs - Structural-quote strip eats trailing escaped \\"\, contradicting the documented 'escaped quotes survive' contract (#1296)
* fix(core): ProcessHelper.cs - GetProcessTreeMetrics does not cap CPU at 100% despite ProcessMetrics XML doc promising 'capped at 100.0' for trees (#1297)
* fix(core): Logger.cs - _logRotationSizeMB field is long but every public setter takes int; the extra width is dead (#1308)
* fix(core): RotatingStreamWriter.cs - InitializeWriter omits FileShare.Delete; same root cause as #1306, affects log rotation file (#1314)
* fix(core): RotatingStreamWriter.cs - EnforceMaxRotations does not catch IO/Unauthorized exceptions from Directory.GetFiles; one stale lock terminates retention enforcement (#1315)
* fix(core): NativeMethods.cs - Multiple critical P/Invoke declarations missing SetLastError=true; callers cannot diagnose failures (#1316)
* fix(core): Helper.cs - WriteFileAtomicCore AV-retry catch on Win32Exception is dead code; File.Move throws IOException/UnauthorizedAccessException, never Win32Exception (#1317)
* fix(core): ProcessKiller.cs - Failed-kill diagnostics emitted at Debug level; production logs hide why processes survived termination requests (#1318)
* fix(core): ServiceValidationRules.cs - Service name length check is dead code; Helper.IsServiceNameValid already returns early on length violation (#1319)
* fix(core): ProtectedKeyProvider.cs - GetOrGenerate retry loop catches only IOException; UnauthorizedAccessException (AV lock) bypasses backoff (#1323)
* fix(core): ServiceHelper.cs (Core) - Misleading 'Fail-safe' comment contradicts the throw on the next line (#1328)
* fix(core): ServiceManager.cs - MapStartupType returns ServiceStartType.Manual on query failure but ServiceStartType.Unknown on unmapped enum values (#1336)
* fix(core): ConfigParser.cs - ParseInt uses culture-dependent TryParse; centralized parser should be invariant (#1341)
* fix(core): StringHelper.cs - NormalizeString joins on raw ';' without escaping pre-existing semicolons in env-var values, breaking PATH-style values (#1347)
* fix(core): AppConfig.cs / XmlServiceValidator.cs / JsonServiceValidator.cs - 'MaxConfigFileSizeMB' uses decimal MB (1,000,000) for char check but 1024*1024 binary MB for file size check (#1353)
* fix(core): Helper.cs - 'ReservedNames' is a mutable public static field; any caller can clear or mutate it (#1355)
* fix(core): Helper.cs - WriteFileAtomic uses deterministic temp file name 'path + .tmp', concurrent writers to the same path collide (#1356)
* fix(core): RotatingStreamWriter.cs - Race window: writer can re-attach to soon-to-be-moved file because PerformPhysicalRotation runs outside the lock (#1357)
* fix(core): ProtectedKeyProvider.cs - GetCachedOrGenerate fast-path snapshot is NOT immune to InvalidateCache's ZeroMemory; comment claims immunity it does not deliver (#1358)
* fix(core): ProtectedKeyProvider.cs - Library code mutates Environment.ExitCode = 13; cross-cutting global side-effect inside a key provider (#1359)
* fix(core): NativeMethods.cs - ValidateCredentials accepts non-service identities ('Everyone', 'Authenticated Users', 'Anonymous Logon') as built-in passwordless accounts (#1363)
* fix(core): NativeMethods.cs - ValidateCredentials regex check '!isBuiltIn' is dead; built-in branch already returned earlier (#1364)
* fix(core): NativeMethods.cs - AtomicSecureMove lacks null/empty validation for source and destination, breaking sibling-API guard convention (#1365)
* fix(core): rocessKiller.cs - BuildSnapshotAndChildMapNative compares against 'new IntPtr(-1)' instead of the central INVALID_HANDLE_VALUE constant (#1366)
* fix(core): ProcessHelper.cs - ResolvePath UnexpandedEnvVarRegex false-positives on legitimate filesystem paths containing literal '%' (#1367)
* fix(core): ProcessHelper.cs - FormatCpuUsage near-zero shortcut returns '0' but non-zero values use '0.0' format, breaking visual alignment (#1369)
* fix(core): AppConfig.cs - TargetFramework metadata fallback hardcoded to 'net10.0-windows' silently drives DEBUG path resolution to a wrong folder (#1370)
* fix(core): SecureData.cs - Decrypt silently returns the encrypted base64 payload as 'plaintext' when AllowLegacyV1Decryption is false (#1371)
* fix(core): ResourceHelper.cs - GetHostProcessLastWriteTimeUTC XML doc claims fallback is 'DateTime.UtcNow' but implementation returns DateTime.MinValue, flipping the staleness decision (#1372)
* fix(core): Logger.cs - LogLevel fallback uses ToUpper() instead of ToUpperInvariant(), breaks under Turkish locale (#1373)
* fix(core): RotatingStreamWriter.cs - Hardcoded rotation timing constants (RotationCooldownMs / CriticalFailureCooldownMs / SyncRotationRetry) bypass AppConfig pattern (#1376)
* fix(core): RotatingStreamWriter.cs - Timestamp format 'yyyyMMdd_HHmmss' duplicated as literal length 16 magic check in GenerateUniqueFileName (#1377)
* fix(core): RotatingStreamWriter.cs - 'Disk space growth is no longer bounded' error log omits FullName, hampering operator triage (#1378)
* fix(core): Logger.cs - Hardcoded MaxInnerExceptionDepth (16) and MaxFormattedExceptionLength (16384) bypass AppConfig central-config pattern (#1379)
* fix(core): SecureData.cs - 'Unsupported encryption version marker' exception echoes full ciphertext into the exception message and logs (#1382)
* fix(core): AppFoldersHelper.cs - 'isChildOfRoot' check uses raw StartsWith on unnormalized paths; '..' segments can fool inheritance-preservation logic (#1386)
* fix(core): ServiceHelper.cs - StartServices/StopServices asymmetric handling of TimeoutException, no CancellationToken on either (#1388)
* fix(core): Logger.cs - LoggerInitializationErrors.log and LoggerWriteErrors.log fallback files grow unbounded (no rotation) (#1389)
* fix(core): Logger.cs - Info(message) is the only level missing the Exception overload (Debug/Warn/Error all accept ex) (#1392)
* fix(core): ProtectedKeyProvider.cs - SaveProtected uses deterministic temp path 'path + .tmp' (same family as #1356) (#1397)
* fix(core): ServiceControllerWrapper.cs - GetDependencies returns empty subtree on second visit (diamond/shared dependency in tree) (#1400)
* fix(core): ServiceValidationRules.cs - wrapperExePath uses File.Exists while every other path field uses _processHelper.ValidatePath (asymmetric env-var expansion) (#1401)
* fix(core): ProcessKiller.cs - KillParentProcesses uses raw '1000' floor instead of SafeWait(AppConfig.MinKillWaitMs); deviates from sibling kill paths (#1403)
* fix(core): ProcessKiller.cs - KillProcessesUsingFile returns true after Logger.Error when file is missing (success contradicts error log) (#1404)
* fix(core): SecureData.cs - DecryptV2 wraps CryptographicException as SecureDataIntegrityException but drops the inner exception (#1405)
* fix(core): Helper.cs - WriteFileAtomicCore (async) uses non-unique '.tmp' suffix while sync sibling uses GUID; concurrent async writes to same path collide (#1406)
* fix(core): AppConfig.cs - UpdateCheckTimeoutSeconds <= UpdateCheckHttpTimeoutSeconds invariant only enforced via XML comment, no compile-time/static check (#1415)
* fix(core): EventLogService.cs - Stale 'using block / ObjectDisposedException' comment; loop iterates DTOs, not EventRecord (#1416)
* fix(core): EventLogService.cs - Hardcoded 5x cushion multiplier on AppConfig.EventLogMaxResults read should be a named constant (#1417)
* fix(core): LoggerConfigurator.cs - ConfigureFromAppSettings recreates the underlying writer up to 3x on every config load (#1418)
* fix(core): EventLogLogger.cs - Scoped logger SetLogLevel/SetIsEventLogEnabled mutate the parent's global state (#1419)
* fix(core): IServyLogger.cs - Info/Warn lack optional Exception parameter while Debug/Error accept one (asymmetric API) (#1420)
* fix(core): AppFoldersHelper.cs - Path comparison in EnsureFolders is not normalized; aesKeyFolder == aesIVFolder duplicates SecureDirectory call (#1428)
* fix(core): ResourceHelper.cs - Comment 'fallback to UtcNow' contradicts actual return DateTime.MinValue (#1429)
* fix(core): SERVY_PASSWORD environment variable name hardcoded across CLI / PowerShell module / help text (#1432)
* fix(core): EnvironmentVariableParser.Parse - Documented quote-escape example KEY="\"value\"" produces ""value" (extra leading quote, missing trailing) (#1442)
* fix(core): EventLogLogger.cs - ScopedEventLogLogger Info/Warn/Error level filter is overridden by parent when event log is enabled (#1443)
* fix(core): ProcessKiller.cs - BuildSnapshotAndChildMapNative silently returns empty maps when CreateToolhelp32Snapshot fails; callers cannot tell (#1450)
* fix(core): NativeMethods.FILE_IDENTITY.IsDifferentFrom returns false (same) when both probes are undeterminable, masking rotations on hostile file systems (#1456)
* fix(core): ServiceHelper.GetRunningServices - substring fallback can falsely match unrelated services (#1461)
* fix(core): RotatingStreamWriter.PrepareRotation - _lastRotationDate set BEFORE physical move; failed date-based rotations skipped until next interval (#1463)
* fix(core): RotatingStreamWriter.PerformPhysicalRotation - UnauthorizedAccessException trips circuit breaker permanently instead of retrying like IOException (#1469)
* fix(core): ProcessKiller.KillParentProcesses - missing cycle/visited guard can cause StackOverflowException on PID-reuse cycles (#1470)
* fix(core): ProcessHelper.GetProcessTree - silently returns rootPid only when CreateToolhelp32Snapshot fails (same family as #1450) (#1471)
* fix(core): ServiceManager.StartServiceAsync / StopServiceAsync swallow OperationCanceledException; inconsistent with UninstallServiceAsync which re-throws (#1475)
* fix(core): NativeMethods.GetFileIdentity - file position not restored on Read/Seek exception, leaves caller stream at offset 0 (#1479)
* fix(core): ServiceHelper.StartServices/StopServices - sc.WaitForStatus inside Task.Run is non-cancellable mid-wait; cancellation only honored between services (#1480)
* fix(core): Helper.WriteFileAtomicAsync - FlushAsync called twice (caller's lambda flushes, then WriteFileAtomicCore flushes again) (#1507)
* fix(core): ServiceControllerWrapper.cs - [ExcludeFromCodeCoverage] hides BuildDependencyTree's recursion logic from coverage (#1520)
* fix(core): Logger.cs - [ExcludeFromCodeCoverage] hides rotation, formatting, sanitization, and exception-truncation logic from coverage (#1529)
* fix(core): EventIds.cs values duplicated as hardcoded magic numbers in PowerShell scripts (3103, 3104) (#1535)
* fix(core): Logger.Initialize - inconsistent parameter name for LogLevel ('initialLevel' vs 'logLevel') across the two overloads (#1538)
* fix(core): EventLogLogger.cs - [ExcludeFromCodeCoverage] hides level filtering, message truncation and ScopedEventLogLogger logic from coverage (same family as #1529, #1520) (#1548)
* fix(core): ServiceManager.UninstallServiceAsync - stop wait loop swallows timeout, leading to a misleading 'Failed to uninstall' error if the service didn't actually stop (#1560)
* fix(core): ResourceHelper.GetHostProcessLastWriteTimeUTC - AppDomain fallback uses FriendlyName which often lacks the .exe suffix on .NET 5+, so File.Exists silently returns false (#1564)
* fix(infra): ServiceRepository.UpsertBatchAsync - IEnumerable parameter enumerated 3+ times (.Any(), .Select(...).ToList(), .ToList()) (#821)
* fix(infra): ServiceRepository.ExportJsonAsync - bypasses injected IJsonServiceSerializer, asymmetric with ImportJsonAsync (#822)
* fix(infra): ServiceRepository.GetServicePidAsync / GetServiceConsoleStateAsync - Name parameter not trimmed (inconsistent with GetByName / DeleteAsync / etc.) (#880)
* fix(infra): SQLiteDbInitializer.GetSqlType - 13 columns declared NOT NULL in the schema but defined as nullable (int?/bool?) on ServiceDto (#897)
* fix(infra): ServiceRepository.ExportXmlAsync - bypasses injected IXmlServiceSerializer, asymmetric with ImportXmlAsync (parallel to #822) (#968)
* fix(infra): DapperExecutor - ThreadLocal<Random> should be Random.Shared on .NET 6+ (correlated jitter, unnecessary allocation) (#994)
* fix(infra): ServiceRepository.ExportXmlAsync - XML preamble declares 'utf-16' but the file is written as UTF-8 (encoding mismatch in exports) (#995)
* fix(infra): ServiceRepository.ImportXml/JsonAsync - UPSERT clobbers Pid / ActiveStdoutPath / ActiveStderrPath of a running service (Manager loses tracking) (#997)
* fix(infra): DapperExecutor - sync ExecuteWithRetry inlines backoff calculation while async path uses CalculateBackoff helper (DRY) (#1085)
* fix(infra): ServiceRepository - private static XmlSerializer ServiceDtoSerializer field declared but never used (dead code) (#1123)
* fix(infra): DatabaseInitializer.InitializeDatabase - XML doc is missing the 'initializer' parameter and the matching ArgumentNullException case for it (#1124)
* fix(infra): SQLiteDbInitializer - only the unversioned-legacy path uses GetExpectedColumns to ALTER missing columns; once a DB reaches Version 1, future SqlConstants additions need a manual ApplyVersionN migration or the DB silently lacks the column (#1143)
* fix(infra): SQLiteDbInitializer.cs - ApplyVersion4 leaves PRAGMA foreign_keys=OFF on the pooled connection if migration throws (#1194)
* fix(infra): ServiceRepository.cs - PatchRuntimeStateAsync / PatchRuntimeState are duplicated and the encrypted/decrypted field list is repeated in two methods (#1204)
* fix(infra): SQLiteDbInitializer.cs - ReconcileSchema runs ALTER TABLE statements without a transaction; partial schema state survives if one ALTER fails mid-loop (#1232)
* fix(infra): SQLiteDbInitializer.cs - GetSqlType silently maps unknown column names to TEXT; a future INTEGER added to SqlConstants but not to nullableInts is created with the wrong affinity (#1233)
* fix(infra): ServiceRepository.cs - DecryptDto null-check is inconsistent between sync and async overloads (#1251)
* fix(infra): DapperExecutor.cs - CalculateBackoff exponential backoff has silent int-overflow path and no upper cap (#1307)
* fix(infra): SQLiteDbInitializer.cs - ApplyVersion1 lacks a transaction; partial failure leaves DB unrecoverable (#1327)
* fix(infra): DapperExecutor.cs - Async retry path silently rethrows on exhaustion while sync path logs Warn (asymmetric retry diagnostics) (#1380)
* fix(infra): DapperExecutor.cs - XML doc claims 'Uses SpinWait' but ExecuteWithRetry actually uses Thread.Sleep (#1381)
* fix(infra): ServiceRepository.UpsertBatchAsync - idMap.TryGetValue(service.Name, ...) throws ArgumentNullException when DTO.Name is null (#1434)
* fix(infra): ServiceRepository.Update - sync-over-async (.GetAwaiter().GetResult()) on PatchRuntimeStateAsync risks UI deadlock (#1435)
* fix(infra): DapperExecutor.cs - Doc-comment typo 'Uses Uses Thread.Sleep' (duplicate word) (#1440)
* fix(infra): ServiceRepository.cs - ImportXmlAsync/ImportJsonAsync swallow OperationCanceledException, returning false instead of propagating cancellation (#1451)
* fix(infra): ServiceRepository.cs - 'updateRuntimeState' parameter name is inverted: false means update, true means skip (#1452)
* fix(infra): DapperExecutor.cs - QueryAsync(string) and QuerySingleOrDefaultAsync(string) overloads missing null check on sql parameter (#1459)
* fix(infra): DapperExecutor.cs - [ExcludeFromCodeCoverage] hides ExecuteWithRetry/ExecuteWithRetryAsync, CalculateBackoff, and FormatSqlForLog logic from coverage (same family as #1529, #1520, #1548) (#1554)
* fix(infra): SQLiteDbInitializer.cs - [ExcludeFromCodeCoverage] hides migration sequencing, table-rebuild (V4), legacy upgrade and ReconcileSchema self-healing logic from coverage (same family as #1529, #1520, #1548, #1554) (#1557)
* fix(service): prevent recovery counter jump by locking gate during terminal actions
* fix(service): ServiceHelper.LogStartupArguments - sensitive env vars are formatted unconditionally even when EnableDebugLogs is false (#824)
* fix(service): ServiceHelper.GetSanitizedArgs - production dead code, only invoked from a single test (#828)
* fix(service): ProcessLauncher.cs - synchronous mode buffers entire stdout/stderr in memory until exit (#846)
* fix(service): Service.cs HandleLogWriters - XML doc comment buried inside method body becomes a no-op (#859)
* fix(service): ServiceHelper.RestartService - log message hardcodes '4 minutes' instead of using RestarterExeMaxWaitMs constant (#928)
* fix(service): ProcessLauncher.Start - appends empty content to StdOutPath/StdErrPath when redirectOutput is false, creating spurious zero-byte log files (#953)
* fix(service): RunFailureProgram bypasses ProcessLauncher.Start centralized utility (#959)
* fix(service): ProcessWrapper.Stop and ProcessWrapper.StopPrivate are ~90% duplicated stop sequences (#961)
* fix(service): ServiceHelper.ValidateStartupOptions - Pre-Stop and Post-Stop paths/working dirs are not validated, while Pre-Launch and Post-Launch are (#967)
* fix(service): EventLogLogger.CreateScoped - every scoped logger allocates a fresh EventLog handle (resource leak across scopes) (#973)
* fix(service): StartOptionsParser.Parse - RecoveryAction default is hardcoded RecoveryAction.None instead of AppConfig.DefaultRecoveryAction (parallel to #893 / #892) (#974)
* fix(service): ProcessLauncher.Start - process wrapper leaked on timeout / log-writer failure (no dispose path when exception thrown) (#989)
* fix(service): OnCustomCommand fallback teardown calls Environment.Exit(1) - overrides distinct exit codes set by ProtectedKeyProvider (e.g. 13) (#1003)
* fix(service): ServiceHelper.InitializeStartup is dead production code - public method not in IServiceHelper interface, only called by unit tests (#1007)
* fix(service): OnOutputDataReceived/OnErrorDataReceived - IsNullOrWhiteSpace silently drops blank/whitespace-only lines from child output (#1040)
* fix(service): ServiceHelper SensitiveKeyWords contains overly-broad 'KEY' entry that masks legitimate values like FOREIGN_KEY/PRIMARY_KEY/SSH_KEY in connection-strings logs (#1055)
* fix(service): ServiceHelper.RestarterExeMaxWaitMs (240000ms) hardcoded as public const, should live in AppConfig (#1053)
* fix(service): ProcessLauncher.Start - TimeoutMs <= 0 guard fires AFTER process started and writers opened (line 154) (#1056)
* fix(service): ServiceHelper.MaskSensitiveValue - KeyMatcherRegex.IsMatch has no RegexMatchTimeoutException catch (inconsistent with MaskRawArguments) (#1081)
* fix(service): ProcessLauncher.Start - child process leaks if writer setup throws after process.Start(): finally only Disposes the wrapper, the started process keeps running orphaned (#1086)
* fix(service): ProcessExtensions.GetAllDescendants - recursive call takes a fresh Toolhelp32 snapshot per node, so a tree of N processes does N system-wide snapshots (#1088)
* fix(service): ProcessExtensions.Format - only catches InvalidOperationException, will throw Win32Exception ('Access denied') on protected processes (#1089)
* fix(service): StartOptionsParser.Parse uses unchecked enum casts (Priority, DateRotationType, RecoveryAction) - bypasses ConfigParser.ParseEnum used by ServiceMapper.ToDomain on the same DTO (#1090)
* fix(service): Service.cs ConditionalResetRestartAttemptsAsync - _fileSemaphore.WaitAsync() called without CancellationToken (inconsistent with read/write helpers) (#1093)
* fix(service): PreShutdownWaitHintMs (30000) and ScmStartupRequestThresholdSeconds (20) hardcoded as private consts, should live in AppConfig (#1094)
* fix(service): ServiceHelper.ValidateStartupOptions - 13 nearly-identical path/working-dir guard blocks (DRY) (#1102)
* fix(service): Servy.Service Hook.Dispose - '_disposed = true' set inside the 'if (disposing)' block, inconsistent with every other Dispose pattern in the codebase (#1106)
* fix(service): Service.cs OnStart - ExitCode 1064 commented as ERROR_SERVICE_SPECIFIC_ERROR but Win32 1064 is actually ERROR_EXCEPTION_IN_SERVICE (line 416 comments same constant correctly) (#1134)
* fix(service): ProcessLauncher.ApplyLanguageFixes - Python overrides silently win over user EnvironmentVariables, but Java's -Dfile.encoding respects user value (asymmetric) (#1141)
* fix(service): Service.cs constructor - AppDbContext created but never Disposed (parallel to #1127) (#1145)
* fix(service): ConsoleAppDetector.CheckPEHeaderForConsole - 'magic' is read from the optional header but never used; PE32/PE32+ magic word is referenced in comments but not validated (#1168)
* fix(service): Service.cs - Misleading comment: 1066 is ERROR_SERVICE_SPECIFIC_ERROR, not ERROR_EXCEPTION_IN_SERVICE (#1174)
* fix(service): missing string interpolation in pre-launch failure log (literal {attempt} in output) (#1175)
* fix(service): ProcessLauncher.cs - StandardOutputEncoding set without RedirectStandardOutput throws InvalidOperationException (#1177)
* fix(service): Service.cs - Missing string interpolation in teardown error log (literal {reason} in output) (#1184)
* fix(service): Helper.cs / Service.cs - Reserved Windows device names (CON/PRN/AUX/NUL/COMn/LPTn) not rejected by IsServiceNameValid or MakeFilenameSafe (#1189)
* fix(service): Service.cs - _cancellationSource disposed without first calling Cancel(), causes ObjectDisposedException for in-flight tasks (#1190)
* fix(service): Service.cs - CheckHealth logs ExitCode read failure at Debug level (silent in production), inconsistent with OnProcessExited which logs at Warn (#1192)
* fix(service): Service.cs - WriteAttemptsInternalAsync uses non-atomic File.WriteAllTextAsync; power loss during write resets restart-attempt counter to 0 (#1195)
* fix(service): ProcessWrapper.cs - SendCtrlC mutates process-wide console state (FreeConsole / AttachConsole / SetConsoleCtrlHandler) without a process-wide lock (#1207)
* fix(service): ProcessLauncher.cs - OutputDataReceived/ErrorDataReceived handlers can rethrow into Process pipe-drain thread, surfacing as AppDomain.UnhandledException on transient log-write failures (#1224)
* fix(service): ProcessLauncher.cs - WaitForExitWithHeartbeat ignores TimeoutMs for the first WaitChunkMs slice; small TimeoutMs gets silently rounded up (#1225)
* fix(service): ProcessWrapper.cs - StopTree hardcodes 3000ms postKillWaitMs while Stop honors operator StopTimeout; descendants ignore configured timeout (#1227)
* fix(service): Service.cs - CheckHealth catch and Cleanup hook loop use static Logger instead of scoped _logger, dropping service-name prefix (#1229)
* fix(service): Service.cs - ConditionalResetRestartAttemptsAsync 'maintain previous session' branch never re-resets after the first cross-boot transit; restart counter sticks indefinitely (#1230)
* fix(service): ProcessLauncher.cs - Java -Dfile.encoding detection IndexOf can false-positive on paths or jar names containing the literal (#1234)
* fix(service): ProcessLauncher.cs - Unbounded WaitForExit() after timeout-aware loop can hang the synchronous launch path (#1235)
* fix(service): ProcessExtensions.cs - GetAllDescendants lacks cycle protection (visited-set), unlike ProcessHelper.GetProcessTree (#1246)
* fix(service): ProcessLauncher.cs - pathsMatch uses raw string comparison; equivalent paths with different normalization create two writers for the same file (#1282)
* fix(service): ProcessLauncher.cs - Hook stdout/stderr FileStreams omit FileShare.Delete; blocks external rotation/delete (LogTailer.cs:108 explicitly does the opposite) (#1306)
* fix(service): EnvironmentVariableHelper.cs - ExpandWithDictionary silently truncates on length cap; outer loop logs, inner loop does not (#1325)
* fix(service): ProcessExtensions.cs - GetChildren/GetAllDescendants leak Process handles when StartTime throws after GetProcessById succeeds (#1326)
* fix(service): Service.cs - SafeKillProcess collapses null Stop() result to true, masking timeouts as graceful cancellation (#1334)
* fix(service): Service.cs - OnStart Stop() paths leave ExitCode=0, SCM treats failed startup as graceful stop (#1337)
* fix(service): Service.cs - public SetProcessPriority dereferences _childProcess with null-forgiving operator and no guard (#1338)
* fix(service): ProcessLauncher.cs - WaitForExitWithHeartbeat enters tight CPU spin when WaitChunkMs is 0 (#1339)
* fix(service): ProcessLauncher.cs - Java -Dfile.encoding regex lacks RegexMatchTimeout, deviates from project pattern (ReDoS surface in launch path) (#1374)
* fix(service): ServiceHelper.cs (Servy.Service) - RestartProcess logs 'Process restarted' even when startProcess delegate is null (false success log) (#1375)
* fix(service): ServiceHelper.cs - EnvironmentVariablesToString throws if any variable Name is null (NRE during startup logging) (#1393)
* fix(service): Servy.Service ServiceHelper.cs - EnsureValidWorkingDirectory silently overrides operator config and can pass null path to Path.GetDirectoryName (#1398)
* fix(service): StartOptionsParser.cs - MapPriority silently defaults unrecognized ProcessPriority enum values to Normal (no log) (#1399)
* fix(service): StartOptions.cs vs Service.cs - Property defaults diverge from AppConfig (Priority, RecoveryAction, DateRotationType, HeartbeatInterval, StartTimeout, StopTimeout, MaxFailedChecks, EnableSizeRotation, EnableDateRotation, PreLaunchIgnoreFailure) (#1407)
* fix(service): Service.cs - Integer overflow in detectionWindowSeconds when HeartbeatInterval and MaxFailedChecks are at validated maximums (#1433)
* fix(service): StartOptionsParser.MapPriority - Normal case falls through to default and triggers misleading 'Unknown ProcessPriority' warning on every service start (#1437)
* fix(service): EnvironmentVariableHelper.cs - Custom env vars with empty values silently dropped; references like %EMPTY_VAR% remain literal in output (#1447)
* fix(service): ProcessLauncher.Start - WaitForExit(timeout) does not drain async output handlers; comment is misleading (#1485)
* fix(service): ProcessLauncher.Start - lazy stdout/stderr writer init retried on every output line when file open fails, flooding logs (#1489)
* fix(service): Service.cs OnStart - 'nint.Zero' used in a single line while the rest of the file uses 'IntPtr.Zero' (#1506)
* fix(service): Service.cs OnStart - ConditionalResetRestartAttempts task captures _cancellationSource before it is initialized; cancellation effectively unreachable in the typical timing (#1511)
* fix(service): Service.cs - background ConditionalReset continuation logs only InnerException.Message (no type/stack, drops siblings) (#1524)
* fix(service): Service.cs Cleanup - tracked hook Process.Kill(entireProcessTree:true) is synchronous with no timeout (#1558)
* fix(service,restarter): invalid EnableEventLog config value (e.g. 'yes', '1') silently disables event log; asymmetric with the documented '?? "true"' default and with neighbouring TryParse fallbacks in the same method (#1166)
* fix(restarter): ServiceController.Status - missing ThrowIfDisposed check, inconsistent with all other public members (#948)
* fix(restarter): ServiceRestarter.RestartService - TimeoutException thrown AFTER controller.Start() with misleading 'before the service could be started' wording (#949)
* fix(restarter): Program.Main mixes static Logger.Info with scopedLogger.Error - info events skip the service-name scope (#1001)
* fix(restarter): ServiceRestarter.RestartService - variable named 'startRemaining' is used during the STOP phase (line 44-48), misleads readers about which phase is racing the deadline (#1084)
* fix(restarter): Program.cs - Duplicate scopedLogger.SetLogLevel call (#1178)
* fix(restarter): ServiceRestarter.cs - HandleTransitionalError passes potentially negative TimeSpan to WaitForStatus (#1179)
* fix(restarter): Program.cs - protectedKeyProvider disposed before secureData (reverse-of-construction order) (#1202)
* fix(restarter): ServiceRestarter.cs - Stop-phase timeout exception incorrectly says 'No time remaining to start service' (#1263)
* fix(restarter): HandleTransitionalError 'Running' branch is dead code; Start phase lacks the same transitional-race handling Stop has (#1300)
* fix(restarter): HandleTransitionalError TimeoutException messages omit service name, breaking correlation in Event Log (#1301)
* fix(desktop): ServiceConfigurationValidator.Validate (Servy desktop) - 'checkServiceStatus' parameter is documented but never used in the body (dead parameter or unimplemented feature) (#899)
* fix(desktop): ServiceConfiguration.cs (Servy desktop) - multiple hardcoded default values bypass AppConfig.Default* constants (#981)
* fix(desktop): Servy.Mappers.ServiceConfigurationMapper - entire class is dead code (no callers) and silently drops EnableConsoleUI / EnableDebugLogs (#998)
* fix(desktop): MainViewModel parameterless constructor always throws (chained ctor rejects null) - body is dead code (#1011)
* fix(desktop): MainViewModel - constructor and ClearForm duplicate ~60 lines of default initialization (DRY) (#1013)
* fix(desktop): Servy ServiceCommands.InstallService rebuilds Config->DTO mapping (drifts from MainViewModel.ModelToServiceDto, different sentinel values) (#1021)
* fix(desktop): Servy ServiceCommands.cs - XML doc references parameter 'serviceRepository' that does not exist (#1022)
* fix(desktop): ServiceCommands - cancellationToken parameter accepted but never propagated to InstallServiceAsync / File.ReadAllTextAsync (#1023)
* fix(desktop): MainViewModel.cs - Import/Export commands don't set IsBusy, breaking command-availability invariants for parallel clicks (#1270)
* fix(desktop): MainViewModel.cs - BindServiceDtoToModel XML doc claims Password and ConfirmPassword are 'set to the same value' but ConfirmPassword is reset to string.Empty (#1335)
* fix(desktop): PasswordBoxHelper.cs - IsUpdating flag leaks if UpdateSource() throws (no try/finally) (#1425)
* fix(desktop,manager): Nullable reference types enabled in 5 projects (Core/CLI/Infra/Restarter/Service) but disabled in the 3 WPF projects (Servy / Servy.Manager / Servy.UI) (#903)
* fix(desktop,manager): MainWindow.xaml.cs OnClosing - Application.Current.Shutdown() called unconditionally, ignoring e.Cancel (#905)
* fix(desktop,manager): AppBootstrapper.cs InitializeAppAsync - 'processHelper' parameter is unused (the field _processHelper is used instead) (#907)
* fix(desktop,manager): AppBootstrapper.cs - fatal/unexpected error MessageBoxes use hardcoded English strings, bypassing Strings.resx (#908)
* fix(desktop,manager): Servy/App.xaml.cs and Servy.Manager/App.xaml.cs - ~80% duplicated code (StartAvailabilityMonitor, OnStartup, OnExit, faults, CTOR boilerplate) (#909)
* fix(desktop,manager): BulkObservableCollection - AddRange/TrimToSize fire CollectionChanged(Reset) but NOT PropertyChanged for Count and Item[] (#914)
* fix(desktop,manager): HelpService.cs - GitHub API URL hardcoded inline instead of in AppConfig (alongside DocumentationLink, LatestReleaseLink) (#915)
* fix(desktop,manager): Servy.UI/Helpers/Helper.cs GetRowsInfo - i18n bug: surrounding 'No', 'Loaded', 'in' words are hardcoded English even though rowText is localized (#916)
* fix(desktop,manager): Servy/App.xaml.cs and Servy.Manager/App.xaml.cs StartAvailabilityMonitor - Directory.CreateDirectory side-effect creates the optional executable's parent directory (#921)
* fix(desktop,manager): Servy.UI AsyncCommand.ExecuteAsync - TOCTOU race between CanExecute() and _isExecuting=true allows two parallel executions when called off the UI thread (#1125)
* fix(desktop,manager): ImportGuard.ValidateFileSizeAsync - file-not-found error is hardcoded English '[Import] File not found:' while size error uses localized format string (parallels #908) (#1140)
* fix(desktop,manager): Servy.UI InverseBooleanConverter.ConvertBack - bare 'catch { b = false; }' silently swallows all exceptions, fabricates an inverted value on garbage input (parallel to #1071) (#1162)
* fix(desktop,manager): Servy.UI DesignTimeFileDialogService - OpenXml / OpenJson doc-comments are stale copy-paste from OpenExecutable ('Simulates selecting an executable file' on the XML and JSON variants) (#1163)
* fix(desktop,manager): Servy.UI FileDialogService - every filter string and dialog title is hardcoded English ('Executable files', 'Select XML file', 'Select startup directory') bypassing Strings.resx (parallel to #908 / #1140) (#1164)
* fix(desktop,manager): Servy.UI HelpService.CheckUpdates - 10-second cancellation timeout (and the static 20-second HttpClient.Timeout) are private magic numbers, should live in AppConfig (#1165)
* fix(desktop,manager): Nullable IServiceRepository? parameters contradict ArgumentNullException guards (#1185)
* fix(desktop,manager): AppBootstrapper.cs - ProtectedKeyProvider created and never disposed; cached AES key material lingers in heap until GC (#1208)
* fix(desktop,manager): AppBootstrapper.cs - StartAvailabilityMonitor outer catch kills watcher permanently after single transient error (#1218)
* fix(desktop,manager): AppBootstrapper.cs - StartAvailabilityMonitor outer catch kills watcher permanently after single transient error (#1219)
* fix(desktop,manager): AppBootstrapper.cs - OnExit disposes _appLifetimeCts while async-void StartAvailabilityMonitor still observes its Token; ObjectDisposedException promoted to AppDomain.UnhandledException (#1221)
* fix(desktop,manager): AppBootstrapper.cs - SplashWindowFactory always invoked even when showSplash=false; resulting Window leaks until GC (#1238)
* fix(desktop,manager): AppBootstrapper.cs - OnExit double-disposes _availabilityWatcher (CleanupAvailabilityWatcher already nulls it out) (#1239)
* fix(desktop,manager): AppBootstrapper.cs - When OnStartup calls app.Shutdown() for security/SQLite checks, caller still proceeds to StartAvailabilityMonitor + InitializeAppWithFaultHandlingAsync (#1278)
* fix(desktop,manager): BulkObservableCollection.cs - TrimToSize(maxItems) throws ArgumentException when maxItems is negative (#1329)
* fix(desktop,manager): WpfUiDispatcher.cs - YieldAsync uses Dispatcher.CurrentDispatcher (per-thread), not the UI dispatcher; latent bug for any future background-thread caller (#1305)
* fix(desktop,manager): WpfUiDispatcher.cs - InvokeAsync(Action) silently no-ops on null while InvokeAsync<T>(Func<T>) throws ArgumentNullException (#1333)
* fix(desktop,manager): HelpService.cs - CheckUpdates ReadAsStringAsync ignores cts.Token, body-stage stall bypasses timeout (#1349)
* fix(desktop,manager): FileDialogService.cs - OpenFolder uses WinForms FolderBrowserDialog (Description is body label, not title); WPF Microsoft.Win32.OpenFolderDialog available since net8.0 (#1350)
* fix(desktop,manager): AppBootstrapper.cs - StartAvailabilityMonitor is 'async void' on a non-event-handler method; unobserved exceptions can crash the process (#1361)
* fix(desktop,manager): AppBootstrapper.cs - SQLite version-check abort uses app.Shutdown() with default exit code 0; sibling admin-check uses Shutdown(1) (#1362)
* fix(desktop,manager): AsyncCommand.cs - Generic 'AsyncCommand execution failed' log entry omits command identity, blocks 3am triage in a WPF app with many commands (#1383)
* fix(desktop,manager): HelpService.cs - Asymmetric handling of null Process.Start result between OpenDocumentation and CheckUpdates (#1387)
* fix(desktop,manager): Manager vs WPF ServiceConfigurationValidator - Reverse warning/error order; Manager comment contradicts code (#1426)
* fix(desktop,manager): AppBootstrapper.cs - Connection string read from wrong config key, silently ignores user override (#1431)
* fix(desktop,manager): AppBootstrapper.cs - AppDomain.UnhandledException handler calls MessageBox.Show; non-UI threads can deadlock or crash (#1499)
* fix(desktop,manager): AppBootstrapper.StartAvailabilityMonitorAsync - FileSystemWatcher EnableRaisingEvents = true is set in the object initializer before event handlers are subscribed (event-loss race window) (#1553)
* fix(manager,cli,service,restarter): Hardcoded delay literals (Task.Delay / Thread.Sleep) bypass the AppConfig timing-constants pattern (#1186)
* fix(desktop,manager,cli,service,restarter): Multiple Program.cs files - Duplicated logger/config bootstrap block across CLI, Restarter, Service, and Manager entry points (#1201)
* fix(desktop,manager,cli,service,restarter): appsettings*.json - DefaultConnection / AESKey / AESIV paths duplicated across 5 files (and re-defined in AppConfig.cs) (#1427)
* fix(manager): ServiceCommands.ExecuteLockedAsync - eager eviction races with GetOrAdd, allows two threads to run the same service operation in parallel (#831)
* fix(manager): LogTailer.RunFromPosition - XML doc references non-existent parameter 'token' (actual name is 'externalToken') (#890)
* fix(manager): ConsoleViewModel.cs - case-sensitive path comparisons trigger spurious resume on Windows (paths are case-insensitive) (#904)
* fix(manager): MainWindow.xaml.cs MainTabControl_SelectionChangedAsync - exception not logged when MessageBoxService is available, silently lost on dialog dismiss (#906)
* fix(manager): App.xaml.cs - typo 'Dependencis' in DependenciesRefreshIntervalInMs XML doc (#910)
* fix(manager): DependenciesViewModel.cs SetExpansion - recursive traversal has no cycle guard, infinite recursion / stack overflow if tree contains a cycle (#912)
* fix(manager): DependenciesViewModel.cs duplicates SearchServicesAsync from ServiceSearchViewModelBase (DRY) (#913)
* fix(manager): ServiceMapper.ToModel - three near-identical overloads (Performance/Console/Dependency Service) instead of one base-class mapping (#919)
* fix(manager): ServiceCommands.ImportXmlConfigAsync/ImportJsonConfigAsync - instantiates serializers directly instead of using validators-style DI (#832)
* fix(manager): ServiceCommands.CopyPid - Clipboard.SetText called without ensuring STA Dispatcher context (#833)
* fix(manager): ServiceCommands.InstallServiceAsync - 'if (wrapperExeDir == null)' is always true (dead defensive check + CS8600 candidate) (#894)
* fix(manager): DependenciesViewModel.cs - repeated 'depdency treer' typo in three command XML doc-comments (#911)
* fix(manager): Servy.Manager ServiceMapper - '?? string.Empty' is unreachable because GetLogOnAsDisplayName never returns null (#920)
* fix(manager): LogsViewModel.cs - magic number 3 for default 'last N days' filter on first load (#924)
* fix(manager): ServiceRowViewModel.Service_PropertyChanged - explicit switch cases are redundant; default branch already forwards every PropertyName 1:1 (#925)
* fix(manager): LogTailer.LoadHistory - historical lines synthesize timestamps 1ms apart, capping the rendered time-spread of any backlog at maxLines milliseconds (#939)
* fix(manager): ServiceSearchViewModelBase.SearchServicesAsync - Helper.IsRunningInUnitTest() branch in production code path is a layering violation (#983)
* fix(manager): ServiceMapper.GetLogOnAsDisplayName - magic string "LocalSystem" duplicated instead of using a named SCM constant (#984)
* fix(manager): ServiceMapper.GetLogOnAsDisplayName - XML doc typo 'ser session display name' (should be 'User') (#1006)
* fix(manager): MainViewModel parameterless constructor always throws (chained ctor rejects null) - same dead-code pattern as Servy (#1017)
* fix(manager): MainViewModel - six occurrences of 'if (ServiceCommands == null) throw new InvalidOperationException' (DRY) (#1018)
* fix(manager): MainViewModel - typo 'Setp 5' in step comment (line 583) (#1019)
* fix(manager): MainViewModel - MaxBulkOperationParallelism = 8 is a private const, should live in AppConfig (#1020)
* fix(manager): ServiceCommands.StartProcess - XML doc references parameter 'app' that does not exist (#1024)
* fix(manager): ServiceCommands.RefreshServices - typo 'resfresh' in XML doc, plus dead null check on injected callback (#1025)
* fix(manager): ConsoleViewModel - SearchDebounceDelayMs = 300 hardcoded as private const, should live in AppConfig (#1026)
* fix(manager): PerformanceViewModel.Dispose duplicates the _monitoringCts cleanup already performed by MonitoringViewModelBase.Dispose (#1109)
* fix(manager): CpuUsageConverter & RamUsageConverter - stale 'this will safely return false' comment doesn't match the string-returning code (#1110)
* fix(manager): ServiceConfigurationValidator depends on concrete ServiceValidationRules instead of IServiceValidationRules (parallel to CLI) (#1111)
* fix(manager): ServiceConfigurationValidator.Validate shows first Warning before any Error, hiding the actually-blocking issue (parallel impl of #1108) (#1126)
* fix(manager): PidConverter.Convert - dead null check (early return + redundant '?.' / '??') (#1154)
* fix(manager): Converters - Pid/Message/CpuUsage/RamUsage throw NotImplementedException in ConvertBack while StatusConverter/StartupTypeConverter return Binding.DoNothing (inconsistent) (#1155)
* fix(manager): CpuUsageConverter / RamUsageConverter constructor throws InvalidOperationException at design time when App.Services is null (breaks XAML designer) (#1156)
* fix(manager): LogEntryModel.LevelIcon - magic-string level comparison ('Information'/'Warning'/'Error') and four hardcoded pack-URI strings (EventLogLevel enum already exists) (#1157)
* fix(manager): Models inconsistency - Service.cs uses SetProperty/CallerMemberName helper, LogEntryModel.cs hand-rolls INotifyPropertyChanged setters with nameof (DRY) (#1158)
* fix(manager): Models Service.cs - backing field '_userSession' for LogOnAs property is inconsistent with every other field in the file (#1159)
* fix(manager): LogTailer.cs - Hardcoded MaxSafeLines / LogBatchFlushThreshold / FileRetryDelayMs / 1000ms recovery delay should live in AppConfig (#1203)
* fix(manager): LogTailer.cs - lastPosition tracked via fs.Position overshoots StreamReader buffer; transient errors skip log content on reopen (#1245)
* fix(manager): PerformanceViewModel.cs - 'Pre-allocated to avoid GC pressure' comment is misleading; Clone() still allocates a new PointCollection on every tick (#1264)
* fix(manager): PerformanceViewModel.cs - hardcoded magic '100.0' for stepX should derive from PerformanceHistoryCapacity (#1265)
* fix(manager): LogsViewModel.cs - eventLogService constructor parameter is not null-guarded; sibling deps are (#1267)
* fix(manager): MainViewModel.cs (Manager) è Dispose(bool) skips most cleanup that Cleanup() performs (timer, CTS, ServiceCommands, busy cursor) (#1271)
* fix(manager): MainViewModel.cs (Manager) - SearchServicesAsync uses ServiceCommands?.SearchServicesAsync but follow-up lines presume non-null result, NREs if guard ever triggers (#1272)
* fix(manager): ServiceCommands.cs - CopyPid uses Thread.Sleep on the WPF Dispatcher; clipboard contention freezes the UI for up to 250ms (#1273)
* fix(manager): MainViewModel.cs - Cleanup() disposes shared ServiceCommands on tab switch; subsequent tab returns leak semaphores via the still-shared disposed engine (#1274)
* fix(manager): MainWindow.xaml.cs (Manager) - Five fire-and-forget '_ = AsyncMethod()' wrappers swallow exceptions silently; only one wrapper has a try/catch (#1275)
* fix(manager): App.xaml.cs - CustomConfigAction parses RefreshIntervalInSeconds/etc. without bounds-checking; zero or negative values crash the timer-creating VMs at startup (#1276)
* fix(manager): ConsoleViewModel.cs - Mixes injected IUiDispatcher with direct Application.Current.Dispatcher reads, breaking testability (#1277)
* fix(manager): WpfUiDispatcher.cs - YieldAsync uses Dispatcher.CurrentDispatcher (per-thread), not the UI dispatcher; latent bug for any future background-thread caller (#1305)
* fix(manager): ServiceCommands.cs - ArgumentException thrown for null arguments instead of ArgumentNullException (Export/RemoveService) (#1321)
* fix(manager): Manager MainWindow.xaml.cs - Three Handle*TabSelected helpers declared async without any await (CS1998) (#1322)
* fix(manager): MonitoringViewModelBase.cs - StopMonitoring 'clearView' parameter declared but ignored in base implementation (#1330)
* fix(manager): ServiceRowViewModel.cs - CanExecuteServiceCommand ignores Service.Status while Status changes still trigger RaiseCanExecuteChanged (#1331)
* fix(manager): DependenciesViewModel.cs - Constructor XML doc omits messageBoxService and contains 'depdency' typos in command summaries (#1332)
* fix(manager): RamUsageConverter.cs - Convert() docs/comment claim 'PID' and 'double?' but value is the RAM usage as long (#1348)
* fix(manager): ServiceCommands.cs - ExecuteLockedAsync semaphore acquisition ignores caller's CancellationToken (#1352)
* fix(manager): ServiceMapper.cs (Manager) - ToModelAsync ignores CancellationToken; bulk search keeps churning processHelper after cancel (#1354)
* fix(manager): App.xaml.cs - Enum.TryParse for LogLevel accepts numeric strings as valid (same family as #1283/#1284/#1289/#1324) (#1390)
* fix(manager): StatusConverter.cs - Convert/ConvertBack maps duplicated; null/unknown value masquerades as 'Not Installed' (#1421)
* fix(manager): App.xaml.cs (Manager) - ConsoleMaxLines upper bound is DefaultConsoleMaxLines*2 magic factor; deviates from sibling GetConfigInt calls (#1422)
* fix(manager): LogTailer.cs - File-not-found uses 3 different retry delays (1000ms / 500ms / 200ms) for similar transient conditions (#1423)
* fix(manager): LogLine.cs / LogTailer.cs - Timestamp Kind drifts: live tailing uses DateTime.Now (Local), history uses LastWriteTimeUtc (Utc) (#1424)
* fix(manager): LogTailer.cs - RunFromPosition byte tracking assumes Environment.NewLine, drifts on LF-only log files (#1455)
* fix(manager): Servy.Manager.Services.ServiceCommands.ImportConfigAsync - no CancellationToken throughout import chain; large/slow-network XML/JSON imports cannot be cancelled (#1483)
* fix(manager): LogTailer.LoadHistory - off-by-one drops the last N lines when file lacks a trailing newline (#1515)
* fix(manager): ServiceCommands.ExecuteServiceCommandAsync - Task.Run drops cancellationToken (inconsistent with InstallServiceAsync/UninstallServiceAsync siblings) (#1551)
* fix(cli): InstallServiceCommand - four ParseEnumOption defaults hardcoded as enum literals instead of AppConfig.Default* constants (#895)
* fix(cli): InstallServiceOptions.cs --params HelpText example uses singular --param= which the CLI does not accept (#964)
* fix(cli): ExportServiceCommand.SaveFile - reserved device name check bypassed by extra extension (e.g. NUL.config.json passes) (#991)
* fix(cli): ExportServiceCommand.SaveFile - comment says '8. Final Atomic Write' but File.WriteAllText is not atomic; Helper.WriteFileAtomic exists (#992)
* fix(cli): InstallServiceCommand - nine int.TryParse fallback lines should call ConfigParser.ParseInt (DRY) (#1035)
* fix(cli): ImportServiceCommand.ValidateServicePaths - hardcoded list of 11 path fields will silently miss new fields added to ServiceDto (#1036)
* fix(cli): ServiceInstallValidator depends on concrete ServiceValidationRules instead of IServiceValidationRules (#1107)
* fix(cli): ervy.CLI ServiceInstallValidator.Validate - reports first Warning before any Error, hiding the actually-blocking issue (#1108)
* fix(cli): Program.Main - AppDbContext is created but never Disposed; finally only disposes _secureData and Logger (#1127)
* fix(cli): Program.Main - Environment.Exit(1064) on SQLite version mismatch reuses Windows-service exit code (ERROR_EXCEPTION_IN_SERVICE) for a console-app exit (#1142)
* fix(cli): Program.cs - SQLite version-fail path uses Environment.Exit, skipping the finally block that flushes the logger (#1280)
* fix(cli): InstallServiceOptions.cs - Multiple copy-paste errors in XML doc summaries (process vs pre-launch/post-launch/pre-stop/post-stop) (#1288)
* fix(cli): ImportServiceCommand.cs - TryParseFileType accepts numeric strings like '0'/'1' for ConfigFileType (#1283)
* fix(cli): InstallServiceCommand.cs - ParseEnumOption accepts undefined numeric enum values (#1284)
* fix(cli): ServiceInstallValidator.cs - MapEnum accepts numeric strings (e.g. '99') as valid enum values (#1289)
* fix(cli): ServiceInstallValidator.cs - TryMapToDto omits EnableConsoleUI; validator DTO drifts from desktop/CLI install paths (#1298)
* fix(cli): ExportServiceCommand.cs - Enum.TryParse accepts numeric strings ('0'/'1') for ConfigFileType (same defect as #1283) (#1324)
* fix(cli): Commands - 7 of 8 Execute methods drop CancellationToken; only UninstallServiceCommand accepts one (#1408)
* fix(cli): BaseCommand.cs - ExecuteWithHandling and ExecuteWithHandlingAsync are near-identical 30-line duplicates (DRY) (#1430)
* fix(cli): ImportServiceCommand.ProcessImportInternalAsync - File.ReadAllTextAsync ignores cancellationToken; cannot abort on slow/network paths (#1436)
* fix(cli): ImportServiceCommand.ProcessImportInternalAsync - DB import happens before path validation; failed --installService leaves DB in dirty state (#1439)
* fix(cli): ImportServiceCommand.TryInstallServiceAsync / GetByNameAsync don't accept or propagate the import's CancellationToken (#1466)
* fix(cli): StartServiceCommand.ExecuteAsync - cancellationToken not propagated to StartServiceAsync (inconsistent with Stop/Restart) (#1474)
* fix(cli): ImportServiceCommand.ProcessXmlAsync - CancellationToken not forwarded; XML imports/installs are not cancellable while JSON ones are (#1540)
* fix(cli): ExportServiceCommand.ExecuteAsync - GetByNameAsync existence check missing CancellationToken (inconsistent with ExportXml/JsonAsync) (#1541)
* fix(cli,restarter,service): ProtectedKeyProvider is never disposed in CLI / Restarter / Service composition roots (cached unprotected key material leaks) (#1182)
* fix(cli,restarter,service): linker.xml duplicated across Servy.Service / Servy.Restarter (and 90% overlapping with Servy.CLI) (#1569)
* fix(psm1): Format-SecureLogMessage - docstring example output does not match actual output (says space-separated, but actual is '=' separated) (#951)
* fix(psm1): Invoke-ServyCli - synchronous stdout read can defeat ServyTimeoutSeconds (CLI may hang past timeout) (#987)
* fix(psm1): Export-ServyServiceConfig and Import-ServyServiceConfig - docstring claims 'Requires Administrator privileges' but Assert-Administrator is never called (#988)
* fix(psm1): identical ValidatePattern regex duplicated for EnvVars and PreLaunchEnv (DRY) (#1029)
* fix(psm1): $script:ServyTimeoutSeconds (600s) and $script:ServyMaxBufferChars (1MB) are hardcoded with no public way to override (#1043)
* fix(psm1): Format-SecureLogMessage line 186 inline regex comment has typo ("[\]*" should be "[^"]*") (#1052)
* fix(psm1): $script:EnvVarValidationPattern is a ReDoS-prone alternation regex with no timeout (PS 2.0 .NET regex doesn't support timeouts) (#1091)
* fix(psm1): Add-Arg ArrayList 'performance' optimization is defeated by PowerShell pipeline unrolling on return (#1237)
* fix(psm1): Logging-&-Log-Rotation.md - Documents -DateRotationType 'None' option, but Install-ServyService [ValidateSet] only accepts Daily/Weekly/Monthly (#1240)
* fix(psm1): Invoke-ServyCli scrubs stderr (success path) and both streams (catch path) but leaves success-path stdout unscrubbed (#1299)
* fix(psm1): Buffer field named ByteCount actually accumulates char count (.Length), making truncation cap unit-ambiguous (#1402)
* fix(tests): CLI command tests - Hardcoded English message assertions break on any Strings.resx edit (#744)
* fix(publish): Five per-project publish.ps1 scripts each contain a near-identical Check-LastExitCode function (DRY) (#977)
* fix(publish): (Servy / Servy.CLI / Servy.Manager) - if ($null -ne $signPath) is unreachable; Join-Path never returns null (#978)
* fix(publish): setup/publish-sc.ps1 and setup/publish-fd.ps1 are ~70% identical (Inno-Setup retry loop, Tool Discovery, Check-LastExitCode, Remove-ItemSafely, package construction) (#1030)
* fix(publish): setup/signpath.ps1 - line 167 contains invalid UTF-8 byte (0x97 / Windows-1252 em-dash) in comment (#1063)
* fix(publish): framework-dependent (publish-fd.ps1) invocation is commented out, leaving FD build orchestration dead code in the master entry script (#1149)
* fix(publish): signpath.ps1 - while-loop unloading the SignPath module relies on Remove-Module silently succeeding, infinite loop if removal is suppressed by -ErrorAction SilentlyContinue (#1150)
* fix(publish): setup/winget/manifests/.../1.0/aelassas.servy.installer.yaml - version+SHA+TFM hardcoded and bump-version.ps1 does NOT update them (#1061)
* fix(publish): setup/scoop/servy.json - version 4.0 + sha256 hardcoded; bump-version.ps1 does NOT update it (#1062)
* fix(publish): publish-common.ps1 - Copy-Item -Recurse -Exclude does not match files in subdirectories; smtp-cred.xml could be packaged into installer (#1200)
* fix(publish): publish-sc.ps1 / publish-fd.ps1 - Inconsistent error termination ('return' vs 'exit 1') between sister scripts (#1255)
* fix(publish): publish-common.ps1 - Check-LastExitCode uses unapproved verb (PSScriptAnalyzer warning) (#1411)
* fix(publish): build-common.ps1 vs publish-common.ps1 - Two divergent definitions of Check-LastExitCode (one exits, one doesn't) (#1412)
* fix(publish): signpath.ps1 - Config key uppercased with culture-dependent ToUpper() (Turkish locale breaks 'I' keys) (#1413)
* fix(publish): tools-config.ps1 - Resolve-Tool returns env-var path without Test-Path validation, may return non-existent path (#1414)
* fix(bump-version): Get-ChildItem -Recurse on \$baseDir without bin/obj/.git/packages exclusion (same root cause as #1258, different file) (#1302)
* fix(bump-runtime): Get-ChildItem -Recurse on $baseDir without bin/obj/.git/packages exclusion can rewrite vendor and build-output files (#1258)
* fix(notifications): setup/taskschd/*.ps1 - dead PS 2.0 $PSScriptRoot fallback in scripts that already declare #Requires -Version 3.0+ (#955)
* fix(notifications): ServyFailureEmail.ps1 - $serviceName not HTML-encoded in email body (only $logText is); subject also unscrubbed (#1031)
* fix(notifications): ServyFailureEmail.ps1 - EnableSsl hardcoded to $true; no way to use internal SMTP relays that don't support TLS (#1032)
* fix(notifications): ServyFailureNotification.ps1 and ServyFailureEmail.ps1 share ~50 lines of identical watermark/event-processing logic (DRY) (#1034)
* fix(notifications): ServyFailureEmail.ps1 - SmtpClient.Send has no Timeout set, defaults to 100s blocking the scheduled task (#1147)
* fix(notifications): ServyFailureNotification.ps1 - Toast Tag/Group both set to 'Servy' causes new toasts to silently replace earlier ones in Action Center (#1044)
* fix(notifications): ServyFailureNotification.ps1 - '#requires -Version 5.1' makes the PS 2.0 fallback at lines 126-132 unreachable (#1057)
* fix(notifications): ServySecurity.ps1 - broad 'KEY' keyword masks legitimate values like FOREIGN_KEY/PRIMARY_KEY (parallel impl of #1055) (#1058)
* fix(notifications): ServyFailureEmail.ps1 - Protect-SensitiveString runs AFTER HTML encoding, defeating the quoted-string regex branch and leaking partial credentials (#1101)
* fix(notifications): Get-ServyLastErrors.ps1 - DateTime.Parse uses current-culture, locale-dependent date parsing of LastProcessed (#1146)
* fix(notifications): Get-ServyLastErrors.ps1 / ServyFailureEmail.ps1 / ServyFailureNotification.ps1 - fallback .log files use Out-File without -Encoding and grow unbounded forever (#1148)
* fix(notifications): ServyFailureNotification.ps1 - Show-Notification references caller-scope $evt without declaring it as a parameter (#1180)
* fix(notifications): ServySecurity.ps1 - Protect-SensitiveString claims parity with C# MaskingRegex but is missing the space-separator and '/' branches; secrets in CLI args leak into email/toast notifications (#1196)
* fix(notifications): Servy-Watermark.psm1 - Update-Watermark guards $null against a [DateTime] (value type) parameter, which can never be null (#1199)
* fix(notifications): ServyFailureEmail.ps1 - Watermark advances even when SMTP send fails; transient outages permanently lose alerts (#1252)
* fix(notifications): ServyFailureNotification.ps1 - Watermark advances even when toast notification fails; transient errors permanently lose alerts (#1253)
* fix(notifications): Get-ServyLastErrors.ps1 / Servy-Watermark.psm1 - EVENT_ID_ERROR=3103 duplicated; consumers hardcode 3104 instead of using exported constants (#1254)
* fix(notifications): ServyFailureEmail.ps1 - 'break' inside switch inside foreach exits switch only, foreach keeps processing on transient SMTP failure (#1344)
* fix(notifications): taskschd .vbs and .xml files hardcode 'C:\Program Files\Servy' - tasks break on custom install paths (#1345)
* fix(notifications): ServySecurity.ps1 - Protect-SensitiveString masking regex has no timeout, vulnerable to ReDoS via nested quantifiers (#1368)
* fix(notifications): ServyFailureEmail.ps1 - Catch-all classifies permanent SmtpExceptions (auth failure, invalid recipient) as TransientFailure, causing watermark stall (#1384)
* fix(notifications): Servy-Watermark.psm1 - Silently loads with Write-Warning when Get-ServyLastErrors.ps1 / Write-ServyLog.ps1 are missing, causing 'command not found' at runtime (#1385)
* fix(notifications): Servy-Watermark.psm1 - $EVENT_ID_ERROR_DEP declared but never used (#1394)
* fix(notifications): Get-ServyLastErrors.ps1 - References $EVENT_ID_ERROR from parent module scope without declaring it as a parameter (same family as #1180) (#1395)
* fix(notifications): Write-ServyLog.ps1 - rotatedFilePath variable computed but never used (dead code) (#1410)
* fix(Get-FileEncoding): Get-FileEncoding.ps1 - ReadAllBytes loads entire file into memory just to inspect 3-byte BOM (#1136)
* fix(bump-runtime): Get-FileEncoding only detects UTF-8 BOM, while bump-version.ps1's identical-name helper also detects UTF-16 LE/BE BOMs (#979)
* fix(bump-runtime): .EXAMPLE comments reference 'update-runtime.ps1' but actual file name is 'bump-runtime.ps1' (#1135)
* fix(bump-runtime): regex 'net\d+\.\d+' will partially replace inside three-segment versions like 'net10.0.1', mangling the version (#1532)
* fix(bump-runtime): bin/obj exclusion regex '[\/]' never matches Windows backslash paths (#1572)
* fix(bump-version): local $matches assignment shadows PowerShell automatic variable (#950)
* fix(bump-version): setup/publish-sc.ps1 and publish-fd.ps1 - default $Version stuck at "1.0" while bump-version.ps1 only updates setup/publish.ps1 (#956)
* fix(bump-version): csproj loop is silent on no-match (unlike Update-FileContent helper) (#1002)
* fix(bump-version): Write-Error does not terminate the script, allowing silent partial failures across version bump steps (#1198)
* fix(docs): Environment-Variables.md - verification recipe uses 'cmd.exe /c set > ... && pause' which makes the service hang in Session 0 (no console = pause never returns) (#1151)
* ci(test): Notify Codecov/Coveralls comments are posted on CLOSED issues, so maintainers receive no notification (#1096)
* ci(test): test.yml - Invoke-Expression to run dotnet test command is unnecessary and an injection-prone anti-pattern (#1262)
* ci(publish): 7-Zip and CycloneDX CLI binaries downloaded without Authenticode signature verification (#849)
* ci(publish): 'dotnet tool install --global CycloneDX' has no version pin (#851)
* ci(publish): unit tests run in Debug while artifacts ship from Release; release-only regressions slip through CI (#862)
* ci(publish): 'Cool down API' step uses legacy 'powershell' shell instead of 'pwsh' (inconsistent with every other step) (#1082)
* ci(publish): initial 'Build Servy projects' phase rebuilds every project that is then re-built via 'dotnet publish' later (wasted work + risk of stale artifacts) (#1083)
* ci(publish): Initial dotnet build runs in Debug while rest of workflow is Release (wasted CI time) (#1181)
* fix(publish): publish-common.ps1 - New-PortablePackage comment 'compress contents of the folder, not the folder itself' contradicts the actual 7-Zip invocation which includes the folder itself (#1226)
* ci(setup-dotnet): .github/actions/setup-dotnet/action.yml - 'version' input + DOTNET_VERSION env are dead; install version actually comes from global.json (#1261)
* ci(publish): publish.yml - Copy-Item -Recurse -Exclude does not match files in subdirectories (same bug as #1200, different file; also packages portable 7z) (#1303)
* ci(choco): chocolateyuninstall.ps1 - 'Servy*' wildcard lookup mis-handles multi-match case (silent partial uninstall) (#1340)
* ci(security): 'Scan for vulnerable packages' uses legacy 'powershell' shell instead of 'pwsh' (parallels #1082) (#1097)
* ci(security): Concurrency block commented out (lines 26-28); either delete or activate (#1098)
* ci(release): 'changelog' workflow triggers this run but is missing from the validation list (asymmetry) (#1099)
* ci(dotnet-reflection): comment claims 'disabled' but workflow_dispatch is still active; also typo 'worflow' and commented-out triggers (#1100)
* ci(sbom): sbom.yml - Generate SBOM step skips exit-code checks between five dotnet-CycloneDX invocations; partial-failure completes with success status (#1312)
* ci(sonar): sonar.yml - Missing top-level permissions block leaves GITHUB_TOKEN at repo defaults; PR trigger silently commented out (#1310)
* ci(loc): loc.yml - 'Create total LoC badge' step uses 'curl -s' without -f or exit-code check; shields.io HTTP errors are silently published as the LoC total badge (#1313)
* ci: five workflow files saved as Windows-1252 (Non-ISO extended-ASCII) instead of UTF-8, breaking YAML 1.2 spec and rendering em-dashes as mojibake (#954)
* chore(deps): update dependencies

</details>

### Downloads
* [servy-8.4-net48-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.4/servy-8.4-net48-sbom.xml) - 0.02 MB
* [servy-8.4-net48-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.4/servy-8.4-net48-x64-installer.exe) - 4.1 MB
* [servy-8.4-net48-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.4/servy-8.4-net48-x64-portable.7z) - 1.84 MB
* [servy-8.4-sbom.xml](https://github.com/aelassas/servy/releases/download/v8.4/servy-8.4-sbom.xml) - 0.04 MB
* [servy-8.4-x64-installer.exe](https://github.com/aelassas/servy/releases/download/v8.4/servy-8.4-x64-installer.exe) - 81.24 MB
* [servy-8.4-x64-portable.7z](https://github.com/aelassas/servy/releases/download/v8.4/servy-8.4-x64-portable.7z) - 79.19 MB
* [Source code (zip)](https://github.com/aelassas/servy/archive/refs/tags/v8.4.zip)
* [Source code (tar.gz)](https://github.com/aelassas/servy/archive/refs/tags/v8.4.tar.gz)

Compare changes: https://github.com/aelassas/servy/compare/v8.3...v8.4

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
* fix(core): SecurityHelper.ApplySecurityRules - comparing the current user's SID to BuiltinAdministrators/LocalSystem GROUP SIDs is meaningless (#933)
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
* fix(service): Partial nginx shutdown leaves orphan process running (#784)
* fix(core): mixed string empty checks: IsNullOrWhiteSpace vs IsNullOrEmpty used interchangeably (#397)
* fix(core): Multiple files - Process.MainModule!.FileName! pattern repeated in 7 locations (follow-up to #724) (#757)
* fix(core): RotatingStreamWriter.cs - Synchronous Thread.Sleep retries during rotation stall stdout/stderr capture (#761)
* fix(core): Post-launch hook missing EnvironmentVariables / Stdout / Stderr / Timeout / Retry / IgnoreFailure - asymmetric with pre-launch (#762)
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
* fix(core): EventLogReader.cs:44 - Truncated \<returns\> docstring ending mid-word (#748)
* fix(service): ensure event log availability during pre-shutdown via explicit dependency
* fix(service): ProcessLauncher.cs - Duplicate FireAndForget check is dead code (#707)
* fix(service): EnvironmentVariableHelper.cs - Duplicate \<summary\> XML doc on ExpandWithDictionary (#708)
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
* fix(cli): InstallServiceCommand.cs - Duplicate \<summary\> XML doc blocks on Execute method (#704)
* fix(cli): ConsoleHelper.cs - Duplicate \<summary\> XML doc on RunWithLoadingAnimation (#709)
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
* fix(core): NativeMethods.cs - Regex character class `[@!-]` parsed as ASCII range, allows unintended characters (#563)
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
* fix(psm1): Add-Arg null check on $list is dead code - callers always pass `@()` (#131)
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
* fix(notifications): ServyFailureEmail/Notification.ps1 - Sort-Object result not wrapped in `@()`, scalar on single event (#614)
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
* ci(actions): `actions/checkout@v5`, `@v6`, and `upload-artifact@v6` may not exist (#548)
* ci(actions): Third-party actions pinned to floating `@master/@main` branches (#554)
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

