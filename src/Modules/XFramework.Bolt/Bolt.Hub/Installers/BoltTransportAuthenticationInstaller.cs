using Bolt.Hub.Configurations;
using XFramework.Core.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using XFramework.Domain.Shared.Interfaces;

namespace Bolt.Hub.Installers;

public sealed class BoltTransportAuthenticationInstaller : IInstaller
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public void InstallServices<TApp>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        var authentication = new BoltTransportAuthentication();
        configuration.GetSection(BoltTransportAuthentication.SectionName).Bind(authentication);
        Validate(authentication);

        services.AddSingleton(authentication);
        services.AddAuthentication()
            .AddJwtBearer(
                BoltTransportAuthentication.Scheme,
                options => ConfigureBearer(options, authentication));
    }

    private static void ConfigureBearer(
        JwtBearerOptions options,
        BoltTransportAuthentication authentication)
    {
        options.Authority = null;
        options.Audience = authentication.Audience;
        options.MetadataAddress = authentication.MetadataAddress;
        options.RequireHttpsMetadata = authentication.RequireHttpsMetadata;
        options.RefreshOnIssuerKeyNotFound = true;
        options.MapInboundClaims = false;
        options.SaveToken = true;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = BoltAccessTokenRedactionMiddleware.GetAccessToken(context.HttpContext)
                    ?? context.Request.Query["access_token"].ToString();
                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/bolt/ws"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            AudienceValidator = (audiences, _, _) =>
                HasExactAudience(audiences, authentication.Audience),
            ClockSkew = ClockSkew,
            IssuerValidator = (issuer, _, _) =>
                ValidateIssuer(issuer, authentication.Issuer),
            RequireAudience = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            TryAllIssuerSigningKeys = true,
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidAudience = authentication.Audience,
            ValidIssuer = authentication.Issuer,
            ValidTypes = [BoltTransportAuthentication.ExpectedTokenType]
        };
    }

    private static bool HasExactAudience(IEnumerable<string>? audiences, string expectedAudience)
    {
        if (audiences is null)
            return false;

        using var enumerator = audiences.GetEnumerator();
        return enumerator.MoveNext() &&
               string.Equals(enumerator.Current, expectedAudience, StringComparison.Ordinal) &&
               !enumerator.MoveNext();
    }

    private static string ValidateIssuer(string? issuer, string expectedIssuer)
    {
        if (!string.Equals(issuer, expectedIssuer, StringComparison.Ordinal))
        {
            throw new SecurityTokenInvalidIssuerException(
                $"Bolt transport token issuer must be '{expectedIssuer}'.");
        }

        return expectedIssuer;
    }

    private static void Validate(BoltTransportAuthentication authentication)
    {
        if (string.IsNullOrWhiteSpace(authentication.MetadataAddress))
        {
            throw new InvalidOperationException(
                $"{BoltTransportAuthentication.SectionName}:MetadataAddress is required.");
        }

        authentication.MetadataAddress = authentication.MetadataAddress.Trim();
        if (!Uri.TryCreate(authentication.MetadataAddress, UriKind.Absolute, out var metadataUri) ||
            (!string.Equals(metadataUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(metadataUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"{BoltTransportAuthentication.SectionName}:MetadataAddress must be an absolute HTTP or HTTPS URI.");
        }

        if (metadataUri.Scheme == Uri.UriSchemeHttp && authentication.RequireHttpsMetadata)
        {
            throw new InvalidOperationException(
                $"{BoltTransportAuthentication.SectionName}:MetadataAddress must use HTTPS unless " +
                $"{BoltTransportAuthentication.SectionName}:RequireHttpsMetadata is explicitly configured as false.");
        }

        if (!string.Equals(
                authentication.Issuer,
                BoltTransportAuthentication.ExpectedIssuer,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{BoltTransportAuthentication.SectionName}:Issuer must be " +
                $"'{BoltTransportAuthentication.ExpectedIssuer}'.");
        }

        if (!string.Equals(
                authentication.Audience,
                BoltTransportAuthentication.ExpectedAudience,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{BoltTransportAuthentication.SectionName}:Audience must be " +
                $"'{BoltTransportAuthentication.ExpectedAudience}'.");
        }
    }
}
