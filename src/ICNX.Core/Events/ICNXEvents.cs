using ICNX.Core.Models;

namespace ICNX.Core.Events;

/// <summary>
/// Base event class for all ICNX events
/// </summary>
public abstract class ICNXEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string EventId { get; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Download session started event
/// </summary>
public class DownloadSessionStarted : ICNXEvent
{
    public string SessionId { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public string Destination { get; set; } = string.Empty;
}

/// <summary>
/// Download session completed event
/// </summary>
public class DownloadSessionCompleted : ICNXEvent
{
    public string SessionId { get; set; } = string.Empty;
    public DownloadStatus Status { get; set; }
    public TimeSpan Duration { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
}

/// <summary>
/// Download item updated event
/// </summary>
public class DownloadItemUpdated : ICNXEvent
{
    public string SessionId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public DownloadStatus PreviousStatus { get; set; }
    public DownloadStatus NewStatus { get; set; }
    public long DownloadedBytes { get; set; }
    public double? Speed { get; set; }
}

/// <summary>
/// Script detected event
/// </summary>
public class ScriptDetected : ICNXEvent
{
    public string Url { get; set; } = string.Empty;
    public List<ScriptDetectionResult> DetectedScripts { get; set; } = new();
}

/// <summary>
/// Script execution started event
/// </summary>
public class ScriptExecutionStarted : ICNXEvent
{
    public string ScriptName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Script execution completed event
/// </summary>
public class ScriptExecutionCompleted : ICNXEvent
{
    public string ScriptName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public ScrapeResult? Result { get; set; }
}

/// <summary>
/// Toast notification requested event
/// </summary>
public class ToastRequested : ICNXEvent
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ToastType Type { get; set; } = ToastType.Info;
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Settings changed event
/// </summary>
public class SettingsChanged : ICNXEvent
{
    public Settings NewSettings { get; set; } = new();
    public string? ChangedProperty { get; set; }
}

/// <summary>
/// Toast notification types
/// </summary>
public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}