using AwesomeAssertions;
using SqliteForge.Security;
using Xunit;

namespace SqliteForge.Tests.Security;

public sealed class DatabaseKeyHolderTests
{
    [Fact]
    public void IsUnlocked_WhenInitialStateNotEncrypted_ReturnsTrue()
    {
        var holder = new DatabaseKeyHolder();

        holder.EncryptionKey.Should().BeNull();
        holder.IsDatabaseEncrypted.Should().BeFalse();
        holder.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public void SetKey_WhenEncryptedWithoutKey_SetsLockedState()
    {
        var holder = new DatabaseKeyHolder();
        holder.SetKey(null, isEncrypted: true);

        holder.IsDatabaseEncrypted.Should().BeTrue();
        holder.IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public void SetKey_WhenEncryptedWithKey_SetsUnlockedState()
    {
        var holder = new DatabaseKeyHolder();
        holder.SetKey("MySecretKey123", isEncrypted: true);

        holder.EncryptionKey.Should().Be("MySecretKey123");
        holder.IsDatabaseEncrypted.Should().BeTrue();
        holder.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public void Clear_WhenKeyWasSet_ResetsKeyAndLocksDatabase()
    {
        var holder = new DatabaseKeyHolder();
        holder.SetKey("MySecretKey123", isEncrypted: true);
        holder.Clear();

        holder.EncryptionKey.Should().BeNull();
        holder.IsUnlocked.Should().BeFalse();
    }
}
