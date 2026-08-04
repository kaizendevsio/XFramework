using Microsoft.AspNetCore.Authentication.JwtBearer;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Infrastructure;

public static class IdentitySessionJwtValidation
{
    public static IServiceCollection AddIdentitySessionJwtValidation(this IServiceCollection services)
    {
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
