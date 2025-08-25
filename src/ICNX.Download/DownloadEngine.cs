using System.Collections.Concurrent;
using System.ComponentModel;
using Downloader;
using ICNX.Core.Interfaces;
using ICNX.Core.Models;
using Microsoft.Extensions.Logging;
using DownloadStatus = ICNX.Core.Models.DownloadStatus;

namespace ICNX.Download;

/// <summary>
/// Download engine implementation using the Downloader library
/// </summary>
public class DownloadEngine : IDownloadEngine
{
    private readonly ILogger<DownloadEngine> _logger;
    private readonly ConcurrentDictionary<string, IDownload> _activeDownloads = new();
    private readonly ConcurrentDictionary<string, DownloadConfiguration> _downloadConfigs = new();

    public DownloadEngine(ILogger<DownloadEngine> logger)
    {
        _logger = logger;
    }

    public async Task<bool> DownloadAsync(DownloadItem item, string destinationPath, 
        IProgress<ProgressUpdate> progress, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting download: {ItemId} -> {Url}", item.Id, item.Url);

            // Create download configuration
            var config = CreateDownloadConfiguration(item);
            
            // Create download instance
            var download = DownloadBuilder.New()
                .WithUrl(item.Url)
                .WithFileLocation(destinationPath)
                .WithFileName(item.Filename)
                .WithConfiguration(config)
                .Build();

            // Store for pause/resume operations
            _activeDownloads[item.Id] = download;
            _downloadConfigs[item.Id] = config;

            // Wire up progress events
            var progressHandler = new ProgressHandler(item, progress, _logger);
            download.DownloadStarted += progressHandler.OnDownloadStarted;
            download.DownloadProgressChanged += progressHandler.OnDownloadProgressChanged;
            download.DownloadFileCompleted += progressHandler.OnDownloadCompleted;
            download.ChunkDownloadProgressChanged += progressHandler.OnChunkProgressChanged;

            // Start download
            await download.StartAsync(cancellationToken);

            // Check final status
            var success = download.Status == Downloader.DownloadStatus.Completed;
            
            if (success)
            {
                _logger.LogInformation("Download completed successfully: {ItemId}", item.Id);
            }
            else
            {
                _logger.LogWarning("Download failed: {ItemId}, Status: {Status}", item.Id, download.Status);
            }

            return success;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Download cancelled: {ItemId}", item.Id);
            ReportProgress(item, DownloadStatus.Cancelled, item.DownloadedBytes, progress, "Download cancelled");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed with exception: {ItemId}", item.Id);
            ReportProgress(item, DownloadStatus.Failed, item.DownloadedBytes, progress, ex.Message);
            return false;
        }
        finally
        {
            // Cleanup
            _activeDownloads.TryRemove(item.Id, out _);
            _downloadConfigs.TryRemove(item.Id, out _);
        }
    }

    public bool SupportsResume(DownloadItem item)
    {
        // Most HTTP servers support range requests
        // The Downloader library will handle this automatically
        return !string.IsNullOrEmpty(item.Url) && item.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<int> GetOptimalChunkCountAsync(string url)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var contentLength = response.Content.Headers.ContentLength;
                var supportsRanges = response.Headers.AcceptRanges?.Contains("bytes") == true;

                if (contentLength.HasValue && supportsRanges)
                {
                    // Calculate optimal chunk count based on file size
                    var fileSizeMB = contentLength.Value / (1024 * 1024);
                    
                    return fileSizeMB switch
                    {
                        < 10 => 1,      // Small files: single chunk
                        < 100 => 4,     // Medium files: 4 chunks
                        < 500 => 6,     // Large files: 6 chunks
                        _ => 8          // Very large files: 8 chunks (max)
                    };
                }
            }

            return 1; // Default to single chunk if we can't determine optimal count
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine optimal chunk count for {Url}, using default", url);
            return 1;
        }
    }

    /// <summary>
    /// Pause an active download
    /// </summary>
    public bool PauseDownload(string itemId)
    {
        if (_activeDownloads.TryGetValue(itemId, out var download))
        {
            try
            {
                download.Pause();
                _logger.LogInformation("Download paused: {ItemId}", itemId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pause download: {ItemId}", itemId);
            }
        }
        
        return false;
    }

    /// <summary>
    /// Resume a paused download
    /// </summary>
    public bool ResumeDownload(string itemId)
    {
        if (_activeDownloads.TryGetValue(itemId, out var download))
        {
            try
            {
                download.Resume();
                _logger.LogInformation("Download resumed: {ItemId}", itemId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resume download: {ItemId}", itemId);
            }
        }
        
        return false;
    }

    /// <summary>
    /// Cancel an active download
    /// </summary>
    public bool CancelDownload(string itemId)
    {
        if (_activeDownloads.TryGetValue(itemId, out var download))
        {
            try
            {
                download.Stop();
                _logger.LogInformation("Download cancelled: {ItemId}", itemId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel download: {ItemId}", itemId);
            }
        }
        
        return false;
    }

    private DownloadConfiguration CreateDownloadConfiguration(DownloadItem item)
    {
        return new DownloadConfiguration
        {
            BufferBlockSize = 65536, // 64KB buffer
            ChunkCount = 4, // Will be determined dynamically
            ParallelDownload = true,
            MaxTryAgainOnFailure = 3,
            MaximumBytesPerSecond = 0, // No limit by default
            MaximumMemoryBufferBytes = 100 * 1024 * 1024, // 100MB
            Timeout = 30000, // 30 second timeout
            RangeDownload = false,
            ClearPackageOnCompletionWithFailure = true,
            MinimumSizeOfChunking = 1024 * 1024, // 1MB minimum for chunking
            ReserveStorageSpaceBeforeStartingDownload = true,
            RequestConfiguration = new RequestConfiguration
            {
                Accept = "*/*",
                UserAgent = "ICNX-Downloader/1.0",
                KeepAlive = true
                // Headers will be set separately if needed
            }
        };
    }

    private static void ReportProgress(DownloadItem item, DownloadStatus status, 
        long downloadedBytes, IProgress<ProgressUpdate> progress, string? error = null)
    {
        var update = new ProgressUpdate
        {
            SessionId = item.SessionId,
            ItemId = item.Id,
            Status = status,
            DownloadedBytes = downloadedBytes,
            TotalBytes = item.TotalBytes,
            Error = error,
            Timestamp = DateTime.UtcNow
        };

        progress?.Report(update);
    }
}

/// <summary>
/// Handles progress events from the Downloader library
/// </summary>
internal class ProgressHandler
{
    private readonly DownloadItem _item;
    private readonly IProgress<ProgressUpdate> _progress;
    private readonly ILogger _logger;
    private DateTime _lastProgressReport = DateTime.MinValue;
    private long _lastDownloadedBytes = 0;

    public ProgressHandler(DownloadItem item, IProgress<ProgressUpdate> progress, ILogger logger)
    {
        _item = item;
        _progress = progress;
        _logger = logger;
    }

    public void OnDownloadStarted(object? sender, DownloadStartedEventArgs e)
    {
        _logger.LogDebug("Download started: {ItemId}, Size: {TotalBytes}", _item.Id, e.TotalBytesToReceive);
        
        // Update item with actual file size
        _item.TotalBytes = e.TotalBytesToReceive;
        
        ReportProgress(Core.Models.DownloadStatus.Started, 0);
    }

    public void OnDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        var now = DateTime.UtcNow;
        
        // Throttle progress updates to avoid overwhelming the UI (max 4 updates per second)
        if ((now - _lastProgressReport).TotalMilliseconds < 250) return;
        
        _lastProgressReport = now;
        
        // Calculate speed
        var timeDiff = (now - _lastProgressReport).TotalSeconds;
        var bytesDiff = e.ReceivedBytesSize - _lastDownloadedBytes;
        var speed = timeDiff > 0 ? bytesDiff / timeDiff : 0;
        
        _lastDownloadedBytes = e.ReceivedBytesSize;

        var update = new ProgressUpdate
        {
            SessionId = _item.SessionId,
            ItemId = _item.Id,
            Status = DownloadStatus.Downloading,
            DownloadedBytes = e.ReceivedBytesSize,
            TotalBytes = e.TotalBytesToReceive > 0 ? e.TotalBytesToReceive : _item.TotalBytes,
            SpeedBytesPerSec = speed,
            EstimatedTimeRemaining = speed > 0 && e.TotalBytesToReceive > 0 
                ? TimeSpan.FromSeconds((e.TotalBytesToReceive - e.ReceivedBytesSize) / speed)
                : null,
            Timestamp = now
        };

        _progress?.Report(update);
    }

    public void OnDownloadCompleted(object? sender, AsyncCompletedEventArgs e)
    {
        var status = e.Cancelled ? DownloadStatus.Cancelled :
                    e.Error != null ? DownloadStatus.Failed :
                    DownloadStatus.Completed;

        var errorMessage = e.Error?.Message;
        
        if (status == DownloadStatus.Completed)
        {
            _logger.LogInformation("Download completed: {ItemId}", _item.Id);
        }
        else
        {
            _logger.LogWarning("Download finished with status {Status}: {ItemId}, Error: {Error}", 
                status, _item.Id, errorMessage);
        }

        ReportProgress(status, _item.DownloadedBytes, errorMessage);
    }

    public void OnChunkProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        // We can use chunk progress for more detailed monitoring if needed
        _logger.LogTrace("Chunk progress: {ItemId}, Chunk: {ActiveChunks}, Progress: {Progress}%", 
            _item.Id, e.ActiveChunks, e.ProgressPercentage);
    }

    private void ReportProgress(DownloadStatus status, long downloadedBytes, string? error = null)
    {
        var update = new ProgressUpdate
        {
            SessionId = _item.SessionId,
            ItemId = _item.Id,
            Status = status,
            DownloadedBytes = downloadedBytes,
            TotalBytes = _item.TotalBytes,
            Error = error,
            Timestamp = DateTime.UtcNow
        };

        _progress?.Report(update);
    }
}