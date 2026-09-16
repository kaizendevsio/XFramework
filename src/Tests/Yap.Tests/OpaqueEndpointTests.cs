using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;

namespace Yap.Tests;

public sealed class OpaqueEndpointTests
{
    [Test]
    public async Task Opaque_RequiresAntiforgery_BindsWorkspace_AndDoesNotReturnBearerTokens()
    {
        Mock<IIdentityServerServiceWrapper>? identity = null;
        OpaqueAuthRequest? received = null;
        await using var app = UiFixture.Create(0, mock => {
            identity = mock;
            mock.Setup(x => x.OpaqueAuth(It.IsAny<OpaqueAuthRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((OpaqueAuthRequest request, CancellationToken _) => {
                    received = request;
                    var authentication = YapSessionsTests.Session("test-opaque-bearer-must-stay-server-side");
                    authentication.Credential!.TenantId = request.Metadata.RequestedTenantId!.Value;
                    return ChatFixture.Ok(new OpaqueAuthResponse { Mode = "opaque", Client = "test-public-client", Message = "protocol-response", Authentication = authentication });
                });
        });
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new() }) { BaseAddress = new Uri(app.Urls.Single()) };
        var body = new OpaqueAuthRequest { Stage = "login-start", UserName = "test-user", RoleId = Guid.NewGuid(), Metadata = new() { RequestedTenantId = Guid.NewGuid() } };
        Assert.That((await client.PostAsJsonAsync("/api/auth/opaque", body)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        identity!.Verify(x => x.OpaqueAuth(It.IsAny<OpaqueAuthRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        using var session = JsonDocument.Parse(await client.GetStringAsync("/api/session"));
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.RootElement.GetProperty("antiforgeryToken").GetString());
        var response = await client.PostAsJsonAsync("/api/auth/opaque", body);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(received!.RoleId, Is.EqualTo(Guid.Parse(app.Configuration["Yap:RoleId"]!)));
        Assert.That(received.Metadata.RequestedTenantId, Is.EqualTo(Guid.Parse(app.Configuration["Yap:TenantId"]!)));
        Assert.That(response.Headers.CacheControl!.NoStore, Is.True);
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain("accessToken"));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain("test-opaque-bearer-must-stay-server-side"));
        Assert.That(response.Headers.Contains("Set-Cookie"), Is.True);
    }
}
