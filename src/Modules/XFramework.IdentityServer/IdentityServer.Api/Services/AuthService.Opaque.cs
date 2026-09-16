using System.Security.Cryptography;
using IdentityServer.Api.Infrastructure;
using IdentityServer.Api.Features.Auth.Register;
using IdentityServer.Domain.Shared.Contracts.Responses;

namespace IdentityServer.Api.Services;

public sealed partial class AuthService
{
    public async Task<Result<OpaqueAuthResponse>> OpaqueAsync(OpaqueAuthRequest request,
        OpaqueExchanges exchanges, OpaqueSetup setup, RegistrationService registration, CancellationToken ct)
    {
        if (!(await new Features.Auth.Opaque.OpaqueAuthValidator().ValidateAsync(request, ct)).IsValid)
            return Result<OpaqueAuthResponse>.Failure("Invalid authentication exchange.", 400);
        var invocation = _trustedInvocationContextAccessor.Current;
        if (CurrentTenantId is not { } tenantId || tenantId == Guid.Empty || invocation?.Service?.ClientId != "XFramework.Yap")
            return Result<OpaqueAuthResponse>.Forbidden("Authentication is unavailable for this application.");
        if (request.Stage == "status")
        {
            if (invocation.Actor is not { } actor) return OpaqueDenied();
            var owner = await _dbContext.Set<IdentityCredential>().IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == actor.CredentialId && x.IsEnabled && !x.IsDeleted, ct);
            if (owner is null) return OpaqueDenied();
            var enabled = await OpaqueCredentialQuery(tenantId, owner.Id).AnyAsync(ct);
            return Result<OpaqueAuthResponse>.Success(new() { Mode = enabled ? "opaque" : "legacy", UserName = owner.UserName,
                Client = $"{tenantId:D}:{owner.Id:D}" });
        }
        var login = new AuthenticateIdentityRequest { UserName = request.UserName, RoleId = request.RoleId,
            AuthorizationType = AuthorizationType.Username, Metadata = request.Metadata };
        if (request.Stage is "options" or "login-start" or "register-start" or "enroll-start" or "change-start")
        {
            var limit = await AcquireAuthenticationRateLimitAsync(login, ct);
            if (!limit.IsAllowed) return Result<OpaqueAuthResponse>.Failure("Too many requests.", 429);
        }
        if (request.Stage.EndsWith("-start", StringComparison.Ordinal))
        {
            // Bound anonymous state allocation even when callers vary the username.
            var limit = await AcquireSecurityRateLimitAsync(new("opaque-start", 120, TimeSpan.FromMinutes(1)),
                request.Metadata, "opaque-exchanges", "OPAQUE exchange", ct);
            if (!limit.IsAllowed) return Result<OpaqueAuthResponse>.Failure("Too many requests.", 429);
        }
        try
        {
            var tenant = await _tenantService.GetTenant(tenantId, ct);
            var credential = await ValidateAuthorization(login, tenant, AuthorizationType.Username, ct);
            var record = credential is null ? null : await OpaqueCredentialQuery(tenantId, credential.Id).SingleOrDefaultAsync(ct);
            var client = $"{tenantId:D}:{credential?.Id ?? Guid.Empty:D}";
            if (request.Stage == "options")
                return Result<OpaqueAuthResponse>.Success(new() { Mode = credential is not null && record is null ? "legacy" : "opaque", Client = client });

            if (request.Stage == "register-start")
            {
                if (credential is not null) return Result<OpaqueAuthResponse>.Conflict("That username is already taken.");
                if (string.IsNullOrWhiteSpace(request.DisplayName)) return Result<OpaqueAuthResponse>.Failure("A display name is required.", 400);
                var newId = Guid.NewGuid(); client = $"{tenantId:D}:{newId:D}";
                var result = OpaqueNative.Execute(new { operation = "register", setup = setup.Value, client, request = request.Message });
                var id = exchanges.Add(new(tenantId, request.RoleId, request.UserName, client, newId, Guid.Empty,
                    Guid.Empty, "register", "", default, DisplayName: request.DisplayName));
                return Result<OpaqueAuthResponse>.Success(new() { ExchangeId = id, Client = client, Message = result.GetProperty("response").GetString() });
            }

            if (request.Stage == "login-start")
            {
                var result = OpaqueNative.Execute(new { operation = "start", setup = setup.Value,
                    record = record?.Record, client, server = OpaqueSetup.ServerIdentity, request = request.Message });
                var id = exchanges.Add(new(tenantId, request.RoleId, request.UserName, client, credential?.Id ?? Guid.Empty,
                    record?.Epoch ?? Guid.Empty, credential?.ConcurrencyStamp ?? Guid.Empty, "login",
                    result.GetProperty("state").GetString()!, default));
                return Result<OpaqueAuthResponse>.Success(new() { ExchangeId = id, Client = client, Message = result.GetProperty("response").GetString() });
            }

            if (request.Stage == "login-finish")
            {
                var exchange = exchanges.Take(request.ExchangeId, tenantId, request.RoleId);
                if (exchange is null || exchange.Kind != "login" || exchange.UserName != request.UserName || record is null ||
                    exchange.CredentialId != credential?.Id || exchange.Epoch != record.Epoch) return OpaqueDenied();
                try { OpaqueNative.Execute(new { operation = "finish", state = exchange.State,
                    client = exchange.Client, server = OpaqueSetup.ServerIdentity, request = request.Message }); }
                catch (CryptographicException)
                {
                    // Preserve the ordinary account lockout policy for submitted invalid proofs.
                    await AuthenticateCoreAsync(login, new(exchange.CredentialId, Guid.Empty), ct);
                    return OpaqueDenied();
                }
                var authenticated = await AuthenticateCoreAsync(login, new(exchange.CredentialId, exchange.Epoch), ct);
                if (!authenticated.IsSuccess) return Result<OpaqueAuthResponse>.Failure(authenticated.Message!, authenticated.StatusCode);
                // This short-lived proof permits one password change without trusting an old browser cookie.
                var grant = exchanges.Add(exchange with { Kind = "reauth", State = "", CredentialStamp = credential!.ConcurrencyStamp });
                return Result<OpaqueAuthResponse>.Success(new() { Client = client, Authentication = authenticated.Data,
                    WrappedRecovery = record.WrappedRecovery, ExchangeId = grant });
            }

            if (request.Stage is "enroll-start" or "change-start")
            {
                if (credential is null || invocation.Actor?.CredentialId != credential.Id) return OpaqueDenied();
                if (request.Stage == "enroll-start")
                {
                    if (record is not null || string.IsNullOrEmpty(request.LegacyPassword)) return OpaqueDenied();
                    login.Password = request.LegacyPassword; login.GenerateToken = false;
                    var confirmed = await AuthenticateAsync(login, ct);
                    if (!confirmed.IsSuccess) return Result<OpaqueAuthResponse>.Failure(confirmed.Message!, confirmed.StatusCode);
                }
                else
                {
                    var grant = exchanges.Take(request.ExchangeId, tenantId, request.RoleId);
                    if (grant is null || grant.Kind != "reauth" || grant.CredentialId != credential.Id || grant.Epoch != record?.Epoch)
                        return OpaqueDenied();
                }
                // Recovery must exist before the account's legacy verifier can be retired.
                var backup = await _dbContext.Set<EncryptionAccount>().IgnoreQueryFilters().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CredentialId == credential.Id, ct);
                if (string.IsNullOrEmpty(backup?.RecoveryArchive))
                    return Result<OpaqueAuthResponse>.Conflict("Restore or set up encrypted messages before enabling password recovery.");
                var result = OpaqueNative.Execute(new { operation = "register", setup = setup.Value, client, request = request.Message });
                var current = await _dbContext.Set<IdentityCredential>().IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == credential.Id && x.TenantId == tenantId, ct);
                var id = exchanges.Add(new(tenantId, request.RoleId, request.UserName, client, credential.Id,
                    record?.Epoch ?? Guid.Empty, current.ConcurrencyStamp, "enroll", "", default, Root: backup.RootPublicKey, BackupRevision: backup.RecoveryRevision));
                return Result<OpaqueAuthResponse>.Success(new() { ExchangeId = id, Client = client, Message = result.GetProperty("response").GetString() });
            }

            if (request.Stage == "enroll-verify")
            {
                var exchange = exchanges.Take(request.ExchangeId, tenantId, request.RoleId);
                if (exchange is null || exchange.Kind is not ("enroll" or "register") || exchange.UserName != request.UserName ||
                    (exchange.Kind == "enroll" && invocation.Actor?.CredentialId != exchange.CredentialId) || string.IsNullOrEmpty(request.Record) ||
                    !ValidWrappedRecovery(request.WrappedRecovery)) return OpaqueDenied();
                if (exchange.Kind == "register" && (request.Directory is null || request.Directory.ExpectedRevision != 0 ||
                    !EncryptionDirectoryService.ValidDirectory(request.Directory) || request.RecoveryArchive is null ||
                    !request.RecoveryArchive.StartsWith("-----BEGIN PGP MESSAGE-----", StringComparison.Ordinal)))
                    return Result<OpaqueAuthResponse>.Failure("An encrypted recovery archive is required.", 400);
                OpaqueNative.Execute(new { operation = "validate", record = request.Record });
                var result = OpaqueNative.Execute(new { operation = "start", setup = setup.Value, record = request.Record,
                    client = exchange.Client, server = OpaqueSetup.ServerIdentity, request = request.Message });
                var id = exchanges.Add(exchange with { Kind = exchange.Kind == "register" ? "register-finish" : "enroll-finish", State = result.GetProperty("state").GetString()!,
                    Record = request.Record, WrappedRecovery = request.WrappedRecovery,
                    Directory = request.Directory, Archive = request.RecoveryArchive is null ? null : Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.RecoveryArchive))) });
                return Result<OpaqueAuthResponse>.Success(new() { ExchangeId = id, Client = exchange.Client, Message = result.GetProperty("response").GetString() });
            }

            if (request.Stage == "enroll-finish")
            {
                var exchange = exchanges.Take(request.ExchangeId, tenantId, request.RoleId);
                if (exchange is null || exchange.Kind is not ("enroll-finish" or "register-finish") || exchange.UserName != request.UserName ||
                    (exchange.Kind == "enroll-finish" && invocation.Actor?.CredentialId != exchange.CredentialId)) return OpaqueDenied();
                OpaqueNative.Execute(new { operation = "finish", state = exchange.State, client = exchange.Client,
                    server = OpaqueSetup.ServerIdentity, request = request.Message });
                if (exchange.Kind == "register-finish")
                {
                    if (request.RecoveryArchive is null || exchange.Archive != Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.RecoveryArchive))))
                        return OpaqueDenied();
                    var directory = exchange.Directory!;
                    var created = await registration.RegisterOpaqueAsync(new() { UserName = request.UserName,
                        DisplayName = exchange.DisplayName!, Password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), Metadata = request.Metadata },
                        new() { TenantId = tenantId, CredentialId = exchange.CredentialId, Epoch = Guid.NewGuid(),
                            Record = exchange.Record!, WrappedRecovery = exchange.WrappedRecovery!, UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime },
                        new() { TenantId = tenantId, CredentialId = exchange.CredentialId, DirectoryRevision = 1,
                            RootPublicKey = directory.RootPublicKey, Roster = directory.Roster,
                            DevicesJson = System.Text.Json.JsonSerializer.Serialize(directory.Devices), RecoveryRevision = 1, RecoveryArchive = request.RecoveryArchive }, ct);
                    return created.IsSuccess ? Result<OpaqueAuthResponse>.Success(new() { Client = exchange.Client })
                        : Result<OpaqueAuthResponse>.Failure(created.Message!, created.StatusCode);
                }
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
                var current = await LockCredentialForAuthenticationAsync(exchange.CredentialId, ct);
                if (current is null || !current.IsEnabled || current.IsDeleted || current.TenantId != tenantId ||
                    current.ConcurrencyStamp != exchange.CredentialStamp) return OpaqueDenied();
                var existing = await OpaqueCredentialQuery(tenantId, exchange.CredentialId).AsTracking().SingleOrDefaultAsync(ct);
                if ((existing?.Epoch ?? Guid.Empty) != exchange.Epoch) return OpaqueDenied();
                var backup = await _dbContext.Set<EncryptionAccount>().IgnoreQueryFilters().AsTracking()
                    .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CredentialId == exchange.CredentialId, ct);
                if (backup is null || backup.RootPublicKey != exchange.Root || backup.RecoveryRevision != exchange.BackupRevision)
                    return Result<OpaqueAuthResponse>.Conflict("Encrypted backup changed. Try again.");
                // Include its concurrency token in this commit, preventing a simultaneous identity reset.
                backup.RecoveryRevision++;
                if (existing is null)
                {
                    existing = new OpaqueCredential { TenantId = tenantId, CredentialId = exchange.CredentialId };
                    _dbContext.Add(existing);
                }
                existing.Epoch = Guid.NewGuid(); existing.Record = exchange.Record!;
                existing.WrappedRecovery = exchange.WrappedRecovery!; existing.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                current.PasswordByte = null;
                current.ConcurrencyStamp = Guid.NewGuid();
                if (exchange.Epoch != Guid.Empty) await RevokeActiveSessionsAsync(tenantId, current.Id, ct);
                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Result<OpaqueAuthResponse>.Success(new() { Client = exchange.Client });
            }
            return Result<OpaqueAuthResponse>.Failure("Unsupported authentication exchange.", 400);
        }
        catch (CryptographicException) { return OpaqueDenied(); }
        catch (OpaqueExchanges.CapacityException) { return Result<OpaqueAuthResponse>.Failure("Too many attempts. Try again shortly.", 429); }
        catch (DbUpdateConcurrencyException) { return Result<OpaqueAuthResponse>.Conflict("Account changed. Sign in again."); }
    }

    private IQueryable<OpaqueCredential> OpaqueCredentialQuery(Guid tenant, Guid credential) =>
        _dbContext.Set<OpaqueCredential>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenant && x.CredentialId == credential);
    private static Result<OpaqueAuthResponse> OpaqueDenied() => Result<OpaqueAuthResponse>.Failure("Invalid credentials or expired exchange.", 401);
    internal static bool ValidWrappedRecovery(string? value)
    {
        if (value is null || value.Length is < 40 or > 4096) return false;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(value);
            var root = document.RootElement;
            return root.GetProperty("v").GetInt32() == 1 &&
                Convert.FromBase64String(root.GetProperty("iv").GetString()!).Length == 12 &&
                Convert.FromBase64String(root.GetProperty("ciphertext").GetString()!).Length is >= 32 and <= 1024;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException or FormatException or InvalidOperationException or ArgumentException) { return false; }
    }
}
