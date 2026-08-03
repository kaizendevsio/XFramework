using System.Text.Json;
using FluentAssertions;
using Npgsql;
using NUnit.Framework;

namespace IdentityServer.UnitTests;

[TestFixture]
[Category("Module:IdentityServer")]
[Category("Area:ConfigurationContract")]
public sealed class IdentityServerConfigurationContractTests
{
    [Test]
    public void DevelopmentSettings_DoNotCommitSecretsOrExtremeDatabasePoolSettings()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settingsPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.IdentityServer",
            "IdentityServer.Api",
            "appsettings.Development.json");
        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var root = document.RootElement;

        var boltSignature = root
            .GetProperty("BoltConfiguration")
            .GetProperty("Signature")
            .GetString();
        boltSignature.Should().Be("CHANGE_ME_BOLT_SIGNATURE_USE_ENVIRONMENT");

        var connectionString = root
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultDatabaseConnection")
            .GetString();
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        builder.Password.Should().Be("CHANGE_ME_DB_PASSWORD_USE_ENVIRONMENT");
        builder.MaxPoolSize.Should().BeLessThanOrEqualTo(100);
        builder.Timeout.Should().BeLessThanOrEqualTo(30);
        builder.CommandTimeout.Should().BeLessThanOrEqualTo(60);
    }

    [Test]
    public void Program_ProcessesTrustedForwardingBeforeEveryRateLimiter()
    {
        var repositoryRoot = FindRepositoryRoot();
        var programPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.IdentityServer",
            "IdentityServer.Api",
            "Program.cs");
        var program = File.ReadAllText(programPath);

        var forwardingIndex = program.IndexOf(
            "app.UseXFrameworkTrustedProxyForwarding();",
            StringComparison.Ordinal);
        var distributedLimiterIndex = program.IndexOf(
            "app.UseDistributedStrictSecurityRateLimiting();",
            StringComparison.Ordinal);
        var frameworkLimiterIndex = program.IndexOf(
            "app.UseXFrameworkRateLimiting();",
            StringComparison.Ordinal);

        forwardingIndex.Should().BeGreaterThanOrEqualTo(0);
        forwardingIndex.Should().BeLessThan(distributedLimiterIndex);
        forwardingIndex.Should().BeLessThan(frameworkLimiterIndex);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "XFramework.slnx")))
                return current;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate XFramework repository root.");
    }
}
