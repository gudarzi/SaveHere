using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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
    public class ParallelDownloadTests : IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly Mock<IDbContextFactory<AppDbContext>> _contextFactoryMock;
        private readonly Mock<ILogger<DownloadQueueService>> _loggerMock;
        private readonly Mock<IProgressHubService> _progressHubMock;
        private readonly DownloadStateService _downloadStateService;
        private readonly string _databaseName;
        private readonly string _testDownloadPath;

        public ParallelDownloadTests()
        {
            _databaseName = Guid.NewGuid().ToString();
            _testDownloadPath = Path.Combine(Path.GetTempPath(), $"test_downloads_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDownloadPath);

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

        [Theory]
        [InlineData(1, 1024)] // 1KB file, 1 connection
        [InlineData(2, 10240)] // 10KB file, 2 connections
        [InlineData(4, 102400)] // 100KB file, 4 connections
        public async Task ParallelDownload_ShouldDownloadFileInChunks(int connections, int fileSize)
        {
            // Arrange
            var testData = GenerateTestData(fileSize);
            var testUrl = "http://example.com/testfile.bin";

            var httpMessageHandler = new Mock<HttpMessageHandler>();

            // Setup HEAD request for file info
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Head &&
                        req.RequestUri.ToString() == testUrl),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Headers = {
                        AcceptRanges = { "bytes" }
                    },
                    Content = new ByteArrayContent(new byte[0])
                    {
                        Headers = { ContentLength = fileSize }
                    }
                });

            // Setup GET requests for chunks (with range headers)
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.Headers.Range != null),
                    ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    var range = request.Headers.Range;
                    var start = (int)(range.Ranges.First().From ?? 0);
                    var end = (int)(range.Ranges.First().To ?? fileSize - 1);
                    var length = end - start + 1;

                    var chunkData = new byte[length];
                    Array.Copy(testData, start, chunkData, 0, length);

                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.PartialContent,
                        Content = new ByteArrayContent(chunkData)
                    });
                });

            // Setup GET requests without range headers (fallback downloads)
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.Headers.Range == null),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(testData)
                    {
                        Headers = { ContentLength = fileSize }
                    }
                });

            var httpClient = new HttpClient(httpMessageHandler.Object);
            var service = new TestableDownloadQueueService(
                _contextFactoryMock.Object,
                _downloadStateService,
                httpClient,
                _loggerMock.Object,
                _progressHubMock.Object,
                _testDownloadPath);

            var queueItem = new FileDownloadQueueItem
            {
                Id = 1,
                InputUrl = testUrl,
                ParallelConnections = connections,
                BufferSizeKB = 8,
                SupportsRangeRequests = true
            };

            // Act
            await service.TestDownloadFileParallel(queueItem, CancellationToken.None);

            // Assert
            var downloadedFile = Path.Combine(_testDownloadPath, "testfile.bin");
            Assert.True(File.Exists(downloadedFile));

            var downloadedData = await File.ReadAllBytesAsync(downloadedFile);
            Assert.Equal(testData.Length, downloadedData.Length);

            // Verify chunks were downloaded
            httpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Exactly(connections + 1), // +1 for HEAD request
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task ParallelDownload_ShouldMaintainFileIntegrity()
        {
            // Arrange
            var fileSize = 1024 * 1024; // 1MB
            var testData = GenerateTestData(fileSize);
            var originalHash = ComputeHash(testData);
            var testUrl = "http://example.com/largefile.bin";

            var httpMessageHandler = CreateMockHandler(testUrl, testData);
            var httpClient = new HttpClient(httpMessageHandler.Object);

            var service = new TestableDownloadQueueService(
                _contextFactoryMock.Object,
                _downloadStateService,
                httpClient,
                _loggerMock.Object,
                _progressHubMock.Object,
                _testDownloadPath);

            var queueItem = new FileDownloadQueueItem
            {
                Id = 1,
                InputUrl = testUrl,
                ParallelConnections = 8, // High number of connections
                BufferSizeKB = 256,
                SupportsRangeRequests = true
            };

            // Act
            await service.TestDownloadFileParallel(queueItem, CancellationToken.None);

            // Assert
            var downloadedFile = Path.Combine(_testDownloadPath, "largefile.bin");
            var downloadedData = await File.ReadAllBytesAsync(downloadedFile);
            var downloadedHash = ComputeHash(downloadedData);

            Assert.Equal(originalHash, downloadedHash);
        }

        [Fact]
        public async Task ParallelDownload_ShouldFallbackToSequential_WhenRangeNotSupported()
        {
            // Arrange
            var testUrl = "http://example.com/norange.bin";
            var testData = GenerateTestData(1024);

            var httpMessageHandler = new Mock<HttpMessageHandler>();

            // Setup to indicate no range support
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(new byte[0])
                    {
                        Headers = { ContentLength = testData.Length }
                    }
                    // No AcceptRanges header
                });

            // Setup regular GET request (without range)
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.Headers.Range == null),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(testData)
                    {
                        Headers = { ContentLength = testData.Length }
                    }
                });

            // Setup GET request with range (should return full content since range not supported)
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.Headers.Range != null),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK, // Not PartialContent, indicating range not supported
                    Content = new ByteArrayContent(testData)
                    {
                        Headers = { ContentLength = testData.Length }
                    }
                });

            var httpClient = new HttpClient(httpMessageHandler.Object);
            var service = new DownloadQueueService(
                _contextFactoryMock.Object,
                _downloadStateService,
                httpClient,
                _loggerMock.Object,
                _progressHubMock.Object);

            var queueItem = new FileDownloadQueueItem
            {
                Id = 1,
                InputUrl = testUrl,
                ParallelConnections = 4, // Request parallel but should fallback
                BufferSizeKB = 80
            };

            // Act
            await service.DownloadFile(queueItem, CancellationToken.None);

            // Assert
            // The key test is that the download should succeed and fallback to sequential
            // We don't need to verify exact HTTP call counts since the important behavior
            // is that it doesn't use parallel downloads when ranges aren't supported

            // Verify that the range support check was made (HEAD request with range)
            httpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Head &&
                    req.Headers.Range != null),
                ItExpr.IsAny<CancellationToken>()
            );

            // Verify no GET requests with range headers were made (no parallel download chunks)
            httpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.Headers.Range != null),
                ItExpr.IsAny<CancellationToken>()
            );

            // Verify that at least one regular GET request was made (sequential download)
            httpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.AtLeastOnce(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.Headers.Range == null),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task ProgressTracking_ShouldReportAccurateProgress()
        {
            // Arrange
            var progressUpdates = new List<DownloadProgress>();
            _progressHubMock
                .Setup(x => x.BroadcastProgressUpdate(It.IsAny<DownloadProgress>()))
                .Callback<DownloadProgress>(p => progressUpdates.Add(p))
                .Returns(Task.CompletedTask);

            var fileSize = 10240; // 10KB
            var testData = GenerateTestData(fileSize);
            var testUrl = "http://example.com/progress.bin";

            var httpMessageHandler = CreateMockHandler(testUrl, testData);
            var httpClient = new HttpClient(httpMessageHandler.Object);

            var service = new TestableDownloadQueueService(
                _contextFactoryMock.Object,
                _downloadStateService,
                httpClient,
                _loggerMock.Object,
                _progressHubMock.Object,
                _testDownloadPath);

            var queueItem = new FileDownloadQueueItem
            {
                Id = 1,
                InputUrl = testUrl,
                ParallelConnections = 2,
                BufferSizeKB = 1, // Small buffer to trigger more progress updates
                SupportsRangeRequests = true
            };

            // Act
            await service.TestDownloadFileParallel(queueItem, CancellationToken.None);

            // Assert
            Assert.NotEmpty(progressUpdates);
            Assert.Contains(progressUpdates, p => p.ProgressPercentage == 100);
        }

        private byte[] GenerateTestData(int size)
        {
            var data = new byte[size];
            new Random(42).NextBytes(data); // Fixed seed for reproducibility
            return data;
        }

        private string ComputeHash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(data);
            return Convert.ToBase64String(hash);
        }

        private Mock<HttpMessageHandler> CreateMockHandler(string url, byte[] data)
        {
            var handler = new Mock<HttpMessageHandler>();

            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Head &&
                        req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Headers = {
                        AcceptRanges = { "bytes" }
                    },
                    Content = new ByteArrayContent(new byte[0])
                    {
                        Headers = { ContentLength = data.Length }
                    }
                });

            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.Headers.Range != null),
                    ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    var range = request.Headers.Range;
                    var start = (int)(range.Ranges.First().From ?? 0);
                    var end = (int)(range.Ranges.First().To ?? data.Length - 1);
                    var length = end - start + 1;

                    var chunkData = new byte[length];
                    Array.Copy(data, start, chunkData, 0, length);

                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.PartialContent,
                        Content = new ByteArrayContent(chunkData)
                    });
                });

            // Setup GET requests without range headers
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.Headers.Range == null),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(data)
                    {
                        Headers = { ContentLength = data.Length }
                    }
                });

            return handler;
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDownloadPath))
                Directory.Delete(_testDownloadPath, true);
        }
    }

    // Testable version of DownloadQueueService with exposed methods
    public class TestableDownloadQueueService : DownloadQueueService
    {
        private readonly string _testDownloadPath;

        public TestableDownloadQueueService(
            IDbContextFactory<AppDbContext> contextFactory,
            DownloadStateService downloadStateService,
            HttpClient httpClient,
            ILogger<DownloadQueueService> logger,
            IProgressHubService progressHubService,
            string testDownloadPath)
            : base(contextFactory, downloadStateService, httpClient, logger, progressHubService)
        {
            _testDownloadPath = testDownloadPath;
        }

        public async Task TestDownloadFileParallel(FileDownloadQueueItem queueItem, CancellationToken cancellationToken)
        {
            // Override the download path for testing
            var originalPath = Directory.GetCurrentDirectory();

            try
            {
                // Set current directory to parent of test path
                var testParentDir = Path.GetDirectoryName(_testDownloadPath)!;
                Directory.SetCurrentDirectory(testParentDir);

                // Create the test downloads directory
                if (!Directory.Exists(_testDownloadPath))
                {
                    Directory.CreateDirectory(_testDownloadPath);
                }

                // Create downloads directory in current directory that maps to our test path
                var downloadsDir = Path.Combine(Directory.GetCurrentDirectory(), "downloads");
                if (Directory.Exists(downloadsDir))
                {
                    Directory.Delete(downloadsDir, true);
                }

                // Rename our test directory to "downloads" temporarily
                var testDirName = Path.GetFileName(_testDownloadPath);
                var tempDownloadsName = Path.Combine(testParentDir, "downloads");

                if (testDirName != "downloads")
                {
                    Directory.Move(_testDownloadPath, tempDownloadsName);
                }

                await base.DownloadFileParallel(queueItem, cancellationToken);

                // Move it back if we renamed it
                if (testDirName != "downloads" && Directory.Exists(tempDownloadsName))
                {
                    if (Directory.Exists(_testDownloadPath))
                        Directory.Delete(_testDownloadPath, true);
                    Directory.Move(tempDownloadsName, _testDownloadPath);
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(originalPath);
            }
        }
    }
}
