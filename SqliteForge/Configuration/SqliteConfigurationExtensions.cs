using Microsoft.Extensions.Configuration;

namespace SqliteForge.Configuration;

/// <summary>
/// Extension methods that register the SQLite configuration provider on an <see cref="IConfigurationBuilder"/>.
/// </summary>
public static class SqliteConfigurationExtensions
{
    /// <summary>
    /// Adds a SQLite configuration source that uses a static connection string.
    /// </summary>
    /// <param name="builder">Configuration builder.</param>
    /// <param name="connectionString">SQLite connection string.</param>
    /// <param name="defaultSettings">Optional initial settings inserted when the table is empty.</param>
    /// <param name="onLoadError">Optional delegate called when a SQLite access error occurs while loading.</param>
    /// <returns>The configuration builder, with the source added.</returns>
    public static IConfigurationBuilder AddSqliteConfiguration(
        this IConfigurationBuilder builder,
        string connectionString,
        IEnumerable<SqliteSettingSeed>? defaultSettings = null,
        Action<Exception>? onLoadError = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Add(new SqliteConfigurationSource(connectionString, defaultSettings, onLoadError));
    }

    /// <summary>
    /// Adds a SQLite configuration source that resolves its connection string on each load.
    /// </summary>
    /// <param name="builder">Configuration builder.</param>
    /// <param name="connectionStringResolver">Function that resolves the active SQLite connection string.</param>
    /// <param name="defaultSettings">Optional initial settings inserted when the table is empty.</param>
    /// <param name="onLoadError">Optional delegate called when a SQLite access error occurs while loading.</param>
    /// <returns>The configuration builder, with the source added.</returns>
    public static IConfigurationBuilder AddSqliteConfiguration(
        this IConfigurationBuilder builder,
        Func<string?> connectionStringResolver,
        IEnumerable<SqliteSettingSeed>? defaultSettings = null,
        Action<Exception>? onLoadError = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Add(new SqliteConfigurationSource(connectionStringResolver, defaultSettings, onLoadError));
    }
}
