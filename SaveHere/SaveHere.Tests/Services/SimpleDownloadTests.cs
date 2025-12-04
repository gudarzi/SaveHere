using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using SaveHere.Models;
using SaveHere.Models.db;
using SaveHere.Services;
using Xunit;

namespace SaveHere.Tests.Services
{
    public class SimpleDownloadTests : IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly Mock<IDbContextFactory<AppDbContext>> _contextFactoryMock;
        private readonly Mock<ILogger<DownloadQueueService>> _loggerMock;
        private readonly Mock<IProgressHubService> _progressHubMock;
        private readonly DownloadStateService _downloadStateService;
        private readonly string _databaseName;

        public SimpleDownloadTests()
        {
            _databaseName = Guid.NewGuid().ToString();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;

            _contextFactoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            _contextFactoryMock.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new AppDbContext(_options));

            _loggerMock = new Mock<ILogger<DownloadQueueService>>();
            _progressHubMock = new Mock<IProgressHubService>();
            _downloadStateService = new DownloadStateService();
        }

        [Fact]
        public async Task CheckRangeSupport_WithAcceptRangesHeader_ShouldReturnTrue()
        {
            // Arrange
            var httpMessageHandler = new Mock<HttpMessageHandler>();
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Headers = { AcceptRanges = { "bytes" } }
                });

            var httpClient = new HttpClient(httpMessageHandler.Object);
            var service = new DownloadQueueService(
                _contextFactoryMock.Object,
                _downloadStateService,
                httpClient,
                _loggerMock.Object,
                _progressHubMock.Object);

            // Act
            var result = await service.CheckRangeSupport("http://example.com/file.zip", CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CheckRangeSupport_WithoutRangeSupport_ShouldReturnFalse()
        {
            // Arrange
            var httpMessageHandler = new Mock<HttpMessageHandler>();
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                    // No AcceptRanges header
                });

            var httpClient = new HttpClient(httpMessageHandler.Object);
            var service = new DownloadQueueService(
                _contextFactoryMock.Object,
                _downloadStateService,
                httpClient,
                _loggerMock.Object,
                _progressHubMock.Object);

            // Act
            var result = await service.CheckRangeSupport("http://example.com/file.zip", CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task AddQueueItem_ShouldCreateItemWithDefaultSettings()
        {
            // Arrange
            var httpClient = new HttpClient();
            var service = new DownloadQueueService(
                _contextFactoryMock.Object,
                _downloadStateService,
                httpClient,
                _loggerMock.Object,
                _progressHubMock.Object);

            var url = "http://example.com/file.zip";

            // Act
            var queueItem = await service.AddQueueItemAsync(url);

            // Assert
            Assert.NotNull(queueItem);
            Assert.Equal(url, queueItem.InputUrl);
            Assert.Equal(EQueueItemStatus.Paused, queueItem.Status);
            Assert.Equal(1, queueItem.ParallelConnections);
            Assert.Equal(80, queueItem.BufferSizeKB);
            Assert.True(queueItem.UseHttp2);
            Assert.True(queueItem.EnableCompression);
        }

        [Fact]
        public void DownloadSettings_ShouldHaveCorrectDefaults()
        {
            // Arrange & Act
            var settings = new DownloadSettings();

            // Assert
            Assert.Equal(1, settings.ParallelConnections);
            Assert.Equal(80, settings.BufferSizeKB);
            Assert.True(settings.UseHttp2);
            Assert.True(settings.EnableCompression);
            Assert.Equal(3, settings.MaxRetryAttempts);
            Assert.Equal(2, settings.RetryDelaySeconds);
            Assert.Equal(80 * 1024, settings.GetBufferSizeBytes());
        }

        [Theory]
        [InlineData(80, 81920)]   // 80KB = 81920 bytes
        [InlineData(256, 262144)] // 256KB = 262144 bytes
        [InlineData(1024, 1048576)] // 1MB = 1048576 bytes
        public void BufferSize_ShouldCalculateCorrectly(int sizeKB, int expectedBytes)
        {
            // Arrange
            var item = new FileDownloadQueueItem
            {
                BufferSizeKB = sizeKB
            };

            // Act
            var actualBytes = item.BufferSizeKB * 1024;

            // Assert
            Assert.Equal(expectedBytes, actualBytes);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
