using Microsoft.Extensions.Configuration;

namespace SqliteForge.Configuration;

/// <summary>
/// Méthodes d'extension pour enregistrer le fournisseur de configuration SQLite sur un <see cref="IConfigurationBuilder"/>.
/// </summary>
public static class SqliteConfigurationExtensions
{
    /// <summary>
    /// Ajoute une source de configuration SQLite utilisant une chaîne de connexion statique.
    /// </summary>
    /// <param name="builder">Constructeur de configuration.</param>
    /// <param name="connectionString">Chaîne de connexion SQLite.</param>
    /// <param name="defaultSettings">Paramètres initiaux optionnels à insérer si la table est vide.</param>
    /// <param name="onLoadError">Délégué optionnel invoqué lorsqu'une erreur d'accès SQLite survient au chargement.</param>
    /// <returns>Le constructeur de configuration enrichi.</returns>
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
    /// Ajoute une source de configuration SQLite utilisant un résolveur dynamique de chaîne de connexion.
    /// </summary>
    /// <param name="builder">Constructeur de configuration.</param>
    /// <param name="connectionStringResolver">Fonction résolvant la chaîne de connexion SQLite active.</param>
    /// <param name="defaultSettings">Paramètres initiaux optionnels à insérer si la table est vide.</param>
    /// <param name="onLoadError">Délégué optionnel invoqué lorsqu'une erreur d'accès SQLite survient au chargement.</param>
    /// <returns>Le constructeur de configuration enrichi.</returns>
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
