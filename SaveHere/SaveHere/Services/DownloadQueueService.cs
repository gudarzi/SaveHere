using Microsoft.EntityFrameworkCore;
using SaveHere.Models;
using SaveHere.Models.db;
using System.Diagnostics;
using System.Net;
using System.Web;
using System.Net.Http.Headers;
using SaveHere.Services;

namespace SaveHere.Services
{
  public interface IDownloadQueueService
  {
    Task<List<FileDownloadQueueItem>> GetQueueItemsAsync();
    Task<FileDownloadQueueItem?> GetQueueItemByIdAsync(int id);
    Task<FileDownloadQueueItem> AddQueueItemAsync(string url);
    Task DeleteQueueItemAsync(int id);
    Task StartDownloadAsync(int id, string? customFileName, string? downloadFolderName);
    Task PauseDownloadAsync(int id);
    Task CancelDownloadAsync(int id);
    Task<VideoInfo> GetFileInfo(string url);
  }

  public class DownloadQueueService : IDownloadQueueService
  {
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly DownloadStateService _downloadStateService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DownloadQueueService> _logger;
    private readonly IProgressHubService _progressHubService;

    public DownloadQueueService(IDbContextFactory<AppDbContext> contextFactory, DownloadStateService downloadStateService, HttpClient httpClient, ILogger<DownloadQueueService> logger, IProgressHubService progressHubService)
    {
      _contextFactory = contextFactory;
      _downloadStateService = downloadStateService;
      _httpClient = httpClient;
      _logger = logger;
      _progressHubService = progressHubService;
    }

    public async Task<List<FileDownloadQueueItem>> GetQueueItemsAsync()
    {
      await using var context = await _contextFactory.CreateDbContextAsync();
      return await context.FileDownloadQueueItems.ToListAsync();
    }

    public async Task<FileDownloadQueueItem?> GetQueueItemByIdAsync(int id)
    {
      await using var context = await _contextFactory.CreateDbContextAsync();
      return await context.FileDownloadQueueItems.FindAsync(id);
    }

    public async Task<FileDownloadQueueItem> AddQueueItemAsync(string url)
    {
      if (string.IsNullOrWhiteSpace(url))
      {
        throw new ArgumentException("URL cannot be empty", nameof(url));
      }

      if (!Uri.TryCreate(url, UriKind.Absolute, out _))
      {
        throw new ArgumentException("URL is not valid", nameof(url));
      }

      var item = new FileDownloadQueueItem
      {
        InputUrl = url,
        Status = EQueueItemStatus.Paused,
        ProgressPercentage = 0
      };

      await using var context = await _contextFactory.CreateDbContextAsync();
      context.FileDownloadQueueItems.Add(item);
      await context.SaveChangesAsync();

      return item;
    }

    public async Task UpdateQueueItemAsync(FileDownloadQueueItem item)
    {
      await using var context = await _contextFactory.CreateDbContextAsync();
      context.FileDownloadQueueItems.Update(item);
      await context.SaveChangesAsync();
    }

    public async Task DeleteQueueItemAsync(int id)
    {
      var item = await GetQueueItemByIdAsync(id);
      if (item != null)
      {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.FileDownloadQueueItems.Remove(item);
        _downloadStateService.RemoveTokenSource(id);
        await context.SaveChangesAsync();
      }
    }

    public async Task PauseDownloadAsync(int id)
    {
      var item = await GetQueueItemByIdAsync(id);
      if (item == null) return;
      if (item.Status != EQueueItemStatus.Downloading) return;
      var tokenSource = _downloadStateService.GetOrAddTokenSource(id);
      tokenSource.Cancel();
    }

    public async Task CancelDownloadAsync(int id)
    {
      var item = await GetQueueItemByIdAsync(id);
      if (item == null) return;
      if (item.Status == EQueueItemStatus.Cancelled) return;

      // Update status immediately
      item.Status = EQueueItemStatus.Cancelled;
      await UpdateQueueItemAsync(item);

      var tokenSource = _downloadStateService.GetOrAddTokenSource(id);
      tokenSource.Cancel();
    }

