using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Host.UnitTests
{
    /// <summary>
    /// Unit tests for the request handling of the Servy host service: who may ask for what, what each action reads and
    /// writes, and how the named pipe's DACL is rebuilt from the accounts of the installed services.
    /// </summary>
    public class ServiceTests : IDisposable
    {
        private const string ServiceName = "MyApp";
        private const int ServicePid = 4242;

        private readonly Mock<IServyLogger> _logger = new Mock<IServyLogger>();
        private readonly Mock<INamedPipesService> _pipes = new Mock<INamedPipesService>();
        private readonly Mock<IServiceRepository> _repository = new Mock<IServiceRepository>();
        private readonly Mock<IWindowsServiceApi> _api = new Mock<IWindowsServiceApi>();
        private readonly Mock<IPipeCallerIdentifier> _identifier = new Mock<IPipeCallerIdentifier>();
        private readonly Service _sut;

        private static readonly PipeCaller TheServiceProcess = new PipeCaller(ServicePid, isAdministrator: false);
        private static readonly PipeCaller AnotherProcess = new PipeCaller(9999, isAdministrator: false);
        private static readonly PipeCaller Administrator = new PipeCaller(1111, isAdministrator: true);
        private static readonly PipeCaller RemoteAdministratorWithTheServicePid = new PipeCaller(ServicePid, isAdministrator: true, isRemote: true);

        public ServiceTests()
        {
            _api.Setup(a => a.GetServiceProcessId(ServiceName)).Returns(ServicePid);
            _repository.Setup(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(Enumerable.Empty<ServiceDto>());
            _sut = new Service(_logger.Object, _pipes.Object, _repository.Object, _api.Object, _identifier.Object);

            // A fake PID can be a real process on the machine running the tests: never read its token
            _sut.ResolveProcessAccount = _ => null;
        }

        public void Dispose() => _sut.Dispose();

        private static IpcRequestDto Request(string action, string serviceName = ServiceName) => new IpcRequestDto { Action = action, ServiceName = serviceName };

        #region Remote clients

        [Theory]
        [InlineData(AppConfig.ServyHostGetByNameAction)]
        [InlineData(AppConfig.ServyHostGetRestartAttemptsAction)]
        [InlineData(AppConfig.ServyHostRefreshPipeAccessAction)]
        public async Task ProcessRequestAsync_ClientOnAnotherComputer_IsRefusedEvenAsAnAdministratorWithTheServicePid(string action)
        {
            // Act: a remote PID is a process on another computer, so even a match with the SCM's PID means nothing
            var response = await _sut.ProcessRequestAsync(Request(action), RemoteAdministratorWithTheServicePid, CancellationToken.None);

            // Assert
            Assert.False(response.Success);
            Assert.Null(response.Data);
            _repository.Verify(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            _repository.Verify(r => r.GetRestartAttemptsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _repository.Verify(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Constructor

        [Fact]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new Service(null, _pipes.Object, _repository.Object, _api.Object, _identifier.Object));
            Assert.Throws<ArgumentNullException>(() => new Service(_logger.Object, null, _repository.Object, _api.Object, _identifier.Object));
            Assert.Throws<ArgumentNullException>(() => new Service(_logger.Object, _pipes.Object, (IServiceRepository)null, _api.Object, _identifier.Object));
            Assert.Throws<ArgumentNullException>(() => new Service(_logger.Object, _pipes.Object, _repository.Object, null, _identifier.Object));
            Assert.Throws<ArgumentNullException>(() => new Service(_logger.Object, _pipes.Object, _repository.Object, _api.Object, null));
        }

        [Fact]
        public void Constructor_RunsAsTheServyService()
        {
            Assert.Equal(AppConfig.ServyHostServiceName, _sut.ServiceName);
            Assert.Equal("Servy", _sut.ServiceName);
            Assert.Equal(AppConfig.ServyHostNamedPipeName, _sut.PipeName);
        }

        [Fact]
        public async Task ProcessRequestAsync_NullArguments_Throw()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.ProcessRequestAsync(null, TheServiceProcess, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.ProcessRequestAsync(new IpcRequestDto(), null, CancellationToken.None));
        }

        #endregion

        #region Authorization

        [Theory]
        [InlineData(AppConfig.ServyHostGetByNameAction)]
        [InlineData(AppConfig.ServyHostUpdateRuntimeStateAction)]
        [InlineData(AppConfig.ServyHostGetRestartAttemptsAction)]
        [InlineData(AppConfig.ServyHostUpdateRestartAttemptsAction)]
        public async Task ProcessRequestAsync_AnotherServicesProcess_IsRefusedWithoutReadingTheDatabase(string action)
        {
            // Arrange: a service account asks about a service it does not run
            var request = Request(action);
            request.RuntimeState = new ServiceRuntimeStateDto();
            request.RestartAttempts = 1;

            // Act
            var response = await _sut.ProcessRequestAsync(request, AnotherProcess, CancellationToken.None);

            // Assert
            Assert.False(response.Success);
            Assert.Equal("Access denied.", response.ErrorMessage);
            Assert.Null(response.Data);
            _repository.VerifyNoOtherCalls();
            _logger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Refused IPC request") && s.Contains("process 9999") && s.Contains(ServiceName)), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task ProcessRequestAsync_CallerWithoutAProcessId_IsRefusedWithoutAskingTheScm()
        {
            // Act
            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostGetByNameAction), new PipeCaller(0, false), CancellationToken.None);

            // Assert
            Assert.False(response.Success);
            _api.Verify(a => a.GetServiceProcessId(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ProcessRequestAsync_ServiceNotRunning_RefusesEvenAMatchingProcessIdOfZero()
        {
            // Arrange: the SCM reports 0 for a stopped service, which must never match a caller
            _api.Setup(a => a.GetServiceProcessId("Stopped")).Returns(0);

            // Act
            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostGetByNameAction, "Stopped"), new PipeCaller(0, false), CancellationToken.None);

            // Assert
            Assert.False(response.Success);
        }

        [Fact]
        public async Task ProcessRequestAsync_Administrator_MayAskAboutAnyService()
        {
            // Arrange
            _repository.Setup(r => r.GetByNameAsync("Other", true, It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = "Other" });

            // Act
            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostGetByNameAction, "Other"), Administrator, CancellationToken.None);

            // Assert
            Assert.True(response.Success);
            _api.Verify(a => a.GetServiceProcessId(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ProcessRequestAsync_RefreshFromAServiceAccount_IsRefused()
        {
            // Act
            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostRefreshPipeAccessAction, null), TheServiceProcess, CancellationToken.None);

            // Assert: only an administrator may change who can connect
            Assert.False(response.Success);
            Assert.Equal("Access denied.", response.ErrorMessage);
            _repository.Verify(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessRequestAsync_ActionAdmittedByTheGuardWithoutACase_FailsInTheDefaultAndTouchesNothing()
        {
            // Arrange: an action the guard admits but the switch has no case for, as when one is added to
            // IsKnownServiceAction and its case is forgotten
            _sut.IsKnownServiceActionCheck = _ => true;
            var request = new IpcRequestDto { Action = "FutureAction", ServiceName = ServiceName, RestartAttempts = 1, RuntimeState = new ServiceRuntimeStateDto() };

            // Act
            var response = await _sut.ProcessRequestAsync(request, Administrator, CancellationToken.None);

            // Assert: refused as unknown, and no other action's handler ran
            Assert.False(response.Success);
            Assert.Equal("Unknown IPC action: FutureAction", response.ErrorMessage);
            _repository.Verify(r => r.UpdateRestartAttemptsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
            _repository.Verify(r => r.UpdateRuntimeStateAsync(It.IsAny<string>(), It.IsAny<ServiceRuntimeStateDto>(), It.IsAny<CancellationToken>()), Times.Never);
            _repository.Verify(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            _repository.Verify(r => r.GetRestartAttemptsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        public static TheoryData<string> EveryServyHostAction()
        {
            var data = new TheoryData<string>();
            foreach (var field in typeof(AppConfig).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (field.IsLiteral && field.Name.StartsWith("ServyHost", StringComparison.Ordinal) && field.Name.EndsWith("Action", StringComparison.Ordinal))
                    data.Add((string)field.GetRawConstantValue());
            }
            return data;
        }

        /// <summary>
        /// The one repository call each declared action's case must make, keyed by action. An action declared later has
        /// no entry until its author says what its case does.
        /// </summary>
        private static readonly Dictionary<string, Action<Mock<IServiceRepository>>> ExpectedRepositoryCall = new Dictionary<string, Action<Mock<IServiceRepository>>>
        {
            [AppConfig.ServyHostGetByNameAction] = r => r.Verify(x => x.GetByNameAsync(ServiceName, true, It.IsAny<CancellationToken>()), Times.Once),
            [AppConfig.ServyHostUpdateRuntimeStateAction] = r => r.Verify(x => x.UpdateRuntimeStateAsync(ServiceName, It.IsAny<ServiceRuntimeStateDto>(), It.IsAny<CancellationToken>()), Times.Once),
            [AppConfig.ServyHostGetRestartAttemptsAction] = r => r.Verify(x => x.GetRestartAttemptsAsync(ServiceName, It.IsAny<CancellationToken>()), Times.Once),
            [AppConfig.ServyHostUpdateRestartAttemptsAction] = r => r.Verify(x => x.UpdateRestartAttemptsAsync(ServiceName, 1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once),
            [AppConfig.ServyHostRefreshPipeAccessAction] = r => r.Verify(x => x.GetAllAsync(false, It.IsAny<CancellationToken>()), Times.Once),
        };

        [Theory]
        [MemberData(nameof(EveryServyHostAction))]
        public async Task ProcessRequestAsync_EveryDeclaredAction_IsHandledByItsOwnCase(string action)
        {
            // Arrange: every AppConfig.ServyHost*Action constant, with what any of them needs
            var request = new IpcRequestDto
            {
                Action = action,
                ServiceName = ServiceName,
                RuntimeState = new ServiceRuntimeStateDto(),
                RestartAttempts = 1,
            };

            // Act
            var response = await _sut.ProcessRequestAsync(request, Administrator, CancellationToken.None);

            // Assert: it succeeded, through its own handler's repository call and no other handler's. An action declared
            // without a guard entry and a case of its own would end in "Unknown IPC action", one stacked onto another
            // action's case would make that action's call, and a case that does nothing would make none
            Assert.True(response.Success, $"'{action}': {response.ErrorMessage}");
            Assert.True(ExpectedRepositoryCall.ContainsKey(action), $"'{action}' is declared but the test does not say which repository call its case makes.");
            ExpectedRepositoryCall[action](_repository);
            _repository.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Update")]
        [InlineData("getbyname")]
        public async Task ProcessRequestAsync_UnknownAction_Fails(string action)
        {
            var response = await _sut.ProcessRequestAsync(new IpcRequestDto { Action = action, ServiceName = ServiceName }, Administrator, CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal($"Unknown IPC action: {action}", response.ErrorMessage);
            _repository.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ProcessRequestAsync_NoServiceName_Fails(string serviceName)
        {
            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostGetByNameAction, serviceName), Administrator, CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal("A service name is required.", response.ErrorMessage);
        }

        #endregion

        #region Actions

        [Fact]
        public async Task GetByName_TheServicesOwnProcess_GetsItsDecryptedConfigurationWithoutThePassword()
        {
            // Arrange
            _repository.Setup(r => r.GetByNameAsync(ServiceName, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = ServiceName, Parameters = "--token abc", Password = "p@ss", UserAccount = @".\svc" });

            // Act
            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostGetByNameAction), TheServiceProcess, CancellationToken.None);

            // Assert
            Assert.True(response.Success);
            Assert.Equal("--token abc", response.Data.Parameters);
            Assert.Null(response.Data.Password);
        }

        [Fact]
        public async Task GetByName_NoRow_SucceedsWithNoData()
        {
            _repository.Setup(r => r.GetByNameAsync(ServiceName, true, It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto)null);

            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostGetByNameAction), TheServiceProcess, CancellationToken.None);

            Assert.True(response.Success);
            Assert.Null(response.Data);
        }

        [Fact]
        public async Task UpdateRuntimeState_WritesOnlyTheRuntimeState()
        {
            // Arrange
            var state = new ServiceRuntimeStateDto { Pid = 77, ActiveStdoutPath = "o", UpdatePreviousStopTimeout = true, PreviousStopTimeout = 5 };
            _repository.Setup(r => r.UpdateRuntimeStateAsync(ServiceName, state, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            var request = Request(AppConfig.ServyHostUpdateRuntimeStateAction);
            request.RuntimeState = state;

            // Act
            var response = await _sut.ProcessRequestAsync(request, TheServiceProcess, CancellationToken.None);

            // Assert
            Assert.True(response.Success);
            Assert.Equal(1, response.UpdateData);
            _repository.Verify(r => r.UpdateRuntimeStateAsync(ServiceName, state, It.IsAny<CancellationToken>()), Times.Once);
            _repository.Verify(r => r.UpdateAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            _repository.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateRuntimeState_NoState_Fails()
        {
            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostUpdateRuntimeStateAction), TheServiceProcess, CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal("The runtime state is required.", response.ErrorMessage);
        }

        [Fact]
        public async Task GetRestartAttempts_ReturnsTheStoredCounter()
        {
            var when = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);
            _repository.Setup(r => r.GetRestartAttemptsAsync(ServiceName, It.IsAny<CancellationToken>())).ReturnsAsync(new RestartAttemptsDto { Attempts = 2, UpdatedAtUtc = when });

            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostGetRestartAttemptsAction), TheServiceProcess, CancellationToken.None);

            Assert.True(response.Success);
            Assert.Equal(2, response.RestartAttempts.Attempts);
            Assert.Equal(when, response.RestartAttempts.UpdatedAtUtc);
        }

        [Fact]
        public async Task GetRestartAttempts_NoRow_ReturnsZero()
        {
            _repository.Setup(r => r.GetRestartAttemptsAsync(ServiceName, It.IsAny<CancellationToken>())).ReturnsAsync((RestartAttemptsDto)null);

            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostGetRestartAttemptsAction), TheServiceProcess, CancellationToken.None);

            Assert.True(response.Success);
            Assert.Equal(0, response.RestartAttempts.Attempts);
            Assert.Null(response.RestartAttempts.UpdatedAtUtc);
        }

        [Fact]
        public async Task UpdateRestartAttempts_StampsTheWriteWithTheHostsClock()
        {
            // Arrange
            var now = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);
            _sut.UtcNow = () => now;
            _repository.Setup(r => r.UpdateRestartAttemptsAsync(ServiceName, 3, now, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            var request = Request(AppConfig.ServyHostUpdateRestartAttemptsAction);
            request.RestartAttempts = 3;

            // Act
            var response = await _sut.ProcessRequestAsync(request, TheServiceProcess, CancellationToken.None);

            // Assert
            Assert.True(response.Success);
            Assert.Equal(1, response.UpdateData);
            _repository.Verify(r => r.UpdateRestartAttemptsAsync(ServiceName, 3, now, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(-1)]
        public async Task UpdateRestartAttempts_MissingOrNegativeCounter_Fails(int? attempts)
        {
            var request = Request(AppConfig.ServyHostUpdateRestartAttemptsAction);
            request.RestartAttempts = attempts;

            var response = await _sut.ProcessRequestAsync(request, TheServiceProcess, CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal("A non-negative restart attempts counter is required.", response.ErrorMessage);
            _repository.Verify(r => r.UpdateRestartAttemptsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessRequestAsync_RepositoryThrows_FailsAndLogs()
        {
            // Arrange
            var failure = new InvalidOperationException("database is locked");
            _repository.Setup(r => r.GetByNameAsync(ServiceName, true, It.IsAny<CancellationToken>())).ThrowsAsync(failure);

            // Act
            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostGetByNameAction), TheServiceProcess, CancellationToken.None);

            // Assert
            Assert.False(response.Success);
            Assert.Equal("The request failed: database is locked", response.ErrorMessage);
            _logger.Verify(l => l.Error(It.Is<string>(s => s.Contains("IPC request 'GetByName' for 'MyApp' failed.")), failure), Times.Once);
        }

        [Fact]
        public async Task ProcessRequestAsync_Cancelled_Propagates()
        {
            _repository.Setup(r => r.GetByNameAsync(ServiceName, true, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

            await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.ProcessRequestAsync(Request(AppConfig.ServyHostGetByNameAction), TheServiceProcess, CancellationToken.None));
        }

        #endregion

        #region Pipe DACL

        [Fact]
        public async Task RefreshPipeAccess_Administrator_RebuildsTheDaclFromTheServiceAccounts()
        {
            // Arrange
            var localService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            var networkService = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
            _repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
            {
                new ServiceDto { Name = "a", RunAsLocalSystem = false, UserAccount = "svc-a" },
                new ServiceDto { Name = "b", RunAsLocalSystem = false, UserAccount = "SVC-A" },
                new ServiceDto { Name = "c", RunAsLocalSystem = true },
                new ServiceDto { Name = "d", RunAsLocalSystem = false, UserAccount = "svc-d" },
            });
            _sut.ResolveAccount = a => a.Equals("svc-a", StringComparison.OrdinalIgnoreCase) ? localService : networkService;

            // Act
            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostRefreshPipeAccessAction, null), Administrator, CancellationToken.None);

            // Assert
            Assert.True(response.Success);
            var rules = _sut.CurrentPipeSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
            Assert.Single(rules, r => localService.Equals(r.IdentityReference));
            Assert.Single(rules, r => networkService.Equals(r.IdentityReference));
            _repository.Verify(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_AccountNotResolvableYet_IsRetriedUntilItResolves()
        {
            // Arrange: a gMSA whose domain controller is not reachable for the first two lookups, as at boot
            var localService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            var lookups = 0;
            _sut.ResolveAccount = _ => Interlocked.Increment(ref lookups) < 3 ? null : localService;
            _sut.UnresolvedAccountRetryDelayMs = 10;
            _sut.ServerStreamFactory = (name, security) => throw new IOException("All pipe instances are busy.");
            _repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ServiceDto { Name = "a", RunAsLocalSystem = false, UserAccount = @"CONTOSO\svc-gmsa$" } });

            // Act
            _sut.StartListening();
            var granted = false;
            for (var i = 0; i < 500 && !granted; i++)
            {
                granted = _sut.CurrentPipeSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().Any(r => localService.Equals(r.IdentityReference));
                if (!granted) await Task.Delay(10, CancellationToken.None);
            }
            await Task.Delay(100, CancellationToken.None);
            _sut.StopListening();

            // Assert: granted on the third lookup, and no retry once it resolved
            Assert.True(granted);
            Assert.Equal(3, Volatile.Read(ref lookups));
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_AccountNeverResolves_StopsRetryingAfterTheConfiguredCount()
        {
            // Arrange
            var lookups = 0;
            _sut.ResolveAccount = _ => { Interlocked.Increment(ref lookups); return null; };
            _sut.UnresolvedAccountRetryDelayMs = 1;
            _sut.ServerStreamFactory = (name, security) => throw new IOException("All pipe instances are busy.");
            _repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ServiceDto { Name = "a", RunAsLocalSystem = false, UserAccount = @"CONTOSO\deleted-user" } });

            // Act
            _sut.StartListening();
            var last = -1;
            for (var i = 0; i < 100 && last != Volatile.Read(ref lookups); i++)
            {
                last = Volatile.Read(ref lookups);
                await Task.Delay(100, CancellationToken.None);
            }
            _sut.StopListening();

            // Assert: the first build plus the configured retries, then nothing until the next refresh
            Assert.Equal(1 + AppConfig.ServyHostUnresolvedAccountRetryCount, Volatile.Read(ref lookups));
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_ServiceStillRunningUnderItsFormerAccount_KeepsItUntilThatProcessIsGone()
        {
            // Arrange: reinstalled from NetworkService to LocalService while running (#7330); the old process still runs
            var localService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            var networkService = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
            var running = networkService;
            var gate = new object();
            _repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ServiceDto { Name = ServiceName, RunAsLocalSystem = false, UserAccount = @"NT AUTHORITY\LocalService" } });
            _sut.ResolveProcessAccount = pid => { lock (gate) return pid == ServicePid ? running : null; };
            _sut.UnresolvedAccountRetryDelayMs = 10;
            _sut.ServerStreamFactory = (name, security) => throw new IOException("All pipe instances are busy.");
            bool Granted(SecurityIdentifier sid) => _sut.CurrentPipeSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().Any(r => sid.Equals(r.IdentityReference));

            // Act 1: the old process can still report its PID when it stops
            _sut.StartListening();
            var bothWhileOldRuns = Granted(localService) && Granted(networkService);

            // Act 2: the service restarts under LocalService
            lock (gate) running = localService;
            var dropped = false;
            for (var i = 0; i < 500 && !dropped; i++)
            {
                dropped = !Granted(networkService);
                if (!dropped) await Task.Delay(10, CancellationToken.None);
            }
            _sut.StopListening();

            // Assert
            Assert.True(bothWhileOldRuns);
            Assert.True(dropped);
            Assert.True(Granted(localService));
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_FormerAccountsLinger_LogsTheLingeringLineOnlyWhenTheirSetChanges()
        {
            // Arrange: two services reinstalled under LocalService while their old processes still run under other accounts,
            // each rechecked every millisecond (#7362)
            const string OtherService = "OtherApp";
            const int OtherPid = 4343;
            var localService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            var networkService = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
            var formerUser = new SecurityIdentifier("S-1-5-21-1000-2000-3000-1001");
            var running = new Dictionary<int, SecurityIdentifier> { [ServicePid] = networkService, [OtherPid] = formerUser };
            var gate = new object();
            var refreshes = 0;
            var ended = 0;
            _api.Setup(a => a.GetServiceProcessId(OtherService)).Returns(OtherPid);
            _repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .Callback(() => Interlocked.Increment(ref refreshes))
                .ReturnsAsync(new[]
                {
                    new ServiceDto { Name = ServiceName, RunAsLocalSystem = false, UserAccount = @"NT AUTHORITY\LocalService" },
                    new ServiceDto { Name = OtherService, RunAsLocalSystem = false, UserAccount = @"NT AUTHORITY\LocalService" },
                });
            _logger.Setup(l => l.Info(It.Is<string>(m => m.Contains("No account keeps access")), It.IsAny<Exception>()))
                .Callback(() => Interlocked.Increment(ref ended));
            _sut.ResolveAccount = _ => localService;
            _sut.ResolveProcessAccount = pid => { lock (gate) return running.ContainsKey(pid) ? running[pid] : null; };
            _sut.UnresolvedAccountRetryDelayMs = 1;
            _sut.ServerStreamFactory = (name, security) => throw new IOException("All pipe instances are busy.");

            // Act 1: several rechecks while both old processes run
            _sut.StartListening();
            for (var i = 0; i < 500 && Volatile.Read(ref refreshes) < 6; i++)
                await Task.Delay(10, CancellationToken.None);
            var whileBothRun = Volatile.Read(ref refreshes);

            // Act 2: the other service restarts under its new account; several more rechecks while this one's old process runs
            lock (gate) running[OtherPid] = localService;
            for (var i = 0; i < 500 && Volatile.Read(ref refreshes) < whileBothRun + 6; i++)
                await Task.Delay(10, CancellationToken.None);
            var whileOneRuns = Volatile.Read(ref refreshes);

            // Act 3: this service restarts too, so no account lingers any more and the rechecks end
            lock (gate) running[ServicePid] = localService;
            for (var i = 0; i < 500 && Volatile.Read(ref ended) == 0; i++)
                await Task.Delay(10, CancellationToken.None);
            _sut.StopListening();

            // Assert: one line per set the rechecks saw, not one per recheck
            Assert.True(whileBothRun >= 6);
            Assert.True(whileOneRuns >= whileBothRun + 6);
            _logger.Verify(l => l.Info(It.Is<string>(m => m.Contains("2 account(s) keep access")), It.IsAny<Exception>()), Times.Once);
            _logger.Verify(l => l.Info(It.Is<string>(m => m.Contains("1 account(s) keep access")), It.IsAny<Exception>()), Times.Once);
            _logger.Verify(l => l.Info(It.Is<string>(m => m.Contains("No account keeps access")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_AccountNeverResolvesWhileAFormerAccountLingers_LogsTheRetryWarningOnlyUpToTheBound()
        {
            // Arrange: an account that never resolves, and the old process still running under the service's former
            // account, so the rechecks go on after the bounded retries for the unresolved account are spent (#7362)
            var lookups = 0;
            var networkService = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
            _sut.ResolveAccount = _ => { Interlocked.Increment(ref lookups); return null; };
            _sut.ResolveProcessAccount = pid => pid == ServicePid ? networkService : null;
            _sut.UnresolvedAccountRetryDelayMs = 1;
            _sut.ServerStreamFactory = (name, security) => throw new IOException("All pipe instances are busy.");
            _repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ServiceDto { Name = ServiceName, RunAsLocalSystem = false, UserAccount = @"CONTOSO\deleted-user" } });
            var pastTheBound = 1 + AppConfig.ServyHostUnresolvedAccountRetryCount + 10;

            // Act
            _sut.StartListening();
            for (var i = 0; i < 1000 && Volatile.Read(ref lookups) < pastTheBound; i++)
                await Task.Delay(10, CancellationToken.None);
            _sut.StopListening();

            // Assert: one warning per retry actually scheduled, then a single line saying no further retry is scheduled
            Assert.True(Volatile.Read(ref lookups) >= pastTheBound);
            _logger.Verify(l => l.Warn(It.Is<string>(m => m.Contains("Retrying in")), It.IsAny<Exception>()), Times.Exactly(AppConfig.ServyHostUnresolvedAccountRetryCount));
            _logger.Verify(l => l.Warn(It.Is<string>(m => m.Contains("No further retry")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_StoppedService_GrantsNothingBeyondItsConfiguredAccount()
        {
            // Arrange: no process (the SCM reports PID 0)
            var reads = 0;
            _api.Setup(a => a.GetServiceProcessId(ServiceName)).Returns(0);
            _repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ServiceDto { Name = ServiceName, RunAsLocalSystem = false, UserAccount = @"NT AUTHORITY\LocalService" } });
            _sut.ResolveProcessAccount = _ => { Interlocked.Increment(ref reads); return new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null); };

            // Act
            await _sut.RefreshPipeSecurityAsync(CancellationToken.None);

            // Assert
            var rules = _sut.CurrentPipeSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
            Assert.Equal(3, rules.Count);
            Assert.DoesNotContain(rules, r => new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null).Equals(r.IdentityReference));
            Assert.Equal(0, reads);
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_AccountRemoved_IsNoLongerGranted()
        {
            // Arrange
            var localService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            _sut.ResolveAccount = _ => localService;
            _repository.SetupSequence(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ServiceDto { Name = "a", RunAsLocalSystem = false, UserAccount = "svc" } })
                .ReturnsAsync(Enumerable.Empty<ServiceDto>());

            // Act
            await _sut.RefreshPipeSecurityAsync(CancellationToken.None);
            var before = _sut.CurrentPipeSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().Count(r => localService.Equals(r.IdentityReference));
            await _sut.RefreshPipeSecurityAsync(CancellationToken.None);
            var after = _sut.CurrentPipeSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().Count(r => localService.Equals(r.IdentityReference));

            // Assert
            Assert.Equal(1, before);
            Assert.Equal(0, after);
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_RepositoryFailsFirst_FallsBackToAdministratorsAndSystemOnly()
        {
            // Arrange
            _repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("locked"));

            // Act
            await _sut.RefreshPipeSecurityAsync(CancellationToken.None);

            // Assert: never wider than the closed DACL (Local System and Administrators only)
            var rules = _sut.CurrentPipeSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
            Assert.Equal(2, rules.Count);
            _logger.Verify(l => l.Error(It.Is<string>(s => s.Contains("Failed to read the service accounts")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_RepositoryFailsLater_KeepsThePreviousDacl()
        {
            // Arrange
            var localService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            _sut.ResolveAccount = _ => localService;
            _repository.SetupSequence(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ServiceDto { Name = "a", RunAsLocalSystem = false, UserAccount = "svc" } })
                .ThrowsAsync(new InvalidOperationException("locked"));

            // Act
            await _sut.RefreshPipeSecurityAsync(CancellationToken.None);
            var first = _sut.CurrentPipeSecurity;
            await _sut.RefreshPipeSecurityAsync(CancellationToken.None);

            // Assert
            Assert.Same(first, _sut.CurrentPipeSecurity);
        }

        [Fact]
        public async Task RefreshPipeAccess_RepositoryFails_AnswersAFailure()
        {
            // Arrange
            _repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("database is locked"));

            // Act
            var response = await _sut.ProcessRequestAsync(Request(AppConfig.ServyHostRefreshPipeAccessAction, null), Administrator, CancellationToken.None);

            // Assert: the DACL was not rebuilt, so the caller must not be told the new account has access (#7392)
            Assert.False(response.Success);
            Assert.Contains("not refreshed", response.ErrorMessage);
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_RepositoryFailsAtStartup_IsRetriedUntilTheServicesAreRead()
        {
            // Arrange: the database is busy at boot for the first read only (#7392)
            var localService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            _sut.ResolveAccount = _ => localService;
            _sut.UnresolvedAccountRetryDelayMs = 10;
            _sut.ServerStreamFactory = (name, security) => throw new IOException("All pipe instances are busy.");
            _repository.SetupSequence(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("database is locked"))
                .ReturnsAsync(new[] { new ServiceDto { Name = "a", RunAsLocalSystem = false, UserAccount = @"NT AUTHORITY\LocalService" } });

            // Act
            _sut.StartListening();
            var granted = false;
            for (var i = 0; i < 500 && !granted; i++)
            {
                granted = _sut.CurrentPipeSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().Any(r => localService.Equals(r.IdentityReference));
                if (!granted) await Task.Delay(10, CancellationToken.None);
            }
            _sut.StopListening();

            // Assert: granted once the retry read the services, with no new install or restart
            Assert.True(granted);
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_RepositoryNeverReadable_StopsRetryingAfterTheConfiguredCount()
        {
            // Arrange
            var reads = 0;
            _sut.UnresolvedAccountRetryDelayMs = 1;
            _sut.ServerStreamFactory = (name, security) => throw new IOException("All pipe instances are busy.");
            _repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .Callback(() => Interlocked.Increment(ref reads))
                .ThrowsAsync(new IOException("database is locked"));

            // Act
            _sut.StartListening();
            var last = -1;
            for (var i = 0; i < 100 && last != Volatile.Read(ref reads); i++)
            {
                last = Volatile.Read(ref reads);
                await Task.Delay(100, CancellationToken.None);
            }
            _sut.StopListening();

            // Assert: the first read plus the configured retries, then nothing until the next refresh
            Assert.Equal(1 + AppConfig.ServyHostUnresolvedAccountRetryCount, Volatile.Read(ref reads));
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_OvertakenByALaterRead_DoesNotReplaceTheNewerDacl()
        {
            // Arrange: rebuild A reads one service and then blocks resolving its account, the way a domain lookup does
            // while no controller answers; rebuild B reads both services and publishes first (#7365)
            var accountX = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            var accountY = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
            var serviceX = new ServiceDto { Name = "x", RunAsLocalSystem = false, UserAccount = "svc-x" };
            var serviceY = new ServiceDto { Name = "y", RunAsLocalSystem = false, UserAccount = "svc-y" };
            _repository.SetupSequence(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { serviceX })
                .ReturnsAsync(new[] { serviceX, serviceY });
            var resolving = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            var lookups = 0;
            _sut.ResolveAccount = account =>
            {
                if (Interlocked.Increment(ref lookups) == 1)
                {
                    resolving.Set();
                    release.Wait();
                }

                return account == "svc-y" ? accountY : accountX;
            };
            bool Granted(SecurityIdentifier sid) => _sut.CurrentPipeSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().Any(r => sid.Equals(r.IdentityReference));

            // Act
            var a = Task.Run(() => _sut.RefreshPipeSecurityAsync(CancellationToken.None));
            resolving.Wait();
            await _sut.RefreshPipeSecurityAsync(CancellationToken.None);
            var publishedByB = Granted(accountY);
            release.Set();
            await a;

            // Assert: A finished last, but B read the services later, so B's DACL stays published
            Assert.True(publishedByB);
            Assert.True(Granted(accountY));
            Assert.True(Granted(accountX));
        }

        [Fact]
        public async Task RefreshPipeSecurityAsync_Cancelled_Propagates()
        {
            _repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

            await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.RefreshPipeSecurityAsync(CancellationToken.None));
        }

        #endregion

        #region Service lifecycle

        /// <summary>
        /// A host <see cref="Service"/> that exposes the Service Control Manager lifecycle callbacks to the test.
        /// </summary>
        private sealed class LifecycleProbe : Service
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="LifecycleProbe"/> class.
            /// </summary>
            /// <param name="logger">The logger the host reports to.</param>
            /// <param name="pipes">The named pipe framing service.</param>
            /// <param name="repository">The repository the pipe DACL is built from.</param>
            /// <param name="api">The Windows service API.</param>
            /// <param name="identifier">The pipe caller identifier.</param>
            public LifecycleProbe(IServyLogger logger, INamedPipesService pipes, IServiceRepository repository, IWindowsServiceApi api, IPipeCallerIdentifier identifier)
                : base(logger, pipes, repository, api, identifier)
            {
            }

            /// <summary>
            /// Invokes the protected start callback, as the Service Control Manager would.
            /// </summary>
            public void RunOnStart() => OnStart(Array.Empty<string>());

            /// <summary>
            /// Invokes the protected stop callback, as the Service Control Manager would.
            /// </summary>
            public void RunOnStop() => OnStop();
        }

        [Fact]
        public void OnStart_PipeCannotBeCreated_LogsTheAcceptFailureAndOnStopFlushesTheLogger()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            logger.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(() => logger.Object);
            using (var accepted = new ManualResetEventSlim())
            using (var probe = new LifecycleProbe(logger.Object, _pipes.Object, _repository.Object, _api.Object, _identifier.Object))
            {
                logger.Setup(l => l.Error("Error accepting Named Pipe connection.", It.IsAny<IOException>())).Callback(() => accepted.Set());
                probe.ServerStreamFactory = (name, security) => throw new IOException("All pipe instances are busy.");

                // Act
                probe.RunOnStart();
                var logged = accepted.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                probe.RunOnStop();

                // Assert
                Assert.True(logged);
                Assert.Equal(0, probe.ExitCode);
                logger.Verify(l => l.Dispose(), Times.Once);
                logger.Verify(l => l.Error("Exception in OnStart.", It.IsAny<Exception>()), Times.Never);
            }
        }

        [Fact]
        public void OnStop_LoggerDisposeHangs_ReturnsAfterTheFlushBudget()
        {
            // Arrange
            using (var release = new ManualResetEventSlim(false))
            {
                var logger = new Mock<IServyLogger>();
                logger.Setup(l => l.Dispose()).Callback(() => release.Wait(TimeSpan.FromSeconds(30)));
                var probe = new LifecycleProbe(logger.Object, _pipes.Object, _repository.Object, _api.Object, _identifier.Object);

                try
                {
                    // Act
                    var stop = Task.Run(() => probe.RunOnStop());
                    var returned = stop.Wait(TimeSpan.FromSeconds(10));

                    // Assert: the flush gives up after AppConfig.LoggerFlushTimeoutMs instead of waiting for the hung Dispose
                    Assert.True(returned);
                    logger.Verify(l => l.Dispose(), Times.Once);
                }
                finally
                {
                    release.Set();
                    probe.Dispose();
                }
            }
        }

        [Fact]
        public void OnStart_PipeDaclCannotBeBuilt_LogsAndSetsTheServiceSpecificExitCode()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            logger.Setup(l => l.CreateScoped(It.IsAny<string>())).Returns(() => logger.Object);
            _repository.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ServiceDto { Name = "a", RunAsLocalSystem = false, UserAccount = "svc-a" } });
            using (var probe = new LifecycleProbe(logger.Object, _pipes.Object, _repository.Object, _api.Object, _identifier.Object))
            {
                probe.ResolveAccount = account => throw new InvalidOperationException("LSA unavailable");

                // Act
                // The catch ends with ServiceBase.Stop(), which runs outside the Service Control Manager. What it does
                // there is not this test's subject, so anything it throws is recorded rather than asserted.
                Record.Exception(() => probe.RunOnStart());

                // Assert
                logger.Verify(l => l.Error("Exception in OnStart.", It.IsAny<InvalidOperationException>()), Times.Once);
                Assert.Equal(AppConfig.ServiceSpecificErrorCode, probe.ExitCode);
            }
        }

        #endregion

        #region Connection handling

        /// <summary>
        /// Starts a host on <paramref name="pipeName"/> whose server instances carry the default DACL instead of the
        /// host's Administrators-and-System one, so a non-elevated test process can connect to it.
        /// </summary>
        /// <param name="logger">The logger the host reports to.</param>
        /// <param name="pipeName">The name of the pipe to listen on.</param>
        /// <param name="requestTimeoutMs">The per-request timeout, in milliseconds.</param>
        /// <returns>The listening host. The caller owns it.</returns>
        private Service ListeningHost(Mock<IServyLogger> logger, string pipeName, int requestTimeoutMs)
        {
            var host = new Service(logger.Object, _pipes.Object, _repository.Object, _api.Object, _identifier.Object)
            {
                PipeName = pipeName,
                RequestTimeoutMs = requestTimeoutMs,
                ServerStreamFactory = (name, security) => new NamedPipeServerStream(name, PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous),
            };
            host.StartListening();
            return host;
        }

        [Fact]
        public void HandleConnectionAsync_ReadThrows_LogsTheError()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var pipeName = "ServyHostUnit_" + Guid.NewGuid().ToString("N");
            _pipes.Setup(p => p.ReadAsync<IpcRequestDto>(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidDataException("bad frame"));
            using (var logged = new ManualResetEventSlim())
            {
                logger.Setup(l => l.Error("Exception in HandleConnectionAsync.", It.IsAny<InvalidDataException>())).Callback(() => logged.Set());
                using (var host = ListeningHost(logger, pipeName, requestTimeoutMs: 5000))
                {
                    // Act
                    bool observed;
                    using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
                    {
                        client.Connect(3000);
                        observed = logged.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                    }

                    // Assert
                    Assert.True(observed);

                    // Draining the handlers is cleanup, not this test's subject, but it is called plainly: since #7353
                    // the listener instances are no longer scheduled with the token, so cancelling the host does not
                    // end the ones the thread pool has not started yet as Canceled, and StopListening tolerates a
                    // cancellation from either wait. A throw here is a regression and must fail the test.
                    host.StopListening();
                }
            }
        }

        [Fact]
        public void HandleConnectionAsync_ClientNeverSendsItsRequest_WarnsAfterTheRequestTimeout()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var pipeName = "ServyHostUnit_" + Guid.NewGuid().ToString("N");
            _pipes.Setup(p => p.ReadAsync<IpcRequestDto>(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns<Stream, CancellationToken>(async (stream, token) =>
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return null;
                });
            using (var warned = new ManualResetEventSlim())
            {
                // Either timeout arm may run - a pending read does not observe its token on every runtime - and both
                // log this warning, so the test asserts the warning rather than which arm produced it.
                logger.Setup(l => l.Warn(It.Is<string>(m => m.Contains("did not complete its request in time")), null)).Callback(() => warned.Set());
                using (var host = ListeningHost(logger, pipeName, requestTimeoutMs: 200))
                {
                    // Act
                    bool observed;
                    using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
                    {
                        client.Connect(3000);
                        observed = warned.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                    }

                    // Assert
                    Assert.True(observed);
                    logger.Verify(l => l.Error("Exception in HandleConnectionAsync.", It.IsAny<Exception>()), Times.Never);

                    // Same as above: the drain keeps the pending handler from setting an event this test is about to
                    // dispose, and since #7353 it is called plainly, so a throw from it fails the test.
                    host.StopListening();
                }
            }
        }

        #endregion
    }
}
