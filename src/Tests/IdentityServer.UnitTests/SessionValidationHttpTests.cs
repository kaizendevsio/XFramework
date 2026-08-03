using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using IdentityServer.Api.Features.Auth.ValidateSession;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Http;
using Moq;
using NUnit.Framework;
using XFramework.Core.Patterns;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class SessionValidationHttpTests
{
    [Test]
    public async Task HandleHttp_OverwritesClientIdentifiersWithAuthenticatedClaims()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var roleTypeId = Guid.NewGuid();
        var request = new ValidateIdentitySessionRequest
        {
            TenantId = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            RoleTypeIds = [Guid.NewGuid()]
        };
        var context = CreateContext(tenantId, credentialId, sessionId, [roleTypeId]);
        var service = new Mock<IAuthService>(MockBehavior.Strict);
        service.Setup(candidate => candidate.ValidateIdentitySessionAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ValidateIdentitySessionResponse>.Success(new ValidateIdentitySessionResponse()));

        var result = await ValidateIdentitySessionEndpoint.HandleHttp(
            request,
            context,
            service.Object,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.TenantId.Should().Be(tenantId);
        request.CredentialId.Should().Be(credentialId);
        request.SessionId.Should().Be(sessionId);
        request.RoleTypeIds.Should().Equal(roleTypeId);
        service.VerifyAll();
    }

    [Test]
    public async Task HandleHttp_WithMissingSessionClaims_ReturnsForbiddenWithoutCallingService()
    {
        var request = new ValidateIdentitySessionRequest();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], "test"))
        };
        var service = new Mock<IAuthService>(MockBehavior.Strict);

        var result = await ValidateIdentitySessionEndpoint.HandleHttp(
            request,
            context,
            service.Object,
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        service.VerifyNoOtherCalls();
    }

    private static DefaultHttpContext CreateContext(
        Guid tenantId,
        Guid credentialId,
        Guid sessionId,
        IReadOnlyCollection<Guid> roleTypeIds)
    {
        var claims = new[]
        {
            new Claim("tenant_id", tenantId.ToString("D")),
            new Claim("credential_id", credentialId.ToString("D")),
            new Claim("session_id", sessionId.ToString("D")),
            new Claim(ClaimTypes.Role, JsonSerializer.Serialize(roleTypeIds))
        };

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
    }
}
