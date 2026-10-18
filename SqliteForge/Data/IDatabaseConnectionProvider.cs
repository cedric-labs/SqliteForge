namespace SqliteForge.Data;

/// <summary>
/// Fournisseur dynamique de chaîne de connexion SQLite intégrant la clé de déchiffrement active.
/// </summary>
public interface IDatabaseConnectionProvider
{
    /// <summary>
    /// Retourne la chaîne de connexion effective (avec mot de passe si la base est chiffrée).
    /// </summary>
    /// <returns>Chaîne de connexion SQLite prête à l'emploi.</returns>
    string GetConnectionString();

    /// <summary>
    /// Retourne le chemin physique absolu du fichier SQLite de base de données.
    /// </summary>
    /// <returns>Chemin absolu du fichier ou <c>:memory:</c>.</returns>
    string GetDatabaseFilePath();

    /// <summary>
    /// Indique si la base de données SQLite est chiffrée.
    /// </summary>
    /// <returns><c>true</c> si la base est chiffrée ; sinon, <c>false</c>.</returns>
    bool IsDatabaseEncrypted();

    /// <summary>
    /// Indique si la base est actuellement verrouillée (chiffrée sans clé valide en mémoire).
    /// </summary>
    /// <returns><c>true</c> si la base est verrouillée ; sinon, <c>false</c>.</returns>
    bool IsDatabaseLocked();
}
