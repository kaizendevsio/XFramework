using Attendance.Domain.Shared.Contracts;
using Attendance.Domain.Shared.Contracts.Requests;
using Attendance.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Security;

namespace Attendance.Api.Services;

public interface IAttendanceReadService
{
    Task<Result<GetAttendanceContextOverviewResponse>> GetContextOverviewAsync(
        GetAttendanceContextOverviewRequest request,
        CancellationToken ct);

    Task<Result<GetAttendanceSessionReadListResponse>> GetSessionsAsync(
        GetAttendanceSessionReadListRequest request,
        CancellationToken ct);

    Task<Result<AttendanceSessionDetailReadResponse>> GetSessionDetailAsync(
        GetAttendanceSessionDetailReadRequest request,
        CancellationToken ct);

    Task<Result<GetAttendanceParticipantReadListResponse>> GetParticipantsAsync(
        GetAttendanceParticipantReadListRequest request,
        CancellationToken ct);

    Task<Result<AttendanceCredentialHistoryResponse>> GetCredentialHistoryAsync(
        GetAttendanceCredentialHistoryRequest request,
        CancellationToken ct);
}

public sealed class AttendanceReadService(
    AppDbContext db,
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor) : IAttendanceReadService
{
    public async Task<Result<GetAttendanceContextOverviewResponse>> GetContextOverviewAsync(
        GetAttendanceContextOverviewRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, out var tenantId))
            return Result<GetAttendanceContextOverviewResponse>.Failure("Tenant ID is required", 400);

        var limit = NormalizeLimit(request.Limit, 500);
        var items = await db.Set<AttendanceContext>()
            .AsNoTracking()
            .Where(context => context.TenantId == tenantId)
            .OrderBy(context => context.Name)
            .Take(limit)
            .Select(context => new AttendanceContextOverviewResponse
            {
                Id = context.Id,
                TenantId = context.TenantId,
                Name = context.Name,
                Code = context.Code,
                ContextType = context.ContextType,
                Description = context.Description,
                IsActive = context.IsActive,
                ActiveParticipantCount = db.Set<AttendanceParticipant>().Count(participant =>
                    participant.TenantId == tenantId &&
                    participant.ContextId == context.Id &&
                    participant.IsActive),
                SessionCount = db.Set<AttendanceSession>().Count(session =>
                    session.TenantId == tenantId &&
                    session.ContextId == context.Id),
                CreatedAt = context.CreatedAt
            })
            .ToListAsync(ct);

        return Result<GetAttendanceContextOverviewResponse>.Success(new()
        {
            Items = items
        });
    }

    public async Task<Result<GetAttendanceSessionReadListResponse>> GetSessionsAsync(
        GetAttendanceSessionReadListRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, out var tenantId))
            return Result<GetAttendanceSessionReadListResponse>.Failure("Tenant ID is required", 400);

        var fromUtc = NormalizeUtc(request.FromUtc);
        var toUtc = NormalizeUtc(request.ToUtc);
        if (fromUtc > toUtc)
            return Result<GetAttendanceSessionReadListResponse>.Failure("FromUtc must be before or equal to ToUtc", 400);

        IQueryable<AttendanceSession> query = db.Set<AttendanceSession>()
            .AsNoTracking()
            .Where(session =>
                session.TenantId == tenantId &&
                session.StartsAt >= fromUtc &&
                session.StartsAt <= toUtc);

        if (request.ContextId is { } contextId && contextId != Guid.Empty)
            query = query.Where(session => session.ContextId == contextId);

        if (request.Status is { } status)
            query = query.Where(session => session.Status == status);

        var sessions = await query
            .OrderByDescending(session => session.StartsAt)
            .Take(NormalizeLimit(request.Limit, 500))
            .Select(session => new AttendanceSessionResponse
            {
                Id = session.Id,
                TenantId = session.TenantId,
                ContextId = session.ContextId,
                PolicyId = session.PolicyId,
                Name = session.Name,
                Code = session.Code,
                StartsAt = session.StartsAt,
                EndsAt = session.EndsAt,
                TimeZoneId = session.TimeZoneId,
                Status = session.Status
            })
            .ToListAsync(ct);

        var contextIds = sessions.Select(session => session.ContextId).Distinct().ToList();
        var contexts = contextIds.Count == 0
            ? []
            : await db.Set<AttendanceContext>()
                .AsNoTracking()
                .Where(context => context.TenantId == tenantId && contextIds.Contains(context.Id))
                .Select(context => ToContextResponse(context))
                .ToListAsync(ct);

        return Result<GetAttendanceSessionReadListResponse>.Success(new()
        {
            Items = sessions,
            Contexts = contexts
        });
    }

    public async Task<Result<AttendanceSessionDetailReadResponse>> GetSessionDetailAsync(
        GetAttendanceSessionDetailReadRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, out var tenantId))
            return Result<AttendanceSessionDetailReadResponse>.Failure("Tenant ID is required", 400);

        if (request.SessionId == Guid.Empty)
            return Result<AttendanceSessionDetailReadResponse>.Failure("Session ID is required", 400);

        var session = await db.Set<AttendanceSession>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Id == request.SessionId)
            .Select(item => new AttendanceSessionResponse
            {
                Id = item.Id,
                TenantId = item.TenantId,
                ContextId = item.ContextId,
                PolicyId = item.PolicyId,
                Name = item.Name,
                Code = item.Code,
                StartsAt = item.StartsAt,
                EndsAt = item.EndsAt,
                TimeZoneId = item.TimeZoneId,
                Status = item.Status
            })
            .FirstOrDefaultAsync(ct);

        if (session is null)
            return Result<AttendanceSessionDetailReadResponse>.NotFound("Attendance session was not found");

        var context = await db.Set<AttendanceContext>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Id == session.ContextId)
            .Select(item => ToContextResponse(item))
            .FirstOrDefaultAsync(ct);

        var participants = await db.Set<AttendanceParticipant>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.ContextId == session.ContextId &&
                item.StartedAt <= session.StartsAt &&
                (item.EndedAt == null || item.EndedAt >= session.StartsAt))
            .OrderBy(item => item.DisplayName)
            .Take(1000)
            .Select(item => ToParticipantResponse(item))
            .ToListAsync(ct);

        var records = await db.Set<AttendanceRecord>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.SessionId == session.Id)
            .Take(1000)
            .Select(item => ToRecordResponse(item))
            .ToListAsync(ct);

        var events = await db.Set<AttendanceEvent>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.SessionId == session.Id)
            .OrderByDescending(item => item.OccurredAt)
            .Take(100)
            .Select(item => ToEventResponse(item))
            .ToListAsync(ct);

        return Result<AttendanceSessionDetailReadResponse>.Success(new()
        {
            Session = session,
            Context = context,
            Participants = participants,
            Records = records,
            RecentEvents = events
        });
    }

    public async Task<Result<GetAttendanceParticipantReadListResponse>> GetParticipantsAsync(
        GetAttendanceParticipantReadListRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, out var tenantId))
            return Result<GetAttendanceParticipantReadListResponse>.Failure("Tenant ID is required", 400);

        if (request.ContextId == Guid.Empty)
            return Result<GetAttendanceParticipantReadListResponse>.Failure("Context ID is required", 400);

        var participants = await db.Set<AttendanceParticipant>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ContextId == request.ContextId)
            .OrderBy(item => item.DisplayName)
            .Take(NormalizeLimit(request.Limit, 1000))
            .Select(item => ToParticipantResponse(item))
            .ToListAsync(ct);

        return Result<GetAttendanceParticipantReadListResponse>.Success(new()
        {
            Items = participants
        });
    }

    public async Task<Result<AttendanceCredentialHistoryResponse>> GetCredentialHistoryAsync(
        GetAttendanceCredentialHistoryRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, out var tenantId))
            return Result<AttendanceCredentialHistoryResponse>.Failure("Tenant ID is required", 400);

        var credentialIds = request.CredentialIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(100)
            .ToList();
        if (credentialIds.Count == 0)
            return Result<AttendanceCredentialHistoryResponse>.Success(new());

        var participants = await db.Set<AttendanceParticipant>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && credentialIds.Contains(item.CredentialId))
            .OrderByDescending(item => item.StartedAt)
            .Take(500)
            .Select(item => ToParticipantResponse(item))
            .ToListAsync(ct);

        var records = await db.Set<AttendanceRecord>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && credentialIds.Contains(item.CredentialId))
            .OrderByDescending(item => item.CreatedAt)
            .Take(500)
            .Select(item => ToRecordResponse(item))
            .ToListAsync(ct);

        var sessionIds = records.Select(record => record.SessionId).Distinct().ToList();
        var sessions = sessionIds.Count == 0
            ? []
            : await db.Set<AttendanceSession>()
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && sessionIds.Contains(item.Id))
                .Select(item => new AttendanceSessionResponse
                {
                    Id = item.Id,
                    TenantId = item.TenantId,
                    ContextId = item.ContextId,
                    PolicyId = item.PolicyId,
                    Name = item.Name,
                    Code = item.Code,
                    StartsAt = item.StartsAt,
                    EndsAt = item.EndsAt,
                    TimeZoneId = item.TimeZoneId,
                    Status = item.Status
                })
                .ToListAsync(ct);

        var contextIds = participants.Select(item => item.ContextId)
            .Concat(sessions.Select(item => item.ContextId))
            .Distinct()
            .ToList();
        var contexts = contextIds.Count == 0
            ? []
            : await db.Set<AttendanceContext>()
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && contextIds.Contains(item.Id))
                .Select(item => ToContextResponse(item))
                .ToListAsync(ct);

        return Result<AttendanceCredentialHistoryResponse>.Success(new()
        {
            Participants = participants,
            Records = records,
            Sessions = sessions,
            Contexts = contexts
        });
    }

    private bool TryResolveTenantId(Guid? requestTenantId, out Guid tenantId)
    {
        tenantId = trustedInvocationContextAccessor.Current?.EffectiveTenantId ?? Guid.Empty;
        return tenantId != Guid.Empty &&
               (requestTenantId is null || requestTenantId == Guid.Empty || requestTenantId == tenantId);
    }

    private static int NormalizeLimit(int requested, int maximum) =>
        requested <= 0 ? Math.Min(100, maximum) : Math.Min(requested, maximum);

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static AttendanceContextResponse ToContextResponse(AttendanceContext context) => new()
    {
        Id = context.Id,
        TenantId = context.TenantId,
        Name = context.Name,
        Code = context.Code,
        ContextType = context.ContextType,
        Description = context.Description,
        DefaultPolicyId = context.DefaultPolicyId,
        IsActive = context.IsActive,
        CreatedAt = context.CreatedAt
    };

    private static AttendanceParticipantResponse ToParticipantResponse(AttendanceParticipant participant) => new()
    {
        Id = participant.Id,
        TenantId = participant.TenantId,
        ContextId = participant.ContextId,
        CredentialId = participant.CredentialId,
        DisplayName = participant.DisplayName,
        ReferenceCode = participant.ReferenceCode,
        StartedAt = participant.StartedAt,
        EndedAt = participant.EndedAt,
        IsActive = participant.IsActive
    };

    private static AttendanceRecordResponse ToRecordResponse(AttendanceRecord record) => new()
    {
        Id = record.Id,
        TenantId = record.TenantId,
        SessionId = record.SessionId,
        ParticipantId = record.ParticipantId,
        CredentialId = record.CredentialId,
        FirstCheckInAt = record.FirstCheckInAt,
        LastCheckOutAt = record.LastCheckOutAt,
        Status = record.Status,
        IsManual = record.IsManual,
        SourceEventId = record.SourceEventId,
        Notes = record.Notes
    };

    private static AttendanceEventResponse ToEventResponse(AttendanceEvent attendanceEvent) => new()
    {
        Id = attendanceEvent.Id,
        TenantId = attendanceEvent.TenantId,
        SessionId = attendanceEvent.SessionId,
        ParticipantId = attendanceEvent.ParticipantId,
        CredentialId = attendanceEvent.CredentialId,
        EventType = attendanceEvent.EventType,
        Source = attendanceEvent.Source,
        OccurredAt = attendanceEvent.OccurredAt,
        RecordedByCredentialId = attendanceEvent.RecordedByCredentialId,
        IdempotencyKey = attendanceEvent.IdempotencyKey,
        SourceReference = attendanceEvent.SourceReference,
        Notes = attendanceEvent.Notes,
        MetadataJson = attendanceEvent.MetadataJson
    };
}
