using System.Security.Cryptography;
using Bolt.Hub.Configurations;
using Bolt.Hub.Health;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltTransportIdentityHealthCheckTests
{
    [Test]
    public async Task CheckHealthAsync_ExactIssuerAndUsableRsaKey_ReturnsHealthy()
    {
        var configuration = CreateConfiguration(
            BoltTransportAuthentication.ExpectedIssuer,
            CreateRsaSecurityKey(2048));
        var check = CreateHealthCheck(
            new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Bolt transport identity metadata is available.");
    }

    [Test]
    public async Task CheckHealthAsync_WrongIssuer_ReturnsUnhealthy()
    {
        var configuration = CreateConfiguration(
            "XFramework.WrongIssuer",
            CreateRsaSecurityKey(2048));
        var check = CreateHealthCheck(
            new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Bolt transport metadata issuer is invalid.");
    }

    [Test]
    public async Task CheckHealthAsync_NoUsableRsaSigningKeys_ReturnsUnhealthy()
    {
        var configuration = CreateConfiguration(
            BoltTransportAuthentication.ExpectedIssuer,
            CreateRsaSecurityKey(1024),
            new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)));
        var check = CreateHealthCheck(
            new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Bolt transport metadata contains no usable RSA signing key.");
    }

    [Test]
    public async Task CheckHealthAsync_MetadataOrJwksFetchFails_ReturnsUnhealthy()
    {
        var exception = new InvalidOperationException("Metadata/JWKS fetch failed.");
        var configurationManager = Substitute.For<IConfigurationManager<OpenIdConnectConfiguration>>();
        configurationManager.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<OpenIdConnectConfiguration>(exception));
        var check = CreateHealthCheck(configurationManager);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Bolt transport identity metadata could not be resolved.");
        result.Exception.Should().BeSameAs(exception);
    }

    private static BoltTransportIdentityHealthCheck CreateHealthCheck(
        IConfigurationManager<OpenIdConnectConfiguration> configurationManager)
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<JwtBearerOptions>>();
        optionsMonitor.Get(BoltTransportAuthentication.Scheme).Returns(new JwtBearerOptions
        {
            ConfigurationManager = configurationManager
        });

        return new BoltTransportIdentityHealthCheck(optionsMonitor);
    }

    private static OpenIdConnectConfiguration CreateConfiguration(
        string issuer,
        params SecurityKey[] signingKeys)
    {
        var configuration = new OpenIdConnectConfiguration { Issuer = issuer };
        foreach (var signingKey in signingKeys)
            configuration.SigningKeys.Add(signingKey);

        return configuration;
    }

    private static RsaSecurityKey CreateRsaSecurityKey(int keySize)
    {
        using var rsa = RSA.Create(keySize);
        return new RsaSecurityKey(rsa.ExportParameters(false))
        {
            KeyId = $"test-rsa-{keySize}"
        };
    }
}
