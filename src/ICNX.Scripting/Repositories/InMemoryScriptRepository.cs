using System.Collections.Concurrent;
using System.Text.Json;
using ICNX.Scripting.Interfaces;
using ICNX.Scripting.Models;
using ICNX.Scripting.Services;
using Microsoft.Extensions.Logging;

namespace ICNX.Scripting.Repositories;

/// <summary>
/// In-memory script repository with built-in scripts
/// </summary>
public class InMemoryScriptRepository : IScriptRepository
{
    private readonly ConcurrentDictionary<string, Script> _scripts = new();
    private readonly IUrlMatcher _urlMatcher;
    private readonly ILogger<InMemoryScriptRepository> _logger;

    public InMemoryScriptRepository(IUrlMatcher urlMatcher, ILogger<InMemoryScriptRepository> logger)
    {
        _urlMatcher = urlMatcher;
        _logger = logger;
        
        // Initialize with built-in scripts
        InitializeBuiltInScripts();
    }

    public async Task<string> SaveScriptAsync(Script script)
    {
        if (string.IsNullOrEmpty(script.Id))
        {
            script.Id = Guid.NewGuid().ToString();
        }

        script.UpdatedAt = DateTime.UtcNow;
        _scripts.AddOrUpdate(script.Id, script, (key, old) => script);
        
        _logger.LogInformation("Saved script: {ScriptName} ({Id})", script.Name, script.Id);
        return await Task.FromResult(script.Id);
    }

    public async Task<Script?> GetScriptAsync(string id)
    {
        _scripts.TryGetValue(id, out var script);
        return await Task.FromResult(script);
    }

    public async Task<IEnumerable<Script>> GetAllScriptsAsync()
    {
        return await Task.FromResult(_scripts.Values.ToList());
    }

    public async Task<IEnumerable<Script>> GetEnabledScriptsAsync()
    {
        var enabledScripts = _scripts.Values.Where(s => s.IsEnabled).ToList();
        return await Task.FromResult(enabledScripts);
    }

    public async Task<IEnumerable<Script>> FindScriptsForUrlAsync(string url)
    {
        var allScripts = await GetEnabledScriptsAsync();
        var matchingScripts = await _urlMatcher.FindBestMatchesAsync(url, allScripts);
        return matchingScripts;
    }

    public async Task<bool> DeleteScriptAsync(string id)
    {
        var removed = _scripts.TryRemove(id, out var script);
        if (removed && script != null)
        {
            _logger.LogInformation("Deleted script: {ScriptName} ({Id})", script.Name, id);
        }
        return await Task.FromResult(removed);
    }

    public async Task<bool> SetScriptEnabledAsync(string id, bool enabled)
    {
        if (_scripts.TryGetValue(id, out var script))
        {
            script.IsEnabled = enabled;
            script.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Set script {ScriptName} enabled: {Enabled}", script.Name, enabled);
            return await Task.FromResult(true);
        }
        return await Task.FromResult(false);
    }

