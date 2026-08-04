using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace XFramework.Core.Tests.Security;

[TestFixture]
public sealed class LegacyTrustPathGuardTests
{
    [Test]
    public void Source_DoesNotUseLegacyMetadataTrustHelper()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenToken = "RequestMetadata" + "Trust";

        var matches = Directory
            .EnumerateFiles(repositoryRoot.FullName, "*.cs", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Where(path => File.ReadAllText(path).Contains(forbiddenToken, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot.FullName, path))
            .ToList();

        matches.Should().BeEmpty(
            "service-to-service trust must use IdentityServer-issued service tokens and the shared invocation resolver");
    }

    [Test]
    public void RequestMetadata_ContainsDiagnosticsAndRequestedTargetOnly()
    {
        typeof(RequestMetadata)
            .GetProperties()
            .Select(property => property.Name)
            .Should()
            .BeEquivalentTo(
                nameof(RequestMetadata.RequestId),
                nameof(RequestMetadata.DeviceName),
                nameof(RequestMetadata.UserAgent),
                nameof(RequestMetadata.IpAddress),
                nameof(RequestMetadata.OperationName),
                nameof(RequestMetadata.RequestedTenantId));
    }

    [Test]
    public void GeneratedRestEndpoints_AreProtectedByDefault()
    {
        new MapPostAttribute("/test").RequireAuthorization.Should().BeTrue();
        new MapGetAttribute("/test").RequireAuthorization.Should().BeTrue();
        new MapPutAttribute("/test").RequireAuthorization.Should().BeTrue();
        new MapPatchAttribute("/test").RequireAuthorization.Should().BeTrue();
        new MapDeleteAttribute("/test").RequireAuthorization.Should().BeTrue();
    }

