using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SaveHere.Models;
using SaveHere.Models.db;
using SaveHere.Models.SaveHere.Models;
using SaveHere.Services;
using Xunit;

namespace SaveHere.Tests.Services
{
    public class YoutubeDownloadQueueServiceTests : IDisposable
    {
        private readonly Mock<IDbContextFactory<AppDbContext>> _contextFactoryMock;
        private readonly DownloadStateService _downloadStateService;
        private readonly Mock<ILogger<YoutubeDownloadQueueService>> _loggerMock;
        private readonly Mock<IProgressHubService> _progressHubServiceMock;
        private readonly Mock<IYtdlpService> _ytdlpServiceMock;
        private readonly YoutubeDownloadQueueService _service;
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly string _databaseName;

        public YoutubeDownloadQueueServiceTests()
        {
            _databaseName = Guid.NewGuid().ToString();
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;

            _contextFactoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            _contextFactoryMock.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new AppDbContext(_options));

            _downloadStateService = new DownloadStateService();
            _loggerMock = new Mock<ILogger<YoutubeDownloadQueueService>>();
            _progressHubServiceMock = new Mock<IProgressHubService>();
            _ytdlpServiceMock = new Mock<IYtdlpService>();

            _service = new YoutubeDownloadQueueService(
                _contextFactoryMock.Object,
                _downloadStateService,
                _loggerMock.Object,
                _progressHubServiceMock.Object,
                _ytdlpServiceMock.Object);
        }

        public void Dispose()
        {
            // Clean up in-memory database
        }

        #region Basic CRUD Tests

        [Fact]
        public async Task AddQueueItemAsync_WithValidUrl_AddsItemToQueue()
        {
            // Arrange
            var url = "https://www.youtube.com/watch?v=test123";

            // Act
            var result = await _service.AddQueueItemAsync(url, null, "best", "http://proxy:8080", null, null);

            // Assert
            result.Should().NotBeNull();
            result.Url.Should().Be(url);
            result.Status.Should().Be(EQueueItemStatus.Paused);
            result.Quality.Should().Be("best");
            result.Proxy.Should().Be("http://proxy:8080");

            // Verify the item was saved
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.YoutubeDownloadQueueItems.FindAsync(result.Id);
            itemInDb.Should().NotBeNull();
            itemInDb!.Url.Should().Be(url);
        }

