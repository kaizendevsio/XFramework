using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Responses;

namespace Communications.Api.Services;

public sealed partial class ThreadService
{
    private static List<Guid> EncryptionMemberIds(string json) => JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
    private static bool EncryptionPendingFor(Message message, Guid memberId) => EncryptionMemberIds(message.PendingEncryptionMembersJson).Contains(memberId);
    private static void SetPendingEncryption(Message message, IEnumerable<MessageThreadMember> audience, ICollection<Guid> ready)
    {
        var pending = audience.Where(m => !ready.Contains(m.CredentialId)).Select(m => m.Id).ToList();
        message.PendingEncryptionMembersJson = JsonSerializer.Serialize(pending);
        message.PendingEncryptionCount = pending.Count;
    }

    private async Task<bool> ReadyEncryptionRecipientsCurrentAsync(Guid tenant, Guid sender, Guid? deviceId, long? senderRevision,
        Dictionary<Guid, long> revisions, IReadOnlyCollection<Guid> audience, CancellationToken ct)
    {
        if (deviceId is null || senderRevision is null || audience.Count is < 1 or > 101
            || revisions is null || revisions.Count is < 1 or > 101 || !revisions.ContainsKey(sender) || encryptionDirectoryReader is null) return false;
        var directories = await encryptionDirectoryReader.ReadAsync(tenant, audience.Distinct().ToArray(), ct);
        var ready = directories.Where(x => JsonSerializer.Deserialize<List<EncryptionDevice>>(x.DevicesJson, OutboxJsonOptions)
            ?.Any(d => d.Revocation is null) == true).ToList();
        // Omission is allowed only for accounts without usable keys. Never silently skip a ready member.
        if (!ready.Select(x => x.CredentialId).ToHashSet().SetEquals(revisions.Keys)
            || ready.Any(x => x.Revision != revisions[x.CredentialId])) return false;
        var own = ready.SingleOrDefault(x => x.CredentialId == sender);
        return own is not null && own.Revision == senderRevision &&
            JsonSerializer.Deserialize<List<EncryptionDevice>>(own.DevicesJson, OutboxJsonOptions)!
                .Any(d => d.DeviceId == deviceId && d.Revocation is null);
    }

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
