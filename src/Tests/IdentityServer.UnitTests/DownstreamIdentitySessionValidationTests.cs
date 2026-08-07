using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Api.Features.Auth.ValidateSession;
using IdentityServer.Integration.Drivers;
using IdentityServer.Integration.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

[TestFixture]
[Category("Module:IdentityServer")]
[Category("Area:SessionValidation")]
public sealed class DownstreamIdentitySessionValidationTests
{
    private static readonly (string ModulePath, string ServiceName)[] InteractiveApiModules =
    [
        ("XFramework.Attendance/Attendance.Api", "attendance"),
        ("XFramework.Communications/Communications.Api", "communications"),
        ("XFramework.Community/Community.Api", "community"),
        ("XFramework.Inventario/Inventario.Api", "inventario"),
        ("XFramework.Notifications/Notifications.Api", "notifications"),
        ("XFramework.POS/POS.Api", "pos"),
        ("XFramework.Storage/Storage.Api", "storage"),
        ("XFramework.Wallets/Wallets.Api", "wallets")
    ];

    private static readonly (string ClientId, string ComposeService)[] DeployedInteractiveApiModules =
    [
        ("XFramework.Attendance", "attendance"),
        ("XFramework.Communications", "communications"),
        ("XFramework.Inventario", "inventario"),
        ("XFramework.Notifications", "notifications"),
        ("XFramework.POS", "pos"),
        ("XFramework.Storage", "storage"),
        ("XFramework.Wallets", "wallets"),
        ("XFramework.Portal", "portal")
    ];