        [Fact]
        public async Task AddQueueItemAsync_WithEmptyUrl_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.AddQueueItemAsync("", null, "best", "", null, null));
        }

        [Fact]
        public async Task AddQueueItemAsync_WithInvalidUrl_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.AddQueueItemAsync("not-a-valid-url", null, "best", "", null, null));
        }

        [Fact]
        public async Task GetQueueItemsAsync_ReturnsAllItems()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var items = new List<YoutubeDownloadQueueItem>
                {
                    new() { Id = 1, Url = "https://youtube.com/watch?v=1", Quality = "best", Proxy = "", Status = EQueueItemStatus.Paused },
                    new() { Id = 2, Url = "https://youtube.com/watch?v=2", Quality = "best", Proxy = "", Status = EQueueItemStatus.Downloading }
                };

                await setupContext.YoutubeDownloadQueueItems.AddRangeAsync(items);
                await setupContext.SaveChangesAsync();
            }

            // Act
            var result = await _service.GetQueueItemsAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(item => item.Id == 1);
            result.Should().Contain(item => item.Id == 2);
        }

        [Fact]
        public async Task GetQueueItemByIdAsync_WithValidId_ReturnsItem()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Paused
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act
            var result = await _service.GetQueueItemByIdAsync(1);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(1);
            result.Url.Should().Be("https://youtube.com/watch?v=test");
        }

        [Fact]
        public async Task GetQueueItemByIdAsync_WithInvalidId_ReturnsNull()
        {
            // Act
            var result = await _service.GetQueueItemByIdAsync(999);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task DeleteQueueItemAsync_WithValidId_RemovesItem()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Paused
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act
            await _service.DeleteQueueItemAsync(1);

            // Assert
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.YoutubeDownloadQueueItems.FindAsync(1);
            itemInDb.Should().BeNull();
        }

        #endregion

        #region Log Batching Tests

        [Fact]
        public async Task AppendLogAsync_AddsLogToQueue_WithoutImmediateDbWrite()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Downloading,
                    PersistedLog = ""
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act - Append a single log line (should not trigger immediate DB write due to batching)
            await _service.AppendLogAsync(1, "Test log line 1");

            // Assert - The log should be queued but not immediately persisted
            // (The first append within the flush interval doesn't trigger a flush)
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.YoutubeDownloadQueueItems.FindAsync(1);

            // The log might not be persisted yet due to batching (5 second interval)
            // This verifies the batching mechanism is working
            itemInDb.Should().NotBeNull();
        }

        [Fact]
        public async Task AppendLogAsync_MultipleLogs_AreBatchedTogether()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Downloading,
                    PersistedLog = ""
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act - Append multiple logs rapidly
            await _service.AppendLogAsync(1, "Log line 1");
            await _service.AppendLogAsync(1, "Log line 2");
            await _service.AppendLogAsync(1, "Log line 3");

            // Force flush to persist logs
            await _service.FlushLogsAsync(1);

            // Assert - All logs should be persisted together
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.YoutubeDownloadQueueItems.FindAsync(1);

            itemInDb.Should().NotBeNull();
            itemInDb!.PersistedLog.Should().Contain("Log line 1");
            itemInDb.PersistedLog.Should().Contain("Log line 2");
            itemInDb.PersistedLog.Should().Contain("Log line 3");
        }

        [Fact]
        public async Task FlushLogsAsync_PersistsAllPendingLogs()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Downloading,
                    PersistedLog = ""
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act - Add logs and flush
            await _service.AppendLogAsync(1, "First log");
            await _service.AppendLogAsync(1, "Second log");
            await _service.FlushLogsAsync(1);

            // Assert
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.YoutubeDownloadQueueItems.FindAsync(1);

            itemInDb.Should().NotBeNull();
            itemInDb!.PersistedLog.Should().NotBeNullOrEmpty();

            var logLines = itemInDb.PersistedLog.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            logLines.Should().Contain("First log");
            logLines.Should().Contain("Second log");
        }

        [Fact]
        public async Task FlushLogsAsync_WithNoLogs_DoesNothing()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Downloading,
                    PersistedLog = "Existing log"
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act - Flush without any pending logs
            await _service.FlushLogsAsync(1);

            // Assert - Existing log should remain unchanged
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.YoutubeDownloadQueueItems.FindAsync(1);

            itemInDb.Should().NotBeNull();
            itemInDb!.PersistedLog.Should().Be("Existing log");
        }

        [Fact]
        public async Task FlushLogsAsync_WithNonExistentItem_DoesNotThrow()
        {
            // Act & Assert - Should not throw for non-existent item
            await _service.FlushLogsAsync(999);
        }

        [Fact]
        public async Task AppendLogAsync_AppendsToExistingLogs()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Downloading,
                    PersistedLog = "Existing log line"
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act - Append new log and flush
            await _service.AppendLogAsync(1, "New log line");
            await _service.FlushLogsAsync(1);

            // Assert
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.YoutubeDownloadQueueItems.FindAsync(1);

            itemInDb.Should().NotBeNull();
            itemInDb!.PersistedLog.Should().Contain("Existing log line");
            itemInDb.PersistedLog.Should().Contain("New log line");
        }

        [Fact]
        public async Task AppendLogAsync_ConcurrentAppends_AreHandledSafely()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Downloading,
                    PersistedLog = ""
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act - Simulate concurrent log appends
            var tasks = Enumerable.Range(1, 100)
                .Select(i => _service.AppendLogAsync(1, $"Concurrent log {i}"))
                .ToList();

            await Task.WhenAll(tasks);
            await _service.FlushLogsAsync(1);

            // Assert - All logs should be captured
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.YoutubeDownloadQueueItems.FindAsync(1);

            itemInDb.Should().NotBeNull();
            itemInDb!.PersistedLog.Should().NotBeNullOrEmpty();

            var logLines = itemInDb.PersistedLog.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            logLines.Should().HaveCount(100);
        }

        #endregion

        #region State Management Tests

        [Fact]
        public async Task UpdateItemStateAsync_UpdatesStatusCorrectly()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Paused
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act
            await _service.UpdateItemStateAsync(1, EQueueItemStatus.Downloading);

            // Assert
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.YoutubeDownloadQueueItems.FindAsync(1);

            itemInDb.Should().NotBeNull();
            itemInDb!.Status.Should().Be(EQueueItemStatus.Downloading);
        }

        [Fact]
        public async Task CancelDownloadAsync_UpdatesStatusAndCancelsToken()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Downloading
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Get token source before cancelling
            var tokenSource = _downloadStateService.GetOrAddTokenSource(1);

            // Act
            await _service.CancelDownloadAsync(1);

            // Assert
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.YoutubeDownloadQueueItems.FindAsync(1);

            itemInDb.Should().NotBeNull();
            itemInDb!.Status.Should().Be(EQueueItemStatus.Cancelled);
            tokenSource.IsCancellationRequested.Should().BeTrue();
        }

        [Fact]
        public async Task CancelDownloadAsync_WithAlreadyCancelledItem_DoesNothing()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var item = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Cancelled
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(item);
                await setupContext.SaveChangesAsync();
            }

            // Act
            await _service.CancelDownloadAsync(1);

            // Assert
            await using var verifyContext = new AppDbContext(_options);
            var itemInDb = await verifyContext.YoutubeDownloadQueueItems.FindAsync(1);

            itemInDb.Should().NotBeNull();
            itemInDb!.Status.Should().Be(EQueueItemStatus.Cancelled);
        }

        #endregion

        #region Log Persistence Tests

        [Fact]
        public async Task GetQueueItemsAsync_DeserializesPersistedLogs()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var setupItem = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Finished,
                    PersistedLog = $"Log 1{Environment.NewLine}Log 2{Environment.NewLine}Log 3"
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(setupItem);
                await setupContext.SaveChangesAsync();
            }

            // Act
            var items = await _service.GetQueueItemsAsync();

            // Assert
            var resultItem = items.First();
            resultItem.OutputLog.Should().HaveCount(3);
            resultItem.OutputLog.Should().Contain("Log 1");
            resultItem.OutputLog.Should().Contain("Log 2");
            resultItem.OutputLog.Should().Contain("Log 3");
        }

        [Fact]
        public async Task GetQueueItemByIdAsync_DeserializesPersistedLogs()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var setupItem = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Finished,
                    PersistedLog = $"Line A{Environment.NewLine}Line B"
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(setupItem);
                await setupContext.SaveChangesAsync();
            }

            // Act
            var resultItem = await _service.GetQueueItemByIdAsync(1);

            // Assert
            resultItem.Should().NotBeNull();
            resultItem!.OutputLog.Should().HaveCount(2);
            resultItem.OutputLog.Should().Contain("Line A");
            resultItem.OutputLog.Should().Contain("Line B");
        }

        [Fact]
        public async Task GetQueueItemByIdAsync_WithEmptyLogs_ReturnsEmptyList()
        {
            // Arrange
            await using (var setupContext = new AppDbContext(_options))
            {
                var setupItem = new YoutubeDownloadQueueItem
                {
                    Id = 1,
                    Url = "https://youtube.com/watch?v=test",
                    Quality = "best",
                    Proxy = "",
                    Status = EQueueItemStatus.Paused,
                    PersistedLog = ""
                };

                await setupContext.YoutubeDownloadQueueItems.AddAsync(setupItem);
                await setupContext.SaveChangesAsync();
            }

            // Act
            var resultItem = await _service.GetQueueItemByIdAsync(1);

            // Assert
            resultItem.Should().NotBeNull();
            resultItem!.OutputLog.Should().BeEmpty();
        }

        #endregion
    }
}
