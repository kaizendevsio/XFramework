using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using IdentityServer.Api.Features.Auth.Authenticate;
using IdentityServer.Api.Features.ServiceIdentity.IssueBoltTransportToken;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Integration.Security;
using XFramework.Domain.Shared.Enums;
using XFramework.Domain.Shared.ServiceIdentity;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class GeneratedTokenHttpAdapterTests
{
    [Test]
    public async Task AuthenticateSuccess_ReturnsBareAuthenticationDto()
    {
        var expected = new AuthenticateIdentityResponse
        {
            AccessToken = "user-access-token",
            TokenType = "Bearer",
            ExpiresIn = 1_800,
            RefreshToken = "user-refresh-token",
            SessionId = Guid.NewGuid()
        };
        var request = new AuthenticateIdentityRequest
        {
            RoleId = Guid.NewGuid(),
            AuthorizationType = AuthorizationType.Username,
            UserName = "test-user",
            Password = "test-password"
        };
        var authService = new Mock<IAuthService>(MockBehavior.Strict);
        authService.Setup(service => service.AuthenticateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthenticateIdentityResponse>.Success(expected));
        var invocationAuthorizer = new Mock<IHttpTrustedInvocationAuthorizer>(MockBehavior.Strict);
        invocationAuthorizer.Setup(authorizer => authorizer.AuthorizeAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                request.Metadata,
                It.IsAny<InvocationAuthorizationPolicy>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TrustedInvocationResult.Success(new TrustedInvocationContext(
                Actor: null,
                Service: null,
                EffectiveTenantId: Guid.NewGuid(),
                RequestedTargetTenantId: null,
                CorrelationId: Guid.NewGuid())));
        var actorAccessTokenScope = new Mock<IActorAccessTokenScope>(MockBehavior.Strict);
        var featureGate = new Mock<ITrustedInvocationFeatureGate>(MockBehavior.Strict);
        featureGate.Setup(gate => gate.EnsureAllowedAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await InvokeGeneratedAdapter(
            typeof(AuthenticateEndpoint).Assembly.GetType(
                "IdentityServer.Api.Features.Auth.Authenticate.Generated.AuthenticateEndpoint_RestEndpoint",
                throwOnError: true)!,
            request,
            authService.Object,
            new AuthenticateIdentityRequestValidator(),
            CancellationToken.None,
            invocationAuthorizer.Object,
            actorAccessTokenScope.Object,
            featureGate.Object,
            new DefaultHttpContext());

        response.Should().BeOfType<Ok<AuthenticateIdentityResponse>>()
            .Which.Value.Should().BeSameAs(expected);
        authService.VerifyAll();
        invocationAuthorizer.VerifyAll();
        featureGate.VerifyAll();
    }

    [Test]
    public async Task BoltTransportTokenSuccess_ReturnsBareServiceTokenDto()
    {
        var expected = new ServiceTokenResponse
        {
            AccessToken = "service-access-token",
            TokenType = "Bearer",
            ExpiresAtUtc = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc)
        };
        var request = new IssueBoltTransportTokenRequest
        {
            ClientId = "test-client",
            ClientSecret = "test-secret"
        };
        var service = new Mock<IServiceIdentityService>(MockBehavior.Strict);
        service.Setup(candidate => candidate.IssueBoltTransportTokenAsync(
                request.ClientId,
                request.ClientSecret,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ServiceTokenResponse>.Success(expected));
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        var invocationAuthorizer = new Mock<IHttpTrustedInvocationAuthorizer>(MockBehavior.Strict);
        invocationAuthorizer.Setup(authorizer => authorizer.AuthorizeAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<XFramework.Domain.Shared.BusinessObjects.RequestMetadata>(),
                It.Is<InvocationAuthorizationPolicy>(policy =>
                    policy.AllowAnonymous &&
                    policy.ActorRequirement == ActorRequirement.None &&
                    policy.TenantAccessMode == TenantAccessMode.Tenantless),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TrustedInvocationResult.Success(new TrustedInvocationContext(
                Actor: null,
                Service: null,
                EffectiveTenantId: null,
                RequestedTargetTenantId: null,
                CorrelationId: Guid.NewGuid())));
        var actorAccessTokenScope = new Mock<IActorAccessTokenScope>(MockBehavior.Strict);
        var featureGate = new Mock<ITrustedInvocationFeatureGate>(MockBehavior.Strict);
        featureGate.Setup(gate => gate.EnsureAllowedAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await InvokeGeneratedAdapter(
            typeof(IssueBoltTransportTokenEndpoint).Assembly.GetType(
                "IdentityServer.Api.Features.ServiceIdentity.IssueBoltTransportToken.Generated.IssueBoltTransportTokenEndpoint_RestEndpoint",
                throwOnError: true)!,
            request,
            context.Request,
            CreateServiceIdentityConfiguration(),
            service.Object,
            new IssueBoltTransportTokenRequestValidator(),
            CancellationToken.None,
            invocationAuthorizer.Object,
            actorAccessTokenScope.Object,
            featureGate.Object,
            context);

        response.Should().BeOfType<Ok<ServiceTokenResponse>>()
            .Which.Value.Should().BeSameAs(expected);

        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        using var requestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        context.RequestServices = requestServices;

        await response.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().StartWith("application/json");
        responseBody.Position = 0;

        using var document = await JsonDocument.ParseAsync(responseBody);
        var root = document.RootElement;
        root.EnumerateObject().Select(static property => property.Name).Should().BeEquivalentTo(
            "accessToken",
            "tokenType",
            "expiresAtUtc");
        root.GetProperty("accessToken").GetString().Should().Be(expected.AccessToken);
        root.GetProperty("tokenType").GetString().Should().Be(expected.TokenType);
        root.GetProperty("expiresAtUtc").GetDateTime().Should().Be(expected.ExpiresAtUtc);
        service.VerifyAll();
        invocationAuthorizer.VerifyAll();
        featureGate.VerifyAll();
    }

    private static async Task<IResult> InvokeGeneratedAdapter(Type adapterType, params object[] arguments)
    {
        var method = adapterType.GetMethod("RestHandle", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var invocation = method!.Invoke(null, arguments);
        invocation.Should().BeAssignableTo<Task>();
        var task = (Task)invocation!;
        await task;

        var generatedUnion = task.GetType().GetProperty("Result")!.GetValue(task);
        generatedUnion.Should().BeAssignableTo<IResult>();

        var result = generatedUnion!.GetType().GetProperty("Result")!.GetValue(generatedUnion);
        return result.Should().BeAssignableTo<IResult>().Subject;
    }

    private static ServiceIdentityConfiguration CreateServiceIdentityConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceIdentity:Clients:0:ClientId"] = "test-client",
                ["ServiceIdentity:Clients:0:GenerationId"] = "test-g1",
                ["ServiceIdentity:Clients:0:ClientSecret"] =
                    "test-service-credential-material-111111111111111111111111",
                ["ServiceIdentity:Clients:0:AllowedAudiences:0"] = XFrameworkServiceNames.IdentityServer,
                ["ServiceIdentity:Clients:0:AllowedScopes:0"] = XFrameworkServiceScopes.BoltService
            })
            .Build();

        return ServiceIdentityConfiguration.FromConfiguration(configuration, DateTimeOffset.UtcNow);
    }
}
