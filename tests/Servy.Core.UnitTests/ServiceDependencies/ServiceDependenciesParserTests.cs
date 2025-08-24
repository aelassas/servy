using Servy.Core.ServiceDependencies;
using Xunit;

namespace Servy.Core.UnitTests.ServiceDependencies
{
    public class ServiceDependenciesParserTests
    {
        [Fact]
        public void Parse_NullInput_ReturnsNull()
        {
            var result = ServiceDependenciesParser.Parse(null);
            Assert.Equal(ServiceDependenciesParser.NoDependencies, result);
        }

        [Fact]
        public void Parse_EmptyString_ReturnsNull()
        {
            var result = ServiceDependenciesParser.Parse(string.Empty);
            Assert.Equal(ServiceDependenciesParser.NoDependencies, result);
        }

        [Fact]
        public void Parse_WhitespaceOnly_ReturnsNull()
        {
            var result = ServiceDependenciesParser.Parse("   \r\n  ");
            Assert.Equal(ServiceDependenciesParser.NoDependencies, result);
        }

        [Fact]
        public void Parse_SingleName_ReturnsNameWithDoubleNullTermination()
        {
            var result = ServiceDependenciesParser.Parse("ServiceA");
            Assert.Equal("ServiceA\0\0", result);
        }

        [Fact]
        public void Parse_SingleNameWithExtraSpaces_ReturnsTrimmed()
        {
            var result = ServiceDependenciesParser.Parse("   ServiceA   ");
            Assert.Equal("ServiceA\0\0", result);
        }

        [Fact]
        public void Parse_MultipleNamesSeparatedBySemicolon_ReturnsNullSeparatedString()
        {
            var result = ServiceDependenciesParser.Parse("ServiceA;ServiceB;ServiceC");
            Assert.Equal("ServiceA\0ServiceB\0ServiceC\0\0", result);
        }

        [Fact]
        public void Parse_MultipleNamesSeparatedByNewlines_ReturnsNullSeparatedString()
        {
            var result = ServiceDependenciesParser.Parse("ServiceA\r\nServiceB\nServiceC");
            Assert.Equal("ServiceA\0ServiceB\0ServiceC\0\0", result);
        }

        [Fact]
        public void Parse_MixedSeparatorsAndExtraSpaces_ReturnsTrimmedAndNullSeparatedString()
        {
            var result = ServiceDependenciesParser.Parse(" ServiceA ;\n ServiceB  ;\r\nServiceC ");
            Assert.Equal("ServiceA\0ServiceB\0ServiceC\0\0", result);
        }

        [Fact]
        public void Parse_EmptyEntriesBetweenSeparators_AreIgnored()
        {
            var result = ServiceDependenciesParser.Parse("ServiceA;;\n\nServiceB;");
            Assert.Equal("ServiceA\0ServiceB\0\0", result);
        }

        [Fact]
        public void Parse_AllEmptyEntries_ReturnsNull()
        {
            var result = ServiceDependenciesParser.Parse(";;\n\n;;");
            Assert.Equal(ServiceDependenciesParser.NoDependencies, result);
        }

        [Fact]
        public void Parse_TrailingSeparator_StillEndsWithDoubleNull()
        {
            var result = ServiceDependenciesParser.Parse("ServiceA;ServiceB;");
            Assert.Equal("ServiceA\0ServiceB\0\0", result);
        }

        [Fact]
        public void Parse_SingleNameWithDifferentLineEndings_StillWorks()
        {
            var result = ServiceDependenciesParser.Parse("ServiceA\rServiceB");
            Assert.Equal("ServiceA\0ServiceB\0\0", result);
        }
    }
}
