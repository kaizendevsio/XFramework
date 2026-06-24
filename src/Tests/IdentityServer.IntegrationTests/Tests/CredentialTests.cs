using System.Net;
using System.Text;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

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

        response.StatusCode.Should().Be(HttpStatusCode.OK);

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

        response.StatusCode.Should().Be(HttpStatusCode.OK);

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

        var request = new Patch<IdentityCredential>(new IdentityCredential
        {
            Id = credential.Id,
            UserName = newUsername,
            IsEnabled = true
        })
        {
            Metadata = CreateMetadata()
        };

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
        })
        {
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PatchAsJsonAsync(
            $"/api/credentials/{fakeId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static RequestMetadata CreateMetadata() => new()
    {
        TenantId = IntegrationTestFixture.TestTenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        Name = "IntegrationTest",
        DeviceName = "TestDevice",
        DeviceAgent = "TestAgent"
    };

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
}
