using Moq;
using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.NamedPipes;
using System.Text;

namespace Servy.Core.UnitTests.NamedPipes
{
    /// <summary>
    /// Unit tests for the framing and the argument contract of <see cref="NamedPipesService"/>. The frames are written to
    /// and read from in-memory streams; the requests over a real pipe are covered by the integration tests.
    /// </summary>
    public class NamedPipesServiceTests
    {
        private readonly NamedPipesService _sut = new NamedPipesService("unit-test-pipe", new Mock<INamedPipeServerVerifier>().Object);

        #region Constructors

        [Fact]
        public void Constructor_NullPipeName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NamedPipesService(null!));
            Assert.Throws<ArgumentNullException>(() => new NamedPipesService(null!, new Mock<INamedPipeServerVerifier>().Object));
        }

        [Fact]
        public void Constructor_NullVerifier_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NamedPipesService("pipe", null!));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-1, 1)]
        [InlineData(1, 0)]
        [InlineData(1, -5)]
        public void Constructor_NonPositiveTimeout_Throws(int connectTimeoutMs, int requestTimeoutMs)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NamedPipesService("pipe", new Mock<INamedPipeServerVerifier>().Object, connectTimeoutMs, requestTimeoutMs));
        }

        [Fact]
        public void DefaultConstructor_DoesNotThrow()
        {
            var ex = Record.Exception(() => new NamedPipesService());
            Assert.Null(ex);
        }

        #endregion

        #region Framing

        [Fact]
        public void Write_WritesALittleEndianLengthPrefixFollowedByTheUtf8Json()
        {
            // Arrange
            var request = new IpcRequestDto { Action = "GetByName", ServiceName = "Sérvice" };
            using (var stream = new MemoryStream())
            {
                // Act
                _sut.Write(stream, request, TestContext.Current.CancellationToken);

                // Assert
                var bytes = stream.ToArray();
                int length = BitConverter.ToInt32(bytes, 0);
                Assert.Equal(bytes.Length - 4, length);
                var json = Encoding.UTF8.GetString(bytes, 4, length);
                Assert.Contains("\"Sérvice\"", json);
            }
        }

        [Fact]
        public void WriteThenRead_RoundTripsARequestWithItsRuntimeStateAndCounter()
        {
            // Arrange
            var request = new IpcRequestDto
            {
                Action = AppConfig.ServyHostUpdateRuntimeStateAction,
                ServiceName = "svc",
                RuntimeState = new ServiceRuntimeStateDto { Pid = 77, ActiveStdoutPath = "o", ActiveStderrPath = "e", UpdatePreviousStopTimeout = true, PreviousStopTimeout = 9 },
                RestartAttempts = 3,
            };
            using (var stream = new MemoryStream())
            {
                // Act
                _sut.Write(stream, request, TestContext.Current.CancellationToken);
                stream.Position = 0;
                var read = _sut.Read<IpcRequestDto>(stream, TestContext.Current.CancellationToken);

                // Assert
                Assert.NotNull(read);
                Assert.Equal(request.Action, read!.Action);
                Assert.Equal("svc", read.ServiceName);
                Assert.Equal(77, read.RuntimeState!.Pid);
                Assert.Equal("o", read.RuntimeState.ActiveStdoutPath);
                Assert.Equal("e", read.RuntimeState.ActiveStderrPath);
                Assert.True(read.RuntimeState.UpdatePreviousStopTimeout);
                Assert.Equal(9, read.RuntimeState.PreviousStopTimeout);
                Assert.Equal(3, read.RestartAttempts);
            }
        }

        [Fact]
        public async Task WriteAsyncThenReadAsync_RoundTripsAResponseWithAUtcTimestamp()
        {
            // Arrange
            var when = new DateTime(2026, 10, 2, 8, 30, 0, DateTimeKind.Utc);
            var response = new IpcResponseDto
            {
                Success = true,
                Data = new ServiceDto { Name = "svc", ExecutablePath = "C:\\app.exe" },
                UpdateData = 1,
                RestartAttempts = new RestartAttemptsDto { Attempts = 4, UpdatedAtUtc = when },
            };
            using (var stream = new MemoryStream())
            {
                // Act
                await _sut.WriteAsync(stream, response, TestContext.Current.CancellationToken);
                stream.Position = 0;
                var read = await _sut.ReadAsync<IpcResponseDto>(stream, TestContext.Current.CancellationToken);

                // Assert
                Assert.NotNull(read);
                Assert.True(read!.Success);
                Assert.Equal("C:\\app.exe", read.Data!.ExecutablePath);
                Assert.Equal(1, read.UpdateData);
                Assert.Equal(4, read.RestartAttempts!.Attempts);
                Assert.Equal(when, read.RestartAttempts.UpdatedAtUtc);
                Assert.Equal(DateTimeKind.Utc, read.RestartAttempts.UpdatedAtUtc!.Value.Kind);
            }
        }

        [Fact]
        public void Write_ServiceDto_NeverCarriesTheExportIgnoredFields()
        {
            // The reason the runtime state has its own contract: ServiceDto's runtime and credential fields are
            // [JsonIgnore] for the export files, so a ServiceDto frame cannot carry them.
            var dto = new ServiceDto { Name = "svc", Pid = 123, ActiveStdoutPath = "out.log", PreviousStopTimeout = 30, Password = "secret", UserAccount = @".\user" };
            using (var stream = new MemoryStream())
            {
                _sut.Write(stream, dto, TestContext.Current.CancellationToken);
                stream.Position = 0;
                var read = _sut.Read<ServiceDto>(stream, TestContext.Current.CancellationToken);

                Assert.NotNull(read);
                Assert.Null(read!.Pid);
                Assert.Null(read.ActiveStdoutPath);
                Assert.Null(read.PreviousStopTimeout);
                Assert.Null(read.Password);
                Assert.Null(read.UserAccount);
            }
        }

        [Fact]
        public void Read_EmptyStream_ReturnsNull()
        {
            using (var stream = new MemoryStream())
            {
                Assert.Null(_sut.Read<IpcRequestDto>(stream, TestContext.Current.CancellationToken));
            }
        }

        [Fact]
        public async Task ReadAsync_EmptyStream_ReturnsNull()
        {
            using (var stream = new MemoryStream())
            {
                Assert.Null(await _sut.ReadAsync<IpcRequestDto>(stream, TestContext.Current.CancellationToken));
            }
        }

        [Fact]
        public async Task Read_TruncatedLengthPrefix_ReturnsNull()
        {
            using (var sync = new MemoryStream(new byte[] { 1, 0 }))
            using (var async = new MemoryStream(new byte[] { 1, 0 }))
            {
                Assert.Null(_sut.Read<IpcRequestDto>(sync, TestContext.Current.CancellationToken));
                Assert.Null(await _sut.ReadAsync<IpcRequestDto>(async, TestContext.Current.CancellationToken));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        public async Task Read_LengthOutOfRange_ReturnsNullWithoutReadingThePayload(int length)
        {
            // Arrange: a prefix that is not positive, or above the 10 MB cap, followed by some bytes
            var bytes = BitConverter.GetBytes(length).Concat(Encoding.UTF8.GetBytes("{}")).ToArray();
            using (var sync = new MemoryStream(bytes))
            using (var async = new MemoryStream(bytes))
            {
                // Act & Assert
                Assert.Null(_sut.Read<IpcRequestDto>(sync, TestContext.Current.CancellationToken));
                Assert.Null(await _sut.ReadAsync<IpcRequestDto>(async, TestContext.Current.CancellationToken));
                Assert.Equal(4, sync.Position);
                Assert.Equal(4, async.Position);
            }
        }

        [Fact]
        public void Read_LengthJustAboveTheCap_ReturnsNull()
        {
            var bytes = BitConverter.GetBytes((int)AppConfig.ServyHostMaxAllowedMessageSizeBytes + 1);
            using (var stream = new MemoryStream(bytes))
            {
                Assert.Null(_sut.Read<IpcRequestDto>(stream, TestContext.Current.CancellationToken));
            }
        }

        [Fact]
        public async Task Read_TruncatedPayload_ReturnsNull()
        {
            // Arrange: the prefix announces 50 bytes, 2 follow
            var bytes = BitConverter.GetBytes(50).Concat(Encoding.UTF8.GetBytes("{}")).ToArray();
            using (var sync = new MemoryStream(bytes))
            using (var async = new MemoryStream(bytes))
            {
                Assert.Null(_sut.Read<IpcRequestDto>(sync, TestContext.Current.CancellationToken));
                Assert.Null(await _sut.ReadAsync<IpcRequestDto>(async, TestContext.Current.CancellationToken));
            }
        }

        [Theory]
        [InlineData("{ not json")]
        [InlineData("[1,2,3]")]
        public async Task Read_MalformedPayload_ReturnsNull(string payload)
        {
            var json = Encoding.UTF8.GetBytes(payload);
            var bytes = BitConverter.GetBytes(json.Length).Concat(json).ToArray();
            using (var sync = new MemoryStream(bytes))
            using (var async = new MemoryStream(bytes))
            {
                Assert.Null(_sut.Read<IpcRequestDto>(sync, TestContext.Current.CancellationToken));
                Assert.Null(await _sut.ReadAsync<IpcRequestDto>(async, TestContext.Current.CancellationToken));
            }
        }

        [Fact]
        public void Read_PayloadWithATypeName_IsNotHonoured()
        {
            // A frame can never choose the type it is read as: a $type hint is ignored, not followed
            var json = Encoding.UTF8.GetBytes("{\"$type\":\"System.IO.FileInfo, System.IO.FileSystem\",\"Action\":\"GetByName\"}");
            var bytes = BitConverter.GetBytes(json.Length).Concat(json).ToArray();
            using (var stream = new MemoryStream(bytes))
            {
                var read = _sut.Read<IpcRequestDto>(stream, TestContext.Current.CancellationToken);

                Assert.NotNull(read);
                Assert.IsType<IpcRequestDto>(read);
                Assert.Equal("GetByName", read!.Action);
            }
        }

        [Fact]
        public async Task ReadAndWrite_NullStream_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => _sut.Read<IpcRequestDto>(null!));
            Assert.Throws<ArgumentNullException>(() => _sut.Write(null!, new IpcRequestDto()));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.ReadAsync<IpcRequestDto>(null!));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.WriteAsync(null!, new IpcRequestDto()));
        }

        [Fact]
        public async Task ReadAndWrite_Cancelled_ThrowAndWriteNothing()
        {
            using (var cts = new CancellationTokenSource())
            using (var stream = new MemoryStream())
            {
                cts.Cancel();

                Assert.Throws<OperationCanceledException>(() => _sut.Write(stream, new IpcRequestDto(), cts.Token));
                await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.WriteAsync(stream, new IpcRequestDto(), cts.Token));
                Assert.Equal(0, stream.Length);

                Assert.Throws<OperationCanceledException>(() => _sut.Read<IpcRequestDto>(stream, cts.Token));
                await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.ReadAsync<IpcRequestDto>(stream, cts.Token));
            }
        }

        [Fact]
        public void Write_PayloadAboveTheCap_ThrowsInsteadOfSendingAFrameTheReaderWouldRefuse()
        {
            // Arrange: a string of 10 MB + 1 byte serializes above the cap
            var request = new IpcRequestDto { ServiceName = new string('x', (int)AppConfig.ServyHostMaxAllowedMessageSizeBytes + 1) };
            using (var stream = new MemoryStream())
            {
                // Act & Assert
                Assert.Throws<InvalidOperationException>(() => _sut.Write(stream, request, TestContext.Current.CancellationToken));
                Assert.Equal(0, stream.Length);
            }
        }

        [Fact]
        public void Read_FrameSplitAcrossManySmallReads_IsReassembled()
        {
            // Arrange: a pipe hands the bytes over in pieces; ReadExact must loop until the frame is complete
            using (var buffer = new MemoryStream())
            {
                _sut.Write(buffer, new IpcRequestDto { Action = "GetByName", ServiceName = "svc" }, TestContext.Current.CancellationToken);
                using (var trickle = new TrickleStream(buffer.ToArray(), chunk: 3))
                {
                    // Act
                    var read = _sut.Read<IpcRequestDto>(trickle, TestContext.Current.CancellationToken);

                    // Assert
                    Assert.Equal("svc", read!.ServiceName);
                    Assert.True(trickle.Reads > 3);
                }
            }
        }

        [Fact]
        public async Task ReadAsync_FrameSplitAcrossManySmallReads_IsReassembled()
        {
            using (var buffer = new MemoryStream())
            {
                await _sut.WriteAsync(buffer, new IpcRequestDto { Action = "GetByName", ServiceName = "svc" }, TestContext.Current.CancellationToken);
                using (var trickle = new TrickleStream(buffer.ToArray(), chunk: 2))
                {
                    var read = await _sut.ReadAsync<IpcRequestDto>(trickle, TestContext.Current.CancellationToken);

                    Assert.Equal("svc", read!.ServiceName);
                    Assert.True(trickle.Reads > 3);
                }
            }
        }

        #endregion

        #region Request Argument Contract

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public async Task Requests_BlankServiceName_ThrowBeforeConnecting(string? name)
        {
            Assert.Throws<ArgumentException>(() => _sut.GetByName(name!, TestContext.Current.CancellationToken));
            Assert.Throws<ArgumentException>(() => _sut.UpdateRuntimeState(name!, new ServiceRuntimeStateDto(), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetRestartAttemptsAsync(name!, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateRestartAttemptsAsync(name!, 1, TestContext.Current.CancellationToken));
        }

        [Fact]
        public void UpdateRuntimeState_NullState_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _sut.UpdateRuntimeState("svc", null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task UpdateRestartAttemptsAsync_NegativeCounter_Throws()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _sut.UpdateRestartAttemptsAsync("svc", -1, TestContext.Current.CancellationToken));
        }

        [Fact]
        public void Constants_AreTheOnesTheHostServes()
        {
            Assert.Equal("Servy", AppConfig.ServyHostServiceName);
            Assert.Equal("SERVY_HOST_IPC_PIPE", AppConfig.ServyHostNamedPipeName);
            var actions = new[]
            {
                AppConfig.ServyHostGetByNameAction, AppConfig.ServyHostUpdateRuntimeStateAction,
                AppConfig.ServyHostGetRestartAttemptsAction, AppConfig.ServyHostUpdateRestartAttemptsAction,
                AppConfig.ServyHostRefreshPipeAccessAction,
            };
            Assert.Equal(actions.Length, actions.Distinct(StringComparer.Ordinal).Count());
        }

        #endregion

        /// <summary>
        /// A read-only stream that hands its bytes over at most <c>chunk</c> at a time, like a pipe can.
        /// </summary>
        private sealed class TrickleStream : MemoryStream
        {
            private readonly int _chunk;

            public TrickleStream(byte[] bytes, int chunk) : base(bytes)
            {
                _chunk = chunk;
            }

            public int Reads { get; private set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                Reads++;
                return base.Read(buffer, offset, Math.Min(count, _chunk));
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Reads++;
                return base.ReadAsync(buffer, offset, Math.Min(count, _chunk), cancellationToken);
            }
        }
    }
}
