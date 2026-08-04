using System.Security.Cryptography;

namespace IdentityServer.Api.Services;

public interface IServiceSigningKeyStore
{
    Task<string> ReadPrivateKeyAsync(string keyReference, CancellationToken ct = default);
    Task<string> StorePrivateKeyAsync(string keyId, string privateKeyPem, CancellationToken ct = default);
    Task DeletePrivateKeyAsync(string keyReference, CancellationToken ct = default);
}

public sealed class FileSystemServiceSigningKeyStore(
    IConfiguration configuration,
    ServiceIdentityConfiguration serviceIdentityConfiguration)
    : IServiceSigningKeyStore
{
    public Task<string> ReadPrivateKeyAsync(string keyReference, CancellationToken ct = default) =>
        File.ReadAllTextAsync(ResolvePath(keyReference), ct);

    public async Task<string> StorePrivateKeyAsync(
        string keyId,
        string privateKeyPem,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var fileName = $"{keyId}.pem";
        var directory = GetDirectory();
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, fileName);
        var temporary = Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
        var bytes = Encoding.ASCII.GetBytes(privateKeyPem);
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None
            };
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

            await using (var stream = new FileStream(temporary, options))
            {
                await stream.WriteAsync(bytes, ct);
                await stream.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, destination, overwrite: false);
            return fileName;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public Task DeletePrivateKeyAsync(string keyReference, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = ResolvePath(keyReference);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string keyReference) =>
        Path.Combine(GetDirectory(), Path.GetFileName(keyReference));

    private string GetDirectory()
    {
        var configured = configuration["ServiceIdentity:ServiceTokenSigningKeyDirectory"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        var transportKeyPath = serviceIdentityConfiguration.BoltTransportSigningKeyPath;
        var parent = string.IsNullOrWhiteSpace(transportKeyPath)
            ? Path.Combine(AppContext.BaseDirectory, ".keys")
            : Path.GetDirectoryName(Path.GetFullPath(transportKeyPath))!;
        return Path.Combine(parent, "service-token-signing-keys");
    }
}
