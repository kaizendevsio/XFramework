using Microsoft.EntityFrameworkCore;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace POS.Api.Services;

public sealed class PosScannerService(
    AppDbContext db, IPosRequestContextResolver resolver,
    ITrustedInvocationContextAccessor invocation, ITenantModuleFeatureService features,
    PosScannerPairingStore store, TimeProvider clock)
{
    public async Task<Result<PosScannerPairingResponse>> CreateAsync(CreatePosScannerPairingRequest request, CancellationToken ct)
    {
        var actor = await AuthorizeAsync(request, ct);
        if (!actor.IsSuccess)
            return Result<PosScannerPairingResponse>.Failure(actor.Message!, actor.StatusCode);
        var register = await db.Set<PosRegister>().AsNoTracking()
            .Where(p => p.Id == request.RegisterId && p.TenantId == actor.Data!.TenantId &&
                !p.IsDeleted && p.IsEnabled)
            .Select(p => new { p.Name }).FirstOrDefaultAsync(ct);
        return register is null
            ? Result<PosScannerPairingResponse>.NotFound("Enabled POS register was not found")
            : store.Create(actor.Data!, request.RegisterId, register.Name);
    }

    public async Task<Result<PosScannerPhoneResponse>> ClaimAsync(ClaimPosScannerPairingRequest request, CancellationToken ct)
    {
        var actor = await AuthorizeAsync(request, ct);
        return actor.IsSuccess ? store.Claim(actor.Data!, request.Challenge) :
            Result<PosScannerPhoneResponse>.Failure(actor.Message!, actor.StatusCode);
    }

    public async Task<Result<PosScannerSendResponse>> SendAsync(SendPosScannerCodeRequest request, CancellationToken ct)
    {
        var actor = await AuthorizeAsync(request, ct);
        return actor.IsSuccess ? store.Send(actor.Data!, request.PairingId, request.PhoneKey, request.Sequence, request.Code) :
            Result<PosScannerSendResponse>.Failure(actor.Message!, actor.StatusCode);
    }

    public async Task<Result<PosScannerPollResponse>> PollAsync(PollPosScannerCodesRequest request, CancellationToken ct)
    {
        var actor = await AuthorizeAsync(request, ct);
        return actor.IsSuccess ? store.Poll(actor.Data!, request.PairingId, request.DesktopKey, request.AcknowledgedSequence, request.PauseDelivery) :
            Result<PosScannerPollResponse>.Failure(actor.Message!, actor.StatusCode);
    }

    public async Task<Result<bool>> RevokeAsync(RevokePosScannerPairingRequest request, CancellationToken ct)
    {
        var actor = await AuthorizeAsync(request, ct);
        return actor.IsSuccess ? store.Revoke(actor.Data!, request.PairingId, request.DesktopKey) :
            Result<bool>.Failure(actor.Message!, actor.StatusCode);
    }

    private async Task<Result<ScannerActor>> AuthorizeAsync(RequestBase request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var resolved = resolver.Resolve(request);
        if (!resolved.IsSuccess)
            return Result<ScannerActor>.Failure(resolved.Message!, resolved.StatusCode);
        var actor = invocation.Current?.Actor;
        if (actor is null || actor.CredentialId == Guid.Empty || actor.SessionId == Guid.Empty ||
            actor.ExpiresAtUtc <= clock.GetUtcNow())
            return Result<ScannerActor>.Failure("An active cashier login is required", 401);
        var tenantId = resolved.Data!.TenantId;
        // Match existing POS delegation policy, without changing the cashier credential.
        if ((actor.TenantId != tenantId && !actor.Capabilities.Contains(XFrameworkActorCapabilities.IdentityTenantsManage)) ||
            !actor.Capabilities.Contains(PosAuthorizationCapabilities.SalesView) ||
            !actor.Capabilities.Contains(PosAuthorizationCapabilities.SalesCreate))
            return Result<ScannerActor>.Forbidden("Cashier permission in the authorized tenant is required");
        foreach (var subFeature in new string?[] { null, "registers", "sales" })
        {
            var enabled = await features.EnsureEnabledAsync(tenantId, "pos", subFeature, ct);
            if (!enabled.IsSuccess)
                return Result<ScannerActor>.Failure(enabled.Message!, enabled.StatusCode);
        }
        return Result<ScannerActor>.Success(new(tenantId, actor.CredentialId, actor.SessionId));
    }

    public async Task<Result<PosScannerPhoneResponse>> StatusAsync(GetPosScannerStatusRequest request, CancellationToken ct)
    {
        var actor = await AuthorizeAsync(request, ct);
        return actor.IsSuccess ? store.Status(actor.Data!, request.PairingId, request.PhoneKey) :
            Result<PosScannerPhoneResponse>.Failure(actor.Message!, actor.StatusCode);
    }
}
