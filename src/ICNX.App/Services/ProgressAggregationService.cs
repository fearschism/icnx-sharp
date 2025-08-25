using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using ICNX.Core.Interfaces;
using ICNX.Core.Models;
using Microsoft.Extensions.Logging;

namespace ICNX.App.Services;

/// <summary>
/// Advanced progress aggregation with real-time analytics
/// </summary>
public class ProgressAggregationService : IDisposable
{
    private readonly IProgressTracker _progressTracker;
    private readonly ILogger<ProgressAggregationService> _logger;
    private readonly Subject<SessionStats> _sessionStatsSubject = new();
    private readonly Timer _statsCalculationTimer;
    private readonly ConcurrentDictionary<string, SessionStatsCalculator> _sessionCalculators = new();
    private bool _disposed = false;

    public ProgressAggregationService(IProgressTracker progressTracker, ILogger<ProgressAggregationService> logger)
    {
        _progressTracker = progressTracker;
        _logger = logger;

        // Subscribe to progress updates
        _progressTracker.GetProgressStream()
            .Subscribe(OnProgressUpdate);

        // Calculate session statistics every 1 second
        _statsCalculationTimer = new Timer(CalculateSessionStats, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Stream of real-time session statistics
    /// </summary>
    public IObservable<SessionStats> SessionStatsStream => _sessionStatsSubject.AsObservable();

    /// <summary>
    /// Get current session statistics
    /// </summary>
    public SessionStats? GetSessionStats(string sessionId)
    {
        return _sessionCalculators.TryGetValue(sessionId, out var calculator) 
            ? calculator.GetCurrentStats() 
            : null;
    }

    /// <summary>
    /// Get aggregated statistics for all active sessions
    /// </summary>
    public GlobalStats GetGlobalStats()
    {
        var allCalculators = _sessionCalculators.Values.ToList();
        
        return new GlobalStats
        {
            ActiveSessions = allCalculators.Count,
            TotalDownloadSpeed = allCalculators.Sum(c => c.GetCurrentStats().AverageDownloadSpeed),
            TotalActiveItems = allCalculators.Sum(c => c.GetCurrentStats().ActiveItems),
            TotalCompletedItems = allCalculators.Sum(c => c.GetCurrentStats().CompletedItems),
            TotalFailedItems = allCalculators.Sum(c => c.GetCurrentStats().FailedItems)
        };
    }

    private void OnProgressUpdate(ProgressUpdate update)
    {
        try
        {
            // Get or create session calculator
            var calculator = _sessionCalculators.GetOrAdd(update.SessionId, 
                _ => new SessionStatsCalculator(update.SessionId, _logger));

            // Update calculator with new progress
            calculator.UpdateProgress(update);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating session statistics for {SessionId}", update.SessionId);
        }
    }

    private void CalculateSessionStats(object? state)
    {
        try
        {
            var sessionsToRemove = new List<string>();

            foreach (var kvp in _sessionCalculators)
            {
                var sessionId = kvp.Key;
                var calculator = kvp.Value;

                var stats = calculator.GetCurrentStats();
                
                // Remove completed/inactive sessions after 30 seconds
                if (stats.IsCompleted && DateTime.UtcNow - stats.LastUpdateTime > TimeSpan.FromSeconds(30))
                {
                    sessionsToRemove.Add(sessionId);
                    continue;
                }

                // Emit updated stats
                _sessionStatsSubject.OnNext(stats);
            }

            // Clean up completed sessions
            foreach (var sessionId in sessionsToRemove)
            {
                if (_sessionCalculators.TryRemove(sessionId, out var calculator))
                {
                    calculator.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating session statistics");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _statsCalculationTimer?.Dispose();
        _sessionStatsSubject?.OnCompleted();
        _sessionStatsSubject?.Dispose();

        foreach (var calculator in _sessionCalculators.Values)
        {
            calculator.Dispose();
        }
        _sessionCalculators.Clear();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Calculates real-time statistics for a single session
/// </summary>
public class SessionStatsCalculator : IDisposable
{
    private readonly string _sessionId;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, ProgressHistory> _itemHistories = new();
    private readonly object _calculationLock = new();
    private SessionStats _currentStats;

    public SessionStatsCalculator(string sessionId, ILogger logger)
    {
        _sessionId = sessionId;
        _logger = logger;
        _currentStats = new SessionStats { SessionId = sessionId };
    }

    public void UpdateProgress(ProgressUpdate update)
    {
        if (update.SessionId != _sessionId) return;

        lock (_calculationLock)
        {
            // Get or create item history
            var itemHistory = _itemHistories.GetOrAdd(update.ItemId, _ => new ProgressHistory(update.ItemId));
            itemHistory.AddUpdate(update);

            // Recalculate session stats
            RecalculateStats();
        }
    }

    public SessionStats GetCurrentStats()
    {
        lock (_calculationLock)
        {
            return _currentStats;
        }
    }

    private void RecalculateStats()
    {
        var allHistories = _itemHistories.Values.ToList();
        var now = DateTime.UtcNow;

        var activeItems = allHistories.Count(h => h.IsActive);
        var completedItems = allHistories.Count(h => h.IsCompleted);
        var failedItems = allHistories.Count(h => h.IsFailed);
        
        var totalBytes = allHistories.Sum(h => h.TotalBytes);
        var downloadedBytes = allHistories.Sum(h => h.DownloadedBytes);
        
        // Calculate average download speed over the last 10 seconds
        var recentUpdates = allHistories
            .SelectMany(h => h.GetRecentUpdates(TimeSpan.FromSeconds(10)))
            .ToList();

        var averageSpeed = recentUpdates.Any() 
            ? recentUpdates.Average(u => u.SpeedBytesPerSec ?? 0)
            : 0;

        // Calculate ETA
        var remainingBytes = Math.Max(0, totalBytes - downloadedBytes);
        var eta = averageSpeed > 0 
            ? TimeSpan.FromSeconds(remainingBytes / averageSpeed)
            : TimeSpan.Zero;

        _currentStats = new SessionStats
        {
            SessionId = _sessionId,
            TotalItems = allHistories.Count,
            ActiveItems = activeItems,
            CompletedItems = completedItems,
            FailedItems = failedItems,
            TotalBytes = totalBytes,
            DownloadedBytes = downloadedBytes,
            OverallProgress = totalBytes > 0 ? (double)downloadedBytes / totalBytes * 100 : 0,
            AverageDownloadSpeed = averageSpeed,
            EstimatedTimeRemaining = eta,
            LastUpdateTime = now,
            IsCompleted = activeItems == 0 && allHistories.Any()
        };
    }

    public void Dispose()
    {
        _itemHistories.Clear();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Tracks progress history for a single download item
/// </summary>
public class ProgressHistory
{
    private readonly string _itemId;
    private readonly List<ProgressUpdate> _updates = new();
    private ProgressUpdate? _latestUpdate;

    public ProgressHistory(string itemId)
    {
        _itemId = itemId;
    }

    public void AddUpdate(ProgressUpdate update)
    {
        if (update.ItemId != _itemId) return;

        _updates.Add(update);
        _latestUpdate = update;

        // Keep only last 100 updates to prevent memory issues
        if (_updates.Count > 100)
        {
            _updates.RemoveRange(0, _updates.Count - 100);
        }
    }

    public IEnumerable<ProgressUpdate> GetRecentUpdates(TimeSpan timeWindow)
    {
        var cutoffTime = DateTime.UtcNow - timeWindow;
        return _updates.Where(u => u.Timestamp >= cutoffTime);
    }

    public bool IsActive => _latestUpdate?.Status is DownloadStatus.Downloading or DownloadStatus.Started or DownloadStatus.Queued;
    public bool IsCompleted => _latestUpdate?.Status == DownloadStatus.Completed;
    public bool IsFailed => _latestUpdate?.Status is DownloadStatus.Failed or DownloadStatus.Cancelled;
    
    public long TotalBytes => _latestUpdate?.TotalBytes ?? 0;
    public long DownloadedBytes => _latestUpdate?.DownloadedBytes ?? 0;
}

/// <summary>
/// Real-time session statistics
/// </summary>
public class SessionStats
{
    public string SessionId { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int ActiveItems { get; set; }
    public int CompletedItems { get; set; }
    public int FailedItems { get; set; }
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public double OverallProgress { get; set; }
    public double AverageDownloadSpeed { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public bool IsCompleted { get; set; }
}

/// <summary>
/// Global statistics across all sessions
/// </summary>
public class GlobalStats
{
    public int ActiveSessions { get; set; }
    public double TotalDownloadSpeed { get; set; }
    public int TotalActiveItems { get; set; }
    public int TotalCompletedItems { get; set; }
    public int TotalFailedItems { get; set; }
}