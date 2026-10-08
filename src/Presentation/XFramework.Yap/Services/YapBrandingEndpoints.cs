using Yap.Contracts;

namespace Yap.Services;

public static class YapBrandingEndpoints
{
    public static void MapYapBranding(this WebApplication app)
    {
        app.MapGet("/api/branding", (HttpContext context, YapTenants tenants) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(tenants.Resolve(context)?.Branding ?? YapBranding.Default);
        }).AllowAnonymous();
        // The published worker verifies static assets against build hashes. Use an API URL for the
        // tenant manifest so it stays network-only; retain the legacy static manifest byte for byte.
        app.MapGet("/api/branding/manifest", (HttpContext context, YapTenants tenants) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var brand = tenants.Resolve(context)?.Branding ?? YapBranding.Default;
            return Results.Json(new
            {
                id = "/", name = brand.Name, short_name = brand.ShortName, description = brand.Tagline,
                start_url = "/", scope = "/", display = "standalone", background_color = "#1b1b1b", theme_color = brand.AccentColor,
                icons = new[] { Icon(brand.Icon192Url, "192x192"), Icon(brand.Icon512Url, "512x512") }
            }, contentType: "application/manifest+json");
        }).AllowAnonymous();
    }

    private static object Icon(string path, string sizes) => new { src = path, sizes, purpose = "any maskable" };
}
