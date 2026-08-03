using System.Security.Claims;
using FluentAssertions;
using IdentityServer.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

[TestFixture]
[Category("Module:IdentityServer")]
[Category("Area:SessionValidation")]
public sealed class IdentitySessionJwtValidationTests
{
    [Test]
    public async Task HmacBearerWithClientIdAndNoInteractiveClaims_FailsClosed()
    {
        var (options, context) = CreateValidationContext(
            new Claim("client_id", "XFramework.Communications"));

        await options.Events.OnTokenValidated(context);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Be("Identity session is no longer valid");
    }

    [Test]
    public async Task InteractiveClaimsWithoutCredentialGeneration_FailClosed()
    {
        var (options, context) = CreateValidationContext(
            new Claim("tenant_id", Guid.NewGuid().ToString("D")),
            new Claim("credential_id", Guid.NewGuid().ToString("D")),
            new Claim("session_id", Guid.NewGuid().ToString("D")));

        await options.Events.OnTokenValidated(context);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Be("Identity session is no longer valid");
    }

    [Test]
    public async Task EmptyCredentialGeneration_FailsClosed()
    {
        var (options, context) = CreateValidationContext(
            new Claim("tenant_id", Guid.NewGuid().ToString("D")),
            new Claim("credential_id", Guid.NewGuid().ToString("D")),
            new Claim("session_id", Guid.NewGuid().ToString("D")),
            new Claim(JwtCredentialSet.GenerationClaim, " "));

        await options.Events.OnTokenValidated(context);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Be("Identity session is no longer valid");
    }

    private static (JwtBearerOptions Options, TokenValidatedContext Context) CreateValidationContext(
        params Claim[] claims)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddIdentitySessionJwtValidation();
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
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
}
