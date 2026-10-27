using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using SqliteForge.Configuration;
using Xunit;

namespace SqliteForge.Tests.Configuration;

public sealed class SqliteConfigurationProviderTests : IDisposable
{
    private readonly string _dbFilePath;
    private readonly string _connectionString;

    public SqliteConfigurationProviderTests()
    {
        SQLitePCL.Batteries_V2.Init();
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"sqliteforge_cfg_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbFilePath};";
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbFilePath))
        {
            try
            {
                File.Delete(_dbFilePath);
            }
            catch (IOException)
            {
                // Ignorer le verrou temporaire du système de fichiers lors du nettoyage
            }
        }
    }

    [Fact]
    public void Load_WhenEmptyDatabaseAndDefaultsProvided_CreatesAppSettingsTableAndSeedsDefaults()
    {
        var defaults = new[]
        {
            new SqliteSettingSeed("App:DefaultCurrency", "EUR", "Devise par défaut"),
            new SqliteSettingSeed("App:Name", "Fluxa", "Nom de l'application"),
            new SqliteSettingSeed("Security:MaxFailedAccessAttempts", "5", "Tentatives max")
        };

        var provider = new SqliteConfigurationProvider(_connectionString, defaults);
        provider.Load();

        provider.TryGet("App:DefaultCurrency", out var currency).Should().BeTrue();
        currency.Should().Be("EUR");

        provider.TryGet("App:Name", out var appName).Should().BeTrue();
        appName.Should().Be("Fluxa");

        provider.TryGet("Security:MaxFailedAccessAttempts", out var attempts).Should().BeTrue();
        attempts.Should().Be("5");
    }

    [Fact]
    public void Load_WhenCustomSettingsInserted_ReadsUpdatedValues()
    {
        var provider = new SqliteConfigurationProvider(_connectionString);
        provider.Load();

        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO AppSettings (Key, Value, Description, UpdatedAtUtc) VALUES ('Custom:FeatureX', 'enabled', 'Test feature', datetime('now'));";
            cmd.ExecuteNonQuery();
        }

        provider.Load();

        provider.TryGet("Custom:FeatureX", out var featureVal).Should().BeTrue();
        featureVal.Should().Be("enabled");
    }

    [Fact]
    public void AddSqliteConfiguration_WhenCalledOnConfigurationBuilder_LoadsSeededSettings()
    {
        var defaults = new[]
        {
            new SqliteSettingSeed("App:DefaultCurrency", "EUR", "Devise")
        };

        var configuration = new ConfigurationBuilder()
            .AddSqliteConfiguration(() => _connectionString, defaults)
            .Build();

        configuration["App:DefaultCurrency"].Should().Be("EUR");
    }
}
