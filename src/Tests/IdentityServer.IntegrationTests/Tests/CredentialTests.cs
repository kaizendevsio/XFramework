using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;
using Session = IdentityServer.Domain.Shared.Contracts.Session;

namespace IdentityServer.IntegrationTests.Tests;

/// <summary>
/// Integration tests for credential CRUD operations.
/// </summary>
[TestFixture]
public class CredentialTests : IntegrationTestBase
{
    [Test]
    public async Task CreateCredential_WithValidData_ReturnsOk()
    {
        var info = await SeedIdentityInfo();
        var username = UniqueUsername();
        var request = new CreateCredentialRequest
        {
            IdentityInfoId = info.Id,
            UserName = username,
            UserAlias = "Test Alias",
            Password = "TestPassword123!",
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/credentials", request);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"Response body: {await response.Content.ReadAsStringAsync()}");

        using var responseDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var responseData = responseDocument.RootElement;
        responseData.GetProperty("id").GetGuid().Should().NotBeEmpty();
        responseData.GetProperty("identityInfoId").GetGuid().Should().Be(info.Id);
        responseData.TryGetProperty("password", out _).Should().BeFalse();
        responseData.TryGetProperty("passwordByte", out _).Should().BeFalse();
        responseData.TryGetProperty("token", out _).Should().BeFalse();

        await using var db = CreateDbContext();
        var created = await db.Set<IdentityCredential>()
            .FirstOrDefaultAsync(c => c.IdentityInfoId == info.Id && c.UserName == username);

        created.Should().NotBeNull();
        created!.TenantId.Should().Be(IntegrationTestFixture.TestTenantId);
        created.UserAlias.Should().Be("Test Alias");
        created.IsEnabled.Should().BeTrue();
    }

    [Test]
    public async Task CreateCredential_PasswordIsHashed_NotStoredPlaintext()
    {
        var info = await SeedIdentityInfo();
        var username = UniqueUsername();
        var password = "AnotherPassword123!";
        var request = new CreateCredentialRequest
        {
            IdentityInfoId = info.Id,
            UserName = username,
            Password = password,
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/credentials", request);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"Response body: {await response.Content.ReadAsStringAsync()}");

        await using var db = CreateDbContext();
        var created = await db.Set<IdentityCredential>()
            .FirstOrDefaultAsync(c => c.IdentityInfoId == info.Id && c.UserName == username);

        created.Should().NotBeNull();
        created!.PasswordByte.Should().NotBeNullOrEmpty();
        Encoding.ASCII.GetString(created.PasswordByte!).Should().NotBe(password);
        BCrypt.Net.BCrypt.Verify(password, Encoding.ASCII.GetString(created.PasswordByte!)).Should().BeTrue();
    }

    [Test]
    public async Task UpdateCredential_WithValidData_UpdatesUsername()
    {
        var credential = await SeedCredential();
        var newUsername = UniqueUsername();

        var request = new UpdateCredentialRequest
        {
            UserName = newUsername,
            ExpectedConcurrencyStamp = credential.ConcurrencyStamp,
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PatchAsJsonAsync(
            $"/api/credentials/{credential.Id}", request);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"Response body: {await response.Content.ReadAsStringAsync()}");

        await using var db = CreateDbContext();
        var updated = await db.Set<IdentityCredential>()
            .FirstOrDefaultAsync(c => c.Id == credential.Id);

        updated.Should().NotBeNull();
        updated!.UserName.Should().Be(newUsername);
        updated.IsEnabled.Should().BeTrue("an omitted nullable field must not change credential lifecycle state");
    }

    [Test]
    public async Task UpdateCredential_NonExistentId_Returns404()
    {
        var fakeId = Guid.NewGuid();
        var request = new UpdateCredentialRequest
        {
            UserName = "nonexistent",
            ExpectedConcurrencyStamp = Guid.NewGuid(),
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PatchAsJsonAsync(
            $"/api/credentials/{fakeId}", request);

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            $"Response body: {await response.Content.ReadAsStringAsync()}");
    }

    [Test]
    public async Task UpdateCredential_WhenBodyIdDiffersFromRoute_Returns400WithoutMutation()
    {
        var credential = await SeedCredential();
        var originalUserName = credential.UserName;
        var response = await HttpClient.PatchAsJsonAsync(
            $"/api/credentials/{credential.Id}",
            new UpdateCredentialRequest
            {
                CredentialId = Guid.NewGuid(),
                UserName = UniqueUsername(),
                ExpectedConcurrencyStamp = credential.ConcurrencyStamp,
                Metadata = CreateMetadata()
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var db = CreateDbContext();
        var unchanged = await db.Set<IdentityCredential>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == credential.Id);
        unchanged.UserName.Should().Be(originalUserName);
        unchanged.IsEnabled.Should().BeTrue();
    }

    [Test]
    public async Task UpdateCredential_DisablingWithManySessions_UsesBoundedBulkRevocation()
    {
        var credential = await SeedCredential();
        var sessionIds = await SeedActiveSessionsAsync(credential.Id, 100);

        await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Role, "SuperAdmin"),
                    new Claim("tenant_id", IntegrationTestFixture.TestTenantId.ToString())
                ],
                "IdentityServerIntegrationTest"))
        };

        var commandCounter = IntegrationTestFixture.Services
            .GetRequiredService<DbCommandCounterInterceptor>();
        using var measurement = commandCounter.BeginMeasurement();
        var metadata = CreateMetadata();
        var credentialFeatureKey = TenantModuleFeatureKeys.Combine(
            TenantModuleFeatureKeys.Identity,
            TenantModuleFeatureKeys.CredentialsSubFeature);
        var updateCapability = $"{credentialFeatureKey}:{IdentityAuthorizationConstants.Update}";
        IntegrationTestFixture.EstablishTrustedActorContext(
            scope.ServiceProvider,
            IntegrationTestFixture.TestTenantId,
            IntegrationTestFixture.TestCredentialId,
            capabilities: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                updateCapability
            });
        var result = await scope.ServiceProvider.GetRequiredService<IAuthService>()
            .UpdateCredentialAsync(
                new UpdateCredentialRequest
                {
                    CredentialId = credential.Id,
                    UserName = credential.UserName,
                    IsEnabled = false,
                    ExpectedConcurrencyStamp = credential.ConcurrencyStamp,
                    Metadata = metadata
                });

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

    private static RequestMetadata CreateMetadata() => new()
    {
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        OperationName = "IntegrationTest",
        DeviceName = "TestDevice",
        UserAgent = "TestAgent"
    };

    private async Task<IdentityInformation> SeedIdentityInfo()
    {
        await using var db = CreateDbContext();
        var info = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityInformation>().Add(info);
        await db.SaveChangesAsync();
        return info;
    }

    private async Task<IdentityCredential> SeedCredential(string password = "TestPassword123!")
    {
        await using var db = CreateDbContext();

        var info = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityInformation>().Add(info);

        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            UserName = UniqueUsername(),
            PasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11)),
            IdentityInfoId = info.Id,
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<IdentityCredential>().Add(credential);

        await db.SaveChangesAsync();
        return credential;
    }

    private async Task<List<Guid>> SeedActiveSessionsAsync(Guid credentialId, int count)
    {
        await using var db = CreateDbContext();
        var sessionTypeId = await db.Set<SessionType>()
            .IgnoreQueryFilters()
            .Where(type => type.TenantId == IntegrationTestFixture.TestTenantId)
            .Where(type => type.SystemReferenceId == IdentityConstants.SessionType.User)
            .Select(type => type.Id)
            .SingleAsync();
        var sessions = Enumerable.Range(0, count)
            .Select(_ => new Session
            {
                Id = Guid.NewGuid(),
                TenantId = IntegrationTestFixture.TestTenantId,
                CredentialId = credentialId,
                SessionTypeId = sessionTypeId,
                Status = CurrentSessionState.Active,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            })
            .ToList();
        db.Set<Session>().AddRange(sessions);
        await db.SaveChangesAsync();
        return sessions.Select(session => session.Id).ToList();
    }
}
