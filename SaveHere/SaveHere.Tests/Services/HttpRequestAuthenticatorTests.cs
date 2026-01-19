using Xunit;
using SaveHere.Helpers;
using SaveHere.Models;
using System.Net.Http;
using System.Text;

namespace SaveHere.Tests.Services
{
    public class HttpRequestAuthenticatorTests
    {
        [Fact]
        public void ApplyAuthentication_BasicAuth_SetsAuthorizationHeader()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file.zip");
            var queueItem = new FileDownloadQueueItem
            {
                InputUrl = "https://example.com/file.zip",
                AuthType = AuthenticationType.BasicAuth,
                AuthUsername = "testuser",
                AuthPassword = "testpass"
            };

            // Act
            HttpRequestAuthenticator.ApplyAuthentication(request, queueItem);

            // Assert
            Assert.NotNull(request.Headers.Authorization);
            Assert.Equal("Basic", request.Headers.Authorization.Scheme);
            
            // Verify the credentials are correctly encoded
            var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("testuser:testpass"));
            Assert.Equal(expectedCredentials, request.Headers.Authorization.Parameter);
        }

        [Fact]
        public void ApplyAuthentication_BasicAuth_WithEmptyPassword_SetsAuthorizationHeader()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file.zip");
            var queueItem = new FileDownloadQueueItem
            {
                InputUrl = "https://example.com/file.zip",
                AuthType = AuthenticationType.BasicAuth,
                AuthUsername = "testuser",
                AuthPassword = null
            };

            // Act
            HttpRequestAuthenticator.ApplyAuthentication(request, queueItem);

            // Assert
            Assert.NotNull(request.Headers.Authorization);
            Assert.Equal("Basic", request.Headers.Authorization.Scheme);
            
            // Verify the credentials are correctly encoded with empty password
            var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("testuser:"));
            Assert.Equal(expectedCredentials, request.Headers.Authorization.Parameter);
        }

        [Fact]
        public void ApplyAuthentication_BasicAuth_WithEmptyUsername_DoesNotSetHeader()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file.zip");
            var queueItem = new FileDownloadQueueItem
            {
                InputUrl = "https://example.com/file.zip",
                AuthType = AuthenticationType.BasicAuth,
                AuthUsername = "",
                AuthPassword = "testpass"
            };

            // Act
            HttpRequestAuthenticator.ApplyAuthentication(request, queueItem);

            // Assert
            Assert.Null(request.Headers.Authorization);
        }

        [Fact]
        public void ApplyAuthentication_BearerToken_SetsAuthorizationHeader()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file.zip");
            var queueItem = new FileDownloadQueueItem
            {
                InputUrl = "https://example.com/file.zip",
                AuthType = AuthenticationType.BearerToken,
                AuthBearerToken = "my-jwt-token-12345"
            };

            // Act
            HttpRequestAuthenticator.ApplyAuthentication(request, queueItem);

            // Assert
            Assert.NotNull(request.Headers.Authorization);
            Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
            Assert.Equal("my-jwt-token-12345", request.Headers.Authorization.Parameter);
        }

        [Fact]
        public void ApplyAuthentication_BearerToken_WithEmptyToken_DoesNotSetHeader()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file.zip");
            var queueItem = new FileDownloadQueueItem
            {
                InputUrl = "https://example.com/file.zip",
                AuthType = AuthenticationType.BearerToken,
                AuthBearerToken = ""
            };

            // Act
            HttpRequestAuthenticator.ApplyAuthentication(request, queueItem);

            // Assert
            Assert.Null(request.Headers.Authorization);
        }

        [Fact]
        public void ApplyAuthentication_Cookie_SetsCookieHeader()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file.zip");
            var queueItem = new FileDownloadQueueItem
            {
                InputUrl = "https://example.com/file.zip",
                AuthType = AuthenticationType.Cookie,
                AuthCookies = "session_id=abc123; user_token=xyz789"
            };

            // Act
            HttpRequestAuthenticator.ApplyAuthentication(request, queueItem);

            // Assert
            Assert.True(request.Headers.Contains("Cookie"));
            var cookieValues = request.Headers.GetValues("Cookie");
            Assert.Contains("session_id=abc123; user_token=xyz789", cookieValues);
        }

        [Fact]
        public void ApplyAuthentication_Cookie_WithEmptyCookies_DoesNotSetHeader()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file.zip");
            var queueItem = new FileDownloadQueueItem
            {
                InputUrl = "https://example.com/file.zip",
                AuthType = AuthenticationType.Cookie,
                AuthCookies = ""
            };

            // Act
            HttpRequestAuthenticator.ApplyAuthentication(request, queueItem);

            // Assert
            Assert.False(request.Headers.Contains("Cookie"));
        }

        [Fact]
        public void ApplyAuthentication_CustomHeaders_SetsMultipleHeaders()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file.zip");
            var queueItem = new FileDownloadQueueItem
            {
                InputUrl = "https://example.com/file.zip",
                AuthType = AuthenticationType.CustomHeaders,
                AuthCustomHeaders = "{\"X-Api-Key\": \"my-api-key\", \"X-Custom-Auth\": \"custom-value\"}"
            };

            // Act
            HttpRequestAuthenticator.ApplyAuthentication(request, queueItem);

            // Assert
            Assert.True(request.Headers.Contains("X-Api-Key"));
            Assert.True(request.Headers.Contains("X-Custom-Auth"));
            Assert.Equal("my-api-key", request.Headers.GetValues("X-Api-Key").First());
            Assert.Equal("custom-value", request.Headers.GetValues("X-Custom-Auth").First());
        }

        [Fact]
        public void ApplyAuthentication_CustomHeaders_WithInvalidJson_DoesNotThrow()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file.zip");
            var queueItem = new FileDownloadQueueItem
            {
                InputUrl = "https://example.com/file.zip",
                AuthType = AuthenticationType.CustomHeaders,
                AuthCustomHeaders = "not valid json"
            };

            // Act & Assert - should not throw
            var exception = Record.Exception(() => HttpRequestAuthenticator.ApplyAuthentication(request, queueItem));
            Assert.Null(exception);
        }

        [Fact]
        public void ApplyAuthentication_CustomHeaders_WithEmptyHeaders_DoesNotThrow()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file.zip");
            var queueItem = new FileDownloadQueueItem
            {
                InputUrl = "https://example.com/file.zip",
                AuthType = AuthenticationType.CustomHeaders,
                AuthCustomHeaders = ""
            };

            // Act & Assert - should not throw
            var exception = Record.Exception(() => HttpRequestAuthenticator.ApplyAuthentication(request, queueItem));
            Assert.Null(exception);
        }

        [Fact]
        public void ApplyAuthentication_None_DoesNotModifyRequest()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file.zip");
            var queueItem = new FileDownloadQueueItem
            {
                InputUrl = "https://example.com/file.zip",
                AuthType = AuthenticationType.None
            };

            // Act
            HttpRequestAuthenticator.ApplyAuthentication(request, queueItem);

            // Assert
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("Cookie"));
        }

        [Fact]
        public void ApplyAuthentication_NullRequest_DoesNotThrow()
        {
            // Arrange
            var queueItem = new FileDownloadQueueItem
            {
                InputUrl = "https://example.com/file.zip",
                AuthType = AuthenticationType.BasicAuth,
                AuthUsername = "user",
                AuthPassword = "pass"
            };

            // Act & Assert - should not throw
            var exception = Record.Exception(() => HttpRequestAuthenticator.ApplyAuthentication(null!, queueItem));
            Assert.Null(exception);
        }

        [Fact]
        public void ApplyAuthentication_NullQueueItem_DoesNotThrow()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/file.zip");

            // Act & Assert - should not throw
            var exception = Record.Exception(() => HttpRequestAuthenticator.ApplyAuthentication(request, null!));
            Assert.Null(exception);
        }

        [Theory]
        [InlineData(AuthenticationType.BasicAuth, true)]
        [InlineData(AuthenticationType.BearerToken, true)]
        [InlineData(AuthenticationType.Cookie, true)]
        [InlineData(AuthenticationType.CustomHeaders, true)]
        [InlineData(AuthenticationType.None, false)]
        public void HasAuthentication_ReturnsCorrectValue_ForAuthType(AuthenticationType authType, bool expected)
        {
            // Arrange
            var queueItem = new FileDownloadQueueItem
            {
                AuthType = authType
            };

            // Assert
            Assert.Equal(expected, queueItem.HasAuthentication);
        }
    }
}
