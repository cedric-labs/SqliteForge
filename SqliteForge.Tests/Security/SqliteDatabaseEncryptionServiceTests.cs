using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SqliteForge.Data;
using SqliteForge.Security;
using Xunit;

namespace SqliteForge.Tests.Security;

public sealed class SqliteDatabaseEncryptionServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testKeyPath;
    private readonly string _connectionString;
    private readonly DatabaseKeyHolder _keyHolder;
    private readonly LocalProtectedFileKeyStore _keyStore;
    private readonly DatabaseConnectionProvider _connectionProvider;
    private readonly ServiceProvider _serviceProvider;
    private readonly SqliteDatabaseEncryptionService _service;

    public SqliteDatabaseEncryptionServiceTests()
    {
        SQLitePCL.Batteries_V2.Init();
        _testDbPath = Path.Combine(Path.GetTempPath(), $"sqliteforge_enc_svc_{Guid.NewGuid():N}.db");
        _testKeyPath = Path.Combine(Path.GetTempPath(), $"sqliteforge_enc_key_{Guid.NewGuid():N}.dat");
        _connectionString = $"Data Source={_testDbPath};Mode=ReadWriteCreate;Foreign Keys=True;";

        _keyHolder = new DatabaseKeyHolder();
        _keyStore = new LocalProtectedFileKeyStore(_testKeyPath);
        _connectionProvider = new DatabaseConnectionProvider(_connectionString, _keyHolder);

        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseKeyHolder>(_keyHolder);
        services.AddSingleton<IDatabaseConnectionProvider>(_connectionProvider);
        _serviceProvider = services.BuildServiceProvider();

        _service = new SqliteDatabaseEncryptionService(
            _connectionProvider,
            _keyHolder,
            _keyStore,
            _serviceProvider,
            NullLogger.Instance);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _serviceProvider.Dispose();
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }

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
    public void CheckIfFileEncrypted_WhenFileDoesNotExist_ReturnsFalse()
    {
        SqliteDatabaseEncryptionService.CheckIfFileEncrypted("non_existent_file.db").Should().BeFalse();
    }

    [Fact]
    public async Task EncryptDatabaseAsync_WhenFullLifecycleExecuted_PreservesDataAcrossRekeyAndDecrypt()
    {
        await using (var conn = new SqliteConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Users (Id INT, Name TEXT); INSERT INTO Users VALUES (1, 'Alice');";
            await cmd.ExecuteNonQueryAsync();
        }

        SqliteConnection.ClearAllPools();
        SqliteDatabaseEncryptionService.CheckIfFileEncrypted(_testDbPath).Should().BeFalse();
        _service.IsDatabaseEncrypted().Should().BeFalse();

        const string pass1 = "MasterPassphrase123!";
        var (encSuccess, encError) = await _service.EncryptDatabaseAsync(pass1, rememberOnDevice: true);
        encSuccess.Should().BeTrue();
        encError.Should().BeNull();

        SqliteDatabaseEncryptionService.CheckIfFileEncrypted(_testDbPath).Should().BeTrue();
        _service.IsDatabaseEncrypted().Should().BeTrue();
        (await _service.IsRememberOnDeviceEnabledAsync()).Should().BeTrue();

        _service.VerifyPassphrase(pass1).Should().BeTrue();
        _service.VerifyPassphrase("WrongPassword").Should().BeFalse();

        const string pass2 = "UpdatedPassphrase456!";
        var (rekeySuccess, rekeyError) = await _service.ChangePassphraseAsync(pass1, pass2, rememberOnDevice: false);
        rekeyError.Should().BeNull();
        rekeySuccess.Should().BeTrue();

        _service.VerifyPassphrase(pass2).Should().BeTrue();
        _service.VerifyPassphrase(pass1).Should().BeFalse();
        (await _service.IsRememberOnDeviceEnabledAsync()).Should().BeFalse();

        _keyHolder.SetKey(null, isEncrypted: true);
        _service.IsDatabaseLocked().Should().BeTrue();

        var unlockFail = await _service.UnlockDatabaseAsync("BadPassword", rememberOnDevice: false);
        unlockFail.Should().BeFalse();
        _service.IsDatabaseLocked().Should().BeTrue();

        var unlockSuccess = await _service.UnlockDatabaseAsync(pass2, rememberOnDevice: true);
        unlockSuccess.Should().BeTrue();
        _service.IsDatabaseLocked().Should().BeFalse();
        (await _service.IsRememberOnDeviceEnabledAsync()).Should().BeTrue();

        var (decSuccess, decError) = await _service.DecryptDatabaseToPlainAsync(pass2);
        decSuccess.Should().BeTrue();
        decError.Should().BeNull();

        SqliteDatabaseEncryptionService.CheckIfFileEncrypted(_testDbPath).Should().BeFalse();
        _service.IsDatabaseEncrypted().Should().BeFalse();
        (await _service.IsRememberOnDeviceEnabledAsync()).Should().BeFalse();

        await using (var conn = new SqliteConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Name FROM Users WHERE Id = 1;";
            var name = (string?)await cmd.ExecuteScalarAsync();
            name.Should().Be("Alice");
        }
    }

    [Fact]
    public async Task EncryptDatabaseAsync_WhenInvalidPassphraseProvided_ReturnsValidationError()
    {
        var (success1, error1) = await _service.EncryptDatabaseAsync("short", rememberOnDevice: false);
        success1.Should().BeFalse();
        error1.Should().Contain("8 caractères");

        var (success2, _) = await _service.EncryptDatabaseAsync(string.Empty, rememberOnDevice: false);
        success2.Should().BeFalse();

        var (success3, error3) = await _service.EncryptDatabaseAsync("LineBreak\nPass123", rememberOnDevice: false);
        success3.Should().BeFalse();
        error3.Should().Contain("retour à la ligne");
    }

    [Fact]
    public async Task SetRememberOnDeviceAsync_WhenToggled_SavesAndClearsKey()
    {
        _keyHolder.SetKey("ValidKey123!", isEncrypted: true);

        await _service.SetRememberOnDeviceAsync(true);
        (await _service.IsRememberOnDeviceEnabledAsync()).Should().BeTrue();

        await _service.SetRememberOnDeviceAsync(false);
        (await _service.IsRememberOnDeviceEnabledAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ChangePassphraseAsync_WhenPreconditionsFail_ReturnsErrorMessages()
    {
        var (s1, e1) = await _service.ChangePassphraseAsync("current", "short", false);
        s1.Should().BeFalse();
        e1.Should().Contain("n'est pas chiffrée");

        _keyHolder.SetKey("ValidOldPassword123!", isEncrypted: true);

        var (s2, e2) = await _service.ChangePassphraseAsync("WrongCurrentPassword", "ValidNewPassword123!", false);
        s2.Should().BeFalse();
        e2.Should().Contain("incorrecte");

        var (s3, e3) = await _service.DecryptDatabaseToPlainAsync("WrongPassphrase");
        s3.Should().BeFalse();
        e3.Should().Contain("incorrecte");

        var unlockEmpty = await _service.UnlockDatabaseAsync("   ", false);
        unlockEmpty.Should().BeFalse();
    }
}
