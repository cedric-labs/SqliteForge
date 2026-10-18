using System.Security.Cryptography;
using System.Text;

namespace SqliteForge.Security;

/// <summary>
/// Stockage local sécurisé de la clé SQLite pour la fonction "Se souvenir sur cet appareil".
/// Sur Windows, utilise l'API DPAPI (<c>ProtectedData</c> avec <c>DataProtectionScope.CurrentUser</c>).
/// Sur Linux / macOS, utilise un chiffrement AES-256 avec vecteur d'initialisation préfixé.
/// </summary>
/// <param name="customPath">Chemin personnalisé optionnel du fichier de clé.</param>
/// <param name="applicationName">Nom du sous-dossier applicatif dans <c>LocalApplicationData</c> lorsque <paramref name="customPath"/> n'est pas fourni.</param>
public class LocalProtectedFileKeyStore(string? customPath = null, string applicationName = "Fluxa") : IDatabaseKeyStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Fluxa-Sqlite3MC-DeviceBinding-v1");
    private readonly string _storagePath = !string.IsNullOrEmpty(customPath)
        ? customPath
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            string.IsNullOrWhiteSpace(applicationName) ? "SqliteForge" : applicationName,
            "dbkey.dat");

    /// <summary>
    /// Récupère de façon synchrone la phrase de passe sauvegardée, ou <c>null</c> si absente ou non déchiffrable.
    /// </summary>
    /// <returns>La phrase de passe déchiffrée ou <c>null</c>.</returns>
    public string? GetSavedKey()
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return null;
            }

            var encryptedBytes = File.ReadAllBytes(_storagePath);
            if (encryptedBytes.Length == 0)
            {
                return null;
            }

            byte[] plainBytes;
            if (OperatingSystem.IsWindows())
            {
                plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            }
            else
            {
                plainBytes = DecryptFallback(encryptedBytes);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            // En cas d'incompatibilité (ex: machine reformatée ou clé corrompue),
            // retourner null pour forcer la saisie manuelle de la phrase de passe.
            return null;
        }
    }

    /// <summary>
    /// Sauvegarde de façon synchrone la phrase de passe sur cet appareil.
    /// </summary>
    /// <param name="key">Phrase de passe à persister.</param>
    public void SaveKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var plainBytes = Encoding.UTF8.GetBytes(key);
        byte[] encryptedBytes;

        if (OperatingSystem.IsWindows())
        {
            encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        }
        else
        {
            encryptedBytes = EncryptFallback(plainBytes);
        }

        var dir = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllBytes(_storagePath, encryptedBytes);
    }

    /// <summary>
    /// Supprime de façon synchrone la phrase de passe mémorisée sur cet appareil.
    /// </summary>
    public void ClearSavedKey()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                File.Delete(_storagePath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Tolérance idempotente si le fichier de clé est déjà supprimé ou verrouillé par l'OS
        }
    }

    /// <inheritdoc />
    public Task<string?> GetSavedKeyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetSavedKey());
    }

    /// <inheritdoc />
    public Task SaveKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveKey(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearSavedKeyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClearSavedKey();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> HasSavedKeyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(_storagePath) && !string.IsNullOrEmpty(GetSavedKey()));
    }

    /// <summary>
    /// Chiffre les octets fournis via AES-256 avec vecteur d'initialisation préfixé (mécanisme de repli hors Windows).
    /// </summary>
    /// <param name="plainBytes">Données en clair.</param>
    /// <returns>Données chiffrées préfixées par l'IV de 16 octets.</returns>
    protected static byte[] EncryptFallback(byte[] plainBytes)
    {
        ArgumentNullException.ThrowIfNull(plainBytes);
        using var aes = Aes.Create();
        aes.Key = SHA256.HashData(Entropy);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var cipherText = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Préfixer le vecteur d'initialisation (IV) de 16 octets au texte chiffré
        var result = new byte[aes.IV.Length + cipherText.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherText, 0, result, aes.IV.Length, cipherText.Length);
        return result;
    }

    /// <summary>
    /// Déchiffre les octets fournis via AES-256 en extrayant le vecteur d'initialisation préfixé.
    /// </summary>
    /// <param name="cipherData">Données chiffrées préfixées par l'IV de 16 octets.</param>
    /// <returns>Données déchiffrées.</returns>
    protected static byte[] DecryptFallback(byte[] cipherData)
    {
        ArgumentNullException.ThrowIfNull(cipherData);
        using var aes = Aes.Create();
        aes.Key = SHA256.HashData(Entropy);

        var iv = new byte[16];
        if (cipherData.Length < iv.Length)
        {
            throw new CryptographicException("Données chiffrées invalides.");
        }

        Buffer.BlockCopy(cipherData, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        return decryptor.TransformFinalBlock(cipherData, iv.Length, cipherData.Length - iv.Length);
    }
}
