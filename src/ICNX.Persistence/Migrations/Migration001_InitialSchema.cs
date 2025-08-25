using Dapper;
using Microsoft.Data.Sqlite;

namespace ICNX.Persistence.Migrations;

/// <summary>
/// Initial migration to create all core tables
/// </summary>
public class Migration001_InitialSchema : IMigration
{
    public int Version => 1;
    public string Name => "Initial Schema";

    public async Task UpgradeAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Download Sessions table
        await connection.ExecuteAsync(@"
            CREATE TABLE download_sessions (
                id TEXT PRIMARY KEY,
                created_at TEXT NOT NULL,
                title TEXT NOT NULL,
                status INTEGER NOT NULL,
                total_bytes INTEGER,
                completed_count INTEGER NOT NULL DEFAULT 0,
                failed_count INTEGER NOT NULL DEFAULT 0,
                cancelled_count INTEGER NOT NULL DEFAULT 0,
                total_count INTEGER NOT NULL DEFAULT 0,
                completed_at TEXT
            );", transaction: transaction);

        // Download Items table
        await connection.ExecuteAsync(@"
            CREATE TABLE download_items (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                url TEXT NOT NULL,
                filename TEXT NOT NULL,
                status INTEGER NOT NULL,
                mime TEXT,
                total_bytes INTEGER,
                downloaded_bytes INTEGER NOT NULL DEFAULT 0,
                error TEXT,
                started_at TEXT,
                completed_at TEXT,
                retry_attempt INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (session_id) REFERENCES download_sessions (id) ON DELETE CASCADE
            );", transaction: transaction);

        // Scripts table
        await connection.ExecuteAsync(@"
            CREATE TABLE scripts (
                name TEXT PRIMARY KEY,
                version TEXT NOT NULL,
                description TEXT NOT NULL,
                pattern TEXT NOT NULL,
                created_at TEXT NOT NULL,
                file_path TEXT NOT NULL,
                is_enabled INTEGER NOT NULL DEFAULT 1
            );", transaction: transaction);

        // Scrape Results table
        await connection.ExecuteAsync(@"
            CREATE TABLE scrape_results (
                id TEXT PRIMARY KEY,
                script_name TEXT NOT NULL,
                url TEXT NOT NULL,
                title TEXT,
                data_json TEXT NOT NULL,
                created_at TEXT NOT NULL,
                is_success INTEGER NOT NULL,
                error_message TEXT,
                FOREIGN KEY (script_name) REFERENCES scripts (name)
            );", transaction: transaction);

        // Settings table (single row configuration)
        await connection.ExecuteAsync(@"
            CREATE TABLE settings (
                id TEXT PRIMARY KEY DEFAULT 'main',
                settings_json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );", transaction: transaction);

        // App Info table
        await connection.ExecuteAsync(@"
            CREATE TABLE app_info (
                id TEXT PRIMARY KEY DEFAULT 'main',
                version TEXT NOT NULL,
                last_launch TEXT NOT NULL,
                launch_count INTEGER NOT NULL DEFAULT 0,
                first_run INTEGER NOT NULL DEFAULT 1,
                installation_id TEXT NOT NULL
            );", transaction: transaction);

        // Create indexes for performance
        await connection.ExecuteAsync(@"
            CREATE INDEX idx_download_items_session_id ON download_items (session_id);
            CREATE INDEX idx_download_items_status ON download_items (status);
            CREATE INDEX idx_download_sessions_created_at ON download_sessions (created_at);
            CREATE INDEX idx_download_sessions_status ON download_sessions (status);
            CREATE INDEX idx_scrape_results_script_name ON scrape_results (script_name);
            CREATE INDEX idx_scrape_results_created_at ON scrape_results (created_at);
        ", transaction: transaction);

        // Insert default settings
        await connection.ExecuteAsync(@"
            INSERT INTO settings (id, settings_json, updated_at) 
            VALUES ('main', @SettingsJson, @UpdatedAt);
        ", new 
        {
            SettingsJson = "{\"DefaultDownloadDir\":\"\",\"Concurrency\":4,\"RetryLimit\":3,\"AutoResumeOnLaunch\":true,\"RetryPolicy\":{\"Enabled\":true,\"MaxAttempts\":5,\"BaseDelayMs\":1000,\"BackoffFactor\":2.0,\"JitterPercent\":0.2,\"PerItemResetOnProgress\":true,\"RetryStatusCodes\":[408,429,500,502,503,504]},\"Appearance\":{\"EnableAnimations\":true,\"ReducedEffects\":false,\"CompactMode\":false,\"EnableProgressSheen\":true,\"EnableGlass\":true,\"EnableBreathing\":true,\"EnableProgressHalo\":false,\"Theme\":1,\"AccentColor\":\"#3b82f6\"},\"EnableScriptAutoDetection\":true,\"CustomSettings\":{}}",
            UpdatedAt = DateTime.UtcNow.ToString("O")
        }, transaction: transaction);

        // Insert default app info
        await connection.ExecuteAsync(@"
            INSERT INTO app_info (id, version, last_launch, launch_count, first_run, installation_id)
            VALUES ('main', @Version, @LastLaunch, 0, 1, @InstallationId);
        ", new
        {
            Version = "1.0.0",
            LastLaunch = DateTime.UtcNow.ToString("O"),
            InstallationId = Guid.NewGuid().ToString()
        }, transaction: transaction);
    }
}