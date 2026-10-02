using Servy.Core.Data;
using Servy.Core.Security;

namespace Servy.Host.Bootstrap
{
    /// <summary>
    /// The data-layer objects the production constructor of <see cref="Service"/> creates
    /// once, in one step, and then keeps for the lifetime of the service: the database context, the
    /// protected key provider, the secure-data helper built on it and the service repository built on
    /// both.
    /// </summary>
    /// <remarks>
    /// This type exists so <see cref="IServiceBootstrapEnvironment.CreateDataStack"/> can hand back the
    /// four objects the inline block used to assign to fields directly. It is a carrier only: it creates
    /// nothing, owns no lifetime and disposes nothing, so moving the block behind it does not change
    /// which object disposes what.
    /// </remarks>
    internal sealed class ServiceDataStack
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceDataStack"/> class.
        /// </summary>
        /// <param name="dbContext">The database context the service opens its connections through.</param>
        /// <param name="protectedKeyProvider">The provider of the AES key and IV used to protect stored secrets.</param>
        /// <param name="secureData">The secure-data helper built on <paramref name="protectedKeyProvider"/>.</param>
        /// <param name="serviceRepository">The service repository built on the other three.</param>
        public ServiceDataStack(
            IAppDbContext dbContext,
            ProtectedKeyProvider protectedKeyProvider,
            SecureData secureData,
            IServiceRepository serviceRepository)
        {
            DbContext = dbContext;
            ProtectedKeyProvider = protectedKeyProvider;
            SecureData = secureData;
            ServiceRepository = serviceRepository;
        }

        /// <summary>
        /// Gets the database context the service opens its connections through.
        /// </summary>
        public IAppDbContext DbContext { get; }

        /// <summary>
        /// Gets the provider of the AES key and IV used to protect stored secrets.
        /// </summary>
        public ProtectedKeyProvider ProtectedKeyProvider { get; }

        /// <summary>
        /// Gets the secure-data helper built on <see cref="ProtectedKeyProvider"/>.
        /// </summary>
        public SecureData SecureData { get; }

        /// <summary>
        /// Gets the service repository the service reads its configuration through.
        /// </summary>
        public IServiceRepository ServiceRepository { get; }
    }
}
