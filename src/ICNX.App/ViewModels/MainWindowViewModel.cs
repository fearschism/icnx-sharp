﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICNX.App.Services;
using ICNX.Core.Interfaces;
using ICNX.Core.Models;
using Microsoft.Extensions.Logging;

namespace ICNX.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDownloadSessionService _downloadService;
    private readonly UIProgressService _progressService;
    private readonly IEventAggregator _eventAggregator;
    private readonly ToastNotificationService _toastService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly System.Timers.Timer _sessionRefreshTimer;

    public SettingsViewModel SettingsViewModel { get; }
    public ToastNotificationService ToastService { get; }

    [ObservableProperty]
    private string _greeting = "Welcome to ICNX!";

    [ObservableProperty]
    private string _quickDownloadUrl = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isDownloading = false;

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    // Visibility properties for sidebar views
    public bool IsQuickDownloadVisible => SelectedTabIndex == 0;
    public bool IsHistoryVisible => SelectedTabIndex == 1;
    public bool IsSettingsVisible => SelectedTabIndex == 2;

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsQuickDownloadVisible));
        OnPropertyChanged(nameof(IsHistoryVisible));
        OnPropertyChanged(nameof(IsSettingsVisible));
    }

    public ObservableCollection<DownloadSessionViewModel> DownloadSessions { get; } = new();
    public ObservableCollection<DownloadSessionViewModel> RecentSessions { get; } = new();

    public MainWindowViewModel(
        IDownloadSessionService downloadService,
        UIProgressService progressService,
        IEventAggregator eventAggregator,
        SettingsViewModel settingsViewModel,
        ToastNotificationService toastService,
        ILogger<MainWindowViewModel> logger)
    {
        _downloadService = downloadService;
        _progressService = progressService;
        _eventAggregator = eventAggregator;
        _toastService = toastService;
        _logger = logger;
        SettingsViewModel = settingsViewModel;
        ToastService = toastService;

        // Subscribe to progress updates
        _progressService.ProgressUpdated += OnProgressUpdate;
        _progressService.SessionProgressUpdated += OnSessionProgressUpdate;

        // Setup automatic session refresh timer (every 2 seconds to reduce DB polling)
        _sessionRefreshTimer = new System.Timers.Timer(2000);
        _sessionRefreshTimer.Elapsed += async (_, _) => await LoadRecentSessionsAsync();
        _sessionRefreshTimer.AutoReset = true;
        _sessionRefreshTimer.Start();

        // Load recent sessions initially
        _ = LoadRecentSessionsAsync();
    }

    [RelayCommand]
    private async Task StartQuickDownloadAsync()
    {
        if (string.IsNullOrWhiteSpace(QuickDownloadUrl))
        {
            StatusMessage = "Please enter a URL";
            _toastService.ShowError("Invalid Input", "Please enter a URL to download");
            return;
        }

        // Validate URL format
        if (!IsValidUrl(QuickDownloadUrl.Trim()))
        {
            StatusMessage = "Please enter a valid URL (e.g., https://example.com/file.zip)";
            _toastService.ShowError("Invalid URL", "Please enter a valid HTTP or HTTPS URL");
            return;
        }

        try
        {
            IsDownloading = true;
            StatusMessage = "Starting download...";

            var downloadRequests = new[]
            {
                new DownloadRequest
                {
                    Url = QuickDownloadUrl.Trim(),
                    Filename = null // Auto-detect filename
                }
            };

            // Use default download directory for now
            var defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "ICNX");
            Directory.CreateDirectory(defaultDir);

            var sessionId = await _downloadService.StartAsync(downloadRequests, defaultDir);

            StatusMessage = $"Download started (Session: {sessionId[..8]}...)";
            QuickDownloadUrl = string.Empty; // Clear the URL
            _toastService.ShowSuccess("Download Started", "Your download has been started successfully");

            // Refresh sessions
            await LoadRecentSessionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start quick download");
            StatusMessage = $"Error: {ex.Message}";
            _toastService.ShowError("Download Failed", $"Failed to start download: {ex.Message}");
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private async Task PauseSessionAsync(string sessionId)
    {
        try
        {
            await _downloadService.PauseAsync(sessionId);
            StatusMessage = "Session paused";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause session {SessionId}", sessionId);
            StatusMessage = $"Error pausing session: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResumeSessionAsync(string sessionId)
    {
        try
        {
            await _downloadService.ResumeAsync(sessionId);
            StatusMessage = "Session resumed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume session {SessionId}", sessionId);
            StatusMessage = $"Error resuming session: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CancelSessionAsync(string sessionId)
    {
        try
        {
            await _downloadService.CancelAsync(sessionId);
            StatusMessage = "Session cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel session {SessionId}", sessionId);
            StatusMessage = $"Error cancelling session: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteSessionAsync(string sessionId)
    {
        try
        {
            await _downloadService.DeleteSessionAsync(sessionId);
            StatusMessage = "Session deleted";

            // Remove from collections
            var sessionToRemove = DownloadSessions.FirstOrDefault(s => s.SessionId == sessionId) ??
                                  RecentSessions.FirstOrDefault(s => s.SessionId == sessionId);

            if (sessionToRemove != null)
            {
                DownloadSessions.Remove(sessionToRemove);
                RecentSessions.Remove(sessionToRemove);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete session {SessionId}", sessionId);
            StatusMessage = $"Error deleting session: {ex.Message}";
        }
    }

    private async Task LoadRecentSessionsAsync()
    {
        try
        {
            var sessions = await _downloadService.GetRecentSessionsAsync(20);

            // Update collections
            RecentSessions.Clear();
            DownloadSessions.Clear();

            foreach (var session in sessions.OrderByDescending(s => s.CreatedAt))
            {
                var viewModel = new DownloadSessionViewModel(session);

                RecentSessions.Add(viewModel);

                // Add active sessions to the main collection
                if (session.Status is DownloadStatus.Queued or DownloadStatus.Started or
                    DownloadStatus.Downloading or DownloadStatus.Paused)
                {
                    DownloadSessions.Add(viewModel);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recent sessions");
            StatusMessage = "Error loading sessions";
        }
    }

    private void OnProgressUpdate(object? sender, ProgressUpdate update)
    {
        try
        {
            // Find the session and update individual item progress
            var session = DownloadSessions.FirstOrDefault(s => s.SessionId == update.SessionId) ??
                         RecentSessions.FirstOrDefault(s => s.SessionId == update.SessionId);

            if (session != null)
            {
                // Update individual item if exists
                var item = session.Items.FirstOrDefault(i => i.ItemId == update.ItemId);
                item?.UpdateFromProgress(update);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing progress update");
        }
    }

    private void OnSessionProgressUpdate(object? sender, SessionProgressSummary summary)
    {
        try
        {
            // Find the session and update its overall progress
            var session = DownloadSessions.FirstOrDefault(s => s.SessionId == summary.SessionId) ??
                         RecentSessions.FirstOrDefault(s => s.SessionId == summary.SessionId);

            if (session != null)
            {
                var previousStatus = session.Status;
                session.UpdateProgress(summary);

                // Check for session completion and show toast
                if (previousStatus != DownloadStatus.Completed && session.Status == DownloadStatus.Completed)
                {
                    _toastService.ShowDownloadCompleted(session.SessionModel);
                }
                else if (previousStatus != DownloadStatus.Failed && session.Status == DownloadStatus.Failed)
                {
                    _toastService.ShowDownloadFailed(session.SessionModel, "One or more downloads failed");
                }
                else if (previousStatus != DownloadStatus.Cancelled && session.Status == DownloadStatus.Cancelled)
                {
                    _toastService.ShowWarning("Download Cancelled", $"Session was cancelled");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing session progress update");
        }
    }

    private bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        try
        {
            var uri = new Uri(url);
            // Check if it's HTTP or HTTPS
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _sessionRefreshTimer?.Stop();
        _sessionRefreshTimer?.Dispose();
    }
}
