using System.Reflection;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ICNX.Persistence.Migrations;

/// <summary>
/// Manages database migrations for SQLite
/// </summary>
public class MigrationRunner
{
    private readonly string _connectionString;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(string connectionString, ILogger<MigrationRunner> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Run all pending migrations
    /// </summary>
    public async Task RunMigrationsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Create migrations table if it doesn't exist
        await CreateMigrationsTableAsync(connection);

        // Get applied migrations
        var appliedMigrations = await GetAppliedMigrationsAsync(connection);
        
        // Get available migrations
        var availableMigrations = GetAvailableMigrations();

        // Run pending migrations
        foreach (var migration in availableMigrations.Where(m => !appliedMigrations.Contains(m.Version)))
        {
            _logger.LogInformation("Applying migration {Version}: {Name}", migration.Version, migration.Name);
            
            using var transaction = connection.BeginTransaction();
            try
            {
                await migration.UpgradeAsync(connection, transaction);
                await RecordMigrationAsync(connection, transaction, migration.Version, migration.Name);
                transaction.Commit();
                
                _logger.LogInformation("Migration {Version} applied successfully", migration.Version);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Failed to apply migration {Version}", migration.Version);
                throw;
            }
        }
    }

    private async Task CreateMigrationsTableAsync(SqliteConnection connection)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS migrations (
                version INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                applied_at TEXT NOT NULL
            );";
        
        await connection.ExecuteAsync(sql);
    }

    private async Task<HashSet<int>> GetAppliedMigrationsAsync(SqliteConnection connection)
    {
        const string sql = "SELECT version FROM migrations ORDER BY version";
        var versions = await connection.QueryAsync<int>(sql);
        return versions.ToHashSet();
    }

    private async Task RecordMigrationAsync(SqliteConnection connection, SqliteTransaction transaction, 
        int version, string name)
    {
        const string sql = @"
            INSERT INTO migrations (version, name, applied_at) 
            VALUES (@Version, @Name, @AppliedAt)";
        
        await connection.ExecuteAsync(sql, new 
        { 
            Version = version, 
            Name = name, 
            AppliedAt = DateTime.UtcNow.ToString("O") 
        }, transaction);
    }

    private List<IMigration> GetAvailableMigrations()
    {
        var migrations = new List<IMigration>();
        var assembly = Assembly.GetExecutingAssembly();
        
        var migrationTypes = assembly.GetTypes()
            .Where(t => typeof(IMigration).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .OrderBy(t => t.Name);

        foreach (var type in migrationTypes)
        {
            if (Activator.CreateInstance(type) is IMigration migration)
            {
                migrations.Add(migration);
            }
        }

        return migrations.OrderBy(m => m.Version).ToList();
    }
}

/// <summary>
/// Migration interface
/// </summary>
public interface IMigration
{
    int Version { get; }
    string Name { get; }
    Task UpgradeAsync(SqliteConnection connection, SqliteTransaction transaction);
}