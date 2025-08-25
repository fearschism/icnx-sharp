using Dapper;
using ICNX.Core.Models;
using Microsoft.Extensions.Logging;

namespace ICNX.Persistence.Repositories;

/// <summary>
/// Repository for download sessions
/// </summary>
public class DownloadSessionRepository : BaseRepository<DownloadSession>
{
    public DownloadSessionRepository(string connectionString, ILogger<DownloadSessionRepository> logger) 
        : base(connectionString, logger)
    {
    }

    protected override string GetTableName() => "download_sessions";

    public override async Task<DownloadSession?> GetByIdAsync(string id)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                SELECT id, created_at, title, status, total_bytes, 
                       completed_count, failed_count, cancelled_count, 
                       total_count, completed_at
                FROM download_sessions 
                WHERE id = @Id";
            
            var result = await connection.QueryFirstOrDefaultAsync(sql, new { Id = id });
            return MapFromDb(result);
        });
    }

    public override async Task<IEnumerable<DownloadSession>> GetAllAsync()
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                SELECT id, created_at, title, status, total_bytes, 
                       completed_count, failed_count, cancelled_count, 
                       total_count, completed_at
                FROM download_sessions 
                ORDER BY created_at DESC";
            
            var results = await connection.QueryAsync(sql);
            return results.Select(MapFromDb).Where(s => s != null).Cast<DownloadSession>();
        });
    }

    public override async Task<string> AddAsync(DownloadSession session)
    {
        await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                INSERT INTO download_sessions 
                (id, created_at, title, status, total_bytes, completed_count, 
                 failed_count, cancelled_count, total_count, completed_at)
                VALUES 
                (@Id, @CreatedAt, @Title, @Status, @TotalBytes, @CompletedCount,
                 @FailedCount, @CancelledCount, @TotalCount, @CompletedAt)";
            
            await connection.ExecuteAsync(sql, MapToDb(session));
        });
        
        return session.Id;
    }

    public override async Task<bool> UpdateAsync(DownloadSession session)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                UPDATE download_sessions 
                SET title = @Title, status = @Status, total_bytes = @TotalBytes,
                    completed_count = @CompletedCount, failed_count = @FailedCount,
                    cancelled_count = @CancelledCount, total_count = @TotalCount,
                    completed_at = @CompletedAt
                WHERE id = @Id";
            
            var rowsAffected = await connection.ExecuteAsync(sql, MapToDb(session));
            return rowsAffected > 0;
        });
    }

    public override async Task<bool> DeleteAsync(string id)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = "DELETE FROM download_sessions WHERE id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        });
    }

    /// <summary>
    /// Get recent sessions with limit
    /// </summary>
    public async Task<IEnumerable<DownloadSession>> GetRecentAsync(int limit = 50)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                SELECT id, created_at, title, status, total_bytes, 
                       completed_count, failed_count, cancelled_count, 
                       total_count, completed_at
                FROM download_sessions 
                ORDER BY created_at DESC 
                LIMIT @Limit";
            
            var results = await connection.QueryAsync(sql, new { Limit = limit });
            return results.Select(MapFromDb).Where(s => s != null).Cast<DownloadSession>();
        });
    }

    /// <summary>
    /// Get active sessions (not completed, failed, or cancelled)
    /// </summary>
    public async Task<IEnumerable<DownloadSession>> GetActiveAsync()
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                SELECT id, created_at, title, status, total_bytes, 
                       completed_count, failed_count, cancelled_count, 
                       total_count, completed_at
                FROM download_sessions 
                WHERE status NOT IN (@Completed, @Failed, @Cancelled)
                ORDER BY created_at DESC";
            
            var results = await connection.QueryAsync(sql, new 
            { 
                Completed = (int)DownloadStatus.Completed,
                Failed = (int)DownloadStatus.Failed,
                Cancelled = (int)DownloadStatus.Cancelled
            });
            
            return results.Select(MapFromDb).Where(s => s != null).Cast<DownloadSession>();
        });
    }

    /// <summary>
    /// Update session status and counts
    /// </summary>
    public async Task<bool> UpdateStatusAndCountsAsync(string sessionId, DownloadStatus status, 
        int completedCount, int failedCount, int cancelledCount)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            const string sql = @"
                UPDATE download_sessions 
                SET status = @Status, 
                    completed_count = @CompletedCount,
                    failed_count = @FailedCount,
                    cancelled_count = @CancelledCount,
                    completed_at = CASE 
                        WHEN @Status IN (@CompletedStatus, @FailedStatus, @CancelledStatus) 
                        THEN @CompletedAt 
                        ELSE completed_at 
                    END
                WHERE id = @Id";
            
            var rowsAffected = await connection.ExecuteAsync(sql, new 
            {
                Id = sessionId,
                Status = (int)status,
                CompletedCount = completedCount,
                FailedCount = failedCount,
                CancelledCount = cancelledCount,
                CompletedStatus = (int)DownloadStatus.Completed,
                FailedStatus = (int)DownloadStatus.Failed,
                CancelledStatus = (int)DownloadStatus.Cancelled,
                CompletedAt = DateTime.UtcNow.ToString("O")
            });
            
            return rowsAffected > 0;
        });
    }

    private static DownloadSession? MapFromDb(dynamic? row)
    {
        if (row == null) return null;

        return new DownloadSession
        {
            Id = row.id,
            CreatedAt = DateTime.Parse(row.created_at),
            Title = row.title,
            Status = (DownloadStatus)(int)(row.status ?? 0),
            TotalBytes = (long)(row.total_bytes ?? 0),
            CompletedCount = (int)(long)(row.completed_count ?? 0),
            FailedCount = (int)(long)(row.failed_count ?? 0),
            CancelledCount = (int)(long)(row.cancelled_count ?? 0),
            TotalCount = (int)(long)(row.total_count ?? 0),
            CompletedAt = row.completed_at != null ? DateTime.Parse(row.completed_at) : null
        };
    }

    private static object MapToDb(DownloadSession session)
    {
        return new
        {
            Id = session.Id,
            CreatedAt = session.CreatedAt.ToString("O"),
            Title = session.Title,
            Status = (int)session.Status,
            TotalBytes = session.TotalBytes,
            CompletedCount = session.CompletedCount,
            FailedCount = session.FailedCount,
            CancelledCount = session.CancelledCount,
            TotalCount = session.TotalCount,
            CompletedAt = session.CompletedAt?.ToString("O")
        };
    }
}