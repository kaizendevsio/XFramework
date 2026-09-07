using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Auditing;

namespace Audit.Api.Services;

public sealed class AuditQueryService(
    AppDbContext db,
    ITrustedInvocationContextAccessor invocation,
    ITenantModuleFeatureService features)
{
    private async Task<Result<Guid>> AuthorizeAsync(CancellationToken ct)
    {
        var context = invocation.Current;
        if (context?.Actor is not { } actor || context.EffectiveTenantId is not Guid tenant || tenant == Guid.Empty)
            return Result<Guid>.Forbidden("Select an authorized tenant.");
        if (!actor.Capabilities.Contains("audit:view"))
            return Result<Guid>.Forbidden("Audit viewing permission is required.");
        var gate = await features.EnsureEnabledAsync(tenant, "audit", ct: ct);
        return gate.IsSuccess ? Result<Guid>.Success(tenant) : Result<Guid>.Failure(gate.Message!, gate.StatusCode);
    }

    // AuditEvent has no IHasTenantId filter. Every entry point starts from this predicate.
    private IQueryable<AuditEvent> Visible(Guid tenant) => db.Set<AuditEvent>().AsNoTracking()
        .Where(e => e.SubjectTenantIdBefore == tenant || e.SubjectTenantIdAfter == tenant);

    public async Task<Result<AuditPage>> SearchAsync(SearchAuditEventsRequest request, CancellationToken ct)
    {
        var auth = await AuthorizeAsync(ct);
        if (!auth.IsSuccess) return Result<AuditPage>.Failure(auth.Message!, auth.StatusCode);
        var query = Visible(auth.Data);
        foreach (var filter in request.Filters)
            query = AuditGridFilters.Apply(query, filter);
        if (request.AnchorEventId is long anchorId)
        {
            var anchor = await query.FirstOrDefaultAsync(e => e.EventId == anchorId, ct);
            if (anchor is null) return Result<AuditPage>.NotFound("Audit event not found.");
            query = request.RelatedMode switch
            {
                "record" => query.Where(e => e.SchemaName == anchor.SchemaName && e.TableName == anchor.TableName && e.EntityKey == anchor.EntityKey),
                "transaction" => query.Where(e => e.TransactionId == anchor.TransactionId),
                "correlation" when anchor.CorrelationId != null => query.Where(e => e.CorrelationId == anchor.CorrelationId),
                _ => query.Where(e => false)
            };
        }
        query = query.Where(e => e.RecordedAt >= request.From && e.RecordedAt < request.To);
        if (!string.IsNullOrWhiteSpace(request.Schema)) query = query.Where(e => e.SchemaName == request.Schema);
        if (!string.IsNullOrWhiteSpace(request.Table)) query = query.Where(e => e.TableName.Contains(request.Table));
        if (!string.IsNullOrWhiteSpace(request.Service)) query = query.Where(e => e.ServiceName != null && e.ServiceName.Contains(request.Service));
        if (!string.IsNullOrWhiteSpace(request.Action)) query = query.Where(e => e.EventKind == request.Action);
        if (!string.IsNullOrWhiteSpace(request.ActorKind)) query = query.Where(e => e.ActorKind == request.ActorKind);
        if (request.ActorId is Guid actor) query = query.Where(e => e.ActorTenantId == auth.Data && e.ActorCredentialId == actor);
        if (request.CorrelationId is Guid correlation) query = query.Where(e => e.CorrelationId == correlation);
        if (request.TransactionId is long transaction) query = query.Where(e => e.TransactionId == transaction);
        if (!string.IsNullOrWhiteSpace(request.EntityKey)) query = query.Where(e => e.EntityKey == request.EntityKey);
        // Do not expose fields from the other side of a tenant transfer through filtering.
        if (!string.IsNullOrWhiteSpace(request.ChangedField))
            query = query.Where(e => (e.SubjectTenantIdBefore == null || e.SubjectTenantIdBefore == auth.Data)
                && (e.SubjectTenantIdAfter == null || e.SubjectTenantIdAfter == auth.Data)
                && e.ChangedFields.Contains(request.ChangedField));
        var total = await query.CountAsync(ct);
        var ordered = request.OldestFirst ? query.OrderBy(e => e.RecordedAt).ThenBy(e => e.EventId)
            : query.OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.EventId);
        // No old/new payloads are fetched for the list.
        var rows = await ordered.Skip(request.StartIndex).Take(request.Count).Select(e => new AuditSummary
        {
            EventId = e.EventId, RecordedAt = e.RecordedAt, Action = e.EventKind,
            ActorKind = e.ActorKind, ActorId = e.ActorTenantId == auth.Data ? e.ActorCredentialId : null,
            Service = e.ServiceName ?? "Unknown", Schema = e.SchemaName, Table = e.TableName,
            EntityKey = e.EntityKey,
            ChangedFields = (e.SubjectTenantIdBefore != null && e.SubjectTenantIdBefore != auth.Data)
                || (e.SubjectTenantIdAfter != null && e.SubjectTenantIdAfter != auth.Data)
                ? "Restricted" : string.Join(", ", e.ChangedFields)
        }).ToListAsync(ct);
        foreach (var row in rows) row.EntityKey = AuditPresentation.SafeKey(row.EntityKey);
        await ResolveActorNamesAsync(rows, auth.Data, ct);
        return Result<AuditPage>.Success(new AuditPage { Items = rows, TotalItemCount = total });
    }

    public async Task<Result<AuditDetail>> GetAsync(GetAuditEventRequest request, CancellationToken ct)
    {
        var auth = await AuthorizeAsync(ct);
        if (!auth.IsSuccess) return Result<AuditDetail>.Failure(auth.Message!, auth.StatusCode);
        var item = await Visible(auth.Data).FirstOrDefaultAsync(e => e.EventId == request.EventId, ct);
        if (item is null) return Result<AuditDetail>.NotFound("Audit event not found.");
        var detail = AuditPresentation.ToDetail(item, auth.Data);
        await ResolveActorNamesAsync([detail.Summary], auth.Data, ct);
        return Result<AuditDetail>.Success(detail);
    }

    private async Task ResolveActorNamesAsync(List<AuditSummary> rows, Guid tenant, CancellationToken ct)
    {
        var ids = rows.Where(r => r.ActorId.HasValue).Select(r => r.ActorId!.Value).Distinct().ToArray();
        if (ids.Length == 0) return;
        // Deliberate read model: only current usernames for actors already visible in this tenant.
        var names = await db.Set<IdentityCredential>().AsNoTracking()
            .Where(c => c.TenantId == tenant && ids.Contains(c.Id) && !c.IsDeleted)
            .Select(c => new { c.Id, c.UserName }).ToDictionaryAsync(c => c.Id, c => c.UserName, ct);
        foreach (var row in rows)
            if (row.ActorId is Guid id && names.TryGetValue(id, out var name)) row.ActorName = name;
    }
}
