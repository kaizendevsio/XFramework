using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IdentityServer.Api.Infrastructure;

/// <summary>Thin adapter to the pinned opaque-ke implementation; contains no protocol arithmetic.</summary>
public static class OpaqueNative
{
    [DllImport("xframework_opaque", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr xfw_opaque_execute(IntPtr input);
    [DllImport("xframework_opaque", CallingConvention = CallingConvention.Cdecl)]
    private static extern void xfw_opaque_free(IntPtr output);

    public static JsonElement Execute(object operation)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(operation) + "\0");
        var input = Marshal.AllocHGlobal(bytes.Length);
        IntPtr output = IntPtr.Zero;
        try
        {
            Marshal.Copy(bytes, 0, input, bytes.Length);
            output = xfw_opaque_execute(input);
            using var document = JsonDocument.Parse(Marshal.PtrToStringUTF8(output) ?? "{}");
            if (document.RootElement.TryGetProperty("error", out _)) throw new CryptographicException("Invalid OPAQUE exchange.");
            return document.RootElement.Clone();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            Marshal.Copy(bytes, 0, input, bytes.Length);
            Marshal.FreeHGlobal(input);
            if (output != IntPtr.Zero) xfw_opaque_free(output);
        }
    }
}
