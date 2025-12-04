using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using SaveHere.Services;
using SaveHere.Models;

namespace SaveHere.Tests.Services
{
    public class MediaConversionServiceTests
    {
        private readonly DownloadStateService _downloadStateService;
        private readonly Mock<ILogger<MediaConversionService>> _loggerMock;
        private readonly Mock<IProgressHubService> _progressHubServiceMock;
        private readonly MediaConversionService _service;

        public MediaConversionServiceTests()
        {
            _downloadStateService = new DownloadStateService();
            _loggerMock = new Mock<ILogger<MediaConversionService>>();
            _progressHubServiceMock = new Mock<IProgressHubService>();

            _service = new MediaConversionService(
                _downloadStateService,
                _loggerMock.Object,
                _progressHubServiceMock.Object);
        }

        [Fact]
        public async Task GetConversionItemsAsync_InitiallyEmpty_ReturnsEmptyList()
        {
            // Act
            var result = await _service.GetConversionItemsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetConversionItemsAsync_WithItems_ReturnsAllItems()
        {
            // Arrange
            await _service.AddConversionItemAsync("test1.mp4", "avi", null);
            await _service.AddConversionItemAsync("test2.mkv", "mp4", "-crf 23");

            // Act
            var result = await _service.GetConversionItemsAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, item => item.InputFile == "test1.mp4");
            Assert.Contains(result, item => item.InputFile == "test2.mkv");
        }

