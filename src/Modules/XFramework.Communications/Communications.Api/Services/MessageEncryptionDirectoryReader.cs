using IdentityServer.Domain.Shared.Contracts;

namespace Communications.Api.Services;

public sealed record MessageEncryptionDirectory(Guid CredentialId, long Revision, string DevicesJson);
public interface IMessageEncryptionDirectoryReader
{
    Task<List<MessageEncryptionDirectory>> ReadAsync(Guid tenant, IReadOnlyCollection<Guid> credentials, CancellationToken ct);
}

/// <summary>Explicit public Identity read model. Never selects private recovery material or writes Identity records.</summary>
public sealed class MessageEncryptionDirectoryReader(DbContext db) : IMessageEncryptionDirectoryReader
{
    public Task<List<MessageEncryptionDirectory>> ReadAsync(Guid tenant, IReadOnlyCollection<Guid> credentials, CancellationToken ct)
    {
        if (credentials.Count is < 1 or > 101) throw new ArgumentOutOfRangeException(nameof(credentials));
        return db.Set<EncryptionAccount>().AsNoTracking().Where(x => x.TenantId == tenant && credentials.Contains(x.CredentialId)
                && db.Set<IdentityCredential>().Any(c => c.Id == x.CredentialId && c.TenantId == tenant && c.IsEnabled && !c.IsDeleted))
            .Select(x => new MessageEncryptionDirectory(x.CredentialId, x.DirectoryRevision, x.DevicesJson)).ToListAsync(ct);
    }
}
