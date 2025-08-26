using System.Text.RegularExpressions;
using ICNX.Scripting.Interfaces;
using ICNX.Scripting.Models;
using Microsoft.Extensions.Logging;

namespace ICNX.Scripting.Services;

/// <summary>
/// Service for matching URLs against script patterns
/// </summary>
public class UrlMatcher : IUrlMatcher
{
    private readonly Dictionary<string, Regex> _compiledPatterns = new();
    private readonly ILogger<UrlMatcher>? _logger;

    public UrlMatcher(ILogger<UrlMatcher>? logger = null)
    {
        _logger = logger;
    }

    public bool IsMatch(string url, IEnumerable<string> patterns)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrEmpty(pattern))
                continue;

            try
            {
                // First try simple substring matching
                if (IsSimplePatternMatch(url, pattern))
                    return true;

                // Then try as regex pattern
                var regex = GetCompiledPattern(pattern);
                if (regex.IsMatch(url))
                    return true;
            }
            catch (RegexMatchTimeoutException)
            {
                // Pattern took too long, skip it
                continue;
            }
            catch (ArgumentException)
            {
                // Invalid regex pattern, try simple substring match as fallback
                if (IsSimplePatternMatch(url, pattern))
                    return true;
            }
        }

        return false;
    }

    private bool IsSimplePatternMatch(string url, string pattern)
    {
        // Direct substring match
        if (url.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            return true;

        // Special handling for YouTube patterns
        if (pattern.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            if (url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Special handling for GitHub patterns with paths
        if (pattern.Contains("github.com/", StringComparison.OrdinalIgnoreCase))
        {
            var parts = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Contains("github.com", StringComparison.OrdinalIgnoreCase))
            {
                // Check if URL contains github.com and the specified path
                if (url.Contains("github.com", StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 1; i < parts.Length; i++)
                    {
                        if (url.Contains($"/{parts[i]}/", StringComparison.OrdinalIgnoreCase) ||
                            url.Contains($"/{parts[i]}", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public async Task<IEnumerable<Script>> FindBestMatchesAsync(string url, IEnumerable<Script> scripts)
    {
        var matches = new List<(Script script, int priority, int specificity)>();

        foreach (var script in scripts.Where(s => s.IsEnabled))
        {
            if (IsMatch(url, script.UrlPatterns))
            {
                var specificity = CalculateSpecificity(url, script.UrlPatterns);
                matches.Add((script, script.Priority, specificity));
            }
        }

        // Sort by priority first, then by specificity
        var sortedMatches = matches
            .OrderByDescending(m => m.priority)
            .ThenByDescending(m => m.specificity)
            .Select(m => m.script);

        return await Task.FromResult(sortedMatches);
    }

    private Regex GetCompiledPattern(string pattern)
    {
        if (_compiledPatterns.TryGetValue(pattern, out var cached))
            return cached;

        var regex = new Regex(pattern,
            RegexOptions.Compiled | RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));

        _compiledPatterns[pattern] = regex;
        return regex;
    }

    private int CalculateSpecificity(string url, IEnumerable<string> patterns)
    {
        int maxSpecificity = 0;

        foreach (var pattern in patterns)
        {
            try
            {
                var regex = GetCompiledPattern(pattern);
                var match = regex.Match(url);

                if (match.Success)
                {
                    // More specific patterns have longer matches
                    var specificity = match.Length + pattern.Length;
                    maxSpecificity = Math.Max(maxSpecificity, specificity);
                }
            }
            catch
            {
                // Ignore invalid patterns
            }
        }

        return maxSpecificity;
    }
}

/// <summary>
/// Built-in URL patterns for common sites
/// </summary>
public static class CommonUrlPatterns
{
    public static readonly Dictionary<string, List<string>> Patterns = new()
    {
        ["YouTube"] = new()
        {
            @"^https?://(www\.)?(youtube\.com/watch\?v=|youtu\.be/)[\w\-_]+",
            @"^https?://(www\.)?youtube\.com/playlist\?list=[\w\-_]+",
            @"^https?://(www\.)?youtube\.com/channel/[\w\-_]+",
        },

        ["Vimeo"] = new()
        {
            @"^https?://(www\.)?vimeo\.com/\d+",
            @"^https?://player\.vimeo\.com/video/\d+",
        },

        ["GitHub"] = new()
        {
            @"^https?://(www\.)?github\.com/[\w\-_]+/[\w\-_]+",
            @"^https?://(www\.)?github\.com/[\w\-_]+/[\w\-_]+/releases",
            @"^https?://(www\.)?github\.com/[\w\-_]+/[\w\-_]+/archive/",
        },

        ["DirectDownload"] = new()
        {
            @"^https?://.*\.(zip|rar|7z|tar|gz|exe|msi|dmg|pkg|deb|rpm)(\?.*)?$",
            @"^https?://.*\.(mp4|avi|mkv|mov|wmv|flv|webm|m4v)(\?.*)?$",
            @"^https?://.*\.(mp3|wav|flac|ogg|aac|m4a|wma)(\?.*)?$",
            @"^https?://.*\.(jpg|jpeg|png|gif|bmp|webp|svg|ico)(\?.*)?$",
            @"^https?://.*\.(pdf|doc|docx|xls|xlsx|ppt|pptx|txt)(\?.*)?$",
        },

        ["Dropbox"] = new()
        {
            @"^https?://(www\.)?dropbox\.com/s/[\w\-_]+",
            @"^https?://(www\.)?dropbox\.com/sh/[\w\-_]+",
        },

        ["GoogleDrive"] = new()
        {
            @"^https?://drive\.google\.com/file/d/[\w\-_]+",
            @"^https?://drive\.google\.com/drive/folders/[\w\-_]+",
        },

        ["OneDrive"] = new()
        {
            @"^https?://1drv\.ms/[\w\-_]+",
            @"^https?://[\w\-_]+\.sharepoint\.com/",
        }
    };

    /// <summary>
    /// Get all patterns as a flat list
    /// </summary>
    public static IEnumerable<string> GetAllPatterns()
    {
        return Patterns.Values.SelectMany(p => p);
    }

    /// <summary>
    /// Get patterns for a specific category
    /// </summary>
    public static IEnumerable<string> GetPatternsForCategory(string category)
    {
        return Patterns.TryGetValue(category, out var patterns) ? patterns : Enumerable.Empty<string>();
    }
}