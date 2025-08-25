using System.Text.Json;
using Dapper;
using ICNX.Core.Models;
using Microsoft.Extensions.Logging;

namespace ICNX.Persistence.Repositories;

/// <summary>
/// Repository for application settings (single row configuration)
/// </summary>
public class SettingsRepository : BaseRepository<Settings>
{
    private const string MainSettingsId = "main";

    public SettingsRepository(string connectionString, ILogger<SettingsRepository> logger) 
        : base(connectionString, logger)
    {
    }

    protected override string GetTableName() => "settings";

    public override async Task<Settings?> GetByIdAsync(string id)
    {
        return await GetSettingsAsync();
    }

    public override async Task<IEnumerable<Settings>> GetAllAsync()
    {
        var settings = await GetSettingsAsync();
        return settings != null ? new[] { settings } : Enumerable.Empty<Settings>();
    }

    public override async Task<string> AddAsync(Settings settings)
    {
        await SaveSettingsAsync(settings);
        return MainSettingsId;
    }

    public override async Task<bool> UpdateAsync(Settings settings)
    {
        await SaveSettingsAsync(settings);
        return true;
    }

    public override async Task<bool> DeleteAsync(string id)
    {
        // Don't allow deleting settings, reset to defaults instead
        await ResetToDefaultsAsync();
        return true;
    }

    /// <summary>
    /// Get the current application settings
    /// </summary>
    public async Task<Settings?> GetSettingsAsync()
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                SELECT settings_json, updated_at 
                FROM settings 
                WHERE id = @Id";
            
            var result = await connection.QueryFirstOrDefaultAsync(sql, new { Id = MainSettingsId });
            
            if (result?.settings_json == null)
            {
                // Return default settings if none exist
                return CreateDefaultSettings();
            }

            try
            {
                var settings = JsonSerializer.Deserialize<Settings>(result.settings_json);
                return settings ?? CreateDefaultSettings();
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "Failed to deserialize settings JSON, returning defaults");
                return CreateDefaultSettings();
            }
        });
    }

    /// <summary>
    /// Save application settings
    /// </summary>
    public async Task SaveSettingsAsync(Settings settings)
    {
        await ExecuteWithConnectionAsync(async connection =>
        {
            const string upsertSql = @"
                INSERT INTO settings (id, settings_json, updated_at) 
                VALUES (@Id, @SettingsJson, @UpdatedAt)
                ON CONFLICT(id) DO UPDATE SET 
                    settings_json = excluded.settings_json,
                    updated_at = excluded.updated_at";

            var settingsJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            await connection.ExecuteAsync(upsertSql, new
            {
                Id = MainSettingsId,
                SettingsJson = settingsJson,
                UpdatedAt = DateTime.UtcNow.ToString("O")
            });
        });

        Logger.LogInformation("Settings saved successfully");
    }

    /// <summary>
    /// Reset settings to default values
    /// </summary>
    public async Task ResetToDefaultsAsync()
    {
        var defaultSettings = CreateDefaultSettings();
        await SaveSettingsAsync(defaultSettings);
        
        Logger.LogInformation("Settings reset to defaults");
    }

    /// <summary>
    /// Get a specific setting value
    /// </summary>
    public async Task<T?> GetSettingAsync<T>(string key)
    {
        var settings = await GetSettingsAsync();
        if (settings?.CustomSettings.TryGetValue(key, out var value) == true)
        {
            try
            {
                if (value is JsonElement element)
                {
                    return element.Deserialize<T>();
                }
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to convert setting {Key} to type {Type}", key, typeof(T));
            }
        }
        
        return default;
    }

    /// <summary>
    /// Set a specific setting value
    /// </summary>
    public async Task SetSettingAsync<T>(string key, T value)
    {
        var settings = await GetSettingsAsync() ?? CreateDefaultSettings();
        settings.CustomSettings[key] = value!;
        await SaveSettingsAsync(settings);
        
        Logger.LogInformation("Setting {Key} updated", key);
    }

    /// <summary>
    /// Get when settings were last updated
    /// </summary>
    public async Task<DateTime?> GetLastUpdatedAsync()
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = "SELECT updated_at FROM settings WHERE id = @Id";
            var result = await connection.QueryFirstOrDefaultAsync<string>(sql, new { Id = MainSettingsId });
            
            return result != null ? DateTime.Parse(result) : (DateTime?)null;
        });
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
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 5,
                BaseDelayMs = 1000,
                BackoffFactor = 2.0,
                JitterPercent = 0.2,
                PerItemResetOnProgress = true,
                RetryStatusCodes = new List<int> { 408, 429, 500, 502, 503, 504 }
            },
            Appearance = new Appearance
            {
                EnableAnimations = true,
                ReducedEffects = false,
                CompactMode = false,
                EnableProgressSheen = true,
                EnableGlass = true,
                EnableBreathing = true,
                EnableProgressHalo = false,
                Theme = ThemeMode.Dark,
                AccentColor = "#3b82f6"
            },
            EnableScriptAutoDetection = true,
            CustomSettings = new Dictionary<string, object>()
        };
    }
}