    [Test]
    public void ProductionEndpoints_CannotDisableTrustedRestAuthorization()
    {
        var repositoryRoot = FindRepositoryRoot();
        var modulesRoot = Path.Combine(repositoryRoot.FullName, "src", "Modules");

        var matches = Directory
            .EnumerateFiles(modulesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Where(path => !IsTestProjectFile(path))
            .Where(path => Regex.IsMatch(
                File.ReadAllText(path),
                @"RequireAuthorization\s*=\s*false",
                RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(repositoryRoot.FullName, path))
            .ToList();

        matches.Should().BeEmpty(
            "public endpoints must use an explicit anonymous trusted-invocation policy instead of bypassing the pipeline");
    }

    [Test]
    public void BusinessModules_CannotConstructOrModifyTrustedInvocationContext()
    {
        var repositoryRoot = FindRepositoryRoot();
        var modulesRoot = Path.Combine(repositoryRoot.FullName, "src", "Modules");

        var matches = Directory
            .EnumerateFiles(modulesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Where(path => !IsTestProjectFile(path))
            .Where(path => !IsApprovedContextAuthorityFile(path))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains("ITrustedInvocationContextStore", StringComparison.Ordinal) ||
                       source.Contains("new TrustedInvocationContext(", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(repositoryRoot.FullName, path))
            .ToList();

        matches.Should().BeEmpty(
            "business modules may read trusted invocation context but only the centralized security pipeline may create or modify it");
    }

    [Test]
    public void BusinessModules_CannotDecodeOrValidateTokensLocally()
    {
        var repositoryRoot = FindRepositoryRoot();
        var modulesRoot = Path.Combine(repositoryRoot.FullName, "src", "Modules");
        string[] forbiddenTokens =
        [
            "DecodeJwtToken",
            "JwtSecurityTokenHandler",
            "TokenValidationParameters"
        ];

        var matches = Directory
            .EnumerateFiles(modulesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Where(path => !IsTestProjectFile(path))
            .Where(path => !IsApprovedIdentityAuthorityFile(path))
            .Where(path => forbiddenTokens.Any(token =>
                File.ReadAllText(path).Contains(token, StringComparison.Ordinal)))
            .Select(path => Path.GetRelativePath(repositoryRoot.FullName, path))
            .ToList();

        matches.Should().BeEmpty(
            "actor and service token validation must remain in the shared security pipeline and approved IdentityServer authority adapters");
    }

    [Test]
    public void ProductionServices_CannotRegisterServiceWrappersAsSingletons()
    {
        var repositoryRoot = FindRepositoryRoot();

        var matches = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Where(path => !IsTestProjectFile(path))
            .Where(path => Regex.IsMatch(
                File.ReadAllText(path),
                @"AddSingleton\s*(?:<[^>]*ServiceWrapper|\([^\r\n]*ServiceWrapper)",
                RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(repositoryRoot.FullName, path))
            .ToList();

        matches.Should().BeEmpty(
            "service wrappers carry scoped trusted invocation state and must not be promoted to singleton lifetime");
    }

    [Test]
    public void ProductionServices_RegisterMessageBusWrapperAsScoped()
    {
        var repositoryRoot = FindRepositoryRoot();
        var extensionPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Infrastructure",
            "XFramework.Integration",
            "Extensions",
            "ServiceCollectionExtensions.cs");
        var source = File.ReadAllText(extensionPath);

        source.Should().Contain("AddScoped<IMessageBusWrapper, BoltDriver>",
            "BoltDriver owns per-scope actor and service invocation state");
        source.Should().NotMatchRegex(
            @"AddSingleton\s*<\s*IMessageBusWrapper",
            "a singleton BoltDriver would leak trusted invocation state across callers");
    }

    [Test]
    public void HandwrittenProtectedEndpoints_InvokeSharedFeatureGate()
    {
        var repositoryRoot = FindRepositoryRoot();
        string[] relativePaths =
        [
            "src/Modules/XFramework.Storage/Storage.Api/Features/Sessions/UploadPart/Endpoint.cs",
            "src/Modules/XFramework.Wallets/Wallets.Api/Features/Wallets/Get/Endpoint.cs",
            "src/Modules/XFramework.Wallets/Wallets.Api/Features/Wallets/GetByCredential/Endpoint.cs",
            "src/Modules/XFramework.Wallets/Wallets.Api/Features/Batch/IncrementBatch/Endpoint.cs",
            "src/Modules/XFramework.Wallets/Wallets.Api/Features/Batch/DecrementBatch/Endpoint.cs",
            "src/Modules/XFramework.Wallets/Wallets.Api/Features/Batch/TransferBatch/Endpoint.cs",
            "src/Modules/XFramework.IdentityServer/IdentityServer.Api/Features/Credentials/Update/Endpoint.cs",
            "src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/Update/Endpoint.cs"
        ];

        foreach (var relativePath in relativePaths)
        {
            var source = File.ReadAllText(Path.Combine(
                repositoryRoot.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

            source.Should().Contain("IHttpTrustedInvocationAuthorizer", relativePath);
            source.Should().Contain("ITrustedInvocationFeatureGate", relativePath);
            source.Should().Contain("EnsureAllowedAsync(", relativePath);
        }
    }

    [Test]
    public void CrossTenantWriteScope_IsRestrictedToApprovedTenantAdministrationWorkflow()
    {
        var repositoryRoot = FindRepositoryRoot();
        var matches = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Where(path => !IsTestProjectFile(path))
            .Where(path => File.ReadAllText(path).Contains(
                "BeginTenantAdministrationScope",
                StringComparison.Ordinal))
            .Where(path => !path.EndsWith(
                Path.Combine("IdentityServer.Api", "Features", "Tenants", "TenantAdministrationService.cs"),
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith(
                Path.Combine("XFramework.Domain.Shared", "Security", "ICrossTenantWriteAuthorization.cs"),
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith(
                Path.Combine("XFramework.Integration", "Security", "CrossTenantWriteAuthorization.cs"),
                StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(repositoryRoot.FullName, path))
            .ToList();

        matches.Should().BeEmpty(
            "cross-tenant persistence authority is reserved for the approved IdentityServer tenant administration workflow");
    }

    [Test]
    public void Coins_RegistersCentralTrustedInvocationComposition()
    {
        var repositoryRoot = FindRepositoryRoot();
        var installerPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.Coins",
            "Server",
            "Coins.Api",
            "Installers",
            "WrapperInstaller.cs");
        var source = File.ReadAllText(installerPath);

        source.Should().Contain("AddXFrameworkBoltClient(");
        source.Should().Contain("AddIdentityServerSessionValidation()");
    }

    [Test]
    public void SensitiveSmsAgentEndpoints_RequireDedicatedServiceAuthorization()
    {
        var repositoryRoot = FindRepositoryRoot();
        string[] relativePaths =
        [
            "src/Modules/XFramework.SmsGateway/SmsGateway.Api/Features/Sms/GetPending/Endpoint.cs",
            "src/Modules/XFramework.SmsGateway/SmsGateway.Api/Features/Sms/GetScheduled/Endpoint.cs",
            "src/Modules/XFramework.SmsGateway/SmsGateway.Api/Features/Sms/CreateReceived/Endpoint.cs",
            "src/Modules/XFramework.SmsGateway/SmsGateway.Api/Features/Sms/ConfirmSent/Endpoint.cs",
            "src/Modules/XFramework.SmsGateway/SmsGateway.Api/Features/Sms/GetPendingWithStatus/Endpoint.cs"
        ];

        foreach (var relativePath in relativePaths)
        {
            var source = File.ReadAllText(Path.Combine(
                repositoryRoot.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

            source.Should().Contain("ActorRequirement = ActorRequirement.None", relativePath);
            source.Should().Contain("TenantAccessMode = TenantAccessMode.ServiceTargetTenant", relativePath);
            source.Should().Contain("XFrameworkServiceScopes.SmsGatewayAgent", relativePath);
            source.Should().Contain("XFrameworkServiceScopes.TenantTarget", relativePath);
            source.Should().Contain("AllowedServiceCallers = [XFrameworkServiceNames.SmsGateway]", relativePath);
        }
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XFramework.slnx")))
                return directory;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found from the test directory.");
    }

    private static bool IsSourceFile(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !segments.Contains("bin", StringComparer.OrdinalIgnoreCase) &&
               !segments.Contains("obj", StringComparer.OrdinalIgnoreCase) &&
               !segments.Contains(".git", StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsTestProjectFile(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase));

    private static bool IsApprovedContextAuthorityFile(string path) =>
        path.EndsWith(
            Path.Combine("IdentityServer.Api", "Infrastructure", "IdentitySessionJwtValidation.cs"),
            StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(
            Path.Combine("IdentityServer.Integration", "Extensions", "IdentitySessionValidationExtensions.cs"),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsApprovedIdentityAuthorityFile(string path) =>
        path.EndsWith(
            Path.Combine("IdentityServer.Api", "Infrastructure", "IdentityServerLocalActorIdentityProvider.cs"),
            StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(
            Path.Combine("IdentityServer.Api", "Services", "AuthService.cs"),
            StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(
            Path.Combine("IdentityServer.Api", "Services", "BoltTransportTokenSigner.cs"),
            StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(
            Path.Combine("IdentityServer.Api", "Services", "ServiceIdentityService.cs"),
            StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(
            Path.Combine("Bolt.Hub", "Installers", "BoltTransportAuthenticationInstaller.cs"),
            StringComparison.OrdinalIgnoreCase);
}
