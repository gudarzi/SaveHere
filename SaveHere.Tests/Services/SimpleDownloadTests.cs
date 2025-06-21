using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly Mock<ILogger<DownloadQueueService>> _loggerMock;
        private readonly Mock<IProgressHubService> _progressHubMock;
        private readonly DownloadStateService _downloadStateService;
        private readonly string _testDbPath;

        public SimpleDownloadTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
            
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;
            
            _contextFactory = new TestDbContextFactory(options);
            _loggerMock = new Mock<ILogger<DownloadQueueService>>();
            _progressHubMock = new Mock<IProgressHubService>();
            _downloadStateService = new DownloadStateService();
            
            // Initialize database
            using var context = _contextFactory.CreateDbContext();
            context.Database.EnsureCreated();
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
                _contextFactory, 
                _downloadStateService, 
                httpClient, 
                _loggerMock.Object, 
                _progressHubMock.Object);

            // Act
            var result = await service.CheckRangeSupport("http://example.com/file.zip", CancellationToken.None);

            // Assert
            result.Should().BeTrue();
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
                _contextFactory, 
                _downloadStateService, 
                httpClient, 
                _loggerMock.Object, 
                _progressHubMock.Object);

            // Act
            var result = await service.CheckRangeSupport("http://example.com/file.zip", CancellationToken.None);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task AddQueueItem_ShouldCreateItemWithDefaultSettings()
        {
            // Arrange
            var httpClient = new HttpClient();
            var service = new DownloadQueueService(
                _contextFactory, 
                _downloadStateService, 
                httpClient, 
                _loggerMock.Object, 
                _progressHubMock.Object);

            var url = "http://example.com/file.zip";

            // Act
            var queueItem = await service.AddQueueItemAsync(url);

            // Assert
            queueItem.Should().NotBeNull();
            queueItem.InputUrl.Should().Be(url);
            queueItem.Status.Should().Be(EQueueItemStatus.Paused);
            queueItem.ParallelConnections.Should().Be(1);
            queueItem.BufferSizeKB.Should().Be(80);
            queueItem.UseHttp2.Should().BeTrue();
            queueItem.EnableCompression.Should().BeTrue();
        }

        [Fact]
        public void DownloadSettings_ShouldHaveCorrectDefaults()
        {
            // Arrange & Act
            var settings = new DownloadSettings();

            // Assert
            settings.ParallelConnections.Should().Be(1);
            settings.BufferSizeKB.Should().Be(80);
            settings.UseHttp2.Should().BeTrue();
            settings.EnableCompression.Should().BeTrue();
            settings.MaxRetryAttempts.Should().Be(3);
            settings.RetryDelaySeconds.Should().Be(2);
            settings.GetBufferSizeBytes().Should().Be(80 * 1024);
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
            actualBytes.Should().Be(expectedBytes);
        }

        public void Dispose()
        {
            // Clean up test database
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
    }
}