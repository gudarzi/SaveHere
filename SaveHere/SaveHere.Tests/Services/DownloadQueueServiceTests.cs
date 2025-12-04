using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq.Protected;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SaveHere.Models;
using SaveHere.Models.db;
using SaveHere.Services;
using System.Net.Http;
using Xunit;

namespace SaveHere.Tests.Services
{
    public class DownloadQueueServiceTests : IDisposable
    {
        private readonly Mock<IDbContextFactory<AppDbContext>> _contextFactoryMock;
        private readonly DownloadStateService _downloadStateService;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<DownloadQueueService>> _loggerMock;
        private readonly Mock<IProgressHubService> _progressHubServiceMock;
        private readonly DownloadQueueService _service;
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly string _databaseName;

        public DownloadQueueServiceTests()
        {
            _databaseName = Guid.NewGuid().ToString();
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;

            _contextFactoryMock = new Mock<IDbContextFactory<AppDbContext>>();

            // CRITICAL FIX: The factory should create NEW contexts each time
            _contextFactoryMock.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new AppDbContext(_options));

            _downloadStateService = new DownloadStateService();
            _httpClient = new HttpClient();
            _loggerMock = new Mock<ILogger<DownloadQueueService>>();
            _progressHubServiceMock = new Mock<IProgressHubService>();

            _service = new DownloadQueueService(
                _contextFactoryMock.Object,
                _downloadStateService,
                _httpClient,
                _loggerMock.Object,
                _progressHubServiceMock.Object);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        [Fact]
        public async Task GetQueueItemsAsync_ReturnsAllItems()
        {
            // Arrange - Use a separate context for setup
            await using (var setupContext = new AppDbContext(_options))
            {
                var items = new List<FileDownloadQueueItem>
                {
                    new() { Id = 1, InputUrl = "https://example.com/file1.zip", Status = EQueueItemStatus.Paused },
                    new() { Id = 2, InputUrl = "https://example.com/file2.zip", Status = EQueueItemStatus.Downloading }
                };

                await setupContext.FileDownloadQueueItems.AddRangeAsync(items);
                await setupContext.SaveChangesAsync();
            }

            // Act
            var result = await _service.GetQueueItemsAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, item => item.Id == 1);
            Assert.Contains(result, item => item.Id == 2);
        }

        [Fact]
        public async Task GetQueueItemByIdAsync_WithValidId_ReturnsItem()
        {
            // Arrange - Use a separate context for setup
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new FileDownloadQueueItem
                {
                    Id = 1,
                    InputUrl = "https://example.com/file.zip",
                    Status = EQueueItemStatus.Paused
                };

                await setupContext.FileDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act
            var result = await _service.GetQueueItemByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("https://example.com/file.zip", result.InputUrl);
        }

