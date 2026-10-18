using Microsoft.Extensions.Configuration;

namespace SqliteForge.Configuration;

/// <summary>
/// Source de configuration <see cref="IConfigurationSource"/> adossée à une table SQLite <c>AppSettings</c>.
/// </summary>
/// <param name="connectionStringResolver">Fonction résolvant la chaîne de connexion SQLite active.</param>
/// <param name="defaultSettings">Paramètres initiaux optionnels à insérer si la table est vide.</param>
/// <param name="onLoadError">Délégué optionnel invoqué lorsqu'une erreur d'accès SQLite survient au chargement.</param>
public class SqliteConfigurationSource(
    Func<string?> connectionStringResolver,
    IEnumerable<SqliteSettingSeed>? defaultSettings = null,
    Action<Exception>? onLoadError = null) : IConfigurationSource
{
    private readonly Func<string?> _connectionStringResolver = connectionStringResolver ?? throw new ArgumentNullException(nameof(connectionStringResolver));

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="SqliteConfigurationSource"/> avec une chaîne de connexion statique.
    /// </summary>
    /// <param name="connectionString">Chaîne de connexion SQLite.</param>
    /// <param name="defaultSettings">Paramètres initiaux optionnels à insérer si la table est vide.</param>
    /// <param name="onLoadError">Délégué optionnel invoqué lorsqu'une erreur d'accès SQLite survient au chargement.</param>
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
