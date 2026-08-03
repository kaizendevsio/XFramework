using System.Text;

namespace IdentityServer.Api.Infrastructure;

public static class IdentityPasswordPolicy
{
    public const int MaximumUtf8ByteCount = 72;

    public static bool IsWithinBcryptByteLimit(string? password) =>
        password is not null && Encoding.UTF8.GetByteCount(password) <= MaximumUtf8ByteCount;
}
