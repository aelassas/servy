using Servy.Infrastructure.Data;
using System;
using Xunit;

namespace Servy.Infrastructure.UnitTests.Data
{
    public class AppDbContextTests
    {
        private const string InMemoryConnectionString = "Data Source=:memory:";

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
