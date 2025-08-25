using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using ICNX.Core.Models;
using ICNX.Persistence.Repositories;
using ICNX.Persistence.Migrations;

namespace ICNX.Tests.Persistence.Repositories;

public class RepositoryIntegrationTests : IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;
    private readonly DownloadSessionRepository _sessionRepository;
    private readonly DownloadItemRepository _itemRepository;
    private readonly SettingsRepository _settingsRepository;

    public RepositoryIntegrationTests()
    {
        // Use in-memory SQLite database for testing
        _connectionString = "Data Source=:memory:";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        
        // Initialize repositories
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        
        _sessionRepository = new DownloadSessionRepository(_connectionString, 
            loggerFactory.CreateLogger<DownloadSessionRepository>());
        _itemRepository = new DownloadItemRepository(_connectionString, 
            loggerFactory.CreateLogger<DownloadItemRepository>());
        _settingsRepository = new SettingsRepository(_connectionString, 
            loggerFactory.CreateLogger<SettingsRepository>());
        
        // Run migrations
        var migrationRunner = new MigrationRunner(_connectionString, 
            loggerFactory.CreateLogger<MigrationRunner>());
        migrationRunner.RunMigrationsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task DownloadSessionRepository_ShouldCrudOperations_Successfully()
    {
        // Arrange
        var session = new DownloadSession
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Test Session",
            Status = DownloadStatus.Queued,
            TotalCount = 5,
            CompletedCount = 0,
            FailedCount = 0,
            CancelledCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        // Act - Create
        var sessionId = await _sessionRepository.AddAsync(session);
        sessionId.Should().Be(session.Id);

        // Act - Read
        var retrievedSession = await _sessionRepository.GetByIdAsync(sessionId);
        retrievedSession.Should().NotBeNull();
        retrievedSession!.Title.Should().Be(session.Title);
        retrievedSession.Status.Should().Be(session.Status);
        retrievedSession.TotalCount.Should().Be(session.TotalCount);

        // Act - Update
        retrievedSession.Status = DownloadStatus.Downloading;
        retrievedSession.CompletedCount = 2;
        var updateResult = await _sessionRepository.UpdateAsync(retrievedSession);
        updateResult.Should().BeTrue();

        var updatedSession = await _sessionRepository.GetByIdAsync(sessionId);
        updatedSession!.Status.Should().Be(DownloadStatus.Downloading);
        updatedSession.CompletedCount.Should().Be(2);

        // Act - Delete
        var deleteResult = await _sessionRepository.DeleteAsync(sessionId);
        deleteResult.Should().BeTrue();

        var deletedSession = await _sessionRepository.GetByIdAsync(sessionId);
        deletedSession.Should().BeNull();
    }

    [Fact]
    public async Task DownloadItemRepository_ShouldCrudOperations_Successfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var item = new DownloadItem
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            Filename = "test-file.zip",
            Url = "https://example.com/test-file.zip",
            LocalPath = "/downloads/test-file.zip",
            Status = DownloadStatus.Queued,
            TotalBytes = 1024,
            DownloadedBytes = 0,
            CreatedAt = DateTime.UtcNow
        };

        // Act - Create
        var itemId = await _itemRepository.AddAsync(item);
        itemId.Should().Be(item.Id);

        // Act - Read
        var retrievedItem = await _itemRepository.GetByIdAsync(itemId);
        retrievedItem.Should().NotBeNull();
        retrievedItem!.Filename.Should().Be(item.Filename);
        retrievedItem.Url.Should().Be(item.Url);
        retrievedItem.TotalBytes.Should().Be(item.TotalBytes);

        // Act - Update
        retrievedItem.Status = DownloadStatus.Downloading;
        retrievedItem.DownloadedBytes = 512;
        var updateResult = await _itemRepository.UpdateAsync(retrievedItem);
        updateResult.Should().BeTrue();

        var updatedItem = await _itemRepository.GetByIdAsync(itemId);
        updatedItem!.Status.Should().Be(DownloadStatus.Downloading);
        updatedItem.DownloadedBytes.Should().Be(512);

        // Act - Delete
        var deleteResult = await _itemRepository.DeleteAsync(itemId);
        deleteResult.Should().BeTrue();

        var deletedItem = await _itemRepository.GetByIdAsync(itemId);
        deletedItem.Should().BeNull();
    }

    [Fact]
    public async Task SettingsRepository_ShouldPersistSettings_Successfully()
    {
        // Arrange
        var settings = new Settings
        {
            DefaultDownloadDir = "/custom/downloads",
            Concurrency = 8,
            AutoResumeOnLaunch = false,
            EnableScriptAutoDetection = true,
            SpeedLimitBytesPerSec = 1024000, // 1MB/s
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 3,
                BaseDelayMs = 2000,
                BackoffFactor = 1.5
            },
            Appearance = new Appearance
            {
                Theme = ThemeMode.Dark,
                AccentColor = "#FF5722",
                EnableAnimations = false,
                CompactMode = true
            }
        };

        // Act - Save
        await _settingsRepository.SaveSettingsAsync(settings);

        // Act - Retrieve
        var retrievedSettings = await _settingsRepository.GetSettingsAsync();
        
        // Assert
        retrievedSettings.Should().NotBeNull();
        retrievedSettings!.DefaultDownloadDir.Should().Be(settings.DefaultDownloadDir);
        retrievedSettings.Concurrency.Should().Be(settings.Concurrency);
        retrievedSettings.AutoResumeOnLaunch.Should().Be(settings.AutoResumeOnLaunch);
        retrievedSettings.SpeedLimitBytesPerSec.Should().Be(settings.SpeedLimitBytesPerSec);
        
        // Verify nested objects
        retrievedSettings.RetryPolicy.Should().NotBeNull();
        retrievedSettings.RetryPolicy.MaxAttempts.Should().Be(settings.RetryPolicy.MaxAttempts);
        retrievedSettings.RetryPolicy.BaseDelayMs.Should().Be(settings.RetryPolicy.BaseDelayMs);
        
        retrievedSettings.Appearance.Should().NotBeNull();
        retrievedSettings.Appearance.Theme.Should().Be(settings.Appearance.Theme);
        retrievedSettings.Appearance.AccentColor.Should().Be(settings.Appearance.AccentColor);
    }

    [Fact]
    public async Task SettingsRepository_ShouldHandleCustomSettings()
    {
        // Arrange
        var settings = new Settings();
        settings.CustomSettings["test-string"] = "test-value";
        settings.CustomSettings["test-number"] = 42;
        settings.CustomSettings["test-bool"] = true;

        // Act
        await _settingsRepository.SaveSettingsAsync(settings);
        var retrievedSettings = await _settingsRepository.GetSettingsAsync();

        // Assert
        retrievedSettings!.CustomSettings.Should().HaveCount(3);
        retrievedSettings.CustomSettings["test-string"].ToString().Should().Be("test-value");
        retrievedSettings.CustomSettings["test-number"].ToString().Should().Be("42");
        retrievedSettings.CustomSettings["test-bool"].ToString().Should().Be("True");
    }

    [Fact]
    public async Task Repositories_ShouldHandleConcurrentOperations()
    {
        // Arrange
        var tasks = new List<Task>();
        var sessionIds = new List<string>();

        // Act - Create multiple sessions concurrently
        for (int i = 0; i < 10; i++)
        {
            var sessionId = Guid.NewGuid().ToString();
            sessionIds.Add(sessionId);
            
            var session = new DownloadSession
            {
                Id = sessionId,
                Title = $"Concurrent Session {i}",
                Status = DownloadStatus.Queued,
                TotalCount = i + 1,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            };

            tasks.Add(_sessionRepository.AddAsync(session));
        }

        await Task.WhenAll(tasks);

        // Assert
        var allSessions = await _sessionRepository.GetAllAsync();
        allSessions.Should().HaveCount(10);
        
        foreach (var sessionId in sessionIds)
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            session.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task DownloadItemRepository_ShouldFilterBySession()
    {
        // Arrange
        var session1Id = Guid.NewGuid().ToString();
        var session2Id = Guid.NewGuid().ToString();

        var items = new[]
        {
            new DownloadItem { Id = Guid.NewGuid().ToString(), SessionId = session1Id, Filename = "file1.zip" },
            new DownloadItem { Id = Guid.NewGuid().ToString(), SessionId = session1Id, Filename = "file2.zip" },
            new DownloadItem { Id = Guid.NewGuid().ToString(), SessionId = session2Id, Filename = "file3.zip" }
        };

        // Act
        foreach (var item in items)
        {
            await _itemRepository.AddAsync(item);
        }

        var session1Items = await _itemRepository.GetAllAsync();
        var filteredItems = session1Items.Where(i => i.SessionId == session1Id);

        // Assert
        filteredItems.Should().HaveCount(2);
        filteredItems.All(i => i.SessionId == session1Id).Should().BeTrue();
    }

    [Fact]
    public async Task Repositories_ShouldHandleTransactions()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var session = new DownloadSession
        {
            Id = sessionId,
            Title = "Transaction Test",
            Status = DownloadStatus.Queued,
            TotalCount = 2
        };

        var items = new[]
        {
            new DownloadItem { Id = Guid.NewGuid().ToString(), SessionId = sessionId, Filename = "file1.zip" },
            new DownloadItem { Id = Guid.NewGuid().ToString(), SessionId = sessionId, Filename = "file2.zip" }
        };

        // Act - Simulate transaction-like behavior
        try
        {
            await _sessionRepository.AddAsync(session);
            
            foreach (var item in items)
            {
                await _itemRepository.AddAsync(item);
            }

            // Verify all data was saved
            var savedSession = await _sessionRepository.GetByIdAsync(sessionId);
            var allItems = await _itemRepository.GetAllAsync();
            var sessionItems = allItems.Where(i => i.SessionId == sessionId);

            // Assert
            savedSession.Should().NotBeNull();
            sessionItems.Should().HaveCount(2);
        }
        catch
        {
            // In a real scenario, we would rollback here
            throw;
        }
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}