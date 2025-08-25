namespace ICNX.Scripting.Models;

/// <summary>
/// Represents a script for URL pattern matching and content extraction
/// </summary>
public class Script
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// URL patterns this script can handle (regex patterns)
    /// </summary>
    public List<string> UrlPatterns { get; set; } = new();

    /// <summary>
    /// Script content (JavaScript or C# expressions)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Script type (JavaScript, CSharp, etc.)
    /// </summary>
    public ScriptType Type { get; set; } = ScriptType.JavaScript;

    /// <summary>
    /// Priority for script execution (higher = more priority)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Types of scripts supported
/// </summary>
public enum ScriptType
{
    JavaScript,
    CSharp,
    Regex
}

/// <summary>
/// Result of script execution
/// </summary>
public class ScriptExecutionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ExtractedLink> ExtractedLinks { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// Represents an extracted download link
/// </summary>
public class ExtractedLink
{
    public string Url { get; set; } = string.Empty;
    public string? Filename { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public int Quality { get; set; } = 0; // For video/audio quality selection
    public string? Thumbnail { get; set; }
}

/// <summary>
/// Context provided to scripts during execution
/// </summary>
public class ScriptExecutionContext
{
    public string OriginalUrl { get; set; } = string.Empty;
    public string PageContent { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string UserAgent { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
}

/// <summary>
/// Script validation result
/// </summary>
public class ScriptValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}