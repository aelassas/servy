namespace Servy.Core.Security
{
    /// <summary>
    /// Provides access to the AES encryption key and initialization vector (IV)
    /// used for secure password encryption and decryption.
    /// </summary>
    public interface IProtectedKeyProvider
    {
        /// <summary>
        /// Gets the AES encryption key.
        /// </summary>
        /// <returns>A byte array representing the AES key.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the key cannot be retrieved or generated.
        /// </exception>
        byte[] GetKey();

        /// <summary>
        /// Gets the AES initialization vector (IV).
        /// </summary>
        /// <returns>A byte array representing the AES IV.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the IV cannot be retrieved or generated.
        /// </exception>
        byte[] GetIV();
    }
}
