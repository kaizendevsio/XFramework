using System.Text.Json;
using Attendance.Domain.Shared.Contracts;
using Attendance.Domain.Shared.Contracts.Requests;
using Attendance.Domain.Shared.Contracts.Responses;
using Attendance.Domain.Shared.Enums;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Attendance.Api.Services;

public sealed class AttendanceService(
    AppDbContext db,
    ILogger<AttendanceService> logger,
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor,
    IAttendanceCredentialResolver credentialResolver)
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

        if (request.StartedAt.HasValue && !IsUtc(request.StartedAt.Value))
            return Result<AttendanceParticipantResponse>.Failure("Participant start time must be UTC", 400);

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

        var credentialResult = await credentialResolver.ResolveAsync(request.CredentialId, tenantId, ct);
        if (!credentialResult.IsSuccess)
            return Result<AttendanceParticipantResponse>.Failure(
                credentialResult.Message ?? "Identity credential could not be validated",
                credentialResult.StatusCode);

        var credential = credentialResult.Data!;
        if (credential.CredentialId != request.CredentialId || credential.TenantId != tenantId)
            return Result<AttendanceParticipantResponse>.NotFound("Identity credential was not found for this tenant");

        if (!credential.IsEnabled || credential.IsDeleted)
            return Result<AttendanceParticipantResponse>.Conflict("Identity credential is not active");

        var participant = new AttendanceParticipant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContextId = request.ContextId,
            CredentialId = request.CredentialId,
            DisplayName = NormalizeOptional(credential.UserAlias) ?? NormalizeOptional(credential.UserName),
            ReferenceCode = NormalizeOptional(credential.UserName),
            StartedAt = NormalizeUtcPrecision(request.StartedAt ?? DateTime.UtcNow),
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

        if (!participant.IsActive)
            return Result.Conflict("Attendance participant is already inactive");

        if (request.EndedAt.HasValue && !IsUtc(request.EndedAt.Value))
            return Result.Failure("Participant end time must be UTC", 400);

        var endedAt = NormalizeUtcPrecision(request.EndedAt ?? DateTime.UtcNow);
        if (endedAt < participant.StartedAt)
            return Result.Failure("Participant end time cannot be before the start time", 400);

        participant.IsActive = false;
        participant.EndedAt = endedAt;
        participant.ModifiedAt = DateTime.UtcNow;
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

        if (!IsUtc(request.StartsAt) || !IsUtc(request.EndsAt))
            return Result<AttendanceSessionResponse>.Failure("Attendance session start and end times must be UTC", 400);

        var startsAt = NormalizeUtcPrecision(request.StartsAt);
        var endsAt = NormalizeUtcPrecision(request.EndsAt);
        if (endsAt <= startsAt)
            return Result<AttendanceSessionResponse>.Failure("Attendance session end must be after start", 400);

        var timeZoneId = NormalizeOptional(request.TimeZoneId);
        if (timeZoneId is null || !IsValidTimeZone(timeZoneId))
            return Result<AttendanceSessionResponse>.Failure("Attendance session time zone is invalid", 400);

        if (request.Status is AttendanceSessionStatus.Closed or AttendanceSessionStatus.Cancelled)
            return Result<AttendanceSessionResponse>.Failure("New attendance sessions must be scheduled or open", 400);

        var context = await db.Set<AttendanceContext>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.ContextId && item.IsActive, ct);

        if (context is null)
        {
            logger.LogWarning(
                "Attendance context {ContextId} was not found for tenant {TenantId} while creating session {SessionName}.",
                request.ContextId,
                tenantId,
                request.Name);
            return Result<AttendanceSessionResponse>.NotFound("Attendance context was not found");
        }

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
            StartsAt = startsAt,
            EndsAt = endsAt,
            TimeZoneId = timeZoneId,
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

    public async Task<Result<AttendanceSessionResponse>> TransitionSessionAsync(
        TransitionAttendanceSessionRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<AttendanceSessionResponse>.Failure("Tenant ID is required", 400);

        var session = await db.Set<AttendanceSession>()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.SessionId, ct);

        if (session is null)
            return Result<AttendanceSessionResponse>.NotFound("Attendance session was not found");

        if (session.Status == request.Status)
            return Result<AttendanceSessionResponse>.Success(ToSessionResponse(session), "Attendance session status unchanged");

        var transitionAllowed = session.Status switch
        {
            AttendanceSessionStatus.Scheduled => request.Status is AttendanceSessionStatus.Open or AttendanceSessionStatus.Cancelled,
            AttendanceSessionStatus.Open => request.Status is AttendanceSessionStatus.Closed or AttendanceSessionStatus.Cancelled,
            _ => false
        };

        if (!transitionAllowed)
        {
            return Result<AttendanceSessionResponse>.Conflict(
                $"Attendance session cannot transition from {session.Status} to {request.Status}");
        }

        session.Status = request.Status;
        session.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result<AttendanceSessionResponse>.Success(
            ToSessionResponse(session),
            "Attendance session status updated");
    }

    public async Task<Result<AttendanceEventResponse>> RecordEventAsync(
        RecordAttendanceEventRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<AttendanceEventResponse>.Failure("Tenant ID is required", 400);

        var actorCredentialId = trustedInvocationContextAccessor.Current?.Actor?.CredentialId;
        if (!actorCredentialId.HasValue || actorCredentialId == Guid.Empty)
            return Result<AttendanceEventResponse>.Failure("Authenticated actor credential is required", 401);

        if (request.RecordedByCredentialId is { } suppliedActorCredentialId &&
            suppliedActorCredentialId != Guid.Empty &&
            suppliedActorCredentialId != actorCredentialId.Value)
        {
            return Result<AttendanceEventResponse>.Failure(
                "Recorded-by credential must match the authenticated actor",
                403);
        }

        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        if (idempotencyKey is null)
            return Result<AttendanceEventResponse>.Failure("Idempotency key is required", 400);

        if (request.OccurredAt.HasValue && !IsUtc(request.OccurredAt.Value))
            return Result<AttendanceEventResponse>.Failure("Attendance event time must be UTC", 400);

        var requestedOccurredAt = request.OccurredAt.HasValue
            ? NormalizeUtcPrecision(request.OccurredAt.Value)
            : (DateTime?)null;
        var sourceReference = NormalizeOptional(request.SourceReference);
        var notes = NormalizeOptional(request.Notes);
        var metadataJson = SerializeMetadata(request.Data);

        var existingEvent = await db.Set<AttendanceEvent>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.IdempotencyKey == idempotencyKey, ct);

        if (existingEvent is not null)
            return await ResolveEventReplayAsync(
                tenantId,
                existingEvent,
                request,
                requestedOccurredAt,
                actorCredentialId.Value,
                sourceReference,
                notes,
                metadataJson,
                ct);

        var session = await db.Set<AttendanceSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.SessionId, ct);

        if (session is null)
            return Result<AttendanceEventResponse>.NotFound("Attendance session was not found");

        if (session.Status != AttendanceSessionStatus.Open)
            return Result<AttendanceEventResponse>.Conflict("Attendance events can only be recorded for open sessions");

        var occurredAt = requestedOccurredAt ?? NormalizeUtcPrecision(DateTime.UtcNow);
        if (occurredAt > DateTime.UtcNow)
            return Result<AttendanceEventResponse>.Failure("Attendance event time cannot be in the future", 400);

        var sessionStartsAt = NormalizeUtcPrecision(session.StartsAt);
        var sessionEndsAt = NormalizeUtcPrecision(session.EndsAt);
        if (occurredAt < sessionStartsAt || occurredAt > sessionEndsAt)
            return Result<AttendanceEventResponse>.Conflict("Attendance event time must be within the session window");

        var participant = await db.Set<AttendanceParticipant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.Id == request.ParticipantId &&
                item.ContextId == session.ContextId &&
                item.StartedAt <= session.StartsAt &&
                (!item.EndedAt.HasValue || item.EndedAt.Value > session.StartsAt),
                ct);

        if (participant is null)
            return Result<AttendanceEventResponse>.NotFound("Attendance participant was not found for this session context");

        var policy = await GetEffectivePolicyAsync(tenantId, session, ct);
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
            RecordedByCredentialId = actorCredentialId.Value,
            IdempotencyKey = idempotencyKey,
            SourceReference = sourceReference,
            Notes = notes,
            MetadataJson = metadataJson,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        var recordResult = await ApplyEventToRecordAsync(tenantId, session, participant, policy, attendanceEvent, ct);
        if (!recordResult.IsSuccess)
            return Result<AttendanceEventResponse>.Failure(recordResult.Message ?? "Attendance event rejected", recordResult.StatusCode);

        db.Set<AttendanceEvent>().Add(attendanceEvent);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var concurrentEvent = await db.Set<AttendanceEvent>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.TenantId == tenantId && item.IdempotencyKey == idempotencyKey,
                    ct);

            if (concurrentEvent is null)
                throw;

            return await ResolveEventReplayAsync(
                tenantId,
                concurrentEvent,
                request,
                requestedOccurredAt,
                actorCredentialId.Value,
                sourceReference,
                notes,
                metadataJson,
                ct);
        }

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

        if (!IsUtc(request.FromUtc) || !IsUtc(request.ToUtc))
            return Result<AttendanceReportResponse>.Failure("Attendance report range must be UTC", 400);

        var fromUtc = NormalizeUtcPrecision(request.FromUtc);
        var toUtc = NormalizeUtcPrecision(request.ToUtc);
        if (toUtc <= fromUtc)
            return Result<AttendanceReportResponse>.Failure("Attendance report end must be after start", 400);

        var contextExists = await db.Set<AttendanceContext>()
            .AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == request.ContextId, ct);

        if (!contextExists)
            return Result<AttendanceReportResponse>.NotFound("Attendance context was not found");

        var (page, pageSize) = NormalizePage(request.Page, request.PageSize);
        var participants = await db.Set<AttendanceParticipant>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.ContextId == request.ContextId &&
                item.StartedAt <= toUtc)
            .Select(item => new
            {
                item.Id,
                item.StartedAt,
                item.EndedAt
            })
            .ToListAsync(ct);

        var activeParticipantCount = participants.Count(item =>
            item.StartedAt <= toUtc &&
            (!item.EndedAt.HasValue || item.EndedAt.Value > toUtc));

        IQueryable<AttendanceSession> sessionQuery = db.Set<AttendanceSession>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.ContextId == request.ContextId &&
                item.StartsAt >= fromUtc &&
                item.StartsAt <= toUtc);

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
            var sessionParticipantIds = participants
                .Where(item =>
                    item.StartedAt <= session.StartsAt &&
                    (!item.EndedAt.HasValue || item.EndedAt.Value > session.StartsAt))
                .Select(item => item.Id)
                .ToHashSet();
            var sessionRecords = records
                .Where(item =>
                    item.SessionId == session.SessionId &&
                    sessionParticipantIds.Contains(item.ParticipantId))
                .ToList();
            session.PresentCount = sessionRecords.Count(item => item.Status == AttendanceRecordStatus.Present);
            session.LateCount = sessionRecords.Count(item => item.Status == AttendanceRecordStatus.Late);
            session.IncompleteCount = sessionRecords.Count(item => item.Status == AttendanceRecordStatus.Incomplete);
            session.ManualAdjustedCount = sessionRecords.Count(item => item.Status == AttendanceRecordStatus.ManualAdjusted);
            session.ExcusedCount = sessionRecords.Count(item => item.Status == AttendanceRecordStatus.Excused);
            session.AbsentCount = Math.Max(
                0,
                sessionParticipantIds.Count -
                sessionRecords.Select(item => item.ParticipantId).Distinct().Count() +
                sessionRecords.Count(item => item.Status == AttendanceRecordStatus.Absent));
        }

        return Result<AttendanceReportResponse>.Success(new AttendanceReportResponse
        {
            TenantId = tenantId,
            ContextId = request.ContextId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
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

        var actorCredentialId = trustedInvocationContextAccessor.Current?.Actor?.CredentialId;
        if (!actorCredentialId.HasValue || actorCredentialId == Guid.Empty)
            return Result<AttendanceAdjustmentResponse>.Failure("Authenticated actor credential is required", 401);

        if (request.ActorCredentialId != Guid.Empty && request.ActorCredentialId != actorCredentialId.Value)
        {
            return Result<AttendanceAdjustmentResponse>.Failure(
                "Adjustment actor credential must match the authenticated actor",
                403);
        }

        if (request.AdjustedCheckInAt.HasValue && !IsUtc(request.AdjustedCheckInAt.Value) ||
            request.AdjustedCheckOutAt.HasValue && !IsUtc(request.AdjustedCheckOutAt.Value))
        {
            return Result<AttendanceAdjustmentResponse>.Failure("Adjusted attendance times must be UTC", 400);
        }

        var adjustedCheckInAt = request.AdjustedCheckInAt.HasValue
            ? NormalizeUtcPrecision(request.AdjustedCheckInAt.Value)
            : (DateTime?)null;
        var adjustedCheckOutAt = request.AdjustedCheckOutAt.HasValue
            ? NormalizeUtcPrecision(request.AdjustedCheckOutAt.Value)
            : (DateTime?)null;

        if (adjustedCheckOutAt.HasValue && !adjustedCheckInAt.HasValue)
            return Result<AttendanceAdjustmentResponse>.Failure("Adjusted checkout requires an adjusted check-in", 400);

        if (adjustedCheckInAt.HasValue &&
            adjustedCheckOutAt.HasValue &&
            adjustedCheckOutAt.Value < adjustedCheckInAt.Value)
            return Result<AttendanceAdjustmentResponse>.Failure("Adjusted checkout cannot be before adjusted check-in", 400);

        var session = await db.Set<AttendanceSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.SessionId, ct);

        if (session is null)
            return Result<AttendanceAdjustmentResponse>.NotFound("Attendance session was not found");

        if (session.Status is AttendanceSessionStatus.Scheduled or AttendanceSessionStatus.Cancelled)
            return Result<AttendanceAdjustmentResponse>.Conflict("Attendance adjustments require an open or closed session");

        var sessionStartsAt = NormalizeUtcPrecision(session.StartsAt);
        var sessionEndsAt = NormalizeUtcPrecision(session.EndsAt);
        if (adjustedCheckInAt.HasValue &&
            (adjustedCheckInAt.Value < sessionStartsAt || adjustedCheckInAt.Value > sessionEndsAt) ||
            adjustedCheckOutAt.HasValue &&
            (adjustedCheckOutAt.Value < sessionStartsAt || adjustedCheckOutAt.Value > sessionEndsAt))
        {
            return Result<AttendanceAdjustmentResponse>.Conflict("Adjusted attendance times must be within the session window");
        }

        var participant = await db.Set<AttendanceParticipant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.Id == request.ParticipantId &&
                item.ContextId == session.ContextId &&
                item.StartedAt <= session.StartsAt &&
                (!item.EndedAt.HasValue || item.EndedAt.Value > session.StartsAt),
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

        record.FirstCheckInAt = adjustedCheckInAt;
        record.LastCheckOutAt = adjustedCheckOutAt;
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
            AdjustedCheckInAt = adjustedCheckInAt,
            AdjustedCheckOutAt = adjustedCheckOutAt,
            ActorCredentialId = actorCredentialId.Value,
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

                if (attendanceEvent.OccurredAt < record.FirstCheckInAt.Value)
                    return Result<AttendanceRecordResponse>.Conflict("Checkout cannot be before check-in");

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
        if (record.FirstCheckInAt.Value > NormalizeUtcPrecision(session.StartsAt).AddMinutes(graceMinutes))
            return AttendanceRecordStatus.Late;

        var checkoutRequired = policy?.CheckoutRequired ?? true;
        if (checkoutRequired && !record.LastCheckOutAt.HasValue)
            return AttendanceRecordStatus.Incomplete;

        var earlyCheckoutGrace = policy?.EarlyCheckoutGraceMinutes ?? DefaultEarlyCheckoutGraceMinutes;
        if (checkoutRequired &&
            record.LastCheckOutAt.HasValue &&
            record.LastCheckOutAt.Value < NormalizeUtcPrecision(session.EndsAt).AddMinutes(-earlyCheckoutGrace))
            return AttendanceRecordStatus.Incomplete;

        return AttendanceRecordStatus.Present;
    }

    private async Task<Result<AttendanceEventResponse>> ResolveEventReplayAsync(
        Guid tenantId,
        AttendanceEvent existingEvent,
        RecordAttendanceEventRequest request,
        DateTime? requestedOccurredAt,
        Guid actorCredentialId,
        string? sourceReference,
        string? notes,
        string? metadataJson,
        CancellationToken ct)
    {
        if (!EventPayloadMatches(
                existingEvent,
                request,
                requestedOccurredAt,
                actorCredentialId,
                sourceReference,
                notes,
                metadataJson))
        {
            return Result<AttendanceEventResponse>.Conflict(
                "Idempotency key is already associated with a different attendance event");
        }

        var replayRecord = await GetRecordEntityAsync(
            tenantId,
            existingEvent.SessionId,
            existingEvent.ParticipantId,
            false,
            ct);
        return Result<AttendanceEventResponse>.Success(
            ToEventResponse(
                existingEvent,
                replayRecord is null ? null : ToRecordResponse(replayRecord, replayRecord.Status)),
            "Attendance event replayed");
    }

    private static bool EventPayloadMatches(
        AttendanceEvent existingEvent,
        RecordAttendanceEventRequest request,
        DateTime? requestedOccurredAt,
        Guid actorCredentialId,
        string? sourceReference,
        string? notes,
        string? metadataJson) =>
        existingEvent.SessionId == request.SessionId &&
        existingEvent.ParticipantId == request.ParticipantId &&
        existingEvent.EventType == request.EventType &&
        existingEvent.Source == request.Source &&
        (!requestedOccurredAt.HasValue ||
         NormalizeUtcPrecision(existingEvent.OccurredAt).Ticks == requestedOccurredAt.Value.Ticks) &&
        existingEvent.RecordedByCredentialId == actorCredentialId &&
        string.Equals(existingEvent.SourceReference, sourceReference, StringComparison.Ordinal) &&
        string.Equals(existingEvent.Notes, notes, StringComparison.Ordinal) &&
        MetadataMatches(existingEvent.MetadataJson, metadataJson);

    private static string? SerializeMetadata(IReadOnlyDictionary<string, string>? data)
    {
        if (data is null)
            return null;

        var orderedData = data
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        return JsonSerializer.Serialize(orderedData);
    }

    private static bool MetadataMatches(string? existingJson, string? requestJson)
    {
        if (existingJson is null || requestJson is null)
            return existingJson is null && requestJson is null;

        try
        {
            var existing = JsonSerializer.Deserialize<Dictionary<string, string>>(existingJson);
            var requested = JsonSerializer.Deserialize<Dictionary<string, string>>(requestJson);
            return existing is not null &&
                   requested is not null &&
                   existing.Count == requested.Count &&
                   existing.All(item =>
                       requested.TryGetValue(item.Key, out var value) &&
                       string.Equals(item.Value, value, StringComparison.Ordinal));
        }
        catch (JsonException)
        {
            return string.Equals(existingJson, requestJson, StringComparison.Ordinal);
        }
    }

    private static bool IsUtc(DateTime value) => value.Kind == DateTimeKind.Utc;

    private static DateTime NormalizeUtcPrecision(DateTime value) =>
        new(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);

    private static bool IsValidTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return false;

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private bool TryResolveTenantId(Guid? requestTenantId, RequestMetadata metadata, out Guid tenantId)
    {
        tenantId = trustedInvocationContextAccessor.Current?.EffectiveTenantId ?? Guid.Empty;
        if (requestTenantId is { } suppliedTenantId &&
            suppliedTenantId != Guid.Empty &&
            suppliedTenantId != tenantId)
        {
            return false;
        }

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
