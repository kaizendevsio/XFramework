using System.Net;
using System.Text.RegularExpressions;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;

namespace Yap.Tests;

[TestFixture]
public sealed class RegistrationEndpointTests
{
    [Test]
    public async Task Register_ValidForm_UsesConfiguredWorkspaceAndRequiresAntiforgery()
    {
        Mock<IIdentityServerServiceWrapper>? identity = null;
        await using var app = UiFixture.Create(0, mock => identity = mock);
        await app.StartAsync();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
        Assert.That(await client.GetStringAsync("/login"), Does.Contain("/register"));
        var page = await client.GetStringAsync("/register");
        Assert.That(page, Does.Contain("Confirm password"));
        var rejected = await client.PostAsync("/auth/register", Form(null));
        Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        identity!.Verify(x => x.RegisterIdentity(It.IsAny<RegisterIdentityRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        var response = await client.PostAsync("/auth/register", Form(Token(page)));
        Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo("/login?registered=true"));
        var tenant = Guid.Parse(app.Configuration["Yap:TenantId"]!);
        identity.Verify(x => x.RegisterIdentity(It.Is<RegisterIdentityRequest>(r =>
            r.UserName == "new.member" && r.DisplayName == "New Member" && r.Metadata.RequestedTenantId == tenant),
            It.IsAny<CancellationToken>()), Times.Once);
        // Registration does not create an unverified authentication session.
        Assert.That((await client.GetAsync("/settings")).StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        await app.StopAsync();
    }

    [TestCase("confirmPassword", "DifferentPassword123!", "mismatch")]
    [TestCase("password", "short", "validation")]
    [TestCase("username", "bad username", "validation")]
    public async Task Register_InvalidForm_DoesNotCallIdentity(string field, string value, string error)
    {
        Mock<IIdentityServerServiceWrapper>? identity = null;
        await using var app = UiFixture.Create(0, mock => identity = mock);
        await app.StartAsync();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
        var page = await client.GetStringAsync("/register");
        var response = await client.PostAsync("/auth/register", Form(Token(page), field, value));
        Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo($"/register?error={error}"));
        identity!.Verify(x => x.RegisterIdentity(It.IsAny<RegisterIdentityRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        await app.StopAsync();
    }

    [TestCase(HttpStatusCode.Conflict, "taken")]
    [TestCase(HttpStatusCode.Forbidden, "disabled")]
    [TestCase(HttpStatusCode.TooManyRequests, "limited")]
    [TestCase(HttpStatusCode.ServiceUnavailable, "unavailable")]
    public async Task Register_ApiFailure_ShowsActionableError(HttpStatusCode status, string error)
    {
        await using var app = UiFixture.Create(0, mock => mock
            .Setup(x => x.RegisterIdentity(It.IsAny<RegisterIdentityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XFramework.Domain.Shared.BusinessObjects.CmdResponse<IdentityServer.Domain.Shared.Contracts.Responses.RegisterIdentityResponse>
                { HttpStatusCode = status }));
        await app.StartAsync();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
        var page = await client.GetStringAsync("/register");
        var response = await client.PostAsync("/auth/register", Form(Token(page)));
        Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo($"/register?error={error}"));
        Assert.That(await client.GetStringAsync(response.Headers.Location), Does.Contain("role=\"alert\""));
        await app.StopAsync();
    }

    private static FormUrlEncodedContent Form(string? token, string? field = null, string? value = null)
    {
        var values = new Dictionary<string, string>
        {
            ["displayName"] = "New Member", ["username"] = "new.member", ["password"] = "Registration123!",
            ["confirmPassword"] = "Registration123!", ["tenantId"] = Guid.NewGuid().ToString(), ["roleId"] = Guid.NewGuid().ToString()
        };
        if (token is not null) values["__RequestVerificationToken"] = token;
        if (field is not null) values[field] = value!;
        return new(values);
    }
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value);
}
