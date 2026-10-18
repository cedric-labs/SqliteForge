namespace SqliteForge.Security;

/// <summary>
/// Service de gestion du chiffrement global de la base SQLite (SQLCipher / SQLite3 Multiple Ciphers).
/// </summary>
public interface IDatabaseEncryptionService
{
    /// <summary>
    /// Indique si la base de données physique SQLite est actuellement chiffrée.
    /// </summary>
    /// <returns><c>true</c> si la base est chiffrée ; sinon, <c>false</c>.</returns>
    bool IsDatabaseEncrypted();

    /// <summary>
    /// Indique si la base de données est chiffrée et verrouillée (en attente du mot de passe maître).
    /// </summary>
    /// <returns><c>true</c> si la base est verrouillée ; sinon, <c>false</c>.</returns>
    bool IsDatabaseLocked();

    /// <summary>
    /// Vérifie si une phrase de passe permet de déchiffrer la base de données.
    /// </summary>
    /// <param name="passphrase">Phrase de passe à vérifier.</param>
    /// <returns><c>true</c> si la phrase de passe est valide ; sinon, <c>false</c>.</returns>
    bool VerifyPassphrase(string passphrase);

    /// <summary>
    /// Déverrouille la base de données au démarrage ou après verrouillage.
    /// </summary>
    /// <param name="passphrase">Phrase de passe maître.</param>
    /// <param name="rememberOnDevice">Indique s'il faut mémoriser la clé sur cet appareil.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns><c>true</c> si le déverrouillage a réussi ; sinon, <c>false</c>.</returns>
    Task<bool> UnlockDatabaseAsync(string passphrase, bool rememberOnDevice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chiffre pour la première fois une base de données existante non chiffrée.
    /// </summary>
    /// <param name="newPassphrase">Nouvelle phrase de passe maître.</param>
    /// <param name="rememberOnDevice">Indique s'il faut mémoriser la clé sur cet appareil.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Résultat de l'opération et message d'erreur éventuel.</returns>
    Task<(bool Success, string? ErrorMessage)> EncryptDatabaseAsync(string newPassphrase, bool rememberOnDevice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifie la phrase de passe de chiffrement de la base (opération <c>PRAGMA rekey</c>).
    /// </summary>
    /// <param name="currentPassphrase">Phrase de passe actuelle.</param>
    /// <param name="newPassphrase">Nouvelle phrase de passe.</param>
    /// <param name="rememberOnDevice">Indique s'il faut mémoriser la nouvelle clé sur cet appareil.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Résultat de l'opération et message d'erreur éventuel.</returns>
    Task<(bool Success, string? ErrorMessage)> ChangePassphraseAsync(string currentPassphrase, string newPassphrase, bool rememberOnDevice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retire le chiffrement pour restaurer la base au format SQLite standard non chiffré.
    /// </summary>
    /// <param name="currentPassphrase">Phrase de passe actuelle.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Résultat de l'opération et message d'erreur éventuel.</returns>
    Task<(bool Success, string? ErrorMessage)> DecryptDatabaseToPlainAsync(string currentPassphrase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indique si la mémorisation sur cet appareil est active.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns><c>true</c> si la mémorisation est active ; sinon, <c>false</c>.</returns>
    Task<bool> IsRememberOnDeviceEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Active ou désactive la mémorisation sur cet appareil avec la clé en mémoire.
    /// </summary>
    /// <param name="remember"><c>true</c> pour mémoriser la clé ; <c>false</c> pour l'effacer.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Tâche asynchrone.</returns>
    Task SetRememberOnDeviceAsync(bool remember, CancellationToken cancellationToken = default);
}
