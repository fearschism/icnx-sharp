using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ICNX.Core.Interfaces;
using ICNX.Core.Models;
using ICNX.Download;

namespace ICNX.Tests.Download;

public class DownloadSessionServiceTests
{
    private readonly Mock<IRepository<DownloadSession>> _mockSessionRepository;
    private readonly Mock<IRepository<DownloadItem>> _mockItemRepository;
    private readonly Mock<IDownloadEngine> _mockDownloadEngine;
    private readonly Mock<IEventAggregator> _mockEventAggregator;
    private readonly Mock<ILogger<DownloadSessionService>> _mockLogger;
    private readonly DownloadSessionService _downloadService;

    public DownloadSessionServiceTests()
    {
        _mockSessionRepository = new Mock<IRepository<DownloadSession>>();
        _mockItemRepository = new Mock<IRepository<DownloadItem>>();
        _mockDownloadEngine = new Mock<IDownloadEngine>();
        _mockEventAggregator = new Mock<IEventAggregator>();
        _mockLogger = new Mock<ILogger<DownloadSessionService>>();
        
        _downloadService = new DownloadSessionService(
            _mockSessionRepository.Object,
            _mockItemRepository.Object,
            _mockDownloadEngine.Object,
            _mockEventAggregator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task StartAsync_ShouldCreateSession_WithCorrectItemCount()
    {
        // Arrange
        var downloadRequests = new[]
        {
            new DownloadRequest { Url = "https://example.com/file1.zip", Filename = "file1.zip" },
            new DownloadRequest { Url = "https://example.com/file2.zip", Filename = "file2.zip" }
        };
        var destinationDir = "/downloads";
        
        _mockSessionRepository.Setup(r => r.AddAsync(It.IsAny<DownloadSession>()))
            .ReturnsAsync("session-id");
        _mockItemRepository.Setup(r => r.AddAsync(It.IsAny<DownloadItem>()))
            .ReturnsAsync("item-id");

        // Act
        var sessionId = await _downloadService.StartAsync(downloadRequests, destinationDir);

        // Assert
        sessionId.Should().NotBeNullOrEmpty();
        
        _mockSessionRepository.Verify(r => r.AddAsync(It.Is<DownloadSession>(s => 
            s.TotalCount == 2 && 
            s.Status == DownloadStatus.Queued)), Times.Once);
            
        _mockItemRepository.Verify(r => r.AddAsync(It.IsAny<DownloadItem>()), Times.Exactly(2));
    }

    [Fact]
    public async Task StartAsync_ShouldThrow_WhenNoDownloadRequests()
    {
        // Arrange
        var emptyRequests = Array.Empty<DownloadRequest>();
        var destinationDir = "/downloads";

        // Act & Assert
        var act = async () => await _downloadService.StartAsync(emptyRequests, destinationDir);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StartAsync_ShouldThrow_WhenInvalidDestinationDirectory()
    {
        // Arrange
        var downloadRequests = new[]
        {
            new DownloadRequest { Url = "https://example.com/file.zip", Filename = "file.zip" }
        };
        var invalidDir = "";

        // Act & Assert
        var act = async () => await _downloadService.StartAsync(downloadRequests, invalidDir);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PauseAsync_ShouldUpdateSessionStatus_ToPaused()
    {
        // Arrange
        var sessionId = "test-session";
        var session = new DownloadSession 
        { 
            Id = sessionId, 
            Status = DownloadStatus.Downloading 
        };
        
        _mockSessionRepository.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);
        _mockSessionRepository.Setup(r => r.UpdateAsync(It.IsAny<DownloadSession>()))
            .ReturnsAsync(true);

        // Act
        await _downloadService.PauseAsync(sessionId);

        // Assert
        _mockSessionRepository.Verify(r => r.UpdateAsync(It.Is<DownloadSession>(s => 
            s.Id == sessionId && s.Status == DownloadStatus.Paused)), Times.Once);
    }

    [Fact]
    public async Task ResumeAsync_ShouldUpdateSessionStatus_ToDownloading()
    {
        // Arrange
        var sessionId = "test-session";
        var session = new DownloadSession 
        { 
            Id = sessionId, 
            Status = DownloadStatus.Paused 
        };
        
        _mockSessionRepository.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);
        _mockSessionRepository.Setup(r => r.UpdateAsync(It.IsAny<DownloadSession>()))
            .ReturnsAsync(true);

        // Act
        await _downloadService.ResumeAsync(sessionId);

        // Assert
        _mockSessionRepository.Verify(r => r.UpdateAsync(It.Is<DownloadSession>(s => 
            s.Id == sessionId && s.Status == DownloadStatus.Downloading)), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ShouldUpdateSessionStatus_ToCancelled()
    {
        // Arrange
        var sessionId = "test-session";
        var session = new DownloadSession 
        { 
            Id = sessionId, 
            Status = DownloadStatus.Downloading 
        };
        
        _mockSessionRepository.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);
        _mockSessionRepository.Setup(r => r.UpdateAsync(It.IsAny<DownloadSession>()))
            .ReturnsAsync(true);

        // Act
        await _downloadService.CancelAsync(sessionId);

        // Assert
        _mockSessionRepository.Verify(r => r.UpdateAsync(It.Is<DownloadSession>(s => 
            s.Id == sessionId && s.Status == DownloadStatus.Cancelled)), Times.Once);
    }

    [Fact]
    public async Task GetRecentSessionsAsync_ShouldReturnOrderedSessions()
    {
        // Arrange
        var sessions = new[]
        {
            new DownloadSession { Id = "1", CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new DownloadSession { Id = "2", CreatedAt = DateTime.UtcNow.AddHours(-1) },
            new DownloadSession { Id = "3", CreatedAt = DateTime.UtcNow }
        };
        
        _mockSessionRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(sessions);

        // Act
        var result = await _downloadService.GetRecentSessionsAsync(10);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInDescendingOrder(s => s.CreatedAt);
        result.First().Id.Should().Be("3"); // Most recent first
    }

    [Fact]
    public async Task GetRecentSessionsAsync_ShouldLimitResults()
    {
        // Arrange
        var sessions = Enumerable.Range(1, 20)
            .Select(i => new DownloadSession 
            { 
                Id = i.ToString(), 
                CreatedAt = DateTime.UtcNow.AddHours(-i) 
            });
        
        _mockSessionRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(sessions);

        // Act
        var result = await _downloadService.GetRecentSessionsAsync(5);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task DeleteSessionAsync_ShouldRemoveSessionAndItems()
    {
        // Arrange
        var sessionId = "test-session";
        var session = new DownloadSession { Id = sessionId };
        var items = new[]
        {
            new DownloadItem { Id = "item1", SessionId = sessionId },
            new DownloadItem { Id = "item2", SessionId = sessionId }
        };
        
        _mockSessionRepository.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);
        _mockItemRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(items);
        _mockSessionRepository.Setup(r => r.DeleteAsync(sessionId))
            .ReturnsAsync(true);
        _mockItemRepository.Setup(r => r.DeleteAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        await _downloadService.DeleteSessionAsync(sessionId);

        // Assert
        _mockSessionRepository.Verify(r => r.DeleteAsync(sessionId), Times.Once);
        _mockItemRepository.Verify(r => r.DeleteAsync("item1"), Times.Once);
        _mockItemRepository.Verify(r => r.DeleteAsync("item2"), Times.Once);
    }

    [Theory]
    [InlineData(DownloadStatus.Completed, false)]
    [InlineData(DownloadStatus.Failed, false)]
    [InlineData(DownloadStatus.Cancelled, false)]
    [InlineData(DownloadStatus.Downloading, true)]
    [InlineData(DownloadStatus.Paused, true)]
    [InlineData(DownloadStatus.Queued, true)]
    public async Task PauseAsync_ShouldOnlyPauseActiveSessions(DownloadStatus status, bool shouldPause)
    {
        // Arrange
        var sessionId = "test-session";
        var session = new DownloadSession 
        { 
            Id = sessionId, 
            Status = status 
        };
        
        _mockSessionRepository.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);

        // Act
        if (shouldPause)
        {
            await _downloadService.PauseAsync(sessionId);
            
            // Assert
            _mockSessionRepository.Verify(r => r.UpdateAsync(It.IsAny<DownloadSession>()), Times.Once);
        }
        else
        {
            var act = async () => await _downloadService.PauseAsync(sessionId);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    [Fact]
    public async Task StartAsync_ShouldGenerateCorrectFilenames_WhenNotProvided()
    {
        // Arrange
        var downloadRequests = new[]
        {
            new DownloadRequest { Url = "https://example.com/downloads/file.zip" }, // No filename provided
            new DownloadRequest { Url = "https://example.com/api/download?id=123" } // No clear filename
        };
        var destinationDir = "/downloads";
        
        _mockSessionRepository.Setup(r => r.AddAsync(It.IsAny<DownloadSession>()))
            .ReturnsAsync("session-id");
        _mockItemRepository.Setup(r => r.AddAsync(It.IsAny<DownloadItem>()))
            .ReturnsAsync("item-id");

        // Act
        await _downloadService.StartAsync(downloadRequests, destinationDir);

        // Assert
        _mockItemRepository.Verify(r => r.AddAsync(It.Is<DownloadItem>(item => 
            !string.IsNullOrEmpty(item.Filename))), Times.Exactly(2));
    }
}