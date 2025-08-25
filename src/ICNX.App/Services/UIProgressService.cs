using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ICNX.Core.Interfaces;
using ICNX.Core.Models;
using Microsoft.Extensions.Logging;

namespace ICNX.App.Services;

/// <summary>
/// UI-focused progress service that handles thread marshalling for Avalonia
/// </summary>
public class UIProgressService : IDisposable
{
    private readonly IProgressTracker _progressTracker;
    private readonly ILogger<UIProgressService> _logger;
    private readonly IDisposable? _progressSubscription;
    private bool _disposed = false;

    public UIProgressService(IProgressTracker progressTracker, ILogger<UIProgressService> logger)
    {
        _progressTracker = progressTracker;
        _logger = logger;

        // Subscribe to progress updates and marshal to UI thread
        // Subscribe to progress updates directly - marshalling will be handled in OnProgressUpdate
        _progressSubscription = _progressTracker.GetProgressStream()
            .Subscribe(OnProgressUpdate, OnProgressError);
    }

    /// <summary>
    /// Event fired when progress updates occur (on UI thread)
    /// </summary>
    public event EventHandler<ProgressUpdate>? ProgressUpdated;

    /// <summary>
    /// Event fired when session progress summaries update (on UI thread)
    /// </summary>
    public event EventHandler<SessionProgressSummary>? SessionProgressUpdated;

    /// <summary>
    /// Get current progress for an item
    /// </summary>
    public ProgressUpdate? GetCurrentProgress(string sessionId, string itemId)
    {
        return _progressTracker.GetCurrentProgress(sessionId, itemId);
    }

    /// <summary>
    /// Get session summary
    /// </summary>
    public SessionProgressSummary GetSessionSummary(string sessionId)
    {
        return _progressTracker.GetSessionSummary(sessionId);
    }

    /// <summary>
    /// Subscribe to updates for a specific session
    /// </summary>
    public IObservable<ProgressUpdate> GetSessionUpdates(string sessionId)
    {
        return _progressTracker.GetSessionProgressStream(sessionId)
            .Select(update => 
            {
                // Ensure updates are marshalled to UI thread in subscribers
                return update;
            });
    }

    /// <summary>
    /// Clear completed progress data
    /// </summary>
    public Task ClearCompletedProgressAsync()
    {
        return _progressTracker.ClearCompletedProgressAsync();
    }

    private void OnProgressUpdate(ProgressUpdate update)
    {
        try
        {
            // Ensure we're on the UI thread
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnProgressUpdate(update));
                return;
            }

            // Fire progress event
            ProgressUpdated?.Invoke(this, update);

            // Calculate and fire session summary update if this is a significant change
            if (IsSignificantUpdate(update))
            {
                var sessionSummary = _progressTracker.GetSessionSummary(update.SessionId);
                SessionProgressUpdated?.Invoke(this, sessionSummary);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing progress update for {SessionId}_{ItemId}", 
                update.SessionId, update.ItemId);
        }
    }

    private void OnProgressError(Exception error)
    {
        _logger.LogError(error, "Error in progress stream");
    }

    private static bool IsSignificantUpdate(ProgressUpdate update)
    {
        // Consider status changes and more frequent progress milestones as significant
        return update.Status switch
        {
            DownloadStatus.Started => true,
            DownloadStatus.Completed => true,
            DownloadStatus.Failed => true,
            DownloadStatus.Cancelled => true,
            DownloadStatus.Paused => true,
            DownloadStatus.Resumed => true,
            _ => update.Progress % 2 < 1 // Every ~2% progress for faster updates
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _progressSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}