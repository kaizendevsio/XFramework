using System.Net;
using System.Text;
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
public sealed class WrapperCoverageTests : IntegrationTestBase
{
    [Test]
    public async Task CreateCredential_WithValidData_HashesPasswordAndCreatesCredential()
    {
        var info = await SeedIdentityInfo();
        var username = UniqueUsername();
        var password = "WrapperPassword123!";

        var result = await IntegrationTestFixture.ServiceWrapper.CreateCredential(new CreateCredentialRequest
        {
            IdentityInfoId = info.Id,
            UserName = username,
            UserAlias = "Wrapper Alias",
            Password = password,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();

        await using var db = CreateDbContext();
        var credential = await db.Set<IdentityCredential>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.IdentityInfoId == info.Id && c.UserName == username);

        credential.Should().NotBeNull();
        credential!.PasswordByte.Should().NotBeNullOrEmpty();
        BCrypt.Net.BCrypt.Verify(password, Encoding.ASCII.GetString(credential.PasswordByte!)).Should().BeTrue();

        var verify = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = password,
                Metadata = CreateMetadata()
            });

        verify.HttpStatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ForgotPassword_WithUnknownEmail_ReturnsSuccess()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.ForgotPassword(new ForgotPasswordRequest
        {
            Email = UniqueEmail(),
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task Logout_WithActiveSession_DeactivatesSession()
    {
        var auth = await AuthenticateThroughWrapper();
        var sessionId = auth.Response!.SessionId!.Value;
        var credentialId = auth.Response.Credential!.Id;

        var result = await IntegrationTestFixture.ServiceWrapper.Logout(new LogoutRequest
        {
            SessionId = sessionId,
            CredentialId = credentialId,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();

        await using var db = CreateDbContext();
        var session = await db.Set<Session>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        session.Should().NotBeNull();
        session!.Status.Should().Be(CurrentSessionState.Inactive);
    }

    [Test]
    public async Task Logout_WithUnknownSession_ReturnsNotFound()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.Logout(new LogoutRequest
        {
            SessionId = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task RefreshToken_WithValidTokens_ReturnsNewTokenPair()
    {
        var auth = await AuthenticateThroughWrapper();
        var authResponse = auth.Response!;

        var result = await IntegrationTestFixture.ServiceWrapper.RefreshToken(new RefreshTokenRequest
        {
            AccessToken = authResponse.AccessToken,
            RefreshToken = authResponse.RefreshToken,
            SessionId = authResponse.SessionId!.Value,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.Response.Should().NotBeNull();
        result.Response!.AccessToken.Should().NotBeNullOrEmpty();
        result.Response.RefreshToken.Should().NotBeNullOrEmpty();
        result.Response.SessionId.Should().Be(authResponse.SessionId.Value);
        result.Response.ExpiresIn.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task RefreshToken_WithInvalidRefreshToken_ReturnsUnauthorized()
    {
        var auth = await AuthenticateThroughWrapper();
        var authResponse = auth.Response!;

        var result = await IntegrationTestFixture.ServiceWrapper.RefreshToken(new RefreshTokenRequest
        {
            AccessToken = authResponse.AccessToken,
            RefreshToken = "invalid_refresh_token",
            SessionId = authResponse.SessionId!.Value,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task ResetPassword_WithValidToken_ChangesPasswordAndApprovesVerification()
    {
        const string oldPassword = "OldPassword123!";
        const string newPassword = "NewPassword456!";
        var credential = await SeedCredentialWithRole(UniqueUsername(), oldPassword);
        var token = await SeedPendingPasswordResetVerification(credential.Id);

        var result = await IntegrationTestFixture.ServiceWrapper.ResetPassword(new ResetPasswordRequest
        {
            Token = token,
            NewPassword = newPassword,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();

        var verifyOld = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = oldPassword,
                Metadata = CreateMetadata()
            });
        verifyOld.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);

        var verifyNew = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = newPassword,
                Metadata = CreateMetadata()
            });
        verifyNew.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = CreateDbContext();
        var verification = await db.Set<IdentityVerification>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Token == token);

        verification.Should().NotBeNull();
        verification!.Status.Should().Be((short)GenericStatusType.Approved);
    }

    [Test]
    public async Task ResetPassword_WithInvalidToken_ReturnsBadRequest()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.ResetPassword(new ResetPasswordRequest
        {
            Token = "missing_" + Guid.NewGuid().ToString("N"),
            NewPassword = "NewPassword456!",
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
    }

    private async Task<QueryResponse<AuthenticateIdentityResponse>> AuthenticateThroughWrapper()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        await SeedCredentialWithRole(username, password);

        var result = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(new AuthenticateIdentityRequest
        {
            UserName = username,
            Password = password,
            RoleId = TestData.RoleTypeId,
            AuthorizationType = AuthorizationType.Default,
            GenerateToken = true,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.Response.Should().NotBeNull();
        result.Response!.AccessToken.Should().NotBeNullOrEmpty();
        result.Response.RefreshToken.Should().NotBeNullOrEmpty();
        result.Response.SessionId.Should().NotBeNull();
        result.Response.Credential.Should().NotBeNull();

        return result;
    }

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

    private async Task<IdentityCredential> SeedCredentialWithRole(string username, string password)
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
            UserName = username,
            PasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11)),
            IdentityInfoId = info.Id,
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityCredential>().Add(credential);

        var role = new IdentityRole
        {
            Id = Guid.NewGuid(),
            CredentialId = credential.Id,
            TypeId = TestData.RoleTypeId,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityRole>().Add(role);

        await db.SaveChangesAsync();
        return credential;
    }

    private async Task<string> SeedPendingPasswordResetVerification(Guid credentialId)
    {
        var token = "reset_" + Guid.NewGuid().ToString("N");

        await using var db = CreateDbContext();
        db.Set<IdentityVerification>().Add(new IdentityVerification
        {
            Id = Guid.NewGuid(),
            CredentialId = credentialId,
            VerificationTypeId = IdentityConstants.VerificationType.Email,
            Token = token,
            Status = (short)GenericStatusType.Pending,
            StatusUpdatedOn = DateTimeOffset.UtcNow,
            Expiry = DateTime.UtcNow.AddMinutes(10),
            TenantId = IntegrationTestFixture.TestTenantId
        });

        await db.SaveChangesAsync();
        return token;
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
}
