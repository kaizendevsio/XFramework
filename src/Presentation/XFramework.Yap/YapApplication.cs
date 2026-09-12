using Bolt.Client;
using Communications.Integration.Clients;
using Communications.Integration.Drivers;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Storage.Integration.Drivers;
using XFramework.Integration.Extensions;
using XFramework.Integration.Logging;
using Yap.Contracts;
using Yap.Services;

namespace Yap;

public static class YapApplication
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.AddXFrameworkLogging(builder.Configuration);
        builder.Services.AddAntiforgery();
        // A staged attachment arrives in one request; anything larger arrives one part at a time.
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = ChatLimits.StagedFileBytes + 65536);
        // Sessions hold refresh tokens, so they outlive a restart only when the store does.
        // Without a connection string this falls back to memory, which suits tests and local runs.
        var sessionCache = builder.Configuration["Yap:SessionCacheConnection"]
            ?? builder.Configuration["CacheConfiguration:RedisConnectionString"];
        if (string.IsNullOrWhiteSpace(sessionCache)) builder.Services.AddDistributedMemoryCache();
        else builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = sessionCache;
            options.InstanceName = string.Empty;
        });
        builder.Services.AddDataProtection();
        builder.Services.AddHttpClient("attachments", client => client.Timeout = TimeSpan.FromMinutes(2));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMemoryCache();
        builder.Services.AddAuthentication(YapAuth.Scheme).AddCookie(YapAuth.Scheme, options =>
        {
            options.Cookie.Name = "Yap.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.LoginPath = "/login";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            };
            options.Events.OnValidatePrincipal = async context =>
            {
                var sessions = context.HttpContext.RequestServices.GetRequiredService<YapSessions>();
                if (!await sessions.ContainsAsync(context.Principal, context.HttpContext.RequestAborted))
                    context.RejectPrincipal();
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddXFrameworkBoltClient(builder.Configuration,
            hostEnvironment: builder.Environment, connectAfterApplicationStarted: true);
        builder.Services.AddIdentityServerWrapperServices();
        builder.Services.AddCommunicationsWrapperServices();
        builder.Services.AddStorageWrapperServices();
        builder.Services.AddSingleton<YapSessions>();
        builder.Services.AddScoped<ICommunicationsChatActorProvider, YapActorProvider>();
        builder.Services.AddScoped<IChatDirectory, ChatDirectory>();
        builder.Services.AddScoped<ChatFiles>();

        configure?.Invoke(builder);
        var app = builder.Build();
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error");
            app.UseHsts();
            app.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"),
                branch => branch.UseHttpsRedirection());
        }
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                if (context.File.Name is "index.html" or "service-worker.js" or "service-worker-assets.js" or "manifest.webmanifest")
                    context.Context.Response.Headers.CacheControl = "no-cache";
            }
        });
        app.MapYapAuth();
        app.MapYapApi();
        app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
        app.MapGet("/health/ready", (BoltClient client, IConfiguration configuration) =>
        {
            var configured = Guid.TryParse(configuration["Yap:TenantId"], out var tenant) && tenant != Guid.Empty &&
                             Guid.TryParse(configuration["Yap:RoleId"], out var role) && role != Guid.Empty;
            return client.IsConnected && configured
                ? Results.Ok(new { status = "Healthy" })
                : Results.Json(new { status = "Unhealthy", reason = configured ? "Bolt is disconnected" : "Workspace is not configured" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });
        app.Map("/api/{**path}", () => Results.NotFound());
        app.MapFallbackToFile("index.html");
        return app;
    }
}
