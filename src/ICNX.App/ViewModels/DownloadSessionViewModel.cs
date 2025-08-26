using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICNX.Core.Models;
using ICNX.Core.Services;

namespace ICNX.App.ViewModels;

/// <summary>
/// ViewModel for a download session
/// </summary>
public partial class DownloadSessionViewModel : ViewModelBase
{
    private readonly DownloadSession _session;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private DownloadStatus _status;

    [ObservableProperty]
    private DateTime _createdAt;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private int _cancelledCount;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _progressText = "0%";

    [ObservableProperty]
    private string _statusText = "Idle";

    [ObservableProperty]
    private bool _canPause = false;

    [ObservableProperty]
    private bool _canResume = false;

    [ObservableProperty]
    private bool _canCancel = false;

    public string SessionId { get; }

    // Expose the underlying model for consumers that need full access to the session data
    public DownloadSession SessionModel => _session;

    public ObservableCollection<DownloadItemViewModel> Items { get; } = new();

    public DownloadSessionViewModel(DownloadSession session)
    {
        _session = session;
        SessionId = session.Id;
        UpdateFromSession(session);
    }

    public void UpdateFromSession(DownloadSession session)
    {
        Title = session.Title;
        Status = session.Status;
        CreatedAt = session.CreatedAt;
        TotalCount = session.TotalCount;
        CompletedCount = session.CompletedCount;
        FailedCount = session.FailedCount;
        CancelledCount = session.CancelledCount;

        // Calculate progress
        if (TotalCount > 0)
        {
            OverallProgress = (double)(CompletedCount + FailedCount + CancelledCount) / TotalCount * 100;
            ProgressText = $"{OverallProgress:F1}%";
        }
        else
        {
            OverallProgress = 0;
            ProgressText = "0%";
        }

        // Update status text
        StatusText = Status switch
        {
            DownloadStatus.Queued => "Queued",
            DownloadStatus.Started => "Starting...",
            DownloadStatus.Downloading => $"Downloading ({CompletedCount}/{TotalCount})",
            DownloadStatus.Paused => "Paused",
            DownloadStatus.Completed => "Completed",
            DownloadStatus.Failed => "Failed",
            DownloadStatus.Cancelled => "Cancelled",
            _ => "Unknown"
        };

        // Update command states
        CanPause = Status is DownloadStatus.Downloading or DownloadStatus.Started;
        CanResume = Status == DownloadStatus.Paused;
        CanCancel = Status is DownloadStatus.Queued or DownloadStatus.Started or DownloadStatus.Downloading or DownloadStatus.Paused;
    }

    public void UpdateProgress(SessionProgressSummary summary)
    {
        OverallProgress = summary.OverallProgress;
        ProgressText = $"{summary.OverallProgress:F1}%";

        // Update counts from summary
        CompletedCount = summary.CompletedItems;
        FailedCount = summary.FailedItems;
        CancelledCount = summary.CancelledItems;

        // Update status text with more detail
        if (summary.ActiveItems > 0)
        {
            var speed = FormatSpeed(summary.AverageSpeed);
            StatusText = $"Downloading - {speed} ({summary.CompletedItems}/{summary.TotalItems})";
        }
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
        if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
        if (bytesPerSecond < 1024 * 1024 * 1024) return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        return $"{bytesPerSecond / (1024 * 1024 * 1024):F1} GB/s";
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync()
    {
        // Command will be handled by the parent ViewModel
        await Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task ResumeAsync()
    {
        // Command will be handled by the parent ViewModel
        await Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelAsync()
    {
        // Command will be handled by the parent ViewModel
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        // Command will be handled by the parent ViewModel
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        // Command will be handled by the parent ViewModel
        await Task.CompletedTask;
    }
}