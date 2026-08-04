using System.Net;
using System.Text;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
public class PasswordTests : IntegrationTestBase
{
    #region HTTP — VerifyPassword

    [Test]
    public async Task Http_VerifyPassword_WithoutAuthentication_ReturnsUnauthorized()
    {
        var password = "CorrectPassword123!";
        var credential = await SeedCredential(password: password);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/verify-password")
        {
            Content = JsonContent.Create(new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = password
            })
        };
        request.Headers.Add(TestAuthHeaders.Unauthenticated, "true");

        var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region HTTP — ChangePassword

    [Test]
    public async Task Http_ChangePassword_WithoutAuthentication_ReturnsUnauthorized()
    {
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";
        var credential = await SeedCredential(password: oldPassword);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest
            {
                CreadentialId = credential.Id,
                NewPassword = newPassword,
                VerificationId = Guid.NewGuid()
            })
        };
        request.Headers.Add(TestAuthHeaders.Unauthenticated, "true");

        var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Bolt — VerifyPassword

    [Test]
    public async Task Bolt_VerifyPassword_WithCorrectPassword_ReturnsOk()
    {
        var password = "CorrectPassword123!";
        var credential = await SeedCredential(password: password);

        var result = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = password,
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Bolt_VerifyPassword_WithWrongPassword_Returns400()
    {
        var credential = await SeedCredential(password: "CorrectPassword123!");

        var result = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = "WrongPassword!",
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Bolt_VerifyPassword_WithEmptyPassword_Returns400()
    {
        var credential = await SeedCredential();

        var result = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = "",
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Bolt_VerifyPassword_WithEmptyCredentialId_Returns400()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = Guid.Empty,
                Password = "SomePassword!",
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Bolt_VerifyPassword_WithNonExistentCredential_Returns404()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = Guid.NewGuid(),
                Password = "SomePassword!",
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Bolt — ChangePassword

    [Test]
    public async Task Bolt_ChangePassword_WithApprovedVerification_ConsumesProofAndRejectsReplay()
    {
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";
        var credential = await SeedCredential(password: oldPassword);
        var verification = await SeedApprovedVerification(credential.Id);
        var sessionId = await SeedActiveSession(credential.Id);

        var result = await IntegrationTestFixture.ServiceWrapper.ChangePassword(
            new ChangePasswordRequest
            {
                CreadentialId = credential.Id,
                NewPassword = newPassword,
                VerificationId = verification.Id,
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);

        await using (var db = CreateDbContext())
        {
            var persistedVerification = await db.Set<IdentityVerification>()
                .IgnoreQueryFilters()
                .FirstAsync(v => v.Id == verification.Id);

            persistedVerification.ConsumedAt.Should().NotBeNull();
            var session = await db.Set<Session>()
                .IgnoreQueryFilters()
                .FirstAsync(item => item.Id == sessionId);
            session.Status.Should().Be(CurrentSessionState.Inactive);
        }

        // Verify via Bolt: old password fails
        var verifyOld = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = oldPassword,
                Metadata = CreateMetadata()
            });
        verifyOld.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Verify via Bolt: new password works
        var verifyNew = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = newPassword,
                Metadata = CreateMetadata()
            });
        verifyNew.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        var replay = await IntegrationTestFixture.ServiceWrapper.ChangePassword(
            new ChangePasswordRequest
            {
                CreadentialId = credential.Id,
                NewPassword = "ReplayPassword789!",
                VerificationId = verification.Id,
                Metadata = CreateMetadata()
            });

        replay.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        replay.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task Bolt_ChangePassword_ConcurrentConsumers_AllowExactlyOnePasswordChange()
    {
        var credential = await SeedCredential(password: "OriginalPassword123!");
        var verification = await SeedApprovedVerification(credential.Id);
        const string firstPassword = "ConcurrentFirst123!";
        const string secondPassword = "ConcurrentSecond123!";
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<CmdResponse> ChangePasswordAsync(string password)
        {
            await start.Task;
            return await IntegrationTestFixture.ServiceWrapper.ChangePassword(new ChangePasswordRequest
            {
                CreadentialId = credential.Id,
                NewPassword = password,
                VerificationId = verification.Id,
                Metadata = CreateMetadata()
            });
        }

        var attempts = new[]
        {
            ChangePasswordAsync(firstPassword),
            ChangePasswordAsync(secondPassword)
        };
        start.SetResult();
        var results = await Task.WhenAll(attempts);

        results.Count(result => result.IsSuccess).Should().Be(1);
        var verificationResults = await Task.WhenAll(
            IntegrationTestFixture.ServiceWrapper.VerifyPassword(new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = firstPassword,
                Metadata = CreateMetadata()
            }),
            IntegrationTestFixture.ServiceWrapper.VerifyPassword(new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = secondPassword,
                Metadata = CreateMetadata()
            }));
        verificationResults.Count(result => result.IsSuccess).Should().Be(1);
    }

    [Test]
    public async Task Bolt_ChangePassword_WithEmptyCredentialId_Returns400()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.ChangePassword(
            new ChangePasswordRequest
            {
                CreadentialId = Guid.Empty,
                NewPassword = "NewPassword!",
                VerificationId = Guid.NewGuid(),
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Bolt_ChangePassword_WithEmptyNewPassword_Returns400()
    {
        var credential = await SeedCredential();

        var result = await IntegrationTestFixture.ServiceWrapper.ChangePassword(
            new ChangePasswordRequest
            {
                CreadentialId = credential.Id,
                NewPassword = "",
                VerificationId = Guid.NewGuid(),
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Bolt_ChangePassword_WithNonExistentCredential_Returns404()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.ChangePassword(
            new ChangePasswordRequest
            {
                CreadentialId = Guid.NewGuid(),
                NewPassword = "NewPassword123!",
                VerificationId = Guid.NewGuid(),
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Helpers

    private static RequestMetadata CreateMetadata() => new()
    {
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        OperationName = "IntegrationTest",
        DeviceName = "TestDevice",
        UserAgent = "TestAgent"
    };

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
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityCredential>().Add(credential);

        await db.SaveChangesAsync();
        return credential;
    }

    private async Task<IdentityVerification> SeedApprovedVerification(Guid credentialId)
    {
        await using var db = CreateDbContext();
        var verification = new IdentityVerification
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            CredentialId = credentialId,
            VerificationTypeId = IdentityConstants.VerificationType.Sms,
            Purpose = IdentityConstants.VerificationPurpose.ContactVerification,
            Status = (short)GenericStatusType.Approved,
            StatusUpdatedOn = DateTimeOffset.UtcNow,
            Expiry = DateTime.UtcNow.AddMinutes(10),
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<IdentityVerification>().Add(verification);
        await db.SaveChangesAsync();
        return verification;
    }

    private async Task<Guid> SeedActiveSession(Guid credentialId)
    {
        await using var db = CreateDbContext();
        var sessionTypeId = await db.Set<SessionType>()
            .IgnoreQueryFilters()
            .Where(type => type.TenantId == IntegrationTestFixture.TestTenantId)
            .Where(type => type.SystemReferenceId == IdentityConstants.SessionType.User)
            .Select(type => type.Id)
            .FirstAsync();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            CredentialId = credentialId,
            SessionTypeId = sessionTypeId,
            Status = CurrentSessionState.Active,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<Session>().Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    #endregion
}
