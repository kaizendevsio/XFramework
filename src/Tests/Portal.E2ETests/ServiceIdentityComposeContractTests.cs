using FluentAssertions;

namespace Portal.E2ETests;

[TestFixture]
[Category("Kind:Integration")]
[Category("Area:ServiceIdentityContract")]
public sealed class ServiceIdentityComposeContractTests
{
    private static readonly string[] ServiceSecretVariables =
    [
        "IDENTITYSERVER_SERVICE_IDENTITY_SECRET",
        "BOLT_HUB_SERVICE_IDENTITY_SECRET",
        "COMMUNICATIONS_SERVICE_IDENTITY_SECRET",
        "NOTIFICATIONS_SERVICE_IDENTITY_SECRET",
        "STORAGE_SERVICE_IDENTITY_SECRET",
        "ATTENDANCE_SERVICE_IDENTITY_SECRET",
        "SMSGATEWAY_SERVICE_IDENTITY_SECRET",
        "WALLETS_SERVICE_IDENTITY_SECRET",
        "INVENTARIO_SERVICE_IDENTITY_SECRET",
        "POS_SERVICE_IDENTITY_SECRET",
        "PORTAL_SERVICE_IDENTITY_SECRET",
        "OPERATIONS_DASHBOARD_SERVICE_IDENTITY_SECRET"
    ];

    private static readonly string[] RegisteredServiceClients =
    [
        "XFramework.IdentityServer",
        "XFramework.Portal",
        "XFramework.Bolt.Hub",
        "XFramework.Communications",
        "XFramework.Notifications",
        "XFramework.Storage",
        "XFramework.Attendance",
        "XFramework.SmsGateway",
        "XFramework.Wallets",
        "XFramework.Inventario",
        "XFramework.POS",
        "XFramework.Operations.Dashboard"
    ];

    [Test]
    public void DockerCompose_UsesExplicitServiceIdentityClientsInsteadOfDevelopmentFallback()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "docker-compose.yml"));
        var envExample = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".env.example"));
        var serviceIdentityService = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.IdentityServer",
            "IdentityServer.Api",
            "Services",
            "ServiceIdentityService.cs"));

        compose.Should().NotContain("ServiceIdentity__DevelopmentClientSecret");
        compose.Should().NotContain("ServiceIdentity__AllowDevelopmentClientSecretFallback");
        compose.Should().NotContain("SERVICE_IDENTITY_DEVELOPMENT_SECRET");
        envExample.Should().NotContain("SERVICE_IDENTITY_DEVELOPMENT_SECRET");
        serviceIdentityService.Should().NotContain("DevelopmentClientSecret");
        serviceIdentityService.Should().NotContain("AllowDevelopmentClientSecretFallback");
        serviceIdentityService.Should().NotContain("AllowsDevelopmentClientFallback");

        foreach (var variable in ServiceSecretVariables)
        {
            compose.Should().Contain($"${{{variable}:?Set {variable} in .env}}");
            envExample.Should().Contain($"{variable}=");
        }

        foreach (var clientId in RegisteredServiceClients)
        {
            compose.Should().Contain($"ClientId: {clientId}");
        }

        compose.Should().Contain("x-service-identity-audiences:");
        compose.Should().Contain("x-service-identity-scopes:");
        compose.Should().Contain("ServiceIdentity__Clients__");
        compose.Should().Contain("AllowedAudiences: *service-identity-audiences");
        compose.Should().Contain("AllowedScopes: *service-identity-scopes");
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
