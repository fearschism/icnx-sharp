using ICNX.Persistence.Migrations;
using Microsoft.Extensions.Logging;

namespace ICNX.Persistence;

/// <summary>
/// Factory for creating and managing database connections
/// </summary>
public class DatabaseConnectionFactory
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseConnectionFactory> _logger;
    private bool _initialized = false;

    public DatabaseConnectionFactory(string connectionString, ILogger<DatabaseConnectionFactory> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Get the connection string
    /// </summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Initialize the database (run migrations)
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            _logger.LogInformation("Initializing database...");

            // Ensure directory exists
            var dbFile = GetDatabaseFilePath(_connectionString);
            var directory = Path.GetDirectoryName(dbFile);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInformation("Created database directory: {Directory}", directory);
            }

            // Run migrations
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var migrationLogger = loggerFactory.CreateLogger<MigrationRunner>();
            var migrationRunner = new MigrationRunner(_connectionString, migrationLogger);
            await migrationRunner.RunMigrationsAsync();

            _initialized = true;
            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }

    /// <summary>
    /// Create a connection string for SQLite
    /// </summary>
    public static string CreateConnectionString(string databasePath)
    {
        return $"Data Source={databasePath};Cache=Shared;";
    }

    /// <summary>
    /// Create a connection string for in-memory SQLite (for testing)
    /// </summary>
    public static string CreateInMemoryConnectionString()
    {
        return "Data Source=:memory:;Cache=Shared;";
    }

    /// <summary>
    /// Get the default database file path
    /// </summary>
    public static string GetDefaultDatabasePath()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ICNX");
        
        return Path.Combine(appDataDir, "icnx.db");
    }

    /// <summary>
    /// Extract database file path from connection string
    /// </summary>
    private static string GetDatabaseFilePath(string connectionString)
    {
        // Simple extraction - in production you might want more robust parsing
        var parts = connectionString.Split(';');
        var dataSourcePart = parts.FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));
        
        if (dataSourcePart != null)
        {
            return dataSourcePart.Substring("Data Source=".Length);
        }

        throw new InvalidOperationException("Could not extract database path from connection string");
    }

    /// <summary>
    /// Check if database file exists
    /// </summary>
    public bool DatabaseExists()
    {
        try
        {
            var dbFile = GetDatabaseFilePath(_connectionString);
            return File.Exists(dbFile);
        }
        catch
        {
            return false; // In-memory or invalid connection string
        }
    }

    /// <summary>
    /// Get database file size in bytes
    /// </summary>
    public long GetDatabaseSize()
    {
        try
        {
            var dbFile = GetDatabaseFilePath(_connectionString);
            return File.Exists(dbFile) ? new FileInfo(dbFile).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Backup database to specified path
    /// </summary>
    public async Task BackupDatabaseAsync(string backupPath)
    {
        try
        {
            var dbFile = GetDatabaseFilePath(_connectionString);
            if (File.Exists(dbFile))
            {
                var backupDir = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                await Task.Run(() => File.Copy(dbFile, backupPath, true));
                _logger.LogInformation("Database backed up to: {BackupPath}", backupPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backup database to {BackupPath}", backupPath);
            throw;
        }
    }
}
