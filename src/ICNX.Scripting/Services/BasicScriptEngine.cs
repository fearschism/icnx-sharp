using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ICNX.Scripting.Interfaces;
using ICNX.Scripting.Models;
using Microsoft.Extensions.Logging;

namespace ICNX.Scripting.Services;

/// <summary>
/// Basic script engine supporting regex and pattern-based extraction
/// </summary>
public class BasicScriptEngine : IScriptEngine
{
    private readonly ILogger<BasicScriptEngine> _logger;
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);

    public BasicScriptEngine(ILogger<BasicScriptEngine> logger)
    {
        _logger = logger;
    }

    public async Task<ScriptExecutionResult> ExecuteAsync(Script script, ScriptExecutionContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ScriptExecutionResult();

        try
        {
            _logger.LogDebug("Executing script: {ScriptName} ({Type})", script.Name, script.Type);

            result = script.Type switch
            {
                ScriptType.Regex => await ExecuteRegexScriptAsync(script, context, cancellationToken),
                ScriptType.JavaScript => await ExecuteJavaScriptAsync(script, context, cancellationToken),
                ScriptType.CSharp => await ExecuteCSharpAsync(script, context, cancellationToken),
                _ => throw new NotSupportedException($"Script type {script.Type} is not supported")
            };

            result.Success = true;
            _logger.LogDebug("Script execution completed: {ScriptName}, Links: {Count}", script.Name, result.ExtractedLinks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing script: {ScriptName}", script.Name);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<ScriptValidationResult> ValidateAsync(Script script)
    {
        var result = new ScriptValidationResult();

        try
        {
            switch (script.Type)
            {
                case ScriptType.Regex:
                    result = ValidateRegexScript(script);
                    break;
                case ScriptType.JavaScript:
                    result = await ValidateJavaScriptAsync(script);
                    break;
                case ScriptType.CSharp:
                    result = await ValidateCSharpAsync(script);
                    break;
                default:
                    result.Errors.Add($"Unsupported script type: {script.Type}");
                    break;
            }

            result.IsValid = !result.Errors.Any();
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
        }

        return result;
    }

    public IEnumerable<ScriptType> GetSupportedTypes()
    {
        return new[] { ScriptType.Regex, ScriptType.JavaScript, ScriptType.CSharp };
    }

    private async Task<ScriptExecutionResult> ExecuteRegexScriptAsync(Script script, ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        var result = new ScriptExecutionResult();

        try
        {
            // Parse script content as JSON configuration
            var config = JsonSerializer.Deserialize<RegexScriptConfig>(script.Content);
            if (config == null)
            {
                throw new InvalidOperationException("Invalid regex script configuration");
            }

            // Extract links using regex patterns
            foreach (var pattern in config.Patterns)
            {
                var regex = new Regex(pattern.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
                var matches = regex.Matches(context.PageContent);

                foreach (Match match in matches)
                {
                    var link = new ExtractedLink();

                    // Extract URL (required)
                    if (!string.IsNullOrEmpty(pattern.UrlGroup) && match.Groups[pattern.UrlGroup].Success)
                    {
                        link.Url = match.Groups[pattern.UrlGroup].Value;
                    }
                    else if (match.Groups.Count > 1)
                    {
                        link.Url = match.Groups[1].Value;
                    }
                    else
                    {
                        continue; // Skip if no URL found
                    }

                    // Make URL absolute if needed
                    if (!Uri.IsWellFormedUriString(link.Url, UriKind.Absolute))
                    {
                        var baseUri = new Uri(context.OriginalUrl);
                        link.Url = new Uri(baseUri, link.Url).ToString();
                    }

                    // Extract optional fields
                    if (!string.IsNullOrEmpty(pattern.FilenameGroup) && match.Groups[pattern.FilenameGroup].Success)
                    {
                        link.Filename = match.Groups[pattern.FilenameGroup].Value;
                    }

                    if (!string.IsNullOrEmpty(pattern.TitleGroup) && match.Groups[pattern.TitleGroup].Success)
                    {
                        link.Title = match.Groups[pattern.TitleGroup].Value;
                    }

                    link.Filename ??= ExtractFilenameFromUrl(link.Url);
                    link.Title ??= link.Filename;

                    result.ExtractedLinks.Add(link);
                }
            }

            // Remove duplicates
            result.ExtractedLinks = result.ExtractedLinks
                .GroupBy(l => l.Url)
                .Select(g => g.First())
                .ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Regex script execution failed: {ex.Message}", ex);
        }

        return result;
    }

    private async Task<ScriptExecutionResult> ExecuteJavaScriptAsync(Script script, ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        // For now, return a placeholder result
        // In a full implementation, you would use a JavaScript engine like Jint or V8
        await Task.Delay(1, cancellationToken);
        
        return new ScriptExecutionResult
        {
            Success = false,
            ErrorMessage = "JavaScript execution not yet implemented"
        };
    }

    private async Task<ScriptExecutionResult> ExecuteCSharpAsync(Script script, ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        // For now, return a placeholder result
        // In a full implementation, you would use Roslyn for C# script compilation
        await Task.Delay(1, cancellationToken);
        
        return new ScriptExecutionResult
        {
            Success = false,
            ErrorMessage = "C# script execution not yet implemented"
        };
    }

    private ScriptValidationResult ValidateRegexScript(Script script)
    {
        var result = new ScriptValidationResult();

        try
        {
            var config = JsonSerializer.Deserialize<RegexScriptConfig>(script.Content);
            if (config == null)
            {
                result.Errors.Add("Invalid JSON configuration");
                return result;
            }

            if (config.Patterns == null || !config.Patterns.Any())
            {
                result.Errors.Add("No patterns defined");
                return result;
            }

            foreach (var pattern in config.Patterns)
            {
                try
                {
                    var regex = new Regex(pattern.Pattern, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException ex)
                {
                    result.Errors.Add($"Invalid regex pattern '{pattern.Pattern}': {ex.Message}");
                }
            }
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"JSON parsing error: {ex.Message}");
        }

        return result;
    }

    private async Task<ScriptValidationResult> ValidateJavaScriptAsync(Script script)
    {
        await Task.Delay(1);
        return new ScriptValidationResult
        {
            IsValid = true,
            Warnings = { "JavaScript validation not yet implemented" }
        };
    }

    private async Task<ScriptValidationResult> ValidateCSharpAsync(Script script)
    {
        await Task.Delay(1);
        return new ScriptValidationResult
        {
            IsValid = true,
            Warnings = { "C# validation not yet implemented" }
        };
    }

    private static string ExtractFilenameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var filename = Path.GetFileName(uri.LocalPath);
            return string.IsNullOrEmpty(filename) ? "download" : filename;
        }
        catch
        {
            return "download";
        }
    }
}

/// <summary>
/// Configuration for regex-based scripts
/// </summary>
public class RegexScriptConfig
{
    public List<RegexPattern> Patterns { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = new();
}

/// <summary>
/// Regex pattern configuration
/// </summary>
public class RegexPattern
{
    public string Pattern { get; set; } = string.Empty;
    public string? UrlGroup { get; set; }
    public string? FilenameGroup { get; set; }
    public string? TitleGroup { get; set; }
    public string? DescriptionGroup { get; set; }
}