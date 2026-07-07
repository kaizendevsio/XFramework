using Bolt.Hub.Installers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class BoltHubConfigurationTests
{
    [Test]
    public void ResolveConnectionString_UsesTopLevelDefaultDatabaseConnection()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["DefaultDatabaseConnection"] = "Host=compose;Database=XFramework"
        });

        DbInstaller.ResolveConnectionString(configuration)
            .Should().Be("Host=compose;Database=XFramework");
    }

    [Test]
    public void ResolveConnectionString_FallsBackToConnectionStringsDatabaseConnection()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DatabaseConnection"] = "Host=localhost;Database=XFramework"
        });

        DbInstaller.ResolveConnectionString(configuration)
            .Should().Be("Host=localhost;Database=XFramework");
    }

    [Test]
    public void ResolveConnectionString_ThrowsWhenMissing()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        var act = () => DbInstaller.ResolveConnectionString(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*DefaultDatabaseConnection*ConnectionStrings:DatabaseConnection*");
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
