using Attendance.Domain.Shared.Contracts;
using Attendance.Domain.Shared.Contracts.Requests;
using Attendance.Domain.Shared.Contracts.Responses;
using Attendance.Domain.Shared.Enums;
using Attendance.Integration.Drivers;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Portal.Services;

public sealed class AttendancePortalReadService(
    IDataContext dataContext,
    IAttendanceServiceWrapper attendance,
    RequestMetadata requestMetadata,
    ILogger<AttendancePortalReadService> logger)
{
    public async Task<IReadOnlyList<AttendanceContextRow>> LoadContextRowsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var response = await attendance.GetAttendanceContextOverview(new()
        {
            TenantId = tenantId,
            Limit = 500,
            Metadata = BuildMetadata(tenantId)
        }, cancellationToken);
        if (!response.IsSuccess || response.Response is null)
        {
            LogReadFailure("context overview", tenantId, response.HttpStatusCode, response.Message);
            return [];
        }

        return response.Response.Items.Select(context =>
            new AttendanceContextRow(
                context.Id,
                context.TenantId,
                context.Name,
                context.Code,
                context.ContextType,
                context.Description,
                context.IsActive,
                context.ActiveParticipantCount,
                context.SessionCount,
                context.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<AttendanceSessionRow>> LoadSessionRowsAsync(
        Guid tenantId,
        Guid? contextId,
        DateTime fromUtc,
        DateTime toUtc,
        AttendanceSessionStatus? status,
        CancellationToken cancellationToken = default)
    {
        var from = NormalizeUtc(fromUtc);
        var to = NormalizeUtc(toUtc);
        var response = await attendance.GetAttendanceSessionReadList(new()
        {
            TenantId = tenantId,
            ContextId = contextId,
            FromUtc = from,
            ToUtc = to,
            Status = status,
            Limit = 500,
            Metadata = BuildMetadata(tenantId)
        }, cancellationToken);
        if (!response.IsSuccess || response.Response is null)
        {
            LogReadFailure("session list", tenantId, response.HttpStatusCode, response.Message);
            return [];
        }

        var contexts = response.Response.Contexts.ToDictionary(context => context.Id, ContextLabel);
        return response.Response.Items
            .Select(session =>
                new AttendanceSessionRow(
                    session.Id,
                    session.TenantId,
                    session.ContextId,
                    LabelOrFallback(session.ContextId, contexts, "Context"),
                    session.Name,
                    session.Code,
                    NormalizeUtc(session.StartsAt),
                    NormalizeUtc(session.EndsAt),
                    session.TimeZoneId,
                    session.Status))
            .ToList();
    }

    public async Task<AttendanceSessionDetail?> LoadSessionDetailAsync(
        Guid tenantId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var response = await attendance.GetAttendanceSessionDetailRead(new()
        {
            TenantId = tenantId,
            SessionId = sessionId,
            Metadata = BuildMetadata(tenantId)
        }, cancellationToken);
        if (!response.IsSuccess || response.Response is null)
        {
            LogReadFailure("session detail", tenantId, response.HttpStatusCode, response.Message);
            return null;
        }

        var detail = response.Response;

        var credentialLabels = await LoadCredentialLabelsAsync(
            tenantId,
            detail.Participants.Select(x => (Guid?)x.CredentialId)
                .Concat(detail.Records.Select(x => (Guid?)x.CredentialId)),
            cancellationToken);
        var recordsByParticipant = detail.Records
            .GroupBy(x => x.ParticipantId)
            .ToDictionary(x => x.Key, x => x.First());

        var roster = detail.Participants.Select(participant =>
        {
            recordsByParticipant.TryGetValue(participant.Id, out var record);
            return new AttendanceSessionRosterRow(
                participant.Id,
                participant.CredentialId,
                LabelOrFallback(participant.CredentialId, credentialLabels, "Credential"),
                DisplayNameForParticipant(participant),
                participant.ReferenceCode ?? ShortId(participant.CredentialId),
                record?.Id,
                record?.FirstCheckInAt,
                record?.LastCheckOutAt,
                record?.Status ?? AttendanceRecordStatus.Absent,
                record?.IsManual ?? false,
                record?.Notes);
        }).ToList();

        return new AttendanceSessionDetail(detail.Session, detail.Context, roster, detail.RecentEvents);
    }

    public async Task<IReadOnlyList<AttendanceParticipantRow>> LoadParticipantRowsAsync(
        Guid tenantId,
        Guid contextId,
        CancellationToken cancellationToken = default)
    {
        var response = await attendance.GetAttendanceParticipantReadList(new()
        {
            TenantId = tenantId,
            ContextId = contextId,
            Limit = 1000,
            Metadata = BuildMetadata(tenantId)
        }, cancellationToken);
        if (!response.IsSuccess || response.Response is null)
        {
            LogReadFailure("participant list", tenantId, response.HttpStatusCode, response.Message);
            return [];
        }

        var participants = response.Response.Items;

        var labels = await LoadCredentialLabelsAsync(
            tenantId,
            participants.Select(x => (Guid?)x.CredentialId),
            cancellationToken);

        return participants.Select(participant =>
            new AttendanceParticipantRow(
                participant.Id,
                participant.ContextId,
                participant.CredentialId,
                LabelOrFallback(participant.CredentialId, labels, "Credential"),
                DisplayNameForParticipant(participant),
                participant.ReferenceCode ?? ShortId(participant.CredentialId),
                participant.StartedAt,
                participant.EndedAt,
                participant.IsActive))
            .ToList();
    }

    public async Task<IReadOnlyList<IdentityCredential>> LoadCredentialOptionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var credentials = await dataContext.Query<IdentityCredential>()
            .NoCache()
            .Include(x => x.IdentityInfo)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Take(500)
            .ToListAsync(cancellationToken);

        return credentials
            .OrderBy(BuildCredentialSortText, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<AttendanceUserParticipationRow>> LoadUserParticipationRowsAsync(
        Guid tenantId,
        Guid identityInfoId,
        CancellationToken cancellationToken = default)
    {
        var credentials = await LoadUserCredentialsAsync(tenantId, identityInfoId, cancellationToken);
        var credentialIds = credentials.Select(x => x.Id).ToArray();
        if (credentialIds.Length == 0)
        {
            return [];
        }

        var response = await attendance.GetAttendanceCredentialHistory(new()
        {
            TenantId = tenantId,
            CredentialIds = credentialIds.ToList(),
            Metadata = BuildMetadata(tenantId)
        }, cancellationToken);
        if (!response.IsSuccess || response.Response is null)
        {
            LogReadFailure("credential participation", tenantId, response.HttpStatusCode, response.Message);
            return [];
        }

        var contextsById = response.Response.Contexts.ToDictionary(x => x.Id);
        var credentialLabels = credentials.ToDictionary(x => x.Id, BuildCredentialLabel);

        return response.Response.Participants.Select(participant =>
        {
            contextsById.TryGetValue(participant.ContextId, out var context);
            return
            new AttendanceUserParticipationRow(
                participant.Id,
                participant.ContextId,
                context is null ? $"Context {ShortId(participant.ContextId)}" : ContextLabel(context),
                context?.ContextType ?? AttendanceContextType.General,
                participant.CredentialId,
                LabelOrFallback(participant.CredentialId, credentialLabels, "Credential"),
                DisplayNameForParticipant(participant),
                participant.ReferenceCode ?? ShortId(participant.CredentialId),
                participant.StartedAt,
                participant.EndedAt,
                participant.IsActive);
        })
            .ToList();
    }

    public async Task<IReadOnlyList<AttendanceUserRecordRow>> LoadUserRecordRowsAsync(
        Guid tenantId,
        Guid identityInfoId,
        Guid? credentialId,
        CancellationToken cancellationToken = default)
    {
        var credentials = await LoadUserCredentialsAsync(tenantId, identityInfoId, cancellationToken);
        if (credentialId is Guid selectedCredentialId)
        {
            credentials = credentials.Where(x => x.Id == selectedCredentialId).ToList();
        }

        var credentialIds = credentials.Select(x => x.Id).ToArray();
        if (credentialIds.Length == 0)
        {
            return [];
        }

        var response = await attendance.GetAttendanceCredentialHistory(new()
        {
            TenantId = tenantId,
            CredentialIds = credentialIds.ToList(),
            Metadata = BuildMetadata(tenantId)
        }, cancellationToken);
        if (!response.IsSuccess || response.Response is null)
        {
            LogReadFailure("credential records", tenantId, response.HttpStatusCode, response.Message);
            return [];
        }

        var credentialLabels = credentials.ToDictionary(x => x.Id, BuildCredentialLabel);
        var sessionsById = response.Response.Sessions.ToDictionary(x => x.Id);
        var contextsById = response.Response.Contexts.ToDictionary(x => x.Id);

        return response.Response.Records.Select(record =>
        {
            sessionsById.TryGetValue(record.SessionId, out var session);
            var contextName = session is not null && contextsById.TryGetValue(session.ContextId, out var context)
                ? ContextLabel(context)
                : "Unknown context";

            return new AttendanceUserRecordRow(
                record.Id,
                record.SessionId,
                session?.Name ?? $"Session {ShortId(record.SessionId)}",
                contextName,
                record.CredentialId,
                LabelOrFallback(record.CredentialId, credentialLabels, "Credential"),
                session?.StartsAt,
                session?.EndsAt,
                record.FirstCheckInAt,
                record.LastCheckOutAt,
                record.Status,
                record.IsManual,
                record.Notes);
        }).ToList();
    }

    public async Task<IReadOnlyList<AttendanceContextOption>> LoadContextOptionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var response = await attendance.GetAttendanceContextOverview(new GetAttendanceContextOverviewRequest
        {
            TenantId = tenantId,
            Limit = 500,
            Metadata = BuildMetadata(tenantId)
        }, cancellationToken);

        if (!response.IsSuccess || response.Response is null)
        {
            logger.LogWarning(
                "Attendance context options could not be loaded for tenant {TenantId}. Status: {StatusCode}. Message: {Message}",
                tenantId,
                response.HttpStatusCode,
                response.Message);
            return [];
        }

        var options = response.Response.Items
            .Where(context => context.Id != Guid.Empty && context.IsActive)
            .OrderBy(context => context.Name)
            .Select(context => new AttendanceContextOption(context.Id, ContextLabel(context), context.ContextType, context.IsActive))
            .ToList();

        var emptyIdCount = response.Response.Items.Count - options.Count;
        if (emptyIdCount > 0)
        {
            logger.LogWarning(
                "Attendance context options response for tenant {TenantId} contained {EmptyIdCount} context(s) with an empty ID.",
                tenantId,
                emptyIdCount);
        }

        return options;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> LoadCredentialLabelsAsync(
        Guid tenantId,
        IEnumerable<Guid?> credentialIds,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(credentialIds);
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var credentials = await dataContext.Query<IdentityCredential>()
            .NoCache()
            .Include(x => x.IdentityInfo)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return credentials.ToDictionary(x => x.Id, BuildCredentialLabel);
    }

    private async Task<List<IdentityCredential>> LoadUserCredentialsAsync(
        Guid tenantId,
        Guid identityInfoId,
        CancellationToken cancellationToken)
    {
        return await dataContext.Query<IdentityCredential>()
            .NoCache()
            .Include(x => x.IdentityInfo)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IdentityInfoId == identityInfoId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public static string BuildCredentialLabel(IdentityCredential credential)
    {
        var displayName = Normalize(credential.IdentityInfo?.FullName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = Normalize(credential.IdentityInfo?.IdentityName);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = Normalize(credential.UserName) ?? Normalize(credential.UserAlias) ?? "Unnamed user";
        }

        var login = Normalize(credential.UserName) ?? Normalize(credential.UserAlias);
        return string.IsNullOrWhiteSpace(login)
            ? $"{displayName} ({ShortId(credential.Id)})"
            : $"{displayName} ({login})";
    }

    public static string BuildCredentialReferenceCode(IdentityCredential credential) =>
        Normalize(credential.UserName) ?? Normalize(credential.UserAlias) ?? ShortId(credential.Id);

    public static string BuildCredentialSearchText(IdentityCredential credential) =>
        $"{credential.IdentityInfo?.FullName} {credential.IdentityInfo?.IdentityName} {credential.UserName} {credential.UserAlias} {credential.Device} {credential.Location} {credential.Id}";

    public static string FormatEnumLabel<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var text = value.ToString();
        return string.Concat(text.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));
    }

    public static string StatusKey(AttendanceRecordStatus status) => status switch
    {
        AttendanceRecordStatus.Present => "present",
        AttendanceRecordStatus.Late => "late",
        AttendanceRecordStatus.Absent => "absent",
        AttendanceRecordStatus.Incomplete => "incomplete",
        AttendanceRecordStatus.Excused => "excused",
        AttendanceRecordStatus.ManualAdjusted => "manual-adjusted",
        _ => "inactive"
    };

    public static string StatusKey(AttendanceSessionStatus status) => status switch
    {
        AttendanceSessionStatus.Open => "open",
        AttendanceSessionStatus.Closed => "closed",
        AttendanceSessionStatus.Cancelled => "cancelled",
        AttendanceSessionStatus.Scheduled => "scheduled",
        _ => "inactive"
    };

    public static string ShortId(Guid? id) => id is Guid value ? value.ToString("N")[..8] : "N/A";

    private RequestMetadata BuildMetadata(Guid tenantId) => new()
    {
        RequestedTenantId = tenantId,
        RequestId = Guid.NewGuid(),
        OperationName = requestMetadata.OperationName ?? "Portal",
        DeviceName = requestMetadata.DeviceName,
        UserAgent = requestMetadata.UserAgent,
        IpAddress = requestMetadata.IpAddress
    };

    private static string ContextLabel(AttendanceContextResponse context) => ContextLabel(context.Name, context.Code);

    private static string ContextLabel(AttendanceContextOverviewResponse context) => ContextLabel(context.Name, context.Code);

    private static string ContextLabel(string name, string? code) =>
        string.IsNullOrWhiteSpace(code) ? name : $"{code} - {name}";

    private static string DisplayNameForParticipant(AttendanceParticipantResponse participant) =>
        Normalize(participant.DisplayName) ?? $"Credential {ShortId(participant.CredentialId)}";

    private void LogReadFailure(string operation, Guid tenantId, object statusCode, string? message) =>
        logger.LogWarning(
            "Attendance {Operation} could not be loaded for tenant {TenantId}. Status: {StatusCode}. Message: {Message}",
            operation,
            tenantId,
            statusCode,
            message);

    private static string LabelOrFallback(Guid? id, IReadOnlyDictionary<Guid, string> labels, string noun)
    {
        if (id is not Guid value)
        {
            return "N/A";
        }

        return labels.TryGetValue(value, out var label) && !string.IsNullOrWhiteSpace(label)
            ? label
            : $"{noun} {ShortId(value)}";
    }

    private static string BuildCredentialSortText(IdentityCredential credential) =>
        Normalize(credential.IdentityInfo?.FullName)
        ?? Normalize(credential.IdentityInfo?.IdentityName)
        ?? Normalize(credential.UserName)
        ?? Normalize(credential.UserAlias)
        ?? credential.Id.ToString();

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static Guid[] NormalizeIds(IEnumerable<Guid?> ids) =>
        ids.Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AttendanceContextRow(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Code,
    AttendanceContextType ContextType,
    string? Description,
    bool IsActive,
    int ActiveParticipantCount,
    int SessionCount,
    DateTime CreatedAt);

public sealed record AttendanceContextOption(
    Guid Id,
    string Label,
    AttendanceContextType ContextType,
    bool IsActive);

public sealed record AttendanceSessionRow(
    Guid Id,
    Guid TenantId,
    Guid ContextId,
    string ContextName,
    string Name,
    string? Code,
    DateTime StartsAt,
    DateTime EndsAt,
    string TimeZoneId,
    AttendanceSessionStatus Status);

public sealed record AttendanceParticipantRow(
    Guid Id,
    Guid ContextId,
    Guid CredentialId,
    string CredentialLabel,
    string DisplayName,
    string ReferenceCode,
    DateTime StartedAt,
    DateTime? EndedAt,
    bool IsActive);

public sealed record AttendanceSessionDetail(
    AttendanceSessionResponse Session,
    AttendanceContextResponse? Context,
    IReadOnlyList<AttendanceSessionRosterRow> Roster,
    IReadOnlyList<AttendanceEventResponse> RecentEvents);

public sealed record AttendanceSessionRosterRow(
    Guid ParticipantId,
    Guid CredentialId,
    string CredentialLabel,
    string DisplayName,
    string ReferenceCode,
    Guid? RecordId,
    DateTime? FirstCheckInAt,
    DateTime? LastCheckOutAt,
    AttendanceRecordStatus Status,
    bool IsManual,
    string? Notes);

public sealed record AttendanceUserParticipationRow(
    Guid ParticipantId,
    Guid ContextId,
    string ContextName,
    AttendanceContextType ContextType,
    Guid CredentialId,
    string CredentialLabel,
    string DisplayName,
    string ReferenceCode,
    DateTime StartedAt,
    DateTime? EndedAt,
    bool IsActive);

public sealed record AttendanceUserRecordRow(
    Guid? RecordId,
    Guid SessionId,
    string SessionName,
    string ContextName,
    Guid CredentialId,
    string CredentialLabel,
    DateTime? StartsAt,
    DateTime? EndsAt,
    DateTime? FirstCheckInAt,
    DateTime? LastCheckOutAt,
    AttendanceRecordStatus Status,
    bool IsManual,
    string? Notes);
