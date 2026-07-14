using System.Text.Json;
using Bolt.Hub.Installers;
using Bolt.Server;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using XFramework.Domain.Shared.Configurations;
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
    public void InstallServices_RegistersHubAuthorizationEntitiesInTheEfModel()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["DefaultDatabaseConnection"] = "Host=localhost;Database=XFramework;Username=test;Password=test"
        });
        var services = new ServiceCollection();

        new DbInstaller().InstallServices<BoltHubConfigurationTests>(
            services,
            configuration,
            new TestHostEnvironment());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<DbContext>().Model;

        var credential = model.FindEntityType("IdentityServer.Domain.Shared.Contracts.IdentityCredential");
        credential.Should().NotBeNull();
        var mappedCredential = credential!;
        mappedCredential.GetSchema().Should().Be("Identity");
        mappedCredential.GetTableName().Should().Be("IdentityCredential");

        var threadMember = model.FindEntityType("Communications.Domain.Shared.Contracts.MessageThreadMember");
        threadMember.Should().NotBeNull();
        var mappedThreadMember = threadMember!;
        mappedThreadMember.GetSchema().Should().Be("Communications");
        mappedThreadMember.GetTableName().Should().Be("MessageThreadMember");
    }

    [Test]
    public void BoltRegistrationIdentityBinding_DefaultsToEnforce()
    {
        var serverOptions = new BoltServerOptions();
        var configuration = new BoltConfiguration();

        serverOptions.RegistrationIdentityBindingMode
            .Should().Be(BoltRegistrationIdentityBindingMode.Enforce);
        configuration.RegistrationIdentityBindingMode
            .Should().Be(nameof(BoltRegistrationIdentityBindingMode.Enforce));
        serverOptions.MediaEnabled.Should().BeTrue();
        serverOptions.RequireSecureTransport.Should().BeFalse();
        serverOptions.MaxConnectionLifetimeSeconds.Should().Be(0);
        serverOptions.MaxFrameBytes.Should().Be(8 * 1024 * 1024);
        configuration.MediaEnabled.Should().BeFalse();
        configuration.MaxConnectionLifetimeSeconds.Should().Be(1800);
        configuration.MaxFrameBytes.Should().Be(8 * 1024 * 1024);
    }

    [Test]
    public void InstallServices_DefaultRegistrationIdentityBindingMode_UsesEnforce()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());
        var services = new ServiceCollection();

        new BoltInstaller().InstallServices<BoltHubConfigurationTests>(
            services,
            configuration,
            new TestHostEnvironment());

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<BoltServerOptions>();
        options.RegistrationIdentityBindingMode.Should().Be(BoltRegistrationIdentityBindingMode.Enforce);
        options.RequireSecureTransport.Should().BeFalse();
        options.MediaEnabled.Should().BeFalse();
        options.MaxPendingRpcCalls.Should().Be(1000);
        options.MaxPendingRpcCallsPerPrincipal.Should().Be(128);
        options.MaxConnectionsPerPrincipal.Should().Be(16);
        options.MaxActiveStreamsPerPrincipal.Should().Be(64);
        options.MaxMediaStreamsPerPrincipal.Should().Be(8);
        options.MaxSubscriptionsPerPrincipal.Should().Be(128);
        options.MaxDurableSubscribersPerTopic.Should().Be(128);
        options.MaxConnectionLifetimeSeconds.Should().Be(1800);
        options.ReservedServiceNames.Should().Contain(XFrameworkServiceNames.IdentityServer);
        options.ReservedServiceNamePrefixes.Should().Contain("XFramework.");
    }

    [Test]
    public void InstallServices_Development_BindsContainmentOptions()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BoltConfiguration:RequireSecureTransport"] = "true",
            ["BoltConfiguration:MediaEnabled"] = "true",
            ["BoltConfiguration:MaxPendingRpcCalls"] = "101",
            ["BoltConfiguration:MaxPendingRpcCallsPerPrincipal"] = "17",
            ["BoltConfiguration:MaxConnectionsPerPrincipal"] = "11",
            ["BoltConfiguration:MaxActiveStreamsPerPrincipal"] = "22",
            ["BoltConfiguration:MaxMediaStreamsPerPrincipal"] = "3",
            ["BoltConfiguration:MaxSubscriptionsPerPrincipal"] = "44",
            ["BoltConfiguration:MaxDurableSubscribersPerTopic"] = "55",
            ["BoltConfiguration:MaxConnectionLifetimeSeconds"] = "555",
            ["BoltConfiguration:RegistrationMigrationAllowances:0:AuthenticatedServiceName"] = "XFramework.Current",
            ["BoltConfiguration:RegistrationMigrationAllowances:0:ClientId"] = "legacy-id",
            ["BoltConfiguration:RegistrationMigrationAllowances:0:ClientName"] = "XFramework.Legacy",
            ["BoltConfiguration:RegistrationMigrationAllowances:0:ExpiresAtUtc"] =
                DateTimeOffset.UtcNow.AddHours(1).ToString("O")
        });
        var services = new ServiceCollection();

        new BoltInstaller().InstallServices<BoltHubConfigurationTests>(
            services,
            configuration,
            new TestHostEnvironment());

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<BoltServerOptions>();
        options.RequireSecureTransport.Should().BeTrue();
        options.MediaEnabled.Should().BeTrue();
        options.MaxPendingRpcCalls.Should().Be(101);
        options.MaxPendingRpcCallsPerPrincipal.Should().Be(17);
        options.MaxConnectionsPerPrincipal.Should().Be(11);
        options.MaxActiveStreamsPerPrincipal.Should().Be(22);
        options.MaxMediaStreamsPerPrincipal.Should().Be(3);
        options.MaxSubscriptionsPerPrincipal.Should().Be(44);
        options.MaxDurableSubscribersPerTopic.Should().Be(55);
        options.MaxConnectionLifetimeSeconds.Should().Be(555);
        options.RegistrationMigrationAllowances.Should().ContainSingle()
            .Which.ClientId.Should().Be("legacy-id");
    }

    [TestCase("Development", false)]
    [TestCase("Development", true)]
    [TestCase("Staging", false)]
    [TestCase("Staging", true)]
    [TestCase("Production", false)]
    [TestCase("Production", true)]
    public void InstallServices_RequireSecureTransport_UsesExplicitSetting(
        string environmentName,
        bool requireSecureTransport)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BoltConfiguration:RequireSecureTransport"] = requireSecureTransport.ToString(),
            ["BoltConfiguration:Durable:RedisConnectionString"] = "localhost:6379"
        });
        var services = new ServiceCollection();

        new BoltInstaller().InstallServices<BoltHubConfigurationTests>(
            services,
            configuration,
            new TestHostEnvironment { EnvironmentName = environmentName });

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<BoltServerOptions>()
            .RequireSecureTransport.Should().Be(requireSecureTransport);
    }

    [Test]
    public void InstallServices_BlankRegistrationIdentityBindingMode_UsesEnforce()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BoltConfiguration:RegistrationIdentityBindingMode"] = "   "
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

    [TestCase("Off")]
    [TestCase("Audit")]
    public void InstallServices_Development_AllowsCompatibilityBindingModes(string bindingMode)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BoltConfiguration:RegistrationIdentityBindingMode"] = bindingMode
        });
        var services = new ServiceCollection();

        new BoltInstaller().InstallServices<BoltHubConfigurationTests>(
            services,
            configuration,
            new TestHostEnvironment { EnvironmentName = Environments.Development });

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<BoltServerOptions>()
            .RegistrationIdentityBindingMode.Should().Be(Enum.Parse<BoltRegistrationIdentityBindingMode>(bindingMode));
    }

    [TestCase("Staging", "Off")]
    [TestCase("Staging", "Audit")]
    [TestCase("Production", "Off")]
    [TestCase("Production", "Audit")]
    public void InstallServices_NonDevelopment_RejectsCompatibilityBindingModes(
        string environmentName,
        string bindingMode)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BoltConfiguration:RegistrationIdentityBindingMode"] = bindingMode
        });
        var services = new ServiceCollection();

        var act = () => new BoltInstaller().InstallServices<BoltHubConfigurationTests>(
            services,
            configuration,
            new TestHostEnvironment { EnvironmentName = environmentName });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*RegistrationIdentityBindingMode*{bindingMode}*only in Development*{environmentName}*requires Enforce*");
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

    [Test]
    public void ContainerAppSettings_ExplicitlyUseInternalHttpTransport()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var environment in new[] { "Docker", "Staging" })
        {
            using var identitySettings = ReadAppSettings(
                repositoryRoot,
                "XFramework.IdentityServer",
                "IdentityServer.Api",
                environment);
            var identityBolt = identitySettings.RootElement.GetProperty("BoltConfiguration");
            identityBolt.GetProperty("ServerUrls")[0].GetString()
                .Should().Be("ws://bolt-hub:8080/bolt/ws");
            identityBolt.GetProperty("RequireSecureTransport").GetBoolean().Should().BeFalse();

            using var hubSettings = ReadAppSettings(
                repositoryRoot,
                "XFramework.Bolt",
                "Bolt.Hub",
                environment);
            var hubAuthentication = hubSettings.RootElement.GetProperty("BoltTransportAuthentication");
            hubAuthentication.GetProperty("MetadataAddress").GetString()
                .Should().Be("http://identityserver:8080/.well-known/openid-configuration");
            hubAuthentication.GetProperty("RequireHttpsMetadata").GetBoolean().Should().BeFalse();
            hubSettings.RootElement
                .GetProperty("BoltConfiguration")
                .GetProperty("RequireSecureTransport")
                .GetBoolean()
                .Should()
                .BeFalse();
        }
    }

    [Test]
    public void DockerBoltClients_UsePrivateServiceNameTransport()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dockerSettings = Directory
            .EnumerateFiles(
                Path.Combine(repositoryRoot, "src"),
                "appsettings.Docker.json",
                SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase));

        var clients = 0;
        foreach (var path in dockerSettings)
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(path));
            if (!settings.RootElement.TryGetProperty("BoltConfiguration", out var bolt) ||
                !bolt.TryGetProperty("ServerUrls", out var serverUrls))
                continue;

            clients++;
            serverUrls.GetArrayLength().Should().Be(1, because: path);
            serverUrls[0].GetString().Should().Be("ws://bolt-hub:8080/bolt/ws", because: path);
            bolt.GetProperty("RequireSecureTransport").GetBoolean().Should().BeFalse(because: path);
        }

        clients.Should().Be(11, "every deployed Docker Bolt client must be covered");
    }

    private static JsonDocument ReadAppSettings(
        string repositoryRoot,
        string module,
        string application,
        string environment)
    {
        var path = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            module,
            application,
            $"appsettings.{environment}.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XFramework.slnx")))
            directory = directory.Parent;

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Could not locate the XFramework repository root.");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Bolt.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
