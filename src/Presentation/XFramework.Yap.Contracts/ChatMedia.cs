namespace Yap.Contracts;

public static class ChatMedia
{
    // Mobile file pickers sometimes omit the type or return application/octet-stream.
    public static string ContentType(string? type, string name)
    {
        var normalized = type?.Split(';')[0].Trim().ToLowerInvariant();
        if (normalized is not (null or "" or "application/octet-stream" or "binary/octet-stream"))
            return normalized;
        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".mov" => "video/quicktime",
            ".mp4" or ".m4v" => "video/mp4",
            ".webm" => "video/webm",
            _ => normalized ?? "application/octet-stream"
        };
    }
}
