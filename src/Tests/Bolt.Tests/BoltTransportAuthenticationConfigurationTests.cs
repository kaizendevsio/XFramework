using Bolt.Hub.Configurations;
using Bolt.Hub.Installers;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using XFramework.Core.Extensions;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltTransportAuthenticationConfigurationTests
{
    private const string MetadataAddress = "https://identity.test/.well-known/openid-configuration";

    [Test]
    public void InstallServices_MissingMetadataAddress_FailsClosed()
    {
        var installer = new BoltTransportAuthenticationInstaller();
        var services = new ServiceCollection();

        var act = () => installer.InstallServices<BoltTransportAuthenticationConfigurationTests>(
            services,
            BuildConfiguration([]),
            new TestHostEnvironment());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*BoltTransportAuthentication:MetadataAddress*required*");
    }

    [TestCase("metadata", true)]
    [TestCase("ftp://identity.test/metadata", true)]
    [TestCase("http://identity.test/metadata", true)]
    public void InstallServices_InvalidMetadataAddress_FailsClosed(
        string metadataAddress,
        bool requireHttpsMetadata)
    {
        var installer = new BoltTransportAuthenticationInstaller();
        var services = new ServiceCollection();
        var configuration = BuildTransportConfiguration(
            metadataAddress,
            requireHttpsMetadata: requireHttpsMetadata);

        var act = () => installer.InstallServices<BoltTransportAuthenticationConfigurationTests>(
            services,
            configuration,
            new TestHostEnvironment());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*BoltTransportAuthentication:MetadataAddress*");
    }

    [TestCase("Wrong.Issuer", BoltTransportAuthentication.ExpectedAudience, "Issuer")]
    [TestCase(BoltTransportAuthentication.ExpectedIssuer, "Wrong.Audience", "Audience")]
    public void InstallServices_NonCanonicalIssuerOrAudience_FailsClosed(
        string issuer,
        string audience,
        string invalidSetting)
    {
        var installer = new BoltTransportAuthenticationInstaller();
        var services = new ServiceCollection();
        var configuration = BuildTransportConfiguration(
            MetadataAddress,
            issuer: issuer,
            audience: audience);

        var act = () => installer.InstallServices<BoltTransportAuthenticationConfigurationTests>(
            services,
            configuration,
            new TestHostEnvironment());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*BoltTransportAuthentication:{invalidSetting}*");
    }

    [Test]
    public void InstallServices_HttpMetadataExplicitlyAllowed_ConfiguresBearer()
    {
        const string httpMetadataAddress = "http://identity.test/.well-known/openid-configuration";
        var configuration = BuildTransportConfiguration(
            httpMetadataAddress,
            requireHttpsMetadata: false);
        var services = new ServiceCollection();

        new BoltTransportAuthenticationInstaller()
            .InstallServices<BoltTransportAuthenticationConfigurationTests>(
                services,
                configuration,
                new TestHostEnvironment());
        services.InstallJwt(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(BoltTransportAuthentication.Scheme);

        options.MetadataAddress.Should().Be(httpMetadataAddress);
        options.RequireHttpsMetadata.Should().BeFalse();
    }

    [Test]
    public void InstallServices_DedicatedBearer_UsesRsaAndPreservesSharedBearer()
    {
        var configuration = BuildTransportConfiguration(MetadataAddress);
        var services = new ServiceCollection();

        // Match Hub startup order: assembly installers run before InstallJwt.
        new BoltTransportAuthenticationInstaller()
            .InstallServices<BoltTransportAuthenticationConfigurationTests>(
                services,
                configuration,
                new TestHostEnvironment());
        services.InstallJwt(configuration);

        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var options = monitor.Get(BoltTransportAuthentication.Scheme);
        var sharedOptions = monitor.Get(JwtBearerDefaults.AuthenticationScheme);
        var validation = options.TokenValidationParameters;

        options.MetadataAddress.Should().Be(MetadataAddress);
        options.RequireHttpsMetadata.Should().BeTrue();
        options.RefreshOnIssuerKeyNotFound.Should().BeTrue();
        options.MapInboundClaims.Should().BeFalse();
        options.Events.OnMessageReceived.Should().NotBeNull(
            "the shared Bolt query-token redaction handoff must remain installed");

        validation.ValidateIssuerSigningKey.Should().BeTrue();
        validation.RequireSignedTokens.Should().BeTrue();
        validation.ValidateIssuer.Should().BeTrue();
        validation.ValidIssuer.Should().Be(BoltTransportAuthentication.ExpectedIssuer);
        validation.ValidateAudience.Should().BeTrue();
        validation.ValidAudience.Should().Be(BoltTransportAuthentication.ExpectedAudience);
        validation.RequireExpirationTime.Should().BeTrue();
        validation.ValidateLifetime.Should().BeTrue();
        validation.ClockSkew.Should().Be(TimeSpan.FromSeconds(30));
        validation.ValidTypes.Should().Equal(BoltTransportAuthentication.ExpectedTokenType);
        validation.ValidAlgorithms.Should().Equal(SecurityAlgorithms.RsaSha256);

        validation.IssuerSigningKey.Should().BeNull();
        validation.IssuerSigningKeys.Should().BeNullOrEmpty();
        validation.IssuerSigningKeyResolver.Should().BeNull();
        validation.IssuerSigningKeyValidator.Should().BeNull();
        validation.ValidAlgorithms.Should().NotContain(SecurityAlgorithms.HmacSha512);

        sharedOptions.MetadataAddress.Should().BeNull();
        sharedOptions.TokenValidationParameters.IssuerSigningKeyResolver.Should().NotBeNull();
        sharedOptions.TokenValidationParameters.IssuerSigningKeyValidator.Should().NotBeNull();
        sharedOptions.TokenValidationParameters.ValidAlgorithms.Should().Contain(SecurityAlgorithms.RsaSha512);
        sharedOptions.TokenValidationParameters.ValidAlgorithms.Should().NotContain(SecurityAlgorithms.HmacSha512);
        sharedOptions.TokenValidationParameters.ValidIssuer.Should().Be("shared-issuer");
        sharedOptions.TokenValidationParameters.ValidAudience.Should().Be("shared-audience");
    }

    private static IConfiguration BuildTransportConfiguration(
        string metadataAddress,
        bool requireHttpsMetadata = true,
        string issuer = BoltTransportAuthentication.ExpectedIssuer,
        string audience = BoltTransportAuthentication.ExpectedAudience)
    {
        var values = new Dictionary<string, string?>
        {
            ["BoltTransportAuthentication:MetadataAddress"] = metadataAddress,
            ["BoltTransportAuthentication:Issuer"] = issuer,
            ["BoltTransportAuthentication:Audience"] = audience,
            ["BoltTransportAuthentication:RequireHttpsMetadata"] = requireHttpsMetadata.ToString(),
            ["JwtOptions:GenerationId"] = "shared-rsa-g0",
            ["JwtOptions:SigningPublicKeyPath"] = TestJwtKeyMaterial.PublicKeyPath,
            ["JwtOptions:ValidIssuer"] = "shared-issuer",
            ["JwtOptions:ValidAudience"] = "shared-audience"
        };

        return BuildConfiguration(values);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Bolt.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
