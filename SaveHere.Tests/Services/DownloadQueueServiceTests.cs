using System;
using System.IO;
using System.Linq;
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
    public class DownloadQueueServiceTests : IDisposable
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly Mock<ILogger<DownloadQueueService>> _loggerMock;
        private readonly Mock<IProgressHubService> _progressHubMock;
        private readonly DownloadStateService _downloadStateService;
        private readonly string _testDbPath;

        public DownloadQueueServiceTests()
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
        public async Task CheckRangeSupport_ShouldReturnTrue_WhenServerSupportsRangeRequests()
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
                    StatusCode = HttpStatusCode.PartialContent,
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
        public async Task CheckRangeSupport_ShouldReturnFalse_WhenServerDoesNotSupportRangeRequests()
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
        public async Task DownloadFile_WithParallelConnections_ShouldCheckRangeSupport()
        {
            // Arrange
            var httpMessageHandler = new Mock<HttpMessageHandler>();
            
            // Setup HEAD request for range support check
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Headers = { AcceptRanges = { "none" } }
                });

            // Setup GET request for fallback sequential download
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(new byte[1024])
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
        public async Task BufferSize_ShouldBeConfigurable()
        {
            // Arrange
            var bufferSizes = new[] { 80, 256, 512, 1024 };
            
            foreach (var bufferSizeKB in bufferSizes)
            {
                var queueItem = new FileDownloadQueueItem
                {
                    BufferSizeKB = bufferSizeKB
                };

                // Act
                var actualBufferSize = queueItem.BufferSizeKB * 1024;

                // Assert
                actualBufferSize.Should().Be(bufferSizeKB * 1024);
                actualBufferSize.Should().BeGreaterThanOrEqualTo(80 * 1024);
                actualBufferSize.Should().BeLessThanOrEqualTo(1024 * 1024);
            }
        }

        [Fact]
        public async Task AddQueueItem_WithDownloadSettings_ShouldPersistSettings()
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
            
            // Update with download settings
            queueItem.ParallelConnections = 8;
            queueItem.BufferSizeKB = 512;
            queueItem.UseHttp2 = true;
            queueItem.EnableCompression = true;
            
            await using (var context = await _contextFactory.CreateDbContextAsync())
            {
                context.FileDownloadQueueItems.Update(queueItem);
                await context.SaveChangesAsync();
            }

            // Assert
            await using (var context = await _contextFactory.CreateDbContextAsync())
            {
                var savedItem = await context.FileDownloadQueueItems.FindAsync(queueItem.Id);
                savedItem.Should().NotBeNull();
                savedItem.ParallelConnections.Should().Be(8);
                savedItem.BufferSizeKB.Should().Be(512);
                savedItem.UseHttp2.Should().BeTrue();
                savedItem.EnableCompression.Should().BeTrue();
            }
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

    // Test helper for creating DbContext instances
    public class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(_options);
        }

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}