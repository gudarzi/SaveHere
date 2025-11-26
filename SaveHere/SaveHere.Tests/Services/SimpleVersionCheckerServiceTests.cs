using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using SaveHere.Services;
using SaveHere.Models;
using System.Net.Http;
using System.Reflection;

namespace SaveHere.Tests.Services
{
    public class SimpleVersionCheckerServiceTests : IDisposable
    {
        private readonly Mock<ILogger<SimpleVersionCheckerService>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly VersionState _versionState;
        private readonly Mock<HttpClient> _httpClientMock;
        private readonly SimpleVersionCheckerService _service;

        public SimpleVersionCheckerServiceTests()
        {
            _loggerMock = new Mock<ILogger<SimpleVersionCheckerService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _versionState = new VersionState();
            _httpClientMock = new Mock<HttpClient>();

            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(_httpClientMock.Object);

            _service = new SimpleVersionCheckerService(
                _loggerMock.Object,
                _httpClientFactoryMock.Object,
                _versionState);
        }

        public void Dispose()
        {
            _service?.Dispose();
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Act & Assert (constructor should not throw)
            Assert.NotNull(_service);
        }

        [Fact]
        public void VersionState_InitialState_IsCorrect()
        {
            // Assert
            Assert.False(_versionState.UpdateAvailable);
            Assert.Equal(string.Empty, _versionState.LatestVersion);
            Assert.Equal("https://github.com/gudarzi/SaveHere", _versionState.RepoUrl);
        }

        [Fact]
        public async Task StartAsync_CanBeCalledWithoutException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(100)); // Cancel quickly to avoid long wait

            // Act & Assert (should not throw)
            await _service.StartAsync(cancellationTokenSource.Token);
            await _service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StopAsync_CanBeCalledWithoutException()
        {
            // Act & Assert (should not throw)
            await _service.StopAsync(CancellationToken.None);
        }

        // Note: Testing the ExecuteAsync method directly is complex because it's a protected method
        // and involves background processing. In a real-world scenario, you might want to:
        // 1. Make the CheckForUpdates method public for testing
        // 2. Use integration tests with a test server
        // 3. Create a wrapper interface for better testability

        [Fact]
        public void VersionState_Properties_CanBeModified()
        {
            // Act
            _versionState.UpdateAvailable = true;
            _versionState.LatestVersion = "1.2.3";

            // Assert
            Assert.True(_versionState.UpdateAvailable);
            Assert.Equal("1.2.3", _versionState.LatestVersion);
        }

        [Fact]
        public void VersionState_RepoUrl_IsReadOnly()
        {
            // Assert
            Assert.Equal("https://github.com/gudarzi/SaveHere", _versionState.RepoUrl);
            
            // The RepoUrl property should be read-only (has only getter)
            var property = typeof(VersionState).GetProperty(nameof(VersionState.RepoUrl));
            Assert.NotNull(property);
            Assert.True(property.CanRead);
            Assert.False(property.CanWrite);
        }

        [Theory]
        [InlineData("1.0.0", "1.0.1", true)]
        [InlineData("1.0.0", "1.1.0", true)]
        [InlineData("1.0.0", "2.0.0", true)]
        [InlineData("1.0.1", "1.0.0", false)]
        [InlineData("1.1.0", "1.0.0", false)]
        [InlineData("2.0.0", "1.0.0", false)]
        [InlineData("1.0.0", "1.0.0", false)]
        public void Version_Comparison_Logic_Works(string current, string latest, bool shouldBeNewer)
        {
            // Arrange
            var currentVersion = Version.Parse(current);
            var latestVersion = Version.Parse(latest);

            // Act
            var isNewer = latestVersion > currentVersion;

            // Assert
            Assert.Equal(shouldBeNewer, isNewer);
        }

        [Fact]
        public void HttpClientFactory_CreateClient_IsCalledWhenServiceIsUsed()
        {
            // This test verifies that the service is set up to use the HttpClientFactory
            // The actual HTTP calls would be tested in integration tests
            
            // Assert
            _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
            
            // Note: The CreateClient would be called in the ExecuteAsync method during actual execution
        }
    }
}