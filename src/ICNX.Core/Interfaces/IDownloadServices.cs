using ICNX.Core.Models;

namespace ICNX.Core.Interfaces;

/// <summary>
/// Main download session management service
/// </summary>
public interface IDownloadSessionService
{
    /// <summary>
    /// Start a new download session with multiple items
    /// </summary>
    Task<string> StartAsync(IEnumerable<DownloadRequest> items, string destination, int? concurrency = null);
    
    /// <summary>
    /// Pause an active download session
    /// </summary>
    Task PauseAsync(string sessionId);
    
    /// <summary>
    /// Resume a paused download session
    /// </summary>
    Task ResumeAsync(string sessionId);
    
    /// <summary>
    /// Cancel a download session
    /// </summary>
    Task CancelAsync(string sessionId, bool force = false);
    
    /// <summary>
    /// Get real-time progress updates for a session
    /// </summary>
    IAsyncEnumerable<ProgressUpdate> StreamSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get session information
    /// </summary>
    Task<DownloadSession?> GetSessionAsync(string sessionId);
    
    /// <summary>
    /// Get recent download sessions
    /// </summary>
    Task<IEnumerable<DownloadSession>> GetRecentSessionsAsync(int limit = 50);
    
    /// <summary>
    /// Get all items in a session
    /// </summary>
    Task<IEnumerable<DownloadItem>> GetSessionItemsAsync(string sessionId);
    
    /// <summary>
    /// Remove a completed or failed session
    /// </summary>
    Task DeleteSessionAsync(string sessionId);
    
    /// <summary>
    /// Get active sessions
    /// </summary>
    Task<IEnumerable<DownloadSession>> GetActiveSessionsAsync();
}

/// <summary>
/// Progress tracking and event aggregation
/// </summary>
public interface IProgressTracker
{
    /// <summary>
    /// Report progress update
    /// </summary>
    void ReportProgress(ProgressUpdate update);
    
    /// <summary>
    /// Get current progress for an item
    /// </summary>
    ProgressUpdate? GetCurrentProgress(string sessionId, string itemId);
    
    /// <summary>
    /// Subscribe to progress updates
    /// </summary>
    IObservable<ProgressUpdate> GetProgressStream();
    
    /// <summary>
    /// Subscribe to progress updates for a specific session
    /// </summary>
    IObservable<ProgressUpdate> GetSessionProgressStream(string sessionId);
    
    /// <summary>
    /// Clear progress data for completed sessions
    /// </summary>
    Task ClearCompletedProgressAsync();
    
    /// <summary>
    /// Get summary of progress for a session
    /// </summary>
    SessionProgressSummary GetSessionSummary(string sessionId);
}

/// <summary>
/// Download engine abstraction
/// </summary>
public interface IDownloadEngine
{
    /// <summary>
    /// Start downloading an item
    /// </summary>
    Task<bool> DownloadAsync(DownloadItem item, string destinationPath, 
        IProgress<ProgressUpdate> progress, CancellationToken cancellationToken);
    
    /// <summary>
    /// Check if an item can be paused/resumed
    /// </summary>
    bool SupportsResume(DownloadItem item);
    
    /// <summary>
    /// Get optimal chunk count for a URL
    /// </summary>
    Task<int> GetOptimalChunkCountAsync(string url);
}