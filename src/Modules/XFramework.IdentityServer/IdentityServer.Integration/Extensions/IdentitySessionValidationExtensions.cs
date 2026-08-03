using System.Security.Claims;
using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace IdentityServer.Integration.Extensions;

public static class IdentitySessionValidationExtensions
{
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddIdentityServerSessionValidation(this IServiceCollection services)
    {
        services.AddIdentityServerWrapperServices();
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            var priorValidation = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async context =>
            {
                if (priorValidation is not null)
                    await priorValidation(context);

                if (context.Result?.Failure is not null || context.Principal is null)
                    return;

                var principal = context.Principal;
                var credentialClaim = principal.FindFirst("credential_id")?.Value;
                var sessionClaim = principal.FindFirst("session_id")?.Value;
                var tenantClaim = principal.FindFirst("tenant_id")?.Value
                    ?? principal.FindFirst("tenantId")?.Value;
                var generationClaim = principal
                    .FindFirst(JwtCredentialSet.GenerationClaim)?.Value;

                if (!Guid.TryParse(credentialClaim, out var credentialId)
                    || !Guid.TryParse(sessionClaim, out var sessionId)
                    || !Guid.TryParse(tenantClaim, out var tenantId)
                    || string.IsNullOrWhiteSpace(generationClaim))
                {
                    context.Fail("Identity session is no longer valid");
                    return;
                }

                try
                {
                    var request = new ValidateIdentitySessionRequest
                    {
                        TenantId = tenantId,
                        CredentialId = credentialId,
                        SessionId = sessionId,
                        RoleTypeIds = ParseRoleTypeIds(principal),
                        Metadata = new RequestMetadata
                        {
                            TenantId = tenantId,
                            CredentialId = credentialId,
                            SessionId = sessionId,
                            RequestId = Guid.NewGuid(),
                            Name = "Downstream identity session validation",
                            DeviceName = Environment.MachineName
                        }
                    };

                    var identityServer = context.HttpContext.RequestServices
                        .GetRequiredService<IIdentityServerServiceWrapper>();
                    using var validationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        context.HttpContext.RequestAborted);
                    validationCancellation.CancelAfter(ValidationTimeout);
                    var result = await identityServer.ValidateIdentitySession(
                        request,
                        validationCancellation.Token);
                    var response = result.Response;

                    if (!result.IsSuccess
                        || response is not { IsValid: true }
                        || response.TenantId != tenantId
                        || response.CredentialId != credentialId
                        || response.SessionId != sessionId)
                    {
                        context.Fail("Identity session is no longer valid");
                    }
                }
                catch (Exception exception)
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("IdentityServerSessionValidation");
                    logger.LogWarning(
                        exception,
                        "Identity session validation failed closed for tenant {TenantId}, credential {CredentialId}, session {SessionId}",
                        tenantId,
                        credentialId,
                        sessionId);
                    context.Fail("Identity session validation is unavailable");
                }
            };
        });

        return services;
    }

    private static List<Guid> ParseRoleTypeIds(ClaimsPrincipal principal)
    {
        var result = new HashSet<Guid>();
        foreach (var claim in principal.FindAll(ClaimTypes.Role))
        {
            if (Guid.TryParse(claim.Value, out var roleTypeId))
            {
                result.Add(roleTypeId);
                continue;
            }

            try
            {
                foreach (var value in JsonSerializer.Deserialize<List<Guid>>(claim.Value) ?? [])
                    result.Add(value);
            }
            catch (JsonException)
            {
                result.Add(Guid.Empty);
            }
        }

        return result.ToList();
    }
}
