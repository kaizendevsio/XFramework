using FluentAssertions;
using IdentityServer.Api.Features.Auth.ValidateSession;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class SessionValidationHttpTests
{
    [Test]
    public async Task HandleHttp_ReturnsAuthenticatedActorSnapshotWithoutReadingIdentityFromBody()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var roleTypeId = Guid.NewGuid();
        var identityId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var request = new ValidateIdentitySessionRequest();
        var context = new TestTrustedInvocationContextAccessor(new TrustedInvocationContext(
            new TrustedActorIdentity(
                credentialId,
                identityId,
                tenantId,
                sessionId,
                new HashSet<string> { roleTypeId.ToString("D") },
                new HashSet<string> { "identity.users.view" },
                "g1",
                expiresAt),
            Service: null,
            EffectiveTenantId: tenantId,
            RequestedTargetTenantId: null,
            CorrelationId: Guid.NewGuid()));

        var result = await ValidateIdentitySessionEndpoint.HandleHttp(
            request,
            context,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.TenantId.Should().Be(tenantId);
        result.Data.CredentialId.Should().Be(credentialId);
        result.Data.IdentityId.Should().Be(identityId);
        result.Data.SessionId.Should().Be(sessionId);
        result.Data.Roles.Should().Equal(roleTypeId.ToString("D"));
        result.Data.Capabilities.Should().Equal("identity.users.view");
        result.Data.GenerationId.Should().Be("g1");
        result.Data.ExpiresAtUtc.Should().Be(expiresAt.UtcDateTime);
        result.Data.IsValid.Should().BeTrue();
    }

    [Test]
    public async Task HandleHttp_WithoutTrustedActor_ReturnsUnauthorized()
    {
        var request = new ValidateIdentitySessionRequest();
        var context = new TestTrustedInvocationContextAccessor();

        var result = await ValidateIdentitySessionEndpoint.HandleHttp(
            request,
            context,
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}
