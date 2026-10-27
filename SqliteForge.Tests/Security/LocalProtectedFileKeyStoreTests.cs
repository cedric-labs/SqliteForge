using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using SqliteForge.Security;
using Xunit;

namespace SqliteForge.Tests.Security;

public sealed class LocalProtectedFileKeyStoreTests : IDisposable
{
    private readonly string _testKeyPath;

    public LocalProtectedFileKeyStoreTests()
    {
        _testKeyPath = Path.Combine(Path.GetTempPath(), $"sqliteforge_test_key_{Guid.NewGuid():N}.dat");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testKeyPath))
            {
                File.Delete(_testKeyPath);
            }
        }
        catch (IOException)
        {
            // Ignorer le verrou temporaire du système de fichiers lors du nettoyage
        }
    }

    [Fact]
    public async Task GetSavedKeyAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);

        var key = await store.GetSavedKeyAsync();

        key.Should().BeNull();
        (await store.HasSavedKeyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task SaveKeyAsync_WhenValidKeyProvided_RoundTripsCorrectly()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);
        const string expectedKey = "MySuperSecretKey#2026!";

        await store.SaveKeyAsync(expectedKey);

        (await store.HasSavedKeyAsync()).Should().BeTrue();
        var retrievedKey = await store.GetSavedKeyAsync();
        retrievedKey.Should().Be(expectedKey);
    }

    [Fact]
    public async Task ClearSavedKeyAsync_WhenKeyExists_RemovesStoredKey()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);
        await store.SaveKeyAsync("KeyToDelete");

        await store.ClearSavedKeyAsync();

        (await store.HasSavedKeyAsync()).Should().BeFalse();
        (await store.GetSavedKeyAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GetSavedKeyAsync_WhenFileCorrupted_ReturnsNullGracefully()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);
        await File.WriteAllBytesAsync(_testKeyPath, [0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02]);

        var key = await store.GetSavedKeyAsync();

        key.Should().BeNull();
    }

    [Fact]
    public Task SaveKeyAsync_WhenEmptyKeyProvided_ThrowsArgumentException()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);

        Func<Task> act = async () => await store.SaveKeyAsync("   ");

        return act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetSavedKeyAsync_WhenFileIsEmpty_ReturnsNull()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);
        await File.WriteAllBytesAsync(_testKeyPath, []);

        var key = await store.GetSavedKeyAsync();

        key.Should().BeNull();
    }

    [Fact]
    public void SaveKey_WhenCalledSynchronously_RoundTripsAndClearsKey()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);
        store.SaveKey("SyncKeyVal");
        store.GetSavedKey().Should().Be("SyncKeyVal");
        store.ClearSavedKey();
        store.GetSavedKey().Should().BeNull();
    }

    [Fact]
    public void EncryptFallback_WhenRoundTrippedWithDecryptFallback_RestoresOriginalBytes()
    {
        var encryptMethod = typeof(LocalProtectedFileKeyStore).GetMethod("EncryptFallback", BindingFlags.NonPublic | BindingFlags.Static);
        var decryptMethod = typeof(LocalProtectedFileKeyStore).GetMethod("DecryptFallback", BindingFlags.NonPublic | BindingFlags.Static);

        encryptMethod.Should().NotBeNull();
        decryptMethod.Should().NotBeNull();

        if (encryptMethod is null || decryptMethod is null)
        {
            return;
        }

        const string originalText = "CrossPlatformSecretPassphrase123!";
        var originalBytes = Encoding.UTF8.GetBytes(originalText);

        var encryptedBytes = encryptMethod.Invoke(null, [originalBytes]) as byte[];
        encryptedBytes.Should().NotBeNull();

        if (encryptedBytes is null)
        {
            return;
        }

        encryptedBytes.Length.Should().BeGreaterThan(originalBytes.Length);

        var decryptedBytes = decryptMethod.Invoke(null, [encryptedBytes]) as byte[];
        decryptedBytes.Should().NotBeNull();

        if (decryptedBytes is null)
        {
            return;
        }

        var decryptedText = Encoding.UTF8.GetString(decryptedBytes);
        decryptedText.Should().Be(originalText);
    }

    [Fact]
    public void DecryptFallback_WhenDataShorterThanIv_ThrowsCryptographicException()
    {
        var decryptMethod = typeof(LocalProtectedFileKeyStore).GetMethod("DecryptFallback", BindingFlags.NonPublic | BindingFlags.Static);
        decryptMethod.Should().NotBeNull();

        if (decryptMethod is null)
        {
            return;
        }

        byte[] shortBytes = [1, 2, 3];
        Action act = () =>
        {
            try
            {
                decryptMethod.Invoke(null, [shortBytes]);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        };

        act.Should().Throw<CryptographicException>();
    }
}
