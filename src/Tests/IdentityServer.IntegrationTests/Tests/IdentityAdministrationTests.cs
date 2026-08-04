using System.Net;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
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
public sealed class IdentityAdministrationTests : IntegrationTestBase
{
    [Test]
    public async Task CreateAndUpdateIdentity_ThroughWrapper_PreservesVerificationAndRejectsStaleStamp()
    {
        var created = await CreateIdentityAsync("Initial");

        await using (var db = CreateDbContext())
        {
            var stored = await db.Set<IdentityInformation>()
                .IgnoreQueryFilters()
                .SingleAsync(identity => identity.Id == created.Id);
            stored.IsVerified = true;
            stored.ConcurrencyStamp = Guid.NewGuid();
            await db.SaveChangesAsync();
            created.ConcurrencyStamp = stored.ConcurrencyStamp;
            created.IsVerified = stored.IsVerified;
        }

        var originalStamp = created.ConcurrencyStamp;
        var update = await IntegrationTestFixture.ServiceWrapper.UpdateIdentityProfile(
            new UpdateIdentityProfileRequest
            {
                IdentityId = created.Id,
                ExpectedConcurrencyStamp = originalStamp,
                FirstName = "Updated",
                LastName = "Identity",
                IdentityName = created.IdentityName,
                IdentityDescription = "Updated profile",
                BirthDate = new DateOnly(1990, 1, 2),
                Gender = Gender.Male,
                CivilStatus = CivilStatus.Single,
                Metadata = CreateMetadata()
            });

        update.HttpStatusCode.Should().Be(HttpStatusCode.OK, update.Message);
        update.Response.Should().NotBeNull();
        update.Response!.FirstName.Should().Be("Updated");
        update.Response.IsVerified.Should().BeTrue();
        update.Response.ConcurrencyStamp.Should().NotBe(originalStamp);

        var stale = await IntegrationTestFixture.ServiceWrapper.UpdateIdentityProfile(
            new UpdateIdentityProfileRequest
            {
                IdentityId = created.Id,
                ExpectedConcurrencyStamp = originalStamp,
                FirstName = "Stale",
                IdentityName = created.IdentityName,
                Metadata = CreateMetadata()
            });

        stale.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var verificationDb = CreateDbContext();
        var storedIdentity = await verificationDb.Set<IdentityInformation>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(identity => identity.Id == created.Id);
        storedIdentity.FirstName.Should().Be("Updated");
        storedIdentity.IsVerified.Should().BeTrue();
    }

    [Test]
    public async Task SetIdentityEnabled_DisablingIdentity_RevokesActiveSessions()
    {
        var identity = await CreateIdentityAsync("Disable");
        var sessionId = await SeedActiveSessionAsync(identity);

        var result = await IntegrationTestFixture.ServiceWrapper.SetIdentityEnabled(
            new SetIdentityEnabledRequest
            {
                IdentityId = identity.Id,
                ExpectedConcurrencyStamp = identity.ConcurrencyStamp,
                IsEnabled = false,
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.Response.Should().NotBeNull();
        result.Response!.IsEnabled.Should().BeFalse();
        result.Response.ConcurrencyStamp.Should().NotBe(identity.ConcurrencyStamp);

        await using var db = CreateDbContext();
        var session = await db.Set<Session>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == sessionId);
        session.Status.Should().Be(CurrentSessionState.Inactive);
    }

    [Test]
    public async Task SetIdentityEnabled_WithManySessions_UsesBoundedBulkRevocation()
    {
        var identity = await CreateIdentityAsync("BulkDisable");
        var sessionIds = await SeedActiveSessionsAsync(identity, 100);

        await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
        var commandCounter = IntegrationTestFixture.Services
            .GetRequiredService<DbCommandCounterInterceptor>();
        using var measurement = commandCounter.BeginMeasurement();
        IntegrationTestFixture.EstablishTrustedActorContext(
            scope.ServiceProvider,
            IntegrationTestFixture.TestTenantId,
            IntegrationTestFixture.TestCredentialId);

        var result = await scope.ServiceProvider
            .GetRequiredService<IIdentityAdministrationService>()
            .SetEnabledAsync(
                new SetIdentityEnabledRequest
                {
                    IdentityId = identity.Id,
                    ExpectedConcurrencyStamp = identity.ConcurrencyStamp,
                    IsEnabled = false,
                    Metadata = CreateMetadata()
                },
                CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        measurement.CommandCount.Should().BeLessThanOrEqualTo(3);
        scope.ServiceProvider.GetRequiredService<DbContext>()
            .ChangeTracker.Entries<Session>()
            .Should().BeEmpty();

        await using var db = CreateDbContext();
        var sessions = await db.Set<Session>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => sessionIds.Contains(item.Id))
            .ToListAsync();
        sessions.Should().HaveCount(100);
        sessions.Should().OnlyContain(item => item.Status == CurrentSessionState.Inactive);
    }

    [Test]
    public async Task SoftDeleteIdentity_ThroughWrapper_SoftDeletesAndRevokesActiveSessions()
    {
        var identity = await CreateIdentityAsync("Delete");
        var sessionId = await SeedActiveSessionAsync(identity);

        var result = await IntegrationTestFixture.ServiceWrapper.SoftDeleteIdentity(
            new SoftDeleteIdentityRequest
            {
                IdentityId = identity.Id,
                ExpectedConcurrencyStamp = identity.ConcurrencyStamp,
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);

        await using var db = CreateDbContext();
        var deleted = await db.Set<IdentityInformation>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == identity.Id);
        var session = await db.Set<Session>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == sessionId);

        deleted.IsDeleted.Should().BeTrue();
        deleted.IsEnabled.Should().BeFalse();
        deleted.DeletedAt.Should().NotBeNull();
        session.Status.Should().Be(CurrentSessionState.Inactive);
    }

