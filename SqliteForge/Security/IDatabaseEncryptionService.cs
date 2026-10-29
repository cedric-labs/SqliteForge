namespace SqliteForge.Security;

/// <summary>
/// Manages whole-database encryption of SQLite (SQLCipher / SQLite3 Multiple Ciphers).
/// </summary>
public interface IDatabaseEncryptionService
{
    /// <summary>
    /// Indicates whether the SQLite database file is encrypted.
    /// </summary>
    /// <returns><c>true</c> when the database is encrypted; otherwise <c>false</c>.</returns>
    bool IsDatabaseEncrypted();

    /// <summary>
    /// Indicates whether the database is encrypted and locked (waiting for the master passphrase).
    /// </summary>
    /// <returns><c>true</c> when the database is locked; otherwise <c>false</c>.</returns>
    bool IsDatabaseLocked();

    /// <summary>
    /// Checks whether a passphrase decrypts the database.
    /// </summary>
    /// <param name="passphrase">Passphrase to check.</param>
    /// <returns><c>true</c> when the passphrase is valid; otherwise <c>false</c>.</returns>
    bool VerifyPassphrase(string passphrase);

    /// <summary>
    /// Unlocks the database at startup or after it was locked.
    /// </summary>
    /// <param name="passphrase">Master passphrase.</param>
    /// <param name="rememberOnDevice">Whether the key is remembered on this device.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns><c>true</c> when the database was unlocked; otherwise <c>false</c>.</returns>
    Task<bool> UnlockDatabaseAsync(string passphrase, bool rememberOnDevice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts an existing unencrypted database for the first time.
    /// </summary>
    /// <param name="newPassphrase">New master passphrase.</param>
    /// <param name="rememberOnDevice">Whether the key is remembered on this device.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>The outcome of the operation and the error message, if any.</returns>
    Task<(bool Success, string? ErrorMessage)> EncryptDatabaseAsync(string newPassphrase, bool rememberOnDevice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the database encryption passphrase (<c>PRAGMA rekey</c>).
    /// </summary>
    /// <param name="currentPassphrase">Current passphrase.</param>
    /// <param name="newPassphrase">New passphrase.</param>
    /// <param name="rememberOnDevice">Whether the new key is remembered on this device.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>The outcome of the operation and the error message, if any.</returns>
    Task<(bool Success, string? ErrorMessage)> ChangePassphraseAsync(string currentPassphrase, string newPassphrase, bool rememberOnDevice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the encryption and restores the database to the standard unencrypted SQLite format.
    /// </summary>
    /// <param name="currentPassphrase">Current passphrase.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>The outcome of the operation and the error message, if any.</returns>
    Task<(bool Success, string? ErrorMessage)> DecryptDatabaseToPlainAsync(string currentPassphrase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether the key is remembered on this device.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns><c>true</c> when the key is remembered; otherwise <c>false</c>.</returns>
    Task<bool> IsRememberOnDeviceEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns remembering the in-memory key on this device on or off.
    /// </summary>
    /// <param name="remember"><c>true</c> to remember the key; <c>false</c> to erase it.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>A task that completes when the setting is saved.</returns>
    Task SetRememberOnDeviceAsync(bool remember, CancellationToken cancellationToken = default);
}
