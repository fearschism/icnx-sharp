using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICNX.Core.Models;

namespace ICNX.App.Services;

public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}

public partial class ToastNotification : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;
    
    [ObservableProperty]
    private string _message = string.Empty;
    
    [ObservableProperty]
    private ToastType _type = ToastType.Info;
    
    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;
    
    [ObservableProperty]
    private bool _isVisible = true;
    
    public string TypeIcon => Type switch
    {
        ToastType.Success => "✅",
        ToastType.Error => "❌", 
        ToastType.Warning => "⚠️",
        ToastType.Info => "ℹ️",
        _ => "ℹ️"
    };
    
    public string TypeColor => Type switch
    {
        ToastType.Success => "#10B981",
        ToastType.Error => "#EF4444",
        ToastType.Warning => "#F59E0B", 
        ToastType.Info => "#3B82F6",
        _ => "#3B82F6"
    };
}

public partial class ToastNotificationService : ObservableObject
{
    public ObservableCollection<ToastNotification> Notifications { get; } = new();
    
    public void ShowSuccess(string title, string message)
    {
        ShowNotification(title, message, ToastType.Success);
    }
    
    public void ShowError(string title, string message)
    {
        ShowNotification(title, message, ToastType.Error);
    }
    
    public void ShowWarning(string title, string message)
    {
        ShowNotification(title, message, ToastType.Warning);
    }
    
    public void ShowInfo(string title, string message)
    {
        ShowNotification(title, message, ToastType.Info);
    }
    
    public void ShowDownloadCompleted(DownloadSession session)
    {
        var completedCount = session.CompletedCount;
        var totalCount = session.TotalCount;
        var hasErrors = session.FailedCount > 0;
        
        if (hasErrors)
        {
            ShowError(
                "Download Completed with Errors",
                $"Session completed: {completedCount}/{totalCount} files downloaded successfully"
            );
        }
        else
        {
            ShowSuccess(
                "Download Completed",
                $"Successfully downloaded {completedCount} file(s)"
            );
        }
    }
    
    public void ShowDownloadFailed(DownloadSession session, string error)
    {
        ShowError(
            "Download Failed",
            $"Session failed: {error}"
        );
    }
    
    private void ShowNotification(string title, string message, ToastType type)
    {
        var notification = new ToastNotification
        {
            Title = title,
            Message = message,
            Type = type,
            Timestamp = DateTime.Now
        };
        
        // Add to beginning of collection for newest first
        Notifications.Insert(0, notification);
        
        // Auto-remove after 5 seconds
        _ = Task.Delay(5000).ContinueWith(_ =>
        {
            notification.IsVisible = false;
            // Remove after fade animation
            _ = Task.Delay(500).ContinueWith(__ =>
            {
                Notifications.Remove(notification);
            });
        });
        
        // Keep only the latest 10 notifications
        while (Notifications.Count > 10)
        {
            Notifications.RemoveAt(Notifications.Count - 1);
        }
    }
    
    public void ClearAll()
    {
        Notifications.Clear();
    }
    
    [RelayCommand]
    public void RemoveNotification(ToastNotification notification)
    {
        Notifications.Remove(notification);
    }
}