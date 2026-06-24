using System.Reflection;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.DataGrid;
using BlazorBlueprint.Primitives.Filtering;
using IdentityServer.Domain.Shared.Contracts;
using global::Messaging.Domain.Shared;
using global::Messaging.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;

namespace ControlPanel.Server.Services;

public sealed class MessagingControlPanelReadService(IDataContext dataContext)
{
    public async Task<MessagingUsersSummary> GetUsersSummaryAsync(Guid tenantId, CancellationToken ct = default)
    {
        var rows = await BuildUserRowsAsync(tenantId, ct);
        return new MessagingUsersSummary(
            rows.Count,
            rows.Count(x => x.IsOnline),
            rows.Count(x => x.MutedThreadCount > 0),
            rows.Count(x => x.BlockRelationshipCount > 0));
    }

    public async ValueTask<DataGridResult<MessagingUserAdminRow>> GetUsersAsync(
        Guid tenantId,
        DataGridRequest request)
    {
        var rows = await BuildUserRowsAsync(tenantId, request.CancellationToken);
        return ApplyRequest(rows, request);
    }

    public async Task<MessagingThreadsSummary> GetThreadsSummaryAsync(Guid tenantId, CancellationToken ct = default)
    {
        var totalThreads = await ThreadQuery(tenantId).CountAsync(ct);
        var totalMessages = await dataContext.Query<Message>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => !x.IsDeleted)
            .CountAsync(ct);
        var totalMembers = await dataContext.Query<MessageThreadMember>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => !x.IsDeleted)
            .CountAsync(ct);
        var pendingOutboxCount = await dataContext.Query<MessageOutboxEvent>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => !x.IsDeleted)
            .Where(x => x.ProcessedAt == null)
            .CountAsync(ct);

        return new MessagingThreadsSummary(totalThreads, totalMessages, totalMembers, pendingOutboxCount);
    }

    public async ValueTask<DataGridResult<MessagingThreadAdminRow>> GetThreadsAsync(
        Guid tenantId,
        DataGridRequest request)
    {
        var rows = await BuildThreadRowsAsync(tenantId, request.CancellationToken);
        return ApplyRequest(rows, request);
    }

    private async Task<List<MessagingUserAdminRow>> BuildUserRowsAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var members = await dataContext.Query<MessageThreadMember>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1000)
            .ToListAsync(ct);

        var credentialIds = members
            .Select(x => x.CredentialId)
            .Distinct()
            .ToHashSet();

        if (credentialIds.Count == 0)
        {
            return [];
        }

        var credentials = await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.IdentityInfo)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.UserName)
            .Take(1000)
            .ToListAsync(ct);

        var messages = await dataContext.Query<Message>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        var invites = await dataContext.Query<MessageThreadInvite>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1000)
            .ToListAsync(ct);

        var blocks = await dataContext.Query<MessageBlock>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1000)
            .ToListAsync(ct);

        var credentialMap = credentials.ToDictionary(x => x.Id);
        return credentialIds
            .Select(credentialId => CreateUserRow(
                credentialId,
                credentialMap.GetValueOrDefault(credentialId),
                members.Where(x => x.CredentialId == credentialId).ToList(),
                messages,
                invites,
                blocks))
            .OrderBy(row => row.DisplayName)
            .ToList();
    }

    private async Task<List<MessagingThreadAdminRow>> BuildThreadRowsAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var threads = await ThreadQuery(tenantId)
            .Include(x => x.Type)
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        var loadedMessages = await dataContext.Query<Message>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        var loadedMembers = await dataContext.Query<MessageThreadMember>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        var loadedPins = await dataContext.Query<MessagePin>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1000)
            .ToListAsync(ct);

        var loadedReports = await dataContext.Query<MessageReport>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1000)
            .ToListAsync(ct);

        var loadedInvites = await dataContext.Query<MessageThreadInvite>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1000)
            .ToListAsync(ct);

        return threads
            .Select(thread =>
            {
                var threadMessages = loadedMessages
                    .Where(message => message.MessageThreadId == thread.Id)
                    .ToList();
                var threadMembers = loadedMembers
                    .Where(member => member.MessageThreadId == thread.Id)
                    .ToList();
                var latestMessage = threadMessages.MaxBy(message => message.CreatedAt);

                return new MessagingThreadAdminRow(
                    thread.Id,
                    string.IsNullOrWhiteSpace(thread.Name) ? thread.Id.ToString()[..8] : thread.Name,
                    Truncate(thread.Description, 90, "No description"),
                    thread.Type?.Name ?? "N/A",
                    threadMembers.Count,
                    threadMessages.Count,
                    loadedInvites.Count(invite =>
                        invite.MessageThreadId == thread.Id &&
                        invite.Status == MessageThreadInviteStatuses.Pending),
                    loadedPins.Count(pin => pin.MessageThreadId == thread.Id),
                    loadedReports.Count(report =>
                        threadMessages.Any(message => message.Id == report.MessageId)),
                    threadMembers.Count(member => member.IsMuted),
                    threadMembers.Count(member => member.IsArchived),
                    latestMessage is null ? "No messages" : Truncate(latestMessage.Text, 90, "No message text"),
                    latestMessage?.CreatedAt,
                    thread.IsEnabled,
                    thread.CreatedAt);
            })
            .OrderByDescending(row => row.LastMessageAt ?? row.CreatedAt)
            .ToList();
    }

    private IRemoteQuery<MessageThread> ThreadQuery(Guid tenantId) =>
        dataContext.Query<MessageThread>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => !x.IsDeleted);

    private static MessagingUserAdminRow CreateUserRow(
        Guid credentialId,
        IdentityCredential? credential,
        IReadOnlyCollection<MessageThreadMember> memberships,
        IReadOnlyCollection<Message> messages,
        IReadOnlyCollection<MessageThreadInvite> invites,
        IReadOnlyCollection<MessageBlock> blocks)
    {
        var memberIds = memberships.Select(x => x.Id).ToHashSet();
        var userMessages = messages.Where(x => memberIds.Contains(x.MessageThreadMemberId)).ToList();
        var roleGroups = memberships
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Role) ? MessageThreadMemberRoles.Member : x.Role)
            .OrderByDescending(x => x.Count())
            .Select(x => $"{x.Key}: {x.Count()}")
            .ToList();

        return new MessagingUserAdminRow(
            credentialId,
            credential?.IdentityInfoId,
            GetDisplayName(credential),
            credential?.UserName ?? "No username",
            credential?.IdentityInfo?.FullName ?? credential?.IdentityInfo?.IdentityName ?? "No identity record",
            credential?.IsOnline == true,
            credential?.LastSeen,
            memberships.Select(x => x.MessageThreadId).Distinct().Count(),
            userMessages.Count,
            memberships.Count(x => x.IsMuted),
            memberships.Count(x => x.IsArchived),
            invites.Count(x => x.InvitedCredentialId == credentialId && x.Status == MessageThreadInviteStatuses.Pending),
            blocks.Count(x => x.BlockerCredentialId == credentialId || x.BlockedCredentialId == credentialId),
            roleGroups.Count == 0 ? "Member: 0" : string.Join(", ", roleGroups));
    }

    private static DataGridResult<T> ApplyRequest<T>(
        IEnumerable<T> source,
        DataGridRequest request)
    {
        var rows = ApplySearch(source, request.SearchText);
        rows = ApplyFilters(rows, request.Filters);
        rows = ApplySort(rows, request.SortDefinitions);

        var total = rows.Count();
        var count = request.Count.GetValueOrDefault(20);
        rows = rows
            .Skip(Math.Max(request.StartIndex, 0))
            .Take(count <= 0 ? 20 : count);

        return new DataGridResult<T>
        {
            Items = rows.ToList(),
            TotalItemCount = total
        };
    }

    private static IEnumerable<T> ApplySearch<T>(
        IEnumerable<T> source,
        string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return source;
        }

        var search = searchText.Trim();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return source.Where(row => properties.Any(property =>
            Convert.ToString(property.GetValue(row))?.Contains(search, StringComparison.OrdinalIgnoreCase) == true));
    }

    private static IEnumerable<T> ApplyFilters<T>(
        IEnumerable<T> source,
        IReadOnlyDictionary<string, FilterCondition>? filters)
    {
        if (filters is null || filters.Count == 0)
        {
            return source;
        }

        foreach (var filter in filters.Values)
        {
            var property = FindProperty<T>(filter.Field);
            if (property is null)
            {
                continue;
            }

            source = source.Where(row => MatchesFilter(property.GetValue(row), filter));
        }

        return source;
    }

    private static IEnumerable<T> ApplySort<T>(
        IEnumerable<T> source,
        IReadOnlyList<SortDefinition>? sortDefinitions)
    {
        var sort = sortDefinitions?.FirstOrDefault(x => x.Direction != SortDirection.None);
        if (sort is null)
        {
            return source;
        }

        var property = FindProperty<T>(sort.ColumnId);
        if (property is null)
        {
            return source;
        }

        return sort.Direction == SortDirection.Descending
            ? source.OrderByDescending(row => property.GetValue(row))
            : source.OrderBy(row => property.GetValue(row));
    }

    private static bool MatchesFilter(object? value, FilterCondition filter)
    {
        var text = Convert.ToString(value) ?? string.Empty;
        var filterValue = Convert.ToString(filter.Value) ?? string.Empty;

        return filter.Operator switch
        {
            FilterOperator.Equals => string.Equals(text, filterValue, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotEquals => !string.Equals(text, filterValue, StringComparison.OrdinalIgnoreCase),
            FilterOperator.IsEmpty => string.IsNullOrWhiteSpace(text),
            FilterOperator.IsNotEmpty => !string.IsNullOrWhiteSpace(text),
            FilterOperator.NotContains => !text.Contains(filterValue, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith => text.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase),
            FilterOperator.EndsWith => text.EndsWith(filterValue, StringComparison.OrdinalIgnoreCase),
            FilterOperator.IsTrue => bool.TryParse(text, out var parsed) && parsed,
            FilterOperator.IsFalse => bool.TryParse(text, out var parsed) && !parsed,
            _ => text.Contains(filterValue, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static PropertyInfo? FindProperty<T>(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalized = Normalize(name);
        return typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(property =>
                string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Normalize(property.Name), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).ToArray());

    private static string GetDisplayName(IdentityCredential? credential)
    {
        if (!string.IsNullOrWhiteSpace(credential?.IdentityInfo?.FullName))
        {
            return credential.IdentityInfo.FullName;
        }

        if (!string.IsNullOrWhiteSpace(credential?.IdentityInfo?.IdentityName))
        {
            return credential.IdentityInfo.IdentityName;
        }

        if (!string.IsNullOrWhiteSpace(credential?.UserAlias))
        {
            return credential.UserAlias;
        }

        if (!string.IsNullOrWhiteSpace(credential?.UserName))
        {
            return credential.UserName;
        }

        return credential is null ? "Credential not found" : credential.Id.ToString()[..8];
    }

    private static string Truncate(string? value, int maxLength, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Length <= maxLength ? value : $"{value[..maxLength]}...";
    }
}

public sealed record MessagingUsersSummary(
    int MessagingUserCount,
    int OnlineCount,
    int MutedUserCount,
    int BlockedUserCount);

public sealed record MessagingThreadsSummary(
    int TotalThreads,
    int TotalMessages,
    int TotalMembers,
    int PendingOutboxCount);

public sealed record MessagingUserAdminRow(
    Guid CredentialId,
    Guid? IdentityInfoId,
    string DisplayName,
    string UserName,
    string IdentityLabel,
    bool IsOnline,
    DateTime? LastSeen,
    int ThreadCount,
    int MessageCount,
    int MutedThreadCount,
    int ArchivedThreadCount,
    int PendingInviteCount,
    int BlockRelationshipCount,
    string RoleSummary)
{
    public string PresenceText => IsOnline ? "Online" : "Offline";
    public string LastSeenText => LastSeen is null ? "Never seen" : $"Last seen {LastSeen:g}";
}

public sealed record MessagingThreadAdminRow(
    Guid Id,
    string Name,
    string Description,
    string TypeName,
    int MemberCount,
    int MessageCount,
    int PendingInviteCount,
    int PinnedCount,
    int ReportCount,
    int MutedMemberCount,
    int ArchivedMemberCount,
    string LastMessagePreview,
    DateTime? LastMessageAt,
    bool IsEnabled,
    DateTime CreatedAt)
{
    public string StatusText => IsEnabled ? "Enabled" : "Disabled";
    public string MemberState => $"{MutedMemberCount} muted / {ArchivedMemberCount} archived";
}
