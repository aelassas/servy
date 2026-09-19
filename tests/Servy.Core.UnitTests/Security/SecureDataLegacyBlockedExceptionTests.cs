using Servy.Core.Security;
using System;
using Xunit;

namespace Servy.Core.UnitTests.Security
{
    public class SecureDataLegacyBlockedExceptionTests
    {
        [Fact]
        public void Constructor_MessageOnly_SetsMessage()
        {
            // Act
            var ex = new SecureDataLegacyBlockedException("v1 payload refused by policy");

            // Assert
            Assert.Equal("v1 payload refused by policy", ex.Message);
            Assert.Null(ex.InnerException);
        }

        [Fact]
        public void Constructor_MessageAndInnerException_SetsBoth()
        {
            // Arrange
            var inner = new InvalidOperationException("legacy path disabled");

            // Act
            var ex = new SecureDataLegacyBlockedException("v1 payload refused by policy", inner);

            // Assert
            Assert.Equal("v1 payload refused by policy", ex.Message);
            Assert.Same(inner, ex.InnerException);
        }
    }
}
