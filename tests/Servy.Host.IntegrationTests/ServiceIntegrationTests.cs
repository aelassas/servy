using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Servy.Host.IntegrationTests
{
    /// <summary>
    /// Runs the Servy host's named pipe listener for real - the production server instances with the host's DACL, a
    /// SQLite <see cref="ServiceRepository"/>, and the production <see cref="NamedPipesService"/> client - inside the test
    /// process, which plays both the host and the service whose process is asking.
    /// </summary>
    /// <remarks>
    /// Connecting to a pipe that carries the host's DACL requires Local System or an elevated administrator, so every
    /// test is skipped when the run is not elevated (CI runners are). The caller identification is wrapped to report a
    /// non-administrator by default, so the PID-based authorization is what decides, as it does for a service account.
    /// </remarks>
    public class ServiceIntegrationTests : IDisposable
    {
        private const string NotElevatedSkipReason = "Connecting to the host's pipe requires an elevated process.";
        private const string ServiceName = "IntegrationService";

        private readonly bool _isElevated = SecurityHelper.IsAdministrator();
        private readonly string _pipeName = "ServyHostTest_" + Guid.NewGuid().ToString("N");
        private readonly InMemoryDbContext _db = new InMemoryDbContext();
        private readonly ServiceRepository _repository;
        private readonly Mock<IWindowsServiceApi> _api = new Mock<IWindowsServiceApi>();
        private readonly SwitchableCallerIdentifier _identifier = new SwitchableCallerIdentifier();
        private readonly Service _host;

        public ServiceIntegrationTests()
        {
            _db.Initialize();
            _repository = new ServiceRepository(new DapperExecutor(_db), new PassThroughSecureData(), new XmlServiceSerializer(), new JsonServiceSerializer());

            // The test process is both the host service and the process of IntegrationService
            _api.Setup(a => a.GetServiceProcessId(AppConfig.ServyHostServiceName)).Returns(Environment.ProcessId);
            _api.Setup(a => a.GetServiceProcessId(ServiceName)).Returns(Environment.ProcessId);
            _api.Setup(a => a.GetServiceProcessId("OtherService")).Returns(Environment.ProcessId + 100);

            _host = new Service(new Mock<IServyLogger>().Object, new NamedPipesService(_pipeName), _repository, _api.Object, _identifier)
            {
                PipeName = _pipeName,
                // Generous: twelve concurrent synchronous clients can wait a while for thread-pool threads
                RequestTimeoutMs = 15000,
            };
        }

        public void Dispose()
        {
            try { _host.StopListening(); } catch { /* best effort */ }
            _host.Dispose();
            _db.Dispose();
        }

        private NamedPipesService Client(int requestTimeoutMs = 5000, int connectTimeoutMs = 3000)
            => new NamedPipesService(_pipeName, new ServyHostServerVerifier(_api.Object), connectTimeoutMs: connectTimeoutMs, requestTimeoutMs: requestTimeoutMs);

        private async Task SeedAsync(string name)
            => await _repository.AddAsync(new ServiceDto { Name = name, ExecutablePath = "C:\\app.exe", Parameters = "--secret", Password = "pwd" }, TestContext.Current.CancellationToken);

        [Fact]
        public async Task ServiceProcess_ReadsItsConfigurationAndWritesItsRuntimeStateAndCounter()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            var ct = TestContext.Current.CancellationToken;
            await SeedAsync(ServiceName);
            _host.StartListening();
            var client = Client();

            // Act
            var config = await Task.Run(() => client.GetByName(ServiceName, ct), ct);
            var updated = await Task.Run(() => client.UpdateRuntimeState(ServiceName, new ServiceRuntimeStateDto
            {
                Pid = 31337,
                ActiveStdoutPath = "C:\\out.log",
                ActiveStderrPath = "C:\\err.log",
                UpdatePreviousStopTimeout = true,
                PreviousStopTimeout = 45,
            }, ct), ct);
            var written = await client.UpdateRestartAttemptsAsync(ServiceName, 2, ct);
            var attempts = await client.GetRestartAttemptsAsync(ServiceName, ct);

            // Assert: the configuration arrives decrypted, without the password
            Assert.Equal("C:\\app.exe", config!.ExecutablePath);
            Assert.Equal("--secret", config.Parameters);
            Assert.Null(config.Password);

            // ... the runtime state and the counter reached the database
            Assert.Equal(1, updated);
            Assert.Equal(1, written);
            Assert.Equal(2, attempts.Attempts);
            Assert.NotNull(attempts.UpdatedAtUtc);
            Assert.True(attempts.UpdatedAtUtc > DateTime.UtcNow.AddMinutes(-5));
            var row = await _repository.GetByNameAsync(ServiceName, decrypt: true, ct);
            Assert.Equal(31337, row!.Pid);
            Assert.Equal("C:\\out.log", row.ActiveStdoutPath);
            Assert.Equal(45, row.PreviousStopTimeout);

            // ... and the configuration was not touched
            Assert.Equal("C:\\app.exe", row.ExecutablePath);
            Assert.Equal("--secret", row.Parameters);
            Assert.Equal("pwd", row.Password);
        }

        [Fact]
        public async Task ServiceProcess_AskingAboutAnotherService_IsRefusedAndTheOtherServiceIsUntouched()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            var ct = TestContext.Current.CancellationToken;
            await SeedAsync("OtherService");
            _host.StartListening();
            var client = Client();

            // Act
            var read = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => client.GetByName("OtherService", ct), ct));
            var write = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => client.UpdateRuntimeState("OtherService", new ServiceRuntimeStateDto { Pid = 1 }, ct), ct));
            var counter = await Assert.ThrowsAsync<InvalidOperationException>(() => client.UpdateRestartAttemptsAsync("OtherService", 99, ct));

            // Assert
            Assert.Contains("Access denied.", read.Message);
            Assert.Contains("Access denied.", write.Message);
            Assert.Contains("Access denied.", counter.Message);
            var row = await _repository.GetByNameAsync("OtherService", decrypt: true, ct);
            Assert.Null(row!.Pid);
            Assert.Null(row.RestartAttempts);
        }

        [Fact]
        public async Task ServiceProcess_AskingToRefreshTheDacl_IsRefused()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            _host.StartListening();

            // Act & Assert
            Assert.False(await Client().RefreshPipeAccessAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Administrator_RefreshesTheDacl_TheNewServiceAccountIsGranted()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var localService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            _host.StartListening();
            var before = _host.CurrentPipeSecurity!.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().Any(r => localService.Equals(r.IdentityReference));
            await _repository.AddAsync(new ServiceDto { Name = "LocalServiceApp", ExecutablePath = "C:\\a.exe", RunAsLocalSystem = false, UserAccount = @"NT AUTHORITY\LocalService" }, ct);
            _identifier.IsAdministrator = true;

            // Act
            var refreshed = await Client().RefreshPipeAccessAsync(ct);

            // Assert
            Assert.False(before);
            Assert.True(refreshed);
            var rule = Assert.Single(_host.CurrentPipeSecurity!.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>(), r => localService.Equals(r.IdentityReference));
            Assert.Equal(0, (int)(rule.PipeAccessRights & PipeAccessRights.CreateNewInstance));

            // ... and the listener still serves after recycling its waiting instance
            _identifier.IsAdministrator = false;
            await SeedAsync(ServiceName);
            Assert.NotNull(await Task.Run(() => Client().GetByName(ServiceName, ct), ct));
        }

        [Fact]
        public async Task ManyClientsAtOnce_AreAllServed()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            var ct = TestContext.Current.CancellationToken;
            await SeedAsync(ServiceName);
            _host.StartListening();

            // Act
            var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => Client(requestTimeoutMs: 20000, connectTimeoutMs: 20000).GetByName(ServiceName, ct), ct)));

            // Assert
            Assert.All(results, r => Assert.Equal(ServiceName, r!.Name));
        }

        [Fact]
        public async Task ClientThatNeverSendsItsRequest_DoesNotBlockTheOthers()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            var ct = TestContext.Current.CancellationToken;
            await SeedAsync(ServiceName);
            _host.RequestTimeoutMs = 1500;
            _host.StartListening();

            using (var silent = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await silent.ConnectAsync(3000, ct);

                // Act: a real request while the silent client holds its connection
                var config = await Task.Run(() => Client().GetByName(ServiceName, ct), ct);

                // Assert
                Assert.Equal(ServiceName, config!.Name);

                // The silent connection is closed by the host once its request timeout expires
                var buffer = new byte[1];
                var read = await silent.ReadAsync(buffer, 0, 1, ct).WaitAsync(TimeSpan.FromSeconds(10), ct);
                Assert.Equal(0, read);
            }
        }

        [Fact]
        public async Task GarbageFrame_IsDroppedAndTheListenerKeepsServing()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            var ct = TestContext.Current.CancellationToken;
            await SeedAsync(ServiceName);
            _host.StartListening();

            using (var rogue = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await rogue.ConnectAsync(3000, ct);
                var garbage = BitConverter.GetBytes(int.MaxValue).Concat(new byte[] { 1, 2, 3 }).ToArray();
                try
                {
                    await rogue.WriteAsync(garbage, 0, garbage.Length, ct);
                    await rogue.FlushAsync(ct);
                }
                catch (IOException)
                {
                    // The host may already have dropped the connection after reading the oversized length prefix
                }
            }

            // Act
            var config = await Task.Run(() => Client().GetByName(ServiceName, ct), ct);

            // Assert
            Assert.Equal(ServiceName, config!.Name);
        }

        [Fact]
        public async Task StopListening_NoMoreConnectionsAreAccepted()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            var ct = TestContext.Current.CancellationToken;
            await SeedAsync(ServiceName);
            _host.StartListening();
            Assert.NotNull(await Task.Run(() => Client().GetByName(ServiceName, ct), ct));

            // Act
            _host.StopListening();

            // Assert
            var shortClient = new NamedPipesService(_pipeName, new ServyHostServerVerifier(_api.Object), connectTimeoutMs: 300, requestTimeoutMs: 1000);
            await Assert.ThrowsAsync<TimeoutException>(() => Task.Run(() => shortClient.GetByName(ServiceName, ct), ct));
        }

        [Fact]
        public void PipeSecurity_AfterStart_IsTheProtectedHostDacl()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Act
            _host.StartListening();

            // Assert
            var security = _host.CurrentPipeSecurity!;
            Assert.True(security.AreAccessRulesProtected);
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
            Assert.DoesNotContain(rules, r => r.AccessControlType == AccessControlType.Deny);
            Assert.DoesNotContain(rules, r => new SecurityIdentifier(WellKnownSidType.WorldSid, null).Equals(r.IdentityReference));
            Assert.DoesNotContain(rules, r => new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null).Equals(r.IdentityReference));
        }

        [Fact]
        public async Task Administrator_RefreshesTheDacl_ThePipeItselfCarriesTheNewGrant()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var localService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            var created = TrackCreatedInstances();
            _host.StartListening();
            await _repository.AddAsync(new ServiceDto { Name = "LocalServiceApp", ExecutablePath = "C:\\a.exe", RunAsLocalSystem = false, UserAccount = @"NT AUTHORITY\LocalService" }, ct);
            _identifier.IsAdministrator = true;

            // Act
            Assert.True(await Client().RefreshPipeAccessAsync(ct));
            await WaitForInstanceCreatedWithAsync(created, localService, ct);

            // Assert: the security descriptor the kernel checks a client against, not the host's in-memory copy. Every
            // instance of a pipe name shares the descriptor of the first one, so without writing it through the handle
            // the grant never reached the pipe until the host restarted (#7330).
            var kernel = KernelRules(created);
            var rule = Assert.Single(kernel, r => localService.Equals(r.IdentityReference));
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
            Assert.Equal(0, (int)(rule.PipeAccessRights & PipeAccessRights.CreateNewInstance));
        }

        [Fact]
        public async Task Administrator_RefreshesTheDacl_AfterTheLastServiceOfAnAccountIsRemoved_ThePipeRevokesIt()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var localService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            var created = TrackCreatedInstances();
            await _repository.AddAsync(new ServiceDto { Name = "LocalServiceApp", ExecutablePath = "C:\\a.exe", RunAsLocalSystem = false, UserAccount = @"NT AUTHORITY\LocalService" }, ct);
            _host.StartListening();
            await WaitForInstanceCreatedWithAsync(created, localService, ct);
            Assert.Contains(KernelRules(created), r => localService.Equals(r.IdentityReference));
            await _repository.DeleteAsync("LocalServiceApp", ct);
            _identifier.IsAdministrator = true;
            int before;
            lock (created) before = created.Count;

            // Act
            Assert.True(await Client().RefreshPipeAccessAsync(ct));

            // Assert: once an instance was created after the refresh, the pipe no longer grants Local Service anything
            var recreated = false;
            for (var i = 0; i < 200 && !recreated; i++)
            {
                lock (created)
                    recreated = created.Skip(before).Any(c => !c.Requested.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().Any(r => localService.Equals(r.IdentityReference)));
                if (!recreated) await Task.Delay(25, ct);
            }
            Assert.True(recreated);
            Assert.DoesNotContain(KernelRules(created), r => localService.Equals(r.IdentityReference));
        }

        [Fact]
        public async Task RealAccounts_GrantedAfterTheHostStarted_ConnectWithTheirOwnToken_AndAreRefusedOnceRevoked()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange: the order of a real installation - the host is already running, then services are installed under
            // these accounts and the installer asks the host to refresh. Each client runs as a scheduled task under the
            // account itself, so the access check is made against that account's real token (not an administrator's).
            var ct = TestContext.Current.CancellationToken;
            using var sandbox = new AccountSandbox(_pipeName);
            _host.StartListening();

            var accounts = new[]
            {
                (Stored: ServiceAccounts.NetworkService, RunAs: "NT AUTHORITY\\NETWORKSERVICE", Password: (string?)null),
                (Stored: ServiceAccounts.LocalService, RunAs: "NT AUTHORITY\\LOCALSERVICE", Password: (string?)null),
                (Stored: ".\\" + sandbox.LocalUser, RunAs: Environment.MachineName + "\\" + sandbox.LocalUser, Password: (string?)sandbox.LocalUserPassword),
            };

            // Not granted yet: refused by the pipe's DACL
            foreach (var account in accounts)
                Assert.StartsWith("ERROR UnauthorizedAccessException", await sandbox.ConnectAsAsync(account.RunAs, account.Password, ct));

            // Act 1: install
            var index = 0;
            foreach (var account in accounts)
                await _repository.AddAsync(new ServiceDto { Name = "Svc" + index++, ExecutablePath = "C:\\a.exe", RunAsLocalSystem = false, UserAccount = account.Stored }, ct);
            _identifier.IsAdministrator = true;
            Assert.True(await Client().RefreshPipeAccessAsync(ct));

            // Assert 1
            foreach (var account in accounts)
                Assert.Equal("CONNECTED", await sandbox.ConnectAsAsync(account.RunAs, account.Password, ct));
            Assert.Equal("CONNECTED", await sandbox.ConnectAsAsync("SYSTEM", null, ct));

            // Act 2: uninstall
            for (var i = 0; i < accounts.Length; i++)
                await _repository.DeleteAsync("Svc" + i, ct);
            Assert.True(await Client().RefreshPipeAccessAsync(ct));

            // Assert 2
            foreach (var account in accounts)
                Assert.StartsWith("ERROR UnauthorizedAccessException", await sandbox.ConnectAsAsync(account.RunAs, account.Password, ct));
        }

        [Fact]
        public async Task RunningServiceProcess_ItsRealAccountIsGranted_WhileTheServiceNowNamesAnother()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange: the SCM reports this test process as the service's process, and the service is configured for
            // LocalService now, as after a reinstall under another account that has not been followed by a restart
            var ct = TestContext.Current.CancellationToken;
            var me = WindowsIdentity.GetCurrent().User!;
            await _repository.AddAsync(new ServiceDto { Name = ServiceName, ExecutablePath = "C:\\a.exe", RunAsLocalSystem = false, UserAccount = @"NT AUTHORITY\LocalService" }, ct);

            // Act: the real token of the running process is read
            _host.StartListening();

            // Assert
            var rules = _host.CurrentPipeSecurity!.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
            Assert.Contains(rules, r => me.Equals(r.IdentityReference) && r.AccessControlType == AccessControlType.Allow);
            Assert.Contains(rules, r => new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null).Equals(r.IdentityReference));
        }

        [Fact]
        public async Task NetworkLogon_OfAGrantedAccount_ConnectsOnceGranted()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange: a real network logon (LOGON32_LOGON_NETWORK) of a local user, so the token carries
            // NT AUTHORITY\NETWORK - what a local process started over WinRM or PowerShell remoting runs with (#7330)
            var ct = TestContext.Current.CancellationToken;
            using var sandbox = new AccountSandbox(_pipeName);
            using var token = sandbox.NetworkLogon();
            using (var identity = new WindowsIdentity(token.DangerousGetHandle()))
                Assert.Contains(identity.Groups!, g => new SecurityIdentifier(WellKnownSidType.NetworkSid, null).Equals(g));
            _host.StartListening();

            // Act & Assert: refused while not granted, connects once its service is installed
            Assert.StartsWith("ERROR UnauthorizedAccessException", ConnectAs(token));
            await _repository.AddAsync(new ServiceDto { Name = "NetworkLogonApp", ExecutablePath = "C:\\a.exe", RunAsLocalSystem = false, UserAccount = ".\\" + sandbox.LocalUser }, ct);
            _identifier.IsAdministrator = true;
            Assert.True(await Client().RefreshPipeAccessAsync(ct));
            Assert.Equal("CONNECTED", ConnectAs(token));
        }

        [Fact]
        public async Task ClientOnAnotherComputer_ThroughSmb_CannotOpenThePipeEvenWhenGranted()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange: the test account is granted, and connects through \\<machine>\pipe\..., i.e. over SMB as a remote
            // client. The identifier is told to report an administrator, so nothing but the pipe's own remote-client
            // rejection can stop it.
            var ct = TestContext.Current.CancellationToken;
            await _repository.AddAsync(new ServiceDto { Name = ServiceName, ExecutablePath = "C:\\a.exe", Parameters = "--secret", RunAsLocalSystem = false, UserAccount = WindowsIdentity.GetCurrent().Name }, ct);
            _host.StartListening();
            _identifier.IsAdministrator = true;
            var frames = new NamedPipesService(_pipeName);

            // Act: the remote open is refused by the pipe itself, before any request can be sent
            var opened = false;
            var error = await Record.ExceptionAsync(async () =>
            {
                using (var remote = new NamedPipeClientStream(Environment.MachineName, _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification))
                {
                    await remote.ConnectAsync(10000, ct);
                    opened = true;
                    await frames.WriteAsync(remote, new IpcRequestDto { Action = AppConfig.ServyHostGetByNameAction, ServiceName = ServiceName }, ct);
                    await frames.ReadAsync<IpcResponseDto>(remote, ct);
                }
            });

            // Assert: never connected, never identified; the same account still connects locally
            Assert.NotNull(error);
            Assert.False(opened, "A client on another computer opened the pipe: " + error);
            Assert.Null(_identifier.LastWasRemote);
            _identifier.IsAdministrator = false;
            Assert.NotNull(await Task.Run(() => Client().GetByName(ServiceName, ct), ct));
        }

        [Fact]
        public async Task LocalClient_IsStillIdentifiedAsLocalAndServed_NowThatOnlyErrorPipeLocalMeansLocal()
        {
            Assert.SkipUnless(_isElevated, NotElevatedSkipReason);

            // Arrange: an ordinary local client, the one case for which GetNamedPipeClientComputerName fails with
            // ERROR_PIPE_LOCAL. Every other failure of that call is now read as "not proven local" and denied
            // (#7350), so this pins that the local path is still served.
            var ct = TestContext.Current.CancellationToken;
            await SeedAsync(ServiceName);
            _host.StartListening();

            // Act
            var config = await Task.Run(() => Client().GetByName(ServiceName, ct), ct);

            // Assert
            Assert.False(_identifier.LastWasRemote);
            Assert.NotNull(config);
            Assert.Equal("C:\\app.exe", config!.ExecutablePath);
        }

        /// <summary>
        /// Connects to the host's pipe while impersonating a token, and returns <c>CONNECTED</c> or <c>ERROR</c> and the exception.
        /// </summary>
        private string ConnectAs(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle token)
        {
            return WindowsIdentity.RunImpersonated(token, () =>
            {
                try
                {
                    using (var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut))
                        client.Connect(5000);
                    return "CONNECTED";
                }
                catch (Exception ex)
                {
                    return "ERROR " + ex.GetType().Name + ": " + ex.Message;
                }
            });
        }

        private List<(PipeSecurity Requested, NamedPipeServerStream Stream)> TrackCreatedInstances()
        {
            var created = new List<(PipeSecurity Requested, NamedPipeServerStream Stream)>();
            var inner = _host.ServerStreamFactory;
            _host.ServerStreamFactory = (name, security) =>
            {
                var stream = inner(name, security);
                lock (created) created.Add((security, stream));
                return stream;
            };
            return created;
        }

        private static async Task WaitForInstanceCreatedWithAsync(List<(PipeSecurity Requested, NamedPipeServerStream Stream)> created, SecurityIdentifier grantee, CancellationToken ct)
        {
            for (var i = 0; i < 200; i++)
            {
                lock (created)
                {
                    var match = created.Where(c => c.Requested.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().Any(r => grantee.Equals(r.IdentityReference)))
                        .Select(c => c.Stream).LastOrDefault();
                    if (match != null) return;
                }
                await Task.Delay(25, ct);
            }
            throw new TimeoutException("No pipe instance was created with the expected grant.");
        }

        /// <summary>
        /// Reads the pipe's security descriptor through the newest instance that is still open: every instance of a pipe
        /// name shares one descriptor, and the older ones may have served a client and been closed already.
        /// </summary>
        private static List<PipeAccessRule> KernelRules(List<(PipeSecurity Requested, NamedPipeServerStream Stream)> created)
        {
            List<NamedPipeServerStream> streams;
            lock (created) streams = created.Select(c => c.Stream).Reverse().ToList();

            foreach (var stream in streams)
            {
                try
                {
                    return stream.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
                }
                catch (ObjectDisposedException)
                {
                    // Served a client and closed; the next one shares the same descriptor
                }
            }

            throw new InvalidOperationException("No pipe instance is open.");
        }

        #region Test doubles

        /// <summary>
        /// Runs a named pipe client as another account through a scheduled task, and owns the local user, the tasks and
        /// the folder it needs; everything is removed on dispose.
        /// </summary>
        private sealed class AccountSandbox : IDisposable
        {
            private readonly string _pipeName;
            private readonly string _directory;
            private readonly string _taskPrefix = "ServyPipeTest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            private readonly List<string> _tasks = new List<string>();
            private int _run;

            public AccountSandbox(string pipeName)
            {
                _pipeName = pipeName;
                LocalUser = "svypt" + Guid.NewGuid().ToString("N").Substring(0, 10);
                // net.exe asks for confirmation, and so fails without a console, for a password longer than 14 characters
                LocalUserPassword = "Aa1!" + Guid.NewGuid().ToString("N").Substring(0, 10);

                _directory = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "ServyPipeTests", _taskPrefix);
                Directory.CreateDirectory(_directory);
                var security = new DirectorySecurity();
                security.SetAccessRuleProtection(true, false);
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                new DirectoryInfo(_directory).SetAccessControl(security);

                // A plain local user. Performance Log Users holds "Log on as a batch job" by default, which a scheduled
                // task needs, and grants nothing on the pipe.
                Run("net.exe", $"user {LocalUser} {LocalUserPassword} /add");
                Run("net.exe", $"localgroup \"Performance Log Users\" {LocalUser} /add");
            }

            public string LocalUser { get; }

            public string LocalUserPassword { get; }

            /// <summary>
            /// Logs the local user on with a network logon, whose token carries <c>NT AUTHORITY\NETWORK</c>.
            /// </summary>
            public Microsoft.Win32.SafeHandles.SafeAccessTokenHandle NetworkLogon()
            {
                const int Logon32LogonNetwork = 3;
                const int Logon32ProviderDefault = 0;
                Assert.True(LogonUser(LocalUser, ".", LocalUserPassword, Logon32LogonNetwork, Logon32ProviderDefault, out var token),
                    $"LogonUser (network) failed: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                return token;
            }

            [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            private static extern bool LogonUser(string userName, string domain, string password, int logonType, int logonProvider, out Microsoft.Win32.SafeHandles.SafeAccessTokenHandle token);

            /// <summary>
            /// Connects to the pipe as the account and returns <c>CONNECTED</c>, or <c>ERROR</c> and the exception.
            /// </summary>
            public async Task<string> ConnectAsAsync(string runAs, string? password, CancellationToken ct)
            {
                var run = ++_run;
                var output = Path.Combine(_directory, $"out{run}.txt");
                var script = Path.Combine(_directory, $"c{run}.ps1");
                File.WriteAllText(script,
                    "$out = '" + output + "'\r\n" +
                    "try {\r\n" +
                    "  $c = New-Object System.IO.Pipes.NamedPipeClientStream('.', '" + _pipeName + "', [System.IO.Pipes.PipeDirection]::InOut)\r\n" +
                    "  $c.Connect(5000)\r\n" +
                    "  $c.Dispose()\r\n" +
                    "  Set-Content -LiteralPath $out -Value 'CONNECTED'\r\n" +
                    "} catch {\r\n" +
                    "  $e = $_.Exception; while ($e.InnerException) { $e = $e.InnerException }\r\n" +
                    "  Set-Content -LiteralPath $out -Value ('ERROR ' + $e.GetType().Name + ': ' + $e.Message + ' as ' + [Security.Principal.WindowsIdentity]::GetCurrent().Name)\r\n" +
                    "}\r\n");

                var task = $"{_taskPrefix}_{run}";
                _tasks.Add(task);
                var command = $"powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File {script}";
                Run("schtasks.exe", $"/Create /F /TN {task} /SC ONCE /ST 23:59 /RU \"{runAs}\"" + (password == null ? "" : $" /RP \"{password}\"") + $" /TR \"{command}\"");
                Run("schtasks.exe", $"/Run /TN {task}");

                for (var i = 0; i < 240; i++)
                {
                    if (File.Exists(output))
                    {
                        try
                        {
                            var text = File.ReadAllText(output).Trim();
                            if (text.Length > 0) return text;
                        }
                        catch (IOException)
                        {
                            // Still being written
                        }
                    }
                    await Task.Delay(250, ct);
                }

                return $"TIMEOUT: the scheduled task as '{runAs}' wrote nothing in 60 s";
            }

            public void Dispose()
            {
                foreach (var task in _tasks)
                    TryRun("schtasks.exe", $"/Delete /F /TN {task}");
                TryRun("net.exe", $"user {LocalUser} /delete");
                try { Directory.Delete(_directory, recursive: true); } catch { /* best effort */ }
            }

            private static void Run(string file, string arguments)
            {
                var (exitCode, output) = Execute(file, arguments);
                Assert.True(exitCode == 0, $"{file} {arguments} failed ({exitCode}): {output}");
            }

            private static void TryRun(string file, string arguments)
            {
                try { Execute(file, arguments); } catch { /* best effort */ }
            }

            private static (int ExitCode, string Output) Execute(string file, string arguments)
            {
                var psi = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory, file), arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (var process = System.Diagnostics.Process.Start(psi)!)
                {
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    return (process.ExitCode, stdout.Result + stderr.Result);
                }
            }
        }


        /// <summary>
        /// Wraps the production <see cref="PipeCallerIdentifier"/> and replaces its administrator answer, so the tests
        /// can play a service account (or an administrator) from an elevated test process.
        /// </summary>
        private sealed class SwitchableCallerIdentifier : IPipeCallerIdentifier
        {
            private readonly PipeCallerIdentifier _inner = new PipeCallerIdentifier();

            public bool IsAdministrator { get; set; }

            public PipeCaller Identify(NamedPipeServerStream connectedServer)
            {
                var identified = _inner.Identify(connectedServer);
                LastWasRemote = identified.IsRemote;
                return new PipeCaller(identified.ProcessId, IsAdministrator, identified.IsRemote);
            }

            public bool? LastWasRemote { get; private set; }
        }

        /// <summary>
        /// A shared in-memory SQLite database kept alive by a master connection, migrated by the production initializer.
        /// </summary>
        private sealed class InMemoryDbContext : IAppDbContext, IDisposable
        {
            private readonly string _connectionString = $"Data Source=ServyHostTests_{Guid.NewGuid():N};Mode=Memory;Cache=Shared;";
            private readonly SQLiteConnection _master;

            public InMemoryDbContext()
            {
                _master = new SQLiteConnection(_connectionString);
                _master.Open();
            }

            public void Initialize() => SQLiteDbInitializer.Initialize(_master);

            public DbConnection CreateConnection() => new SQLiteConnection(_connectionString);

            public void Dispose() => _master.Dispose();
        }

        /// <summary>
        /// Stores secrets as is, so the tests can read what the host decrypted and what it left in the database.
        /// </summary>
        private sealed class PassThroughSecureData : ISecureData
        {
            public string Encrypt(string plainText) => plainText;

            public string Decrypt(string cipherText) => cipherText;

            public void Dispose()
            {
            }
        }

        #endregion
    }
}
