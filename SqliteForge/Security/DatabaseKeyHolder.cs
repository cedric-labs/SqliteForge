namespace SqliteForge.Security;

/// <summary>
/// Détenteur singleton thread-safe en mémoire de la clé de déchiffrement SQLite.
/// </summary>
public class DatabaseKeyHolder : IDatabaseKeyHolder
{
#if NET9_0_OR_GREATER
    private readonly Lock _syncRoot = new();
#else
    private readonly object _syncRoot = new();
#endif
    private string? _encryptionKey;
    private bool _isDatabaseEncrypted;

    /// <inheritdoc />
    public string? EncryptionKey
    {
        get
        {
            lock (_syncRoot)
            {
                return _encryptionKey;
            }
        }
    }

    /// <inheritdoc />
    public bool IsDatabaseEncrypted
    {
        get
        {
            lock (_syncRoot)
            {
                return _isDatabaseEncrypted;
            }
        }
    }

    /// <inheritdoc />
    public bool IsUnlocked
    {
        get
        {
            lock (_syncRoot)
            {
                return !_isDatabaseEncrypted || !string.IsNullOrEmpty(_encryptionKey);
            }
        }
    }

    /// <inheritdoc />
    public void SetKey(string? key, bool isEncrypted)
    {
        lock (_syncRoot)
        {
            _encryptionKey = key;
            _isDatabaseEncrypted = isEncrypted;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_syncRoot)
        {
            _encryptionKey = null;
        }
    }
}
