using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using BlazorBlueprint.Primitives.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using POS.Integration.Drivers;
using Storage.Integration.Drivers;
using Wallets.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Core.Health;
using XFramework.Integration.Security;
using IdentityServer.Integration.Security;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Services;
using XFramework.Portal.Composition;

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
        options.EventsType = typeof(PortalCookieAuthenticationEvents);
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
// Remove this compatibility layer after a BlazorBlueprint upgrade passes repeated dynamic-dialog tests.
builder.Services.AddScoped<XfPortalService>();
builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(
    services => services.GetRequiredService<XfPortalService>()));

// Bolt - thin binary RPC transport to microservices
builder.Services.AddXFrameworkBoltClient(builder.Configuration, hostEnvironment: builder.Environment);
builder.Services.AddScoped<PortalActorContext>();
builder.Services.AddScoped<PortalActorAccessTokenProvider>();
builder.Services.Replace(ServiceDescriptor.Scoped<IActorAccessTokenProvider>(services =>
    services.GetRequiredService<PortalActorAccessTokenProvider>()));
builder.Services.Replace(ServiceDescriptor.Scoped<IActorAccessTokenScope>(services =>
    services.GetRequiredService<PortalActorAccessTokenProvider>()));
builder.Services.Replace(ServiceDescriptor.Scoped<IActorIdentityProvider, IdentityServerActorIdentityProvider>());

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
        RequestedTenantId = tenantFilter.SelectedTenantId ?? loginTenantId,
        RequestId = Guid.NewGuid(),
        OperationName = "Portal",
        DeviceName = Environment.MachineName,
        UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
        IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString()
    };

    tenantFilter.OnChanged += () =>
    {
        metadata.RequestedTenantId = tenantFilter.SelectedTenantId ?? loginTenantId;
        metadata.RequestId = Guid.NewGuid();
    };

    return metadata;
});
builder.Services.AddRemoteDataContext();

// Tenant filter state (sidebar selection)
builder.Services.AddScoped<TenantFilterService>();
builder.Services.AddScoped<TenantModuleNavigationService>();
builder.Services.AddScoped<IPortalTenantContext>(services =>
    services.GetRequiredService<TenantFilterService>());
builder.Services.AddScoped<IPortalModuleAvailability>(services =>
    services.GetRequiredService<TenantModuleNavigationService>());
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
builder.Services.AddSingleton<PortalActorTokenRefreshCoordinator>();
builder.Services.AddScoped<PortalIdentitySessionValidator>();
builder.Services.AddScoped<PortalCookieAuthenticationEvents>();
builder.Services.AddScoped<AuthenticationStateProvider, PortalRevalidatingAuthenticationStateProvider>();
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

app.MapXFrameworkHealthChecks("XFramework.Portal");

app.MapStaticAssets();
app.MapPortalAuthEndpoints();
app.MapRazorComponents<XFramework.Portal.Components.App>()
    .AddAdditionalAssemblies(PortalFeatureAssemblies.All)
    .AddInteractiveServerRenderMode();

app.Run();

static Guid? TryGetGuidClaim(ClaimsPrincipal? user, string claimType)
{
    if (user?.Identity?.IsAuthenticated != true)
    {
        return null;
    }

    var value = user.FindFirst(claimType)?.Value;
    return Guid.TryParse(value, out var id) ? id : null;
}
