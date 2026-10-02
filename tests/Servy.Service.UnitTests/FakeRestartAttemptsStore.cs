using Moq;
using Servy.Core.DTOs;
using Servy.Core.NamedPipes;

namespace Servy.Service.UnitTests
{
    /// <summary>
    /// An in-memory stand-in for the restart attempts counter the Servy host keeps in <c>Servy.db</c>, wired into a
    /// <see cref="Mock{INamedPipesService}"/>: reads return the stored counter and its timestamp, writes store the value
    /// and stamp it with <see cref="Clock"/>, as the host does. Either side can be made to fail.
    /// </summary>
    internal sealed class FakeRestartAttemptsStore
    {
        private readonly object _gate = new object();

        /// <summary>Gets or sets the stored counter.</summary>
        public int Attempts { get; set; }

        /// <summary>Gets or sets the UTC time the counter was last written; <see langword="null"/> when never.</summary>
        public DateTime? UpdatedAtUtc { get; set; }

        /// <summary>Gets or sets the exception every read throws; <see langword="null"/> reads normally.</summary>
        public Exception? ReadFailure { get; set; }

        /// <summary>Gets or sets the exception every write throws; <see langword="null"/> writes normally.</summary>
        public Exception? WriteFailure { get; set; }

        /// <summary>Gets or sets the clock that stamps a write.</summary>
        public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

        /// <summary>Gets the values written, in order.</summary>
        public List<int> Writes { get; } = new List<int>();

        /// <summary>Gets the service names the counter was read or written for.</summary>
        public List<string> ServiceNames { get; } = new List<string>();

        /// <summary>
        /// Wires the store into the pipe mock's restart attempts members.
        /// </summary>
        /// <param name="namedPipesService">The mock the service under test talks to.</param>
        /// <param name="attempts">The initial counter.</param>
        /// <param name="updatedAtUtc">The initial timestamp; defaults to now (written in the current OS session).</param>
        /// <returns>The store.</returns>
        public static FakeRestartAttemptsStore Attach(Mock<INamedPipesService> namedPipesService, int attempts = 0, DateTime? updatedAtUtc = null)
        {
            var store = new FakeRestartAttemptsStore { Attempts = attempts, UpdatedAtUtc = updatedAtUtc ?? DateTime.UtcNow };

            namedPipesService
                .Setup(p => p.GetRestartAttemptsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((name, ct) => store.Read(name));

            namedPipesService
                .Setup(p => p.UpdateRestartAttemptsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns<string, int, CancellationToken>((name, value, ct) => store.Write(name, value));

            return store;
        }

        private Task<RestartAttemptsDto> Read(string name)
        {
            lock (_gate)
            {
                ServiceNames.Add(name);
                if (ReadFailure != null)
                    return Task.FromException<RestartAttemptsDto>(ReadFailure);

                return Task.FromResult(new RestartAttemptsDto { Attempts = Attempts, UpdatedAtUtc = UpdatedAtUtc });
            }
        }

        private Task<int> Write(string name, int value)
        {
            lock (_gate)
            {
                ServiceNames.Add(name);
                if (WriteFailure != null)
                    return Task.FromException<int>(WriteFailure);

                Attempts = value;
                UpdatedAtUtc = Clock();
                Writes.Add(value);
                return Task.FromResult(1);
            }
        }
    }
}
