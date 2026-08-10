using System.Net;
using Attendance.Api.Services;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Integration.Drivers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;

namespace Attendance.Tests.Services;

[TestFixture]
public sealed class AttendanceCredentialResolverTests
{
    [Test]
    public async Task ResolveAsync_ActiveIdentityCredential_ReturnsAuthoritativeSnapshot()
    {
        var tenantId = Guid.NewGuid();
        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IsEnabled = true,
            UserAlias = "Authoritative Alias",
            UserName = "authoritative.user"
        };
        var resolver = CreateResolver(new QueryResponse<IdentityCredential>
        {
            HttpStatusCode = HttpStatusCode.OK,
            Response = credential
        });

        var result = await resolver.ResolveAsync(credential.Id, tenantId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(new AttendanceCredentialSnapshot(
            credential.Id,
            tenantId,
            true,
            false,
            credential.UserAlias,
            credential.UserName));
    }

    [Test]
    public async Task ResolveAsync_IdentityServerFailure_ReturnsServiceUnavailable()
    {
        var resolver = CreateResolver(exception: new HttpRequestException("IdentityServer unavailable"));

        var result = await resolver.ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(503);
        result.Message.Should().Contain("unavailable");
    }

    private static AttendanceCredentialResolver CreateResolver(
        QueryResponse<IdentityCredential>? response = null,
        Exception? exception = null)
    {
        var credentials = new Mock<IIdentityCredentialCrudService>();
        var setup = credentials.Setup(service => service.Get(
            It.IsAny<Guid>(),
            It.IsAny<Guid?>(),
            true,
            0,
            false,
            It.IsAny<List<string>?>()));
        if (exception is not null)
            setup.ThrowsAsync(exception);
        else
            setup.ReturnsAsync(response!);

        var identityServer = new Mock<IIdentityServerServiceWrapper>();
        identityServer.SetupGet(wrapper => wrapper.IdentityCredential).Returns(credentials.Object);
        return new AttendanceCredentialResolver(
            identityServer.Object,
            NullLogger<AttendanceCredentialResolver>.Instance);
    }
}
