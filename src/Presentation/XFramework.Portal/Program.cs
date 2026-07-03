using BlazorBlueprint.Components;
using System.Security.Claims;
using System.Text.Json;
using Attendance.Integration.Drivers;
using Community.Integration.Drivers;
using XFramework.Portal.Extensions;
using XFramework.Portal.Health;
using XFramework.Portal.Services;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Integration.Extensions;
using IdentityServer.Integration.Drivers;
using Inventario.Integration.Drivers;
using Communications.Integration.Drivers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using POS.Integration.Drivers;
using Storage.Integration.Drivers;
using Wallets.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;

var builder = WebApplication.CreateBuilder(args);

// Logging - ZLogger console (lifecycle only) + Seq (everything including Bolt RPC payloads)
builder.Logging.AddXFrameworkLogging(builder.Configuration);

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(PortalAuthDefaults.AuthenticationScheme)
    .AddCookie(PortalAuthDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "XFramework.Portal";
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();

// BlueprintUI
builder.Services.AddBlazorBlueprintComponents(configureTheme: options =>
{
    options.DefaultBaseColor = BaseColor.Slate;
    options.DefaultPrimaryColor = PrimaryColor.Blue;
    options.DefaultDarkMode = false;
    options.DetectSystemPreference = true;
    options.DefaultRadius = 0.5;
    options.PersistToLocalStorage = true;
});

// Bolt - thin binary RPC transport to microservices
builder.Services.AddXFrameworkBoltClient(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddCheck(
        "portal-live",
        () => HealthCheckResult.Healthy("Portal is running."),
        tags: ["live"])
    .AddCheck<BoltClientHealthCheck>(
        "portal-bolt",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

// Service wrappers - auto-generated CRUD + custom operations for each microservice
builder.Services.AddAttendanceWrapperServices();
builder.Services.AddCommunityWrapperServices();
builder.Services.AddIdentityServerWrapperServices();
builder.Services.AddInventarioWrapperServices();
builder.Services.AddPOSWrapperServices();
builder.Services.AddCommunicationsWrapperServices();
builder.Services.AddStorageWrapperServices();
builder.Services.AddWalletsWrapperServices();
builder.Services.AddTenantModuleFeatureDefinitions(builder.Configuration);

// IDataContext - universal query layer routed through service wrappers
builder.Services.AddScoped(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var user = httpContext?.User;
    var tenantFilter = sp.GetRequiredService<TenantFilterService>();
    var loginTenantId = TryGetGuidClaim(user, PortalAuthClaims.TenantId);

    var metadata = new RequestMetadata
    {
        TenantId = tenantFilter.SelectedTenantId ?? loginTenantId,
        CredentialId = TryGetGuidClaim(user, PortalAuthClaims.CredentialId),
        SessionId = TryGetGuidClaim(user, PortalAuthClaims.SessionId),
        RequestId = Guid.NewGuid(),
        Name = "Portal",
        DeviceName = Environment.MachineName,
        DeviceAgent = httpContext?.Request.Headers.UserAgent.ToString(),
        IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString()
    };

    tenantFilter.OnChanged += () =>
    {
        metadata.TenantId = tenantFilter.SelectedTenantId ?? loginTenantId;
        metadata.RequestId = Guid.NewGuid();
    };

    return metadata;
});
builder.Services.AddRemoteDataContext();

// Tenant filter state (sidebar selection)
builder.Services.AddScoped<TenantFilterService>();
builder.Services.AddScoped<TenantModuleNavigationService>();
builder.Services.AddScoped<TenantModuleFeatureDefinitionResolver>();
builder.Services.AddScoped<CommunicationsPortalGuard>();
builder.Services.AddScoped<CommunicationsPortalReadService>();
builder.Services.AddScoped<CommunicationsPortalSettingsService>();
builder.Services.AddScoped<CommunicationsPortalTemplateService>();
builder.Services.AddScoped<NavigationHistoryService>();
builder.Services.AddScoped<CommunityPortalAccessService>();
builder.Services.AddScoped<AttendancePortalReadService>();
builder.Services.AddScoped<WalletsAdminBackendContractService>();
builder.Services.AddScoped<WalletsPortalDisplayService>();
builder.Services.AddScoped<PortalAuthService>();
builder.Services.AddScoped<PortalBootstrapSeeder>();
builder.Services.AddHostedService<PortalBootstrapHostedService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponse,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteHealthResponse,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapStaticAssets();
app.MapPortalAuthEndpoints();
app.MapRazorComponents<XFramework.Portal.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

static async Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var response = new
    {
        status = report.Status.ToString(),
        duration = report.TotalDuration.TotalMilliseconds,
        timestamp = DateTime.UtcNow,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            duration = entry.Value.Duration.TotalMilliseconds,
            tags = entry.Value.Tags,
            data = entry.Value.Data,
            exception = entry.Value.Exception?.Message
        })
    };

    await context.Response.WriteAsync(
        JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
}

static Guid? TryGetGuidClaim(ClaimsPrincipal? user, string claimType)
{
    if (user?.Identity?.IsAuthenticated != true)
    {
        return null;
    }

    var value = user.FindFirst(claimType)?.Value;
    return Guid.TryParse(value, out var id) ? id : null;
}
