using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ICNX.Core.Models;
using ICNX.Core.Interfaces;
using ICNX.Core.Services;
using ICNX.Persistence.Services;
using ICNX.Persistence.Repositories;
using System.Text.Json;

namespace ICNX.Tests.Core.Services;

public class SettingsServiceTests
{
    private readonly Mock<ISettingsRepository> _mockRepository;
    private readonly Mock<ILogger<SettingsService>> _mockLogger;
    private readonly SettingsService _settingsService;

    public SettingsServiceTests()
    {
        _mockRepository = new Mock<ISettingsRepository>();
        _mockLogger = new Mock<ILogger<SettingsService>>();
        _settingsService = new SettingsService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetSettingsAsync_ShouldReturnDefaultSettings_WhenNoSettingsExist()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetSettingsAsync()).ReturnsAsync((Settings?)null);
        _mockRepository.Setup(r => r.SaveSettingsAsync(It.IsAny<Settings>())).Returns(Task.CompletedTask);

        // Act
        var result = await _settingsService.GetSettingsAsync();

        // Assert
        result.Should().NotBeNull();
        result.DefaultDownloadDir.Should().NotBeNullOrEmpty();
        result.Concurrency.Should().Be(4);
        result.AutoResumeOnLaunch.Should().BeTrue();
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldValidateSettings_BeforeSaving()
    {
        // Arrange
        var invalidSettings = new Settings
        {
            DefaultDownloadDir = "", // Invalid - empty directory
            Concurrency = 0, // Invalid - zero concurrency
            RetryPolicy = new RetryPolicy { MaxAttempts = -1 } // Invalid - negative attempts
        };

        // Act & Assert
        var act = async () => await _settingsService.SaveSettingsAsync(invalidSettings);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldFireEvent_WhenSettingsChange()
    {
        // Arrange
        var originalSettings = new Settings { Concurrency = 4 };
        var newSettings = new Settings { Concurrency = 8 };

        _mockRepository.Setup(r => r.GetSettingsAsync()).ReturnsAsync(originalSettings);
        _mockRepository.Setup(r => r.SaveSettingsAsync(It.IsAny<Settings>())).Returns(Task.CompletedTask);

        var eventFired = false;
        _settingsService.SettingsChanged += (sender, args) =>
        {
            eventFired = true;
            args.ChangedProperties.Should().Contain(nameof(Settings.Concurrency));
        };

        // Act
        await _settingsService.SaveSettingsAsync(newSettings);

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateConcurrencyAsync_ShouldClampValues_BetweenValidRange()
    {
        // Arrange
        var settings = new Settings { Concurrency = 4 };
        _mockRepository.Setup(r => r.GetSettingsAsync()).ReturnsAsync(settings);
        _mockRepository.Setup(r => r.SaveSettingsAsync(It.IsAny<Settings>())).Returns(Task.CompletedTask);

        // Act
        await _settingsService.UpdateConcurrencyAsync(0); // Below minimum
        var result1 = await _settingsService.GetSettingsAsync();

        await _settingsService.UpdateConcurrencyAsync(20); // Above maximum
        var result2 = await _settingsService.GetSettingsAsync();

        // Assert
        result1.Concurrency.Should().Be(1); // Clamped to minimum
        result2.Concurrency.Should().Be(16); // Clamped to maximum
    }

    [Fact]
    public async Task ValidateSettingsAsync_ShouldReturnErrors_ForInvalidSettings()
    {
        // Arrange
        var invalidSettings = new Settings
        {
            DefaultDownloadDir = "",
            Concurrency = 0,
            RetryPolicy = new RetryPolicy
            {
                MaxAttempts = 15, // Too high
                BaseDelayMs = 70000 // Too high
            },
            Appearance = new Appearance
            {
                AccentColor = "invalid-color"
            }
        };

        // Act
        var result = await _settingsService.ValidateSettingsAsync(invalidSettings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("Download directory"));
        result.Errors.Should().Contain(e => e.Contains("Concurrency"));
        result.Errors.Should().Contain(e => e.Contains("accent color"));
    }

    [Fact]
    public async Task ExportSettingsAsync_ShouldCreateValidJsonFile()
    {
        // Arrange
        var settings = new Settings
        {
            DefaultDownloadDir = "/test/downloads",
            Concurrency = 6,
            EnableScriptAutoDetection = true
        };

        _mockRepository.Setup(r => r.GetSettingsAsync()).ReturnsAsync(settings);

        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            var result = await _settingsService.ExportSettingsAsync(tempFile);

            // Assert
            result.Should().Be(tempFile);
            File.Exists(tempFile).Should().BeTrue();

            var json = await File.ReadAllTextAsync(tempFile);
            var exportedSettings = JsonSerializer.Deserialize<Settings>(json);

            exportedSettings.Should().NotBeNull();
            exportedSettings!.DefaultDownloadDir.Should().Be(settings.DefaultDownloadDir);
            exportedSettings.Concurrency.Should().Be(settings.Concurrency);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ImportSettingsAsync_ShouldReturnFalse_ForNonExistentFile()
    {
        // Arrange
        var nonExistentFile = "/path/that/does/not/exist.json";

        // Act
        var result = await _settingsService.ImportSettingsAsync(nonExistentFile);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ImportSettingsAsync_ShouldReturnFalse_ForInvalidJson()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "invalid json content");

        try
        {
            // Act
            var result = await _settingsService.ImportSettingsAsync(tempFile);

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            // Cleanup
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ImportSettingsAsync_ShouldReturnTrue_ForValidSettings()
    {
        // Arrange
        var validSettings = new Settings
        {
            DefaultDownloadDir = "/valid/path",
            Concurrency = 8,
            AutoResumeOnLaunch = false
        };

        var json = JsonSerializer.Serialize(validSettings, new JsonSerializerOptions { WriteIndented = true });
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, json);

        _mockRepository.Setup(r => r.SaveSettingsAsync(It.IsAny<Settings>())).Returns(Task.CompletedTask);

        try
        {
            // Act
            var result = await _settingsService.ImportSettingsAsync(tempFile);

            // Assert
            result.Should().BeTrue();
            _mockRepository.Verify(r => r.SaveSettingsAsync(It.Is<Settings>(s =>
                s.DefaultDownloadDir == validSettings.DefaultDownloadDir &&
                s.Concurrency == validSettings.Concurrency)), Times.Once);
        }
        finally
        {
            // Cleanup
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("#FF0000", true)]
    [InlineData("#123456", true)]
    [InlineData("FF0000", true)]
    [InlineData("#GGG000", false)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    public async Task ValidateSettingsAsync_ShouldValidateAccentColor(string accentColor, bool expectedValid)
    {
        // Arrange
        var settings = new Settings
        {
            DefaultDownloadDir = "/valid/path",
            Concurrency = 4,
            Appearance = new Appearance
            {
                AccentColor = accentColor
            }
        };

        // Act
        var result = await _settingsService.ValidateSettingsAsync(settings);

        // Assert
        if (expectedValid)
        {
            result.Errors.Should().NotContain(e => e.Contains("accent color", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            result.Errors.Should().Contain(e => e.Contains("accent color", StringComparison.OrdinalIgnoreCase));
        }
    }
}