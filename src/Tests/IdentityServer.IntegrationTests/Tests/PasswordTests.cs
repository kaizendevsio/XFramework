using System.Net;
using System.Net.Http.Json;
using System.Text;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
public class PasswordTests : IntegrationTestBase
{
    #region HTTP — VerifyPassword

    [Test]
    public async Task Http_VerifyPassword_WithCorrectPassword_ReturnsOk()
    {
        var password = "CorrectPassword123!";
        var credential = await SeedCredential(password: password);

        var response = await HttpClient.PostAsJsonAsync("/api/auth/verify-password",
            new VerifyPasswordRequest { CredentialId = credential.Id, Password = password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Http_VerifyPassword_WithWrongPassword_Returns400()
    {
        var credential = await SeedCredential(password: "CorrectPassword123!");

        var response = await HttpClient.PostAsJsonAsync("/api/auth/verify-password",
            new VerifyPasswordRequest { CredentialId = credential.Id, Password = "WrongPassword!" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Http_VerifyPassword_WithEmptyPassword_Returns400()
    {
        var credential = await SeedCredential();

        var response = await HttpClient.PostAsJsonAsync("/api/auth/verify-password",
            new VerifyPasswordRequest { CredentialId = credential.Id, Password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Http_VerifyPassword_WithEmptyCredentialId_Returns400()
    {
        var response = await HttpClient.PostAsJsonAsync("/api/auth/verify-password",
            new VerifyPasswordRequest { CredentialId = Guid.Empty, Password = "SomePassword!" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Http_VerifyPassword_WithNonExistentCredential_Returns404()
    {
        var response = await HttpClient.PostAsJsonAsync("/api/auth/verify-password",
            new VerifyPasswordRequest { CredentialId = Guid.NewGuid(), Password = "SomePassword!" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region HTTP — ChangePassword

    [Test]
    public async Task Http_ChangePassword_WithValidData_ChangesPassword()
    {
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";
        var credential = await SeedCredential(password: oldPassword);

        var response = await HttpClient.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest
            {
                CreadentialId = credential.Id,
                NewPassword = newPassword,
                RequireVerificationId = false
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Old password no longer works
        var verifyOld = await HttpClient.PostAsJsonAsync("/api/auth/verify-password",
            new VerifyPasswordRequest { CredentialId = credential.Id, Password = oldPassword });
        verifyOld.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // New password works
        var verifyNew = await HttpClient.PostAsJsonAsync("/api/auth/verify-password",
            new VerifyPasswordRequest { CredentialId = credential.Id, Password = newPassword });
        verifyNew.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Http_ChangePassword_WithEmptyCredentialId_Returns400()
    {
        var response = await HttpClient.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest
            {
                CreadentialId = Guid.Empty,
                NewPassword = "NewPassword!",
                RequireVerificationId = false
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Http_ChangePassword_WithEmptyNewPassword_Returns400()
    {
        var credential = await SeedCredential();

        var response = await HttpClient.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest
            {
                CreadentialId = credential.Id,
                NewPassword = "",
                RequireVerificationId = false
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Http_ChangePassword_WithNonExistentCredential_Returns404()
    {
        var response = await HttpClient.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest
            {
                CreadentialId = Guid.NewGuid(),
                NewPassword = "NewPassword123!",
                RequireVerificationId = false
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Bolt — VerifyPassword

    [Test]
    public async Task Bolt_VerifyPassword_WithCorrectPassword_ReturnsOk()
    {
        var password = "CorrectPassword123!";
        var credential = await SeedCredential(password: password);

        var result = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest { CredentialId = credential.Id, Password = password });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Bolt_VerifyPassword_WithWrongPassword_Returns400()
    {
        var credential = await SeedCredential(password: "CorrectPassword123!");

        var result = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest { CredentialId = credential.Id, Password = "WrongPassword!" });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Bolt_VerifyPassword_WithEmptyPassword_Returns400()
    {
        var credential = await SeedCredential();

        var result = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest { CredentialId = credential.Id, Password = "" });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Bolt_VerifyPassword_WithEmptyCredentialId_Returns400()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest { CredentialId = Guid.Empty, Password = "SomePassword!" });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Bolt_VerifyPassword_WithNonExistentCredential_Returns404()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest { CredentialId = Guid.NewGuid(), Password = "SomePassword!" });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Bolt — ChangePassword

    [Test]
    public async Task Bolt_ChangePassword_WithValidData_ChangesPassword()
    {
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";
        var credential = await SeedCredential(password: oldPassword);

        var result = await IntegrationTestFixture.ServiceWrapper.ChangePassword(
            new ChangePasswordRequest
            {
                CreadentialId = credential.Id,
                NewPassword = newPassword,
                RequireVerificationId = false
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        // Verify via Bolt: old password fails
        var verifyOld = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest { CredentialId = credential.Id, Password = oldPassword });
        verifyOld.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Verify via Bolt: new password works
        var verifyNew = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest { CredentialId = credential.Id, Password = newPassword });
        verifyNew.HttpStatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Bolt_ChangePassword_WithEmptyCredentialId_Returns400()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.ChangePassword(
            new ChangePasswordRequest
            {
                CreadentialId = Guid.Empty,
                NewPassword = "NewPassword!",
                RequireVerificationId = false
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
                RequireVerificationId = false
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
                RequireVerificationId = false
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Helpers

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