        [Fact]
        public async Task GetQueueItemByIdAsync_WithInvalidId_ReturnsNull()
        {
            // Act
            var result = await _service.GetQueueItemByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task AddQueueItemAsync_WithValidUrl_AddsItemToQueue()
        {
            // Arrange
            var url = "https://example.com/file.zip";

            // Act
            var result = await _service.AddQueueItemAsync(url);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(url, result.InputUrl);
            Assert.Equal(EQueueItemStatus.Paused, result.Status);
            Assert.Equal(0, result.ProgressPercentage);

            // Verify the item was saved using a separate context
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.FileDownloadQueueItems.FindAsync(result.Id);

            Assert.NotNull(itemInDb);
            Assert.Equal(url, itemInDb.InputUrl);
        }

        [Theory]
        [InlineData(null)]
        public async Task AddQueueItemAsync_WithEmptyUrl_ThrowsArgumentException(string url)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.AddQueueItemAsync(url));
        }

        [Theory]
        [InlineData("not-a-url")]
        [InlineData("ftp://example.com/file.zip")]
        [InlineData("invalid-scheme://example.com")]
        public async Task AddQueueItemAsync_WithInvalidUrl_ThrowsArgumentException(string url)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.AddQueueItemAsync(url));
        }

        [Fact]
        public async Task DeleteQueueItemAsync_WithValidId_RemovesItem()
        {
            // Arrange - Use a separate context for setup
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new FileDownloadQueueItem
                {
                    Id = 1,
                    InputUrl = "https://example.com/file.zip",
                    Status = EQueueItemStatus.Paused
                };

                await setupContext.FileDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act
            await _service.DeleteQueueItemAsync(1);

            // Assert - Use a separate context for verification
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.FileDownloadQueueItems.FindAsync(1);
            Assert.Null(itemInDb);
        }

        [Fact]
        public async Task DeleteQueueItemAsync_WithInvalidId_DoesNothing()
        {
            // Act & Assert (should not throw)
            await _service.DeleteQueueItemAsync(999);
        }

        [Fact]
        public async Task PauseDownloadAsync_WithDownloadingItem_CancelsToken()
        {
            // Arrange - Use a separate context for setup
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new FileDownloadQueueItem
                {
                    Id = 1,
                    InputUrl = "https://example.com/file.zip",
                    Status = EQueueItemStatus.Downloading
                };

                await setupContext.FileDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Get token source before pausing (it will be created)
            var tokenSource = _downloadStateService.GetOrAddTokenSource(1);

            // Act
            await _service.PauseDownloadAsync(1);

            // Assert
            Assert.True(tokenSource.IsCancellationRequested);
        }

        [Fact]
        public async Task PauseDownloadAsync_WithNonDownloadingItem_DoesNothing()
        {
            // Arrange - Use a separate context for setup
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new FileDownloadQueueItem
                {
                    Id = 1,
                    InputUrl = "https://example.com/file.zip",
                    Status = EQueueItemStatus.Paused
                };

                await setupContext.FileDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act
            await _service.PauseDownloadAsync(1);

            // Assert - method should return without doing anything
            // No exception should be thrown
        }

        [Fact]
        public async Task CancelDownloadAsync_WithValidItem_UpdatesStatusAndCancelsToken()
        {
            // Arrange - Use a separate context for setup
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new FileDownloadQueueItem
                {
                    Id = 1,
                    InputUrl = "https://example.com/file.zip",
                    Status = EQueueItemStatus.Downloading
                };

                await setupContext.FileDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Get token source before cancelling (it will be created)
            var tokenSource = _downloadStateService.GetOrAddTokenSource(1);

            // Act
            await _service.CancelDownloadAsync(1);

            // Assert - Use a separate context for verification
            await using var verifyContext = new AppDbContext(_options);
            var updatedItem = await verifyContext.FileDownloadQueueItems.FindAsync(1);
            Assert.NotNull(updatedItem);
            Assert.Equal(EQueueItemStatus.Cancelled, updatedItem.Status);
            Assert.True(tokenSource.IsCancellationRequested);
        }

        [Fact]
        public async Task CancelDownloadAsync_WithAlreadyCancelledItem_DoesNotCancelToken()
        {
            // Arrange - Use a separate context for setup
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new FileDownloadQueueItem
                {
                    Id = 1,
                    InputUrl = "https://example.com/file.zip",
                    Status = EQueueItemStatus.Cancelled
                };

                await setupContext.FileDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act
            await _service.CancelDownloadAsync(1);

            // Assert - Use a separate context for verification
            await using var verifyContext = new AppDbContext(_options);
            var updatedItem = await verifyContext.FileDownloadQueueItems.FindAsync(1);
            Assert.NotNull(updatedItem);
            Assert.Equal(EQueueItemStatus.Cancelled, updatedItem.Status);
        }

        [Fact]
        public async Task StartDownloadAsync_WithNonExistentItem_ThrowsException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() =>
                _service.StartDownloadAsync(999, null, null));

            Assert.Equal("Item not found", exception.Message);
        }

        [Fact]
        public async Task StartDownloadAsync_WithAlreadyDownloadingItem_ThrowsException()
        {
            // Arrange - Use a separate context for setup
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new FileDownloadQueueItem
                {
                    Id = 1,
                    InputUrl = "https://example.com/file.zip",
                    Status = EQueueItemStatus.Downloading
                };

                await setupContext.FileDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() =>
                _service.StartDownloadAsync(1, null, null));

            Assert.Equal("Item is already downloading", exception.Message);
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
                _contextFactoryMock.Object,
                _downloadStateService,
                httpClient,
                _loggerMock.Object,
                _progressHubServiceMock.Object
                );

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
                _contextFactoryMock.Object,
                _downloadStateService,
                httpClient,
                _loggerMock.Object,
                _progressHubServiceMock.Object
                );
            
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
                    StatusCode = HttpStatusCode.OK
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
                _contextFactoryMock.Object,
                _downloadStateService,
                httpClient,
                _loggerMock.Object,
                _progressHubServiceMock.Object
                );

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

            var url = "http://example.com/file.zip";

            // Act
            var queueItem = await _service.AddQueueItemAsync(url);
            
            // Update with download settings
            queueItem.ParallelConnections = 8;
            queueItem.BufferSizeKB = 512;
            queueItem.UseHttp2 = true;
            queueItem.EnableCompression = true;
            
            await using (var context = await _contextFactoryMock.Object.CreateDbContextAsync())
            {
                context.FileDownloadQueueItems.Update(queueItem);
                await context.SaveChangesAsync();
            }

            // Assert
            await using (var context = await _contextFactoryMock.Object.CreateDbContextAsync())
            {
                var savedItem = await context.FileDownloadQueueItems.FindAsync(queueItem.Id);
                savedItem.Should().NotBeNull();
                savedItem.ParallelConnections.Should().Be(8);
                savedItem.BufferSizeKB.Should().Be(512);
                savedItem.UseHttp2.Should().BeTrue();
                savedItem.EnableCompression.Should().BeTrue();
            }
        }

        public void DisposeDataBase()
        {
            // Clean up test database
            if (File.Exists(_databaseName))
            {
                File.Delete(_databaseName);
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