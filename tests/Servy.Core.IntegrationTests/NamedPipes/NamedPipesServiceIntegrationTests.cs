using Moq;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.NamedPipes;
using Servy.Core.Security;
using Servy.Core.Services;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Servy.Core.IntegrationTests.NamedPipes
{
    /// <summary>
    /// Runs <see cref="NamedPipesService"/> against a real named pipe served by this test process: the requests and their
    /// answers, the timeouts, the server-identity check against pipe squatting, the peer process identifiers, the caller
    /// identification and the pipe DACL.
    /// </summary>
    [Collection(CoreOsIntegrationCollection.Name)]
    public class NamedPipesServiceIntegrationTests
    {
        /// <summary>A real pipe that is never connected or answered must fail the test, not hang the run.</summary>
        private const int IntegrationTestTimeoutMs = 60000;

        private readonly string _pipeName = "ServyTestPipe_" + Guid.NewGuid().ToString("N");
        private readonly Mock<IWindowsServiceApi> _api = new Mock<IWindowsServiceApi>();

        public NamedPipesServiceIntegrationTests()
        {
            // The test process plays the Servy host service
            _api.Setup(a => a.GetServiceProcessId(AppConfig.ServyHostServiceName)).Returns(Environment.ProcessId);
        }

        private NamedPipesService CreateClient(int connectTimeoutMs = 2000, int requestTimeoutMs = 5000)
            => new NamedPipesService(_pipeName, new ServyHostServerVerifier(_api.Object), connectTimeoutMs, requestTimeoutMs);

        /// <summary>
        /// Serves one connection: reads the request, records it, and answers with <paramref name="respond"/>
        /// (or closes the pipe without answering when it returns <see langword="null"/>).
        /// </summary>
        private Task<IpcRequestDto?> ServeOnceAsync(Func<IpcRequestDto?, IpcResponseDto?> respond, CancellationToken ct, TimeSpan? delayBeforeAnswer = null)
        {
            var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            var framing = new NamedPipesService(_pipeName, new Mock<INamedPipeServerVerifier>().Object);
            return Task.Run(async () =>
            {
                using (server)
                {
                    await server.WaitForConnectionAsync(ct);
                    var request = await framing.ReadAsync<IpcRequestDto>(server, ct);

                    // A client that refused the server closes without writing anything
                    if (request == null)
                        return null;

                    if (delayBeforeAnswer.HasValue)
                        await Task.Delay(delayBeforeAnswer.Value, ct);

                    var response = respond(request);
                    if (response != null)
                        await framing.WriteAsync(server, response, ct);
                    return request;
                }
            }, ct);
        }

        #region Requests

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task GetByName_HostAnswers_ReturnsTheConfigurationAndSendsTheAction()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var served = ServeOnceAsync(_ => new IpcResponseDto { Success = true, Data = new ServiceDto { Name = "svc", ExecutablePath = "C:\\app.exe" } }, ct);

            // Act
            var dto = await Task.Run(() => CreateClient().GetByName("svc", ct), ct);

            // Assert
            Assert.Equal("C:\\app.exe", dto!.ExecutablePath);
            var request = await served;
            Assert.Equal(AppConfig.ServyHostGetByNameAction, request!.Action);
            Assert.Equal("svc", request.ServiceName);
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task GetByName_ServiceHasNoRow_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var served = ServeOnceAsync(_ => new IpcResponseDto { Success = true, Data = null }, ct);

            var dto = await Task.Run(() => CreateClient().GetByName("svc", ct), ct);

            Assert.Null(dto);
            await served;
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task GetByName_HostRefuses_ThrowsWithTheHostsReason()
        {
            var ct = TestContext.Current.CancellationToken;
            var served = ServeOnceAsync(_ => new IpcResponseDto { Success = false, ErrorMessage = "Access denied." }, ct);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => CreateClient().GetByName("svc", ct), ct));

            Assert.Contains("Failed to retrieve configuration for service 'svc'", ex.Message);
            Assert.Contains("Access denied.", ex.Message);
            await served;
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task GetByName_HostClosesWithoutAnswering_Throws()
        {
            var ct = TestContext.Current.CancellationToken;
            var served = ServeOnceAsync(_ => null, ct);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => CreateClient().GetByName("svc", ct), ct));

            Assert.Contains("No additional error details received.", ex.Message);
            await served;
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task UpdateRuntimeState_SendsOnlyTheRuntimeStateAndReturnsTheRowCount()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var served = ServeOnceAsync(_ => new IpcResponseDto { Success = true, UpdateData = 1 }, ct);
            var state = new ServiceRuntimeStateDto { Pid = 1234, ActiveStdoutPath = "out", ActiveStderrPath = "err", UpdatePreviousStopTimeout = true, PreviousStopTimeout = 20 };

            // Act
            var updated = await Task.Run(() => CreateClient().UpdateRuntimeState("svc", state, ct), ct);

            // Assert
            Assert.Equal(1, updated);
            var request = await served;
            Assert.Equal(AppConfig.ServyHostUpdateRuntimeStateAction, request!.Action);
            Assert.Equal("svc", request.ServiceName);
            Assert.Equal(1234, request.RuntimeState!.Pid);
            Assert.Equal(20, request.RuntimeState.PreviousStopTimeout);
            Assert.True(request.RuntimeState.UpdatePreviousStopTimeout);
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task GetRestartAttemptsAsync_ReturnsTheHostsCounter()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var when = new DateTime(2026, 10, 2, 7, 0, 0, DateTimeKind.Utc);
            var served = ServeOnceAsync(_ => new IpcResponseDto { Success = true, RestartAttempts = new RestartAttemptsDto { Attempts = 3, UpdatedAtUtc = when } }, ct);

            // Act
            var attempts = await CreateClient().GetRestartAttemptsAsync("svc", ct);

            // Assert
            Assert.Equal(3, attempts.Attempts);
            Assert.Equal(when, attempts.UpdatedAtUtc);
            Assert.Equal(AppConfig.ServyHostGetRestartAttemptsAction, (await served)!.Action);
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task GetRestartAttemptsAsync_AnswerWithoutCounter_ReturnsZero()
        {
            var ct = TestContext.Current.CancellationToken;
            var served = ServeOnceAsync(_ => new IpcResponseDto { Success = true }, ct);

            var attempts = await CreateClient().GetRestartAttemptsAsync("svc", ct);

            Assert.Equal(0, attempts.Attempts);
            Assert.Null(attempts.UpdatedAtUtc);
            await served;
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task UpdateRestartAttemptsAsync_SendsTheCounter()
        {
            var ct = TestContext.Current.CancellationToken;
            var served = ServeOnceAsync(_ => new IpcResponseDto { Success = true, UpdateData = 1 }, ct);

            var updated = await CreateClient().UpdateRestartAttemptsAsync("svc", 5, ct);

            Assert.Equal(1, updated);
            var request = await served;
            Assert.Equal(AppConfig.ServyHostUpdateRestartAttemptsAction, request!.Action);
            Assert.Equal(5, request.RestartAttempts);
        }

        [Theory(Timeout = IntegrationTestTimeoutMs)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task RefreshPipeAccessAsync_ReportsTheHostsAnswer(bool success, bool expected)
        {
            var ct = TestContext.Current.CancellationToken;
            var served = ServeOnceAsync(_ => new IpcResponseDto { Success = success }, ct);

            var refreshed = await CreateClient().RefreshPipeAccessAsync(ct);

            Assert.Equal(expected, refreshed);
            Assert.Equal(AppConfig.ServyHostRefreshPipeAccessAction, (await served)!.Action);
        }

        #endregion

        #region Timeouts and an absent host

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task GetByName_NoHost_ThrowsTimeoutAfterTheConnectTimeout()
        {
            var stopwatch = Stopwatch.StartNew();

            var ex = await Assert.ThrowsAsync<TimeoutException>(() => Task.Run(() => CreateClient(connectTimeoutMs: 200).GetByName("svc", TestContext.Current.CancellationToken)));

            Assert.Contains("timed out after 200 ms", ex.Message);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30));
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task RefreshPipeAccessAsync_NoHost_ReturnsFalseInsteadOfThrowing()
        {
            Assert.False(await CreateClient(connectTimeoutMs: 200).RefreshPipeAccessAsync(TestContext.Current.CancellationToken));
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task GetRestartAttemptsAsync_HostNeverAnswers_ThrowsTimeoutAfterTheRequestTimeout()
        {
            // Arrange: the server reads the request and then sits on it
            var ct = TestContext.Current.CancellationToken;
            using (var stopServer = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                var served = ServeOnceAsync(_ => new IpcResponseDto { Success = true }, stopServer.Token, delayBeforeAnswer: TimeSpan.FromMinutes(5));

                // Act
                var ex = await Assert.ThrowsAsync<TimeoutException>(() => CreateClient(requestTimeoutMs: 300).GetRestartAttemptsAsync("svc", ct));

                // Assert
                Assert.Contains("did not answer the 'GetRestartAttempts' request within 300 ms", ex.Message);
                stopServer.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => served);
            }
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task GetRestartAttemptsAsync_CallerCancels_ThrowsOperationCanceledNotTimeout()
        {
            var ct = TestContext.Current.CancellationToken;
            using (var stopServer = CancellationTokenSource.CreateLinkedTokenSource(ct))
            using (var caller = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                var served = ServeOnceAsync(_ => new IpcResponseDto { Success = true }, stopServer.Token, delayBeforeAnswer: TimeSpan.FromMinutes(5));
                caller.CancelAfter(300);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateClient(requestTimeoutMs: 60000).GetRestartAttemptsAsync("svc", caller.Token));

                stopServer.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => served);
            }
        }

        #endregion

        #region Server identity (pipe squatting)

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task GetByName_PipeServedByAnotherProcessThanTheHost_RefusesBeforeSendingTheRequest()
        {
            // Arrange: the SCM reports another process as the host, so this process is a squatter
            var ct = TestContext.Current.CancellationToken;
            _api.Setup(a => a.GetServiceProcessId(AppConfig.ServyHostServiceName)).Returns(Environment.ProcessId + 1);
            var served = ServeOnceAsync(_ => new IpcResponseDto { Success = true, Data = new ServiceDto { Name = "evil", ExecutablePath = "C:\\evil.exe" } }, ct);

            // Act
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Task.Run(() => CreateClient().GetByName("svc", ct), ct));

            // Assert: the client closed the connection without writing a request
            Assert.Contains($"process {Environment.ProcessId}", ex.Message);
            Assert.Null(await served);
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task GetByName_HostServiceNotRunning_Refuses()
        {
            var ct = TestContext.Current.CancellationToken;
            _api.Setup(a => a.GetServiceProcessId(AppConfig.ServyHostServiceName)).Returns(0);
            var served = ServeOnceAsync(_ => new IpcResponseDto { Success = true }, ct);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Task.Run(() => CreateClient().GetByName("svc", ct), ct));
            Assert.Null(await served);
        }

        #endregion

        #region Peer identification

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task PipePeer_BothEnds_ReportThisProcess()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            using (var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            using (var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification))
            {
                var waiting = server.WaitForConnectionAsync(ct);
                await client.ConnectAsync(2000, ct);
                await waiting;

                // Act & Assert
                Assert.True(PipePeer.TryGetClientProcessId(server, out var clientPid));
                Assert.Equal(Environment.ProcessId, clientPid);
                Assert.True(PipePeer.TryGetServerProcessId(client, out var serverPid));
                Assert.Equal(Environment.ProcessId, serverPid);
            }
        }

        [Fact]
        public void PipePeer_NotConnected_ReturnsFalse()
        {
            using (var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            {
                Assert.False(PipePeer.TryGetClientProcessId(server, out var pid));
                Assert.Equal(0, pid);
                Assert.False(PipePeer.TryGetServerProcessId(null!, out _));
            }
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task PipeCallerIdentifier_IdentifiesThisProcessAndItsElevation()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            using (var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            using (var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification))
            {
                var waiting = server.WaitForConnectionAsync(ct);
                await client.ConnectAsync(2000, ct);
                await waiting;

                // Impersonation needs the server to have read something the client wrote. No Flush: flushing a pipe
                // waits until the other end has read, and that end is this same thread.
                var received = server.ReadAsync(new byte[1], 0, 1, ct);
                await client.WriteAsync(new byte[] { 1 }, 0, 1, ct);
                Assert.Equal(1, await received);

                // Act
                var caller = new PipeCallerIdentifier().Identify(server);

                // Assert
                Assert.Equal(Environment.ProcessId, caller.ProcessId);
                Assert.Equal(SecurityHelper.IsAdministrator(), caller.IsAdministrator);
            }
        }

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task PipeCallerIdentifier_AnonymousClient_IsNeverAnAdministrator()
        {
            // Arrange: a client that allows no impersonation at all cannot be identified as anything
            var ct = TestContext.Current.CancellationToken;
            using (var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            using (var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Anonymous))
            {
                var waiting = server.WaitForConnectionAsync(ct);
                await client.ConnectAsync(2000, ct);
                await waiting;
                var received = server.ReadAsync(new byte[1], 0, 1, ct);
                await client.WriteAsync(new byte[] { 1 }, 0, 1, ct);
                Assert.Equal(1, await received);

                // Act
                var caller = new PipeCallerIdentifier().Identify(server);

                // Assert
                Assert.False(caller.IsAdministrator);
                Assert.Equal(Environment.ProcessId, caller.ProcessId);
            }
        }

        #endregion

        #region Pipe DACL

        [Fact(Timeout = IntegrationTestTimeoutMs)]
        public async Task ServyHostPipeSecurity_AppliedToARealPipe_IsWhatTheServerInstanceCarries()
        {
            // Only Local System and Administrators may connect to a pipe carrying this DACL besides LocalService
            Assert.SkipUnless(SecurityHelper.IsAdministrator(), "Connecting to a pipe with the host's DACL requires an elevated process.");

            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var localService = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            var security = ServyHostPipeSecurity.Create(new[] { localService });

            // Act
            using (var server = NamedPipeServerStreamAcl.Create(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security))
            using (var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification))
            {
                var waiting = server.WaitForConnectionAsync(ct);
                await client.ConnectAsync(2000, ct);
                await waiting;

                // Assert: the instance's own descriptor, read back from the kernel
                var applied = server.GetAccessControl();
                Assert.True(applied.AreAccessRulesProtected);
                var rules = applied.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
                var grant = Assert.Single(rules, r => localService.Equals(r.IdentityReference));
                Assert.Equal(0, (int)(grant.PipeAccessRights & PipeAccessRights.CreateNewInstance));
                Assert.Contains(rules, r => new SecurityIdentifier(WellKnownSidType.NetworkSid, null).Equals(r.IdentityReference) && r.AccessControlType == AccessControlType.Deny);
                Assert.DoesNotContain(rules, r => new SecurityIdentifier(WellKnownSidType.WorldSid, null).Equals(r.IdentityReference));
            }
        }

        #endregion
    }
}
