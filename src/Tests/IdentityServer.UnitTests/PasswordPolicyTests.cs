using FluentAssertions;
using IdentityServer.Api.Features.Auth.ChangePassword;
using IdentityServer.Api.Features.Auth.ResetPassword;
using IdentityServer.Api.Features.Credentials.Create;
using IdentityServer.Api.Features.PortalBootstrap.EnsureAdmin;
using IdentityServer.Api.Infrastructure;
using IdentityServer.Domain.Shared.Contracts.Requests;
using NUnit.Framework;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class PasswordPolicyTests
{
    [Test]
    public void BcryptByteLimit_UsesUtf8BytesRatherThanCharacters()
    {
        var exactlySeventyTwoBytes = new string('a', 70) + "\u00E9";
        var seventyThreeBytes = exactlySeventyTwoBytes + "a";

        IdentityPasswordPolicy.IsWithinBcryptByteLimit(exactlySeventyTwoBytes).Should().BeTrue();
        IdentityPasswordPolicy.IsWithinBcryptByteLimit(seventyThreeBytes).Should().BeFalse();
    }

    [Test]
    public void CredentialCreateValidator_RejectsPasswordOverBcryptByteLimit()
    {
        var result = new CreateCredentialRequestValidator().Validate(new CreateCredentialRequest
        {
            IdentityInfoId = Guid.NewGuid(),
            UserName = "test-user",
            Password = OverLimitPassword()
        });

        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateCredentialRequest.Password));
    }

    [Test]
    public void ChangePasswordValidator_RejectsPasswordOverBcryptByteLimit()
    {
        var result = new ChangePasswordRequestValidator().Validate(new ChangePasswordRequest
        {
            CreadentialId = Guid.NewGuid(),
            VerificationId = Guid.NewGuid(),
            NewPassword = OverLimitPassword()
        });

        result.Errors.Should().Contain(error => error.PropertyName == nameof(ChangePasswordRequest.NewPassword));
    }

    [Test]
    public void ResetPasswordValidator_RejectsPasswordOverBcryptByteLimit()
    {
        var result = new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest
        {
            Token = Guid.NewGuid().ToString("N"),
            NewPassword = OverLimitPassword()
        });

        result.Errors.Should().Contain(error => error.PropertyName == nameof(ResetPasswordRequest.NewPassword));
    }

    [Test]
    public void PortalBootstrapValidator_RejectsPasswordOverBcryptByteLimit()
    {
        var result = new EnsurePortalBootstrapAdminRequestValidator().Validate(
            new EnsurePortalBootstrapAdminRequest
            {
                TenantName = "Admin",
                DisplayName = "Admin",
                UserName = "admin",
                Password = OverLimitPassword()
            });

        result.Errors.Should().Contain(error => error.PropertyName == nameof(EnsurePortalBootstrapAdminRequest.Password));
    }

    private static string OverLimitPassword() => new('a', IdentityPasswordPolicy.MaximumUtf8ByteCount + 1);
}
