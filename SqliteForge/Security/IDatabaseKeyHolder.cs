namespace SqliteForge.Security;

/// <summary>
/// Contrat du détenteur en mémoire de la clé de déchiffrement de la base SQLite.
/// </summary>
public interface IDatabaseKeyHolder
{
    /// <summary>
    /// Clé ou phrase de passe active de déchiffrement en mémoire.
    /// </summary>
    string? EncryptionKey { get; }

    /// <summary>
    /// Indique si le fichier physique SQLite est chiffré.
    /// </summary>
    bool IsDatabaseEncrypted { get; }

    /// <summary>
    /// Indique si la base est déverrouillée et prête pour les lectures/écritures.
    /// </summary>
    bool IsUnlocked { get; }

    /// <summary>
    /// Met à jour la clé active et l'état de chiffrement.
    /// </summary>
    /// <param name="key">Clé ou phrase de passe de déchiffrement.</param>
    /// <param name="isEncrypted">Indique si la base est chiffrée.</param>
    void SetKey(string? key, bool isEncrypted);

    /// <summary>
    /// Réinitialise la clé en mémoire.
    /// </summary>
    void Clear();
}
