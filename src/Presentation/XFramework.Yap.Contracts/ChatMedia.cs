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
            // Voice clips land as .m4a/.ogg/.opus; without a type they would never play inline.
            ".m4a" => "audio/mp4",
            ".ogg" or ".opus" => "audio/ogg",
            _ => normalized ?? "application/octet-stream"
        };
    }

    /// <summary>Audio the browser can play in the voice player; anything else stays a download row.</summary>
    // Kept in step with the types yap.device.mediaUrl will hand back.
    public static bool IsInlineAudio(string? type, string name) => ContentType(type, name)
        is "audio/mp4" or "audio/mpeg" or "audio/ogg" or "audio/webm" or "audio/wav" or "audio/x-wav" or "audio/aac";
}
