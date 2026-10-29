namespace SqliteForge.Security;

/// <summary>
/// Holds the SQLite database decryption key in memory.
/// </summary>
public interface IDatabaseKeyHolder
{
    /// <summary>
    /// Active decryption key or passphrase held in memory.
    /// </summary>
    string? EncryptionKey { get; }

    /// <summary>
    /// Indicates whether the SQLite database file is encrypted.
    /// </summary>
    bool IsDatabaseEncrypted { get; }

    /// <summary>
    /// Indicates whether the database is unlocked and ready for reads and writes.
    /// </summary>
    bool IsUnlocked { get; }

    /// <summary>
    /// Updates the active key and the encryption state.
    /// </summary>
    /// <param name="key">Decryption key or passphrase.</param>
    /// <param name="isEncrypted">Whether the database is encrypted.</param>
    void SetKey(string? key, bool isEncrypted);

    /// <summary>
    /// Clears the key held in memory.
    /// </summary>
    void Clear();
}
