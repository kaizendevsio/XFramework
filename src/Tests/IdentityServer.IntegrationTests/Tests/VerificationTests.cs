using System.Net;
using System.Text;
using System.Text.Json;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;
using System.Security.Cryptography;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
public class VerificationTests : IntegrationTestBase
{
    [SetUp]
    public void ResetWorkflowFailureInjection() => IdentityServerWorkflowFailureInjection.Reset();

    #region HTTP Tests

    [Test]
    public async Task Http_CreateVerification_WithValidCredential_CreatesRecord()
    {
        var credential = await SeedCredentialWithContact();
        var verificationType = await GetSmsVerificationType();

        var request = new Create<IdentityVerification>(new IdentityVerification
        {
            CredentialId = credential.Id,
            VerificationTypeId = verificationType.Id,
            TenantId = IntegrationTestFixture.TestTenantId
        })
        {
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/verifications", request);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"Response body: {await response.Content.ReadAsStringAsync()}");

        using var responseDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var responseData = responseDocument.RootElement;
        responseData.GetProperty("credentialId").GetGuid().Should().Be(credential.Id);
        responseData.TryGetProperty("token", out _).Should().BeFalse();
        responseData.TryGetProperty("tokenHash", out _).Should().BeFalse();
        await using var db = CreateDbContext();
        var verification = await db.Set<IdentityVerification>()
            .Where(v => v.CredentialId == credential.Id)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync();

        verification.Should().NotBeNull();
        verification!.TenantId.Should().Be(IntegrationTestFixture.TestTenantId);
        verification.TokenHash.Should().StartWith("$2");
        verification.Purpose.Should().Be(IdentityConstants.VerificationPurpose.ContactVerification);
        verification.Token.Should().BeNull();
        verification.Status.Should().Be((short)GenericStatusType.Pending);
    }

    [Test]
    public async Task Http_CreateVerification_WhenDeliveryFails_InvalidatesVerification()
    {
        var credential = await SeedCredentialWithContact();
        var verificationType = await GetSmsVerificationType();
        IdentityServerWorkflowFailureInjection.FailCommunicationsDelivery = true;
        var request = new Create<IdentityVerification>(new IdentityVerification
        {
            CredentialId = credential.Id,
            VerificationTypeId = verificationType.Id,
            TenantId = IntegrationTestFixture.TestTenantId
        })
        {
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/verifications", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var verification = await WaitForCanceledVerificationAsync(credential.Id);
        verification.Status.Should().Be((short)GenericStatusType.Canceled);
        verification.ConsumedAt.Should().NotBeNull();
        verification.IsEnabled.Should().BeFalse();

        await using var db = CreateDbContext();
        var outbox = await db.Set<VerificationDeliveryOutboxMessage>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.VerificationId == verification.Id);
        outbox.DeadLetteredAt.Should().NotBeNull();
        outbox.Recipient.Should().BeNull();
        outbox.Message.Should().BeNull();
    }

    [Test]
    public async Task Http_ConfirmVerification_WithValidToken_ApprovesVerification()
    {
        var credential = await SeedCredentialWithContact();
        var token = "confirm_http_" + Guid.NewGuid().ToString("N")[..6];
        var verificationId = await SeedPendingVerification(credential.Id, token);

        var response = await HttpClient.PatchAsJsonAsync(
            $"/api/verifications/{verificationId}/confirm",
            new { token });

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"Response body: {await response.Content.ReadAsStringAsync()}");

        using var responseDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var responseData = responseDocument.RootElement;
        responseData.GetProperty("id").GetGuid().Should().Be(verificationId);
        responseData.TryGetProperty("token", out _).Should().BeFalse();
        responseData.TryGetProperty("tokenHash", out _).Should().BeFalse();

        await using var db = CreateDbContext();
        var verification = await db.Set<IdentityVerification>()
            .Where(v => v.Id == verificationId)
            .FirstOrDefaultAsync();

        verification.Should().NotBeNull();
        verification!.Status.Should().Be((short)GenericStatusType.Approved);
    }

