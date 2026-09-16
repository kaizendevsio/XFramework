using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace IdentityServer.Api.Infrastructure;

/// <summary>Single-use short-lived exchanges for the current single-instance IdentityServer.</summary>
public sealed class OpaqueExchanges(TimeProvider clock)
{
    private readonly object admission = new();
    private readonly ConcurrentDictionary<Guid, Entry> entries = new();
    public sealed record Entry(Guid TenantId, Guid RoleId, string UserName, string Client,
        Guid CredentialId, Guid Epoch, Guid CredentialStamp, string Kind, string State,
        DateTimeOffset Expires, string? Record = null, string? WrappedRecovery = null,
        string? DisplayName = null, IdentityServer.Domain.Shared.Contracts.Requests.PutEncryptionDirectoryRequest? Directory = null, string? Archive = null, string? Root = null, long BackupRevision = 0);

    public Guid Add(Entry entry)
    {
        lock (admission)
        {
            foreach (var item in entries.Where(x => x.Value.Expires <= clock.GetUtcNow())) entries.TryRemove(item.Key, out _);
            if (entries.Count >= 256) throw new InvalidOperationException("Too many authentication exchanges.");
            var id = Guid.NewGuid();
            if (!entries.TryAdd(id, entry with { Expires = clock.GetUtcNow().AddMinutes(2) })) throw new CryptographicException();
            return id;
        }
    }

    public Entry? TakeForActor(Guid id, Guid tenant, Guid credential)
    {
        if (!entries.TryRemove(id, out var entry) || entry.Expires <= clock.GetUtcNow() ||
            entry.TenantId != tenant || entry.CredentialId != credential || entry.Kind != "reauth") return null;
        return entry;
    }

    public Entry? Take(Guid id, Guid tenant, Guid role)
    {
        if (!entries.TryRemove(id, out var entry) || entry.Expires <= clock.GetUtcNow() ||
            entry.TenantId != tenant || entry.RoleId != role) return null;
        return entry;
    }
}
