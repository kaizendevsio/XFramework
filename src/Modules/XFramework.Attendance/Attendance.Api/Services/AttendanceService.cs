using System.Text.Json;
using Attendance.Domain.Shared.Contracts;
using Attendance.Domain.Shared.Contracts.Requests;
using Attendance.Domain.Shared.Contracts.Responses;
using Attendance.Domain.Shared.Enums;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;

namespace Attendance.Api.Services;

public sealed class AttendanceService(AppDbContext db, ILogger<AttendanceService> logger)
{
    private const int DefaultGracePeriodMinutes = 5;
    private const int DefaultEarlyCheckoutGraceMinutes = 0;

    public async Task<Result<AttendanceContextResponse>> CreateContextAsync(
        CreateAttendanceContextRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<AttendanceContextResponse>.Failure("Tenant ID is required", 400);

        if (request.DefaultPolicyId.HasValue && !await PolicyExistsAsync(tenantId, request.DefaultPolicyId.Value, ct))
            return Result<AttendanceContextResponse>.NotFound("Attendance policy was not found");

        var context = new AttendanceContext
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Code = NormalizeOptional(request.Code),
            ContextType = request.ContextType,
            Description = NormalizeOptional(request.Description),
            DefaultPolicyId = request.DefaultPolicyId,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        db.Set<AttendanceContext>().Add(context);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Attendance context {ContextId} created for tenant {TenantId}",
            context.Id,
            tenantId);

