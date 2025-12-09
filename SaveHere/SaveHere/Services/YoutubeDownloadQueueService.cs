using Microsoft.EntityFrameworkCore;
using SaveHere.Models;
using SaveHere.Models.db;
using SaveHere.Models.SaveHere.Models;
using System.Collections.Concurrent;

namespace SaveHere.Services
{
  public interface IYoutubeDownloadQueueService
  {
    Task<List<YoutubeDownloadQueueItem>> GetQueueItemsAsync();
    Task<YoutubeDownloadQueueItem?> GetQueueItemByIdAsync(int id);
    Task<YoutubeDownloadQueueItem> AddQueueItemAsync(string url, string? customFileName, string selectedQuality, string proxyUrl, string? downloadFolderName, string? subtitleLanguage);
    //Task UpdateQueueItemAsync(YoutubeDownloadQueueItem item);
    Task UpdateItemStateAsync(int itemId, EQueueItemStatus newStatus);
    Task DeleteQueueItemAsync(int id);
    Task StartDownloadAsync(YoutubeDownloadQueueItem item);
    Task CancelDownloadAsync(int id);
    Task AppendLogAsync(int itemId, string logLine);
    Task FlushLogsAsync(int itemId);
  }

  public class YoutubeDownloadQueueService : IYoutubeDownloadQueueService
  {
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly DownloadStateService _downloadStateService;
    private readonly ILogger<YoutubeDownloadQueueService> _logger;
    private readonly IProgressHubService _progressHubService;
    private readonly IYtdlpService _ytdlpService;

    // Log batching: accumulate logs in memory and flush periodically
    private readonly ConcurrentDictionary<int, ConcurrentQueue<string>> _pendingLogs = new();
    private readonly ConcurrentDictionary<int, DateTime> _lastFlushTime = new();
    private readonly TimeSpan _logFlushInterval = TimeSpan.FromSeconds(5);

    public YoutubeDownloadQueueService(
        IDbContextFactory<AppDbContext> contextFactory,
        DownloadStateService downloadStateService,
        ILogger<YoutubeDownloadQueueService> logger,
        IProgressHubService progressHubService,
        IYtdlpService ytdlpService)
    {
      _contextFactory = contextFactory;
      _downloadStateService = downloadStateService;
      _logger = logger;
      _progressHubService = progressHubService;
      _ytdlpService = ytdlpService;
    }

    public async Task<List<YoutubeDownloadQueueItem>> GetQueueItemsAsync()
    {
      await using var context = await _contextFactory.CreateDbContextAsync();
      var items = await context.YoutubeDownloadQueueItems.ToListAsync();
      foreach (var item in items)
      {
        if (!string.IsNullOrEmpty(item.PersistedLog))
        {
          item.OutputLog = item.PersistedLog
              .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
              .ToList();
        }
        else
        {
          item.OutputLog = new List<string>();
        }
      }
      return items;
    }

    public async Task<YoutubeDownloadQueueItem?> GetQueueItemByIdAsync(int id)
    {
      await using var context = await _contextFactory.CreateDbContextAsync();
      var item = await context.YoutubeDownloadQueueItems.FindAsync(id);
      if (item != null && !string.IsNullOrEmpty(item.PersistedLog))
      {
        item.OutputLog = item.PersistedLog
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
      }
      return item;
    }


    public async Task<YoutubeDownloadQueueItem> AddQueueItemAsync(string url, string? customFileName, string selectedQuality, string proxyUrl, string? downloadFolderName, string? subtitleLanguage)
    {
      if (string.IsNullOrWhiteSpace(url))
      {
        throw new ArgumentException("URL cannot be empty", nameof(url));
      }

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
      {
        throw new ArgumentException("URL is not valid", nameof(url));
      }

      var item = new YoutubeDownloadQueueItem
      {
        Url = url,
        CustomFileName = customFileName,
        Quality = selectedQuality,
        Proxy = proxyUrl,
        DownloadFolder = downloadFolderName,
        SubtitleLanguage = subtitleLanguage,
        Status = EQueueItemStatus.Paused
      };

      await using var context = await _contextFactory.CreateDbContextAsync();
      context.YoutubeDownloadQueueItems.Add(item);
      await context.SaveChangesAsync();

      return item;
    }

    //public async Task UpdateQueueItemAsync(YoutubeDownloadQueueItem item)
    //{
    //  await using var context = await _contextFactory.CreateDbContextAsync();
    //  context.YoutubeDownloadQueueItems.Update(item);
    //  await context.SaveChangesAsync();
    //}

    public async Task UpdateItemStateAsync(int itemId, EQueueItemStatus newStatus)
    {
      await using var context = await _contextFactory.CreateDbContextAsync();
      var item = await context.YoutubeDownloadQueueItems.FindAsync(itemId);
      if (item != null)
      {
        // Only update the status, preserving all other fields
        item.Status = newStatus;
        await context.SaveChangesAsync();
      }
    }

    public async Task DeleteQueueItemAsync(int id)
    {
      var item = await GetQueueItemByIdAsync(id);
      if (item != null)
      {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.YoutubeDownloadQueueItems.Remove(item);
        _downloadStateService.RemoveTokenSource(id);
        await context.SaveChangesAsync();
      }
    }


