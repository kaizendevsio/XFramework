using FluentAssertions;
using XFramework.Domain.Shared.ServiceIdentity;

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
    public void Repository_IgnoresGeneratedJwtKeyDirectories()
    {
        var repositoryRoot = FindRepositoryRoot();
        var gitIgnore = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".gitignore"));

        gitIgnore.Should().Contain("**/.data/",
            "development JWT keys must never be staged from module working directories");
        Directory.GetFiles(repositoryRoot.FullName, "*.pem", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}.data{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Should().BeEmpty("generated JWT key material must not remain in the repository worktree");
    }

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
        serviceIdentityService.Should().NotContain("DevelopmentClientSecret");
        serviceIdentityService.Should().NotContain("AllowDevelopmentClientSecretFallback");
        serviceIdentityService.Should().NotContain("AllowsDevelopmentClientFallback");

        foreach (var variable in ServiceSecretVariables)
        {
            compose.Should().Contain($"${{{variable}:?Set {variable} in .env}}");
            envExample.Should().Contain($"{variable}=");
            deployWorkflow.Should().Contain($"{variable}: compose-validation-placeholder");
            deployWorkflow.Should().Contain(variable);
        }

        foreach (var clientId in RegisteredServiceClients)
        {
            compose.Should().Contain($"ClientId: {clientId}");
        }

        compose.Should().Contain("x-service-identity-audiences:");
        compose.Should().Contain("ServiceIdentity__Clients__");
        compose.Should().Contain("AllowedAudiences: *service-identity-audiences");
        compose.Should().Contain("AllowedScopes: bolt.service");
        ExtractAllowedScopesForClient(compose, XFrameworkServiceNames.Portal).Should().BeEquivalentTo(
        [
            XFrameworkServiceScopes.BoltService,
            XFrameworkServiceScopes.DataContextQuery,
            XFrameworkServiceScopes.DataContextQueryAllTenants,
            XFrameworkServiceScopes.DataContextMutate,
            XFrameworkServiceScopes.TenantTarget,
            XFrameworkServiceScopes.IdentityAdmin,
            XFrameworkServiceScopes.IdentitySessionValidate
        ]);
        ExtractAllowedScopesForClient(compose, XFrameworkServiceNames.SmsGateway).Should().Contain(
        [
            XFrameworkServiceScopes.SmsGatewayAgent,
            XFrameworkServiceScopes.TenantTarget
        ]);
        ExtractAllowedScopesForClient(compose, XFrameworkServiceNames.Wallets).Should().Contain(
        [
            XFrameworkServiceScopes.TenantTarget,
            XFrameworkServiceScopes.WalletsAdmin,
            XFrameworkServiceScopes.DataContextQueryAllTenants
        ]);

        var wallets = ExtractService(compose, "wallets");
        wallets.Should().NotContain("ServiceIdentity__DefaultScopes__2:");
        wallets.Should().NotContain("ServiceIdentity__DefaultScopes__3:");
        wallets.Should().NotContain("ServiceIdentity__DefaultScopes__4:");
    }

    [Test]
    public void DockerCompose_CentralizesBoltTransportIdentityInIdentityServer()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "docker-compose.yml"));
        var envExample = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".env.example"));
        var dockerfile = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "Dockerfile"));
        var deployWorkflow = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            ".github",
            "workflows",
            "deploy-xeon-dev.yml"));
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
        commonEnvironment.Should().Contain(
            "BoltConfiguration__ServerUrls__0: ws://bolt-hub:8080/bolt/ws");
        commonEnvironment.Should().Contain("BoltConfiguration__RequireSecureTransport: false");
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
            if (!string.Equals(service, "identityserver", StringComparison.Ordinal))
                serviceBlock.Should().Contain("secrets: *identity-user-jwt-public-key-secret");
        }

        hub.Should().Contain(
            "BoltTransportAuthentication__MetadataAddress: " +
            "http://identityserver:8080/.well-known/openid-configuration");
        hub.Should().Contain("BoltTransportAuthentication__Issuer: XFramework.IdentityServer");
        hub.Should().Contain("BoltTransportAuthentication__Audience: XFramework.Bolt.Hub");
        hub.Should().Contain("BoltTransportAuthentication__RequireHttpsMetadata: false");
        hub.Should().Contain("ASPNETCORE_URLS: http://+:8080");
        hub.Should().Contain("Kestrel__Endpoints__Http__Url: http://0.0.0.0:8080");
        hub.Should().Contain("secrets: *identity-user-jwt-public-key-secret");

        identityServer.Should().Contain("ASPNETCORE_URLS: http://+:8080");
        identityServer.Should().Contain("Kestrel__Endpoints__Http__Url: http://0.0.0.0:8080");
        identityServer.Should().NotContain("Kestrel__Endpoints__Https");
        identityServer.Should().Contain(
            "TrustedProxyForwarding__KnownProxies__0: host.docker.internal");
        identityServer.Should().Contain("- \"host.docker.internal:host-gateway\"");
        identityServer.Should().Contain(
            "ServiceIdentity__BoltTransportTokenIssuer__SigningKeyPath: " +
            "/var/lib/xframework/identity/bolt-transport-signing-key.pem");
        identityServer.Should().Contain("- identity-keydata:/var/lib/xframework/identity");
        identityServer.Should().Contain("    secrets:");

        var hubPorts = ExtractSection(hub, "    ports:", "    depends_on:");
        hubPorts.Should().Contain(
            "- \"127.0.0.1:${BOLT_HUB_EXPOSE_PORT:-7000}:8080\"");
        var identityPorts = ExtractSection(identityServer, "    ports:", "    depends_on:");
        identityPorts.Should().Contain(
            "- \"127.0.0.1:${IDENTITYSERVER_EXPOSE_PORT:-8261}:8080\"");

        dockerfile.Should().Contain("EXPOSE 8080");
        dockerfile.Should().NotContain("EXPOSE 8080 8443");
        compose.Should().NotContain("Kestrel__Endpoints__Https");
        compose.Should().NotContain("identityserver-ca");
        compose.Should().NotContain("identityserver-tls");
        compose.Should().NotContain("/usr/local/share/ca-certificates");
        envExample.Should().NotContain("IDENTITYSERVER_TLS_CA_PATH");
        envExample.Should().NotContain("IDENTITYSERVER_TLS_FULLCHAIN_PATH");
        envExample.Should().NotContain("IDENTITYSERVER_TLS_PRIVATE_KEY_PATH");
        deployWorkflow.Should().NotContain("IDENTITYSERVER_TLS_CA_PATH:");
        deployWorkflow.Should().NotContain("IDENTITYSERVER_TLS_FULLCHAIN_PATH:");
        deployWorkflow.Should().NotContain("IDENTITYSERVER_TLS_PRIVATE_KEY_PATH:");
        envExample.Should().NotContain("IDENTITYSERVER_PUBLIC_HTTPS_PORT");
        envExample.Should().NotContain("BOLT_SYNTHETIC_IDENTITYSERVER_CA_PATH");
        envExample.Should().Contain("IDENTITYSERVER_EXPOSE_PORT=8261");
        envExample.Should().Contain(
            "BOLT_SYNTHETIC_IDENTITYSERVER_BASE_URL=https://xeon-dev.tailed40e.ts.net:8261");
        envExample.Should().Contain(
            "BOLT_SYNTHETIC_TARGET=wss://xeon-dev.tailed40e.ts.net:7000/bolt/ws");
        envExample.Should().Contain(
            "BOLT_SYNTHETIC_COMMUNICATIONS_IDENTITY_SERVICE_TOKEN_PATH=" +
            "./.secrets/bolt-phase0/communications-identity-service-token");
        envExample.Should().Contain(
            "BOLT_SYNTHETIC_PORTAL_IDENTITY_SERVICE_TOKEN_PATH=" +
            "./.secrets/bolt-phase0/portal-identity-service-token");
        envExample.Should().NotContain("BOLT_SYNTHETIC_PROXY_");
        envExample.Should().NotContain("BOLT_SYNTHETIC_PLAINTEXT_REJECTION_COMMAND_PATH");
        envExample.Should().NotContain("BOLT_SYNTHETIC_REDIS_INTERRUPTION_COMMAND_PATH");

        var operationsDashboard = ExtractService(compose, "operations-dashboard");
        operationsDashboard.Should().Contain(
            "BoltConfiguration__ServerUrls__0: ws://bolt-hub:8080/bolt/ws");
        operationsDashboard.Should().Contain("BoltConfiguration__RequireSecureTransport: false");
        var synthetics = ExtractService(compose, "bolt-phase0-synthetics");
        synthetics.Should().Contain(
            "BOLT_SYNTHETIC_TARGET: ${BOLT_SYNTHETIC_TARGET:" +
            "?Set the external Tailscale Serve WSS endpoint}");
        synthetics.Should().Contain(
            "BOLT_SYNTHETIC_COMMUNICATIONS_IDENTITY_SERVICE_TOKEN_FILE: " +
            "/run/secrets/bolt-synthetic-communications-identity-service-token");
        synthetics.Should().Contain(
            "BOLT_SYNTHETIC_PORTAL_IDENTITY_SERVICE_TOKEN_FILE: " +
            "/run/secrets/bolt-synthetic-portal-identity-service-token");

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

    [Test]
    public void UserJwtSigning_IsAsymmetricAndPrivateKeyIsOwnedOnlyByIdentityServer()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "docker-compose.yml"));
        var envExample = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".env.example"));
        var workflow = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            ".github",
            "workflows",
            "deploy-xeon-dev.yml"));
        var commonEnvironment = ExtractSection(compose, "x-common-env: &common-env", "services:");
        var identityServer = ExtractService(compose, "identityserver");

        compose.Should().NotContain("JwtOptions__Secret");
        compose.Should().NotContain("JWT_SECRET");
        envExample.Should().NotContain("JWT_SECRET");
        commonEnvironment.Should().Contain("JwtOptions__SigningPublicKeyPath");
        commonEnvironment.Should().NotContain("JwtOptions__SigningPrivateKeyPath");
        identityServer.Should().Contain(
            "JwtOptions__SigningPrivateKeyPath: /run/secrets/identity-user-jwt-private-key.pem");
        identityServer.Should().Contain("identity-user-jwt-private-key");

        foreach (var service in CentralIdentityServices.Where(service => service != "identityserver"))
        {
            ExtractService(compose, service).Should().NotContain("JwtOptions__SigningPrivateKeyPath");
        }

        envExample.Should().Contain("USER_JWT_GENERATION_ID=");
        envExample.Should().Contain("SERVICE_CREDENTIAL_GENERATION_ID=");
        envExample.Should().Contain("SERVICE_CREDENTIAL_SECONDARY_GENERATION_ID=");
        envExample.Should().Contain("SERVICE_CREDENTIAL_SECONDARY_VALID_UNTIL_UTC=");
        envExample.Should().NotContain("\nCREDENTIAL_GENERATION_ID=");
        envExample.Should().NotContain("\nCREDENTIAL_SECONDARY_GENERATION_ID=");
        envExample.Should().NotContain("\nCREDENTIAL_SECONDARY_VALID_UNTIL_UTC=");
        compose.Should().NotContain("${CREDENTIAL_GENERATION_ID");
        compose.Should().NotContain("${CREDENTIAL_SECONDARY_GENERATION_ID");
        compose.Should().NotContain("${CREDENTIAL_SECONDARY_VALID_UNTIL_UTC");
        workflow.Should().Contain("SERVICE_CREDENTIAL_GENERATION_ID is missing or invalid");
        envExample.Should().Contain("IDENTITY_USER_JWT_PUBLIC_KEY_PATH=");
        envExample.Should().Contain("IDENTITY_USER_JWT_PRIVATE_KEY_PATH=");
        workflow.Should().Contain("Identity user JWT public and private keys do not match");
        workflow.Should().Contain("openssl\", \"pkey");
    }

    [Test]
    public void DeploymentSmoke_ExercisesExternalTailscaleIngressWithoutLegacyTlsMachinery()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "docker-compose.yml"));
        var envExample = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".env.example"));
        var workflow = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            ".github",
            "workflows",
            "deploy-xeon-dev.yml"));
        var syntheticRunner = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Tools",
            "XFramework.Bolt.Phase0Synthetics",
            "BoltPhase0SyntheticRunner.cs"));

        compose.Should().Contain(
            "BOLT_SYNTHETIC_TARGET: ${BOLT_SYNTHETIC_TARGET:" +
            "?Set the external Tailscale Serve WSS endpoint}");
        envExample.Should().Contain(
            "BOLT_SYNTHETIC_TARGET=wss://xeon-dev.tailed40e.ts.net:7000/bolt/ws");
        workflow.Should().Contain("if not hub_url.startswith(\"https://\"):");
        workflow.Should().Contain(
            "hub_websocket_url = f\"wss://{hub_url.removeprefix('https://').rstrip('/')}" +
            "/bolt/ws\"");
        workflow.Should().Contain("\"BOLT_SYNTHETIC_TARGET\": hub_websocket_url");
        workflow.Should().Contain("Verify diagnostic sink marker absence");
        workflow.Should().Contain("verify-bolt-phase0-diagnostic-sinks.py");
        workflow.Should().Contain("--seq-base-url");
        workflow.Should().Contain("--jaeger-base-url");
        syntheticRunner.Should().Contain("SendAccessTokenAsQueryString = true");

        workflow.Should().NotContain("run-bolt-phase0-synthetics.sh");
        workflow.Should().NotContain("manage-bolt-phase0-deployment-lease.py");
        workflow.Should().NotContain("verify-bolt-phase0-tls.sh");
    }

    [Test]
    public void DeploymentWorkflow_PreservesExactRollbackStateWithoutPartialDeploymentWrappers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            ".github",
            "workflows",
            "deploy-xeon-dev.yml"));
        workflow.Should().Contain("src/Tools/**");
        workflow.Should().Contain("src/Modules/**");
        workflow.Should().Contain("src/Presentation/**");
        workflow.Should().Contain("for service in \"${services[@]}\"; do");
        workflow.Should().Contain("docker compose -f \"$COMPOSE_FILE_PATH\" build \"$service\"");
        workflow.Should().NotContain(
            "docker compose -f \"$COMPOSE_FILE_PATH\" build \"${services[@]}\"");
        workflow.Should().Contain("REMOTE_ACTIVE_ENV:");
        workflow.Should().Contain("runtime_snapshot=\"$REMOTE_RUN_DIR/pre-deployment\"");
        workflow.Should().Contain("runtime_snapshot_ready=false");
        workflow.Should().Contain("if ! image_id=\"$(docker inspect --format '{{.Image}}'");
        workflow.Should().Contain("! docker image inspect \"$image_id\" >/dev/null 2>&1; then");
        workflow.Should().Contain("if [ \"$runtime_snapshot_ready\" = true ]; then");
        workflow.Should().Contain("postgres redis minio seq jaeger identityserver bolt-hub");
        workflow.Should().Contain("docker inspect --format '{{.Image}}'");
        workflow.Should().Contain("docker image inspect \"$value\"");
        workflow.Should().Contain("docker pull \"$value\"");
        workflow.Should().Contain("\"--profile\", \"phase0-verification\", \"--env-file\", env_file");
        workflow.Should().Contain("os.replace(temporary, path)");
        workflow.Should().Contain("$REMOTE_RUN_DIR/legacy-previous.env");
        workflow.Should().Contain("compose_tmp=\"${REMOTE_COMPOSE_FILE}.tmp.$$\"");
        workflow.Should().Contain("trap 'stop_hub; exit 1' ERR");
        workflow.Should().NotContain(
            "install -m 600 \"$release/docker-compose.yml\" \"$REMOTE_COMPOSE_FILE\"");
        workflow.Should().NotContain("xframework-bolt-phase0-root ensure-watchdog");
        workflow.Should().NotContain("xframework-bolt-phase0-root verify-bootstrap");
        workflow.Should().Contain("systemctl is-active xframework-bolt-phase0-watchdog.service");
        workflow.Should().Contain("flock -n 9");
        workflow.Should().Contain("No complete previous release is available; transition is blocked before mutation.");
        workflow.Should().NotContain("APPROVE_BOLT_TAILSCALE_TRANSITION");
        workflow.Should().NotContain("xframework.bolt.transition-acceptance.v1");
        workflow.Should().NotContain("tailscale_acl_applied");

        workflow.Should().NotContain("logs --tail=200");
        workflow.Should().NotContain("deploy-xeon-dev-service.yml");

        var removedWrappers = new[]
        {
            "deploy-xeon-dev-service.yml",
            "deploy-xeon-dev-attendance.yml",
            "deploy-xeon-dev-inventario.yml",
            "deploy-xeon-dev-notifications.yml",
            "deploy-xeon-dev-operations-dashboard.yml",
            "deploy-xeon-dev-portal.yml",
            "deploy-xeon-dev-pos.yml",
            "deploy-xeon-dev-smsgateway.yml",
            "deploy-xeon-dev-storage.yml",
            "deploy-xeon-dev-wallets.yml"
        };
        foreach (var wrapper in removedWrappers)
        {
            File.Exists(Path.Combine(repositoryRoot.FullName, ".github", "workflows", wrapper))
                .Should().BeFalse();
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

    private static IReadOnlySet<string> ExtractAllowedScopesForClient(
        string compose,
        string clientId)
    {
        var lines = NormalizeLines(compose).Split('\n');
        var clientIndex = Array.FindIndex(
            lines,
            line => line.Trim().EndsWith($"ClientId: {clientId}", StringComparison.Ordinal));
        if (clientIndex < 0)
            throw new InvalidOperationException($"Could not locate service identity client '{clientId}'.");

        var scopesLine = lines
            .Skip(clientIndex + 1)
            .TakeWhile(line => !line.TrimStart().Contains("ClientId:", StringComparison.Ordinal))
            .FirstOrDefault(line => line.TrimStart().Contains("AllowedScopes:", StringComparison.Ordinal));
        if (scopesLine is null)
            throw new InvalidOperationException($"Could not locate allowed scopes for '{clientId}'.");

        return scopesLine[(scopesLine.IndexOf("AllowedScopes:", StringComparison.Ordinal) + "AllowedScopes:".Length)..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
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