        return Result<AttendanceContextResponse>.Success(ToContextResponse(context), 201, "Attendance context created");
    }

    public async Task<Result<AttendanceContextResponse>> UpdateContextAsync(
        UpdateAttendanceContextRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<AttendanceContextResponse>.Failure("Tenant ID is required", 400);

        var context = await db.Set<AttendanceContext>()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.ContextId, ct);

        if (context is null)
            return Result<AttendanceContextResponse>.NotFound("Attendance context was not found");

        if (request.DefaultPolicyId.HasValue && !await PolicyExistsAsync(tenantId, request.DefaultPolicyId.Value, ct))
            return Result<AttendanceContextResponse>.NotFound("Attendance policy was not found");

        context.Name = request.Name.Trim();
        context.Code = NormalizeOptional(request.Code);
        context.ContextType = request.ContextType;
        context.Description = NormalizeOptional(request.Description);
        context.DefaultPolicyId = request.DefaultPolicyId;
        context.IsActive = request.IsActive;
        context.ModifiedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<AttendanceContextResponse>.Success(ToContextResponse(context), "Attendance context updated");
    }

    public async Task<Result<GetAttendanceContextsResponse>> GetContextsAsync(
        GetAttendanceContextsRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<GetAttendanceContextsResponse>.Failure("Tenant ID is required", 400);

        var (page, pageSize) = NormalizePage(request.Page, request.PageSize);
        IQueryable<AttendanceContext> query = db.Set<AttendanceContext>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

        if (request.ContextType.HasValue)
            query = query.Where(item => item.ContextType == request.ContextType.Value);

        if (request.IsActive.HasValue)
            query = query.Where(item => item.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim();
            query = query.Where(item =>
                item.Name.Contains(searchTerm) ||
                (item.Code != null && item.Code.Contains(searchTerm)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(item => item.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AttendanceContextResponse
            {
                Id = item.Id,
                TenantId = item.TenantId,
                Name = item.Name,
                Code = item.Code,
                ContextType = item.ContextType,
                Description = item.Description,
                DefaultPolicyId = item.DefaultPolicyId,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(ct);

        return Result<GetAttendanceContextsResponse>.Success(new GetAttendanceContextsResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = GetTotalPages(totalCount, pageSize)
        });
    }

    public async Task<Result<AttendanceParticipantResponse>> AddParticipantAsync(
        AddAttendanceParticipantRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<AttendanceParticipantResponse>.Failure("Tenant ID is required", 400);

        var contextExists = await db.Set<AttendanceContext>()
            .AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == request.ContextId && item.IsActive, ct);

        if (!contextExists)
            return Result<AttendanceParticipantResponse>.NotFound("Attendance context was not found");

        var existing = await db.Set<AttendanceParticipant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.ContextId == request.ContextId &&
                item.CredentialId == request.CredentialId,
                ct);

        if (existing is not null)
            return Result<AttendanceParticipantResponse>.Conflict("Credential is already an attendance participant in this context");

        var participant = new AttendanceParticipant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContextId = request.ContextId,
            CredentialId = request.CredentialId,
            DisplayName = NormalizeOptional(request.DisplayName),
            ReferenceCode = NormalizeOptional(request.ReferenceCode),
            StartedAt = request.StartedAt ?? DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        db.Set<AttendanceParticipant>().Add(participant);
        await db.SaveChangesAsync(ct);

        return Result<AttendanceParticipantResponse>.Success(ToParticipantResponse(participant), 201, "Attendance participant added");
    }

    public async Task<Result> RemoveParticipantAsync(
        RemoveAttendanceParticipantRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result.Failure("Tenant ID is required", 400);

        var participant = await db.Set<AttendanceParticipant>()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.ParticipantId, ct);

        if (participant is null)
            return Result.NotFound("Attendance participant was not found");

        participant.IsActive = false;
        participant.EndedAt = request.EndedAt ?? DateTime.UtcNow;
        participant.ModifiedAt = DateTime.UtcNow;
        db.Set<AttendanceParticipant>().Remove(participant);
        await db.SaveChangesAsync(ct);

        return Result.Success("Attendance participant removed");
    }

    public async Task<Result<GetAttendanceParticipantsResponse>> GetParticipantsAsync(
        GetAttendanceParticipantsRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<GetAttendanceParticipantsResponse>.Failure("Tenant ID is required", 400);

        var (page, pageSize) = NormalizePage(request.Page, request.PageSize);
        IQueryable<AttendanceParticipant> query = db.Set<AttendanceParticipant>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ContextId == request.ContextId);

        if (request.CredentialId.HasValue)
            query = query.Where(item => item.CredentialId == request.CredentialId.Value);

        if (request.IsActive.HasValue)
            query = query.Where(item => item.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim();
            query = query.Where(item =>
                (item.DisplayName != null && item.DisplayName.Contains(searchTerm)) ||
                (item.ReferenceCode != null && item.ReferenceCode.Contains(searchTerm)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(item => item.DisplayName)
            .ThenBy(item => item.ReferenceCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AttendanceParticipantResponse
            {
                Id = item.Id,
                TenantId = item.TenantId,
                ContextId = item.ContextId,
                CredentialId = item.CredentialId,
                DisplayName = item.DisplayName,
                ReferenceCode = item.ReferenceCode,
                StartedAt = item.StartedAt,
                EndedAt = item.EndedAt,
                IsActive = item.IsActive
            })
            .ToListAsync(ct);

        return Result<GetAttendanceParticipantsResponse>.Success(new GetAttendanceParticipantsResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = GetTotalPages(totalCount, pageSize)
        });
    }

    public async Task<Result<AttendanceSessionResponse>> CreateSessionAsync(
        CreateAttendanceSessionRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<AttendanceSessionResponse>.Failure("Tenant ID is required", 400);

        var context = await db.Set<AttendanceContext>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.ContextId && item.IsActive, ct);

        if (context is null)
            return Result<AttendanceSessionResponse>.NotFound("Attendance context was not found");

        if (request.PolicyId.HasValue && !await PolicyExistsAsync(tenantId, request.PolicyId.Value, ct))
            return Result<AttendanceSessionResponse>.NotFound("Attendance policy was not found");

        var session = new AttendanceSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContextId = request.ContextId,
            PolicyId = request.PolicyId ?? context.DefaultPolicyId,
            Name = request.Name.Trim(),
            Code = NormalizeOptional(request.Code),
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            TimeZoneId = request.TimeZoneId.Trim(),
            Status = request.Status,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        db.Set<AttendanceSession>().Add(session);
        await db.SaveChangesAsync(ct);

        return Result<AttendanceSessionResponse>.Success(ToSessionResponse(session), 201, "Attendance session created");
    }

    public async Task<Result<GetAttendanceSessionsResponse>> GetSessionsAsync(
        GetAttendanceSessionsRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<GetAttendanceSessionsResponse>.Failure("Tenant ID is required", 400);

        var (page, pageSize) = NormalizePage(request.Page, request.PageSize);
        IQueryable<AttendanceSession> query = db.Set<AttendanceSession>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ContextId == request.ContextId);

        if (request.FromUtc.HasValue)
            query = query.Where(item => item.StartsAt >= request.FromUtc.Value);

        if (request.ToUtc.HasValue)
            query = query.Where(item => item.StartsAt <= request.ToUtc.Value);

        if (request.Status.HasValue)
            query = query.Where(item => item.Status == request.Status.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(item => item.StartsAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        return Result<GetAttendanceSessionsResponse>.Success(new GetAttendanceSessionsResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = GetTotalPages(totalCount, pageSize)
        });
    }

    public async Task<Result<AttendanceEventResponse>> RecordEventAsync(
        RecordAttendanceEventRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<AttendanceEventResponse>.Failure("Tenant ID is required", 400);

        var existingEvent = await db.Set<AttendanceEvent>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.IdempotencyKey == request.IdempotencyKey, ct);

        if (existingEvent is not null)
        {
            var replayRecord = await GetRecordEntityAsync(tenantId, existingEvent.SessionId, existingEvent.ParticipantId, false, ct);
            return Result<AttendanceEventResponse>.Success(
                ToEventResponse(existingEvent, replayRecord is null ? null : ToRecordResponse(replayRecord, replayRecord.Status)),
                "Attendance event replayed");
        }

        var session = await db.Set<AttendanceSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.SessionId, ct);

        if (session is null)
            return Result<AttendanceEventResponse>.NotFound("Attendance session was not found");

        if (session.Status == AttendanceSessionStatus.Cancelled)
            return Result<AttendanceEventResponse>.Conflict("Cannot record attendance against a cancelled session");

        var participant = await db.Set<AttendanceParticipant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.Id == request.ParticipantId &&
                item.ContextId == session.ContextId &&
                item.IsActive,
                ct);

        if (participant is null)
            return Result<AttendanceEventResponse>.NotFound("Attendance participant was not found for this session context");

        var policy = await GetEffectivePolicyAsync(tenantId, session, ct);
        var occurredAt = request.OccurredAt ?? DateTime.UtcNow;
        var attendanceEvent = new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            ParticipantId = participant.Id,
            CredentialId = participant.CredentialId,
            EventType = request.EventType,
            Source = request.Source,
            OccurredAt = occurredAt,
            RecordedByCredentialId = request.RecordedByCredentialId ?? request.Metadata.CredentialId,
            IdempotencyKey = request.IdempotencyKey.Trim(),
            SourceReference = NormalizeOptional(request.SourceReference),
            Notes = NormalizeOptional(request.Notes),
            MetadataJson = request.Data is null ? null : JsonSerializer.Serialize(request.Data),
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        var recordResult = await ApplyEventToRecordAsync(tenantId, session, participant, policy, attendanceEvent, ct);
        if (!recordResult.IsSuccess)
            return Result<AttendanceEventResponse>.Failure(recordResult.Message ?? "Attendance event rejected", recordResult.StatusCode);

        db.Set<AttendanceEvent>().Add(attendanceEvent);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Attendance event {AttendanceEventId} recorded for credential {CredentialId} in session {SessionId}",
            attendanceEvent.Id,
            participant.CredentialId,
            session.Id);

        return Result<AttendanceEventResponse>.Success(
            ToEventResponse(attendanceEvent, recordResult.Data),
            201,
            "Attendance event recorded");
    }

    public async Task<Result<AttendanceRecordResponse>> GetRecordAsync(
        GetAttendanceRecordRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<AttendanceRecordResponse>.Failure("Tenant ID is required", 400);

        var session = await db.Set<AttendanceSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.SessionId, ct);

        if (session is null)
            return Result<AttendanceRecordResponse>.NotFound("Attendance session was not found");

        var participant = await db.Set<AttendanceParticipant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.Id == request.ParticipantId &&
                item.ContextId == session.ContextId,
                ct);

        if (participant is null)
            return Result<AttendanceRecordResponse>.NotFound("Attendance participant was not found for this session context");

        var record = await GetRecordEntityAsync(tenantId, session.Id, participant.Id, false, ct);
        if (record is null)
        {
            return Result<AttendanceRecordResponse>.Success(new AttendanceRecordResponse
            {
                TenantId = tenantId,
                SessionId = session.Id,
                ParticipantId = participant.Id,
                CredentialId = participant.CredentialId,
                Status = AttendanceRecordStatus.Absent
            });
        }

        var policy = await GetEffectivePolicyAsync(tenantId, session, ct);
        return Result<AttendanceRecordResponse>.Success(ToRecordResponse(record, ResolveRecordStatus(record, session, policy)));
    }

    public async Task<Result<AttendanceReportResponse>> GetReportAsync(
        GetAttendanceReportRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<AttendanceReportResponse>.Failure("Tenant ID is required", 400);

        var contextExists = await db.Set<AttendanceContext>()
            .AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == request.ContextId, ct);

        if (!contextExists)
            return Result<AttendanceReportResponse>.NotFound("Attendance context was not found");

        var (page, pageSize) = NormalizePage(request.Page, request.PageSize);
        var activeParticipantCount = await db.Set<AttendanceParticipant>()
            .AsNoTracking()
            .CountAsync(item => item.TenantId == tenantId && item.ContextId == request.ContextId && item.IsActive, ct);

        IQueryable<AttendanceSession> sessionQuery = db.Set<AttendanceSession>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.ContextId == request.ContextId &&
                item.StartsAt >= request.FromUtc &&
                item.StartsAt <= request.ToUtc);

        var totalSessions = await sessionQuery.CountAsync(ct);
        var sessions = await sessionQuery
            .OrderByDescending(item => item.StartsAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AttendanceSessionReportItemResponse
            {
                SessionId = item.Id,
                SessionName = item.Name,
                StartsAt = item.StartsAt,
                EndsAt = item.EndsAt
            })
            .ToListAsync(ct);

        var sessionIds = sessions.Select(item => item.SessionId).ToList();
        var records = await db.Set<AttendanceRecord>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && sessionIds.Contains(item.SessionId))
            .Select(item => new
            {
                item.SessionId,
                item.ParticipantId,
                item.Status
            })
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            var sessionRecords = records.Where(item => item.SessionId == session.SessionId).ToList();
            session.PresentCount = sessionRecords.Count(item => item.Status == AttendanceRecordStatus.Present);
            session.LateCount = sessionRecords.Count(item => item.Status == AttendanceRecordStatus.Late);
            session.IncompleteCount = sessionRecords.Count(item => item.Status == AttendanceRecordStatus.Incomplete);
            session.ManualAdjustedCount = sessionRecords.Count(item => item.Status == AttendanceRecordStatus.ManualAdjusted);
            session.ExcusedCount = sessionRecords.Count(item => item.Status == AttendanceRecordStatus.Excused);
            session.AbsentCount = Math.Max(
                0,
                activeParticipantCount -
                sessionRecords.Select(item => item.ParticipantId).Distinct().Count() +
                sessionRecords.Count(item => item.Status == AttendanceRecordStatus.Absent));
        }

        return Result<AttendanceReportResponse>.Success(new AttendanceReportResponse
        {
            TenantId = tenantId,
            ContextId = request.ContextId,
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            ActiveParticipantCount = activeParticipantCount,
            Page = page,
            PageSize = pageSize,
            TotalSessions = totalSessions,
            TotalPages = GetTotalPages(totalSessions, pageSize),
            Sessions = sessions
        });
    }

    public async Task<Result<AttendanceAdjustmentResponse>> CreateAdjustmentAsync(
        CreateAttendanceAdjustmentRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<AttendanceAdjustmentResponse>.Failure("Tenant ID is required", 400);

        var session = await db.Set<AttendanceSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.SessionId, ct);

        if (session is null)
            return Result<AttendanceAdjustmentResponse>.NotFound("Attendance session was not found");

        var participant = await db.Set<AttendanceParticipant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.Id == request.ParticipantId &&
                item.ContextId == session.ContextId,
                ct);

        if (participant is null)
            return Result<AttendanceAdjustmentResponse>.NotFound("Attendance participant was not found for this session context");

        var record = await GetRecordEntityAsync(tenantId, session.Id, participant.Id, true, ct);
        if (record is null)
        {
            record = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = session.Id,
                ParticipantId = participant.Id,
                CredentialId = participant.CredentialId,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            };
            db.Set<AttendanceRecord>().Add(record);
        }

        var previousStatus = record.Status == AttendanceRecordStatus.Unknown
            ? AttendanceRecordStatus.Absent
            : record.Status;

        record.FirstCheckInAt = request.AdjustedCheckInAt;
        record.LastCheckOutAt = request.AdjustedCheckOutAt;
        record.Status = request.NewStatus;
        record.IsManual = true;
        record.Notes = NormalizeOptional(request.Notes);
        record.ModifiedAt = DateTime.UtcNow;

        var adjustment = new AttendanceAdjustment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecordId = record.Id,
            SessionId = session.Id,
            ParticipantId = participant.Id,
            CredentialId = participant.CredentialId,
            PreviousStatus = previousStatus,
            NewStatus = request.NewStatus,
            AdjustedCheckInAt = request.AdjustedCheckInAt,
            AdjustedCheckOutAt = request.AdjustedCheckOutAt,
            ActorCredentialId = request.ActorCredentialId,
            Reason = request.Reason.Trim(),
            Notes = NormalizeOptional(request.Notes),
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        db.Set<AttendanceAdjustment>().Add(adjustment);
        await db.SaveChangesAsync(ct);

        return Result<AttendanceAdjustmentResponse>.Success(
            ToAdjustmentResponse(adjustment, ToRecordResponse(record, record.Status)),
            201,
            "Attendance adjustment created");
    }

    private async Task<Result<AttendanceRecordResponse>> ApplyEventToRecordAsync(
        Guid tenantId,
        AttendanceSession session,
        AttendanceParticipant participant,
        AttendancePolicy? policy,
        AttendanceEvent attendanceEvent,
        CancellationToken ct)
    {
        var record = await GetRecordEntityAsync(tenantId, session.Id, participant.Id, true, ct);

        switch (attendanceEvent.EventType)
        {
            case AttendanceEventType.CheckIn:
                if (record is not null && record.FirstCheckInAt.HasValue)
                    return Result<AttendanceRecordResponse>.Conflict("Participant is already checked in for this session");

                var isNewRecord = record is null;
                record ??= CreateRecord(tenantId, session.Id, participant);
                record.FirstCheckInAt = attendanceEvent.OccurredAt;
                record.SourceEventId = attendanceEvent.Id;
                record.Status = ResolveRecordStatus(record, session, policy);
                record.ModifiedAt = DateTime.UtcNow;
                if (!isNewRecord)
                    db.Set<AttendanceRecord>().Update(record);

                return Result<AttendanceRecordResponse>.Success(ToRecordResponse(record, record.Status));

            case AttendanceEventType.CheckOut:
                if (record is null || !record.FirstCheckInAt.HasValue)
                    return Result<AttendanceRecordResponse>.Conflict("Participant must check in before checking out");

                if (record.LastCheckOutAt.HasValue)
                    return Result<AttendanceRecordResponse>.Conflict("Participant is already checked out for this session");

                record.LastCheckOutAt = attendanceEvent.OccurredAt;
                record.SourceEventId = attendanceEvent.Id;
                record.Status = ResolveRecordStatus(record, session, policy);
                record.ModifiedAt = DateTime.UtcNow;
                return Result<AttendanceRecordResponse>.Success(ToRecordResponse(record, record.Status));

            default:
                return Result<AttendanceRecordResponse>.Failure("Use attendance adjustments for manual attendance changes", 400);
        }
    }

    private AttendanceRecord CreateRecord(Guid tenantId, Guid sessionId, AttendanceParticipant participant)
    {
        var record = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = sessionId,
            ParticipantId = participant.Id,
            CredentialId = participant.CredentialId,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        db.Set<AttendanceRecord>().Add(record);
        return record;
    }

    private async Task<AttendanceRecord?> GetRecordEntityAsync(
        Guid tenantId,
        Guid sessionId,
        Guid participantId,
        bool tracking,
        CancellationToken ct)
    {
        IQueryable<AttendanceRecord> query = db.Set<AttendanceRecord>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.SessionId == sessionId &&
                item.ParticipantId == participantId);

        if (tracking)
            query = query.AsTracking();
        else
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(ct);
    }

    private async Task<AttendancePolicy?> GetEffectivePolicyAsync(
        Guid tenantId,
        AttendanceSession session,
        CancellationToken ct)
    {
        if (session.PolicyId.HasValue)
        {
            var sessionPolicy = await db.Set<AttendancePolicy>()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == session.PolicyId.Value, ct);
            if (sessionPolicy is not null)
                return sessionPolicy;
        }

        var defaultPolicyId = await db.Set<AttendanceContext>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Id == session.ContextId)
            .Select(item => item.DefaultPolicyId)
            .FirstOrDefaultAsync(ct);

        if (!defaultPolicyId.HasValue)
            return null;

        return await db.Set<AttendancePolicy>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == defaultPolicyId.Value, ct);
    }

    private async Task<bool> PolicyExistsAsync(Guid tenantId, Guid policyId, CancellationToken ct) =>
        await db.Set<AttendancePolicy>()
            .AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == policyId, ct);

    private static AttendanceRecordStatus ResolveRecordStatus(
        AttendanceRecord record,
        AttendanceSession session,
        AttendancePolicy? policy)
    {
        if (record.IsManual)
            return record.Status;

        if (!record.FirstCheckInAt.HasValue)
            return AttendanceRecordStatus.Absent;

        var graceMinutes = policy?.GracePeriodMinutes ?? DefaultGracePeriodMinutes;
        if (record.FirstCheckInAt.Value > session.StartsAt.AddMinutes(graceMinutes))
            return AttendanceRecordStatus.Late;

        var checkoutRequired = policy?.CheckoutRequired ?? true;
        if (checkoutRequired && !record.LastCheckOutAt.HasValue)
            return AttendanceRecordStatus.Incomplete;

        var earlyCheckoutGrace = policy?.EarlyCheckoutGraceMinutes ?? DefaultEarlyCheckoutGraceMinutes;
        if (checkoutRequired &&
            record.LastCheckOutAt.HasValue &&
            record.LastCheckOutAt.Value < session.EndsAt.AddMinutes(-earlyCheckoutGrace))
            return AttendanceRecordStatus.Incomplete;

        return AttendanceRecordStatus.Present;
    }

    private static bool TryResolveTenantId(Guid? requestTenantId, RequestMetadata metadata, out Guid tenantId)
    {
        tenantId = requestTenantId ?? metadata.TenantId ?? Guid.Empty;
        return tenantId != Guid.Empty;
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
        return (normalizedPage, normalizedPageSize);
    }

    private static int GetTotalPages(int totalCount, int pageSize) =>
        totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    private static AttendanceSessionResponse ToSessionResponse(AttendanceSession session) => new()
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
    };

    private static AttendanceEventResponse ToEventResponse(
        AttendanceEvent attendanceEvent,
        AttendanceRecordResponse? record) => new()
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
        MetadataJson = attendanceEvent.MetadataJson,
        Record = record
    };

    private static AttendanceRecordResponse ToRecordResponse(AttendanceRecord record, AttendanceRecordStatus status) => new()
    {
        Id = record.Id,
        TenantId = record.TenantId,
        SessionId = record.SessionId,
        ParticipantId = record.ParticipantId,
        CredentialId = record.CredentialId,
        FirstCheckInAt = record.FirstCheckInAt,
        LastCheckOutAt = record.LastCheckOutAt,
        Status = status,
        IsManual = record.IsManual,
        SourceEventId = record.SourceEventId,
        Notes = record.Notes
    };

    private static AttendanceAdjustmentResponse ToAdjustmentResponse(
        AttendanceAdjustment adjustment,
        AttendanceRecordResponse record) => new()
    {
        Id = adjustment.Id,
        TenantId = adjustment.TenantId,
        RecordId = adjustment.RecordId,
        SessionId = adjustment.SessionId,
        ParticipantId = adjustment.ParticipantId,
        CredentialId = adjustment.CredentialId,
        PreviousStatus = adjustment.PreviousStatus,
        NewStatus = adjustment.NewStatus,
        AdjustedCheckInAt = adjustment.AdjustedCheckInAt,
        AdjustedCheckOutAt = adjustment.AdjustedCheckOutAt,
        ActorCredentialId = adjustment.ActorCredentialId,
        Reason = adjustment.Reason,
        Notes = adjustment.Notes,
        CreatedAt = adjustment.CreatedAt,
        Record = record
    };
}
