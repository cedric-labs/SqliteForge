using Microsoft.Data.Sqlite;
using SqliteForge.Security;

namespace SqliteForge.Data;

/// <summary>
/// Implémentation du fournisseur dynamique de chaîne de connexion SQLite.
/// </summary>
/// <param name="baseConnectionString">Chaîne de connexion SQLite de base.</param>
/// <param name="keyHolder">Détenteur en mémoire de la clé de déchiffrement.</param>
public class DatabaseConnectionProvider(string baseConnectionString, IDatabaseKeyHolder keyHolder) : IDatabaseConnectionProvider
{
    private readonly string _databaseFilePath = ResolveDatabaseFilePath(baseConnectionString);
    private readonly IDatabaseKeyHolder _keyHolder = keyHolder ?? throw new ArgumentNullException(nameof(keyHolder));

    /// <inheritdoc />
    public string GetConnectionString()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseConnectionString);

        var key = _keyHolder.EncryptionKey;
        if (string.IsNullOrEmpty(key))
        {
            return baseConnectionString;
        }

        var builder = new SqliteConnectionStringBuilder(baseConnectionString)
        {
            Password = key
        };
        return builder.ConnectionString;
    }

    /// <inheritdoc />
    public string GetDatabaseFilePath() => _databaseFilePath;

    /// <inheritdoc />
    public bool IsDatabaseEncrypted() => _keyHolder.IsDatabaseEncrypted;

    /// <inheritdoc />
    public bool IsDatabaseLocked() => _keyHolder.IsDatabaseEncrypted && !_keyHolder.IsUnlocked;

    private static string ResolveDatabaseFilePath(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var source = builder.DataSource;
        if (string.Equals(source, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return ":memory:";
        }

        return Path.IsPathRooted(source)
            ? source
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, source));
    }
}
