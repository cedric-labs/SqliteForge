using System.Globalization;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SqliteForge.Data;
using Xunit;

namespace SqliteForge.Tests.Data;

public sealed class SqliteWalConnectionInterceptorTests : IDisposable
{
    private readonly string _dbFilePath;
    private readonly string _connectionString;

    public SqliteWalConnectionInterceptorTests()
    {
        SQLitePCL.Batteries_V2.Init();
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"sqliteforge_wal_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbFilePath};";
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbFilePath))
        {
            try
            {
                File.Delete(_dbFilePath);
            }
            catch (IOException)
            {
                // Ignorer le verrou temporaire du système de fichiers lors du nettoyage
            }
        }
    }

    [Fact]
    public void ConnectionOpened_WhenWritableConnectionOpenedViaEfCore_EnablesForeignKeysAndWal()
    {
        var options = new DbContextOptionsBuilder<WalTestDbContext>()
            .UseSqlite(_connectionString)
            .AddInterceptors(new SqliteWalConnectionInterceptor())
            .Options;

        using var context = new WalTestDbContext(options);
        context.Database.OpenConnection();

        var connection = context.Database.GetDbConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys;";
        var foreignKeys = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        foreignKeys.Should().Be(1);
    }

    [Fact]
    public async Task ConnectionOpenedAsync_WhenWritableConnectionOpenedViaEfCore_EnablesForeignKeysAndWal()
    {
        var options = new DbContextOptionsBuilder<WalTestDbContext>()
            .UseSqlite(_connectionString)
            .AddInterceptors(new SqliteWalConnectionInterceptor())
            .Options;

        await using var context = new WalTestDbContext(options);
        await context.Database.OpenConnectionAsync();

        var connection = context.Database.GetDbConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys;";
        var foreignKeys = Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        foreignKeys.Should().Be(1);
    }

    private sealed class WalTestDbContext(DbContextOptions<WalTestDbContext> options) : DbContext(options);
}
