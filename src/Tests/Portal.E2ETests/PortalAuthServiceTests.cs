using System.Net;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Portal.Services;

namespace Portal.E2ETests;

[TestFixture]
[Category("Module:Portal")]
[Category("Area:PortalContract")]
public sealed class PortalAuthServiceTests
{
    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.NotFound)]
    public async Task AuthenticateAsync_RejectedCredentials_DoesNotReportServiceOutage(HttpStatusCode status)
    {
        var result = await AuthenticateAsync(status);

        result.IsSuccess.Should().BeFalse();
        result.Principal.Should().BeNull();
        result.Error.Should().Be("Invalid username, password, or admin permission.");
    }

    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.GatewayTimeout)]
    public async Task AuthenticateAsync_ServiceFailure_ReportsDependencyFailure(HttpStatusCode status)
    {
        var result = await AuthenticateAsync(status);

        result.IsSuccess.Should().BeFalse();
        result.Principal.Should().BeNull();
        result.Error.Should().Be("Unable to sign in. Check IdentityServer health and try again.");
    }

    private static async Task<PortalLoginResult> AuthenticateAsync(HttpStatusCode status)
    {
        var identityServer = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        identityServer.Setup(x => x.AuthenticateIdentity(It.IsAny<AuthenticateIdentityRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<AuthenticateIdentityResponse>
            {
                HttpStatusCode = status,
                Message = "Internal details must not be displayed."
            });
        var service = new PortalAuthService(identityServer.Object, NullLogger<PortalAuthService>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";

        return await service.AuthenticateAsync("scanner-test", "unused-test-input", false, context, CancellationToken.None);
    }
}
