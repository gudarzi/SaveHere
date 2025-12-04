using Xunit;
using SaveHere.Services;
using SaveHere.Helpers;
using System.IO;
using System.Collections.Generic;

namespace SaveHere.Tests.Services
{
    public class FileManagerServiceTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly IFileManagerService _fileManagerService;

        public FileManagerServiceTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "FileManagerServiceTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _fileManagerService = new FileManagerService();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Fact]
        public void GetFiles_WithValidDirectory_ReturnsFileSystemItems()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "test.txt");
            var testSubDir = Path.Combine(_testDirectory, "subdir");
            File.WriteAllText(testFile, "test content");
            Directory.CreateDirectory(testSubDir);

            // Act
            var result = _fileManagerService.GetFiles(_testDirectory);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Contains(result, item => item.Name == "test.txt" && item.Type == "file");
            Assert.Contains(result, item => item.Name == "subdir" && item.Type == "directory");
        }

        [Fact]
        public void GetFiles_WithEmptyDirectory_ReturnsEmptyList()
        {
            // Act
            var result = _fileManagerService.GetFiles(_testDirectory);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void GetFiles_WithInvalidDirectory_ThrowsException()
        {
            // Arrange
            var invalidPath = Path.Combine(_testDirectory, "nonexistent");

            // Act & Assert
            Assert.Throws<DirectoryAccessException>(() => _fileManagerService.GetFiles(invalidPath));
        }

        [Fact]
        public void DeleteFile_WithValidFileItem_DeletesFile()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "test.txt");
            File.WriteAllText(testFile, "test content");
            var fileItem = new FileItem
            {
                Name = "test.txt",
                FullName = testFile,
                Type = "file"
            };

            // Act
            _fileManagerService.DeleteFile(fileItem);

            // Assert
            Assert.False(File.Exists(testFile));
        }

        [Fact]
        public void DeleteDirectory_WithValidDirectoryItem_DeletesDirectory()
        {

            // Arrange
            var testDir = Path.Combine(_testDirectory, "testdir");
            Directory.CreateDirectory(testDir);
            var dirItem = new DirectoryItem
            {
                Name = "testdir",
                FullName = testDir,
                Type = "directory"
            };

            // Act
            _fileManagerService.DeleteDirectory(dirItem);

            // Assert
            Assert.False(Directory.Exists(testDir));
        }

        [Fact]
        public void DeleteDirectory_WithNonexistentDirectory_ThrowsException()
        {
            // Arrange
            var nonexistentDir = Path.Combine(_testDirectory, "nonexistent");
            var dirItem = new DirectoryItem
            {
                Name = "nonexistent",
                FullName = nonexistentDir,
                Type = "directory"
            };

            // Act & Assert
            Assert.Throws<DirectoryNotFoundException>(() => _fileManagerService.DeleteDirectory(dirItem));
        }

        [Fact]
        public void RenameItem_WithValidFile_ReturnsTrue()
        {
            File.Delete(Path.Combine(_testDirectory, "newname.txt"));

            // Arrange
            var testFile = Path.Combine(_testDirectory, "oldname.txt");
            File.WriteAllText(testFile, "test content");
            var fileItem = new FileItem
            {
                Name = "oldname.txt",
                FullName = testFile,
                Type = "file"
            };

            // Act
            var result = _fileManagerService.RenameItem(fileItem, "newname.txt");

            // Assert
            Assert.True(result);
            Assert.False(File.Exists(testFile));
            Assert.True(File.Exists(Path.Combine(".", "newname.txt")));
            //clean up created file

        }

        [Fact]
        public void RenameItem_WithInvalidName_ReturnsFalse()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "test.txt");
            File.WriteAllText(testFile, "test content");
            var fileItem = new FileItem
            {
                Name = "test.txt",
                FullName = testFile,
                Type = "file"
            };

            // Act
            var result = _fileManagerService.RenameItem(fileItem, "invalid<>name.txt");

            // Assert
            Assert.False(result);
            Assert.True(File.Exists(testFile)); // Original file should still exist
        }

        [Fact]
        public void RenameItem_WithExistingTargetName_ReturnsFalse()
        {
            // Arrange
            var testFile1 = Path.Combine(_testDirectory, "file1.txt");
            var testFile2 = Path.Combine(_testDirectory, "file2.txt");
            File.WriteAllText(testFile1, "content1");
            File.WriteAllText(testFile2, "content2");
            
            var fileItem = new FileItem
            {
                Name = "file1.txt",
                FullName = testFile1,
                Type = "file"
            };

            // Act
            var result = _fileManagerService.RenameItem(fileItem, "file2.txt");

            // Assert
            Assert.False(result);
            Assert.True(File.Exists(testFile1)); // Original file should still exist
            Assert.True(File.Exists(testFile2)); // Target file should still exist
        }
    }
}