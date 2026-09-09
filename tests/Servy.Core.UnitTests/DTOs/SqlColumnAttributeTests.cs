using Servy.Core.DTOs;
using System;
using Xunit;

namespace Servy.Core.UnitTests.DTOs
{
    public class SqlColumnAttributeTests
    {
        [Fact]
        public void Constructor_ValidSqlType_SetsSqlType()
        {
            // Act
            var attribute = new SqlColumnAttribute("TEXT NOT NULL");

            // Assert
            Assert.Equal("TEXT NOT NULL", attribute.SqlType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_NullEmptyOrWhitespaceSqlType_ThrowsArgumentException(string sqlType)
        {
            // Act
            var ex = Assert.Throws<ArgumentException>(() => new SqlColumnAttribute(sqlType));

            // Assert
            Assert.Equal("sqlType", ex.ParamName);
        }
    }
}
