using Attendance.Domain.Shared.Contracts;
using Attendance.Domain.Shared.Enums;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.DataContext;

namespace ControlPanel.Server.Services;

public sealed class AttendanceControlPanelReadService(IDataContext dataContext)
{
    public async Task<IReadOnlyList<AttendanceContextRow>> LoadContextRowsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var contexts = await dataContext.Query<AttendanceContext>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Take(500)
            .ToListAsync(cancellationToken);

        var participants = await dataContext.Query<AttendanceParticipant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var sessions = await dataContext.Query<AttendanceSession>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return contexts.Select(context =>
            new AttendanceContextRow(
                context.Id,
                context.TenantId,
                context.Name,
                context.Code,
                context.ContextType,
                context.Description,
                context.IsActive,
                participants.Count(x => x.ContextId == context.Id && x.IsActive),
                sessions.Count(x => x.ContextId == context.Id),
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
        var contexts = await LoadContextLabelsAsync(tenantId, cancellationToken);
        var query = dataContext.Query<AttendanceSession>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        if (contextId is Guid selectedContextId)
        {
            query = query.Where(x => x.ContextId == selectedContextId);
        }

        if (status is AttendanceSessionStatus selectedStatus)
        {
            query = query.Where(x => x.Status == selectedStatus);
        }

        var sessions = await query
            .OrderByDescending(x => x.StartsAt)
            .Take(2000)
            .ToListAsync(cancellationToken);

        var from = NormalizeUtc(fromUtc);
        var to = NormalizeUtc(toUtc);

        return sessions
            .Where(session =>
            {
                var startsAt = NormalizeUtc(session.StartsAt);
                return startsAt >= from && startsAt <= to;
            })
            .Take(500)
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
        var session = await dataContext.Query<AttendanceSession>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == sessionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return null;
        }

        var context = await dataContext.Query<AttendanceContext>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == session.ContextId)
            .FirstOrDefaultAsync(cancellationToken);

        var participants = await dataContext.Query<AttendanceParticipant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.ContextId == session.ContextId && x.IsActive)
            .OrderBy(x => x.DisplayName)
            .Take(1000)
            .ToListAsync(cancellationToken);

        var records = await dataContext.Query<AttendanceRecord>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.SessionId == sessionId)
            .Take(1000)
            .ToListAsync(cancellationToken);

        var events = await dataContext.Query<AttendanceEvent>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.SessionId == sessionId)
            .OrderByDescending(x => x.OccurredAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var credentialLabels = await LoadCredentialLabelsAsync(
            tenantId,
            participants.Select(x => (Guid?)x.CredentialId).Concat(records.Select(x => (Guid?)x.CredentialId)),
            cancellationToken);
        var recordsByParticipant = records
            .GroupBy(x => x.ParticipantId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(record => record.ModifiedAt ?? record.CreatedAt).First());

        var roster = participants.Select(participant =>
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

        return new AttendanceSessionDetail(session, context, roster, events);
    }

    public async Task<IReadOnlyList<AttendanceParticipantRow>> LoadParticipantRowsAsync(
        Guid tenantId,
        Guid contextId,
        CancellationToken cancellationToken = default)
    {
        var participants = await dataContext.Query<AttendanceParticipant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.ContextId == contextId)
            .OrderBy(x => x.DisplayName)
            .Take(1000)
            .ToListAsync(cancellationToken);

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
            .IgnoreQueryFilters()
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

        var participants = await dataContext.Query<AttendanceParticipant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && credentialIds.Contains(x.CredentialId))
            .OrderByDescending(x => x.StartedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var contextIds = participants.Select(x => x.ContextId).Distinct().ToArray();
        var contexts = contextIds.Length == 0
            ? []
            : await dataContext.Query<AttendanceContext>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted && contextIds.Contains(x.Id))
                .Take(500)
                .ToListAsync(cancellationToken);
        var contextsById = contexts.ToDictionary(x => x.Id);
        var credentialLabels = credentials.ToDictionary(x => x.Id, BuildCredentialLabel);

        return participants.Select(participant =>
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

        var records = await dataContext.Query<AttendanceRecord>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && credentialIds.Contains(x.CredentialId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var sessionIds = records.Select(x => x.SessionId).Distinct().ToArray();
        List<AttendanceSession> sessions = sessionIds.Length == 0
            ? []
            : await dataContext.Query<AttendanceSession>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted && sessionIds.Contains(x.Id))
                .Take(500)
                .ToListAsync(cancellationToken);

        var contextIds = sessions.Select(x => x.ContextId).Distinct().ToArray();
        List<AttendanceContext> contexts = contextIds.Length == 0
            ? []
            : await dataContext.Query<AttendanceContext>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted && contextIds.Contains(x.Id))
                .Take(500)
                .ToListAsync(cancellationToken);

        var credentialLabels = credentials.ToDictionary(x => x.Id, BuildCredentialLabel);
        var sessionsById = sessions.ToDictionary(x => x.Id);
        var contextsById = contexts.ToDictionary(x => x.Id);

        return records.Select(record =>
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
        var contexts = await dataContext.Query<AttendanceContext>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsActive)
            .OrderBy(x => x.Name)
            .Take(500)
            .ToListAsync(cancellationToken);

        return contexts
            .Select(context => new AttendanceContextOption(context.Id, ContextLabel(context), context.ContextType, context.IsActive))
            .ToList();
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
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.IdentityInfo)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return credentials.ToDictionary(x => x.Id, BuildCredentialLabel);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadContextLabelsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var contexts = await dataContext.Query<AttendanceContext>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Take(500)
            .ToListAsync(cancellationToken);

        return contexts.ToDictionary(x => x.Id, ContextLabel);
    }

    private async Task<List<IdentityCredential>> LoadUserCredentialsAsync(
        Guid tenantId,
        Guid identityInfoId,
        CancellationToken cancellationToken)
    {
        return await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
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

    private static string ContextLabel(AttendanceContext context) =>
        string.IsNullOrWhiteSpace(context.Code) ? context.Name : $"{context.Code} - {context.Name}";

    private static string DisplayNameForParticipant(AttendanceParticipant participant) =>
        Normalize(participant.DisplayName) ?? $"Credential {ShortId(participant.CredentialId)}";

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
    AttendanceSession Session,
    AttendanceContext? Context,
    IReadOnlyList<AttendanceSessionRosterRow> Roster,
    IReadOnlyList<AttendanceEvent> RecentEvents);

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
