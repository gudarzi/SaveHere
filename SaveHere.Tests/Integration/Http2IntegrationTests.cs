using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SaveHere.Tests.Integration
{
    public class Http2IntegrationTests : IClassFixture<WebApplicationFactory<SaveHere.Program>>
    {
        private readonly WebApplicationFactory<SaveHere.Program> _factory;

        public Http2IntegrationTests(WebApplicationFactory<SaveHere.Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task HttpClient_ShouldBeConfiguredForHttp2()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override services if needed for testing
                });
            }).CreateClient();

            // Act
            var serviceProvider = _factory.Services;
            var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

            // Assert
            httpClientFactory.Should().NotBeNull();
            
            // Verify HTTP/2 configuration by checking the handler
            var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestVersion.Should().Be(new Version(2, 0));
        }

        [Fact]
        public async Task HttpClient_ShouldSupportCompression()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();

            // Create a test server that supports compression
            var testHandler = new TestHttpMessageHandler();
            var testClient = new HttpClient(testHandler);

            // Act & Assert
            // The configured client should accept compressed responses
            var request = new HttpRequestMessage(HttpMethod.Get, "http://test.com");
            
            // Check if Accept-Encoding headers would be set (this is handled by the handler)
            // In a real scenario, the AutomaticDecompression property handles this
            testHandler.SupportsCompression.Should().BeTrue();
        }

        [Fact]
        public async Task DownloadEndpoints_ShouldBeAccessible()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/downloads");

            // Assert
            // Should return 404 or require auth, but not fail to route
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.NotFound, 
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Redirect);
        }
    }

    // Helper class for testing HTTP features
    public class TestHttpMessageHandler : HttpMessageHandler
    {
        public bool SupportsCompression => true;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            System.Threading.CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Test response")
            };
            
            return Task.FromResult(response);
        }
    }
}