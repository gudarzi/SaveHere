using Microsoft.EntityFrameworkCore;
using SaveHere.Models;
using SaveHere.Models.db;
using SaveHere.Models.SaveHere.Models;

namespace SaveHere.Services
{
  public interface IYoutubeDownloadQueueService
  {
    Task<List<YoutubeDownloadQueueItem>> GetQueueItemsAsync();
    Task<YoutubeDownloadQueueItem?> GetQueueItemByIdAsync(int id);
    Task<YoutubeDownloadQueueItem> AddQueueItemAsync(string url, string? customFileName, string selectedQuality, string proxyUrl, string? downloadFolderName);
    //Task UpdateQueueItemAsync(YoutubeDownloadQueueItem item);
    Task UpdateItemStateAsync(int itemId, EQueueItemStatus newStatus);
    Task DeleteQueueItemAsync(int id);
    Task StartDownloadAsync(YoutubeDownloadQueueItem item);
    Task CancelDownloadAsync(int id);
    Task AppendLogAsync(int itemId, string logLine);
  }

  public class YoutubeDownloadQueueService : IYoutubeDownloadQueueService
  {
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly DownloadStateService _downloadStateService;
    private readonly ILogger<YoutubeDownloadQueueService> _logger;
    private readonly IProgressHubService _progressHubService;
    private readonly IYtdlpService _ytdlpService;

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


    public async Task<YoutubeDownloadQueueItem> AddQueueItemAsync(string url, string? customFileName, string selectedQuality, string proxyUrl, string? downloadFolderName)
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
        item.OutputLog.Clear();
        item.PersistedLog = string.Empty;
        item.Status = EQueueItemStatus.Downloading;
        await UpdateItemStateAsync(item.Id, EQueueItemStatus.Downloading);
        await _progressHubService.BroadcastStateChange(item.Id, item.Status.ToString());

        await _ytdlpService.DownloadVideo(item.Id, item.Url, item.CustomFileName, item.Quality, item.Proxy, item.DownloadFolder, token);

        // Ensure we capture all logs before setting status to finished
        var currentLogs = item.OutputLog.ToList();
        item.Status = EQueueItemStatus.Finished;
        item.PersistedLog = string.Join(Environment.NewLine, currentLogs);
        await UpdateItemStateAsync(item.Id, EQueueItemStatus.Finished);
        await _progressHubService.BroadcastStateChange(item.Id, item.Status.ToString());
      }
      catch (OperationCanceledException)
      {
        item.Status = EQueueItemStatus.Cancelled;
        item.PersistedLog = string.Join(Environment.NewLine, item.OutputLog);
        await UpdateItemStateAsync(item.Id, EQueueItemStatus.Cancelled);
        await _progressHubService.BroadcastStateChange(item.Id, item.Status.ToString());
        throw;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error downloading video for item {id}: {message}", item.Id, ex.Message);
        item.Status = EQueueItemStatus.Paused;
        item.PersistedLog = string.Join(Environment.NewLine, item.OutputLog);
        await UpdateItemStateAsync(item.Id, EQueueItemStatus.Paused);
        await _progressHubService.BroadcastStateChange(item.Id, item.Status.ToString());
        throw;
      }
      finally
      {
        _downloadStateService.RemoveTokenSource(item.Id);
      }
    }

    public async Task AppendLogAsync(int itemId, string logLine)
    {
      try
      {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var item = await context.YoutubeDownloadQueueItems.FindAsync(itemId);
        if (item != null)
        {
          item.OutputLog = string.IsNullOrEmpty(item.PersistedLog)
              ? new List<string> { logLine }
              : item.PersistedLog.Split(Environment.NewLine).Concat(new[] { logLine }).ToList();

          item.PersistedLog = string.Join(Environment.NewLine, item.OutputLog);
          await context.SaveChangesAsync();
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error appending log to database for item {itemId}: {Message}", itemId, ex.Message);
      }
    }

  }
}