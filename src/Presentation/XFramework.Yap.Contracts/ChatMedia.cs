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

    // HEIF never decodes natively; image-previews.js converts it before anything renders it.
    private static bool IsHeif(string? type, string name) =>
        (type ?? "").StartsWith("image/heic", StringComparison.OrdinalIgnoreCase)
        || (type ?? "").StartsWith("image/heif", StringComparison.OrdinalIgnoreCase)
        || Path.GetExtension(name).ToLowerInvariant() is ".heic" or ".heif";

    /// <summary>Images shown as a thumbnail; anything else stays a download row.</summary>
    public static bool IsInlineImage(string? type, string name) => IsHeif(type, name)
        || type?.Split(';')[0].ToLowerInvariant() is "image/jpeg" or "image/png" or "image/gif" or "image/webp" or "image/avif";

    /// <summary>Video shown as a poster thumbnail; anything else stays a download row.</summary>
    public static bool IsInlineVideo(string? type, string name) => ContentType(type, name)
        is "video/mp4" or "video/quicktime" or "video/webm";

    /// <summary>
    /// What stands in for a file on screen. Only <see cref="AttachmentPreview.File"/> has nothing
    /// to show, so only it earns a name-and-size row; the rest are their own preview.
    /// </summary>
    public static AttachmentPreview Preview(string? type, string name) =>
        IsInlineImage(type, name) ? AttachmentPreview.Photo
        : IsInlineVideo(type, name) ? AttachmentPreview.Video
        : IsInlineAudio(type, name) ? AttachmentPreview.Voice
        : AttachmentPreview.File;
}

public enum AttachmentPreview { Photo, Video, Voice, File }
