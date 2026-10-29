using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace SqliteForge.Configuration;

/// <summary>
/// Configuration provider that reads business and system settings
/// directly from a dedicated SQLite table (<c>AppSettings</c>).
/// </summary>
/// <param name="connectionStringResolver">Function that resolves the active SQLite connection string.</param>
/// <param name="defaultSettings">Optional initial settings inserted when the table is empty.</param>
/// <param name="onLoadError">Optional delegate called when a SQLite access error occurs while loading.</param>
public class SqliteConfigurationProvider(
    Func<string?> connectionStringResolver,
    IEnumerable<SqliteSettingSeed>? defaultSettings = null,
    Action<Exception>? onLoadError = null) : ConfigurationProvider
{
    private readonly Func<string?> _connectionStringResolver = connectionStringResolver ?? throw new ArgumentNullException(nameof(connectionStringResolver));

    /// <summary>
    /// Creates a <see cref="SqliteConfigurationProvider"/> with a static connection string.
    /// </summary>
    /// <param name="connectionString">SQLite connection string.</param>
    /// <param name="defaultSettings">Optional initial settings inserted when the table is empty.</param>
    /// <param name="onLoadError">Optional delegate called when a SQLite access error occurs while loading.</param>
    public SqliteConfigurationProvider(
        string connectionString,
        IEnumerable<SqliteSettingSeed>? defaultSettings = null,
        Action<Exception>? onLoadError = null)
        : this(() => connectionString, defaultSettings, onLoadError)
    {
    }

    /// <inheritdoc />
    public override void Load()
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var connStr = _connectionStringResolver();
        if (string.IsNullOrWhiteSpace(connStr))
        {
            Data = settings;
            return;
        }

        try
        {
            using var connection = new SqliteConnection(connStr);
            connection.Open();

            EnsureTableCreated(connection);

            if (defaultSettings is not null && CountExistingSettings(connection) == 0)
            {
                SeedSettings(connection, defaultSettings);
            }

            ReadAllSettings(connection, settings);
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException or IOException)
        {
            // During early startup, or while the encrypted database is locked,
            // the error is tolerated so the host keeps starting, and the caller is notified.
            onLoadError?.Invoke(ex);
        }

        Data = settings;
    }

    private static void EnsureTableCreated(SqliteConnection connection)
    {
        using var initCmd = connection.CreateCommand();
        initCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT NOT NULL PRIMARY KEY,
                Value TEXT,
                Description TEXT,
                UpdatedAtUtc TEXT NOT NULL
            );
            """;
        initCmd.ExecuteNonQuery();
    }

    private static long CountExistingSettings(SqliteConnection connection)
    {
        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM AppSettings;";
        return Convert.ToInt64(countCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void SeedSettings(SqliteConnection connection, IEnumerable<SqliteSettingSeed> seeds)
    {
        using var transaction = connection.BeginTransaction();
        var nowIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        foreach (var seed in seeds)
        {
            if (string.IsNullOrWhiteSpace(seed.Key))
            {
                continue;
            }

            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO AppSettings (Key, Value, Description, UpdatedAtUtc)
                VALUES (@Key, @Value, @Description, @UpdatedAtUtc);
                """;
            cmd.Parameters.AddWithValue("@Key", seed.Key);
            cmd.Parameters.AddWithValue("@Value", (object?)seed.Value ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Description", (object?)seed.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UpdatedAtUtc", nowIso);
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void ReadAllSettings(SqliteConnection connection, Dictionary<string, string?> settings)
    {
        using var readCmd = connection.CreateCommand();
        readCmd.CommandText = "SELECT Key, Value FROM AppSettings;";
        using var reader = readCmd.ExecuteReader();
        while (reader.Read())
        {
            var key = reader.GetString(0);
            var value = reader.IsDBNull(1) ? null : reader.GetString(1);
            settings[key] = value;
        }
    }
}
