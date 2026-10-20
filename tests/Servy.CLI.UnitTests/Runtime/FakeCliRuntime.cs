using Servy.CLI.Runtime;
using Servy.Core.Data;
using Servy.Core.Security;
using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.CLI.UnitTests.Runtime
{
    /// <summary>
    /// A recording <see cref="ICliRuntime"/> for <see cref="Program.RunAsync"/> tests: every bootstrap step is logged
    /// in <see cref="Calls"/> and can be made to throw, and the SQLite and console probes return what the test sets.
    /// </summary>
    internal sealed class FakeCliRuntime : ICliRuntime
    {
        /// <summary>Gets the name of every runtime member that was called, in order.</summary>
        public List<string> Calls { get; } = new List<string>();

        /// <summary>Gets or sets the value <see cref="IsSqliteVersionSafe"/> returns.</summary>
        public bool SqliteVersionSafe { get; set; } = true;

        /// <summary>Gets or sets the version <see cref="IsSqliteVersionSafe"/> reports.</summary>
        public string SqliteVersion { get; set; } = "3.50.4";

        /// <summary>Gets or sets the value <see cref="IsAdministrator"/> returns.</summary>
        public bool Elevated { get; set; } = true;

        /// <summary>Gets the number of times <see cref="IsAdministrator"/> was called; it is not logged in <see cref="Calls"/>.</summary>
        public int AdministratorChecks { get; private set; }

        /// <summary>Gets or sets the value <see cref="IsRealConsole"/> returns.</summary>
        public bool RealConsole { get; set; }

        /// <summary>Gets or sets the exception <see cref="EnsureDatabase"/> throws, or <see langword="null"/> to succeed.</summary>
        public Exception EnsureDatabaseThrows { get; set; }

        /// <summary>Gets or sets the exception <see cref="EnsureServiceBinariesAsync"/> throws, or <see langword="null"/> to succeed.</summary>
        public Exception EnsureServiceBinariesThrows { get; set; }

        public bool IsSqliteVersionSafe(out string detectedVersion)
        {
            Calls.Add(nameof(IsSqliteVersionSafe));
            detectedVersion = SqliteVersion;
            return SqliteVersionSafe;
        }

        public bool IsAdministrator()
        {
            AdministratorChecks++;
            return Elevated;
        }

        public void EnsureDatabase(string connectionString, string aesKeyFilePath, string aesIVFilePath, IAppDbContext dbContext, ISecureData secureData)
        {
            Calls.Add(nameof(EnsureDatabase));
            if (EnsureDatabaseThrows != null) throw EnsureDatabaseThrows;
        }

        public Task EnsureServiceBinariesAsync(IServiceRepository serviceRepository, Func<string, IServiceControllerWrapper> controllerFactory, CancellationToken cancellationToken)
        {
            Calls.Add(nameof(EnsureServiceBinariesAsync));
            if (EnsureServiceBinariesThrows != null) throw EnsureServiceBinariesThrows;
            return Task.CompletedTask;
        }

        public bool IsRealConsole()
        {
            Calls.Add(nameof(IsRealConsole));
            return RealConsole;
        }

        public async Task RunWithLoadingAnimationAsync(Func<Task> action)
        {
            Calls.Add(nameof(RunWithLoadingAnimationAsync));
            await action();
        }
    }
}
