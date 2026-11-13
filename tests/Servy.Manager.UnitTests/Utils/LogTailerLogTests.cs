using Servy.Manager.Models;
using Servy.Manager.Utils;
using Servy.Testing;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Manager.UnitTests.Utils
{
    /// <summary>
    /// Pins what <see cref="LogTailer"/> writes to the static <see cref="Servy.Core.Logging.Logger"/>.
    /// Kept apart from <see cref="LogTailerTests"/> because <see cref="LogCapture"/> swaps that static logger,
    /// so these tests must not run beside other tests that do the same.
    /// </summary>
    [Collection(AmbientTestCollection.Name)]
    public class LogTailerLogTests : IDisposable
    {
        private readonly string _tempFilePath;

        public LogTailerLogTests()
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), $"logtailer_log_test_{Guid.NewGuid()}.log");
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_tempFilePath))
                {
                    File.Delete(_tempFilePath);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup; a leftover temp file must not fail the test.
            }
        }

        [Fact]
        public async Task RunFromPosition_SubscriberFaults_LogsTheFirstOccurrenceWithThePathAndTheException()
        {
            // Arrange
            File.WriteAllText(_tempFilePath, "only line\n");
            var fileInfo = new FileInfo(_tempFilePath);

            using (var tailer = new LogTailer())
            using (var cts = new CancellationTokenSource())
            {
                int throwOnce = 1;
                tailer.OnNewLines += (lines) =>
                {
                    if (Interlocked.Exchange(ref throwOnce, 0) == 1)
                    {
                        throw new InvalidOperationException("Simulated subscriber fault.");
                    }
                };

                // The faulted pass never reaches OnLoopCompleted, so the first signal is the pass after the recovery back-off.
                var recoveredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                tailer.OnLoopCompleted += () => recoveredTcs.TrySetResult(true);

                // Act
                var log = await LogCapture.RunAsync(async () =>
                {
                    var tailTask = tailer.RunFromPositionAsync(_tempFilePath, LogType.StdOut, 0, fileInfo.CreationTimeUtc, cts.Token);

                    await Helper.WaitUntilAsync(
                        () => recoveredTcs.Task.IsCompleted,
                        TimeSpan.FromSeconds(TestTimeouts.LogTailerErrorRecoveryWaitSeconds),
                        cancellationToken: CancellationToken.None);

                    cts.Cancel();
                    try { await tailTask; } catch (OperationCanceledException) { }
                });

                // Assert - the fault must actually have fired, or nothing below is meaningful
                Assert.Equal(0, Volatile.Read(ref throwOnce));

                // Assert - one error line naming the file, the occurrence number and the cause
                Assert.Contains($"Unexpected error in log tailer for {_tempFilePath} (occurrence #1, throttled).", log);
                Assert.Contains("Simulated subscriber fault.", log);
            }
        }
    }
}
