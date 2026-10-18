namespace SqliteForge.Security;

/// <summary>
/// Contrat du magasin local de persistance sécurisée de la clé ("Se souvenir sur cet appareil").
/// </summary>
public interface IDatabaseKeyStore
{
    /// <summary>
    /// Récupère la phrase de passe sauvegardée pour cet appareil, ou <c>null</c> si absente ou non déchiffrable.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>La clé déchiffrée ou <c>null</c>.</returns>
    Task<string?> GetSavedKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sauvegarde de manière protégée la phrase de passe sur cet appareil.
    /// </summary>
    /// <param name="key">Phrase de passe à persister.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Tâche asynchrone.</returns>
    Task SaveKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime la phrase de passe mémorisée sur cet appareil.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Tâche asynchrone.</returns>
    Task ClearSavedKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indique si une clé sauvegardée existe sur cet appareil.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns><c>true</c> si une clé valide est mémorisée ; sinon, <c>false</c>.</returns>
    Task<bool> HasSavedKeyAsync(CancellationToken cancellationToken = default);
}
