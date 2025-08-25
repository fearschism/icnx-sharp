using ICNX.Scripting.Models;

namespace ICNX.Scripting.Interfaces;

/// <summary>
/// Main interface for the scripting engine
/// </summary>
public interface IScriptEngine
{
    /// <summary>
    /// Execute a script against a URL and page content
    /// </summary>
    Task<ScriptExecutionResult> ExecuteAsync(Script script, ScriptExecutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate script syntax and content
    /// </summary>
    Task<ScriptValidationResult> ValidateAsync(Script script);

    /// <summary>
    /// Get supported script types
    /// </summary>
    IEnumerable<ScriptType> GetSupportedTypes();
}

/// <summary>
/// Interface for managing script repository
/// </summary>
public interface IScriptRepository
{
    /// <summary>
    /// Add or update a script
    /// </summary>
    Task<string> SaveScriptAsync(Script script);

    /// <summary>
    /// Get script by ID
    /// </summary>
    Task<Script?> GetScriptAsync(string id);

    /// <summary>
    /// Get all scripts
    /// </summary>
    Task<IEnumerable<Script>> GetAllScriptsAsync();

    /// <summary>
    /// Get enabled scripts
    /// </summary>
    Task<IEnumerable<Script>> GetEnabledScriptsAsync();

    /// <summary>
    /// Find scripts that match a URL
    /// </summary>
    Task<IEnumerable<Script>> FindScriptsForUrlAsync(string url);

    /// <summary>
    /// Delete a script
    /// </summary>
    Task<bool> DeleteScriptAsync(string id);

    /// <summary>
    /// Enable or disable a script
    /// </summary>
    Task<bool> SetScriptEnabledAsync(string id, bool enabled);
}

/// <summary>
/// Interface for URL pattern matching
/// </summary>
public interface IUrlMatcher
{
    /// <summary>
    /// Check if URL matches any patterns
    /// </summary>
    bool IsMatch(string url, IEnumerable<string> patterns);

    /// <summary>
    /// Find the best matching scripts for a URL
    /// </summary>
    Task<IEnumerable<Script>> FindBestMatchesAsync(string url, IEnumerable<Script> scripts);
}

/// <summary>
/// Interface for content extraction
/// </summary>
public interface IContentExtractor
{
    /// <summary>
    /// Extract download links from page content
    /// </summary>
    Task<ScriptExecutionResult> ExtractLinksAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract links using specific script
    /// </summary>
    Task<ScriptExecutionResult> ExtractLinksWithScriptAsync(string url, Script script, CancellationToken cancellationToken = default);

    /// <summary>
    /// Download page content for processing
    /// </summary>
    Task<string> GetPageContentAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for script execution sandbox
/// </summary>
public interface IScriptSandbox
{
    /// <summary>
    /// Execute code in a sandboxed environment
    /// </summary>
    Task<object?> ExecuteAsync(string code, ScriptExecutionContext context, TimeSpan timeout);

    /// <summary>
    /// Check if code is safe to execute
    /// </summary>
    bool IsSafeCode(string code);
}