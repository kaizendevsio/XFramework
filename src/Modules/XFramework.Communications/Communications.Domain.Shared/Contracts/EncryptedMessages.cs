namespace Communications.Domain.Shared.Contracts;

/// <summary>Transport bounds only. Signature and recipient verification happen on trusted clients.</summary>
public static class EncryptedMessages
{
    public const string Preview = "Encrypted message";
    public const int MaxEnvelopeLength = 256 * 1024;
    public static bool IsVoiceFile(string? name, string? contentType) =>
        contentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true
        || name == "voice.pgp" && contentType == "application/octet-stream";

    public static bool ValidEnvelope(string? envelope) => envelope is { Length: > 80 and <= MaxEnvelopeLength }
        && envelope.StartsWith("-----BEGIN PGP MESSAGE-----", StringComparison.Ordinal)
        && envelope.TrimEnd().EndsWith("-----END PGP MESSAGE-----", StringComparison.Ordinal);
}
