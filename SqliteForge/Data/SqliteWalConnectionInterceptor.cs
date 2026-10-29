using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SqliteForge.Data;

/// <summary>
/// EF Core interceptor that turns on WAL (Write-Ahead Logging),
/// busy_timeout and foreign keys each time a SQLite connection opens.
/// WAL lets reads run concurrently without being blocked by writes.
/// </summary>
public class SqliteWalConnectionInterceptor : DbConnectionInterceptor
{
    private const string WalPragmaCommands = """
        PRAGMA journal_mode = WAL;
        PRAGMA synchronous = NORMAL;
        PRAGMA busy_timeout = 5000;
        PRAGMA foreign_keys = ON;
        """;

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ExecutePragmas(connection);
        base.ConnectionOpened(connection, eventData);
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await ExecutePragmasAsync(connection, cancellationToken).ConfigureAwait(false);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }

    private static void ExecutePragmas(DbConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = WalPragmaCommands;
            cmd.ExecuteNonQuery();
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 8)
        {
            // A read-only connection (SQLITE_READONLY = 8) cannot switch to WAL; it keeps its journal mode
        }
    }

    private static async Task ExecutePragmasAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = WalPragmaCommands;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 8)
        {
            // A read-only connection (SQLITE_READONLY = 8) cannot switch to WAL; it keeps its journal mode
        }
    }
}
