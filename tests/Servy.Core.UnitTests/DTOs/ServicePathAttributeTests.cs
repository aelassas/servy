using Servy.Core.DTOs;

namespace Servy.Core.UnitTests.DTOs
{
    public class ServicePathAttributeTests
    {
        [Fact]
        public void Constructor_ValidLabel_SetsProperties()
        {
            // Act
            var attribute = new ServicePathAttribute("executable path", isFile: false, required: true, errorResourceKey: "Msg_InvalidPath");

            // Assert
            Assert.Equal("executable path", attribute.Label);
            Assert.False(attribute.IsFile);
            Assert.True(attribute.Required);
            Assert.Equal("Msg_InvalidPath", attribute.ErrorResourceKey);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_NullEmptyOrWhitespaceLabel_ThrowsArgumentException(string? label)
        {
            // Act
            var ex = Assert.Throws<ArgumentException>(() => new ServicePathAttribute(label!));

            // Assert
            Assert.Equal("label", ex.ParamName);
        }
    }
}
