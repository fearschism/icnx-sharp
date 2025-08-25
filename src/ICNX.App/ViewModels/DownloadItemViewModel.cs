using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICNX.Core.Models;

namespace ICNX.App.ViewModels;

/// <summary>
/// ViewModel for an individual download item
/// </summary>
public partial class DownloadItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _filename = string.Empty;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private DownloadStatus _status;

    [ObservableProperty]
    private long _totalBytes;

    [ObservableProperty]
    private long _downloadedBytes;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = "0%";

    [ObservableProperty]
    private string _speedText = "";

    [ObservableProperty]
    private string _etaText = "";

    [ObservableProperty]
    private string _sizeText = "";

    [ObservableProperty]
    private string _statusText = "Queued";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    public string ItemId { get; }
    public string SessionId { get; }

    public DownloadItemViewModel(DownloadItem item)
    {
        ItemId = item.Id;
        SessionId = item.SessionId;
        UpdateFromItem(item);
    }

    public void UpdateFromItem(DownloadItem item)
    {
        Filename = item.Filename;
        Url = item.Url;
        Status = item.Status;
        TotalBytes = item.TotalBytes ?? 0;
        DownloadedBytes = item.DownloadedBytes;
        ErrorMessage = item.Error;
        HasError = !string.IsNullOrEmpty(item.Error);

        UpdateProgress();
        UpdateStatusText();
        UpdateSizeText();
    }

    public void UpdateFromProgress(ProgressUpdate progress)
    {
        Status = progress.Status;
        DownloadedBytes = progress.DownloadedBytes;
        TotalBytes = progress.TotalBytes ?? TotalBytes;
        ErrorMessage = progress.Error;
        HasError = !string.IsNullOrEmpty(progress.Error);

        // Update speed and ETA
        if (progress.SpeedBytesPerSec.HasValue)
        {
            SpeedText = FormatSpeed(progress.SpeedBytesPerSec.Value);
        }

        if (progress.EstimatedTimeRemaining.HasValue)
        {
            EtaText = FormatTimeSpan(progress.EstimatedTimeRemaining.Value);
        }

        UpdateProgress();
        UpdateStatusText();
        UpdateSizeText();
    }

    private void UpdateProgress()
    {
        if (TotalBytes > 0)
        {
            Progress = (double)DownloadedBytes / TotalBytes * 100;
            ProgressText = $"{Progress:F1}%";
        }
        else
        {
            Progress = Status == DownloadStatus.Completed ? 100 : 0;
            ProgressText = Status == DownloadStatus.Completed ? "100%" : "0%";
        }
    }

    private void UpdateStatusText()
    {
        StatusText = Status switch
        {
            DownloadStatus.Queued => "Waiting...",
            DownloadStatus.Started => "Starting...",
            DownloadStatus.Downloading => "Downloading",
            DownloadStatus.Paused => "Paused",
            DownloadStatus.Resumed => "Resuming...",
            DownloadStatus.Completed => "Completed",
            DownloadStatus.Failed => HasError ? $"Failed: {ErrorMessage}" : "Failed",
            DownloadStatus.Cancelled => "Cancelled",
            _ => "Unknown"
        };
    }

    private void UpdateSizeText()
    {
        if (TotalBytes > 0)
        {
            SizeText = $"{FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes)}";
        }
        else if (DownloadedBytes > 0)
        {
            SizeText = FormatBytes(DownloadedBytes);
        }
        else
        {
            SizeText = "Unknown size";
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        double number = bytes;

        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }

        return $"{number:n1} {suffixes[counter]}";
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
        if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
        if (bytesPerSecond < 1024 * 1024 * 1024) return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        return $"{bytesPerSecond / (1024 * 1024 * 1024):F1} GB/s";
    }

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1) return $"{timeSpan.Days}d {timeSpan.Hours}h {timeSpan.Minutes}m";
        if (timeSpan.TotalHours >= 1) return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        if (timeSpan.TotalMinutes >= 1) return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        return $"{timeSpan.Seconds}s";
    }

    [RelayCommand]
    private async Task OpenFileAsync()
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
    private async Task RetryAsync()
    {
        // Command will be handled by the parent ViewModel
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        // Command will be handled by the parent ViewModel
        await Task.CompletedTask;
    }
}