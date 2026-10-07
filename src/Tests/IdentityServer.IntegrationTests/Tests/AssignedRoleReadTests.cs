using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Integration.Drivers;
using Microsoft.EntityFrameworkCore;
using XFramework.Core.DataContext;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture, NonParallelizable]
[Category(TestCategories.Integration)]
[Category(TestCategories.IdentityServer)]
[Category(TestCategories.DataContext)]
public sealed class AssignedRoleReadTests : IntegrationTestBase
{
    [Test]
    public void AssignedRole_GeneratedReadPolicy_RequiresManagedTenantDelegationAndDisallowsMutation()
    {
        var registry = typeof(global::IdentityServer.Api.Features.Auth.ValidateSession.ValidateIdentitySessionEndpoint)
            .Assembly.GetType("XFramework.Core.DataContext.DataContextEntityRegistrations", true)!;
        var policies = (IReadOnlyCollection<GeneratedEntityAuthorizationPolicy>)registry
            .GetMethod("GetDataContextAuthorizationPolicies")!.Invoke(null, null)!;
        var rolePolicies = policies.Where(policy => policy.EntityTypeName == nameof(IdentityRole)).ToArray();

        var policy = rolePolicies.Should().ContainSingle().Subject;
        policy.Operation.Should().Be(GeneratedEntityOperation.Read);
        policy.TenantAccessMode.Should().Be(TenantAccessMode.DelegatedTenant);
        policy.RequiredCrossTenantActorCapabilities.Should().ContainSingle().Which.Should().Be("identity.tenants:manage");
        policy.RequiredCapability.Should().Be("identity.roles:view");
        policy.AllowRemoteQuery.Should().BeTrue();
        policy.AllowRemoteMutation.Should().BeFalse();
        policy.AllowServiceOnly.Should().BeFalse();
    }

    [Test]
    public async Task AssignedRole_PortalReadInSelectedTenant_ReturnsExistingRoleThroughBoltAndPostgreSql()
    {
        var seed = await SeedAssignedRole();
        using var services = new ServiceCollection()
            .AddSingleton<IIdentityServerServiceWrapper>(IntegrationTestFixture.ServiceWrapper).BuildServiceProvider();
        var dataContext = new RemoteDataContext(services, new RequestMetadata
        {
            RequestedTenantId = seed.TenantId, RequestId = Guid.NewGuid(), OperationName = "Portal"
        });
        var credentials = await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters().NoCache().Where(credential => credential.IdentityInfoId == seed.Credential.IdentityInfoId)
            .OrderByDescending(credential => credential.CreatedAt).Take(100).ToListAsync();
        var roles = new List<IdentityRole>();
        var credentialIds = credentials.Where(credential => credential.TenantId == seed.TenantId)
            .Select(credential => credential.Id).ToArray();

        // Match UserDetail.LoadRoles, including its administrative scopes, batch filter, and Include.
        foreach (var credentialBatch in credentialIds.Chunk(50))
        {
            roles.AddRange(await dataContext.Query<IdentityRole>()
                .IgnoreQueryFilters().NoCache()
                .Where(role => role.TenantId == seed.TenantId)
                .Where(role => credentialBatch.Contains(role.CredentialId))
                .Include(role => role.Type).OrderByDescending(role => role.CreatedAt).Take(1_000).ToListAsync());
        }

        roles.Should().ContainSingle().Which.Id.Should().Be(seed.Id);
        roles[0].Type!.Name.Should().Be("Assigned role read");
        roles[0].TenantId.Should().Be(seed.TenantId);
        await using var db = CreateDbContext();
        var stored = await db.Set<IdentityRole>().IgnoreQueryFilters().SingleAsync(role => role.Id == seed.Id);
        stored.CredentialId.Should().Be(seed.CredentialId);
        stored.TypeId.Should().Be(seed.TypeId);
        stored.ConcurrencyStamp.Should().Be(seed.ConcurrencyStamp);
    }

    [Test]
    public async Task AssignedRole_WithoutActor_IsRejectedRatherThanReturningEmptyRoles()
    {
        using var actorSuppression = IntegrationTestFixture.SuppressActorAccessToken();
        var read = async () => await IntegrationTestFixture.DataContext.Query<IdentityRole>()
            .Where(role => role.TenantId == IntegrationTestFixture.TestTenantId).Take(1_000).ToListAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*status 401*");
    }

    private async Task<IdentityRole> SeedAssignedRole()
    {
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var tenant = new Tenant { Id = tenantId, TenantId = tenantId, Name = "Assigned role tenant", IsEnabled = true, CreatedAt = now };
        var group = new IdentityRoleTypeGroup
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Assigned role group", Description = "Read regression",
            SystemReferenceId = Guid.NewGuid(), IsEnabled = true, CreatedAt = now
        };
        var identity = new IdentityInformation { Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Role reader", IsEnabled = true, CreatedAt = now };
        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(), TenantId = tenantId, IdentityInfoId = identity.Id,
            UserName = $"role_reader_{Guid.NewGuid():N}", IsEnabled = true, CreatedAt = now
        };
        var type = new IdentityRoleType
        {
            Id = Guid.NewGuid(), TenantId = tenantId, GroupId = group.Id, Name = "Assigned role read",
            SystemReferenceId = Guid.NewGuid(), IsEnabled = true, CreatedAt = now
        };
        var role = new IdentityRole
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CredentialId = credential.Id, Credential = credential,
            TypeId = type.Id, RoleExpiration = now.AddYears(1), IsEnabled = true, CreatedAt = now, ConcurrencyStamp = Guid.NewGuid()
        };
        await using var db = CreateDbContext();
        db.AddRange(tenant, group, identity, credential, type, role);
        foreach (var subFeature in new[] { "", "users", "credentials", "roles" })
        {
            db.Add(new TenantModuleFeature
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ModuleKey = "identity", SubFeatureKey = subFeature,
                DisplayName = $"identity.{subFeature}", IsEnabled = true, CreatedAt = now
            });
        }
        await db.SaveChangesAsync();
        return role;
    }
}
