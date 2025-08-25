using Dapper;
using ICNX.Core.Models;
using Microsoft.Extensions.Logging;

namespace ICNX.Persistence.Repositories;

/// <summary>
/// Repository for download items
/// </summary>
public class DownloadItemRepository : BaseRepository<DownloadItem>
{
    public DownloadItemRepository(string connectionString, ILogger<DownloadItemRepository> logger) 
        : base(connectionString, logger)
    {
    }

    protected override string GetTableName() => "download_items";

    public override async Task<DownloadItem?> GetByIdAsync(string id)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                SELECT id, session_id, url, filename, status, mime, total_bytes, 
                       downloaded_bytes, error, started_at, completed_at, retry_attempt
                FROM download_items 
                WHERE id = @Id";
            
            var result = await connection.QueryFirstOrDefaultAsync(sql, new { Id = id });
            return MapFromDb(result);
        });
    }

    public override async Task<IEnumerable<DownloadItem>> GetAllAsync()
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                SELECT id, session_id, url, filename, status, mime, total_bytes, 
                       downloaded_bytes, error, started_at, completed_at, retry_attempt
                FROM download_items 
                ORDER BY started_at DESC";
            
            var results = await connection.QueryAsync(sql);
            return results.Select(MapFromDb).Where(i => i != null).Cast<DownloadItem>();
        });
    }

    public override async Task<string> AddAsync(DownloadItem item)
    {
        await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                INSERT INTO download_items 
                (id, session_id, url, filename, status, mime, total_bytes, 
                 downloaded_bytes, error, started_at, completed_at, retry_attempt)
                VALUES 
                (@Id, @SessionId, @Url, @Filename, @Status, @Mime, @TotalBytes,
                 @DownloadedBytes, @Error, @StartedAt, @CompletedAt, @RetryAttempt)";
            
            await connection.ExecuteAsync(sql, MapToDb(item));
        });
        
        return item.Id;
    }

    public override async Task<bool> UpdateAsync(DownloadItem item)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                UPDATE download_items 
                SET status = @Status, mime = @Mime, total_bytes = @TotalBytes,
                    downloaded_bytes = @DownloadedBytes, error = @Error,
                    started_at = @StartedAt, completed_at = @CompletedAt,
                    retry_attempt = @RetryAttempt
                WHERE id = @Id";
            
            var rowsAffected = await connection.ExecuteAsync(sql, MapToDb(item));
            return rowsAffected > 0;
        });
    }

    public override async Task<bool> DeleteAsync(string id)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = "DELETE FROM download_items WHERE id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        });
    }

    /// <summary>
    /// Get all items for a specific session
    /// </summary>
    public async Task<IEnumerable<DownloadItem>> GetBySessionIdAsync(string sessionId)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                SELECT id, session_id, url, filename, status, mime, total_bytes, 
                       downloaded_bytes, error, started_at, completed_at, retry_attempt
                FROM download_items 
                WHERE session_id = @SessionId
                ORDER BY started_at";
            
            var results = await connection.QueryAsync(sql, new { SessionId = sessionId });
            return results.Select(MapFromDb).Where(i => i != null).Cast<DownloadItem>();
        });
    }

    /// <summary>
    /// Batch add multiple items for performance
    /// </summary>
    public async Task<int> BatchAddAsync(IEnumerable<DownloadItem> items)
    {
        const string sql = @"
            INSERT INTO download_items 
            (id, session_id, url, filename, status, mime, total_bytes, 
             downloaded_bytes, error, started_at, completed_at, retry_attempt)
            VALUES 
            (@Id, @SessionId, @Url, @Filename, @Status, @Mime, @TotalBytes,
             @DownloadedBytes, @Error, @StartedAt, @CompletedAt, @RetryAttempt)";

        return await BatchInsertAsync(items, sql, MapToDb);
    }

    /// <summary>
    /// Update progress for an item
    /// </summary>
    public async Task<bool> UpdateProgressAsync(string itemId, DownloadStatus status, 
        long downloadedBytes, string? error = null)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                UPDATE download_items 
                SET status = @Status, 
                    downloaded_bytes = @DownloadedBytes,
                    error = @Error,
                    completed_at = CASE 
                        WHEN @Status IN (@Completed, @Failed, @Cancelled) 
                        THEN @CompletedAt 
                        ELSE completed_at 
                    END
                WHERE id = @Id";
            
            var rowsAffected = await connection.ExecuteAsync(sql, new 
            {
                Id = itemId,
                Status = (int)status,
                DownloadedBytes = downloadedBytes,
                Error = error,
                Completed = (int)DownloadStatus.Completed,
                Failed = (int)DownloadStatus.Failed,
                Cancelled = (int)DownloadStatus.Cancelled,
                CompletedAt = DateTime.UtcNow.ToString("O")
            });
            
            return rowsAffected > 0;
        });
    }

    /// <summary>
    /// Increment retry attempt for an item
    /// </summary>
    public async Task<bool> IncrementRetryAttemptAsync(string itemId)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                UPDATE download_items 
                SET retry_attempt = retry_attempt + 1
                WHERE id = @Id";
            
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = itemId });
            return rowsAffected > 0;
        });
    }

    /// <summary>
    /// Reset retry attempt for an item (when progress is made)
    /// </summary>
    public async Task<bool> ResetRetryAttemptAsync(string itemId)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                UPDATE download_items 
                SET retry_attempt = 0
                WHERE id = @Id";
            
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = itemId });
            return rowsAffected > 0;
        });
    }

    /// <summary>
    /// Get session status counts
    /// </summary>
    public async Task<(int completed, int failed, int cancelled, int total)> GetSessionStatusCountsAsync(string sessionId)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                SELECT 
                    SUM(CASE WHEN status = @Completed THEN 1 ELSE 0 END) as completed,
                    SUM(CASE WHEN status = @Failed THEN 1 ELSE 0 END) as failed,
                    SUM(CASE WHEN status = @Cancelled THEN 1 ELSE 0 END) as cancelled,
                    COUNT(*) as total
                FROM download_items 
                WHERE session_id = @SessionId";
            
            var result = await connection.QueryFirstAsync(sql, new 
            {
                SessionId = sessionId,
                Completed = (int)DownloadStatus.Completed,
                Failed = (int)DownloadStatus.Failed,
                Cancelled = (int)DownloadStatus.Cancelled
            });
            
            return ((int)result.completed, (int)result.failed, (int)result.cancelled, (int)result.total);
        });
    }

    private static DownloadItem? MapFromDb(dynamic? row)
    {
        if (row == null) return null;

        return new DownloadItem
        {
            Id = row.id,
            SessionId = row.session_id,
            Url = row.url,
            Filename = row.filename,
            Status = (DownloadStatus)row.status,
            Mime = row.mime,
            TotalBytes = row.total_bytes,
            DownloadedBytes = row.downloaded_bytes ?? 0,
            Error = row.error,
            StartedAt = row.started_at != null ? DateTime.Parse(row.started_at) : null,
            CompletedAt = row.completed_at != null ? DateTime.Parse(row.completed_at) : null,
            RetryAttempt = (int)(row.retry_attempt ?? 0)
        };
    }

    private static object MapToDb(DownloadItem item)
    {
        return new
        {
            Id = item.Id,
            SessionId = item.SessionId,
            Url = item.Url,
            Filename = item.Filename,
            Status = (int)item.Status,
            Mime = item.Mime,
            TotalBytes = item.TotalBytes,
            DownloadedBytes = item.DownloadedBytes,
            Error = item.Error,
            StartedAt = item.StartedAt?.ToString("O"),
            CompletedAt = item.CompletedAt?.ToString("O"),
            RetryAttempt = item.RetryAttempt
        };
    }
}