    public async Task StartDownloadAsync(int id, string? customFileName, string? downloadFolderName)
    {
      var item = await GetQueueItemByIdAsync(id);

      if (item == null) throw new Exception("Item not found");
      if (item.Status == EQueueItemStatus.Downloading) throw new Exception("Item is already downloading");

      var tokenSource = _downloadStateService.GetOrAddTokenSource(id);
      var token = tokenSource.Token;

      try
      {
        item.DownloadFolder = downloadFolderName;
        item.CustomFileName = customFileName;

        item.Status = EQueueItemStatus.Downloading;
        await UpdateQueueItemAsync(item);
        await _progressHubService.BroadcastStateChange(id, item.Status.ToString());

        await DownloadFile(item, token);

        item.Status = EQueueItemStatus.Finished;
        item.ProgressPercentage = 100;
        await UpdateQueueItemAsync(item);
        await _progressHubService.BroadcastStateChange(id, item.Status.ToString());
      }
      catch (OperationCanceledException)
      {
        item.Status = EQueueItemStatus.Cancelled;
        await UpdateQueueItemAsync(item);
        await _progressHubService.BroadcastStateChange(id, item.Status.ToString());
        throw;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error downloading the file for item {id}", id);
        item.Status = EQueueItemStatus.Paused;
        await UpdateQueueItemAsync(item);
        await _progressHubService.BroadcastStateChange(id, item.Status.ToString());
        throw;
      }
      finally
      {
        _downloadStateService.RemoveTokenSource(id);
      }
    }

