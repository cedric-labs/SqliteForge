namespace SqliteForge.Data;

/// <summary>
/// Provides the SQLite connection string, including the active decryption key.
/// </summary>
public interface IDatabaseConnectionProvider
{
    /// <summary>
    /// Returns the effective connection string (with the password when the database is encrypted).
    /// </summary>
    /// <returns>A ready-to-use SQLite connection string.</returns>
    string GetConnectionString();

    /// <summary>
    /// Returns the absolute path of the SQLite database file.
    /// </summary>
    /// <returns>Absolute file path, or <c>:memory:</c>.</returns>
    string GetDatabaseFilePath();

    /// <summary>
    /// Indicates whether the SQLite database is encrypted.
    /// </summary>
    /// <returns><c>true</c> when the database is encrypted; otherwise <c>false</c>.</returns>
    bool IsDatabaseEncrypted();

    /// <summary>
    /// Indicates whether the database is locked (encrypted with no valid key in memory).
    /// </summary>
    /// <returns><c>true</c> when the database is locked; otherwise <c>false</c>.</returns>
    bool IsDatabaseLocked();
}
