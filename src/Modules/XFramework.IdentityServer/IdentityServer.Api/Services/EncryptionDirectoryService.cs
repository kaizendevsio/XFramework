using System.Text.Json;
using Npgsql;

namespace IdentityServer.Api.Services;

/// <summary>Account ownership and persistence boundary. Clients verify all OpenPGP signatures and pinned roots.</summary>
public sealed class EncryptionDirectoryService(DbContext db, ITrustedInvocationContextAccessor trusted)
{
    public async Task<Result<EncryptionDirectoryResponse>> GetEncryptionDirectoryAsync(GetEncryptionDirectoryRequest request, CancellationToken ct = default)
    {
        var actor = Actor();
        if (actor is null) return Result<EncryptionDirectoryResponse>.Failure("An authenticated user is required", 401);
        if (request.CredentialId == Guid.Empty) return Result<EncryptionDirectoryResponse>.Failure("A credential is required", 400);
        // Public directory is same-tenant only; encrypted recovery is never included in this projection.
        var row = await db.Set<EncryptionAccount>().AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId && x.CredentialId == request.CredentialId)
            .Select(x => new { x.TenantId, x.CredentialId, x.DirectoryRevision, x.RootPublicKey, x.Roster, x.DevicesJson })
            .SingleOrDefaultAsync(ct);
        if (row is not null && request.SenderDeviceId is { } device && !ReadDevices(row.DevicesJson).Any(x => x.DeviceId == device))
        {
            // History belongs to the requested same-tenant sender, not necessarily the caller.
            var json = await db.Set<EncryptionAccount>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CredentialId == request.CredentialId)
                .Select(x => x.PublicHistoryJson).SingleOrDefaultAsync(ct);
            var previous = JsonSerializer.Deserialize<List<EncryptionDirectoryResponse>>(json ?? "[]")!
                .SingleOrDefault(x => x.Devices.Any(d => d.DeviceId == device));
            return previous is null ? Result<EncryptionDirectoryResponse>.NotFound("Sending device not found")
                : Result<EncryptionDirectoryResponse>.Success(previous);
        }
        return row is null ? Result<EncryptionDirectoryResponse>.NotFound("Encryption directory not found")
            : Result<EncryptionDirectoryResponse>.Success(new()
            {
                TenantId = row.TenantId, CredentialId = row.CredentialId, Revision = row.DirectoryRevision,
                RootPublicKey = row.RootPublicKey, Roster = row.Roster, Devices = ReadDevices(row.DevicesJson)
            });
    }

    public async Task<Result<EncryptionDirectoryResponse>> PutEncryptionDirectoryAsync(PutEncryptionDirectoryRequest request, CancellationToken ct = default)
    {
        var actor = Actor();
        if (actor is null) return Result<EncryptionDirectoryResponse>.Failure("An authenticated user is required", 401);
        if (!ValidDirectory(request)) return Result<EncryptionDirectoryResponse>.Failure("Invalid public encryption directory", 400);
        var row = await OwnAccount(actor).AsTracking().SingleOrDefaultAsync(ct);
        if ((row?.DirectoryRevision ?? 0) != request.ExpectedRevision)
            return Result<EncryptionDirectoryResponse>.Failure("Encryption directory changed; refresh it before retrying", 409);
        if (row is not null && (row.RootPublicKey != request.RootPublicKey || !PreservesDevices(ReadDevices(row.DevicesJson), request.Devices)))
            return Result<EncryptionDirectoryResponse>.Failure("Existing roots, device keys and revocations cannot be replaced or removed", 409);
        if (row is not null && JsonSerializer.Deserialize<List<EncryptionDirectoryResponse>>(row.PublicHistoryJson)!
            .Any(h => h.Devices.Any(old => request.Devices.Any(d => d.DeviceId == old.DeviceId))))
            return Result<EncryptionDirectoryResponse>.Failure("Devices from a previous identity must enroll with fresh keys", 409);
        if (row is null)
        {
            row = new EncryptionAccount { TenantId = actor.TenantId, CredentialId = actor.CredentialId };
            db.Add(row);
        }
        row.DirectoryRevision = request.ExpectedRevision + 1;
        row.RootPublicKey = request.RootPublicKey;
        row.Roster = request.Roster;
        row.DevicesJson = JsonSerializer.Serialize(request.Devices);
        var saved = await SaveAsync(row, ct);
        return saved ? Result<EncryptionDirectoryResponse>.Success(new()
        {
            TenantId = actor.TenantId, CredentialId = actor.CredentialId, Revision = row.DirectoryRevision,
            RootPublicKey = row.RootPublicKey, Roster = row.Roster, Devices = ReadDevices(row.DevicesJson)
        }) : Result<EncryptionDirectoryResponse>.Failure("Encryption directory changed; refresh it before retrying", 409);
    }

    public async Task<Result<EncryptionDirectoryResponse>> ResetEncryptionIdentityAsync(ResetEncryptionIdentityRequest request, IAuthService auth, CancellationToken ct = default)
    {
        var actor = Actor();
        if (actor is null) return Result<EncryptionDirectoryResponse>.Failure("An authenticated user is required", 401);
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 256 || request.Directory is null || !ValidDirectory(request.Directory)
            || !Armor(request.RecoveryArchive, "MESSAGE", 2097152))
            return Result<EncryptionDirectoryResponse>.Failure("A password, new directory and encrypted backup are required", 400);
        var verified = await auth.VerifyPasswordAsync(new VerifyPasswordRequest
        { CredentialId = actor.CredentialId, Password = request.Password, Metadata = request.Metadata }, ct);
        if (verified.StatusCode == 429) return Result<EncryptionDirectoryResponse>.Failure("Too many attempts. Try again later.", 429);
        if (!verified.IsSuccess || !verified.Data) return Result<EncryptionDirectoryResponse>.Failure("Confirm your password before resetting encryption", 403);
        var row = await OwnAccount(actor).AsTracking().SingleOrDefaultAsync(ct);
        if (row is null) return Result<EncryptionDirectoryResponse>.NotFound("Encryption directory not found");
        var next = request.Directory;
        // A retry after a lost response returns the already committed identity.
        if (row.RootPublicKey == next.RootPublicKey && row.Roster == next.Roster)
            return await GetEncryptionDirectoryAsync(new() { CredentialId = actor.CredentialId }, ct);
        if (row.DirectoryRevision != next.ExpectedRevision || row.RootPublicKey == next.RootPublicKey)
            return Result<EncryptionDirectoryResponse>.Failure("Encryption changed; refresh before resetting", 409);
        var history = JsonSerializer.Deserialize<List<EncryptionDirectoryResponse>>(row.PublicHistoryJson)!;
        if (row.RecoveryRevision == long.MaxValue || history.Any(h => h.RootPublicKey == next.RootPublicKey))
            return Result<EncryptionDirectoryResponse>.Failure("Reset requires a new encryption identity", 409);
        if (next.Devices.Any(d => ReadDevices(row.DevicesJson).Any(old => old.DeviceId == d.DeviceId)
            || history.Any(h => h.Devices.Any(old => old.DeviceId == d.DeviceId))))
            return Result<EncryptionDirectoryResponse>.Failure("Reset requires fresh device identities", 400);
        history.Add(new() { TenantId = actor.TenantId, CredentialId = actor.CredentialId,
            Revision = row.DirectoryRevision, RootPublicKey = row.RootPublicKey, Roster = row.Roster, Devices = ReadDevices(row.DevicesJson) });
        row.PublicHistoryJson = JsonSerializer.Serialize(history);
        row.DirectoryRevision++; row.RecoveryRevision++;
        row.RootPublicKey = next.RootPublicKey; row.Roster = next.Roster; row.DevicesJson = JsonSerializer.Serialize(next.Devices);
        row.RecoveryArchive = request.RecoveryArchive;
        return await SaveAsync(row, ct)
            ? await GetEncryptionDirectoryAsync(new() { CredentialId = actor.CredentialId }, ct)
            : Result<EncryptionDirectoryResponse>.Failure("Encryption changed; refresh before resetting", 409);
    }

    public async Task<Result<EncryptionRecoveryResponse>> GetEncryptionRecoveryAsync(GetEncryptionRecoveryRequest request, CancellationToken ct = default)
    {
        var actor = Actor();
        if (actor is null) return Result<EncryptionRecoveryResponse>.Failure("An authenticated user is required", 401);
        var result = await OwnAccount(actor).AsNoTracking().Select(x => new EncryptionRecoveryResponse
        { Revision = x.RecoveryRevision, Archive = x.RecoveryArchive }).SingleOrDefaultAsync(ct);
        return result is null ? Result<EncryptionRecoveryResponse>.NotFound("Encryption directory not found")
            : Result<EncryptionRecoveryResponse>.Success(result);
    }

    public async Task<Result<EncryptionRecoveryResponse>> PutEncryptionRecoveryAsync(PutEncryptionRecoveryRequest request, CancellationToken ct = default)
    {
        var actor = Actor();
        if (actor is null) return Result<EncryptionRecoveryResponse>.Failure("An authenticated user is required", 401);
        if (request.ExpectedRevision < 0 || request.ExpectedRevision == long.MaxValue || !Armor(request.Archive, "MESSAGE", 2097152))
            return Result<EncryptionRecoveryResponse>.Failure("An encrypted recovery archive is required", 400);
        var row = await OwnAccount(actor).AsTracking().SingleOrDefaultAsync(ct);
        if (row is null) return Result<EncryptionRecoveryResponse>.NotFound("Encryption directory not found");
        if (row.RecoveryRevision != request.ExpectedRevision)
            return Result<EncryptionRecoveryResponse>.Failure("Recovery archive changed; refresh it before retrying", 409);
        if (request.RootPublicKey != row.RootPublicKey && (request.RootPublicKey is not null || row.PublicHistoryJson != "[]"))
            return Result<EncryptionRecoveryResponse>.Failure("Encryption identity changed; refresh before backing up", 409);
        row.RecoveryRevision++;
        row.RecoveryArchive = request.Archive;
        return await SaveAsync(row, ct)
            ? Result<EncryptionRecoveryResponse>.Success(new() { Revision = row.RecoveryRevision, Archive = row.RecoveryArchive })
            : Result<EncryptionRecoveryResponse>.Failure("Recovery archive changed; refresh it before retrying", 409);
    }

    private TrustedActorIdentity? Actor()
    {
        var context = trusted.Current;
        return context?.Actor is { } actor && actor.CredentialId != Guid.Empty && actor.TenantId != Guid.Empty
            && context.EffectiveTenantId == actor.TenantId && actor.ExpiresAtUtc > DateTimeOffset.UtcNow ? actor : null;
    }

    private IQueryable<EncryptionAccount> OwnAccount(TrustedActorIdentity actor) => db.Set<EncryptionAccount>()
        .Where(x => x.TenantId == actor.TenantId && x.CredentialId == actor.CredentialId);

    private async Task<bool> SaveAsync(EncryptionAccount row, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { db.Entry(row).State = EntityState.Detached; return false; }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { db.Entry(row).State = EntityState.Detached; return false; }
    }

    private static List<EncryptionDevice> ReadDevices(string json) => JsonSerializer.Deserialize<List<EncryptionDevice>>(json)!;

    private static bool ValidDirectory(PutEncryptionDirectoryRequest request) =>
        request.ExpectedRevision >= 0 && request.ExpectedRevision < long.MaxValue
        && Armor(request.RootPublicKey, "PUBLIC KEY BLOCK", 16384)
        && Armor(request.Roster, "MESSAGE", 524288)
        && request.Devices is { Count: > 0 and <= 16 }
        && request.Devices.All(x => x is not null && x.DeviceId != Guid.Empty
            && Armor(x.SigningPublicKey, "PUBLIC KEY BLOCK", 16384)
            && Armor(x.EncryptionPublicKey, "PUBLIC KEY BLOCK", 16384)
            && x.SigningPublicKey != x.EncryptionPublicKey
            && x.SigningPublicKey != request.RootPublicKey && x.EncryptionPublicKey != request.RootPublicKey
            && Armor(x.Approval, "MESSAGE", 32768)
            && (x.Revocation is null || Armor(x.Revocation, "MESSAGE", 32768)))
        && request.Devices.Select(x => x.DeviceId).Distinct().Count() == request.Devices.Count
        && Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(request.Devices)) <= 1048576;

    private static bool PreservesDevices(List<EncryptionDevice> old, List<EncryptionDevice> next) => old.All(previous =>
        next.SingleOrDefault(x => x.DeviceId == previous.DeviceId) is { } current
        && previous.SigningPublicKey == current.SigningPublicKey && previous.EncryptionPublicKey == current.EncryptionPublicKey
        && previous.Approval == current.Approval && (previous.Revocation is null || previous.Revocation == current.Revocation));

    // Structural checks only. A syntactically armored value is not a verified signature or proof of encryption.
    private static bool Armor(string? value, string type, int maxBytes) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= maxBytes && Encoding.UTF8.GetByteCount(value) <= maxBytes
        && value.StartsWith($"-----BEGIN PGP {type}-----", StringComparison.Ordinal)
        && value.TrimEnd().EndsWith($"-----END PGP {type}-----", StringComparison.Ordinal)
        && !value.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase);
}
