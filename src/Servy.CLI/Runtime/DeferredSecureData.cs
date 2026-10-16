using Servy.Core.Security;
using System;

namespace Servy.CLI.Runtime
{
    /// <summary>
    /// An <see cref="ISecureData"/> that builds the real one on the first <see cref="Encrypt"/> or <see cref="Decrypt"/>,
    /// not when the CLI composes its commands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Building <see cref="SecureData"/> reads the AES key through <see cref="ProtectedKeyProvider"/>, which creates the
    /// key file when it is missing. The CLI composes every command before it parses the arguments, and each command checks
    /// <see cref="SecurityHelper.EnsureAdministrator"/> only when it runs, so an eager build let an unelevated
    /// <c>servy-cli --help</c> create the key and grant the calling user Full Control on it (#7531).
    /// </para>
    /// <para>
    /// No CLI path encrypts or decrypts before the command's elevation check, so the key is now resolved only after it.
    /// Once built, every call is forwarded unchanged to the instance <c>factory</c> returned.
    /// </para>
    /// </remarks>
    internal sealed class DeferredSecureData : ISecureData
    {
        /// <summary>
        /// Builds the real <see cref="ISecureData"/> on first use.
        /// </summary>
        private readonly Func<ISecureData> _factory;

        /// <summary>
        /// Guards <see cref="_inner"/> and <see cref="_disposed"/>, so concurrent first calls build one instance.
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// The instance <see cref="_factory"/> built, or <see langword="null"/> while nothing was encrypted or decrypted.
        /// </summary>
        private ISecureData _inner;

        /// <summary>
        /// Whether <see cref="Dispose"/> ran.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeferredSecureData"/> class.
        /// </summary>
        /// <param name="factory">Builds the real <see cref="ISecureData"/>; called at most once, on the first use.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <see langword="null"/>.</exception>
        public DeferredSecureData(Func<ISecureData> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Gets a value indicating whether the real <see cref="ISecureData"/> was built.
        /// </summary>
        internal bool IsCreated
        {
            get
            {
                lock (_sync)
                {
                    return _inner != null;
                }
            }
        }

        /// <summary>
        /// Encrypts <paramref name="plainText"/> with the real <see cref="ISecureData"/>, building it first when needed.
        /// </summary>
        /// <param name="plainText">The plain text to encrypt.</param>
        /// <returns>What the real <see cref="ISecureData.Encrypt"/> returns.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance was disposed.</exception>
        public string Encrypt(string plainText) => GetInner().Encrypt(plainText);

        /// <summary>
        /// Decrypts <paramref name="cipherText"/> with the real <see cref="ISecureData"/>, building it first when needed.
        /// </summary>
        /// <param name="cipherText">The ciphertext to decrypt.</param>
        /// <returns>What the real <see cref="ISecureData.Decrypt"/> returns.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance was disposed.</exception>
        public string Decrypt(string cipherText) => GetInner().Decrypt(cipherText);

        /// <summary>
        /// Disposes the real <see cref="ISecureData"/> when it was built. Never builds it.
        /// </summary>
        public void Dispose()
        {
            ISecureData inner;
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                inner = _inner;
                _inner = null;
            }

            inner?.Dispose();
        }

        /// <summary>
        /// Returns the real <see cref="ISecureData"/>, building it on the first call.
        /// </summary>
        /// <returns>The instance <see cref="_factory"/> built.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance was disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the factory returns <see langword="null"/>.</exception>
        private ISecureData GetInner()
        {
            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DeferredSecureData));

                if (_inner == null)
                {
                    _inner = _factory() ?? throw new InvalidOperationException("The secure data factory returned null.");
                }

                return _inner;
            }
        }
    }
}
