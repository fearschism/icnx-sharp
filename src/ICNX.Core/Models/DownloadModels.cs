using System.ComponentModel;

namespace ICNX.Core.Models;

/// <summary>
/// Status enum for download items and sessions
/// </summary>
public enum DownloadStatus
{
    Queued,
    Started, 
    Downloading,
    Paused,
    Resumed,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Represents a download session containing multiple items
/// </summary>
public class DownloadSession
{
    public string Id { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Title { get; set; } = string.Empty;
    public DownloadStatus Status { get; set; }
    public long? TotalBytes { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int CancelledCount { get; set; }
    public int TotalCount { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Represents an individual download item within a session
/// </summary>
public class DownloadItem
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public DownloadStatus Status { get; set; }
    public string? Mime { get; set; }
    public long? TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public string? Error { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryAttempt { get; set; } = 0;
}

/// <summary>
/// Progress update event data
/// </summary>
public class ProgressUpdate
{
    public string SessionId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public DownloadStatus Status { get; set; }
    public long DownloadedBytes { get; set; }
    public long? TotalBytes { get; set; }
    public double? SpeedBytesPerSec { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double Progress => TotalBytes.HasValue && TotalBytes.Value > 0 
        ? Math.Min(100.0, (double)DownloadedBytes / TotalBytes.Value * 100.0)
        : 0.0;
}

/// <summary>
/// Download request for starting new downloads
/// </summary>
public class DownloadRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Url { get; set; } = string.Empty;
    public string? Filename { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
}

/// <summary>
/// Progress summary for a download session
/// </summary>
public class SessionProgressSummary
{
    public string SessionId { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int FailedItems { get; set; }
    public int CancelledItems { get; set; }
    public int ActiveItems { get; set; }
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public double AverageSpeed { get; set; }
    public double OverallProgress { get; set; } // 0-100
}