using System.Net;
using System.Security.Claims;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Portal.Services;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Module:Portal")]
public sealed class PortalLoginSitesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RoleId = Guid.NewGuid();

    [Test]
    public void Resolve_Unconfigured_PreservesPlatformLoginAndXSystemsBrand()
    {
        var site = Sites().Resolve("xeon-dev")!;
        site.BrandName.Should().Be("XSystems");
        site.IsTenantLogin.Should().BeFalse();
        site.AuthenticationRoleId.Should().Be(PortalAccess.AdminRoleTypeId);
        site.LogoUrl.Should().BeNull();
    }

    [Test]
    public void Resolve_ConfiguredHosts_MatchesExactlyAndRejectsUnknownHosts()
    {
        var sites = TenantSites();
        sites.Resolve("SHOP.EXAMPLE.COM.")!.TenantId.Should().Be(TenantId);
        sites.Resolve("shop.example.com")!.BrandName.Should().Be("Example Shop");
        sites.Resolve("admin.example.com")!.IsTenantLogin.Should().BeFalse();
        sites.Resolve("shop.example.com.attacker.test").Should().BeNull();
        sites.Resolve("unknown.example.com").Should().BeNull();
    }

    [Test]
    public void Resolve_DefaultTenant_AppliesToDedicatedDeployment()
    {
        var sites = Sites(new()
        {
            ["Portal:Login:Default:TenantId"] = TenantId.ToString(),
            ["Portal:Login:Default:RoleTypeId"] = RoleId.ToString()
        });
        sites.Resolve("dedicated.example.com")!.TenantId.Should().Be(TenantId);
    }

    [TestCase("TenantId", "00000000-0000-0000-0000-000000000000")]
    [TestCase("RoleTypeId", "14524d87-582d-4af6-8d6c-4f58ffad34f5")]
    [TestCase("LogoUrl", "javascript:alert(1)")]
    [TestCase("LogoUrl", "//external.example/logo.png")]
    [TestCase("LogoUrl", "/\\external.example/logo.png")]
    [TestCase("LogoUrl", "http://external.example/logo.png")]
    [TestCase("Host", "https://shop.example.com")]
    public void Configuration_InvalidSite_FailsClosed(string key, string value)
    {
        var config = TenantConfiguration();
        config[$"Portal:Login:Hosts:0:{key}"] = value;
        var construct = () => Sites(config);
        construct.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Configuration_PartialTenantOrDuplicateHosts_FailsClosed()
    {
        var partial = TenantConfiguration();
        partial.Remove("Portal:Login:Hosts:0:RoleTypeId");
        var construct = () => Sites(partial);
        construct.Should().Throw<InvalidOperationException>();
        var duplicate = TenantConfiguration();
        duplicate["Portal:Login:Hosts:1:Host"] = "SHOP.EXAMPLE.COM";
        construct = () => Sites(duplicate);
        construct.Should().Throw<InvalidOperationException>();
    }

    [TestCase("/branding/logo.png")]
    [TestCase("https://assets.example.com/logo.png")]
    public void Resolve_ConfiguredLogo_PreservesSafeUrl(string url)
    {
        var config = TenantConfiguration();
        config["Portal:Login:Hosts:0:LogoUrl"] = url;
        Sites(config).Resolve("shop.example.com")!.LogoUrl.Should().Be(url);
    }

    [Test]
    public void AcceptsPrincipal_RejectsDifferentTenantRoleAndUnknownHost()
    {
        var sites = TenantSites();
        sites.AcceptsPrincipal("shop.example.com", Principal(TenantId, RoleId)).Should().BeTrue();
        sites.AcceptsPrincipal("shop.example.com", Principal(Guid.NewGuid(), RoleId)).Should().BeFalse();
        sites.AcceptsPrincipal("shop.example.com", Principal(TenantId, Guid.NewGuid())).Should().BeFalse();
        sites.AcceptsPrincipal("admin.example.com", Principal(TenantId, RoleId)).Should().BeFalse();
        sites.AcceptsPrincipal("unknown.example.com", Principal(TenantId, RoleId)).Should().BeFalse();
        sites.AcceptsPrincipal("shop.example.com", null).Should().BeFalse();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Authenticate_ConfiguredTenant_UsesServerScopeAndRejectsMismatchedResponse(bool mismatched)
    {
        var wrapper = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        AuthenticateIdentityRequest? captured = null;
        wrapper.Setup(x => x.AuthenticateIdentity(It.IsAny<AuthenticateIdentityRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AuthenticateIdentityRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new QueryResponse<AuthenticateIdentityResponse>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = new()
                {
                    Identity = new() { Id = Guid.NewGuid(), TenantId = TenantId },
                    Credential = new() { Id = Guid.NewGuid(), TenantId = mismatched ? Guid.NewGuid() : TenantId },
                    AccessToken = "test-access", RefreshToken = "test-refresh", SessionId = Guid.NewGuid()
                }
            });
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("shop.example.com", 5298);
        context.Request.QueryString = new QueryString("?tenantId=attacker&roleId=admin");
        var service = new PortalAuthService(wrapper.Object, NullLogger<PortalAuthService>.Instance, TenantSites());

        var result = await service.AuthenticateAsync(" cashier ", "test-input", false, context, CancellationToken.None);

        captured!.RoleId.Should().Be(RoleId);
        captured.Metadata!.RequestedTenantId.Should().Be(TenantId);
        captured.UserName.Should().Be("cashier");
        result.IsSuccess.Should().Be(!mismatched);
        if (!mismatched)
        {
            result.Principal!.FindFirst(PortalAuthClaims.TenantId)!.Value.Should().Be(TenantId.ToString());
            result.Principal.FindFirst(PortalAuthClaims.IsSuperUser)!.Value.Should().Be("False");
        }
        else
            result.Principal.Should().BeNull();
    }

    [Test]
    public async Task Authenticate_UnknownHost_DoesNotCallIdentityServer()
    {
        var wrapper = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        var service = new PortalAuthService(wrapper.Object, NullLogger<PortalAuthService>.Instance, TenantSites());
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("unknown.example.com");
        var result = await service.AuthenticateAsync("admin", "test-input", false, context, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        wrapper.VerifyNoOtherCalls();
    }

    private static PortalLoginSites TenantSites() => Sites(TenantConfiguration());
    private static Dictionary<string, string?> TenantConfiguration() => new()
    {
        ["Portal:Login:Hosts:0:Host"] = "shop.example.com",
        ["Portal:Login:Hosts:0:TenantId"] = TenantId.ToString(),
        ["Portal:Login:Hosts:0:RoleTypeId"] = RoleId.ToString(),
        ["Portal:Login:Hosts:0:DisplayName"] = "Example Shop",
        ["Portal:Login:Hosts:1:Host"] = "admin.example.com"
    };
    private static PortalLoginSites Sites(Dictionary<string, string?>? config = null) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(config ?? []).Build());
    private static ClaimsPrincipal Principal(Guid tenant, Guid role) => new(new ClaimsIdentity(new[]
    {
        new Claim(PortalAuthClaims.TenantId, tenant.ToString()),
        new Claim(PortalAuthClaims.RoleTypeId, role.ToString())
    }, PortalAuthDefaults.AuthenticationScheme));
}
