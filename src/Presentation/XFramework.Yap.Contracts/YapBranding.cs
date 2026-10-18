namespace Yap.Contracts;

// Only public presentation fields belong here. Tenant routing and credentials stay on the host.
public sealed record YapBranding(string Name, string ShortName, string Tagline, string LogoUrl,
    string AccentColor, string Icon192Url, string Icon512Url, string AppleIconUrl,
    string ManifestUrl = "/manifest.webmanifest")
{
    public static YapBranding Default { get; } = new("Yap", "Yap", "A little closer.", "",
        "#d5f879", "/yap-app-v2-192.png", "/yap-app-v2-512.png", "/yap-app-v2-apple.png");
}
