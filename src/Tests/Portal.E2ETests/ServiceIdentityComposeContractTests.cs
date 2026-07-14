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

    private static readonly string[] IdentityDependentServices =
    [
        "bolt-hub",
        "communications",
        "notifications",
        "storage",
        "attendance",
        "smsgateway",
        "wallets",
        "inventario",
        "pos",
        "portal",
        "operations-dashboard",
        "bolt-phase0-synthetics"
    ];

    private static readonly string[] CentralIdentityServices =
    [
        "bolt-hub",
        "identityserver",
        "communications",
        "notifications",
        "storage",
        "attendance",
        "smsgateway",
        "wallets",
        "inventario",
        "pos",
        "portal",
        "operations-dashboard"
    ];

    [Test]
    public void DockerCompose_UsesExplicitServiceIdentityClientsInsteadOfDevelopmentFallback()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "docker-compose.yml"));
        var envExample = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".env.example"));
        var deployWorkflow = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            ".github",
            "workflows",
            "deploy-xeon-dev.yml"));
        var serviceDeployWorkflow = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            ".github",
            "workflows",
            "deploy-xeon-dev-service.yml"));
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
        deployWorkflow.Should().NotContain("SERVICE_IDENTITY_DEVELOPMENT_SECRET: compose-validation-placeholder");
        serviceDeployWorkflow.Should().NotContain("SERVICE_IDENTITY_DEVELOPMENT_SECRET: compose-validation-placeholder");
        serviceIdentityService.Should().NotContain("DevelopmentClientSecret");
        serviceIdentityService.Should().NotContain("AllowDevelopmentClientSecretFallback");
        serviceIdentityService.Should().NotContain("AllowsDevelopmentClientFallback");

        foreach (var variable in ServiceSecretVariables)
        {
            compose.Should().Contain($"${{{variable}:?Set {variable} in .env}}");
            envExample.Should().Contain($"{variable}=");
            deployWorkflow.Should().Contain($"{variable}: compose-validation-placeholder");
            serviceDeployWorkflow.Should().Contain($"{variable}: compose-validation-placeholder");
            deployWorkflow.Should().Contain(variable);
            serviceDeployWorkflow.Should().Contain(variable);
        }

        foreach (var clientId in RegisteredServiceClients)
        {
            compose.Should().Contain($"ClientId: {clientId}");
        }

        compose.Should().Contain("x-service-identity-audiences:");
        compose.Should().Contain("ServiceIdentity__Clients__");
        compose.Should().Contain("AllowedAudiences: *service-identity-audiences");
        compose.Should().Contain("AllowedScopes: bolt.service");
        compose.Should().Contain("AllowedScopes: bolt.service,datacontext.query,datacontext.mutate,identity.admin");
    }

    [Test]
    public void DockerCompose_CentralizesBoltTransportIdentityInIdentityServer()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "docker-compose.yml"));
        var envExample = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".env.example"));
        var commonEnvironment = ExtractSection(compose, "x-common-env: &common-env", "services:");
        var hub = ExtractService(compose, "bolt-hub");
        var identityServer = ExtractService(compose, "identityserver");

        compose.Should().NotContain("BoltConfiguration__Signature");
        compose.Should().NotContain("BOLT_SIGNATURE");
        envExample.Should().Contain(
            "# Rollback-only until the first centralized-identity LKG is sealed. " +
            "New services do not receive this value.");
        envExample.Should().Contain("BOLT_SIGNATURE=change-me-legacy-rollback-");

        commonEnvironment.Should().Contain("BoltConfiguration__GenerateServiceAccessToken: false");
        commonEnvironment.Should().Contain("ServiceIdentity__Authority: http://identityserver:8080");
        commonEnvironment.Should().Contain("ServiceIdentity__AllowInsecureHttp: true");
        foreach (var service in CentralIdentityServices)
        {
            var serviceBlock = ExtractService(compose, service);
            var hasEffectiveCentralIdentity = serviceBlock.Contains("      <<: *common-env", StringComparison.Ordinal)
                || (serviceBlock.Contains(
                        "BoltConfiguration__GenerateServiceAccessToken: false",
                        StringComparison.Ordinal)
                    && serviceBlock.Contains(
                        "ServiceIdentity__Authority: http://identityserver:8080",
                        StringComparison.Ordinal)
                    && serviceBlock.Contains(
                        "ServiceIdentity__AllowInsecureHttp: true",
                        StringComparison.Ordinal));
            hasEffectiveCentralIdentity.Should().BeTrue(
                "Compose service {0} must receive the common central identity configuration",
                service);
        }

        hub.Should().Contain(
            "BoltTransportAuthentication__MetadataAddress: " +
            "http://identityserver:8080/.well-known/openid-configuration");
        hub.Should().Contain("BoltTransportAuthentication__Issuer: XFramework.IdentityServer");
        hub.Should().Contain("BoltTransportAuthentication__Audience: XFramework.Bolt.Hub");
        hub.Should().Contain("BoltTransportAuthentication__RequireHttpsMetadata: false");

        identityServer.Should().Contain("Kestrel__Endpoints__Http__Url: http://0.0.0.0:8080");
        identityServer.Should().Contain(
            "ServiceIdentity__BoltTransportTokenIssuer__SigningKeyPath: " +
            "/var/lib/xframework/identity/bolt-transport-signing-key.pem");
        identityServer.Should().Contain("- identity-keydata:/var/lib/xframework/identity");

        var identityPorts = ExtractSection(identityServer, "    ports:", "    depends_on:");
        identityPorts.Should().Contain(":8443");
        identityPorts.Should().NotContain("8080");

        var identityDependencies = ExtractSection(
            identityServer,
            "    depends_on:",
            "    healthcheck:");
        identityDependencies.Should().Contain(
            "      migrate:\n        condition: service_completed_successfully");
        identityDependencies.Should().Contain(
            "      postgres:\n        condition: service_healthy");
        identityDependencies.Should().NotContain("      bolt-hub:");

        foreach (var service in IdentityDependentServices)
        {
            ExtractService(compose, service).Should().Contain(
                "      identityserver:\n        condition: service_healthy");
        }
    }

    private static string ExtractService(string compose, string service)
    {
        var lines = NormalizeLines(compose).Split('\n');
        var marker = $"  {service}:";
        var start = Array.FindIndex(lines, line => line == marker);
        if (start < 0)
            throw new InvalidOperationException($"Could not locate Compose service '{service}'.");

        var end = Array.FindIndex(
            lines,
            start + 1,
            line => line.StartsWith("  ", StringComparison.Ordinal)
                && !line.StartsWith("    ", StringComparison.Ordinal)
                && line.EndsWith(':'));
        if (end < 0)
            end = lines.Length;

        return string.Join('\n', lines[start..end]);
    }

    private static string ExtractSection(string text, string startMarker, string endMarker)
    {
        var normalized = NormalizeLines(text);
        var start = normalized.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException($"Could not locate section '{startMarker}'.");

        var end = normalized.IndexOf($"\n{endMarker}", start, StringComparison.Ordinal);
        if (end < 0)
            throw new InvalidOperationException($"Could not locate section terminator '{endMarker}'.");

        return normalized[start..end];
    }

    private static string NormalizeLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

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
