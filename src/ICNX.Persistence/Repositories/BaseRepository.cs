using System.Data;
using Dapper;
using ICNX.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ICNX.Persistence.Repositories;

/// <summary>
/// Base repository with common database operations
/// </summary>
public abstract class BaseRepository<T> : IRepository<T> where T : class
{
    protected readonly string ConnectionString;
    protected readonly ILogger Logger;

    protected BaseRepository(string connectionString, ILogger logger)
    {
        ConnectionString = connectionString;
        Logger = logger;
    }

    protected async Task<IDbConnection> GetConnectionAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public abstract Task<T?> GetByIdAsync(string id);
    public abstract Task<IEnumerable<T>> GetAllAsync();
    public abstract Task<string> AddAsync(T item);
    public abstract Task<bool> UpdateAsync(T item);
    public abstract Task<bool> DeleteAsync(string id);

    public virtual async Task<int> DeleteManyAsync(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        if (!idList.Any()) return 0;

        using var connection = await GetConnectionAsync();
        
        var placeholders = string.Join(",", idList.Select((_, i) => $"@id{i}"));
        var parameters = new DynamicParameters();
        
        for (int i = 0; i < idList.Count; i++)
        {
            parameters.Add($"id{i}", idList[i]);
        }

        var sql = $"DELETE FROM {GetTableName()} WHERE id IN ({placeholders})";
        return await connection.ExecuteAsync(sql, parameters);
    }

    protected abstract string GetTableName();

    /// <summary>
    /// Execute a query with automatic connection management and error handling
    /// </summary>
    protected async Task<TResult> ExecuteWithConnectionAsync<TResult>(
        Func<IDbConnection, Task<TResult>> operation)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            return await operation(connection);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Database operation failed for {EntityType}", typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Execute a command with automatic connection management and error handling
    /// </summary>
    protected async Task ExecuteWithConnectionAsync(Func<IDbConnection, Task> operation)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            await operation(connection);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Database operation failed for {EntityType}", typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Execute operations within a transaction
    /// </summary>
    protected async Task<TResult> ExecuteWithTransactionAsync<TResult>(
        Func<IDbConnection, IDbTransaction, Task<TResult>> operation)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                var result = await operation(connection, transaction);
                transaction.Commit();
                return result;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Database transaction failed for {EntityType}", typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Batch insert operations for performance
    /// </summary>
    protected async Task<int> BatchInsertAsync(IEnumerable<T> items, string insertSql, 
        Func<T, object> parameterMapper)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return 0;

        using var connection = await GetConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var parameters = itemList.Select(parameterMapper);
            var result = await connection.ExecuteAsync(insertSql, parameters, transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}