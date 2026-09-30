using Servy.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Servy.Core.DTOs;

namespace Servy.Core.Data
{
    /// <summary>
    /// Reads and writes the runtime state of a service in the runtime-state database
    /// (<c>Servy.state.db</c>).
    /// </summary>
    /// <remarks>
    /// This seam exists so the runtime state a wrapper writes back about itself is stored apart from
    /// the configuration a wrapper reads. Implementations address rows by service name; there is at
    /// most one row per service.
    /// </remarks>
    public interface IServiceStateRepository
    {
        /// <summary>
        /// Gets the runtime state of one service.
        /// </summary>
        /// <param name="name">The Windows service name.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task whose result is the stored state, or <see langword="null"/> when the service has no
        /// runtime-state row.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty or whitespace.</exception>
        Task<ServiceStateDto> GetAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the runtime state of every service that has one.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task whose result is every stored runtime-state row, ordered by service name.</returns>
        Task<IEnumerable<ServiceStateDto>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes the runtime state of one service, inserting the row when it does not exist yet and
        /// replacing it when it does.
        /// </summary>
        /// <param name="state">The state to write. Its <see cref="ServiceStateDto.Name"/> selects the row.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task whose result is the number of rows written, which is always 1 on success.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the state's name is null, empty or whitespace.</exception>
        Task<int> UpsertAsync(ServiceStateDto state, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the runtime-state row of one service.
        /// </summary>
        /// <param name="name">The Windows service name.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task whose result is the number of rows removed: 1 when a row existed, 0 when it did not.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty or whitespace.</exception>
        Task<int> DeleteAsync(string name, CancellationToken cancellationToken = default);
    }
}
