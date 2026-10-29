using Microsoft.Extensions.Configuration;

namespace SqliteForge.Configuration;

/// <summary>
/// <see cref="IConfigurationSource"/> backed by a SQLite <c>AppSettings</c> table.
/// </summary>
/// <param name="connectionStringResolver">Function that resolves the active SQLite connection string.</param>
/// <param name="defaultSettings">Optional initial settings inserted when the table is empty.</param>
/// <param name="onLoadError">Optional delegate called when a SQLite access error occurs while loading.</param>
public class SqliteConfigurationSource(
    Func<string?> connectionStringResolver,
    IEnumerable<SqliteSettingSeed>? defaultSettings = null,
    Action<Exception>? onLoadError = null) : IConfigurationSource
{
    private readonly Func<string?> _connectionStringResolver = connectionStringResolver ?? throw new ArgumentNullException(nameof(connectionStringResolver));

    /// <summary>
    /// Creates a <see cref="SqliteConfigurationSource"/> with a static connection string.
    /// </summary>
    /// <param name="connectionString">SQLite connection string.</param>
    /// <param name="defaultSettings">Optional initial settings inserted when the table is empty.</param>
    /// <param name="onLoadError">Optional delegate called when a SQLite access error occurs while loading.</param>
    public SqliteConfigurationSource(
        string connectionString,
        IEnumerable<SqliteSettingSeed>? defaultSettings = null,
        Action<Exception>? onLoadError = null)
        : this(() => connectionString, defaultSettings, onLoadError)
    {
    }

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return new SqliteConfigurationProvider(_connectionStringResolver, defaultSettings, onLoadError);
    }
}
