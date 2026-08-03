using System.Net;
using System.Text;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Enums;
using XFramework.TestInfrastructure;
using Session = IdentityServer.Domain.Shared.Contracts.Session;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.IdentityServer)]
public sealed class IdentityServerPersistenceRemediationTests : IntegrationTestBase
{
    [Test]
    public async Task TenantConcurrency_RejectsStaleUpdateAndDeleteAcrossDbContexts()
    {
        var updateTenant = await SeedTenantAsync("Concurrent update");

        await using (var firstDb = CreateDbContext())
        await using (var staleDb = CreateDbContext())
        {
            var firstWriter = await firstDb.Set<Tenant>()
                .IgnoreQueryFilters()
                .SingleAsync(tenant => tenant.Id == updateTenant.Id);
            var staleWriter = await staleDb.Set<Tenant>()
                .IgnoreQueryFilters()
                .SingleAsync(tenant => tenant.Id == updateTenant.Id);

            firstWriter.Name = "First writer";
            firstWriter.ConcurrencyStamp = Guid.NewGuid();
            await firstDb.SaveChangesAsync();

            staleWriter.Name = "Stale writer";
            staleWriter.ConcurrencyStamp = Guid.NewGuid();

            var update = () => staleDb.SaveChangesAsync();
            await update.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }

        var deleteTenant = await SeedTenantAsync("Concurrent delete");

        await using (var firstDb = CreateDbContext())
        await using (var staleDb = CreateDbContext())
        {
            var firstWriter = await firstDb.Set<Tenant>()
                .IgnoreQueryFilters()
                .SingleAsync(tenant => tenant.Id == deleteTenant.Id);
            var staleWriter = await staleDb.Set<Tenant>()
                .IgnoreQueryFilters()
                .SingleAsync(tenant => tenant.Id == deleteTenant.Id);

            firstWriter.Description = "Updated before stale delete";
            firstWriter.ConcurrencyStamp = Guid.NewGuid();
            await firstDb.SaveChangesAsync();

            staleDb.Remove(staleWriter);

            var delete = () => staleDb.SaveChangesAsync();
            await delete.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }
    }

