using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ICNX.Core.Interfaces;
using ICNX.Core.Models;
using ICNX.Core.Services;

namespace ICNX.Tests.Core.Services;

public class ProgressTrackerTests
{
    private readonly Mock<IEventAggregator> _mockEventAggregator;
    private readonly Mock<ILogger<ProgressTracker>> _mockLogger;
    private readonly ProgressTracker _progressTracker;

    public ProgressTrackerTests()
    {
        _mockEventAggregator = new Mock<IEventAggregator>();
        _mockLogger = new Mock<ILogger<ProgressTracker>>();
        _progressTracker = new ProgressTracker(_mockEventAggregator.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ReportProgressAsync_ShouldPublishProgressUpdate()
    {
        // Arrange
        var progress = new ProgressUpdate
        {
            SessionId = "session-1",
            ItemId = "item-1",
            Status = DownloadStatus.Downloading,
            DownloadedBytes = 1024,
            TotalBytes = 2048,
            SpeedBytesPerSec = 512
        };

        // Act
        await _progressTracker.ReportProgressAsync(progress);

        // Assert
        _mockEventAggregator.Verify(e => e.PublishAsync(progress), Times.Once);
    }

    [Fact]
    public async Task ReportProgressAsync_ShouldHandleNullProgress()
    {
        // Act & Assert
        var act = async () => await _progressTracker.ReportProgressAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReportProgressAsync_ShouldCalculateProgressPercentage()
    {
        // Arrange
        var progress = new ProgressUpdate
        {
            SessionId = "session-1",
            ItemId = "item-1",
            Status = DownloadStatus.Downloading,
            DownloadedBytes = 750,
            TotalBytes = 1000
        };

        // Act
        await _progressTracker.ReportProgressAsync(progress);

        // Assert
        _mockEventAggregator.Verify(e => e.PublishAsync(It.Is<ProgressUpdate>(p => 
            Math.Abs(p.ProgressPercentage - 75.0) < 0.01)), Times.Once);
    }

    [Fact]
    public async Task ReportProgressAsync_ShouldHandleUnknownTotalSize()
    {
        // Arrange
        var progress = new ProgressUpdate
        {
            SessionId = "session-1",
            ItemId = "item-1",
            Status = DownloadStatus.Downloading,
            DownloadedBytes = 1024,
            TotalBytes = null // Unknown size
        };

        // Act
        await _progressTracker.ReportProgressAsync(progress);

        // Assert
        _mockEventAggregator.Verify(e => e.PublishAsync(It.Is<ProgressUpdate>(p => 
            p.ProgressPercentage == 0.0)), Times.Once);
    }

    [Fact]
    public async Task ReportProgressAsync_ShouldEstimateTimeRemaining()
    {
        // Arrange
        var progress = new ProgressUpdate
        {
            SessionId = "session-1",
            ItemId = "item-1",
            Status = DownloadStatus.Downloading,
            DownloadedBytes = 500,
            TotalBytes = 1000,
            SpeedBytesPerSec = 100 // 100 bytes per second
        };

        // Act
        await _progressTracker.ReportProgressAsync(progress);

        // Assert
        _mockEventAggregator.Verify(e => e.PublishAsync(It.Is<ProgressUpdate>(p => 
            p.EstimatedTimeRemaining.HasValue && 
            p.EstimatedTimeRemaining.Value.TotalSeconds == 5)), Times.Once); // 500 remaining bytes / 100 bps = 5 seconds
    }

    [Fact]
    public async Task ReportProgressAsync_ShouldNotEstimateTime_WhenSpeedIsZero()
    {
        // Arrange
        var progress = new ProgressUpdate
        {
            SessionId = "session-1",
            ItemId = "item-1",
            Status = DownloadStatus.Downloading,
            DownloadedBytes = 500,
            TotalBytes = 1000,
            SpeedBytesPerSec = 0
        };

        // Act
        await _progressTracker.ReportProgressAsync(progress);

        // Assert
        _mockEventAggregator.Verify(e => e.PublishAsync(It.Is<ProgressUpdate>(p => 
            !p.EstimatedTimeRemaining.HasValue)), Times.Once);
    }

    [Theory]
    [InlineData(DownloadStatus.Completed, 100.0)]
    [InlineData(DownloadStatus.Failed, 0.0)]
    [InlineData(DownloadStatus.Cancelled, 0.0)]
    public async Task ReportProgressAsync_ShouldSetCorrectProgress_ForFinalStatuses(DownloadStatus status, double expectedProgress)
    {
        // Arrange
        var progress = new ProgressUpdate
        {
            SessionId = "session-1",
            ItemId = "item-1",
            Status = status,
            DownloadedBytes = 750,
            TotalBytes = 1000
        };

        // Act
        await _progressTracker.ReportProgressAsync(progress);

        // Assert
        _mockEventAggregator.Verify(e => e.PublishAsync(It.Is<ProgressUpdate>(p => 
            Math.Abs(p.ProgressPercentage - expectedProgress) < 0.01)), Times.Once);
    }

    [Fact]
    public async Task ReportProgressAsync_ShouldIncludeTimestamp()
    {
        // Arrange
        var beforeTime = DateTime.UtcNow;
        var progress = new ProgressUpdate
        {
            SessionId = "session-1",
            ItemId = "item-1",
            Status = DownloadStatus.Downloading
        };

        // Act
        await _progressTracker.ReportProgressAsync(progress);
        var afterTime = DateTime.UtcNow;

        // Assert
        _mockEventAggregator.Verify(e => e.PublishAsync(It.Is<ProgressUpdate>(p => 
            p.UpdatedAt >= beforeTime && p.UpdatedAt <= afterTime)), Times.Once);
    }

    [Fact]
    public async Task ReportProgressAsync_ShouldHandleVeryLargeFiles()
    {
        // Arrange - Test with files larger than int.MaxValue
        var progress = new ProgressUpdate
        {
            SessionId = "session-1",
            ItemId = "item-1",
            Status = DownloadStatus.Downloading,
            DownloadedBytes = 5_000_000_000L, // 5GB
            TotalBytes = 10_000_000_000L, // 10GB
            SpeedBytesPerSec = 100_000_000 // 100MB/s
        };

        // Act
        await _progressTracker.ReportProgressAsync(progress);

        // Assert
        _mockEventAggregator.Verify(e => e.PublishAsync(It.Is<ProgressUpdate>(p => 
            Math.Abs(p.ProgressPercentage - 50.0) < 0.01 && // 50% progress
            p.EstimatedTimeRemaining.HasValue &&
            p.EstimatedTimeRemaining.Value.TotalSeconds == 50)), Times.Once); // 5GB remaining / 100MB/s = 50s
    }

    [Fact]
    public async Task ReportProgressAsync_ShouldHandleZeroSizedFiles()
    {
        // Arrange
        var progress = new ProgressUpdate
        {
            SessionId = "session-1",
            ItemId = "item-1",
            Status = DownloadStatus.Completed,
            DownloadedBytes = 0,
            TotalBytes = 0
        };

        // Act
        await _progressTracker.ReportProgressAsync(progress);

        // Assert
        _mockEventAggregator.Verify(e => e.PublishAsync(It.Is<ProgressUpdate>(p => 
            p.ProgressPercentage == 100.0)), Times.Once);
    }

    [Fact]
    public async Task ReportProgressAsync_ShouldValidateSessionId()
    {
        // Arrange
        var progress = new ProgressUpdate
        {
            SessionId = "", // Invalid empty session ID
            ItemId = "item-1",
            Status = DownloadStatus.Downloading
        };

        // Act & Assert
        var act = async () => await _progressTracker.ReportProgressAsync(progress);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReportProgressAsync_ShouldValidateItemId()
    {
        // Arrange
        var progress = new ProgressUpdate
        {
            SessionId = "session-1",
            ItemId = "", // Invalid empty item ID
            Status = DownloadStatus.Downloading
        };

        // Act & Assert
        var act = async () => await _progressTracker.ReportProgressAsync(progress);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}