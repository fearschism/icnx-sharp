using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ICNX.Core.Models;

/// <summary>
/// Application settings
/// </summary>
public class Settings
{
    public string DefaultDownloadDir { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "ICNX");
    public int Concurrency { get; set; } = 4;
    public int RetryLimit { get; set; } = 3; // Legacy property, use RetryPolicy instead
    public bool AutoResumeOnLaunch { get; set; } = true;
    public RetryPolicy RetryPolicy { get; set; } = new();
    public Appearance Appearance { get; set; } = new();
    public long? SpeedLimitBytesPerSec { get; set; }
    public bool EnableScriptAutoDetection { get; set; } = true;
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// Retry policy configuration
/// </summary>
public class RetryPolicy
{
    public bool Enabled { get; set; } = true;
    public int MaxAttempts { get; set; } = 5;
    public int BaseDelayMs { get; set; } = 1000;
    public double BackoffFactor { get; set; } = 2.0;
    public double JitterPercent { get; set; } = 0.2;
    public bool PerItemResetOnProgress { get; set; } = true;
    public List<int> RetryStatusCodes { get; set; } = new() { 408, 429, 500, 502, 503, 504 };
    
    /// <summary>
    /// Calculate delay for a specific retry attempt
    /// </summary>
    public TimeSpan CalculateDelay(int attempt)
    {
        if (!Enabled || attempt <= 0) return TimeSpan.Zero;
        
        var baseDelay = BaseDelayMs * Math.Pow(BackoffFactor, attempt - 1);
        var maxDelay = 30000; // Cap at 30 seconds
        var delay = Math.Min(baseDelay, maxDelay);
        
        // Add jitter
        var random = new Random();
        var jitter = delay * JitterPercent * (2 * random.NextDouble() - 1);
        delay += jitter;
        
        return TimeSpan.FromMilliseconds(Math.Max(0, delay));
    }
    
    /// <summary>
    /// Check if a status code should be retried
    /// </summary>
    public bool ShouldRetry(int statusCode)
    {
        return Enabled && RetryStatusCodes.Contains(statusCode);
    }
}

/// <summary>
/// UI appearance settings
/// </summary>
public class Appearance
{
    public bool EnableAnimations { get; set; } = true;
    public bool ReducedEffects { get; set; } = false;
    public bool CompactMode { get; set; } = false;
    public bool EnableProgressSheen { get; set; } = true;
    public bool EnableGlass { get; set; } = true;
    public bool EnableBreathing { get; set; } = true;
    public bool EnableProgressHalo { get; set; } = false;
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public string AccentColor { get; set; } = "#3b82f6";
}

/// <summary>
/// Theme modes
/// </summary>
public enum ThemeMode
{
    Light,
    Dark,
    HighContrast,
    System
}

/// <summary>
/// Application info for settings management
/// </summary>
public class AppInfo
{
    public string Version { get; set; } = "1.0.0";
    public DateTime LastLaunch { get; set; }
    public int LaunchCount { get; set; }
    public bool FirstRun { get; set; } = true;
    public string InstallationId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Performance metrics for monitoring
/// </summary>
public class PerformanceMetrics
{
    public TimeSpan AverageFrameTime { get; set; }
    public long MemoryUsageMB { get; set; }
    public int ActiveDownloads { get; set; }
    public double TotalSpeedMBps { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of settings validation
/// </summary>
public class SettingsValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}