    public async Task CancelDownloadAsync(int id)
    {
      var item = await GetQueueItemByIdAsync(id);
      if (item == null) return;
      if (item.Status == EQueueItemStatus.Cancelled) return;

      // Update status immediately
      item.Status = EQueueItemStatus.Cancelled;
      await UpdateItemStateAsync(item.Id, EQueueItemStatus.Cancelled);
      await _progressHubService.BroadcastStateChange(id, item.Status.ToString());

      var tokenSource = _downloadStateService.GetOrAddTokenSource(id);
      tokenSource.Cancel();
    }

    public async Task StartDownloadAsync(YoutubeDownloadQueueItem item)
    {
      if (item == null) throw new Exception("Item not found");
      if (item.Status == EQueueItemStatus.Downloading) throw new Exception("Item is already downloading");

      var tokenSource = _downloadStateService.GetOrAddTokenSource(item.Id);
      var token = tokenSource.Token;

      try
      {
        // Clear previous logs when starting a new download
        CleanupLogsForItem(item.Id);
        item.OutputLog.Clear();
        item.PersistedLog = string.Empty;
        item.Status = EQueueItemStatus.Downloading;
        await UpdateItemStateAsync(item.Id, EQueueItemStatus.Downloading);
        await _progressHubService.BroadcastStateChange(item.Id, item.Status.ToString());

        await _ytdlpService.DownloadVideo(item.Id, item.Url, item.CustomFileName, item.Quality, item.Proxy, item.DownloadFolder,
         item.SubtitleLanguage, token);

        // Flush any remaining logs before marking as finished
        await FlushLogsAsync(item.Id);

        // Ensure we capture all logs before setting status to finished
        var currentLogs = item.OutputLog.ToList();
        item.Status = EQueueItemStatus.Finished;
        item.PersistedLog = string.Join(Environment.NewLine, currentLogs);
        await UpdateItemStateAsync(item.Id, EQueueItemStatus.Finished);
        await _progressHubService.BroadcastStateChange(item.Id, item.Status.ToString());
      }
      catch (OperationCanceledException)
      {
        await FlushLogsAsync(item.Id);
        item.Status = EQueueItemStatus.Cancelled;
        item.PersistedLog = string.Join(Environment.NewLine, item.OutputLog);
        await UpdateItemStateAsync(item.Id, EQueueItemStatus.Cancelled);
        await _progressHubService.BroadcastStateChange(item.Id, item.Status.ToString());
        throw;
      }
      catch (Exception ex)
      {
        await FlushLogsAsync(item.Id);
        _logger.LogError(ex, "Error downloading video for item {id}: {message}", item.Id, ex.Message);
        item.Status = EQueueItemStatus.Paused;
        item.PersistedLog = string.Join(Environment.NewLine, item.OutputLog);
        await UpdateItemStateAsync(item.Id, EQueueItemStatus.Paused);
        await _progressHubService.BroadcastStateChange(item.Id, item.Status.ToString());
        throw;
      }
      finally
      {
        CleanupLogsForItem(item.Id);
        _downloadStateService.RemoveTokenSource(item.Id);
      }
    }

    public Task AppendLogAsync(int itemId, string logLine)
    {
      // Add log to pending queue (in-memory, no DB hit)
      var queue = _pendingLogs.GetOrAdd(itemId, _ => new ConcurrentQueue<string>());
      queue.Enqueue(logLine);

      // Check if we should flush based on time interval
      var now = DateTime.UtcNow;
      var lastFlush = _lastFlushTime.GetOrAdd(itemId, DateTime.MinValue);

      if (now - lastFlush >= _logFlushInterval)
      {
        // Fire and forget the flush - don't await to avoid blocking
        _ = FlushLogsAsync(itemId);
      }

      return Task.CompletedTask;
    }

    public async Task FlushLogsAsync(int itemId)
    {
      try
      {
        if (!_pendingLogs.TryGetValue(itemId, out var queue) || queue.IsEmpty)
        {
          return;
        }

        // Drain all pending logs
        var logsToFlush = new List<string>();
        while (queue.TryDequeue(out var log))
        {
          logsToFlush.Add(log);
        }

        if (logsToFlush.Count == 0) return;

        _lastFlushTime[itemId] = DateTime.UtcNow;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var item = await context.YoutubeDownloadQueueItems.FindAsync(itemId);
        if (item != null)
        {
          // Append all new logs at once
          var existingLogs = string.IsNullOrEmpty(item.PersistedLog)
              ? new List<string>()
              : item.PersistedLog.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();

          existingLogs.AddRange(logsToFlush);
          item.PersistedLog = string.Join(Environment.NewLine, existingLogs);
          item.OutputLog = existingLogs;

          try
          {
            await context.SaveChangesAsync();
          }
          catch (DbUpdateConcurrencyException)
          {
            // Ignore concurrency conflicts for logs
            context.Entry(item).State = EntityState.Detached;
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error flushing logs to database for item {itemId}: {Message}", itemId, ex.Message);
      }
    }

    private void CleanupLogsForItem(int itemId)
    {
      _pendingLogs.TryRemove(itemId, out _);
      _lastFlushTime.TryRemove(itemId, out _);
    }

  }
}