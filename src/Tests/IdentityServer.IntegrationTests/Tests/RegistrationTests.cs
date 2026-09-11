using System.Net;
using System.Text;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Enums;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[NonParallelizable]
public sealed class RegistrationTests : IntegrationTestBase
{
    [OneTimeSetUp]
    public async Task SeedMemberRole()
    {
        await using var db = CreateDbContext();
        db.Add(new IdentityRoleType
        {
            Id = IntegrationTestFixture.RegistrationRoleId, TenantId = IntegrationTestFixture.TestTenantId,
            GroupId = XFramework.TestInfrastructure.TestConstants.RoleGroupId,
            Name = "Registration member", RoleLevel = 0, IsEnabled = true
        });
        await db.SaveChangesAsync();
    }

    [Test]
    public async Task Register_WithoutActor_CreatesHashedMemberAndCanAuthenticate()
    {
        var wrapper = await IntegrationTestFixture.CreateRegistrationServiceWrapper();
        using var anonymous = IntegrationTestFixture.SuppressActorAccessToken();
        var request = Request();
        var result = await wrapper.RegisterIdentity(request);
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Response!.TenantId.Should().Be(IntegrationTestFixture.TestTenantId);
        await using var db = CreateDbContext();
        var credential = await db.Set<IdentityCredential>().SingleAsync(x => x.Id == result.Response.CredentialId);
        BCrypt.Net.BCrypt.Verify(request.Password, Encoding.ASCII.GetString(credential.PasswordByte!)).Should().BeTrue();
        var identity = await db.Set<IdentityInformation>().SingleAsync(x => x.Id == credential.IdentityInfoId);
        identity.IdentityName.Should().Be(request.DisplayName);
        identity.IsVerified.Should().BeFalse();
        var roles = await db.Set<IdentityRole>().Where(x => x.CredentialId == credential.Id).ToListAsync();
        roles.Should().ContainSingle().Which.TypeId.Should().Be(IntegrationTestFixture.RegistrationRoleId);
        var login = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(new AuthenticateIdentityRequest
        {
            UserName = request.UserName, Password = request.Password,
            RoleId = IntegrationTestFixture.RegistrationRoleId, AuthorizationType = AuthorizationType.Username,
            Metadata = request.Metadata
        });
        login.IsSuccess.Should().BeTrue(login.Message);
    }

    [Test]
    public async Task Register_DuplicateUsername_DoesNotLeaveAnOrphanIdentity()
    {
        var wrapper = await IntegrationTestFixture.CreateRegistrationServiceWrapper();
        using var anonymous = IntegrationTestFixture.SuppressActorAccessToken();
        var request = Request();
        (await wrapper.RegisterIdentity(request)).IsSuccess.Should().BeTrue();
        var duplicate = await wrapper.RegisterIdentity(request);
        duplicate.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);
        await using var db = CreateDbContext();
        (await db.Set<IdentityInformation>().CountAsync(x => x.IdentityName == request.DisplayName)).Should().Be(1);
    }

    [Test]
    public async Task Register_AnotherTenant_IsForbiddenAndCreatesNothing()
    {
        var wrapper = await IntegrationTestFixture.CreateRegistrationServiceWrapper();
        using var anonymous = IntegrationTestFixture.SuppressActorAccessToken();
        var request = Request();
        request.Metadata.RequestedTenantId = Guid.NewGuid();
        (await wrapper.RegisterIdentity(request)).HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using var db = CreateDbContext();
        (await db.Set<IdentityCredential>().IgnoreQueryFilters().AnyAsync(x => x.UserName == request.UserName)).Should().BeFalse();
    }

    [Test]
    public async Task Register_ConcurrentDuplicate_OnlyOneCompleteAccountIsSaved()
    {
        var wrapper = await IntegrationTestFixture.CreateRegistrationServiceWrapper();
        using var anonymous = IntegrationTestFixture.SuppressActorAccessToken();
        var first = Request();
        var second = first with { Metadata = new RequestMetadata
            { RequestedTenantId = IntegrationTestFixture.TestTenantId, RequestId = Guid.NewGuid() } };
        var results = await Task.WhenAll(wrapper.RegisterIdentity(first), wrapper.RegisterIdentity(second));
        results.Count(x => x.IsSuccess).Should().Be(1);
        results.Count(x => x.HttpStatusCode == HttpStatusCode.Conflict).Should().Be(1);
        await using var db = CreateDbContext();
        (await db.Set<IdentityInformation>().CountAsync(x => x.IdentityName == first.DisplayName)).Should().Be(1);
        var credential = await db.Set<IdentityCredential>().SingleAsync(x => x.UserName == first.UserName);
        (await db.Set<IdentityRole>().CountAsync(x => x.CredentialId == credential.Id)).Should().Be(1);
    }

    [TestCase(false, (short)0)]
    [TestCase(true, (short)1)]
    public async Task Register_DisabledOrElevatedRole_IsForbidden(bool enabled, short level)
    {
        var wrapper = await IntegrationTestFixture.CreateRegistrationServiceWrapper();
        using var anonymous = IntegrationTestFixture.SuppressActorAccessToken();
        await using var db = CreateDbContext();
        var role = await db.Set<IdentityRoleType>().AsTracking().SingleAsync(x => x.Id == IntegrationTestFixture.RegistrationRoleId);
        try
        {
            role.IsEnabled = enabled;
            role.RoleLevel = level;
            await db.SaveChangesAsync();
            (await wrapper.RegisterIdentity(Request())).HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            role.IsEnabled = true;
            role.RoleLevel = 0;
            await db.SaveChangesAsync();
        }
    }

    [Test]
    public async Task Register_WithoutRegistrationScope_IsForbidden()
    {
        using var anonymous = IntegrationTestFixture.SuppressActorAccessToken();
        var result = await IntegrationTestFixture.ServiceWrapper.RegisterIdentity(Request());
        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestCase("short")]
    [TestCase("ééééééééééééééééééééééééééééééééééééé")]
    public async Task Register_InvalidPassword_CreatesNothing(string password)
    {
        var wrapper = await IntegrationTestFixture.CreateRegistrationServiceWrapper();
        using var anonymous = IntegrationTestFixture.SuppressActorAccessToken();
        var request = Request();
        request.Password = password;
        (await wrapper.RegisterIdentity(request)).HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var db = CreateDbContext();
        (await db.Set<IdentityCredential>().AnyAsync(x => x.UserName == request.UserName)).Should().BeFalse();
    }

    private static RegisterIdentityRequest Request() => new()
    {
        DisplayName = $"Signup {Guid.NewGuid():N}", UserName = UniqueUsername(), Password = "RegistrationTest123!",
        Metadata = new RequestMetadata { RequestedTenantId = IntegrationTestFixture.TestTenantId, RequestId = Guid.NewGuid() }
    };
}