    [Test]
    public async Task RevokedInteractiveSession_FailsDownstreamJwtValidation()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var roleTypeId = Guid.NewGuid();
        var identityServer = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        identityServer
            .Setup(wrapper => wrapper.ValidateIdentitySession(
                It.IsAny<ValidateIdentitySessionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<ValidateIdentitySessionResponse>
            {
                HttpStatusCode = HttpStatusCode.Unauthorized,
                Message = "Identity session is no longer valid"
            });

        var (options, context) = CreateValidationContext(
            identityServer.Object,
            new Claim("tenant_id", tenantId.ToString("D")),
            new Claim("credential_id", credentialId.ToString("D")),
            new Claim("session_id", sessionId.ToString("D")),
            new Claim(JwtCredentialSet.GenerationClaim, "g1"),
            new Claim(ClaimTypes.Role, JsonSerializer.Serialize(new[] { roleTypeId })));

        await options.Events.OnTokenValidated(context);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Be("Identity session is no longer valid");
        identityServer.Verify(wrapper => wrapper.ValidateIdentitySession(
            It.Is<ValidateIdentitySessionRequest>(request =>
                request.Metadata.RequestedTenantId == null
                && request.Metadata.OperationName == "Validate actor identity"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task IdentityServerTransportFailure_FailsClosedWithoutLeakingTheException()
    {
        var identityServer = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        identityServer
            .Setup(wrapper => wrapper.ValidateIdentitySession(
                It.IsAny<ValidateIdentitySessionRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transport details"));

        var (options, context) = CreateValidationContext(
            identityServer.Object,
            new Claim("tenant_id", Guid.NewGuid().ToString("D")),
            new Claim("credential_id", Guid.NewGuid().ToString("D")),
            new Claim("session_id", Guid.NewGuid().ToString("D")),
            new Claim(JwtCredentialSet.GenerationClaim, "g1"));

        await options.Events.OnTokenValidated(context);

        context.Result!.Failure!.Message.Should().Be("Actor identity validation is unavailable.");
    }

    [Test]
    public async Task ActiveInteractiveSession_PreservesDownstreamJwtValidation()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var identityServer = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        identityServer
            .Setup(wrapper => wrapper.ValidateIdentitySession(
                It.IsAny<ValidateIdentitySessionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<ValidateIdentitySessionResponse>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = new ValidateIdentitySessionResponse
                {
                    TenantId = tenantId,
                    CredentialId = credentialId,
                    IdentityId = Guid.NewGuid(),
                    SessionId = sessionId,
                    GenerationId = "g1",
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                    Attributes = new Dictionary<string, string>
                    {
                        [IdentityAuthorizationConstants.ActorAttributeIdentityVerified] = bool.TrueString
                    },
                    IsValid = true
                }
            });

        var (options, context) = CreateValidationContext(
            identityServer.Object,
            new Claim("tenant_id", tenantId.ToString("D")),
            new Claim("credential_id", credentialId.ToString("D")),
            new Claim("session_id", sessionId.ToString("D")),
            new Claim(JwtCredentialSet.GenerationClaim, "g1"));

        await options.Events.OnTokenValidated(context);

        var secondValidation = await context.HttpContext.RequestServices
            .GetRequiredService<IActorIdentityProvider>()
            .ValidateAsync("test-actor-token");

        context.Result?.Failure.Should().BeNull();
        secondValidation.IsValid.Should().BeTrue(secondValidation.Error);
        secondValidation.Identity!.Attributes.Should().Contain(
            IdentityAuthorizationConstants.ActorAttributeIdentityVerified,
            bool.TrueString);
        identityServer.Verify(
            wrapper => wrapper.ValidateIdentitySession(
                It.IsAny<ValidateIdentitySessionRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "authentication and trusted invocation authorization share one validation result per request");
    }

    [Test]
    public async Task RequestAbort_CancelsUnderlyingSessionValidationAndPropagatesCancellation()
    {
        using var requestCancellation = new CancellationTokenSource();
        CancellationToken wrapperToken = default;
        var identityServer = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        identityServer
            .Setup(wrapper => wrapper.ValidateIdentitySession(
                It.IsAny<ValidateIdentitySessionRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<ValidateIdentitySessionRequest, CancellationToken>((_, cancellationToken) =>
            {
                wrapperToken = cancellationToken;
                return WaitForCancellation(cancellationToken);
            });

        var (options, context) = CreateValidationContext(
            identityServer.Object,
            requestCancellation.Token,
            new Claim("tenant_id", Guid.NewGuid().ToString("D")),
            new Claim("credential_id", Guid.NewGuid().ToString("D")),
            new Claim("session_id", Guid.NewGuid().ToString("D")),
            new Claim(JwtCredentialSet.GenerationClaim, "g1"));

        var validation = options.Events.OnTokenValidated(context);
        requestCancellation.Cancel();
        var act = async () => await validation;

        await act.Should().ThrowAsync<OperationCanceledException>();
        wrapperToken.IsCancellationRequested.Should().BeTrue();
    }

    [Test]
    public async Task CallerDeadline_CancelsUnderlyingSessionValidationPromptly()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        CancellationToken wrapperToken = default;
        var identityServer = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        identityServer
            .Setup(wrapper => wrapper.ValidateIdentitySession(
                It.IsAny<ValidateIdentitySessionRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<ValidateIdentitySessionRequest, CancellationToken>((_, cancellationToken) =>
            {
                wrapperToken = cancellationToken;
                return WaitForCancellation(cancellationToken);
            });

        var (options, context) = CreateValidationContext(
            identityServer.Object,
            deadline.Token,
            new Claim("tenant_id", Guid.NewGuid().ToString("D")),
            new Claim("credential_id", Guid.NewGuid().ToString("D")),
            new Claim("session_id", Guid.NewGuid().ToString("D")),
            new Claim(JwtCredentialSet.GenerationClaim, "g1"));
        var stopwatch = Stopwatch.StartNew();
        var act = async () => await options.Events.OnTokenValidated(context);

        await act.Should().ThrowAsync<OperationCanceledException>();

        stopwatch.Stop();
        wrapperToken.IsCancellationRequested.Should().BeTrue();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task HmacBearerWithClientIdAndNoInteractiveClaims_FailsClosed()
    {
        var identityServer = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        var (options, context) = CreateValidationContext(
            identityServer.Object,
            new Claim("client_id", "XFramework.Communications"));

        await options.Events.OnTokenValidated(context);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Be("Identity session is no longer valid");
        identityServer.VerifyNoOtherCalls();
    }

    [Test]
    public async Task InteractiveClaimsWithoutCredentialGeneration_FailClosed()
    {
        var identityServer = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        var (options, context) = CreateValidationContext(
            identityServer.Object,
            new Claim("tenant_id", Guid.NewGuid().ToString("D")),
            new Claim("credential_id", Guid.NewGuid().ToString("D")),
            new Claim("session_id", Guid.NewGuid().ToString("D")));

        await options.Events.OnTokenValidated(context);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Be("Identity session is no longer valid");
        identityServer.VerifyNoOtherCalls();
    }

    [Test]
    public void EveryInteractiveApiModule_RegistersTheSharedSessionValidator()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var (modulePath, serviceName) in InteractiveApiModules)
        {
            var moduleRoot = Path.Combine(
                repositoryRoot.FullName,
                "src",
                "Modules",
                modulePath.Replace('/', Path.DirectorySeparatorChar));
            var installer = File.ReadAllText(Path.Combine(moduleRoot, "Installers", "WrapperInstaller.cs"));
            var project = File.ReadAllText(Directory.GetFiles(moduleRoot, "*.csproj").Single());

            installer.Should().Contain(
                "services.AddIdentityServerSessionValidation();",
                "the {0} API accepts interactive Identity JWTs",
                serviceName);
            project.Should().Contain(
                "IdentityServer.Integration\\IdentityServer.Integration.csproj",
                "the {0} API must consume the shared session-validation extension",
                serviceName);
        }
    }

    [Test]
    public void SessionValidationScope_IsDedicatedAndGrantedToEveryDeployedCaller()
    {
        XFrameworkServiceScopes.IdentitySessionValidate.Should().Be("identity.session.validate");

        var handler = typeof(ValidateIdentitySessionEndpoint)
            .GetMethod(nameof(ValidateIdentitySessionEndpoint.Handle));
        var handlerAuthorization = handler!.GetCustomAttribute<BoltHandlerAttribute>();
        handlerAuthorization.Should().NotBeNull();
        handlerAuthorization!.RequiredServiceScopes.Should()
            .Equal(XFrameworkServiceScopes.IdentitySessionValidate);

        var repositoryRoot = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "docker-compose.yml"));
        compose.Should().NotContain(
            "\n  community:",
            "Community has no central service-identity client and must not be deployed until one is configured");

        foreach (var (clientId, composeService) in DeployedInteractiveApiModules)
        {
            var clientPattern =
                $"ServiceIdentity__Clients__(?<index>\\d+)__ClientId: {Regex.Escape(clientId)}" +
                "[\\s\\S]*?ServiceIdentity__Clients__\\k<index>__AllowedScopes: ([^\\r\\n]+)";
            var clientMatch = Regex.Match(compose, clientPattern);
            clientMatch.Success.Should().BeTrue("IdentityServer must configure client {0}", clientId);
            clientMatch.Groups[1].Value.Split(',').Should().Contain(XFrameworkServiceScopes.IdentitySessionValidate);

            ExtractComposeService(compose, composeService).Should().Contain(
                $"ServiceIdentity__DefaultScopes__{(composeService == "portal" ? 2 : 1)}: " +
                XFrameworkServiceScopes.IdentitySessionValidate);
        }
    }

    private static (JwtBearerOptions Options, TokenValidatedContext Context) CreateValidationContext(
        IIdentityServerServiceWrapper identityServer,
        params Claim[] claims)
    {
        return CreateValidationContext(identityServer, CancellationToken.None, claims);
    }

    private static (JwtBearerOptions Options, TokenValidatedContext Context) CreateValidationContext(
        IIdentityServerServiceWrapper identityServer,
        CancellationToken requestAborted,
        params Claim[] claims)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddIdentityServerSessionValidation();
        services.AddIdentityServerSessionValidation();
        services.AddSingleton(identityServer);
        services.AddSingleton<IActorAccessTokenScope, TestActorAccessTokenScope>();
        services.AddSingleton<ITrustedInvocationContextStore>(new TestTrustedInvocationContextAccessor());
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            RequestAborted = requestAborted
        };
        serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        if (claims.Any(claim => claim.Type == JwtCredentialSet.GenerationClaim))
            httpContext.Request.Headers.Authorization = "Bearer test-actor-token";

        var context = new TokenValidatedContext(
            httpContext,
            new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                JwtBearerDefaults.AuthenticationScheme,
                typeof(JwtBearerHandler)),
            options)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme))
        };

        return (options, context);
    }

    private static async Task<QueryResponse<ValidateIdentitySessionResponse>> WaitForCancellation(
        CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Session validation cancellation was not observed.");
    }

    private sealed class TestActorAccessTokenScope : IActorAccessTokenScope
    {
        public IDisposable Push(string actorAccessToken) => NoopDisposable.Instance;

        private sealed class NoopDisposable : IDisposable
        {
            public static NoopDisposable Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private static string ExtractComposeService(string compose, string serviceName)
    {
        var marker = $"\n  {serviceName}:";
        var start = compose.IndexOf(marker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        var remaining = compose[(start + marker.Length)..];
        var nextService = Regex.Match(remaining, "(?m)^  [^\\s][^:\\r\\n]*:\\s*$");
        return nextService.Success
            ? compose[start..(start + marker.Length + nextService.Index)]
            : compose[start..];
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "XFramework.slnx")))
                return current;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate XFramework repository root.");
    }
}