    [Test]
    public async Task Http_ConfirmVerification_WithInvalidToken_Returns400()
    {
        var credential = await SeedCredentialWithContact();
        var verificationId = await SeedPendingVerification(credential.Id, "valid-token");
        var response = await HttpClient.PatchAsJsonAsync(
            $"/api/verifications/{verificationId}/confirm",
            new { token = "invalid_token_999" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Test]
    public async Task Http_ConfirmVerification_WithoutToken_ReturnsValidationProblem()
    {
        var response = await HttpClient.PatchAsJsonAsync(
            $"/api/verifications/{Guid.NewGuid()}/confirm",
            new { token = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").GetProperty("Token")
            .EnumerateArray().Select(message => message.GetString())
            .Should().Contain("Verification token is required");
    }

    [Test]
    public async Task Http_ConfirmVerification_AfterFiveInvalidAttempts_ConsumesAndDeniesChallenge()
    {
        var credential = await SeedCredentialWithContact();
        var validToken = "valid_" + Guid.NewGuid().ToString("N")[..8];
        var verificationId = await SeedPendingVerification(credential.Id, validToken);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var invalid = await HttpClient.PatchAsJsonAsync(
                $"/api/verifications/{verificationId}/confirm",
                new { token = $"invalid_{attempt}_{Guid.NewGuid():N}" });
            invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        var validAfterLimit = await HttpClient.PatchAsJsonAsync(
            $"/api/verifications/{verificationId}/confirm",
            new { token = validToken });
        validAfterLimit.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var db = CreateDbContext();
        var verification = await db.Set<IdentityVerification>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == verificationId);
        verification.FailedAttempts.Should().Be(5);
        verification.Status.Should().Be((short)GenericStatusType.AccessDenied);
        verification.ConsumedAt.Should().NotBeNull();
    }

    [Test]
    public async Task Http_ConfirmVerification_ParallelInvalidAttempts_AreCountedAtomicallyToLimit()
    {
        var credential = await SeedCredentialOnly();
        var verificationId = await SeedPendingVerification(
            credential.Id,
            "valid_" + Guid.NewGuid().ToString("N")[..8]);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, 12)
            .Select(async attempt =>
            {
                await start.Task;
                return await HttpClient.PatchAsJsonAsync(
                    $"/api/verifications/{verificationId}/confirm",
                    new { token = $"invalid_{attempt}_{Guid.NewGuid():N}" });
            })
            .ToArray();

        start.SetResult();
        var responses = await Task.WhenAll(attempts);

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.BadRequest);
        foreach (var response in responses)
            response.Dispose();

        await using var db = CreateDbContext();
        var verification = await db.Set<IdentityVerification>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == verificationId);
        verification.FailedAttempts.Should().Be(5);
        verification.Status.Should().Be((short)GenericStatusType.AccessDenied);
        verification.ConsumedAt.Should().NotBeNull();
    }

    [Test]
    public async Task Http_ConfirmVerification_WithExpiredToken_Returns400()
    {
        var credential = await SeedCredentialWithContact();
        var token = "expired_http_" + Guid.NewGuid().ToString("N")[..6];
        var verificationId = await SeedPendingVerification(
            credential.Id, token, DateTime.UtcNow.AddMinutes(-1));

        var response = await HttpClient.PatchAsJsonAsync(
            $"/api/verifications/{verificationId}/confirm",
            new { token });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var db = CreateDbContext();
        var verification = await db.Set<IdentityVerification>()
            .Where(v => v.Id == verificationId)
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

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"Response body: {await response.Content.ReadAsStringAsync()}");

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

