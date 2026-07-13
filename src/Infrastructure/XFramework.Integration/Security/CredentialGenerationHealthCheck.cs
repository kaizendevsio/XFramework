using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Integration.Security;

public sealed class CredentialGenerationHealthCheck(
    IConfiguration configuration,
    IServiceProvider serviceProvider,
    TimeProvider timeProvider)
    : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["jwt"] = ReadJwtDiagnostic(),
            ["serviceCredential"] = ReadServiceCredentialDiagnostic(),
            ["identityServerClients"] = ReadIdentityServerClientDiagnostics()
        };

        return Task.FromResult(HealthCheckResult.Healthy(
            "Credential generation convergence metadata.",
            data));
    }

    private IReadOnlyDictionary<string, object> ReadJwtDiagnostic()
    {
        var options = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>() ?? new JwtOptions();
        return CreateDiagnostic(
            options.GenerationId,
            options.ValidationFallback?.GenerationId,
            options.ValidationFallback?.ValidUntilUtc);
    }

    private IReadOnlyDictionary<string, object> ReadServiceCredentialDiagnostic()
    {
        var options = serviceProvider.GetService<IOptions<ServiceIdentityOptions>>()?.Value;
        return CreateDiagnostic(
            options?.GenerationId,
            options?.ValidationFallback?.GenerationId,
            options?.ValidationFallback?.ValidUntilUtc);
    }

    private IReadOnlyDictionary<string, object> ReadIdentityServerClientDiagnostics()
    {
        var diagnostics = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var clientSection in configuration.GetSection("ServiceIdentity:Clients").GetChildren())
        {
            var clientId = clientSection["ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
                continue;

            var fallback = clientSection.GetSection("ValidationFallback");
            diagnostics[clientId] = CreateDiagnostic(
                clientSection["GenerationId"],
                fallback["GenerationId"],
                fallback.GetValue<DateTimeOffset?>("ValidUntilUtc"));
        }

        return diagnostics;
    }

    private IReadOnlyDictionary<string, object> CreateDiagnostic(
        string? currentGenerationId,
        string? fallbackGenerationId,
        DateTimeOffset? fallbackValidUntilUtc)
    {
        var diagnostic = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["configured"] = !string.IsNullOrWhiteSpace(currentGenerationId),
            ["currentGenerationId"] = currentGenerationId?.Trim() ?? string.Empty,
            ["validationFallbackConfigured"] = !string.IsNullOrWhiteSpace(fallbackGenerationId)
        };

        if (!string.IsNullOrWhiteSpace(fallbackGenerationId))
            diagnostic["validationFallbackGenerationId"] = fallbackGenerationId.Trim();

        if (fallbackValidUntilUtc is { } validUntilUtc)
        {
            diagnostic["validationFallbackValidUntilUtc"] = validUntilUtc.UtcDateTime.ToString("O");
            diagnostic["validationFallbackActive"] = validUntilUtc > timeProvider.GetUtcNow();
        }

        return diagnostic;
    }
}

public static class CredentialGenerationHealthCheckExtensions
{
    public static IServiceCollection AddCredentialGenerationHealthCheck(this IServiceCollection services)
    {
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(CredentialGenerationHealthCheck)))
            return services;

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CredentialGenerationHealthCheck>();
        services.Configure<HealthCheckServiceOptions>(options =>
        {
            if (options.Registrations.Any(static registration => registration.Name == "credential-generations"))
                return;

            options.Registrations.Add(new HealthCheckRegistration(
                "credential-generations",
                serviceProvider => serviceProvider.GetRequiredService<CredentialGenerationHealthCheck>(),
                HealthStatus.Unhealthy,
                ["ready", "security", "credentials"],
                timeout: null));
        });
        return services;
    }
}
