using System.Text.Json;

namespace ICNX.Core.Models;

/// <summary>
/// Information about available scripts
/// </summary>
public class ScriptInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty; // Regex pattern to match URLs
    public DateTime CreatedAt { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Result of running a scraping script
/// </summary>
public class ScrapeResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ScriptName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string DataJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Deserialize the data JSON to a specific type
    /// </summary>
    public T? GetData<T>() where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(DataJson);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Set the data as JSON
    /// </summary>
    public void SetData<T>(T data) where T : class
    {
        DataJson = JsonSerializer.Serialize(data);
    }
}

/// <summary>
/// Script execution context for JavaScript scripts
/// </summary>
public class ScriptExecutionContext
{
    public string InputUrl { get; set; } = string.Empty;
    public Dictionary<string, object> Settings { get; set; } = new();
    public Action<string, string?>? EnqueueDownload { get; set; }
    public Action<string>? LogInfo { get; set; }
    public Action<string>? LogError { get; set; }
}

/// <summary>
/// Script detection result
/// </summary>
public class ScriptDetectionResult
{
    public ScriptInfo Script { get; set; } = new();
    public bool IsMatch { get; set; }
    public int Priority { get; set; } = 0; // Higher number = higher priority
}