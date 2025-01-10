using System.Security.Cryptography;
using System.Text;
#pragma warning disable SYSLIB0021

namespace XFramework.SourceGenerators;

public static class Cryptography
{
    public static string ToMd5(this string stringData)
    {
        var md5Provider = new MD5CryptoServiceProvider();
        var bytes = md5Provider.ComputeHash(new UTF8Encoding().GetBytes(stringData));
        return GetStringFromHash(bytes);
    }

    public static string ToSha256(this string stringData)
    {
        var sha256 = SHA256Managed.Create();
        var passwordByte = Encoding.UTF8.GetBytes(stringData);
        var bytes = sha256.ComputeHash(passwordByte);

        return GetStringFromHash(bytes);
    }

    public static string ToSha512(this string stringData)
    {
        var sha512 = SHA512Managed.Create();
        var passwordByte = Encoding.UTF8.GetBytes(stringData);
        var bytes = sha512.ComputeHash(passwordByte);

        return GetStringFromHash(bytes);
    }

    private static string GetStringFromHash(byte[] hash)
    {
        var result = new StringBuilder();
        foreach (var t in hash)
        {
            result.Append(t.ToString("X2"));
        }

        return result.ToString().ToLower();
    }
    
}