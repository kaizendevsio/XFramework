using Audit.Api.Services;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Text;
using XFramework.Core.Services.FeatureGates;
using XFramework.Integration.Security;

namespace Audit.IntegrationTests;

[TestFixture]
public sealed class AuditRouteTests
{
    [Test]
    public async Task GeneratedRoutes_AnonymousRequests_AreUnauthorizedRatherThanMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthentication("test").AddJwtBearer("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddValidatorsFromAssemblyContaining<AuditQueryService>();
        builder.Services.AddScoped<AuditQueryService>(_ => throw new InvalidOperationException("Anonymous requests must not query audit data."));
        builder.Services.AddSingleton(Mock.Of<IHttpTrustedInvocationAuthorizer>());
        builder.Services.AddSingleton(Mock.Of<IActorAccessTokenScope>());
        builder.Services.AddSingleton(Mock.Of<ITrustedInvocationFeatureGate>());
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        app.UseAuthorization();
        Audit.Api.Generated.GeneratedEndpointRoutes.MapGeneratedEndpoints(app);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        foreach (var route in new[] { "/api/audit/events/query", "/api/audit/events/detail" })
        {
            using var response = await client.PostAsync(route, new StringContent("{}", Encoding.UTF8, "application/json"));
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, route);
        }
        await app.StopAsync();
    }
}
