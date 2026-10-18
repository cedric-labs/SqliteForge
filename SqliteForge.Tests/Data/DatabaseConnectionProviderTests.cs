using FluentAssertions;
using SqliteForge.Data;
using SqliteForge.Security;
using Xunit;

namespace SqliteForge.Tests.Data;

public sealed class DatabaseConnectionProviderTests
{
    [Fact]
    public void GetConnectionString_WhenNotEncrypted_ReturnsBaseConnectionString()
    {
        const string baseConn = "Data Source=test.db;Mode=ReadWriteCreate;";
        var keyHolder = new DatabaseKeyHolder();
        var provider = new DatabaseConnectionProvider(baseConn, keyHolder);

        var result = provider.GetConnectionString();

        result.Should().Be(baseConn);
        provider.IsDatabaseEncrypted().Should().BeFalse();
        provider.IsDatabaseLocked().Should().BeFalse();
    }

    [Fact]
    public void GetConnectionString_WhenEncryptedWithKey_AppendsPassword()
    {
        const string baseConn = "Data Source=test.db;Mode=ReadWriteCreate;";
        var keyHolder = new DatabaseKeyHolder();
        keyHolder.SetKey("SecretPassword!", isEncrypted: true);
        var provider = new DatabaseConnectionProvider(baseConn, keyHolder);

        var result = provider.GetConnectionString();

        result.Should().Contain("Password=SecretPassword!");
        provider.IsDatabaseEncrypted().Should().BeTrue();
        provider.IsDatabaseLocked().Should().BeFalse();
    }

    [Fact]
    public void IsDatabaseLocked_WhenEncryptedWithoutKey_ReturnsTrue()
    {
        const string baseConn = "Data Source=test.db;Mode=ReadWriteCreate;";
        var keyHolder = new DatabaseKeyHolder();
        keyHolder.SetKey(null, isEncrypted: true);
        var provider = new DatabaseConnectionProvider(baseConn, keyHolder);

        provider.IsDatabaseLocked().Should().BeTrue();
    }

    [Fact]
    public void GetDatabaseFilePath_WhenRelativePath_ReturnsFullPath()
    {
        const string baseConn = "Data Source=custom.db;";
        var keyHolder = new DatabaseKeyHolder();
        var provider = new DatabaseConnectionProvider(baseConn, keyHolder);

        var path = provider.GetDatabaseFilePath();

        path.Should().EndWith("custom.db");
        Path.IsPathRooted(path).Should().BeTrue();
    }

    [Fact]
    public void GetDatabaseFilePath_WhenMemorySource_ReturnsMemoryLiteral()
    {
        const string baseConn = "Data Source=:memory:;";
        var keyHolder = new DatabaseKeyHolder();
        var provider = new DatabaseConnectionProvider(baseConn, keyHolder);

        var path = provider.GetDatabaseFilePath();

        path.Should().Be(":memory:");
    }
}
