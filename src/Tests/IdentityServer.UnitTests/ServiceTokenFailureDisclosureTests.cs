using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class ServiceTokenFailureDisclosureTests
{
    [Test]
    public async Task ValidateAsync_WhenSigningKeyProviderFails_ReturnsGenericUnavailableMessage()
    {
        var provider = new Mock<IIdentitySigningKeyProvider>(MockBehavior.Strict);
        provider.Setup(candidate => candidate.GetSigningKeysAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("C:\\sensitive\\signing-key.pem"));
        var validator = new ServiceTokenValidator(
            provider.Object,
            Options.Create(new ServiceIdentityOptions { Issuer = "identity-test" }),
            NullLogger<ServiceTokenValidator>.Instance);
        const string token =
            "eyJhbGciOiJSUzI1NiIsImtpZCI6InRlc3QifQ.eyJzdWIiOiJwb3J0YWwifQ.signature";

        var result = await validator.ValidateAsync(token, "identity-test");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Service signing keys are unavailable.");
        result.Error.Should().NotContain("signing-key.pem");
    }
}
