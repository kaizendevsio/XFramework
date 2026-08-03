using System.Net;
using System.Text;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Enums;
using XFramework.TestInfrastructure;
using Session = IdentityServer.Domain.Shared.Contracts.Session;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.IdentityServer)]
[Category(TestCategories.Wrappers)]
public sealed class TenantLifecycleTests : IntegrationTestBase
{
    [Test]
    public async Task CreateTenant_ThroughWrapper_ProvisionsRequiredSessionTypes()
    {
        var tenant = await CreateTenantAsync("Provisioned tenant");

        await using var db = CreateDbContext();
        var sessionTypes = await db.Set<SessionType>()
            .IgnoreQueryFilters()
            .Where(type => type.TenantId == tenant.Id)
            .ToListAsync();

        sessionTypes.Should().HaveCount(3);
        sessionTypes.Select(type => type.SystemReferenceId).Should().BeEquivalentTo(
        [
            IdentityConstants.SessionType.User,
            IdentityConstants.SessionType.Service,
            IdentityConstants.SessionType.Rpc
        ]);
        sessionTypes.Select(type => type.Id).Should().OnlyHaveUniqueItems();
        sessionTypes.Should().OnlyContain(type =>
            type.Id != Guid.Empty &&
            type.Id != type.SystemReferenceId &&
            type.IsEnabled &&
            !type.IsDeleted);
    }

    [Test]
    public async Task UpdateTenant_DisablingTenant_UpdatesLifecycleAndRevokesActiveSessions()
    {
        var tenant = await CreateTenantAsync("Lifecycle update tenant");
        var sessionId = await SeedActiveSessionAsync(tenant.Id);
        var updatedName = $"Updated {Guid.NewGuid():N}";

        var result = await IntegrationTestFixture.ServiceWrapper.UpdateTenant(new UpdateTenantRequest
        {
            TenantId = tenant.Id,
            Name = updatedName,
            Description = "Updated through the tenant lifecycle wrapper",
            Status = 3,
            Version = 2m,
            IsEnabled = false,
            ConcurrencyStamp = tenant.ConcurrencyStamp,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.IsSuccess.Should().BeTrue();

        await using var db = CreateDbContext();
        var updated = await db.Set<Tenant>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == tenant.Id);
        var session = await db.Set<Session>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == sessionId);

        updated.Name.Should().Be(updatedName);
        updated.Status.Should().Be(3);
        updated.Version.Should().Be(2m);
        updated.IsEnabled.Should().BeFalse();
        updated.ConcurrencyStamp.Should().NotBe(tenant.ConcurrencyStamp);
        session.Status.Should().Be(CurrentSessionState.Inactive);
        session.ModifiedAt.Should().NotBeNull();
    }

    [Test]
    public async Task UpdateTenant_DisablingActiveTenantContext_IsForbidden()
    {
        await using var db = CreateDbContext();
        var tenant = await db.Set<Tenant>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == IntegrationTestFixture.TestTenantId);

        var result = await IntegrationTestFixture.ServiceWrapper.UpdateTenant(new UpdateTenantRequest
        {
            TenantId = tenant.Id,
            Name = tenant.Name ?? "Test tenant",
            Description = tenant.Description,
            Status = tenant.Status,
            Version = tenant.Version > 0 ? tenant.Version : 1m,
            ParentTenantId = tenant.ParentTenantId,
            Expiration = tenant.Expiration,
            AvailabilityDate = tenant.AvailabilityDate,
            IsEnabled = false,
            ConcurrencyStamp = tenant.ConcurrencyStamp == Guid.Empty ? Guid.NewGuid() : tenant.ConcurrencyStamp,
            Metadata = CreateMetadata(tenant.Id)
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task DeleteTenant_ThroughWrapper_RevokesActiveSessionsBeforeSoftDelete()
    {
        var tenant = await CreateTenantAsync("Lifecycle delete tenant");
        var sessionId = await SeedActiveSessionAsync(tenant.Id);

        var result = await IntegrationTestFixture.ServiceWrapper.DeleteTenant(new DeleteTenantRequest
        {
            TenantId = tenant.Id,
            ExpectedConcurrencyStamp = tenant.ConcurrencyStamp,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.IsSuccess.Should().BeTrue();

        await using var db = CreateDbContext();
        var deletedTenant = await db.Set<Tenant>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == tenant.Id);
        var session = await db.Set<Session>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == sessionId);

        deletedTenant.IsDeleted.Should().BeTrue();
        deletedTenant.IsEnabled.Should().BeFalse();
        session.Status.Should().Be(CurrentSessionState.Inactive);
    }

    private async Task<Tenant> CreateTenantAsync(string prefix)
    {
        var name = $"{prefix} {Guid.NewGuid():N}";
        var result = await IntegrationTestFixture.ServiceWrapper.CreateTenant(new CreateTenantRequest
        {
            Name = name,
            Description = "Tenant lifecycle integration test",
            Version = 1m,
            Status = 1,
            ParentTenantId = IntegrationTestFixture.TestTenantId,
            Metadata = CreateMetadata()
        });
        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();

        await using var db = CreateDbContext();
        return await db.Set<Tenant>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Name == name);
    }

    private async Task<Guid> SeedActiveSessionAsync(Guid tenantId)
    {
        await using var db = CreateDbContext();
        var sessionTypeId = await db.Set<SessionType>()
            .IgnoreQueryFilters()
            .Where(type => type.TenantId == tenantId)
            .Where(type => type.SystemReferenceId == IdentityConstants.SessionType.User)
            .Select(type => type.Id)
            .SingleAsync();

        var identity = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = "Lifecycle",
            LastName = "Test",
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdentityInfoId = identity.Id,
            UserName = $"lifecycle_{Guid.NewGuid():N}",
            PasswordByte = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword("Lifecycle123!")),
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CredentialId = credential.Id,
            SessionTypeId = sessionTypeId,
            Status = CurrentSessionState.Active,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };

        db.Set<IdentityInformation>().Add(identity);
        db.Set<IdentityCredential>().Add(credential);
        db.Set<Session>().Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private static RequestMetadata CreateMetadata(Guid? tenantId = null) => new()
    {
        TenantId = tenantId ?? IntegrationTestFixture.TestTenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        Name = "TenantLifecycleTests",
        DeviceName = "TestDevice",
        DeviceAgent = "TestAgent"
    };
}
