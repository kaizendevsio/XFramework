namespace Notifications.Api.Services.Push;

/// <summary>Unpadded base64url (RFC 4648 section 5), the wire format for every Web Push key and token part.</summary>
public static class PushBase64Url
{
    public static string Encode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string value)
    {
        var normalized = value.Trim().Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '='));
    }

    public static bool TryDecode(string? value, int expectedLength, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            bytes = Decode(value);
        }
        catch (FormatException)
        {
            return false;
        }

        return expectedLength <= 0 || bytes.Length == expectedLength;
    }
}
