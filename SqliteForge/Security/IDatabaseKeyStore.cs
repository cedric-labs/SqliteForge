namespace SqliteForge.Security;

/// <summary>
/// Local protected store of the key ("Remember on this device").
/// </summary>
public interface IDatabaseKeyStore
{
    /// <summary>
    /// Gets the passphrase saved for this device, or <c>null</c> when it is missing or cannot be decrypted.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>The decrypted key, or <c>null</c>.</returns>
    Task<string?> GetSavedKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the passphrase on this device in protected form.
    /// </summary>
    /// <param name="key">Passphrase to save.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>A task that completes when the passphrase is saved.</returns>
    Task SaveKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the passphrase remembered on this device.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>A task that completes when the passphrase is deleted.</returns>
    Task ClearSavedKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether a saved key exists on this device.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns><c>true</c> when a valid key is remembered; otherwise <c>false</c>.</returns>
    Task<bool> HasSavedKeyAsync(CancellationToken cancellationToken = default);
}
