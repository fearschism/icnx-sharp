using FluentAssertions;
using ICNX.Core.Models;

namespace ICNX.Tests.Core.Models;

public class RetryPolicyTests
{
    [Fact]
    public void CalculateDelay_ShouldReturnZero_WhenDisabled()
    {
        // Arrange
        var retryPolicy = new RetryPolicy { Enabled = false };

        // Act
        var delay = retryPolicy.CalculateDelay(1);

        // Assert
        delay.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(1, 1000, 2.0, 1000)]
    [InlineData(2, 1000, 2.0, 2000)]
    [InlineData(3, 1000, 2.0, 4000)]
    public void CalculateDelay_ShouldUseExponentialBackoff(int attempt, int baseDelayMs, double backoffFactor, int expectedBaseMs)
    {
        // Arrange
        var retryPolicy = new RetryPolicy
        {
            Enabled = true,
            BaseDelayMs = baseDelayMs,
            BackoffFactor = backoffFactor,
            JitterPercent = 0
        };

        // Act
        var delay = retryPolicy.CalculateDelay(attempt);

        // Assert
        delay.TotalMilliseconds.Should().BeApproximately(expectedBaseMs, 1);
    }

    [Theory]
    [InlineData(408, true)]
    [InlineData(500, true)]
    [InlineData(200, false)]
    [InlineData(404, false)]
    public void ShouldRetry_ShouldReturnCorrectResult_ForStatusCodes(int statusCode, bool expectedRetry)
    {
        // Arrange
        var retryPolicy = new RetryPolicy { Enabled = true };

        // Act
        var shouldRetry = retryPolicy.ShouldRetry(statusCode);

        // Assert
        shouldRetry.Should().Be(expectedRetry);
    }
}

public class SettingsTests
{
    [Fact]
    public void Settings_ShouldInitialize_WithDefaults()
    {
        // Act
        var settings = new Settings();

        // Assert
        settings.Concurrency.Should().Be(4);
        settings.AutoResumeOnLaunch.Should().BeTrue();
        settings.RetryPolicy.Should().NotBeNull();
        settings.Appearance.Should().NotBeNull();
    }
}