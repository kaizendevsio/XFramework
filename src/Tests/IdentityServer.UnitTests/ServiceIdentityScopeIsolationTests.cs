using FluentAssertions;
using FluentAssertions.Execution;
using IdentityServer.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class ServiceIdentityScopeIsolationTests
{
    private const string ClientSecret = "least-privilege-service-credential-material-111111111111111111111111";

    private static readonly IReadOnlyDictionary<string, string[]> ScopeMatrix =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [XFrameworkServiceNames.IdentityServer] = [XFrameworkServiceScopes.BoltService],
            [XFrameworkServiceNames.Portal] =
            [
                XFrameworkServiceScopes.BoltService,
                XFrameworkServiceScopes.DataContextQuery,
                XFrameworkServiceScopes.DataContextMutate,
                XFrameworkServiceScopes.IdentityAdmin
            ],
            [XFrameworkServiceNames.BoltHub] = [XFrameworkServiceScopes.BoltService],
            [XFrameworkServiceNames.Communications] = [XFrameworkServiceScopes.BoltService],
            [XFrameworkServiceNames.Notifications] = [XFrameworkServiceScopes.BoltService],
            [XFrameworkServiceNames.Storage] = [XFrameworkServiceScopes.BoltService],
            [XFrameworkServiceNames.Attendance] = [XFrameworkServiceScopes.BoltService],
            [XFrameworkServiceNames.SmsGateway] = [XFrameworkServiceScopes.BoltService],
            [XFrameworkServiceNames.Wallets] = [XFrameworkServiceScopes.BoltService],
            [XFrameworkServiceNames.Inventario] = [XFrameworkServiceScopes.BoltService],
            [XFrameworkServiceNames.Pos] = [XFrameworkServiceScopes.BoltService],
            [XFrameworkServiceNames.OperationsDashboard] = [XFrameworkServiceScopes.BoltService]
        };

    private static readonly string[] PrivilegedScopes =
    [
        XFrameworkServiceScopes.DataContextMutate,
        XFrameworkServiceScopes.AttendanceAdmin,
        XFrameworkServiceScopes.CommunicationsAdmin,
        XFrameworkServiceScopes.CommunityAdmin,
        XFrameworkServiceScopes.IdentityAdmin,
        XFrameworkServiceScopes.InventarioAdmin,
        XFrameworkServiceScopes.NotificationsSend,
        XFrameworkServiceScopes.SmsGatewaySend,
        XFrameworkServiceScopes.StorageWrite,
        XFrameworkServiceScopes.WalletsAdmin
    ];

    [Test]
    public async Task IssueToken_EveryClientRejectsEveryUnassignedPrivilegedScope()
    {
        var service = CreateService();

        foreach (var (clientId, allowedScopes) in ScopeMatrix)
        {
            foreach (var privilegedScope in PrivilegedScopes.Except(allowedScopes, StringComparer.OrdinalIgnoreCase))
            {
                var result = await service.IssueTokenAsync(new IssueServiceTokenRequest
                {
                    ClientId = clientId,
                    ClientSecret = ClientSecret,
                    Audience = XFrameworkServiceNames.IdentityServer,
                    Scopes = [privilegedScope]
                });

                using var scope = new AssertionScope($"{clientId} requesting {privilegedScope}");
                result.IsSuccess.Should().BeFalse();
                result.StatusCode.Should().Be(403);
                result.Message.Should().Contain(privilegedScope);
            }
        }
    }

    private static ServiceIdentityService CreateService()
    {
        var values = new Dictionary<string, string?>
        {
            ["ServiceIdentity:Issuer"] = XFrameworkServiceNames.IdentityServer
        };
        var index = 0;
        foreach (var (clientId, scopes) in ScopeMatrix)
        {
            var prefix = $"ServiceIdentity:Clients:{index}";
            values[$"{prefix}:ClientId"] = clientId;
            values[$"{prefix}:GenerationId"] = "least-privilege-g1";
            values[$"{prefix}:ClientSecret"] = ClientSecret;
            for (var scopeIndex = 0; scopeIndex < scopes.Length; scopeIndex++)
                values[$"{prefix}:AllowedScopes:{scopeIndex}"] = scopes[scopeIndex];
            index++;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var serviceIdentityConfiguration = ServiceIdentityConfiguration.FromConfiguration(
            configuration,
            DateTimeOffset.UtcNow);

        return new ServiceIdentityService(
            new Mock<IDataContext>(MockBehavior.Strict).Object,
            configuration,
            serviceIdentityConfiguration,
            Mock.Of<IBoltTransportTokenSigner>(),
            TimeProvider.System,
            NullLogger<ServiceIdentityService>.Instance);
    }
}
