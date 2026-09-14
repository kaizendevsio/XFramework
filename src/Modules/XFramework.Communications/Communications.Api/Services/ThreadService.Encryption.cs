using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Responses;

namespace Communications.Api.Services;

public sealed partial class ThreadService
{
    private async Task<bool> EncryptionRecipientsCurrentAsync(Guid tenant, Guid sender, Guid? deviceId, long? senderRevision,
        Dictionary<Guid, long> revisions, IReadOnlyCollection<Guid> recipients, CancellationToken ct)
    {
        if (deviceId is null || senderRevision is null || revisions.Count is < 1 or > 101
            || !recipients.ToHashSet().SetEquals(revisions.Keys)) return false;
        // Public Identity read model, like IdentityCredential membership validation in this service.
        // No recovery material is selected; Identity remains the sole writer of these records.
        var keys = revisions.Keys.ToArray();
        if (encryptionDirectoryReader is null) return false;
        var directories = await encryptionDirectoryReader.ReadAsync(tenant, keys, ct);
        if (directories.Count != revisions.Count || directories.Any(x => x.Revision != revisions[x.CredentialId])) return false;
        var own = directories.SingleOrDefault(x => x.CredentialId == sender);
        if (own is null || own.Revision != senderRevision) return false;
        var devices = JsonSerializer.Deserialize<List<EncryptionDevice>>(own.DevicesJson, OutboxJsonOptions) ?? [];
        return devices.Any(x => x.DeviceId == deviceId && x.Revocation is null);
    }
}
