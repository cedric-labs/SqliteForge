using System.Reflection;
using AwesomeAssertions;
using SqliteForge.Configuration;
using Xunit;

namespace SqliteForge.Tests.Architecture;

public sealed class AssemblyVersionTests
{
    /// <summary>
    /// Les consommateurs compilés contre une version antérieure du package référencent sa version d'assembly :
    /// si elle retombait à Majeur.0.0.0 (comportement par défaut de MinVer), le runtime refuserait de la charger.
    /// </summary>
    [Fact]
    public void AssemblyVersion_WhenBuiltFromGitTag_MatchesThePackageVersion()
    {
        // Arrange
        var assembly = typeof(SqliteConfigurationProvider).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        informationalVersion.Should().NotBeNullOrWhiteSpace();
        var packageVersion = Version.Parse(informationalVersion!.Split('+', '-')[0]);

        // Act
        var assemblyVersion = assembly.GetName().Version;

        // Assert
        assemblyVersion.Should().Be(new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, 0));
    }
}
