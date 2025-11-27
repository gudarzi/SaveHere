using Microsoft.EntityFrameworkCore;
using SaveHere.Models;
using SaveHere.Models.db;
using System.Collections.Concurrent;

namespace SaveHere.Services
{
    public class ParallelDownloadProgress : IDisposable
    {
        private readonly int _totalChunks;
        private readonly long _totalFileSize;
        private readonly int _itemId;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IProgressHubService _progressHubService;
        private readonly ConcurrentDictionary<int, ChunkProgress> _chunkProgress;
        private readonly Timer _progressTimer;
        private readonly object _lockObject = new object();
        private volatile bool _disposed = false;

        public ParallelDownloadProgress(
            int totalChunks, 
            long totalFileSize, 
            int itemId, 
            IDbContextFactory<AppDbContext> contextFactory, 
            IProgressHubService progressHubService)
        {
            _totalChunks = totalChunks;
            _totalFileSize = totalFileSize;
            _itemId = itemId;
            _contextFactory = contextFactory;
            _progressHubService = progressHubService;
            _chunkProgress = new ConcurrentDictionary<int, ChunkProgress>();
            
            // Update progress every 1 second
            _progressTimer = new Timer(UpdateProgress, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        public void SetChunkSize(int chunkIndex, long chunkSize)
        {
            _chunkProgress[chunkIndex] = new ChunkProgress
            {
                TotalSize = chunkSize,
                BytesDownloaded = 0,
                CurrentSpeed = 0,
                LastUpdateTime = DateTime.UtcNow
            };
        }

        public void UpdateChunkProgress(int chunkIndex, long bytesDownloaded, double currentSpeed)
        {
            if (_disposed) return;

            _chunkProgress.AddOrUpdate(chunkIndex, 
                new ChunkProgress 
                { 
                    TotalSize = bytesDownloaded, 
                    BytesDownloaded = bytesDownloaded, 
                    CurrentSpeed = currentSpeed,
                    LastUpdateTime = DateTime.UtcNow
                },
                (key, existing) => 
                {
                    existing.BytesDownloaded = bytesDownloaded;
                    existing.CurrentSpeed = currentSpeed;
                    existing.LastUpdateTime = DateTime.UtcNow;
                    return existing;
                });
        }

        private async void UpdateProgress(object? state)
        {
            if (_disposed) return;

            try
            {
                var (totalBytesDownloaded, currentSpeed, averageSpeed, progressPercentage) = CalculateProgress();

                // Update database
                await using var context = await _contextFactory.CreateDbContextAsync();
                var queueItem = await context.FileDownloadQueueItems.FindAsync(_itemId);
                if (queueItem != null)
                {
                    queueItem.ProgressPercentage = progressPercentage;
                    queueItem.CurrentDownloadSpeed = currentSpeed;
                    queueItem.AverageDownloadSpeed = averageSpeed;
                    
                    // Use optimistic concurrency - if update fails, ignore (another thread updated it)
                    try
                    {
                        await context.SaveChangesAsync();
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        // Ignore concurrency exceptions in progress updates
                        context.Entry(queueItem).State = EntityState.Detached;
                    }
                }

                // Broadcast progress
                var downloadProgress = new DownloadProgress
                {
                    ItemId = _itemId,
                    ProgressPercentage = progressPercentage,
                    CurrentSpeed = currentSpeed,
                    AverageSpeed = averageSpeed
                };
                await _progressHubService.BroadcastProgressUpdate(downloadProgress);
            }
            catch (Exception)
            {
                // Ignore errors in progress reporting to avoid crashing downloads
            }
        }

        private (long totalBytesDownloaded, double currentSpeed, double averageSpeed, int progressPercentage) CalculateProgress()
        {
            lock (_lockObject)
            {
                if (_disposed) return (0, 0, 0, 0);

                var totalBytesDownloaded = 0L;
                var totalCurrentSpeed = 0.0;
                var activeChunks = 0;
                var now = DateTime.UtcNow;

                foreach (var chunk in _chunkProgress.Values)
                {
                    totalBytesDownloaded += chunk.BytesDownloaded;
                    
                    // Only count speed from chunks that have been updated recently (within last 5 seconds)
                    if ((now - chunk.LastUpdateTime).TotalSeconds < 5)
                    {
                        totalCurrentSpeed += chunk.CurrentSpeed;
                        activeChunks++;
                    }
                }

                var progressPercentage = _totalFileSize > 0 ? 
                    Math.Min(99, (int)(100.0 * totalBytesDownloaded / _totalFileSize)) : 0;

                // Average speed calculation (simplified - could be improved with time-weighted average)
                var averageSpeed = activeChunks > 0 ? totalCurrentSpeed / activeChunks : totalCurrentSpeed;

                return (totalBytesDownloaded, totalCurrentSpeed, averageSpeed, progressPercentage);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            _progressTimer?.Dispose();
            
            // Final progress update
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var context = await _contextFactory.CreateDbContextAsync();
                    var queueItem = await context.FileDownloadQueueItems.FindAsync(_itemId);
                    if (queueItem != null)
                    {
                        queueItem.ProgressPercentage = 100;
                        queueItem.Status = EQueueItemStatus.Finished;
                        await context.SaveChangesAsync();
                    }
                }
                catch
                {
                    // Ignore errors in final update
                }
            });
        }

        private class ChunkProgress
        {
            public long TotalSize { get; set; }
            public long BytesDownloaded { get; set; }
            public double CurrentSpeed { get; set; }
            public DateTime LastUpdateTime { get; set; }
        }
    }
}