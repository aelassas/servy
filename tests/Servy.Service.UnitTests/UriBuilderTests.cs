namespace Servy.Service.UnitTests
{
    public class UriBuilderTests
    {
        private static Uri BuildHeartbeatUri(Uri baseUri, string suffix)
        {
            var builder = new UriBuilder(baseUri);

            // UriBuilder keeps the existing query when only the path changes, so the flag lands in the path.
            string currentPath = builder.Path.EndsWith("/") ? builder.Path : builder.Path + "/";
            builder.Path = currentPath + suffix.TrimStart('/');

            return builder.Uri;
        }

        [Theory]
        [InlineData("https://example.com/api", "ping", "https://example.com/api/ping")]
        [InlineData("https://example.com/api/", "ping", "https://example.com/api/ping")]
        [InlineData("https://example.com/api", "/ping", "https://example.com/api/ping")]
        [InlineData("https://example.com/api/", "/ping", "https://example.com/api/ping")]
        [InlineData("https://example.com/api/", "///ping", "https://example.com/api/ping")]
        public void BuildHeartbeatUri_NormalizesSlashesCorrectly(string baseUriStr, string suffix, string expectedUriStr)
        {
            // Arrange
            var baseUri = new Uri(baseUriStr);

            // Act
            var result = BuildHeartbeatUri(baseUri, suffix);

            // Assert
            Assert.Equal(expectedUriStr, result.AbsoluteUri);
        }

        [Fact]
        public void BuildHeartbeatUri_PreservesExistingQueryParameters()
        {
            // Arrange
            var baseUri = new Uri("https://example.com/v1/service?token=abc12345&version=2");
            var suffix = "heartbeat";

            // Act
            var result = BuildHeartbeatUri(baseUri, suffix);

            // Assert
            Assert.Equal("https://example.com/v1/service/heartbeat?token=abc12345&version=2", result.AbsoluteUri);
            Assert.Equal("?token=abc12345&version=2", result.Query);
        }

        [Fact]
        public void BuildHeartbeatUri_HandlesRootBaseUriWithoutPath()
        {
            // Arrange
            var baseUri = new Uri("https://example.com");
            var suffix = "ping";

            // Act
            var result = BuildHeartbeatUri(baseUri, suffix);

            // Assert
            Assert.Equal("https://example.com/ping", result.AbsoluteUri);
        }

        [Fact]
        public void BuildHeartbeatUri_PreservesCustomPortAndScheme()
        {
            // Arrange
            var baseUri = new Uri("http://localhost:8080/health");
            var suffix = "ping";

            // Act
            var result = BuildHeartbeatUri(baseUri, suffix);

            // Assert
            Assert.Equal("http", result.Scheme);
            Assert.Equal(8080, result.Port);
            Assert.Equal("http://localhost:8080/health/ping", result.AbsoluteUri);
        }

        [Fact]
        public void BuildHeartbeatUri_HandlesMultiSegmentSuffix()
        {
            // Arrange
            var baseUri = new Uri("https://example.com/api");
            var suffix = "v1/heartbeat/ping";

            // Act
            var result = BuildHeartbeatUri(baseUri, suffix);

            // Assert
            Assert.Equal("https://example.com/api/v1/heartbeat/ping", result.AbsoluteUri);
        }
    }
}