        [Fact]
        public async Task GetConversionItemByIdAsync_WithValidId_ReturnsItem()
        {
            // Arrange
            var addedItem = await _service.AddConversionItemAsync("test.mp4", "avi", null);

            // Act
            var result = await _service.GetConversionItemByIdAsync(addedItem.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(addedItem.Id, result.Id);
            Assert.Equal("test.mp4", result.InputFile);
            Assert.Equal("avi", result.OutputFormat);
        }

        [Fact]
        public async Task GetConversionItemByIdAsync_WithInvalidId_ReturnsNull()
        {
            // Act
            var result = await _service.GetConversionItemByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task AddConversionItemAsync_WithValidInputs_CreatesItem()
        {
            // Act
            var result = await _service.AddConversionItemAsync("test.mp4", "avi", "-crf 23");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test.mp4", result.InputFile);
            Assert.Equal("avi", result.OutputFormat);
            Assert.Equal("-crf 23", result.CustomOptions);
            Assert.Equal(EQueueItemStatus.Paused, result.Status);
            Assert.True(result.Id > 0);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task AddConversionItemAsync_WithEmptyInputFile_ThrowsArgumentException(string inputFile)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.AddConversionItemAsync(inputFile, "avi", null));
        }

        [Fact]
        public async Task AddConversionItemAsync_MultipleItems_AssignsUniqueIds()
        {
            // Act
            var item1 = await _service.AddConversionItemAsync("test1.mp4", "avi", null);
            var item2 = await _service.AddConversionItemAsync("test2.mp4", "mkv", null);
            var item3 = await _service.AddConversionItemAsync("test3.mp4", "mp4", null);

            // Assert
            Assert.NotEqual(item1.Id, item2.Id);
            Assert.NotEqual(item2.Id, item3.Id);
            Assert.NotEqual(item1.Id, item3.Id);
            Assert.True(item1.Id > 0);
            Assert.True(item2.Id > 0);
            Assert.True(item3.Id > 0);
        }

        [Fact]
        public async Task UpdateItemStateAsync_WithValidId_UpdatesStatus()
        {
            // Arrange
            var item = await _service.AddConversionItemAsync("test.mp4", "avi", null);

            // Act
            await _service.UpdateItemStateAsync(item.Id, EQueueItemStatus.Downloading);

            // Assert
            var updatedItem = await _service.GetConversionItemByIdAsync(item.Id);
            Assert.NotNull(updatedItem);
            Assert.Equal(EQueueItemStatus.Downloading, updatedItem.Status);
        }

        [Fact]
        public async Task UpdateItemStateAsync_WithInvalidId_DoesNothing()
        {
            // Act & Assert (should not throw)
            await _service.UpdateItemStateAsync(999, EQueueItemStatus.Downloading);
        }

        [Fact]
        public async Task DeleteConversionItemAsync_WithValidId_RemovesItem()
        {
            // Arrange
            var item = await _service.AddConversionItemAsync("test.mp4", "avi", null);

            // Act
            await _service.DeleteConversionItemAsync(item.Id);

            // Assert
            var deletedItem = await _service.GetConversionItemByIdAsync(item.Id);
            Assert.Null(deletedItem);
        }

        [Fact]
        public async Task DeleteConversionItemAsync_WithInvalidId_DoesNothing()
        {
            // Act & Assert (should not throw)
            await _service.DeleteConversionItemAsync(999);
        }

        [Fact]
        public async Task CancelConversionAsync_WithValidItem_UpdatesStatusAndCancelsToken()
        {
            // Arrange
            var item = await _service.AddConversionItemAsync("test.mp4", "avi", null);
            await _service.UpdateItemStateAsync(item.Id, EQueueItemStatus.Downloading);

            // Get token source before cancelling (it will be created)
            var tokenSource = _downloadStateService.GetOrAddTokenSource(item.Id);

            // Act
            await _service.CancelConversionAsync(item.Id);

            // Assert
            var cancelledItem = await _service.GetConversionItemByIdAsync(item.Id);
            Assert.NotNull(cancelledItem);
            Assert.Equal(EQueueItemStatus.Cancelled, cancelledItem.Status);
            Assert.True(tokenSource.IsCancellationRequested);
            
            _progressHubServiceMock.Verify(x => x.BroadcastStateChange(item.Id, "Cancelled"), Times.Once);
        }

        [Fact]
        public async Task CancelConversionAsync_WithNonExistentItem_DoesNothing()
        {
            // Act & Assert (should not throw)
            await _service.CancelConversionAsync(999);
            
            _progressHubServiceMock.Verify(x => x.BroadcastStateChange(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CancelConversionAsync_WithAlreadyCancelledItem_DoesNotCancelAgain()
        {
            // Arrange
            var item = await _service.AddConversionItemAsync("test.mp4", "avi", null);
            await _service.UpdateItemStateAsync(item.Id, EQueueItemStatus.Cancelled);

            // Act
            await _service.CancelConversionAsync(item.Id);

            // Assert
            _progressHubServiceMock.Verify(x => x.BroadcastStateChange(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task StartConversionAsync_WithNullItem_ThrowsException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => 
                _service.StartConversionAsync(null!));
            
            Assert.Equal("Item not found", exception.Message);
        }

        [Fact]
        public async Task StartConversionAsync_WithAlreadyDownloadingItem_ThrowsException()
        {
            // Arrange
            var item = await _service.AddConversionItemAsync("test.mp4", "avi", null);
            await _service.UpdateItemStateAsync(item.Id, EQueueItemStatus.Downloading);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => 
                _service.StartConversionAsync(item));
            
            Assert.Equal("Conversion is already in progress", exception.Message);
        }

        [Fact]
        public async Task AppendLogAsync_WithValidId_AddsLogLine()
        {
            // Arrange
            var item = await _service.AddConversionItemAsync("test.mp4", "avi", null);
            const string logLine = "Test log message";

            // Act
            await _service.AppendLogAsync(item.Id, logLine);

            // Assert
            var updatedItem = await _service.GetConversionItemByIdAsync(item.Id);
            Assert.NotNull(updatedItem);
            Assert.Single(updatedItem.OutputLog);
            Assert.Equal(logLine, updatedItem.OutputLog.First());
        }

        [Fact]
        public async Task AppendLogAsync_WithInvalidId_DoesNothing()
        {
            // Act & Assert (should not throw)
            await _service.AppendLogAsync(999, "Test log message");
        }

        [Fact]
        public async Task AppendLogAsync_MultipleLogLines_AppendsAll()
        {
            // Arrange
            var item = await _service.AddConversionItemAsync("test.mp4", "avi", null);

            // Act
            await _service.AppendLogAsync(item.Id, "Log line 1");
            await _service.AppendLogAsync(item.Id, "Log line 2");
            await _service.AppendLogAsync(item.Id, "Log line 3");

            // Assert
            var updatedItem = await _service.GetConversionItemByIdAsync(item.Id);
            Assert.NotNull(updatedItem);
            Assert.Equal(3, updatedItem.OutputLog.Count);
            Assert.Equal("Log line 1", updatedItem.OutputLog[0]);
            Assert.Equal("Log line 2", updatedItem.OutputLog[1]);
            Assert.Equal("Log line 3", updatedItem.OutputLog[2]);
        }
    }
}