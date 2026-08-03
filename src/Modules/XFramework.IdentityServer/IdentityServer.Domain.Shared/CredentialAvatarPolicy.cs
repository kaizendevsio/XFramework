namespace IdentityServer.Domain.Shared;

public static class CredentialAvatarPolicy
{
    public const int MaxFileSizeBytes = 5 * 1024 * 1024;
    public const string StorageIdentifierGroupName = "IdentityServer";
    public const string StorageFileIdentifierName = "IdentityCredentialAvatar";

    public static readonly string[] AllowedContentTypes =
    [
        "image/png",
        "image/jpeg",
        "image/webp"
    ];

    public static string? NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        return contentType.Trim().ToLowerInvariant() switch
        {
            "image/jpg" => "image/jpeg",
            var value => value
        };
    }

    public static bool IsAllowedContentType(string? contentType)
    {
        var normalized = NormalizeContentType(contentType);

        return normalized is not null
            && AllowedContentTypes.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public static string GetFileExtension(string contentType) =>
        NormalizeContentType(contentType) switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".img"
        };
}
