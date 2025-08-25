using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using ICNX.Core.Interfaces;
using ICNX.Core.Models;
using Microsoft.Extensions.Logging;

namespace ICNX.App.Services;

/// <summary>
/// Notification service for progress-related alerts and status updates
/// </summary>
public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly ConcurrentQueue<NotificationItem> _notifications = new();

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
        Notifications = new ObservableCollection<NotificationItem>();
    }

    /// <summary>
    /// Collection of current notifications (UI-bound)
    /// </summary>
    public ObservableCollection<NotificationItem> Notifications { get; }

    /// <summary>
    /// Event fired when a new notification is added
    /// </summary>
    public event EventHandler<NotificationItem>? NotificationAdded;

    /// <summary>
    /// Show a success notification
    /// </summary>
    public void ShowSuccess(string title, string message, TimeSpan? autoHide = null)
    {
        var notification = new NotificationItem
        {
            Id = Guid.NewGuid().ToString(),
            Type = NotificationType.Success,
            Title = title,
            Message = message,
            Timestamp = DateTime.Now,
            AutoHideAfter = autoHide ?? TimeSpan.FromSeconds(5)
        };
        
        AddNotification(notification);
    }

    /// <summary>
    /// Show an info notification
    /// </summary>
    public void ShowInfo(string title, string message, TimeSpan? autoHide = null)
    {
        var notification = new NotificationItem
        {
            Id = Guid.NewGuid().ToString(),
            Type = NotificationType.Info,
            Title = title,
            Message = message,
            Timestamp = DateTime.Now,
            AutoHideAfter = autoHide ?? TimeSpan.FromSeconds(8)
        };
        
        AddNotification(notification);
    }

    /// <summary>
    /// Show a warning notification
    /// </summary>
    public void ShowWarning(string title, string message, TimeSpan? autoHide = null)
    {
        var notification = new NotificationItem
        {
            Id = Guid.NewGuid().ToString(),
            Type = NotificationType.Warning,
            Title = title,
            Message = message,
            Timestamp = DateTime.Now,
            AutoHideAfter = autoHide ?? TimeSpan.FromSeconds(10)
        };
        
        AddNotification(notification);
    }

    /// <summary>
    /// Show an error notification
    /// </summary>
    public void ShowError(string title, string message, TimeSpan? autoHide = null)
    {
        var notification = new NotificationItem
        {
            Id = Guid.NewGuid().ToString(),
            Type = NotificationType.Error,
            Title = title,
            Message = message,
            Timestamp = DateTime.Now,
            AutoHideAfter = autoHide // Errors don't auto-hide by default
        };
        
        AddNotification(notification);
    }

    /// <summary>
    /// Show a download completion notification
    /// </summary>
    public void ShowDownloadComplete(string sessionName, int itemCount, TimeSpan duration)
    {
        var message = itemCount == 1 
            ? $"Download completed in {FormatDuration(duration)}"
            : $"{itemCount} downloads completed in {FormatDuration(duration)}";

        ShowSuccess(sessionName, message, TimeSpan.FromSeconds(8));
    }

    /// <summary>
    /// Show a download failure notification
    /// </summary>
    public void ShowDownloadFailed(string sessionName, int failedCount, int totalCount, string? error = null)
    {
        var message = failedCount == totalCount
            ? $"All {totalCount} downloads failed"
            : $"{failedCount} of {totalCount} downloads failed";

        if (!string.IsNullOrEmpty(error))
        {
            message += $": {error}";
        }

        ShowError(sessionName, message);
    }

    /// <summary>
    /// Dismiss a notification
    /// </summary>
    public void DismissNotification(string notificationId)
    {
        var notification = Notifications.FirstOrDefault(n => n.Id == notificationId);
        if (notification != null)
        {
            Notifications.Remove(notification);
            _logger.LogDebug("Dismissed notification {Id}: {Title}", notificationId, notification.Title);
        }
    }

    /// <summary>
    /// Clear all notifications
    /// </summary>
    public void ClearAll()
    {
        var count = Notifications.Count;
        Notifications.Clear();
        _logger.LogDebug("Cleared {Count} notifications", count);
    }

    /// <summary>
    /// Handle progress updates for notification triggers
    /// </summary>
    public void HandleProgressUpdate(ProgressUpdate update)
    {
        try
        {
            switch (update.Status)
            {
                case DownloadStatus.Completed:
                    // Individual download completed - could aggregate these
                    break;
                    
                case DownloadStatus.Failed:
                    if (!string.IsNullOrEmpty(update.Error))
                    {
                        ShowWarning("Download Failed", 
                            $"Failed to download {GetFileNameFromUpdate(update)}: {update.Error}",
                            TimeSpan.FromSeconds(6));
                    }
                    break;
                    
                case DownloadStatus.Cancelled:
                    ShowInfo("Download Cancelled", 
                        $"Download of {GetFileNameFromUpdate(update)} was cancelled",
                        TimeSpan.FromSeconds(4));
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling progress update for notifications");
        }
    }

    private void AddNotification(NotificationItem notification)
    {
        try
        {
            // Add to collection (UI thread should be handled by caller)
            Notifications.Insert(0, notification); // Insert at top
            
            // Keep only last 20 notifications
            while (Notifications.Count > 20)
            {
                Notifications.RemoveAt(Notifications.Count - 1);
            }

            // Fire event
            NotificationAdded?.Invoke(this, notification);

            _logger.LogDebug("Added {Type} notification: {Title}", notification.Type, notification.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add notification");
        }
    }

    private static string GetFileNameFromUpdate(ProgressUpdate update)
    {
        // This would ideally come from the download item, but we can use a fallback
        return $"Item {update.ItemId.Substring(0, Math.Min(8, update.ItemId.Length))}...";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{duration.Days}d {duration.Hours}h {duration.Minutes}m";
        if (duration.TotalHours >= 1)
            return $"{duration.Hours}h {duration.Minutes}m {duration.Seconds}s";
        if (duration.TotalMinutes >= 1)
            return $"{duration.Minutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }
}

/// <summary>
/// Notification item for the UI
/// </summary>
public class NotificationItem
{
    public string Id { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public TimeSpan? AutoHideAfter { get; set; }
    public bool IsRead { get; set; }
}

/// <summary>
/// Notification types with different visual styles
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}