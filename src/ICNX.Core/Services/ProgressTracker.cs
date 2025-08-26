using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ICNX.Core.Interfaces;
using ICNX.Core.Models;
using Microsoft.Extensions.Logging;

namespace ICNX.Core.Services;

/// <summary>
/// Progress tracking service with batched updates and reactive streams
/// </summary>
public class ProgressTracker : IProgressTracker, IDisposable
{
    private readonly ILogger<ProgressTracker> _logger;
    private readonly IEventAggregator _eventAggregator;
    private readonly Subject<ProgressUpdate> _progressSubject = new();
    private readonly ConcurrentDictionary<string, ProgressUpdate> _currentProgress = new();
    private readonly Timer _batchTimer;
    private readonly ConcurrentQueue<ProgressUpdate> _pendingUpdates = new();
    private readonly SemaphoreSlim _batchLock = new(1, 1);

    private bool _disposed = false;

    public ProgressTracker(ILogger<ProgressTracker> logger)
    {
        _logger = logger;

        // Batch progress updates every 250ms to avoid overwhelming subscribers
        _batchTimer = new Timer(FlushPendingUpdates, null, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
    }

    // New constructor used by tests which expect an event aggregator dependency
    public ProgressTracker(IEventAggregator eventAggregator, ILogger<ProgressTracker> logger)
        : this(logger)
    {
        _eventAggregator = eventAggregator;
    }

    /// <summary>
    /// Async-friendly report method expected by tests. Validates input, enriches the update, and publishes via event aggregator.
    /// </summary>
    public async Task ReportProgressAsync(ProgressUpdate? update)
    {
        if (update == null) throw new ArgumentNullException(nameof(update));
        if (string.IsNullOrWhiteSpace(update.SessionId)) throw new ArgumentException("SessionId is required", nameof(update.SessionId));
        if (string.IsNullOrWhiteSpace(update.ItemId)) throw new ArgumentException("ItemId is required", nameof(update.ItemId));

        // Set timestamp/updatedat
        update.UpdatedAt = DateTime.UtcNow;

        // Compute estimated time remaining if possible
        if (update.SpeedBytesPerSec.HasValue && update.SpeedBytesPerSec.Value > 0 && update.TotalBytes.HasValue)
        {
            var remaining = Math.Max(0, (double)(update.TotalBytes.Value - update.DownloadedBytes));
            update.EstimatedTimeRemaining = TimeSpan.FromSeconds(remaining / update.SpeedBytesPerSec.Value);
        }
        else
        {
            update.EstimatedTimeRemaining = null;
        }

        try
        {
            // Update internal structures synchronously for later batched consumers
            ReportProgress(update);

            // Publish via event aggregator if available
            if (_eventAggregator != null)
            {
                await _eventAggregator.PublishAsync(update);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report progress asynchronously");
            throw;
        }
    }

    public void ReportProgress(ProgressUpdate update)
    {
        if (_disposed) return;

        try
        {
            var key = $"{update.SessionId}_{update.ItemId}";

            // Store current progress for lookup
            _currentProgress.AddOrUpdate(key, update, (_, _) => update);

            // Queue for batched processing
            _pendingUpdates.Enqueue(update);

            // Immediate processing for critical updates (completed, failed, cancelled)
            if (IsCriticalUpdate(update.Status))
            {
                _progressSubject.OnNext(update);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report progress for {SessionId}_{ItemId}",
                update.SessionId, update.ItemId);
        }
    }

    public ProgressUpdate? GetCurrentProgress(string sessionId, string itemId)
    {
        var key = $"{sessionId}_{itemId}";
        _currentProgress.TryGetValue(key, out var progress);
        return progress;
    }

    public IObservable<ProgressUpdate> GetProgressStream()
    {
        return _progressSubject.AsObservable();
    }

    public IObservable<ProgressUpdate> GetSessionProgressStream(string sessionId)
    {
        return _progressSubject
            .AsObservable()
            .Where(update => update.SessionId == sessionId);
    }

    public async Task ClearCompletedProgressAsync()
    {
        try
        {
            var keysToRemove = new List<string>();

            foreach (var kvp in _currentProgress)
            {
                if (IsCompletedStatus(kvp.Value.Status))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _currentProgress.TryRemove(key, out _);
            }

            _logger.LogInformation("Cleared {Count} completed progress entries", keysToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear completed progress");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Get progress summary for a session
    /// </summary>
    public SessionProgressSummary GetSessionSummary(string sessionId)
    {
        var sessionUpdates = _currentProgress.Values
            .Where(p => p.SessionId == sessionId)
            .ToList();

        if (!sessionUpdates.Any())
        {
            return new SessionProgressSummary
            {
                SessionId = sessionId,
                TotalItems = 0,
                CompletedItems = 0,
                FailedItems = 0,
                CancelledItems = 0,
                ActiveItems = 0,
                TotalBytes = 0,
                DownloadedBytes = 0,
                AverageSpeed = 0,
                OverallProgress = 0
            };
        }

        var totalItems = sessionUpdates.Count;
        var completedItems = sessionUpdates.Count(u => u.Status == DownloadStatus.Completed);
        var failedItems = sessionUpdates.Count(u => u.Status == DownloadStatus.Failed);
        var cancelledItems = sessionUpdates.Count(u => u.Status == DownloadStatus.Cancelled);
        var activeItems = sessionUpdates.Count(u => IsActiveStatus(u.Status));

        var totalBytes = sessionUpdates.Sum(u => u.TotalBytes ?? 0);
        var downloadedBytes = sessionUpdates.Sum(u => u.DownloadedBytes);

        var speedUpdates = sessionUpdates.Where(u => u.SpeedBytesPerSec.HasValue).ToList();
        var averageSpeed = speedUpdates.Any() ? speedUpdates.Average(u => u.SpeedBytesPerSec!.Value) : 0;

        var overallProgress = totalBytes > 0 ? (double)downloadedBytes / totalBytes * 100 : 0;

        return new SessionProgressSummary
        {
            SessionId = sessionId,
            TotalItems = totalItems,
            CompletedItems = completedItems,
            FailedItems = failedItems,
            CancelledItems = cancelledItems,
            ActiveItems = activeItems,
            TotalBytes = totalBytes,
            DownloadedBytes = downloadedBytes,
            AverageSpeed = averageSpeed,
            OverallProgress = Math.Min(100, Math.Max(0, overallProgress))
        };
    }

    private async void FlushPendingUpdates(object? state)
    {
        if (!await _batchLock.WaitAsync(10)) return; // Quick timeout to avoid blocking

        try
        {
            var updates = new List<ProgressUpdate>();

            // Dequeue up to 100 updates at a time
            while (_pendingUpdates.TryDequeue(out var update) && updates.Count < 100)
            {
                // Skip duplicates for the same item (keep only latest)
                var existingIndex = updates.FindIndex(u => u.SessionId == update.SessionId && u.ItemId == update.ItemId);
                if (existingIndex >= 0)
                {
                    updates[existingIndex] = update; // Replace with newer update
                }
                else
                {
                    updates.Add(update);
                }
            }

            // Emit batched updates
            foreach (var update in updates)
            {
                if (!IsCriticalUpdate(update.Status)) // Critical updates already sent immediately
                {
                    _progressSubject.OnNext(update);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush pending progress updates");
        }
        finally
        {
            _batchLock.Release();
        }
    }

    private static bool IsCriticalUpdate(DownloadStatus status)
    {
        return status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled;
    }

    private static bool IsCompletedStatus(DownloadStatus status)
    {
        return status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled;
    }

    private static bool IsActiveStatus(DownloadStatus status)
    {
        return status is DownloadStatus.Queued or DownloadStatus.Started or DownloadStatus.Downloading or DownloadStatus.Paused or DownloadStatus.Resumed;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _batchTimer?.Dispose();
        _batchLock?.Dispose();
        _progressSubject?.OnCompleted();
        _progressSubject?.Dispose();

        GC.SuppressFinalize(this);
    }
}