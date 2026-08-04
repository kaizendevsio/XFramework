using IdentityServer.Integration.Security;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using XFramework.Integration.Security;

namespace IdentityServer.Integration.Extensions;

public static class IdentitySessionValidationExtensions
{
    private sealed class SessionValidationRegistrationMarker;

    public static IServiceCollection AddIdentityServerHttpActorValidation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ServiceIdentityOptions>()
            .Configure(options => configuration.GetSection(ServiceIdentityOptions.SectionName).Bind(options));
        services.AddHttpClient(IdentityServerHttpActorIdentityProvider.ClientName, (serviceProvider, client) =>
        {
            client.BaseAddress = serviceProvider
                .GetRequiredService<IOptions<ServiceIdentityOptions>>()
                .Value
                .ResolveAuthority();
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.Replace(ServiceDescriptor.Scoped<IActorIdentityProvider, IdentityServerHttpActorIdentityProvider>());
        return services;
    }

    public static IServiceCollection AddIdentityServerSessionValidation(this IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(SessionValidationRegistrationMarker)))
            return services;

        services.AddSingleton<SessionValidationRegistrationMarker>();
        services.AddHttpContextAccessor();
        services.AddIdentityServerWrapperServices();
        services.Replace(ServiceDescriptor.Scoped<IActorIdentityProvider, IdentityServerActorIdentityProvider>());
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            var priorValidation = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async context =>
            {
                if (priorValidation is not null)
                    await priorValidation(context);

                if (context.Result?.Failure is not null)
                    return;

                var token = ReadBearerToken(context.HttpContext.Request.Headers.Authorization.ToString());
                if (string.IsNullOrWhiteSpace(token))
                {
                    context.Fail("Identity session is no longer valid");
                    return;
                }

                var provider = context.HttpContext.RequestServices.GetRequiredService<IActorIdentityProvider>();
                var validation = await provider.ValidateAsync(token, context.HttpContext.RequestAborted);
                if (!validation.IsValid)
                {
                    context.Fail(validation.Error ?? "Identity session is no longer valid");
                    return;
                }

            };
        });

        return services;
    }

    private static string? ReadBearerToken(string value)
    {
        const string prefix = "Bearer ";
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].Trim()
            : null;
    }
}
