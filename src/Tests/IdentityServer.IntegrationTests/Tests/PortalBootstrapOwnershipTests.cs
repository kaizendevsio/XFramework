using System.Net;
using System.Reflection;
using System.Text;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.IdentityServer)]
[Category(TestCategories.Wrappers)]
public sealed class PortalBootstrapOwnershipTests : IntegrationTestBase
{
    private static readonly Guid ExpectedAdminTenantId = PortalBootstrapConstants.AdminTenantId;
    private IDisposable? _actorAccessTokenSuppression;

    [SetUp]
    public void SuppressAmbientActorAccessToken() =>
        _actorAccessTokenSuppression = IntegrationTestFixture.SuppressActorAccessToken();

    [TearDown]
    public void RestoreAmbientActorAccessToken()
    {
        _actorAccessTokenSuppression?.Dispose();
        _actorAccessTokenSuppression = null;
    }

    [Test]
    public async Task EnsurePortalBootstrapAdmin_ThroughWrapper_IsServerOwnedSafeAndIdempotent()
    {
        var password = $"Bootstrap-{Guid.NewGuid():N}!";
        var request = CreateRequest(password);

        var first = await IntegrationTestFixture.ServiceWrapper.EnsurePortalBootstrapAdmin(request);
        var second = await IntegrationTestFixture.ServiceWrapper.EnsurePortalBootstrapAdmin(CreateRequest(password));

        first.HttpStatusCode.Should().Be(HttpStatusCode.OK, first.Message);
        first.IsSuccess.Should().BeTrue();
        first.Response.Should().NotBeNull();
        first.Response!.TenantId.Should().Be(ExpectedAdminTenantId);
        first.Response.TenantId.Should().NotBe(IntegrationTestFixture.TestTenantId);
        first.Response.Created.Should().BeTrue();

        second.HttpStatusCode.Should().Be(HttpStatusCode.OK, second.Message);
        second.Response.Should().NotBeNull();
        second.Response!.Created.Should().BeFalse();
        second.Response.TenantId.Should().Be(first.Response.TenantId);
        second.Response.IdentityId.Should().Be(first.Response.IdentityId);
        second.Response.CredentialId.Should().Be(first.Response.CredentialId);
        second.Response.RoleTypeId.Should().Be(first.Response.RoleTypeId);
        second.Response.RoleId.Should().Be(first.Response.RoleId);

        typeof(PortalBootstrapAdminResponse)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Should()
            .NotContain(name => name.Contains("Password", StringComparison.OrdinalIgnoreCase));

        await using var db = CreateDbContext();
        var credential = await db.Set<IdentityCredential>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == first.Response.CredentialId);
        BCrypt.Net.BCrypt.Verify(password, Encoding.ASCII.GetString(credential.PasswordByte!))
            .Should().BeTrue();

        (await db.Set<Tenant>()
                .IgnoreQueryFilters()
                .CountAsync(item => item.Id == first.Response.TenantId))
            .Should().Be(1);
        (await db.Set<IdentityInformation>()
                .IgnoreQueryFilters()
                .CountAsync(item => item.Id == first.Response.IdentityId))
            .Should().Be(1);
        (await db.Set<IdentityCredential>()
                .IgnoreQueryFilters()
                .CountAsync(item => item.Id == first.Response.CredentialId))
            .Should().Be(1);
        (await db.Set<IdentityRole>()
                .IgnoreQueryFilters()
                .CountAsync(item => item.Id == first.Response.RoleId))
            .Should().Be(1);

        var sessionTypes = await db.Set<SessionType>()
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == first.Response.TenantId)
            .ToListAsync();
        sessionTypes.Select(item => item.SystemReferenceId).Should().Contain(
        [
            IdentityConstants.SessionType.User,
            IdentityConstants.SessionType.Service,
            IdentityConstants.SessionType.Rpc
        ]);

        var identityFeatureCount = TenantModuleFeatureKeys.All.Count(feature =>
            feature.ModuleKey == TenantModuleFeatureKeys.Identity);
        (await db.Set<TenantModuleFeature>()
                .IgnoreQueryFilters()
                .CountAsync(item =>
                    item.TenantId == first.Response.TenantId
                    && item.ModuleKey == TenantModuleFeatureKeys.Identity))
            .Should().Be(identityFeatureCount);

        (await db.Set<IdentityRoleTypeFeaturePermission>()
                .IgnoreQueryFilters()
                .CountAsync(item =>
                    item.TenantId == first.Response.TenantId
                    && item.RoleTypeId == first.Response.RoleTypeId
                    && item.Effect == RoleCapabilityPermissionEffect.Allow
                    && item.IsEnabled
                    && !item.IsDeleted))
            .Should().Be(TenantModuleFeatureKeys.All.Count * IdentityAuthorizationConstants.CapabilityKeys.Count);

        var registryValue = await db.Set<RegistryConfiguration>()
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == first.Response.TenantId && item.Key == "DefaultAuthorizeBy")
            .Select(item => item.Value)
            .SingleAsync();
        registryValue.Should().Be(((int)AuthorizationType.Username).ToString());
    }

    [Test]
    public async Task EnsurePortalBootstrapAdmin_ThroughWrapper_RejectsInvalidPassword()
    {
        var request = CreateRequest(string.Empty);

        var result = await IntegrationTestFixture.ServiceWrapper.EnsurePortalBootstrapAdmin(request);

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
        result.Response.Should().BeNull();
    }

    [Test]
    public async Task EnsurePortalBootstrapAdmin_WithoutTenantTargetScope_IsForbidden()
    {
        var wrapper = await IntegrationTestFixture.CreatePortalWrapperWithoutTenantTargetScope();

        var result = await wrapper.EnsurePortalBootstrapAdmin(
            CreateRequest($"Bootstrap-{Guid.NewGuid():N}!"));

        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task EnsurePortalBootstrapAdmin_FromUnauthorizedServiceCaller_IsForbidden()
    {
        var wrapper = await IntegrationTestFixture.CreateUnauthorizedCallerServiceWrapper();

        var result = await wrapper.EnsurePortalBootstrapAdmin(
            CreateRequest($"Bootstrap-{Guid.NewGuid():N}!"));

        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task EnsurePortalBootstrapAdmin_WithoutRequestedTenant_IsRejectedBeforeValidation()
    {
        var request = CreateRequest($"Bootstrap-{Guid.NewGuid():N}!");
        request.Metadata.RequestedTenantId = null;

        var result = await IntegrationTestFixture.ServiceWrapper.EnsurePortalBootstrapAdmin(request);

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static EnsurePortalBootstrapAdminRequest CreateRequest(string password) => new()
    {
        TenantName = "Portal Admin",
        DisplayName = "Super Admin",
        UserName = "superadmin",
        Password = password,
        Metadata = new RequestMetadata
        {
            RequestId = Guid.NewGuid(),
            OperationName = nameof(PortalBootstrapOwnershipTests),
            RequestedTenantId = PortalBootstrapConstants.AdminTenantId
        }
    };
}
