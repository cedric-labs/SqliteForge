using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SqliteForge.Data;

namespace SqliteForge.Security;

/// <summary>
/// Manages the SQLite encryption life cycle with SQLite3 Multiple Ciphers / SQLCipher.
/// </summary>
/// <param name="connectionProvider">Connection string provider.</param>
/// <param name="keyHolder">In-memory holder of the decryption key.</param>
/// <param name="keyStore">Local store of the key.</param>
/// <param name="serviceProvider">Application service provider.</param>
/// <param name="logger">Structured logger.</param>
/// <param name="configuration">Configuration instance reloaded after unlocking.</param>
/// <param name="onUnlockedCallback">Optional asynchronous action run once the database is unlocked.</param>
public class SqliteDatabaseEncryptionService(
    IDatabaseConnectionProvider connectionProvider,
    IDatabaseKeyHolder keyHolder,
    IDatabaseKeyStore keyStore,
    IServiceProvider serviceProvider,
    ILogger logger,
    IConfiguration? configuration = null,
    Func<IServiceProvider, CancellationToken, Task>? onUnlockedCallback = null) : IDatabaseEncryptionService
{
    private readonly IDatabaseConnectionProvider _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
    private readonly IDatabaseKeyHolder _keyHolder = keyHolder ?? throw new ArgumentNullException(nameof(keyHolder));
    private readonly IDatabaseKeyStore _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IConfiguration? _configuration = configuration;
    private readonly Func<IServiceProvider, CancellationToken, Task>? _onUnlockedCallback = onUnlockedCallback;

    /// <inheritdoc />
    public bool IsDatabaseEncrypted() => _keyHolder.IsDatabaseEncrypted;

    /// <inheritdoc />
    public bool IsDatabaseLocked() => _connectionProvider.IsDatabaseLocked();

    /// <inheritdoc />
    public bool VerifyPassphrase(string passphrase) =>
        VerifyPassphraseInternal(_connectionProvider.GetConnectionString(), passphrase);

    /// <inheritdoc />
    public async Task<bool> UnlockDatabaseAsync(
        string passphrase,
        bool rememberOnDevice,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            return false;
        }

        SqliteConnection.ClearAllPools();

        var isValid = VerifyPassphrase(passphrase);
        if (!isValid)
        {
            _logger.LogWarning("Database unlock failed: wrong passphrase.");
            return false;
        }

        _keyHolder.SetKey(passphrase, isEncrypted: true);
        SqliteConnection.ClearAllPools();

        if (rememberOnDevice)
        {
            await _keyStore.SaveKeyAsync(passphrase, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _keyStore.ClearSavedKeyAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            if (_onUnlockedCallback is not null)
            {
                await _onUnlockedCallback(_serviceProvider, cancellationToken).ConfigureAwait(false);
            }

            if (_configuration is IConfigurationRoot root)
            {
                root.Reload();
            }

            _logger.LogInformation("SQLite database unlocked.");
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Database initialization failed after unlocking.");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> EncryptDatabaseAsync(
        string newPassphrase,
        bool rememberOnDevice,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidatePassphrase(newPassphrase);
        if (!validation.IsValid)
        {
            return (false, validation.ErrorMessage);
        }

        if (_keyHolder.IsDatabaseEncrypted)
        {
            return (false, "The database is already encrypted. Use the passphrase change (rekey) instead.");
        }

        try
        {
            await ApplyRekeyAndPersistStateAsync(newPassphrase, isEncrypted: true, rememberOnDevice, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("SQLite database encryption turned on.");
            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Turning on SQLite database encryption failed.");
            return (false, $"Turning on encryption failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> ChangePassphraseAsync(
        string currentPassphrase,
        string newPassphrase,
        bool rememberOnDevice,
        CancellationToken cancellationToken = default)
    {
        if (!_keyHolder.IsDatabaseEncrypted)
        {
            return (false, "The database is not encrypted.");
        }

        if (!VerifyPassphrase(currentPassphrase))
        {
            return (false, "The current passphrase is wrong.");
        }

        var validation = ValidatePassphrase(newPassphrase);
        if (!validation.IsValid)
        {
            return (false, validation.ErrorMessage);
        }

        try
        {
            await ApplyRekeyAndPersistStateAsync(newPassphrase, isEncrypted: true, rememberOnDevice, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Database passphrase changed.");
            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Changing the SQLite passphrase failed.");
            return (false, $"Changing the passphrase failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> DecryptDatabaseToPlainAsync(
        string currentPassphrase,
        CancellationToken cancellationToken = default)
    {
        if (!_keyHolder.IsDatabaseEncrypted)
        {
            return (false, "The database is not encrypted.");
        }

        if (!VerifyPassphrase(currentPassphrase))
        {
            return (false, "The current passphrase is wrong.");
        }

        try
        {
            await ApplyRekeyAndPersistStateAsync(null, isEncrypted: false, rememberOnDevice: false, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Encryption removed: the database is now stored in plain text.");
            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Removing SQLite encryption failed.");
            return (false, $"Removing encryption failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<bool> IsRememberOnDeviceEnabledAsync(CancellationToken cancellationToken = default) =>
        _keyStore.HasSavedKeyAsync(cancellationToken);

    /// <inheritdoc />
    public async Task SetRememberOnDeviceAsync(bool remember, CancellationToken cancellationToken = default)
    {
        if (remember)
        {
            var key = _keyHolder.EncryptionKey;
            if (!string.IsNullOrEmpty(key))
            {
                await _keyStore.SaveKeyAsync(key, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await _keyStore.ClearSavedKeyAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Detects whether a SQLite file is encrypted by reading its 16-byte header.
    /// An unencrypted SQLite file always starts with <c>SQLite format 3\0</c>.
    /// </summary>
    /// <param name="filePath">Path of the SQLite file to inspect.</param>
    /// <returns><c>true</c> when the file exists and is encrypted; otherwise <c>false</c>.</returns>
    public static bool CheckIfFileEncrypted(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var info = new FileInfo(filePath);
            if (info.Length < 16)
            {
                return false;
            }

            var header = new byte[16];
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                stream.ReadExactly(header, 0, 16);
            }

            var headerText = Encoding.ASCII.GetString(header);
            return !headerText.StartsWith("SQLite format 3", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A header that cannot be read for now (file locked) is treated as unencrypted; the next check reads it again
            return false;
        }
    }

    /// <summary>
    /// Checks whether a passphrase decrypts the database and lets it be queried.
    /// </summary>
    /// <param name="baseConnectionString">Base connection string.</param>
    /// <param name="passphrase">Passphrase to check.</param>
    /// <returns><c>true</c> when the connection and the read of <c>sqlite_master</c> succeed; otherwise <c>false</c>.</returns>
    public static bool VerifyPassphrase(string baseConnectionString, string passphrase) =>
        VerifyPassphraseInternal(baseConnectionString, passphrase);

    /// <summary>
    /// Checks that a SQLite passphrase meets the minimum security rules.
    /// </summary>
    /// <param name="passphrase">Passphrase to check.</param>
    /// <returns>A tuple with the validity and the matching error message.</returns>
    public static (bool IsValid, string? ErrorMessage) ValidatePassphrase(string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            return (false, "The passphrase cannot be empty.");
        }

        if (passphrase.Length < 8)
        {
            return (false, "The passphrase must contain at least 8 characters.");
        }

        if (passphrase.Contains('\n', StringComparison.Ordinal) || passphrase.Contains('\r', StringComparison.Ordinal))
        {
            return (false, "The passphrase cannot contain a line break.");
        }

        return (true, null);
    }

    private async Task ApplyRekeyAndPersistStateAsync(
        string? targetPassphrase,
        bool isEncrypted,
        bool rememberOnDevice,
        CancellationToken cancellationToken)
    {
        SqliteConnection.ClearAllPools();

        var currentConnStr = _connectionProvider.GetConnectionString();
        var escapedPass = (targetPassphrase ?? string.Empty).Replace("'", "''", StringComparison.Ordinal);
        var rekeyStatement = string.Concat("PRAGMA rekey = '", escapedPass, "';");

        var conn = new SqliteConnection(currentConnStr);
        await using (conn.ConfigureAwait(false))
        {
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecutePragmaCommandAsync(conn, "PRAGMA journal_mode = DELETE;", cancellationToken).ConfigureAwait(false);
            await ExecutePragmaCommandAsync(conn, rekeyStatement, cancellationToken).ConfigureAwait(false);
            await ExecutePragmaCommandAsync(conn, "PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);
        }

        SqliteConnection.ClearAllPools();
        _keyHolder.SetKey(targetPassphrase, isEncrypted);

        if (isEncrypted && rememberOnDevice && !string.IsNullOrEmpty(targetPassphrase))
        {
            await _keyStore.SaveKeyAsync(targetPassphrase, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _keyStore.ClearSavedKeyAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ExecutePragmaCommandAsync(
        SqliteConnection connection,
        string pragmaStatement,
        CancellationToken cancellationToken)
    {
        var cmd = connection.CreateCommand();
        await using (cmd.ConfigureAwait(false))
        {
            cmd.CommandText = pragmaStatement;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool VerifyPassphraseInternal(string baseConnectionString, string passphrase)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder(baseConnectionString)
            {
                Password = passphrase,
                Mode = SqliteOpenMode.ReadWrite
            };

            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
            cmd.ExecuteScalar();

            return true;
        }
        catch (Exception ex) when (ex is SqliteException or ArgumentException or InvalidOperationException)
        {
            // A SQLite exception (for example SQLITE_NOTADB = 26) means the decryption key is wrong
            return false;
        }
    }
}
