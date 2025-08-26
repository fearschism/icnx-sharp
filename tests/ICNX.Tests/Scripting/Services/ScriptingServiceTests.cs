using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ICNX.Scripting.Models;
using ICNX.Scripting.Services;
using ICNX.Scripting.Interfaces;
using ICNX.Tests.Testing;

namespace ICNX.Tests.Scripting.Services;

public class UrlMatcherTests
{
    private readonly Mock<ILogger<UrlMatcher>> _mockLogger;
    private readonly UrlMatcher _urlMatcher;

    public UrlMatcherTests()
    {
        _mockLogger = new Mock<ILogger<UrlMatcher>>();
        _urlMatcher = new UrlMatcher(_mockLogger.Object);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "youtube.com", true)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "youtube.com", true)]
    [InlineData("https://github.com/user/repo/releases/tag/v1.0", "github.com/releases", true)]
    [InlineData("https://example.com/file.zip", "youtube.com", false)]
    [InlineData("https://dropbox.com/s/abc123/file.pdf", "dropbox.com", true)]
    public void IsMatch_ShouldMatchUrlPatterns_Correctly(string url, string pattern, bool expectedMatch)
    {
        // Arrange
        var patterns = new[] { pattern };

        // Act
        var result = _urlMatcher.IsMatch(url, patterns);

        // Assert
        result.Should().Be(expectedMatch);
    }

    [Fact]
    public void IsMatch_ShouldHandleRegexPatterns()
    {
        // Arrange
        var url = "https://example.com/download/file123.zip";
        var patterns = new[] { @"example\.com/download/file\d+\.zip" };

        // Act
        var result = _urlMatcher.IsMatch(url, patterns);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_ShouldHandleMultiplePatterns()
    {
        // Arrange
        var url = "https://github.com/user/repo/archive/main.zip";
        var patterns = new[]
        {
            "youtube.com",
            "github.com/archive",
            "dropbox.com"
        };

        // Act
        var result = _urlMatcher.IsMatch(url, patterns);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_ShouldReturnFalse_WhenNoPatternMatches()
    {
        // Arrange
        var url = "https://example.com/file.zip";
        var patterns = new[] { "youtube.com", "github.com", "dropbox.com" };

        // Act
        var result = _urlMatcher.IsMatch(url, patterns);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsMatch_ShouldHandleEmptyPatterns()
    {
        // Arrange
        var url = "https://example.com/file.zip";
        var patterns = Array.Empty<string>();

        // Act
        var result = _urlMatcher.IsMatch(url, patterns);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task FindBestMatchesAsync_ShouldReturnOrderedMatches()
    {
        // Arrange
        var url = "https://github.com/user/repo/releases/download/v1.0/app.zip";
        var scripts = new[]
        {
            new Script
            {
                Id = "generic",
                Name = "Generic Link Extractor",
                UrlPatterns = new List<string> { ".*" }, // Matches everything
                Priority = 1
            },
            new Script
            {
                Id = "github",
                Name = "GitHub Releases",
                UrlPatterns = new List<string> { "github.com/releases", "github.com/.*/releases" },
                Priority = 10
            }
        };

        // Act
        var result = await _urlMatcher.FindBestMatchesAsync(url, scripts);

        // Assert
        result.Should().HaveCount(2);
        result.First().Id.Should().Be("github"); // Higher priority script should be first
        result.Last().Id.Should().Be("generic");
    }

    [Fact]
    public async Task FindBestMatchesAsync_ShouldExcludeNonMatches()
    {
        // Arrange
        var url = "https://example.com/file.zip";
        var scripts = new[]
        {
            new Script
            {
                Id = "youtube",
                Name = "YouTube Downloader",
                UrlPatterns = new List<string> { "youtube.com", "youtu.be" }
            },
            new Script
            {
                Id = "dropbox",
                Name = "Dropbox Files",
                UrlPatterns = new List<string> { "dropbox.com" }
            }
        };

        // Act
        var result = await _urlMatcher.FindBestMatchesAsync(url, scripts);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void IsMatch_ShouldBeCaseInsensitive()
    {
        // Arrange
        var url = "https://GITHUB.COM/user/repo";
        var patterns = new[] { "github.com" };

        // Act
        var result = _urlMatcher.IsMatch(url, patterns);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("not-a-url", false)]
    [InlineData("https://valid-url.com", true)]
    public void IsMatch_ShouldHandleInvalidUrls(string url, bool shouldProcess)
    {
        // Arrange
        var patterns = new[] { "valid-url.com" };

        // Act & Assert
        if (shouldProcess)
        {
            var result = _urlMatcher.IsMatch(url, patterns);
            result.Should().BeTrue();
        }
        else
        {
            var act = () => _urlMatcher.IsMatch(url, patterns);
            act.Should().NotThrow(); // Should handle gracefully, not throw

            var result = _urlMatcher.IsMatch(url, patterns);
            result.Should().BeFalse();
        }
    }
}

public class ContentExtractorTests
{
    private readonly Mock<IScriptRepository> _mockScriptRepository;
    private readonly Mock<IUrlMatcher> _mockUrlMatcher;
    private readonly Mock<IScriptEngine> _mockScriptEngine;
    private readonly Mock<ILogger<ContentExtractor>> _mockLogger;
    private readonly ContentExtractor _contentExtractor;

    public ContentExtractorTests()
    {
        _mockScriptRepository = new Mock<IScriptRepository>();
        _mockUrlMatcher = new Mock<IUrlMatcher>();
        _mockScriptEngine = new Mock<IScriptEngine>();
        _mockLogger = new Mock<ILogger<ContentExtractor>>();

        _contentExtractor = new ContentExtractor(
            _mockScriptRepository.Object,
            _mockUrlMatcher.Object,
            _mockScriptEngine.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ExtractLinksAsync_ShouldReturnDirectDownload_WhenUrlIsDirectFile()
    {
        // Arrange
        var url = "https://example.com/file.zip";

        _mockScriptRepository.Setup(r => r.GetEnabledScriptsAsync())
            .ReturnsAsync(new List<Script>());

        // Act
        var result = await _contentExtractor.ExtractLinksAsync(url);

        // Assert
        result.Success.Should().BeTrue();
        result.ExtractedLinks.Should().HaveCount(1);
        result.ExtractedLinks.First().Url.Should().Be(url);
        result.ExtractedLinks.First().Filename.Should().Be("file.zip");
    }

    [Fact]
    public async Task ExtractLinksAsync_ShouldUseScript_WhenPatternMatches()
    {
        // Arrange
        var url = "https://github.com/user/repo/releases";
        var script = new Script
        {
            Id = "github",
            Name = "GitHub Releases",
            UrlPatterns = new List<string> { "github.com/releases" }
        };

        var scriptResult = new ScriptExecutionResult
        {
            Success = true,
            ExtractedLinks = new List<ExtractedLink>
            {
                new() { Url = "https://github.com/user/repo/releases/download/v1.0/app.zip", Filename = "app.zip" }
            }
        };

        _mockScriptRepository.Setup(r => r.GetEnabledScriptsAsync())
            .ReturnsAsync(new List<Script> { script });
        _mockUrlMatcher.Setup(m => m.FindBestMatchesAsync(url, It.IsAny<IEnumerable<Script>>()))
            .ReturnsAsync(new List<Script> { script });
        _mockScriptEngine.Setup(e => e.ExecuteAsync(script, It.IsAny<ScriptExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scriptResult);

        // Act
        var result = await _contentExtractor.ExtractLinksAsync(url);

        // Assert
        result.Success.Should().BeTrue();
        result.ExtractedLinks.Should().HaveCount(1);
        result.ExtractedLinks.First().Filename.Should().Be("app.zip");
    }

    [Fact]
    public async Task ExtractLinksAsync_ShouldFallbackToGeneric_WhenScriptFails()
    {
        // Arrange
        var url = "https://example.com/downloads.html";
        var script = new Script
        {
            Id = "test",
            UrlPatterns = new List<string> { "example.com" }
        };

        var failedResult = new ScriptExecutionResult
        {
            Success = false,
            Error = "Script execution failed"
        };

        _mockScriptRepository.Setup(r => r.GetEnabledScriptsAsync())
            .ReturnsAsync(new List<Script> { script });
        _mockUrlMatcher.Setup(m => m.FindBestMatchesAsync(url, It.IsAny<IEnumerable<Script>>()))
            .ReturnsAsync(new List<Script> { script });
        _mockScriptEngine.Setup(e => e.ExecuteAsync(script, It.IsAny<ScriptExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResult);

        // Act
        var result = await _contentExtractor.ExtractLinksAsync(url);

        // Assert
        result.Success.Should().BeTrue(); // Should succeed with fallback
        result.ExtractedLinks.Should().NotBeEmpty(); // Should have fallback generic link
    }

    [Fact]
    public async Task ExtractLinksAsync_ShouldHandleTimeout()
    {
        // Arrange
        var url = "https://slow-site.com/file.zip";
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromMilliseconds(1)).Token;

        // Act
        var result = await _contentExtractor.ExtractLinksAsync(url, cancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("timeout", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://example.com/file.zip", "file.zip")]
    [InlineData("https://example.com/downloads/archive.tar.gz", "archive.tar.gz")]
    [InlineData("https://example.com/api/download?file=data.json", "data.json")]
    [InlineData("https://example.com/no-extension", "download")]
    public async Task ExtractLinksAsync_ShouldExtractCorrectFilename(string url, string expectedFilename)
    {
        // Arrange
        _mockScriptRepository.Setup(r => r.GetEnabledScriptsAsync())
            .ReturnsAsync(new List<Script>());

        // Act
        var result = await _contentExtractor.ExtractLinksAsync(url);

        // Assert
        result.ExtractedLinks.First().Filename.Should().Be(expectedFilename);
    }
}