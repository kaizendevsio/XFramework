using Bolt.Hub.Installers;
using Bolt.Server;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using XFramework.Domain.Shared.ServiceIdentity;

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

    [Test]
    public void InstallServices_DefaultRegistrationIdentityBindingMode_UsesAudit()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());
        var services = new ServiceCollection();

        new BoltInstaller().InstallServices<BoltHubConfigurationTests>(
            services,
            configuration,
            new TestHostEnvironment());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<BoltServerOptions>()
            .RegistrationIdentityBindingMode.Should().Be(BoltRegistrationIdentityBindingMode.Audit);
        provider.GetRequiredService<BoltServerOptions>()
            .ReservedServiceNames.Should().Contain(XFrameworkServiceNames.IdentityServer);
        provider.GetRequiredService<BoltServerOptions>()
            .ReservedServiceNamePrefixes.Should().Contain("XFramework.");
    }

    [Test]
    public void InstallServices_RegistrationIdentityBindingModeEnforce_WiresBoltServerOption()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BoltConfiguration:RegistrationIdentityBindingMode"] = "Enforce"
        });
        var services = new ServiceCollection();

        new BoltInstaller().InstallServices<BoltHubConfigurationTests>(
            services,
            configuration,
            new TestHostEnvironment());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<BoltServerOptions>()
            .RegistrationIdentityBindingMode.Should().Be(BoltRegistrationIdentityBindingMode.Enforce);
    }

    [Test]
    public void InstallServices_InvalidRegistrationIdentityBindingMode_ThrowsClearMessage()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BoltConfiguration:RegistrationIdentityBindingMode"] = "Strict"
        });
        var services = new ServiceCollection();

        var act = () => new BoltInstaller().InstallServices<BoltHubConfigurationTests>(
            services,
            configuration,
            new TestHostEnvironment());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RegistrationIdentityBindingMode*Off*Audit*Enforce*");
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Bolt.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