    [Test]
    public async Task DeleteTenant_WithStaleExpectedConcurrencyStamp_ReturnsConflict()
    {
        var tenant = await SeedTenantAsync("Stale wrapper delete");
        var staleStamp = tenant.ConcurrencyStamp;

        await using (var db = CreateDbContext())
        {
            var current = await db.Set<Tenant>()
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == tenant.Id);
            current.Description = "Changed after the caller loaded the tenant";
            current.ConcurrencyStamp = Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        var result = await IntegrationTestFixture.ServiceWrapper.DeleteTenant(new DeleteTenantRequest
        {
            TenantId = tenant.Id,
            ExpectedConcurrencyStamp = staleStamp,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);
        result.IsSuccess.Should().BeFalse();

        Guid currentStamp;
        await using (var verificationDb = CreateDbContext())
        {
            var persisted = await verificationDb.Set<Tenant>()
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == tenant.Id);
            persisted.IsDeleted.Should().BeFalse();
            currentStamp = persisted.ConcurrencyStamp;
        }

        var retry = await IntegrationTestFixture.ServiceWrapper.DeleteTenant(new DeleteTenantRequest
        {
            TenantId = tenant.Id,
            ExpectedConcurrencyStamp = currentStamp,
            Metadata = CreateMetadata()
        });

        retry.HttpStatusCode.Should().Be(HttpStatusCode.OK, retry.Message);

        await using var deletedDb = CreateDbContext();
        var deleted = await deletedDb.Set<Tenant>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == tenant.Id);
        deleted.IsDeleted.Should().BeTrue();
    }

    [TestCaseSource(nameof(AuthenticationContactTypes))]
    public async Task ActiveAuthenticationContact_MustBeUniqueWithinTenant(Guid contactTypeId)
    {
        var (firstCredentialId, secondCredentialId, groupId) = await SeedContactOwnersAsync();
        var value = contactTypeId == IdentityConstants.ContactType.Email
            ? $"duplicate_{Guid.NewGuid():N}@example.test"
            : $"+65{Random.Shared.NextInt64(80000000, 99999999)}";

        await using var db = CreateDbContext();
        db.Set<IdentityContact>().AddRange(
            CreateContact(firstCredentialId, groupId, contactTypeId, value),
            CreateContact(secondCredentialId, groupId, contactTypeId, value));

        var save = () => db.SaveChangesAsync();
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Test]
    public async Task RegistryConfiguration_KeyMustBeUniqueWithinTenant()
    {
        var groupId = Guid.NewGuid();
        var key = $"UniqueKey_{Guid.NewGuid():N}";

        await using (var seedDb = CreateDbContext())
        {
            seedDb.Set<RegistryConfigurationGroup>().Add(new RegistryConfigurationGroup
            {
                Id = groupId,
                TenantId = IntegrationTestFixture.TestTenantId,
                Name = "Persistence remediation",
                SystemReferenceId = Guid.NewGuid(),
                IsEnabled = true
            });
            await seedDb.SaveChangesAsync();
        }

        await using var db = CreateDbContext();
        db.Set<RegistryConfiguration>().AddRange(
            CreateRegistryConfiguration(groupId, key, "first"),
            CreateRegistryConfiguration(groupId, key, "second"));

        var save = () => db.SaveChangesAsync();
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Test]
    public async Task DisableTenant_RevokesAllActiveSessionsWithRotatedConcurrencyStamp()
    {
        var tenant = await SeedTenantAsync("Bulk session revocation");
        var originalStamps = await SeedSessionsAsync(tenant.Id, count: 25);

        var result = await IntegrationTestFixture.ServiceWrapper.UpdateTenant(new UpdateTenantRequest
        {
            TenantId = tenant.Id,
            Name = tenant.Name!,
            Description = tenant.Description,
            Status = tenant.Status,
            Version = tenant.Version,
            ParentTenantId = tenant.ParentTenantId,
            Expiration = tenant.Expiration,
            AvailabilityDate = tenant.AvailabilityDate,
            IsEnabled = false,
            ConcurrencyStamp = tenant.ConcurrencyStamp,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);

        await using var db = CreateDbContext();
        var sessions = await db.Set<Session>()
            .IgnoreQueryFilters()
            .Where(session => session.TenantId == tenant.Id)
            .ToListAsync();

        sessions.Should().HaveCount(25);
        sessions.Should().OnlyContain(session => session.Status == CurrentSessionState.Inactive);
        sessions.Should().OnlyContain(session => session.ModifiedAt.HasValue);
        foreach (var session in sessions)
            session.ConcurrencyStamp.Should().NotBe(originalStamps[session.Id]);
    }

    [Test]
    public async Task Authenticate_DoesNotReloadCredentialRoleCollection()
    {
        var username = UniqueUsername();
        const string password = "QueryShape123!";
        await TestHelpers.SeedCredentialWithRole(
            IntegrationTestFixture.ConnectionString,
            username,
            password);

        await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();
        var commandCounter = IntegrationTestFixture.Services.GetRequiredService<DbCommandCounterInterceptor>();
        using var measurement = commandCounter.BeginMeasurement();

        var result = await scope.ServiceProvider.GetRequiredService<IAuthService>()
            .AuthenticateAsync(new AuthenticateIdentityRequest
            {
                UserName = username,
                Password = password,
                AuthorizationType = AuthorizationType.Username,
                RoleId = TestConstants.RoleTypeId,
                GenerateToken = false,
                Metadata = CreateMetadata()
            });

        result.IsSuccess.Should().BeTrue(result.Message);
        measurement.CommandCount.Should().BeLessThanOrEqualTo(5,
            "authentication should query the tenant and credential, lock the credential, load active roles once, and persist its audit state");
    }

    private static object[] AuthenticationContactTypes =>
    [
        IdentityConstants.ContactType.Email,
        IdentityConstants.ContactType.Phone
    ];

    private async Task<Tenant> SeedTenantAsync(string prefix)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            Name = $"{prefix} {Guid.NewGuid():N}",
            Description = "IdentityServer persistence remediation test",
            Status = 1,
            Version = 1m,
            ParentTenantId = IntegrationTestFixture.TestTenantId,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };

        await using var db = CreateDbContext();
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    private async Task<(Guid FirstCredentialId, Guid SecondCredentialId, Guid GroupId)> SeedContactOwnersAsync()
    {
        var groupId = Guid.NewGuid();
        var firstIdentityId = Guid.NewGuid();
        var secondIdentityId = Guid.NewGuid();
        var firstCredentialId = Guid.NewGuid();
        var secondCredentialId = Guid.NewGuid();

        await using var db = CreateDbContext();
        db.Set<IdentityContactGroup>().Add(new IdentityContactGroup
        {
            Id = groupId,
            TenantId = IntegrationTestFixture.TestTenantId,
            Name = "Authentication contacts",
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true
        });
        db.Set<IdentityInformation>().AddRange(
            CreateIdentity(firstIdentityId, "First"),
            CreateIdentity(secondIdentityId, "Second"));
        db.Set<IdentityCredential>().AddRange(
            CreateCredential(firstCredentialId, firstIdentityId),
            CreateCredential(secondCredentialId, secondIdentityId));
        await db.SaveChangesAsync();

        return (firstCredentialId, secondCredentialId, groupId);
    }

    private async Task<Dictionary<Guid, Guid>> SeedSessionsAsync(Guid tenantId, int count)
    {
        var identity = CreateIdentity(Guid.NewGuid(), "Session");
        identity.TenantId = tenantId;
        var credential = CreateCredential(Guid.NewGuid(), identity.Id);
        credential.TenantId = tenantId;
        var sessionTypeId = Guid.NewGuid();
        var sessionType = new SessionType
        {
            Id = sessionTypeId,
            TenantId = tenantId,
            Name = "Bulk revocation",
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true
        };
        var sessions = Enumerable.Range(0, count)
            .Select(_ => new Session
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CredentialId = credential.Id,
                SessionTypeId = sessionTypeId,
                Status = CurrentSessionState.Active,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            })
            .ToList();

        await using var db = CreateDbContext();
        db.Set<IdentityInformation>().Add(identity);
        db.Set<IdentityCredential>().Add(credential);
        db.Set<SessionType>().Add(sessionType);
        db.Set<Session>().AddRange(sessions);
        await db.SaveChangesAsync();

        return sessions.ToDictionary(session => session.Id, session => session.ConcurrencyStamp);
    }

    private static IdentityInformation CreateIdentity(Guid id, string firstName) => new()
    {
        Id = id,
        TenantId = IntegrationTestFixture.TestTenantId,
        FirstName = firstName,
        LastName = "Persistence",
        IsEnabled = true,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static IdentityCredential CreateCredential(Guid id, Guid identityId) => new()
    {
        Id = id,
        TenantId = IntegrationTestFixture.TestTenantId,
        IdentityInfoId = identityId,
        UserName = $"persistence_{Guid.NewGuid():N}",
        PasswordByte = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword("Persistence123!")),
        IsEnabled = true,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static IdentityContact CreateContact(
        Guid credentialId,
        Guid groupId,
        Guid contactTypeId,
        string value) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = IntegrationTestFixture.TestTenantId,
        CredentialId = credentialId,
        GroupId = groupId,
        TypeId = contactTypeId,
        Value = value,
        IsEnabled = true,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static RegistryConfiguration CreateRegistryConfiguration(
        Guid groupId,
        string key,
        string value) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = IntegrationTestFixture.TestTenantId,
        GroupId = groupId,
        Key = key,
        Value = value,
        IsEnabled = true,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static RequestMetadata CreateMetadata() => new()
    {
        TenantId = IntegrationTestFixture.TestTenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        Name = nameof(IdentityServerPersistenceRemediationTests),
        DeviceName = "IntegrationTest",
        DeviceAgent = "IntegrationTest"
    };
}
