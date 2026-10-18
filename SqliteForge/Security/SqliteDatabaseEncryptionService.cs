using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SqliteForge.Data;

namespace SqliteForge.Security;

/// <summary>
/// Service gérant le cycle de vie du chiffrement SQLite avec SQLite3 Multiple Ciphers / SQLCipher.
/// </summary>
/// <param name="connectionProvider">Fournisseur dynamique de chaîne de connexion.</param>
/// <param name="keyHolder">Détenteur en mémoire de la clé de déchiffrement.</param>
/// <param name="keyStore">Magasin de persistance locale de la clé.</param>
/// <param name="serviceProvider">Fournisseur de services applicatifs.</param>
/// <param name="logger">Journaliseur structuré.</param>
/// <param name="configuration">Instance de configuration à recharger après déverrouillage.</param>
/// <param name="onUnlockedCallback">Action asynchrone optionnelle exécutée après déverrouillage réussi de la base.</param>
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
            _logger.LogWarning("Échec du déverrouillage de la base de données : phrase de passe incorrecte.");
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

            _logger.LogInformation("Base de données SQLite déverrouillée avec succès.");
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Erreur lors de l'initialisation de la base après déverrouillage.");
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
            return (false, "La base de données est déjà chiffrée. Utilisez la fonction de modification du mot de passe (rekey).");
        }

        try
        {
            await ApplyRekeyAndPersistStateAsync(newPassphrase, isEncrypted: true, rememberOnDevice, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Chiffrement global de la base de données SQLite activé avec succès.");
            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Erreur lors de l'activation du chiffrement de la base SQLite.");
            return (false, $"Erreur lors de l'activation du chiffrement: {ex.Message}");
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
            return (false, "La base de données n'est pas chiffrée.");
        }

        if (!VerifyPassphrase(currentPassphrase))
        {
            return (false, "La phrase de passe actuelle est incorrecte.");
        }

        var validation = ValidatePassphrase(newPassphrase);
        if (!validation.IsValid)
        {
            return (false, validation.ErrorMessage);
        }

        try
        {
            await ApplyRekeyAndPersistStateAsync(newPassphrase, isEncrypted: true, rememberOnDevice, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Phrase de passe de la base de données modifiée avec succès.");
            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Erreur lors de la modification de la phrase de passe SQLite.");
            return (false, $"Erreur lors de la modification de la phrase de passe: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> DecryptDatabaseToPlainAsync(
        string currentPassphrase,
        CancellationToken cancellationToken = default)
    {
        if (!_keyHolder.IsDatabaseEncrypted)
        {
            return (false, "La base de données n'est pas chiffrée.");
        }

        if (!VerifyPassphrase(currentPassphrase))
        {
            return (false, "La phrase de passe actuelle est incorrecte.");
        }

        try
        {
            await ApplyRekeyAndPersistStateAsync(null, isEncrypted: false, rememberOnDevice: false, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Chiffrement retiré : la base de données est désormais en texte brut.");
            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Erreur lors du retrait du chiffrement SQLite.");
            return (false, $"Erreur lors du retrait du chiffrement: {ex.Message}");
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
    /// Détecte si un fichier physique SQLite est chiffré en analysant son en-tête de 16 octets.
    /// Un fichier SQLite non chiffré débute impérativement par <c>SQLite format 3\0</c>.
    /// </summary>
    /// <param name="filePath">Chemin du fichier SQLite à inspecter.</param>
    /// <returns><c>true</c> si le fichier existe et est chiffré ; sinon, <c>false</c>.</returns>
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
            // Tolérer l'impossibilité temporaire de lire l'en-tête et considérer non chiffré par défaut
            return false;
        }
    }

    /// <summary>
    /// Vérifie si une phrase de passe permet de déchiffrer et d'interroger la base de données.
    /// </summary>
    /// <param name="baseConnectionString">Chaîne de connexion de base.</param>
    /// <param name="passphrase">Phrase de passe à vérifier.</param>
    /// <returns><c>true</c> si la connexion et la lecture de <c>sqlite_master</c> réussissent ; sinon, <c>false</c>.</returns>
    public static bool VerifyPassphrase(string baseConnectionString, string passphrase) =>
        VerifyPassphraseInternal(baseConnectionString, passphrase);

    /// <summary>
    /// Valide le respect des contraintes minimales de sécurité d'une phrase de passe SQLite.
    /// </summary>
    /// <param name="passphrase">Phrase de passe à contrôler.</param>
    /// <returns>Un tuple indiquant la validité et le message d'erreur associé.</returns>
    public static (bool IsValid, string? ErrorMessage) ValidatePassphrase(string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            return (false, "La phrase de passe ne peut pas être vide.");
        }

        if (passphrase.Length < 8)
        {
            return (false, "La phrase de passe doit contenir au moins 8 caractères.");
        }

        if (passphrase.Contains('\n', StringComparison.Ordinal) || passphrase.Contains('\r', StringComparison.Ordinal))
        {
            return (false, "La phrase de passe ne peut pas contenir de retour à la ligne.");
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
            // Une exception SQLite (ex: SQLITE_NOTADB = 26) indique que la clé de déchiffrement est invalide
            return false;
        }
    }
}
