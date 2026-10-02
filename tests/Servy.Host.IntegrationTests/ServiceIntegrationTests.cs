using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using System;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

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
            _api.Setup(a => a.GetServiceProcessId(AppConfig.ServyHostServiceName)).Returns(Process.GetCurrentProcess().Id);
            _api.Setup(a => a.GetServiceProcessId(ServiceName)).Returns(Process.GetCurrentProcess().Id);
            _api.Setup(a => a.GetServiceProcessId("OtherService")).Returns(Process.GetCurrentProcess().Id + 100);

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
            => await _repository.AddAsync(new ServiceDto { Name = name, ExecutablePath = "C:\\app.exe", Parameters = "--secret", Password = "pwd" }, CancellationToken.None);

        [Fact]
        public async Task ServiceProcess_ReadsItsConfigurationAndWritesItsRuntimeStateAndCounter()
        {
            if (!(_isElevated)) return; // NotElevatedSkipReason

            // Arrange
            var ct = CancellationToken.None;
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
            Assert.Equal("C:\\app.exe", config.ExecutablePath);
            Assert.Equal("--secret", config.Parameters);
            Assert.Null(config.Password);

            // ... the runtime state and the counter reached the database
            Assert.Equal(1, updated);
            Assert.Equal(1, written);
            Assert.Equal(2, attempts.Attempts);
            Assert.NotNull(attempts.UpdatedAtUtc);
            Assert.True(attempts.UpdatedAtUtc > DateTime.UtcNow.AddMinutes(-5));
            var row = await _repository.GetByNameAsync(ServiceName, decrypt: true, ct);
            Assert.Equal(31337, row.Pid);
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
            if (!(_isElevated)) return; // NotElevatedSkipReason

            // Arrange
            var ct = CancellationToken.None;
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
            Assert.Null(row.Pid);
            Assert.Null(row.RestartAttempts);
        }

        [Fact]
        public async Task ServiceProcess_AskingToRefreshTheDacl_IsRefused()
        {
            if (!(_isElevated)) return; // NotElevatedSkipReason

            // Arrange
            _host.StartListening();

            // Act & Assert
            Assert.False(await Client().RefreshPipeAccessAsync(CancellationToken.None));
        }

        [Fact]
        public async Task Administrator_RefreshesTheDacl_TheNewServiceAccountIsGranted()
        {
            if (!(_isElevated)) return; // NotElevatedSkipReason

            // Arrange
            var ct = CancellationToken.None;
            var localService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            _host.StartListening();
            var before = _host.CurrentPipeSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().Any(r => localService.Equals(r.IdentityReference));
            await _repository.AddAsync(new ServiceDto { Name = "LocalServiceApp", ExecutablePath = "C:\\a.exe", RunAsLocalSystem = false, UserAccount = @"NT AUTHORITY\LocalService" }, ct);
            _identifier.IsAdministrator = true;

            // Act
            var refreshed = await Client().RefreshPipeAccessAsync(ct);

            // Assert
            Assert.False(before);
            Assert.True(refreshed);
            var rule = Assert.Single(_host.CurrentPipeSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>(), r => localService.Equals(r.IdentityReference));
            Assert.Equal(0, (int)(rule.PipeAccessRights & PipeAccessRights.CreateNewInstance));

            // ... and the listener still serves after recycling its waiting instance
            _identifier.IsAdministrator = false;
            await SeedAsync(ServiceName);
            Assert.NotNull(await Task.Run(() => Client().GetByName(ServiceName, ct), ct));
        }

        [Fact]
        public async Task ManyClientsAtOnce_AreAllServed()
        {
            if (!(_isElevated)) return; // NotElevatedSkipReason

            // Arrange
            var ct = CancellationToken.None;
            await SeedAsync(ServiceName);
            _host.StartListening();

            // Act
            var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => Client(requestTimeoutMs: 20000, connectTimeoutMs: 20000).GetByName(ServiceName, ct), ct)));

            // Assert
            Assert.All(results, r => Assert.Equal(ServiceName, r.Name));
        }

        [Fact]
        public async Task ClientThatNeverSendsItsRequest_DoesNotBlockTheOthers()
        {
            if (!(_isElevated)) return; // NotElevatedSkipReason

            // Arrange
            var ct = CancellationToken.None;
            await SeedAsync(ServiceName);
            _host.RequestTimeoutMs = 1500;
            _host.StartListening();

            using (var silent = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await silent.ConnectAsync(3000, ct);

                // Act: a real request while the silent client holds its connection
                var config = await Task.Run(() => Client().GetByName(ServiceName, ct), ct);

                // Assert
                Assert.Equal(ServiceName, config.Name);

                // The silent connection is closed by the host once its request timeout expires
                var buffer = new byte[1];
                var reading = silent.ReadAsync(buffer, 0, 1, ct);
                Assert.Same(reading, await Task.WhenAny(reading, Task.Delay(TimeSpan.FromSeconds(10), ct)));
                Assert.Equal(0, await reading);
            }
        }

        [Fact]
        public async Task GarbageFrame_IsDroppedAndTheListenerKeepsServing()
        {
            if (!(_isElevated)) return; // NotElevatedSkipReason

            // Arrange
            var ct = CancellationToken.None;
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
            Assert.Equal(ServiceName, config.Name);
        }

        [Fact]
        public async Task StopListening_NoMoreConnectionsAreAccepted()
        {
            if (!(_isElevated)) return; // NotElevatedSkipReason

            // Arrange
            var ct = CancellationToken.None;
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
            if (!(_isElevated)) return; // NotElevatedSkipReason

            // Act
            _host.StartListening();

            // Assert
            var security = _host.CurrentPipeSecurity;
            Assert.True(security.AreAccessRulesProtected);
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
            Assert.Contains(rules, r => new SecurityIdentifier(WellKnownSidType.NetworkSid, null).Equals(r.IdentityReference) && r.AccessControlType == AccessControlType.Deny);
            Assert.DoesNotContain(rules, r => new SecurityIdentifier(WellKnownSidType.WorldSid, null).Equals(r.IdentityReference));
            Assert.DoesNotContain(rules, r => new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null).Equals(r.IdentityReference));
        }

        #region Test doubles

        /// <summary>
        /// Wraps the production <see cref="PipeCallerIdentifier"/> and replaces its administrator answer, so the tests
        /// can play a service account (or an administrator) from an elevated test process.
        /// </summary>
        private sealed class SwitchableCallerIdentifier : IPipeCallerIdentifier
        {
            private readonly PipeCallerIdentifier _inner = new PipeCallerIdentifier();

            public bool IsAdministrator { get; set; }

            public PipeCaller Identify(NamedPipeServerStream connectedServer)
                => new PipeCaller(_inner.Identify(connectedServer).ProcessId, IsAdministrator);
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
