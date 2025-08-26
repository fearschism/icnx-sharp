using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICNX.App.Services;
using ICNX.Core.Interfaces;
using ICNX.Core.Models;
using Microsoft.Extensions.Logging;

namespace ICNX.App.ViewModels;

/// <summary>
/// ViewModel for the settings view
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ToastNotificationService _toastService;
    private readonly ThemeService _themeService;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _isInitializing = true;

    [ObservableProperty]
    private string _downloadDirectory = string.Empty;

    [ObservableProperty]
    private int _concurrency = 4;

    [ObservableProperty]
    private bool _autoResumeOnLaunch = true;

    [ObservableProperty]
    private bool _enableScriptAutoDetection = true;

    [ObservableProperty]
    private string? _speedLimitText;

    [ObservableProperty]
    private bool _speedLimitEnabled = false;

    [ObservableProperty]
    private bool _retryEnabled = true;

    [ObservableProperty]
    private int _maxRetryAttempts = 5;

    [ObservableProperty]
    private int _baseRetryDelayMs = 1000;

    [ObservableProperty]
    private double _backoffFactor = 2.0;

    [ObservableProperty]
    private ThemeMode _selectedTheme = ThemeMode.Dark;

    [ObservableProperty]
    private string _accentColor = "#3b82f6";

    [ObservableProperty]
    private bool _enableAnimations = true;

    [ObservableProperty]
    private bool _compactMode = false;

    [ObservableProperty]
    private bool _enableGlass = true;

    [ObservableProperty]
    private bool _enableProgressEffects = true;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _hasUnsavedChanges = false;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private int _selectedSettingsIndex = 0;

    // Visibility properties for settings sections
    public bool IsGeneralVisible => SelectedSettingsIndex == 0;
    public bool IsRetryVisible => SelectedSettingsIndex == 1;
    public bool IsAppearanceVisible => SelectedSettingsIndex == 2;

    partial void OnSelectedSettingsIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsGeneralVisible));
        OnPropertyChanged(nameof(IsRetryVisible));
        OnPropertyChanged(nameof(IsAppearanceVisible));
    }

    partial void OnSelectedThemeChanged(ThemeMode value)
    {
        if (!_isInitializing)
        {
            _themeService.ApplyTheme(value);
            SaveSettingsIfChanged();
        }
    }

    partial void OnAccentColorChanged(string value)
    {
        if (!_isInitializing)
        {
            _themeService.ApplyAccentColor(value);
            SaveSettingsIfChanged();
        }
    }

    public ObservableCollection<ThemeMode> AvailableThemes { get; } = new()
    {
        ThemeMode.Light,
        ThemeMode.Dark,
        ThemeMode.HighContrast,
        ThemeMode.System
    };

    public ObservableCollection<string> PredefinedColors { get; } = new()
    {
        // Blue variants
        "#3b82f6", "#1e40af", "#0ea5e9", "#0284c7",
        // Purple variants  
        "#6366f1", "#8b5cf6", "#a855f7", "#7c3aed",
        // Pink/Red variants
        "#ec4899", "#ef4444", "#f43f5e", "#e11d48", 
        // Green variants
        "#10b981", "#059669", "#16a34a", "#15803d",
        // Orange/Yellow variants
        "#f59e0b", "#d97706", "#ea580c", "#dc2626",
        // Neutral variants
        "#6b7280", "#374151", "#1f2937", "#111827"
    };

    public SettingsViewModel(ISettingsService settingsService, ToastNotificationService toastService, ThemeService themeService, ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _toastService = toastService;
        _themeService = themeService;
        _logger = logger;

        // Subscribe to settings changes
        _settingsService.SettingsChangedObservable.Subscribe(settings => OnSettingsChanged(settings));

        // Load settings
        _ = LoadSettingsAsync();
    }

    /// <summary>
    /// Automatically save settings when they change (with debouncing)
    /// </summary>
    private async void SaveSettingsIfChanged()
    {
        if (_isInitializing) return;

        try
        {
            var settings = CreateSettingsFromProperties();
            await _settingsService.SaveSettingsAsync(settings);
            _toastService.ShowSuccess("Settings Saved", "Your settings have been saved automatically");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-save settings");
            _toastService.ShowError("Save Failed", "Failed to save settings automatically");
        }
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading settings...";

            var settings = await _settingsService.GetSettingsAsync();

            // Update properties without triggering change detection
            SetPropertiesFromSettings(settings, trackChanges: false);

            HasUnsavedChanges = false;
            StatusMessage = "Settings loaded";
            _isInitializing = false; // Enable auto-saving
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings");
            StatusMessage = "Failed to load settings";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Saving settings...";

            var settings = CreateSettingsFromProperties();
            await _settingsService.SaveSettingsAsync(settings);

            HasUnsavedChanges = false;
            StatusMessage = "Settings saved successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            StatusMessage = $"Failed to save settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Resetting to defaults...";

            await _settingsService.ResetToDefaultsAsync();
            await LoadSettingsAsync();

            StatusMessage = "Settings reset to defaults";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset settings");
            StatusMessage = "Failed to reset settings";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task BrowseDownloadDirectoryAsync()
    {
        try
        {
            StatusMessage = "Opening folder selection dialog...";

            var selectedFolder = await FolderPickerService.SelectDownloadFolderAsync(DownloadDirectory);

            if (!string.IsNullOrEmpty(selectedFolder))
            {
                DownloadDirectory = selectedFolder;
                StatusMessage = "Download directory updated";
                _toastService.ShowSuccess("Directory Selected", $"Download directory set to: {DownloadDirectory}");
            }
            else
            {
                StatusMessage = "No directory selected";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to browse for directory");
            StatusMessage = "Failed to open folder browser";
            _toastService.ShowError("Error", "Failed to open folder browser");
        }
    }
    [RelayCommand]
    private async Task TestDownloadDirectoryAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(DownloadDirectory))
            {
                StatusMessage = "Please enter a download directory";
                return;
            }

            // Try to create the directory and write a test file
            Directory.CreateDirectory(DownloadDirectory);
            var testFile = Path.Combine(DownloadDirectory, "icnx_test.tmp");

            await File.WriteAllTextAsync(testFile, "test");
            File.Delete(testFile);

            StatusMessage = "Download directory is valid and writable";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Directory test failed for {Directory}", DownloadDirectory);
            StatusMessage = $"Directory test failed: {ex.Message}";
        }
    }



    [RelayCommand]
    private void SelectColor(string color)
    {
        AccentColor = color;
    }

    [RelayCommand]
    private void SelectTheme(string themeName)
    {
        if (Enum.TryParse<ThemeMode>(themeName, true, out var theme))
        {
            SelectedTheme = theme;
        }
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Skip change tracking for certain properties
        if (e.PropertyName is nameof(StatusMessage) or nameof(HasUnsavedChanges) or nameof(IsLoading))
            return;

        HasUnsavedChanges = true;
    }

    private void OnSettingsChanged(Settings newSettings)
    {
        // Update UI when settings change externally
        SetPropertiesFromSettings(newSettings, trackChanges: false);
        HasUnsavedChanges = false;
    }

    private void SetPropertiesFromSettings(Settings settings, bool trackChanges = true)
    {
        // var wasTracking = PropertyChanged != null;

        // if (!trackChanges && wasTracking)
        // {
        //     PropertyChanged -= OnPropertyChanged;
        // }

        try
        {
            DownloadDirectory = settings.DefaultDownloadDir;
            Concurrency = settings.Concurrency;
            AutoResumeOnLaunch = settings.AutoResumeOnLaunch;
            EnableScriptAutoDetection = settings.EnableScriptAutoDetection;

            if (settings.SpeedLimitBytesPerSec.HasValue)
            {
                SpeedLimitEnabled = true;
                SpeedLimitText = (settings.SpeedLimitBytesPerSec.Value / 1024 / 1024).ToString(); // MB/s
            }
            else
            {
                SpeedLimitEnabled = false;
                SpeedLimitText = null;
            }

            RetryEnabled = settings.RetryPolicy.Enabled;
            MaxRetryAttempts = settings.RetryPolicy.MaxAttempts;
            BaseRetryDelayMs = settings.RetryPolicy.BaseDelayMs;
            BackoffFactor = settings.RetryPolicy.BackoffFactor;

            SelectedTheme = settings.Appearance.Theme;
            AccentColor = settings.Appearance.AccentColor;
            EnableAnimations = settings.Appearance.EnableAnimations;
            CompactMode = settings.Appearance.CompactMode;
            EnableGlass = settings.Appearance.EnableGlass;
            EnableProgressEffects = settings.Appearance.EnableProgressSheen;

            // Apply theme when loading settings (but only if not initializing to avoid triggering change events)
            if (!_isInitializing)
            {
                _themeService.ApplyTheme(settings.Appearance.Theme);
                _themeService.ApplyAccentColor(settings.Appearance.AccentColor);
            }
        }
        finally
        {
            // if (!trackChanges && wasTracking)
            // {
            //     PropertyChanged += OnPropertyChanged;
            // }
        }
    }

    private Settings CreateSettingsFromProperties()
    {
        long? speedLimit = null;
        if (SpeedLimitEnabled && !string.IsNullOrWhiteSpace(SpeedLimitText))
        {
            if (double.TryParse(SpeedLimitText, out var mbps))
            {
                speedLimit = (long)(mbps * 1024 * 1024); // Convert MB/s to bytes/s
            }
        }

        return new Settings
        {
            DefaultDownloadDir = DownloadDirectory,
            Concurrency = Concurrency,
            AutoResumeOnLaunch = AutoResumeOnLaunch,
            EnableScriptAutoDetection = EnableScriptAutoDetection,
            SpeedLimitBytesPerSec = speedLimit,
            RetryPolicy = new RetryPolicy
            {
                Enabled = RetryEnabled,
                MaxAttempts = MaxRetryAttempts,
                BaseDelayMs = BaseRetryDelayMs,
                BackoffFactor = BackoffFactor,
                JitterPercent = 0.2,
                PerItemResetOnProgress = true,
                RetryStatusCodes = new List<int> { 408, 429, 500, 502, 503, 504 }
            },
            Appearance = new Appearance
            {
                Theme = SelectedTheme,
                AccentColor = AccentColor,
                EnableAnimations = EnableAnimations,
                CompactMode = CompactMode,
                EnableGlass = EnableGlass,
                EnableProgressSheen = EnableProgressEffects,
                ReducedEffects = false,
                EnableBreathing = true,
                EnableProgressHalo = false
            },
            CustomSettings = new Dictionary<string, object>()
        };
    }
}