    public async Task DownloadFile(FileDownloadQueueItem queueItem, CancellationToken cancellationToken)
    {
      // Validate the URL (must use either HTTP or HTTPS schemes)
      if (string.IsNullOrEmpty(queueItem.InputUrl) ||
          !Uri.TryCreate(queueItem.InputUrl, UriKind.Absolute, out Uri? uriResult) ||
          !(uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
      {
        throw new Exception("Invalid URL");
      }

      // Check if server supports range requests for parallel downloads
      if (queueItem.ParallelConnections > 1)
      {
        queueItem.SupportsRangeRequests = await CheckRangeSupport(queueItem.InputUrl, cancellationToken);
        if (queueItem.SupportsRangeRequests)
        {
          await DownloadFileParallel(queueItem, cancellationToken);
          return;
        }
      }

      try
      {
        var httpClient = _httpClient;

        ProxyServer? proxyServer = null; // To Do: Add proxy server
        if (proxyServer != null && !(string.IsNullOrWhiteSpace(proxyServer.Protocol) || string.IsNullOrWhiteSpace(proxyServer.Host) || proxyServer.Port == 0))
        {
          var url = proxyServer.Protocol + "://" + proxyServer.Host + ":" + proxyServer.Port;

          var proxy = new WebProxy
          {
            Address = new Uri(url),
            BypassProxyOnLocal = true,
            //Credentials = new NetworkCredential(username, password)
          };

          var httpClientHandler = new HttpClientHandler
          {
            Proxy = proxy,
            UseProxy = true
          };

          httpClient = new HttpClient(httpClientHandler);

        }

        var fileName = string.IsNullOrEmpty(queueItem.CustomFileName)
        ? Helpers.Helpers.ExtractFileNameFromUrl(queueItem.InputUrl)
        : queueItem.CustomFileName;


       var response = await httpClient.GetAsync(queueItem.InputUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
       response.EnsureSuccessStatusCode();

                if (string.IsNullOrWhiteSpace(queueItem.CustomFileName) && queueItem.bShouldGetFilenameFromHttpHeaders)
                {
                    var contentDisposition = response.Content.Headers.ContentDisposition;

                    if (contentDisposition != null)
                    {
                        if (!string.IsNullOrEmpty(contentDisposition.FileNameStar))
                            fileName = System.Web.HttpUtility.UrlDecode(contentDisposition.FileNameStar.Replace("\"", ""));
                        else if (!string.IsNullOrEmpty(contentDisposition.FileName))
                            fileName = System.Web.HttpUtility.UrlDecode(contentDisposition.FileName.Replace("\"", ""));
                    }
                }

                // Ensure the filename is safe by removing invalid characters and making sure it cannot end up being empty
                fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "unnamed_" + DateTime.Now.ToString("yyyyMMddHHmmss");

                // Try to determine the file extension based on common mime types if the filename doesn't have one already
                if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
                {
                    var contentTypeHeader = response.Content.Headers.ContentType;

                    if (contentTypeHeader != null)
                    {
                        var mediaType = contentTypeHeader.MediaType;

                        if (!string.IsNullOrWhiteSpace(mediaType) &&
                            Helpers.Helpers.CommonMimeTypes.TryGetValue(mediaType, out var extension))
                        {
                            fileName += extension;
                        }
                    }
                }


        var (localFilePath, tempFilePath) = PrepareDownloadPath(queueItem, fileName);
        var downloadPath = Path.GetDirectoryName(localFilePath) ?? throw new UnauthorizedAccessException("Invalid file path.");

        // Check for existing temp file and corresponding final file
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        string fileExtension = Path.GetExtension(fileName);
        long totalBytesRead = 0;
        int digit = 1;

        while (File.Exists(tempFilePath) || File.Exists(localFilePath))
        {
          if (File.Exists(tempFilePath))
          {
            totalBytesRead = new FileInfo(tempFilePath).Length;
            break;
          }

          fileName = $"{fileNameWithoutExtension}_{digit}{fileExtension}";
          localFilePath = Path.Combine(downloadPath, fileName);
          tempFilePath = localFilePath + ".tmp";
          digit++;
        }

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, queueItem.InputUrl);

        if (totalBytesRead > 0)
        {
          requestMessage.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(totalBytesRead, null);
        }

        response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        // If the server supports resumption (HTTP 206), do not restart the download from scratch
        bool restartDownload = response.StatusCode != System.Net.HttpStatusCode.PartialContent;

        if (restartDownload)
        {
          totalBytesRead = 0;
        }

        using (var download = await response.Content.ReadAsStreamAsync())
        {
          using var stream = new FileStream(tempFilePath, restartDownload ? FileMode.Create : FileMode.Append, FileAccess.Write);
          var contentLength = response.Content.Headers.ContentLength;

          // Dynamic buffer size based on user configuration
          var bufferSize = queueItem.BufferSizeKB * 1024;
          var buffer = new byte[bufferSize];

          int bytesRead;
          double speedMeasurementPeriodInSeconds = 1;
          long bytesReadInLastPeriod = 0;
          long bytesReadInTotal = 0;
          var speedMeasurementStopwatch = Stopwatch.StartNew();
          var speedMeasurementTotalStopwatch = Stopwatch.StartNew();

          async Task UpdateProgress(int progress, double currentSpeed, double averageSpeed)
          {

                        await using var context = await _contextFactory.CreateDbContextAsync();
                        var dbQueueItem = await context.FileDownloadQueueItems.FindAsync(queueItem.Id, cancellationToken);
                        if (dbQueueItem != null)
                        {
                            dbQueueItem.ProgressPercentage = progress;
                            dbQueueItem.CurrentDownloadSpeed = currentSpeed;
                            dbQueueItem.AverageDownloadSpeed = averageSpeed;
                            
                            try
                            {
                                await context.SaveChangesAsync(cancellationToken);
                                
                                // Update local instance
                                queueItem.ProgressPercentage = progress;
                                queueItem.CurrentDownloadSpeed = currentSpeed;
                                queueItem.AverageDownloadSpeed = averageSpeed;
                            }
                            catch (DbUpdateConcurrencyException)
                            {
                                context.Entry(dbQueueItem).State = EntityState.Detached;
                                // Don't log progress update conflicts as they're frequent and expected
                            }
                        }


                        var downloadProgress = new DownloadProgress()
            {
              ItemId = queueItem.Id,
              ProgressPercentage = progress,
              CurrentSpeed = currentSpeed,
              AverageSpeed = averageSpeed
            };
            await _progressHubService.BroadcastProgressUpdate(downloadProgress);
          }

          double timePerReadInMilliseconds = 1000.0 / queueItem.MaxBytesPerSecond * buffer.Length;

          // when we don't know the file size
          if (!contentLength.HasValue)
          {
            while ((bytesRead = await download.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
            {
              // Check if cancellation is requested
              if (cancellationToken.IsCancellationRequested)
              {
                throw new OperationCanceledException(cancellationToken);
              }

              await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
              if (queueItem.MaxBytesPerSecond > 0) await Task.Delay((int)timePerReadInMilliseconds);
              totalBytesRead += bytesRead;
              bytesReadInLastPeriod += bytesRead;
              bytesReadInTotal += bytesRead;

              var totalMeasurementSeconds = speedMeasurementStopwatch.Elapsed.TotalSeconds;
              if (totalMeasurementSeconds >= speedMeasurementPeriodInSeconds)
              {
                await UpdateProgress(99, bytesReadInLastPeriod / totalMeasurementSeconds, bytesReadInTotal / speedMeasurementTotalStopwatch.Elapsed.TotalSeconds);

                bytesReadInLastPeriod = 0;
                speedMeasurementStopwatch.Restart();
              }
            }
          }
          else
          {
            // Adjusted for correct progress reporting when resuming download
            long totalContentLength = contentLength.Value + totalBytesRead;

            while ((bytesRead = await download.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
            {
              // Check if cancellation is requested
              if (cancellationToken.IsCancellationRequested)
              {
                throw new OperationCanceledException(cancellationToken);
              }

              await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
              if (queueItem.MaxBytesPerSecond > 0) await Task.Delay((int)timePerReadInMilliseconds);
              totalBytesRead += bytesRead;
              bytesReadInLastPeriod += bytesRead;
              bytesReadInTotal += bytesRead;

              var progress = (int)(100.0 * totalBytesRead / totalContentLength);

              var totalMeasurementSeconds = speedMeasurementStopwatch.Elapsed.TotalSeconds;
              if (totalMeasurementSeconds >= speedMeasurementPeriodInSeconds)
              {
                await UpdateProgress(progress, bytesReadInLastPeriod / totalMeasurementSeconds, bytesReadInTotal / speedMeasurementTotalStopwatch.Elapsed.TotalSeconds);

                bytesReadInLastPeriod = 0;
                speedMeasurementStopwatch.Restart();
              }
            }
          }

          // Complete the download
          stream.Close();
          File.Move(tempFilePath, localFilePath);


                    await using (var context = await _contextFactory.CreateDbContextAsync())
                    {
                        var dbQueueItem = await context.FileDownloadQueueItems.FindAsync(queueItem.Id);
                        if (dbQueueItem != null)
                        {
                            dbQueueItem.ProgressPercentage = 100;
                            dbQueueItem.Status = EQueueItemStatus.Finished;
                            
                            try
                            {
                                await context.SaveChangesAsync();
                                
                                // Update local instance
                                queueItem.ProgressPercentage = 100;
                                queueItem.Status = EQueueItemStatus.Finished;
                            }
                            catch (DbUpdateConcurrencyException)
                            {
                                context.Entry(dbQueueItem).State = EntityState.Detached;
                                _logger.LogWarning("Concurrency conflict when updating queue item {ItemId} status", queueItem.Id);
                            }
                        }

                        var downloadProgress = new DownloadProgress()
                        {
                            ItemId = queueItem.Id,
                            ProgressPercentage = 100,
                            CurrentSpeed = queueItem.CurrentDownloadSpeed,
                            AverageSpeed = queueItem.AverageDownloadSpeed
                        };
                        await _progressHubService.BroadcastProgressUpdate(downloadProgress);
                    }


                }

                // Fixing file permissions on linux
                if (OperatingSystem.IsLinux()) File.SetUnixFileMode(localFilePath,
            UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead |
            UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite |
            UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
      }
      catch (Exception)
      {
        throw;
      }
    }

    public async Task<VideoInfo> GetFileInfo(string url)
    {
      var fileInfo = new VideoInfo();
      try
      {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url);
        HttpResponseMessage response = await _httpClient.SendAsync(request);

        // Check if authentication is required
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
          fileInfo.RequiresLogin = true;
          fileInfo.Errors = "Access denied: authentication required.";
          return fileInfo;
        }

        if (!response.IsSuccessStatusCode)
        {
          fileInfo.Errors = $"Failed to retrieve file. Status Code: {response.StatusCode}";
          return fileInfo;
        }

        fileInfo.Title = GetTitle(response, url);
        fileInfo.Filename = fileInfo.Title;
        fileInfo.Ext = GetFileExtension(fileInfo.Title);
        fileInfo.FileSize = GetFileSize(response);

      }
      catch (Exception ex)
      {
        fileInfo.Errors = $"Error: {ex.Message}";
      }

      return fileInfo;
    }

    private string GetTitle(HttpResponseMessage response, string url)
    {
      if (response.Content.Headers.ContentDisposition != null)
      {
        return response.Content.Headers.ContentDisposition.FileName?.Trim('"') ?? "Unknown";
      }

      Uri uri = new Uri(url);
      return HttpUtility.UrlDecode(uri.Segments[^1]); // Extract title from URL
    }

    private string GetFileExtension(string title)
    {
      return title.Contains('.') ? title.Substring(title.LastIndexOf('.') + 1) : "Unknown";
    }

    private long? GetFileSize(HttpResponseMessage response)
    {
      return response.Content.Headers.ContentLength ?? null;
    }

    public async Task<bool> CheckRangeSupport(string url, CancellationToken cancellationToken)
    {
      try
      {
        var request = new HttpRequestMessage(HttpMethod.Head, url);
        request.Headers.Range = new RangeHeaderValue(0, 0);
        
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return response.StatusCode == HttpStatusCode.PartialContent || 
               response.Headers.AcceptRanges?.Contains("bytes") == true;
      }
      catch
      {
        return false;
      }
    }

    public async Task DownloadFileParallel(FileDownloadQueueItem queueItem, CancellationToken cancellationToken)
    {
      var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, queueItem.InputUrl), cancellationToken);
      var contentLength = response.Content.Headers.ContentLength;
      
      if (!contentLength.HasValue)
      {
        // Fall back to sequential download
        await DownloadFile(queueItem, cancellationToken);
        return;
      }

      if (queueItem.InputUrl != null)
      {
        var (localFilePath, tempFilePath) = PrepareDownloadPath(queueItem);

        // Calculate chunk size
        var totalSize = contentLength.Value;
        var chunkSize = totalSize / queueItem.ParallelConnections;
        var tasks = new List<Task>();
        var chunkFiles = new List<string>();

        // Create thread-safe progress tracker
        var progressTracker = new ParallelDownloadProgress(queueItem.ParallelConnections, totalSize, queueItem.Id, _contextFactory, _progressHubService);
        
        for (int i = 0; i < queueItem.ParallelConnections; i++)
        {
          var chunkStart = i * chunkSize;
          var chunkEnd = (i == queueItem.ParallelConnections - 1) ? totalSize - 1 : (i + 1) * chunkSize - 1;
          var chunkFile = $"{tempFilePath}.part{i}";
          chunkFiles.Add(chunkFile);

          progressTracker.SetChunkSize(i, chunkEnd - chunkStart + 1);
          tasks.Add(DownloadChunk(queueItem, chunkStart, chunkEnd, chunkFile, i, cancellationToken, progressTracker));
        }

        await Task.WhenAll(tasks);
        progressTracker.Dispose();

        // Merge chunks
        await MergeChunks(chunkFiles, localFilePath, cancellationToken);
      }
      else
      {
        throw new ArgumentException("InputUrl cannot be null");
      }

      // Update status
      await using var context = await _contextFactory.CreateDbContextAsync();
      var dbQueueItem = await context.FileDownloadQueueItems.FindAsync(queueItem.Id, cancellationToken);
      if (dbQueueItem != null)
      {
        dbQueueItem.ProgressPercentage = 100;
        dbQueueItem.Status = EQueueItemStatus.Finished;
        
        // Use optimistic concurrency handling
        try
        {
          await context.SaveChangesAsync(cancellationToken);
          
          // Update the local instance for consistency
          queueItem.ProgressPercentage = 100;
          queueItem.Status = EQueueItemStatus.Finished;
        }
        catch (DbUpdateConcurrencyException)
        {
          // Handle concurrency conflicts gracefully
          context.Entry(dbQueueItem).State = EntityState.Detached;
          _logger.LogWarning("Concurrency conflict when updating queue item {ItemId} status", queueItem.Id);
        }
      }

      var downloadProgress = new DownloadProgress()
      {
        ItemId = queueItem.Id,
        ProgressPercentage = 100,
        CurrentSpeed = queueItem.CurrentDownloadSpeed,
        AverageSpeed = queueItem.AverageDownloadSpeed
      };
      await _progressHubService.BroadcastProgressUpdate(downloadProgress);
    }

    private async Task DownloadChunk(FileDownloadQueueItem queueItem, long start, long end, string chunkFile, int chunkIndex, CancellationToken cancellationToken, ParallelDownloadProgress progressTracker)
    {
      var request = new HttpRequestMessage(HttpMethod.Get, queueItem.InputUrl);
      request.Headers.Range = new RangeHeaderValue(start, end);

      using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
      response.EnsureSuccessStatusCode();

      using var download = await response.Content.ReadAsStreamAsync();
      using var stream = new FileStream(chunkFile, FileMode.Create, FileAccess.Write);

      var bufferSize = queueItem.BufferSizeKB * 1024;
      var buffer = new byte[bufferSize];
      var totalBytesRead = 0L;
      var chunkTotal = end - start + 1;
      int bytesRead;

      var speedMeasurementStopwatch = Stopwatch.StartNew();
      long bytesReadInLastPeriod = 0;

      while ((bytesRead = await download.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
      {
        if (cancellationToken.IsCancellationRequested)
        {
          throw new OperationCanceledException(cancellationToken);
        }

        await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
        totalBytesRead += bytesRead;
        bytesReadInLastPeriod += bytesRead;

        // Update progress tracker periodically
        var totalMeasurementSeconds = speedMeasurementStopwatch.Elapsed.TotalSeconds;
        if (totalMeasurementSeconds >= 1)
        {
          var currentSpeed = bytesReadInLastPeriod / totalMeasurementSeconds;
          progressTracker.UpdateChunkProgress(chunkIndex, totalBytesRead, currentSpeed);

          bytesReadInLastPeriod = 0;
          speedMeasurementStopwatch.Restart();
        }
      }
      
      // Final update for this chunk
      progressTracker.UpdateChunkProgress(chunkIndex, totalBytesRead, bytesReadInLastPeriod / speedMeasurementStopwatch.Elapsed.TotalSeconds);
    }

    private static (string localFilePath, string tempFilePath) PrepareDownloadPath(FileDownloadQueueItem queueItem, string? customFileName = null)
    {
      var fileName = string.IsNullOrEmpty(customFileName) 
        ? (string.IsNullOrEmpty(queueItem.CustomFileName)
          ? Helpers.Helpers.ExtractFileNameFromUrl(queueItem.InputUrl!)
          : queueItem.CustomFileName)
        : customFileName;

      var downloadPath = Path.Combine(Directory.GetCurrentDirectory(), "downloads");

      // Adding custom folders if required
      if (!string.IsNullOrWhiteSpace(queueItem.DownloadFolder))
      {
        string combinedPath = Path.Combine(downloadPath, queueItem.DownloadFolder);
        string normalizedFullPath = Path.GetFullPath(combinedPath);
        string normalizedBasePath = Path.GetFullPath(downloadPath);

        if (normalizedFullPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
        {
          downloadPath = normalizedFullPath;
        }
        else throw new UnauthorizedAccessException("Invalid folder path.");

        // Create directory if it doesn't exist
        Directory.CreateDirectory(downloadPath);
      }

      // Construct the file path using the base directory and the sanitized filename
      var localFilePath = Path.GetFullPath(Path.Combine(downloadPath, fileName));

      // Ensure the file path is within the intended directory
      if (!localFilePath.StartsWith(Path.GetFullPath(downloadPath), StringComparison.OrdinalIgnoreCase))
      {
        throw new UnauthorizedAccessException("Invalid file path.");
      }

      var tempFilePath = localFilePath + ".tmp";
      return (localFilePath, tempFilePath);
    }

    private static async Task MergeChunks(List<string> chunkFiles, string outputFile, CancellationToken cancellationToken)
    {
      using var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
      
      foreach (var chunkFile in chunkFiles)
      {
        using var inputStream = new FileStream(chunkFile, FileMode.Open, FileAccess.Read);
        await inputStream.CopyToAsync(outputStream, cancellationToken);
        inputStream.Close();
        File.Delete(chunkFile);
      }
    }
  }
}
