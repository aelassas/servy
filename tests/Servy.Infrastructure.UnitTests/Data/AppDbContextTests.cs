using Servy.Infrastructure.Data;
using System;
using Xunit;

namespace Servy.Infrastructure.UnitTests.Data
{
    public class AppDbContextTests
    {
        private const string InMemoryConnectionString = "Data Source=:memory:";

        [Fact]
        public void Constructor_NullConnectionString_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new AppDbContext(null!));

            Assert.Equal("connectionString", exception.ParamName);
        }

        [Fact]
        public void CreateConnection_AfterDispose_ThrowsObjectDisposedException()
        {
            var context = new AppDbContext(InMemoryConnectionString);

            context.Dispose();

            Assert.Throws<ObjectDisposedException>(() => context.CreateConnection());
        }

        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var context = new AppDbContext(InMemoryConnectionString);

            context.Dispose();

            var exception = Record.Exception(() => context.Dispose());

            Assert.Null(exception);
        }
    }
}
