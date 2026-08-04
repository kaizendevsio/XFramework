using FluentAssertions;
using IdentityServer.Api.Features.Auth.Authenticate;
using IdentityServer.Api.Features.Auth.ForgotPassword;
using IdentityServer.Api.Features.Auth.Refresh;
using IdentityServer.Api.Features.Auth.ResetPassword;
using IdentityServer.Api.Features.Auth.ValidateSession;
using IdentityServer.Api.Features.Authorization.CheckCredentialCapability;
using IdentityServer.Api.Features.Credentials.Create;
using IdentityServer.Api.Features.ServiceIdentity.GetSigningKeys;
using IdentityServer.Api.Features.ServiceIdentity.IssueBoltTransportToken;
using IdentityServer.Api.Features.ServiceIdentity.IssueToken;
using IdentityServer.Api.Features.ServiceIdentity.RetireSigningKey;
using IdentityServer.Api.Features.ServiceIdentity.RotateSigningKey;
using IdentityServer.Api.Features.Verification.Create;
using IdentityServer.Api.Features.Verification.Confirm;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using NUnit.Framework;
using XFramework.Domain.Shared.Enums;
using XFramework.Domain.Shared.ServiceIdentity;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class PublicRequestValidationTests
{
    [Test]
    public void ServiceTokenValidator_RejectsNullOrOversizedInputs()
    {
        var nullScopes = new IssueServiceTokenRequest
        {
            ClientId = "client",
            ClientSecret = "secret",
            Audience = "audience",
            Scopes = null!
        };
        var oversized = new IssueServiceTokenRequest
        {
            ClientId = new string('c', 201),
            ClientSecret = new string('s', 1_025),
            Audience = new string('a', 257),
            Scopes = Enumerable.Repeat("scope", 65).ToList()
        };

        new IssueServiceTokenRequestValidator().Validate(nullScopes).IsValid.Should().BeFalse();
        new IssueServiceTokenRequestValidator().Validate(oversized).Errors
            .Select(error => error.PropertyName)
            .Should().Contain(
                nameof(IssueServiceTokenRequest.ClientId),
                nameof(IssueServiceTokenRequest.ClientSecret),
                nameof(IssueServiceTokenRequest.Audience),
                nameof(IssueServiceTokenRequest.Scopes));
    }

    [Test]
    public void BoltTransportTokenValidator_RejectsOversizedCredentials()
    {
        var result = new IssueBoltTransportTokenRequestValidator().Validate(
            new IssueBoltTransportTokenRequest
            {
                ClientId = new string('c', 201),
                ClientSecret = new string('s', 1_025)
            });

        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(IssueBoltTransportTokenRequest.ClientId),
            nameof(IssueBoltTransportTokenRequest.ClientSecret));
    }

    [Test]
    public void PublicAuthValidators_RejectOversizedUserAndTokenInputs()
    {
        new AuthenticateIdentityRequestValidator().Validate(new AuthenticateIdentityRequest
        {
            RoleId = Guid.NewGuid(),
            AuthorizationType = AuthorizationType.Username,
            UserName = new string('u', 321),
            Password = "ValidPassword123!"
        }).Errors.Should().Contain(error => error.PropertyName == nameof(AuthenticateIdentityRequest.UserName));

        new ForgotPasswordRequestValidator().Validate(new ForgotPasswordRequest
        {
            Email = $"{new string('e', 310)}@example.com"
        }).Errors.Should().Contain(error => error.PropertyName == nameof(ForgotPasswordRequest.Email));

        new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest
        {
            Token = new string('t', 2_049),
            NewPassword = "ValidPassword123!"
        }).Errors.Should().Contain(error => error.PropertyName == nameof(ResetPasswordRequest.Token));

        new RefreshTokenRequestValidator().Validate(new RefreshTokenRequest
        {
            AccessToken = new string('a', 16_385),
            RefreshToken = new string('r', 2_049),
            SessionId = Guid.NewGuid()
        }).Errors.Select(error => error.PropertyName).Should().Contain(
            nameof(RefreshTokenRequest.AccessToken),
            nameof(RefreshTokenRequest.RefreshToken));
    }

    [Test]
    public void CredentialAndVerificationValidators_RejectOversizedInputs()
    {
        new CreateCredentialRequestValidator().Validate(new CreateCredentialRequest
        {
            IdentityInfoId = Guid.NewGuid(),
            UserName = new string('u', 257),
            UserAlias = new string('a', 257),
            Password = "ValidPassword123!"
        }).Errors.Select(error => error.PropertyName).Should().Contain(
            nameof(CreateCredentialRequest.UserName),
            nameof(CreateCredentialRequest.UserAlias));

        new ConfirmVerificationRequestValidator().Validate(
            new ConfirmVerificationRequest(new string('t', 2_049), Guid.NewGuid()))
            .Errors.Should().Contain(error => error.PropertyName == nameof(ConfirmVerificationRequest.Token));
        new ConfirmVerificationRequestValidator().Validate(
            new ConfirmVerificationRequest("valid-token", Guid.Empty))
            .Errors.Should().Contain(error => error.PropertyName == nameof(ConfirmVerificationRequest.TenantId));
    }

    [Test]
    public void VerificationValidator_RejectsNullModel_AndSessionValidationRequestHasNoIdentityBody()
    {
        var createVerification = new XFramework.Domain.Shared.Contracts.Requests.Create<IdentityVerification>(null!);

        var verificationResult = new CreateVerificationRequestValidator().Validate(createVerification);

        verificationResult.IsValid.Should().BeFalse();
        verificationResult.Errors.Should().Contain(error => error.PropertyName == "Model");
        typeof(ValidateIdentitySessionRequest).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(["TenantId", "CredentialId", "SessionId", "RoleTypeIds"]);
    }

    [Test]
    public void CapabilityAndSigningKeyValidators_RejectOversizedInputs()
    {
        var capability = new CheckCredentialCapabilityRequest
        {
            CredentialId = Guid.NewGuid(),
            ModuleKey = new string('m', 101),
            SubFeatureKey = new string('s', 101),
            CapabilityKey = "view"
        };

        new CheckCredentialCapabilityRequestValidator().Validate(capability).Errors
            .Select(error => error.PropertyName)
            .Should().Contain(
                nameof(CheckCredentialCapabilityRequest.ModuleKey),
                nameof(CheckCredentialCapabilityRequest.SubFeatureKey));
        new GetServiceSigningKeysRequestValidator().Validate(new GetServiceSigningKeysRequest
        {
            KeyId = new string('k', 129)
        }).IsValid.Should().BeFalse();
        new RotateServiceSigningKeyRequestValidator().Validate(new RotateServiceSigningKeyRequest
        {
            Reason = new string('r', 257)
        }).IsValid.Should().BeFalse();
        new RetireServiceSigningKeyRequestValidator().Validate(new RetireServiceSigningKeyRequest
        {
            KeyId = new string('k', 129)
        }).IsValid.Should().BeFalse();
    }
}