    private async Task<IdentityCredential> SeedCredentialOnly()
    {
        await using var db = CreateDbContext();
        var identity = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            FirstName = "Verification",
            LastName = "Concurrency",
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            UserName = UniqueUsername(),
            PasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword("TestPassword123!", workFactor: 11)),
            IdentityInfoId = identity.Id,
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<IdentityInformation>().Add(identity);
        db.Set<IdentityCredential>().Add(credential);
        await db.SaveChangesAsync();
        return credential;
    }

    private async Task<IdentityCredential> SeedCredentialWithContact()
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
                BCrypt.Net.BCrypt.HashPassword("TestPassword123!", workFactor: 11)),
            IdentityInfoId = info.Id,
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityCredential>().Add(credential);

        var otpGroup = await db.Set<RegistryConfigurationGroup>()
            .FirstOrDefaultAsync(group =>
                group.TenantId == IntegrationTestFixture.TestTenantId &&
                group.Name == "CommunicationsService_Otp");
        if (otpGroup is null)
        {
            otpGroup = new RegistryConfigurationGroup
            {
                Id = Guid.NewGuid(),
                Name = "CommunicationsService_Otp",
                TenantId = IntegrationTestFixture.TestTenantId,
                SystemReferenceId = Guid.NewGuid(),
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };
            db.Set<RegistryConfigurationGroup>().Add(otpGroup);
        }

        if (!await db.Set<RegistryConfiguration>().AnyAsync(configuration =>
                configuration.TenantId == IntegrationTestFixture.TestTenantId &&
                configuration.GroupId == otpGroup.Id))
        {
            db.Set<RegistryConfiguration>().Add(new RegistryConfiguration
            {
                Id = Guid.NewGuid(),
                Key = "OtpMessage",
                Value = "Your verification code is |Value|.",
                GroupId = otpGroup.Id,
                TenantId = IntegrationTestFixture.TestTenantId,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }

        // Ensure ContactGroup exists
        var contactGroupId = Guid.Parse("d1d2d3d4-e5f6-7890-abcd-ef1234567890");
        if (!await db.Set<IdentityContactGroup>().AnyAsync(g => g.Id == contactGroupId))
        {
            db.Set<IdentityContactGroup>().Add(new IdentityContactGroup
            {
                Id = contactGroupId,
                Name = "Default",
                TenantId = IntegrationTestFixture.TestTenantId,
                IsEnabled = true
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
                TenantId = IntegrationTestFixture.TestTenantId,
                IsEnabled = true
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
            TenantId = IntegrationTestFixture.TestTenantId,
            IsEnabled = true
        });

        await db.SaveChangesAsync();
        return credential;
    }

    private async Task<IdentityVerificationType> GetSmsVerificationType()
    {
        await using var db = CreateDbContext();
        return (await db.Set<IdentityVerificationType>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == IdentityConstants.VerificationType.Sms))!;
    }

    private async Task<Guid> SeedPendingVerification(
        Guid credentialId,
        string token,
        DateTime? expiry = null)
    {
        await using var db = CreateDbContext();

        var verificationType = await db.Set<IdentityVerificationType>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == IdentityConstants.VerificationType.Sms);

        var verification = new IdentityVerification
        {
            Id = Guid.NewGuid(),
            CredentialId = credentialId,
            VerificationTypeId = verificationType!.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(token, workFactor: 11),
            Purpose = IdentityConstants.VerificationPurpose.ContactVerification,
            Status = (short)GenericStatusType.Pending,
            StatusUpdatedOn = DateTimeOffset.UtcNow,
            Expiry = expiry ?? DateTime.UtcNow.AddMinutes(10),
            TenantId = IntegrationTestFixture.TestTenantId,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<IdentityVerification>().Add(verification);
        await db.SaveChangesAsync();
        return verification.Id;
    }

    private async Task<IdentityVerification> WaitForCanceledVerificationAsync(Guid credentialId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = CreateDbContext();
            var verification = await db.Set<IdentityVerification>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.CredentialId == credentialId)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync();
            if (verification is { IsEnabled: false, ConsumedAt: not null })
                return verification;

            await Task.Delay(100);
        }

        throw new AssertionException("Verification delivery failure was not finalized within 10 seconds.");
    }

    #endregion
}
