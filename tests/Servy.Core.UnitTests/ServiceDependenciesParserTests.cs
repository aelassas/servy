using Servy.Core.ServiceDependencies;
using Xunit;

namespace Servy.Core.UnitTests
{
    public class ServiceDependenciesParserTests
    {
        [Fact]
        public void Parse_NullInput_ReturnsNull()
        {
            string result = ServiceDependenciesParser.Parse(null);
            Assert.Null(result);
        }

        [Fact]
        public void Parse_EmptyString_ReturnsNull()
        {
            string result = ServiceDependenciesParser.Parse(string.Empty);
            Assert.Null(result);
        }

        [Fact]
        public void Parse_WhitespaceOnly_ReturnsNull()
        {
            string result = ServiceDependenciesParser.Parse("   \r\n  ");
            Assert.Null(result);
        }

        [Fact]
        public void Parse_SingleName_ReturnsNameWithDoubleNullTermination()
        {
            string result = ServiceDependenciesParser.Parse("ServiceA");
            Assert.Equal("ServiceA\0\0", result);
        }

        [Fact]
        public void Parse_SingleNameWithExtraSpaces_ReturnsTrimmed()
        {
            string result = ServiceDependenciesParser.Parse("   ServiceA   ");
            Assert.Equal("ServiceA\0\0", result);
        }

        [Fact]
        public void Parse_MultipleNamesSeparatedBySemicolon_ReturnsNullSeparatedString()
        {
            string result = ServiceDependenciesParser.Parse("ServiceA;ServiceB;ServiceC");
            Assert.Equal("ServiceA\0ServiceB\0ServiceC\0\0", result);
        }

        [Fact]
        public void Parse_MultipleNamesSeparatedByNewlines_ReturnsNullSeparatedString()
        {
            string result = ServiceDependenciesParser.Parse("ServiceA\r\nServiceB\nServiceC");
            Assert.Equal("ServiceA\0ServiceB\0ServiceC\0\0", result);
        }

        [Fact]
        public void Parse_MixedSeparatorsAndExtraSpaces_ReturnsTrimmedAndNullSeparatedString()
        {
            string result = ServiceDependenciesParser.Parse(" ServiceA ;\n ServiceB  ;\r\nServiceC ");
            Assert.Equal("ServiceA\0ServiceB\0ServiceC\0\0", result);
        }

        [Fact]
        public void Parse_EmptyEntriesBetweenSeparators_AreIgnored()
        {
            string result = ServiceDependenciesParser.Parse("ServiceA;;\n\nServiceB;");
            Assert.Equal("ServiceA\0ServiceB\0\0", result);
        }

        [Fact]
        public void Parse_AllEmptyEntries_ReturnsNull()
        {
            string result = ServiceDependenciesParser.Parse(";;\n\n;;");
            Assert.Null(result);
        }

        [Fact]
        public void Parse_TrailingSeparator_StillEndsWithDoubleNull()
        {
            string result = ServiceDependenciesParser.Parse("ServiceA;ServiceB;");
            Assert.Equal("ServiceA\0ServiceB\0\0", result);
        }

        [Fact]
        public void Parse_SingleNameWithDifferentLineEndings_StillWorks()
        {
            string result = ServiceDependenciesParser.Parse("ServiceA\rServiceB");
            Assert.Equal("ServiceA\0ServiceB\0\0", result);
        }
    }
}
