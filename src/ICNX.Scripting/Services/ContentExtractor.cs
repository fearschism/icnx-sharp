using System.Text.RegularExpressions;
using ICNX.Scripting.Interfaces;
using ICNX.Scripting.Models;
using Microsoft.Extensions.Logging;

namespace ICNX.Scripting.Services;

/// <summary>
/// Service for extracting download links from web pages
/// </summary>
public class ContentExtractor : IContentExtractor
{
    private readonly IScriptRepository _scriptRepository;
    private readonly IUrlMatcher _urlMatcher;
    private readonly IScriptEngine _scriptEngine;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ContentExtractor> _logger;

    public ContentExtractor(
        IScriptRepository scriptRepository,
        IUrlMatcher urlMatcher,
        IScriptEngine scriptEngine,
        HttpClient httpClient,
        ILogger<ContentExtractor> logger)
    {
        _scriptRepository = scriptRepository;
        _urlMatcher = urlMatcher;
        _scriptEngine = scriptEngine;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ScriptExecutionResult> ExtractLinksAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Extracting links from URL: {Url}", url);

            // Get page content
            var pageContent = await GetPageContentAsync(url, cancellationToken);

            // Find matching scripts
            var scripts = await _scriptRepository.GetEnabledScriptsAsync();
            var matchingScripts = await _urlMatcher.FindBestMatchesAsync(url, scripts);

            // Try each script until one succeeds
            foreach (var script in matchingScripts)
            {
                var result = await ExtractLinksWithScriptAsync(url, script, cancellationToken);
                if (result.Success && result.ExtractedLinks.Any())
                {
                    _logger.LogInformation("Successfully extracted {Count} links using script {ScriptName}", 
                        result.ExtractedLinks.Count, script.Name);
                    return result;
                }
            }

            // Fallback to basic extraction
            _logger.LogInformation("No matching scripts found, using fallback extraction");
            return await ExtractLinksBasicAsync(url, pageContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting links from {Url}", url);
            return new ScriptExecutionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ScriptExecutionResult> ExtractLinksWithScriptAsync(string url, Script script, CancellationToken cancellationToken = default)
    {
        try
        {
            var pageContent = await GetPageContentAsync(url, cancellationToken);

            var context = new ScriptExecutionContext
            {
                OriginalUrl = url,
                PageContent = pageContent,
                Headers = GetDefaultHeaders(),
                UserAgent = GetUserAgent()
            };

            return await _scriptEngine.ExecuteAsync(script, context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing script {ScriptName} for URL {Url}", script.Name, url);
            return new ScriptExecutionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<string> GetPageContentAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Downloading page content from: {Url}", url);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", GetUserAgent());
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Downloaded {Length} characters from {Url}", content.Length, url);

            return content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error downloading content from {Url}", url);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout downloading content from {Url}", url);
            throw;
        }
    }

    /// <summary>
    /// Basic fallback extraction using common patterns
    /// </summary>
    private async Task<ScriptExecutionResult> ExtractLinksBasicAsync(string url, string pageContent)
    {
        var result = new ScriptExecutionResult { Success = true };

        try
        {
            // Check if this is a direct download link
            if (IsDirectDownloadLink(url))
            {
                result.ExtractedLinks.Add(new ExtractedLink
                {
                    Url = url,
                    Filename = ExtractFilenameFromUrl(url),
                    Title = "Direct Download"
                });
                return result;
            }

            // Extract common download link patterns
            var downloadLinks = ExtractDownloadLinksFromHtml(pageContent, url);
            result.ExtractedLinks.AddRange(downloadLinks);

            if (!result.ExtractedLinks.Any())
            {
                result.Success = false;
                result.ErrorMessage = "No download links found";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in basic extraction");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private bool IsDirectDownloadLink(string url)
    {
        var downloadExtensions = new[]
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".exe", ".msi", ".dmg", ".pkg", ".deb", ".rpm",
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v",
            ".mp3", ".wav", ".flac", ".ogg", ".aac", ".m4a", ".wma",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt"
        };

        var uri = new Uri(url);
        var path = uri.AbsolutePath.ToLowerInvariant();
        
        return downloadExtensions.Any(ext => path.EndsWith(ext));
    }

    private string ExtractFilenameFromUrl(string url)
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

    private List<ExtractedLink> ExtractDownloadLinksFromHtml(string html, string baseUrl)
    {
        var links = new List<ExtractedLink>();

        try
        {
            // Pattern for href attributes with download extensions
            var linkPattern = @"href=[""']([^""']*\.(?:zip|rar|7z|tar|gz|exe|msi|dmg|pkg|deb|rpm|mp4|avi|mkv|mov|wmv|flv|webm|m4v|mp3|wav|flac|ogg|aac|m4a|wma|jpg|jpeg|png|gif|bmp|webp|svg|pdf|doc|docx|xls|xlsx|ppt|pptx|txt))[""']";
            
            var matches = Regex.Matches(html, linkPattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                var linkUrl = match.Groups[1].Value;
                
                // Make absolute URL
                if (!Uri.IsWellFormedUriString(linkUrl, UriKind.Absolute))
                {
                    var baseUri = new Uri(baseUrl);
                    linkUrl = new Uri(baseUri, linkUrl).ToString();
                }

                links.Add(new ExtractedLink
                {
                    Url = linkUrl,
                    Filename = ExtractFilenameFromUrl(linkUrl),
                    Title = $"Found download link"
                });
            }

            // Remove duplicates
            links = links.GroupBy(l => l.Url).Select(g => g.First()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting links from HTML");
        }

        return links;
    }

    private Dictionary<string, string> GetDefaultHeaders()
    {
        return new Dictionary<string, string>
        {
            ["User-Agent"] = GetUserAgent(),
            ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
            ["Accept-Language"] = "en-US,en;q=0.5",
            ["Accept-Encoding"] = "gzip, deflate",
            ["Cache-Control"] = "no-cache"
        };
    }

    private static string GetUserAgent()
    {
        return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";
    }
}