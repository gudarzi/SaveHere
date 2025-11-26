using Xunit;
using SaveHere.Services;
using SaveHere.Helpers;

namespace SaveHere.Tests.Services
{
    public class ShortLinkServiceTests
    {
        private readonly ShortLinkService _service;

        public ShortLinkServiceTests()
        {
            _service = new ShortLinkService();
        }

        [Fact]
        public void GetShortLink_WithFilePath_ReturnsDeterministicShortLink()
        {
            // Arrange
            var filePath = "/path/to/test/file.txt";

            // Act
            var shortLink1 = _service.GetShortLink(filePath);
            var shortLink2 = _service.GetShortLink(filePath);

            // Assert
            Assert.NotNull(shortLink1);
            Assert.NotEmpty(shortLink1);
            Assert.Equal(shortLink1, shortLink2); // Should be deterministic
            Assert.Equal(16, shortLink1.Length); // Should be 16 characters (8 bytes in hex)
        }

        [Fact]
        public void GetShortLink_WithDifferentFilePaths_ReturnsDifferentShortLinks()
        {
            // Arrange
            var filePath1 = "/path/to/file1.txt";
            var filePath2 = "/path/to/file2.txt";

            // Act
            var shortLink1 = _service.GetShortLink(filePath1);
            var shortLink2 = _service.GetShortLink(filePath2);

            // Assert
            Assert.NotEqual(shortLink1, shortLink2);
        }

        [Fact]
        public void GetShortLink_CalledMultipleTimesWithSamePath_ReturnsSameLink()
        {
            // Arrange
            var filePath = "/path/to/test/file.txt";

            // Act
            var shortLink1 = _service.GetShortLink(filePath);
            var shortLink2 = _service.GetShortLink(filePath);
            var shortLink3 = _service.GetShortLink(filePath);

            // Assert
            Assert.Equal(shortLink1, shortLink2);
            Assert.Equal(shortLink2, shortLink3);
        }

        [Fact]
        public void TryGetFilePath_WithValidShortLink_ReturnsTrue()
        {
            // Arrange
            var filePath = "/path/to/test/file.txt";
            var shortLink = _service.GetShortLink(filePath);

            // Act
            var result = _service.TryGetFilePath(shortLink, out var retrievedFilePath);

            // Assert
            Assert.True(result);
            Assert.Equal(filePath, retrievedFilePath);
        }

        [Fact]
        public void TryGetFilePath_WithInvalidShortLink_ReturnsFalse()
        {
            // Act
            var result = _service.TryGetFilePath("invalidshortlink", out var filePath);

            // Assert
            Assert.False(result);
            Assert.Null(filePath);
        }

        [Fact]
        public void RefreshShortLinks_WithFileItems_UpdatesMappings()
        {
            // Arrange
            var fileItems = new List<FileSystemItem>
            {
                new FileItem { FullName = "/path/to/file1.txt", Name = "file1.txt", Type = "file" },
                new FileItem { FullName = "/path/to/file2.txt", Name = "file2.txt", Type = "file" },
                new DirectoryItem { FullName = "/path/to/dir", Name = "dir", Type = "directory" } // Should be ignored
            };

            // Act
            _service.RefreshShortLinks(fileItems);

            // Assert
            Assert.True(_service.TryGetFilePath(_service.GetShortLink("/path/to/file1.txt"), out var filePath1));
            Assert.True(_service.TryGetFilePath(_service.GetShortLink("/path/to/file2.txt"), out var filePath2));
            Assert.Equal("/path/to/file1.txt", filePath1);
            Assert.Equal("/path/to/file2.txt", filePath2);
        }

        [Fact]
        public void RefreshShortLinks_WithExistingMappings_PreservesExistingShortLinks()
        {
            // Arrange
            var filePath = "/path/to/test/file.txt";
            var originalShortLink = _service.GetShortLink(filePath);

            var fileItems = new List<FileSystemItem>
            {
                new FileItem { FullName = filePath, Name = "file.txt", Type = "file" }
            };

            // Act
            _service.RefreshShortLinks(fileItems);

            // Assert
            var newShortLink = _service.GetShortLink(filePath);
            Assert.Equal(originalShortLink, newShortLink);
        }

        [Fact]
        public void RefreshShortLinks_RemovesOldMappings()
        {
            // Arrange
            var oldFilePath = "/path/to/old/file.txt";
            var newFilePath = "/path/to/new/file.txt";
            
            var oldShortLink = _service.GetShortLink(oldFilePath);
            
            var fileItems = new List<FileSystemItem>
            {
                new FileItem { FullName = newFilePath, Name = "file.txt", Type = "file" }
            };

            // Act
            _service.RefreshShortLinks(fileItems);

            // Assert
            Assert.False(_service.TryGetFilePath(oldShortLink, out _));
            Assert.True(_service.TryGetFilePath(_service.GetShortLink(newFilePath), out var retrievedPath));
            Assert.Equal(newFilePath, retrievedPath);
        }

        [Fact]
        public void RefreshShortLinks_WithEmptyList_ClearsMappings()
        {
            // Arrange
            var filePath = "/path/to/test/file.txt";
            var shortLink = _service.GetShortLink(filePath);

            // Act
            _service.RefreshShortLinks(new List<FileSystemItem>());

            // Assert
            Assert.False(_service.TryGetFilePath(shortLink, out _));
        }

        [Fact]
        public void GetShortLink_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var filePath = "/path/to/file with spaces & symbols!@#$%^&*().txt";

            // Act
            var shortLink = _service.GetShortLink(filePath);

            // Assert
            Assert.NotNull(shortLink);
            Assert.NotEmpty(shortLink);
            Assert.Equal(16, shortLink.Length);
            Assert.True(_service.TryGetFilePath(shortLink, out var retrievedPath));
            Assert.Equal(filePath, retrievedPath);
        }

        [Fact]
        public void GetShortLink_WithUnicodeCharacters_HandlesCorrectly()
        {
            // Arrange
            var filePath = "/path/to/файл_测试_テスト.txt";

            // Act
            var shortLink = _service.GetShortLink(filePath);

            // Assert
            Assert.NotNull(shortLink);
            Assert.NotEmpty(shortLink);
            Assert.Equal(16, shortLink.Length);
            Assert.True(_service.TryGetFilePath(shortLink, out var retrievedPath));
            Assert.Equal(filePath, retrievedPath);
        }
    }
}