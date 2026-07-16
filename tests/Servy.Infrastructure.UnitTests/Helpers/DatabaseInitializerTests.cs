using Moq;
using Moq.Protected;
using Servy.Core.Data;
using Servy.Infrastructure.Helpers;
using System.Data.Common;

namespace Servy.Infrastructure.UnitTests.Helpers
{
    public class DatabaseInitializerTests
    {
        [Fact]
        public void InitializeDatabase_Throws_WhenDbContextIsNull()
        {
            // Arrange
            // No preparation needed for null instance validation paths

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                DatabaseInitializer.InitializeDatabase(null!, conn => { }));
        }

        [Fact]
        public void InitializeDatabase_Throws_WhenInitializerIsNull()
        {
            // Arrange
            var mockDbContext = new Mock<IAppDbContext>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                DatabaseInitializer.InitializeDatabase(mockDbContext.Object, null!));
        }

        [Fact]
        public void InitializeDatabase_CallsInitializer_WithConnection()
        {
            // Arrange
            var mockConnection = new Mock<DbConnection>();
            var mockDbContext = new Mock<IAppDbContext>();
            mockDbContext.Setup(c => c.CreateConnection()).Returns(mockConnection.Object);

            var initializerCalled = false;

            // Act
            DatabaseInitializer.InitializeDatabase(mockDbContext.Object, conn =>
            {
                // Assert (Inline parameter validation check)
                Assert.Equal(mockConnection.Object, conn);
                initializerCalled = true;
            });

            // Assert
            Assert.True(initializerCalled);
            mockConnection.Verify(c => c.Open(), Times.Once);
        }

        [Fact]
        public void InitializeDatabase_DisposesConnection_WhenInitializerThrows()
        {
            // Arrange
            var mockConnection = new Mock<DbConnection>();
            var mockDbContext = new Mock<IAppDbContext>();
            mockDbContext.Setup(c => c.CreateConnection()).Returns(mockConnection.Object);

            // Act
            Action act = () => DatabaseInitializer.InitializeDatabase(mockDbContext.Object,
                       _ => throw new InvalidOperationException("Boom!"));

            // Assert
            Assert.Throws<InvalidOperationException>(act);
            mockConnection.Protected().Verify("Dispose", Times.Once(), true, ItExpr.IsAny<bool>());
        }
    }
}