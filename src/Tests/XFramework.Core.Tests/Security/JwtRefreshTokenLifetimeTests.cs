using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Services;

namespace XFramework.Core.Tests.Security;

[TestFixture]
public sealed class JwtRefreshTokenLifetimeTests
{
    [Test]
    public async Task GenerateToken_ExposesConfiguredRefreshTokenExpiration()
    {
        var now = DateTimeOffset.Parse("2026-07-31T01:00:00Z");
        var clock = new MutableTimeProvider(now);
        var keyDirectory = Path.Combine(Path.GetTempPath(), "XFramework.JwtRefreshTokenLifetimeTests", Guid.NewGuid().ToString("N"));
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns("Test");
        var service = new JwtService(CreateOptions(keyDirectory), environment.Object, clock);

        try
        {
            var token = await service.GenerateToken(
                "security-test",
                Guid.NewGuid(),
                [Guid.NewGuid()],
                Guid.NewGuid());

            token.RefreshTokenExpiresAt.Should().Be(now.UtcDateTime.AddMinutes(45));
        }
        finally
        {
            Directory.Delete(keyDirectory, recursive: true);
        }
    }

    private static JwtOptions CreateOptions(string keyDirectory) => new()
    {
        ValidAudience = "identity-tests",
        ValidIssuer = "identity-tests",
        GenerationId = "identity-tests-g1",
        SigningPrivateKeyPath = Path.Combine(keyDirectory, "private.pem"),
        SigningPublicKeyPath = Path.Combine(keyDirectory, "public.pem"),
        AccessTokenLifespan = "00:15:00",
        RefreshTokenLifespan = "00:45:00"
    };

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
