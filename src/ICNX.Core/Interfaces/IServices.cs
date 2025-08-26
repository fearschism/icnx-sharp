using ICNX.Core.Models;

namespace ICNX.Core.Interfaces;

/// <summary>
/// Repository interface for settings
/// </summary>
public interface ISettingsRepository : IRepository<Settings>
{
    Task<Settings?> GetSettingsAsync();
    Task SaveSettingsAsync(Settings settings);
    Task<T?> GetSettingAsync<T>(string key);
    Task SetSettingAsync<T>(string key, T value);
}

/// <summary>
/// Script detection and execution service
/// </summary>
public interface IScriptService
{
    /// <summary>
    /// Detect applicable scripts for a URL
    /// </summary>
    Task<IEnumerable<ScriptDetectionResult>> DetectScriptsAsync(string url);

    /// <summary>
    /// Run a script against a URL
    /// </summary>
    Task<ScrapeResult> RunScriptAsync(string scriptName, string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available scripts
    /// </summary>
    Task<IEnumerable<ScriptInfo>> GetAvailableScriptsAsync();

    /// <summary>
    /// Load scripts from the scripts directory
    /// </summary>
    Task RefreshScriptsAsync();

    /// <summary>
    /// Install a new script
    /// </summary>
    Task<bool> InstallScriptAsync(string scriptPath);

    /// <summary>
    /// Enable or disable a script
    /// </summary>
    Task SetScriptEnabledAsync(string scriptName, bool enabled);
}

/// <summary>
/// Settings persistence and management
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Get current settings
    /// </summary>
    Task<Settings> GetSettingsAsync();

    /// <summary>
    /// Save settings
    /// </summary>
    Task SaveSettingsAsync(Settings settings);

    /// <summary>
    /// Reset settings to defaults
    /// </summary>
    Task ResetToDefaultsAsync();

    /// <summary>
    /// Subscribe to settings changes (observable stream)
    /// </summary>
    IObservable<Settings> SettingsChangedObservable { get; }

    /// <summary>
    /// Event emitted when settings change (legacy/event-based API expected by tests)
    /// </summary>
    event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    /// <summary>
    /// Update concurrency setting with clamping to valid range
    /// </summary>
    Task UpdateConcurrencyAsync(int concurrency);

    /// <summary>
    /// Export settings to a JSON file path, returns the path on success
    /// </summary>
    Task<string?> ExportSettingsAsync(string filePath);

    /// <summary>
    /// Import settings from a JSON file path, returns true on success
    /// </summary>
    Task<bool> ImportSettingsAsync(string filePath);

    /// <summary>
    /// Get a specific setting value
    /// </summary>
    Task<T?> GetSettingAsync<T>(string key);

    /// <summary>
    /// Set a specific setting value
    /// </summary>
    Task SetSettingAsync<T>(string key, T value);
}

/// <summary>
/// Data persistence abstraction
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Get item by ID
    /// </summary>
    Task<T?> GetByIdAsync(string id);

    /// <summary>
    /// Get all items
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Add new item
    /// </summary>
    Task<string> AddAsync(T item);

    /// <summary>
    /// Update existing item
    /// </summary>
    Task<bool> UpdateAsync(T item);

    /// <summary>
    /// Delete item by ID
    /// </summary>
    Task<bool> DeleteAsync(string id);

    /// <summary>
    /// Delete multiple items
    /// </summary>
    Task<int> DeleteManyAsync(IEnumerable<string> ids);
}

/// <summary>
/// Event aggregation service
/// </summary>
public interface IEventAggregator
{
    /// <summary>
    /// Publish an event
    /// </summary>
    void Publish<T>(T eventData) where T : class;

    /// <summary>
    /// Publish an event asynchronously
    /// </summary>
    Task PublishAsync<T>(T eventData) where T : class;

    /// <summary>
    /// Subscribe to events of type T
    /// </summary>
    IObservable<T> Subscribe<T>() where T : class;

    /// <summary>
    /// Subscribe to events with a specific filter
    /// </summary>
    IObservable<T> Subscribe<T>(Func<T, bool> filter) where T : class;
}

/// <summary>
/// Platform-specific operations
/// </summary>
public interface IPlatformService
{
    /// <summary>
    /// Open file in default application
    /// </summary>
    Task OpenFileAsync(string filePath);

    /// <summary>
    /// Open folder in file explorer
    /// </summary>
    Task OpenFolderAsync(string folderPath);

    /// <summary>
    /// Show system notification
    /// </summary>
    Task ShowNotificationAsync(string title, string message);

    /// <summary>
    /// Get system theme preference
    /// </summary>
    ThemeMode GetSystemTheme();

    /// <summary>
    /// Check if running on mobile platform
    /// </summary>
    bool IsMobile { get; }

    /// <summary>
    /// Get app data directory
    /// </summary>
    string GetAppDataDirectory();
}