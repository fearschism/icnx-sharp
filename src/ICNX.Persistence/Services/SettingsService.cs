using System.Text.Json;
using ICNX.Core.Interfaces;
using ICNX.Core.Models;
using ICNX.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using System.Reactive.Subjects;
using System.Reactive.Linq;

namespace ICNX.Persistence.Services;

/// <summary>
/// Service for managing application settings with validation and events
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repository;
    private readonly ILogger<SettingsService> _logger;
    private Settings? _cachedSettings;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly BehaviorSubject<Settings> _settingsSubject;

    public IObservable<Settings> SettingsChangedObservable => _settingsSubject.AsObservable();

    // Legacy/event-based settings changed event expected by some tests
    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    public SettingsService(ISettingsRepository repository, ILogger<SettingsService> logger)
    {
        _repository = repository;
        _logger = logger;
        _settingsSubject = new BehaviorSubject<Settings>(CreateDefaultSettings());
    }

    public async Task<Settings> GetSettingsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_cachedSettings == null)
            {
                _cachedSettings = await _repository.GetSettingsAsync();
                if (_cachedSettings == null)
                {
                    _cachedSettings = CreateDefaultSettings();
                    await _repository.SaveSettingsAsync(_cachedSettings);
                }
            }

            return _cachedSettings;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        await _semaphore.WaitAsync();
        try
        {
            // Validate settings
            var validation = await ValidateSettingsAsync(settings);
            if (!validation.IsValid)
            {
                throw new ArgumentException($"Invalid settings: {string.Join(", ", validation.Errors)}");
            }

            // Get current settings for comparison (without semaphore to avoid deadlock)
            Settings? oldSettings = _cachedSettings;
            if (oldSettings == null)
            {
                oldSettings = await _repository.GetSettingsAsync();
            }

            await _repository.SaveSettingsAsync(settings);
            _cachedSettings = settings;

            // Publish to observable
            _settingsSubject.OnNext(settings);

            // Fire legacy event with changed properties (comparing old vs new)
            var changed = new SettingsChangedEventArgs();
            if (oldSettings != null)
            {
                if (oldSettings.Concurrency != settings.Concurrency)
                    changed.ChangedProperties.Add(nameof(Settings.Concurrency));
                if (oldSettings.DefaultDownloadDir != settings.DefaultDownloadDir)
                    changed.ChangedProperties.Add(nameof(Settings.DefaultDownloadDir));
                if (oldSettings.AutoResumeOnLaunch != settings.AutoResumeOnLaunch)
                    changed.ChangedProperties.Add(nameof(Settings.AutoResumeOnLaunch));
            }
            SettingsChanged?.Invoke(this, changed);

            _logger.LogInformation("Settings updated successfully");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ResetToDefaultsAsync()
    {
        var defaultSettings = CreateDefaultSettings();
        await SaveSettingsAsync(defaultSettings);
        _logger.LogInformation("Settings reset to defaults");
    }

    public async Task<T?> GetSettingAsync<T>(string key)
    {
        return await _repository.GetSettingAsync<T>(key);
    }

    public async Task SetSettingAsync<T>(string key, T value)
    {
        await _repository.SetSettingAsync(key, value);

        // Invalidate cache
        await _semaphore.WaitAsync();
        try
        {
            _cachedSettings = null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<SettingsValidationResult> ValidateSettingsAsync(Settings settings)
    {
        await Task.CompletedTask;

        var result = new SettingsValidationResult { IsValid = true };

        // Validate download directory
        if (string.IsNullOrWhiteSpace(settings.DefaultDownloadDir))
        {
            result.Errors.Add("Download directory cannot be empty");
            result.IsValid = false;
        }

        // Validate concurrency
        if (settings.Concurrency < 1 || settings.Concurrency > 16)
        {
            result.Errors.Add("Concurrency must be between 1 and 16");
            result.IsValid = false;
        }

        // Validate accent color
        if (string.IsNullOrEmpty(settings.Appearance.AccentColor))
        {
            result.Errors.Add("Accent color cannot be empty");
            result.IsValid = false;
        }
        else if (!IsValidHexColor(settings.Appearance.AccentColor))
        {
            result.Errors.Add("Invalid accent color format");
            result.IsValid = false;
        }

        return result;
    }

    public async Task UpdateConcurrencyAsync(int concurrency)
    {
        var settings = await GetSettingsAsync();
        // Create a copy to avoid mutating the original object
        var newSettings = new Settings
        {
            Concurrency = Math.Max(1, Math.Min(16, concurrency)), // Clamp between 1 and 16
            DefaultDownloadDir = settings.DefaultDownloadDir,
            AutoResumeOnLaunch = settings.AutoResumeOnLaunch,
            Appearance = settings.Appearance
        };
        await SaveSettingsAsync(newSettings);
    }

    public async Task<string?> ExportSettingsAsync(string filePath)
    {
        try
        {
            var settings = await GetSettingsAsync();
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export settings");
            return null;
        }
    }

    public async Task<bool> ImportSettingsAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;
            var json = await File.ReadAllTextAsync(filePath);
            var settings = JsonSerializer.Deserialize<Settings>(json);
            if (settings == null) return false;
            var validation = await ValidateSettingsAsync(settings);
            if (!validation.IsValid) return false;
            await SaveSettingsAsync(settings);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import settings");
            return false;
        }
    }

    private static bool IsValidHexColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color)) return false;

        var cleanColor = color.Trim();
        if (cleanColor.StartsWith("#"))
            cleanColor = cleanColor[1..];

        return cleanColor.Length == 6 &&
               cleanColor.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }

    private static Settings CreateDefaultSettings()
    {
        return new Settings
        {
            DefaultDownloadDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads", "ICNX"),
            Concurrency = 4,
            RetryLimit = 3,
            AutoResumeOnLaunch = true,
            RetryPolicy = new RetryPolicy(),
            Appearance = new Appearance(),
            EnableScriptAutoDetection = true,
            CustomSettings = new Dictionary<string, object>()
        };
    }
}