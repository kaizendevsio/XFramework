namespace XFramework.Portal.Features.POS.Scanner;

public static class ScannerPublicOrigin
{
    public static bool TryResolve(string? configured, string desktopBaseUri, out Uri? origin)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? desktopBaseUri : configured.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate) ||
            candidate.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(candidate.Host) ||
            !string.IsNullOrEmpty(candidate.UserInfo) || !string.IsNullOrEmpty(candidate.Query) ||
            !string.IsNullOrEmpty(candidate.Fragment) || candidate.AbsolutePath != "/")
        {
            origin = null;
            return false;
        }
        origin = candidate;
        return true;
    }
}