    private void InitializeBuiltInScripts()
    {
        _logger.LogInformation("Initializing built-in scripts");

        // Direct download detector
        var directDownloadScript = new Script
        {
            Id = "builtin-direct-download",
            Name = "Direct Download Detector",
            Description = "Detects direct download links for common file types",
            Author = "ICNX",
            Version = "1.0.0",
            Type = ScriptType.Regex,
            Priority = 10,
            IsEnabled = true,
            Tags = new List<string> { "builtin", "direct", "files" },
            UrlPatterns = CommonUrlPatterns.GetPatternsForCategory("DirectDownload").ToList(),
            Content = JsonSerializer.Serialize(new RegexScriptConfig
            {
                Patterns = new List<RegexPattern>
                {
                    new RegexPattern
                    {
                        Pattern = @"^(https?://.*\.(zip|rar|7z|tar|gz|exe|msi|dmg|pkg|deb|rpm|mp4|avi|mkv|mov|wmv|flv|webm|m4v|mp3|wav|flac|ogg|aac|m4a|wma|jpg|jpeg|png|gif|bmp|webp|svg|pdf|doc|docx|xls|xlsx|ppt|pptx|txt))(\?.*)?$",
                        UrlGroup = "1"
                    }
                }
            })
        };

        // GitHub releases detector
        var githubScript = new Script
        {
            Id = "builtin-github-releases",
            Name = "GitHub Releases",
            Description = "Extracts download links from GitHub release pages",
            Author = "ICNX",
            Version = "1.0.0",
            Type = ScriptType.Regex,
            Priority = 20,
            IsEnabled = true,
            Tags = new List<string> { "builtin", "github", "releases" },
            UrlPatterns = CommonUrlPatterns.GetPatternsForCategory("GitHub").ToList(),
            Content = JsonSerializer.Serialize(new RegexScriptConfig
            {
                Patterns = new List<RegexPattern>
                {
                    new RegexPattern
                    {
                        Pattern = @"href=[""'](https://github\.com/[^/]+/[^/]+/releases/download/[^""']+)[""']",
                        UrlGroup = "1"
                    },
                    new RegexPattern
                    {
                        Pattern = @"href=[""'](https://github\.com/[^/]+/[^/]+/archive/[^""']+\.(?:zip|tar\.gz))[""']",
                        UrlGroup = "1"
                    }
                }
            })
        };

        // Dropbox public links
        var dropboxScript = new Script
        {
            Id = "builtin-dropbox",
            Name = "Dropbox Public Links",
            Description = "Converts Dropbox share links to direct download links",
            Author = "ICNX",
            Version = "1.0.0",
            Type = ScriptType.Regex,
            Priority = 15,
            IsEnabled = true,
            Tags = new List<string> { "builtin", "dropbox", "cloud" },
            UrlPatterns = CommonUrlPatterns.GetPatternsForCategory("Dropbox").ToList(),
            Content = JsonSerializer.Serialize(new RegexScriptConfig
            {
                Patterns = new List<RegexPattern>
                {
                    new RegexPattern
                    {
                        Pattern = @"^(https://(?:www\.)?dropbox\.com/s/[^?]+)",
                        UrlGroup = "1"
                    }
                }
            })
        };

        // Generic link extractor
        var genericScript = new Script
        {
            Id = "builtin-generic-links",
            Name = "Generic Link Extractor",
            Description = "Extracts download links from any webpage using common patterns",
            Author = "ICNX",
            Version = "1.0.0",
            Type = ScriptType.Regex,
            Priority = 1, // Lowest priority - fallback
            IsEnabled = true,
            Tags = new List<string> { "builtin", "generic", "fallback" },
            UrlPatterns = new List<string> { @"^https?://.*" }, // Matches any HTTP(S) URL
            Content = JsonSerializer.Serialize(new RegexScriptConfig
            {
                Patterns = new List<RegexPattern>
                {
                    new RegexPattern
                    {
                        Pattern = @"href=[""']([^""']*\.(?:zip|rar|7z|tar|gz|exe|msi|dmg|pkg|deb|rpm|mp4|avi|mkv|mov|wmv|flv|webm|m4v|mp3|wav|flac|ogg|aac|m4a|wma|jpg|jpeg|png|gif|bmp|webp|svg|pdf|doc|docx|xls|xlsx|ppt|pptx|txt))[""']",
                        UrlGroup = "1"
                    },
                    new RegexPattern
                    {
                        Pattern = @"<a[^>]*download[^>]*href=[""']([^""']+)[""']",
                        UrlGroup = "1"
                    }
                }
            })
        };

        // Save all built-in scripts
        var builtInScripts = new[] { directDownloadScript, githubScript, dropboxScript, genericScript };
        
        foreach (var script in builtInScripts)
        {
            _scripts.TryAdd(script.Id, script);
            _logger.LogDebug("Added built-in script: {ScriptName}", script.Name);
        }

        _logger.LogInformation("Initialized {Count} built-in scripts", builtInScripts.Length);
    }
}