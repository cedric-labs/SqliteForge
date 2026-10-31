using System.Security.Cryptography;
using System.Text;

namespace SqliteForge.Security;

/// <summary>
/// Local protected storage of the SQLite key for the "Remember on this device" feature.
/// On Windows it uses DPAPI (<c>ProtectedData</c> with <c>DataProtectionScope.CurrentUser</c>).
/// On Linux and macOS it uses AES-256 encryption with a prefixed initialization vector.
/// </summary>
/// <param name="customPath">Optional custom path of the key file.</param>
/// <param name="applicationName">Name of the application subfolder in <c>LocalApplicationData</c> when <paramref name="customPath"/> is not given.</param>
public class LocalProtectedFileKeyStore(string? customPath = null, string applicationName = "Fluxa") : IDatabaseKeyStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Fluxa-Sqlite3MC-DeviceBinding-v1");
    private readonly string _storagePath = !string.IsNullOrEmpty(customPath)
        ? customPath
        : ResolveDefaultStoragePath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), applicationName, AppContext.BaseDirectory);

    /// <summary>
    /// Resolves the default key file path.
    /// </summary>
    /// <remarks>
    /// A service account without a home directory gets an empty <c>LocalApplicationData</c>, which would turn the path into a
    /// relative one that can collide with the application's own files (an executable named like the application folder).
    /// The key file then sits next to the application, under a name that cannot collide.
    /// </remarks>
    internal static string ResolveDefaultStoragePath(string localApplicationData, string applicationName, string baseDirectory) =>
        string.IsNullOrWhiteSpace(localApplicationData)
            ? Path.Combine(baseDirectory, "dbkey.dat")
            : Path.Combine(
                localApplicationData,
                string.IsNullOrWhiteSpace(applicationName) ? "SqliteForge" : applicationName,
                "dbkey.dat");

    /// <summary>
    /// Synchronously gets the saved passphrase, or <c>null</c> when it is missing or cannot be decrypted.
    /// </summary>
    /// <returns>The decrypted passphrase, or <c>null</c>.</returns>
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
            // When the key cannot be read back (for example a reinstalled machine or a corrupted key),
            // null makes the caller ask for the passphrase again.
            return null;
        }
    }

    /// <summary>
    /// Synchronously saves the passphrase on this device.
    /// </summary>
    /// <param name="key">Passphrase to save.</param>
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
    /// Synchronously deletes the passphrase remembered on this device.
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
            // Deleting stays idempotent: a key file already gone or locked by the OS is not an error for the caller
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
    /// Encrypts the bytes with AES-256 and a prefixed initialization vector (fallback outside Windows).
    /// </summary>
    /// <param name="plainBytes">Plain data.</param>
    /// <returns>Encrypted data prefixed with the 16-byte IV.</returns>
    protected static byte[] EncryptFallback(byte[] plainBytes)
    {
        ArgumentNullException.ThrowIfNull(plainBytes);
        using var aes = Aes.Create();
        aes.Key = SHA256.HashData(Entropy);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var cipherText = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // The 16-byte initialization vector (IV) is stored in front of the cipher text
        var result = new byte[aes.IV.Length + cipherText.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherText, 0, result, aes.IV.Length, cipherText.Length);
        return result;
    }

    /// <summary>
    /// Decrypts the bytes with AES-256, reading the prefixed initialization vector.
    /// </summary>
    /// <param name="cipherData">Encrypted data prefixed with the 16-byte IV.</param>
    /// <returns>Decrypted data.</returns>
    protected static byte[] DecryptFallback(byte[] cipherData)
    {
        ArgumentNullException.ThrowIfNull(cipherData);
        using var aes = Aes.Create();
        aes.Key = SHA256.HashData(Entropy);

        var iv = new byte[16];
        if (cipherData.Length < iv.Length)
        {
            throw new CryptographicException("Invalid encrypted data.");
        }

        Buffer.BlockCopy(cipherData, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        return decryptor.TransformFinalBlock(cipherData, iv.Length, cipherData.Length - iv.Length);
    }
}
