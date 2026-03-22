using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;

namespace IdentityServer.IntegrationTests.Tests;

/// <summary>
/// Integration tests for credential CRUD operations.
/// Credential CRUD uses generic Create/Patch requests — no dedicated StreamFlow wrapper methods.
/// These are HTTP-only tests (entity CRUD goes through REST endpoints, not StreamFlow wrappers).
/// </summary>
[TestFixture]
public class CredentialTests : IntegrationTestBase
{
    [Test]
    public async Task CreateCredential_WithValidData_ReturnsCreated()
    {
        var identityInfo = await SeedIdentityInfo();

        var request = new Create<IdentityCredential>(new IdentityCredential
        {
            UserName = UniqueUsername(),
            Password = "TestPassword123!",
            IdentityInfoId = identityInfo.Id,
            TenantId = IntegrationTestFixture.TestTenantId
        });

        var response = await HttpClient.PostAsJsonAsync("/api/credentials", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var db = CreateDbContext();
        var credential = await db.Set<IdentityCredential>()
            .FirstOrDefaultAsync(c => c.UserName == request.Model.UserName);

        credential.Should().NotBeNull();
        credential!.PasswordByte.Should().NotBeNull();

        var storedHash = Encoding.ASCII.GetString(credential.PasswordByte);
        storedHash.Should().StartWith("$2");
    }

    [Test]
    public async Task CreateCredential_PasswordIsHashed_NotStoredPlaintext()
    {
        var identityInfo = await SeedIdentityInfo();
        var plainPassword = "MySecret123!";

        var request = new Create<IdentityCredential>(new IdentityCredential
        {
            UserName = UniqueUsername(),
            Password = plainPassword,
            IdentityInfoId = identityInfo.Id,
            TenantId = IntegrationTestFixture.TestTenantId
        });

        await HttpClient.PostAsJsonAsync("/api/credentials", request);

        await using var db = CreateDbContext();
        var credential = await db.Set<IdentityCredential>()
            .FirstOrDefaultAsync(c => c.UserName == request.Model.UserName);

        credential.Should().NotBeNull();
        var storedHash = Encoding.ASCII.GetString(credential!.PasswordByte);
        storedHash.Should().NotBe(plainPassword);
        storedHash.Should().StartWith("$2");
    }

    [Test]
    public async Task UpdateCredential_WithValidData_UpdatesUsername()
    {
        var credential = await SeedCredential();
        var newUsername = UniqueUsername();

        var request = new Patch<IdentityCredential>(new IdentityCredential
        {
            Id = credential.Id,
            UserName = newUsername,
            IsEnabled = true
        });

        var response = await HttpClient.PatchAsJsonAsync(
            $"/api/credentials/{credential.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = CreateDbContext();
        var updated = await db.Set<IdentityCredential>()
            .FirstOrDefaultAsync(c => c.Id == credential.Id);

        updated.Should().NotBeNull();
        updated!.UserName.Should().Be(newUsername);
    }

    [Test]
    public async Task UpdateCredential_NonExistentId_Returns404()
    {
        var fakeId = Guid.NewGuid();
        var request = new Patch<IdentityCredential>(new IdentityCredential
        {
            Id = fakeId,
            UserName = "nonexistent"
        });

        var response = await HttpClient.PatchAsJsonAsync(
            $"/api/credentials/{fakeId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #region Helpers

    private async Task<IdentityInformation> SeedIdentityInfo()
    {
        await using var db = CreateDbContext();
        var info = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
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
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityCredential>().Add(credential);

        await db.SaveChangesAsync();
        return credential;
    }

    #endregion
}
