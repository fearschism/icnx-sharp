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
    private readonly SettingsRepository _repository;
    private readonly ILogger<SettingsService> _logger;
    private Settings? _cachedSettings;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly BehaviorSubject<Settings> _settingsSubject;

    public IObservable<Settings> SettingsChanged => _settingsSubject.AsObservable();

    public SettingsService(SettingsRepository repository, ILogger<SettingsService> logger)
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

            await _repository.SaveSettingsAsync(settings);
            _cachedSettings = settings;

            // Publish to observable
            _settingsSubject.OnNext(settings);
            
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
        if (!string.IsNullOrEmpty(settings.Appearance.AccentColor))
        {
            if (!IsValidHexColor(settings.Appearance.AccentColor))
            {
                result.Errors.Add("Invalid accent color format");
                result.IsValid = false;
            }
        }

        return result;
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