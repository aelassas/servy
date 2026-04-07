using Moq;
using Servy.Core.Data;
using Servy.Infrastructure.Helpers;
using System;
using System.Data.Common;
using Xunit;

namespace Servy.Infrastructure.UnitTests.Helpers
{
    public class DatabaseInitializerTests
    {
        [Fact]
        public void InitializeDatabase_Throws_WhenDbContextIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                DatabaseInitializer.InitializeDatabase(null, conn => { }));
        }

        [Fact]
        public void InitializeDatabase_Throws_WhenInitializerIsNull()
        {
            var mockDbContext = new Mock<IAppDbContext>();
            Assert.Throws<ArgumentNullException>(() =>
                DatabaseInitializer.InitializeDatabase(mockDbContext.Object, null));
        }

        [Fact]
        public void InitializeDatabase_CallsInitializer_WithConnection()
        {
            var mockConnection = new Mock<DbConnection>();
            var mockDbContext = new Mock<IAppDbContext>();
            mockDbContext.Setup(c => c.CreateConnection()).Returns(mockConnection.Object);

            var initializerCalled = false;
            DatabaseInitializer.InitializeDatabase(mockDbContext.Object, conn =>
            {
                Assert.Equal(mockConnection.Object, conn);
                initializerCalled = true;
            });

            Assert.True(initializerCalled);
            mockConnection.Verify(c => c.Open(), Times.Once);
        }
    }
}
