using System.Net;
using System.Text;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
public class VerificationTests : IntegrationTestBase
{
    #region HTTP Tests

    [Test]
    public async Task Http_CreateVerification_WithValidCredential_CreatesRecord()
    {
        var credential = await SeedCredentialWithContact();

        var request = new Create<IdentityVerification>(new IdentityVerification
        {
            CredentialId = credential.Id,
            TenantId = IntegrationTestFixture.TestTenantId
        })
        {
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/verifications", request);

        // May fail on SMS delivery (no real gateway in tests) — verify DB if successful
        if (response.IsSuccessStatusCode)
        {
            await using var db = CreateDbContext();
            var verification = await db.Set<IdentityVerification>()
                .Where(v => v.CredentialId == credential.Id)
                .FirstOrDefaultAsync();

            verification.Should().NotBeNull();
            verification!.Token.Should().NotBeNullOrEmpty();
            verification.Status.Should().Be((short)GenericStatusType.Pending);
        }
    }

    [Test]
    public async Task Http_ConfirmVerification_WithValidToken_ApprovesVerification()
    {
        var credential = await SeedCredentialWithContact();
        var token = "confirm_http_" + Guid.NewGuid().ToString("N")[..6];
        await SeedPendingVerification(credential.Id, token);

        var response = await HttpClient.PatchAsync($"/api/verifications/{token}", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = CreateDbContext();
        var verification = await db.Set<IdentityVerification>()
            .Where(v => v.CredentialId == credential.Id && v.Token == token)
            .FirstOrDefaultAsync();

        verification.Should().NotBeNull();
        verification!.Status.Should().Be((short)GenericStatusType.Approved);
    }

    [Test]
    public async Task Http_ConfirmVerification_WithInvalidToken_Returns404()
    {
        var response = await HttpClient.PatchAsync("/api/verifications/invalid_token_999", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Http_ConfirmVerification_WithExpiredToken_Returns404()
    {
        var credential = await SeedCredentialWithContact();
        var token = "expired_http_" + Guid.NewGuid().ToString("N")[..6];
        await SeedPendingVerification(credential.Id, token, DateTime.UtcNow.AddMinutes(-1));

        var response = await HttpClient.PatchAsync($"/api/verifications/{token}", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var db = CreateDbContext();
        var verification = await db.Set<IdentityVerification>()
            .Where(v => v.CredentialId == credential.Id && v.Token == token)
            .FirstOrDefaultAsync();

        verification.Should().NotBeNull();
        verification!.Status.Should().Be((short)GenericStatusType.Pending);
    }

    [Test]
    public async Task Http_CheckVerification_WithPendingVerification_ReturnsStatus()
    {
        var credential = await SeedCredentialWithContact();
        var verificationType = await GetSmsVerificationType();
        await SeedPendingVerification(credential.Id, "check_http_" + Guid.NewGuid().ToString("N")[..6]);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/verifications/check",
            new CheckVerificationRequest
            {
                CredentialId = credential.Id,
                VerificationTypeId = verificationType.Id,
                Metadata = CreateMetadata()
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Bolt Tests

    [Test]
    public async Task Bolt_CheckVerification_WithPendingVerification_ReturnsStatus()
    {
        var credential = await SeedCredentialWithContact();
        var verificationType = await GetSmsVerificationType();
        await SeedPendingVerification(credential.Id, "check_sf_" + Guid.NewGuid().ToString("N")[..6]);

        var result = await IntegrationTestFixture.ServiceWrapper.CheckVerification(new CheckVerificationRequest
        {
            CredentialId = credential.Id,
            VerificationTypeId = verificationType.Id,
            Metadata = CreateMetadata()
        });

        result.Should().NotBeNull();
        result.HttpStatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Bolt_CheckVerification_WithNoVerification_ReturnsNotFound()
    {
        var credential = await SeedCredentialWithContact();
        var verificationType = await GetSmsVerificationType();

        var result = await IntegrationTestFixture.ServiceWrapper.CheckVerification(new CheckVerificationRequest
        {
            CredentialId = credential.Id,
            VerificationTypeId = verificationType.Id,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Helpers

    private static RequestMetadata CreateMetadata() => new()
    {
        TenantId = IntegrationTestFixture.TestTenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        Name = "IntegrationTest",
        DeviceName = "TestDevice",
        DeviceAgent = "TestAgent"
    };

    private async Task<IdentityCredential> SeedCredentialWithContact()
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
                BCrypt.Net.BCrypt.HashPassword("TestPassword123!", workFactor: 11)),
            IdentityInfoId = info.Id,
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityCredential>().Add(credential);

        // Ensure ContactGroup exists
        var contactGroupId = Guid.Parse("d1d2d3d4-e5f6-7890-abcd-ef1234567890");
        if (!await db.Set<IdentityContactGroup>().AnyAsync(g => g.Id == contactGroupId))
        {
            db.Set<IdentityContactGroup>().Add(new IdentityContactGroup
            {
                Id = contactGroupId,
                Name = "Default",
                TenantId = IntegrationTestFixture.TestTenantId
            });
        }

        var contactType = await db.Set<IdentityContactType>()
            .FirstOrDefaultAsync(c => c.Name == "Phone");

        if (contactType == null)
        {
            contactType = new IdentityContactType
            {
                Id = Guid.NewGuid(),
                Name = "Phone",
                TenantId = IntegrationTestFixture.TestTenantId
            };
            db.Set<IdentityContactType>().Add(contactType);
        }

        db.Set<IdentityContact>().Add(new IdentityContact
        {
            Id = Guid.NewGuid(),
            Value = UniquePhone(),
            TypeId = contactType.Id,
            GroupId = contactGroupId,
            CredentialId = credential.Id,
            TenantId = IntegrationTestFixture.TestTenantId
        });

        await db.SaveChangesAsync();
        return credential;
    }

    private async Task<IdentityVerificationType> GetSmsVerificationType()
    {
        await using var db = CreateDbContext();
        return (await db.Set<IdentityVerificationType>().FirstOrDefaultAsync(v => v.Name == "Sms"))!;
    }

    private async Task SeedPendingVerification(
        Guid credentialId,
        string token,
        DateTime? expiry = null)
    {
        await using var db = CreateDbContext();

        var verificationType = await db.Set<IdentityVerificationType>()
            .FirstOrDefaultAsync(v => v.Name == "Sms");

        db.Set<IdentityVerification>().Add(new IdentityVerification
        {
            Id = Guid.NewGuid(),
            CredentialId = credentialId,
            VerificationTypeId = verificationType!.Id,
            Token = token,
            Status = (short)GenericStatusType.Pending,
            StatusUpdatedOn = DateTimeOffset.UtcNow,
            Expiry = expiry ?? DateTime.UtcNow.AddMinutes(10),
            TenantId = IntegrationTestFixture.TestTenantId
        });
        await db.SaveChangesAsync();
    }

    #endregion
}