    [Test]
    public async Task CreateIdentity_WithoutAName_ReturnsBadRequest()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.CreateIdentity(new CreateIdentityRequest
        {
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task IdentityMutationWrappers_WithUnknownIdentity_ReturnNotFound()
    {
        var identityId = Guid.NewGuid();

        var setEnabled = await IntegrationTestFixture.ServiceWrapper.SetIdentityEnabled(
            new SetIdentityEnabledRequest
            {
                IdentityId = identityId,
                ExpectedConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = false,
                Metadata = CreateMetadata()
            });
        setEnabled.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        setEnabled.IsSuccess.Should().BeFalse();

        var softDelete = await IntegrationTestFixture.ServiceWrapper.SoftDeleteIdentity(
            new SoftDeleteIdentityRequest
            {
                IdentityId = identityId,
                ExpectedConcurrencyStamp = Guid.NewGuid(),
                Metadata = CreateMetadata()
            });
        softDelete.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        softDelete.IsSuccess.Should().BeFalse();
    }

    private async Task<IdentityAdministrationResponse> CreateIdentityAsync(string prefix)
    {
        var result = await IntegrationTestFixture.ServiceWrapper.CreateIdentity(new CreateIdentityRequest
        {
            FirstName = prefix,
            LastName = "Identity",
            IdentityName = $"{prefix}-{Guid.NewGuid():N}",
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.Response.Should().NotBeNull();
        result.Response!.TenantId.Should().Be(IntegrationTestFixture.TestTenantId);
        result.Response.IsVerified.Should().BeFalse();
        result.Response.IsEnabled.Should().BeTrue();
        return result.Response;
    }

    private async Task<Guid> SeedActiveSessionAsync(IdentityAdministrationResponse identity)
    {
        var sessionIds = await SeedActiveSessionsAsync(identity, 1);
        return sessionIds[0];
    }

    private async Task<List<Guid>> SeedActiveSessionsAsync(
        IdentityAdministrationResponse identity,
        int count)
    {
        await using var db = CreateDbContext();
        var sessionTypeId = await db.Set<SessionType>()
            .IgnoreQueryFilters()
            .Where(type => type.TenantId == identity.TenantId)
            .Where(type => type.SystemReferenceId == IdentityConstants.SessionType.User)
            .Select(type => type.Id)
            .SingleAsync();

        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            TenantId = identity.TenantId,
            IdentityInfoId = identity.Id,
            UserName = $"identity_admin_{Guid.NewGuid():N}",
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var sessions = Enumerable.Range(0, count)
            .Select(_ => new Session
            {
                Id = Guid.NewGuid(),
                TenantId = identity.TenantId,
                CredentialId = credential.Id,
                SessionTypeId = sessionTypeId,
                Status = CurrentSessionState.Active,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            })
            .ToList();

        db.Set<IdentityCredential>().Add(credential);
        db.Set<Session>().AddRange(sessions);
        await db.SaveChangesAsync();
        return sessions.Select(session => session.Id).ToList();
    }

    private static RequestMetadata CreateMetadata() => new()
    {
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        OperationName = nameof(IdentityAdministrationTests),
        DeviceName = "TestDevice",
        UserAgent = "TestAgent"
    };
}
