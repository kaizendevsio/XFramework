using System.Reflection;
using System.Text.Json;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Requests.Admin;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.DataContext;

namespace Communications.Api.Services;

public sealed class CommunicationsAdminReadService(
    IDataContext dataContext,
    ICommunicationsRequestContextResolver requestContextResolver,
    ICommunicationsPolicyService policyService) : ICommunicationsAdminReadService
{
    public async Task<Result<CommunicationsAdminUsersResponse>> QueryUsersAsync(
        QueryCommunicationsAdminUsersRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveAdminTenantId(request.Metadata);
        if (!tenantResult.IsSuccess)
        {
            return Failure<CommunicationsAdminUsersResponse>(tenantResult);
        }

        var tenantId = tenantResult.Data;
        var rows = await BuildUserRowsAsync(tenantId, ct);
        var page = ApplyGrid(rows, request.Grid);

        return Result<CommunicationsAdminUsersResponse>.Success(new CommunicationsAdminUsersResponse
        {
            Summary = new CommunicationsAdminUsersSummary
            {
                CommunicationsUserCount = rows.Count,
                OnlineCount = rows.Count(x => x.IsOnline),
                MutedUserCount = rows.Count(x => x.MutedThreadCount > 0),
                BlockedUserCount = rows.Count(x => x.BlockRelationshipCount > 0)
            },
            Items = page.Items,
            TotalItemCount = page.TotalItemCount
        });
    }

    public async Task<Result<CommunicationsAdminUserDetailResponse>> GetUserDetailAsync(
        GetCommunicationsAdminUserDetailRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveAdminTenantId(request.Metadata);
        if (!tenantResult.IsSuccess)
        {
            return Failure<CommunicationsAdminUserDetailResponse>(tenantResult);
        }

        var tenantId = tenantResult.Data;
        var credential = await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.IdentityInfo)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Where(x => x.Id == request.CredentialId)
            .FirstOrDefaultAsync(ct);

        if (credential is null)
        {
            return Result<CommunicationsAdminUserDetailResponse>.NotFound("Communications user was not found for this tenant.");
        }

        var members = await dataContext.Query<MessageThreadMember>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Where(x => x.CredentialId == request.CredentialId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        var threadIds = members.Select(x => x.MessageThreadId).Distinct().ToHashSet();
        var memberIds = members.Select(x => x.Id).ToHashSet();
        var threads = await dataContext.Query<MessageThread>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.Type)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Where(x => threadIds.Contains(x.Id))
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        var messages = await dataContext.Query<Message>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Where(x => memberIds.Contains(x.MessageThreadMemberId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(250)
            .ToListAsync(ct);

        var invites = await dataContext.Query<MessageThreadInvite>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Where(x => x.InvitedCredentialId == request.CredentialId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        var blocks = await dataContext.Query<MessageBlock>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Where(x => x.BlockerCredentialId == request.CredentialId || x.BlockedCredentialId == request.CredentialId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        var credentialIds = blocks
            .SelectMany(x => new[] { x.BlockerCredentialId, x.BlockedCredentialId })
            .Where(x => x != request.CredentialId)
            .Distinct()
            .ToHashSet();
        var credentials = await LoadCredentialsAsync(tenantId, credentialIds, ct);
        credentials[request.CredentialId] = credential;

        var threadMap = threads.ToDictionary(x => x.Id);

        return Result<CommunicationsAdminUserDetailResponse>.Success(new CommunicationsAdminUserDetailResponse
        {
            Credential = ToCredentialContext(credential),
            Summary = new CommunicationsAdminUserDetailSummary
            {
                ThreadCount = threadIds.Count,
                MessageCount = messages.Count,
                MutedThreadCount = members.Count(x => x.IsMuted),
                BlockRelationshipCount = blocks.Count
            },
            Threads = members
                .Select(member => ToUserThreadRow(member, threadMap.GetValueOrDefault(member.MessageThreadId)))
                .OrderByDescending(x => x.JoinedAt)
                .ToList(),
            Messages = messages
                .Select(message => ToUserMessageRow(message, threadMap.GetValueOrDefault(message.MessageThreadId)))
                .ToList(),
            Invites = invites
                .Select(invite => ToUserInviteRow(invite, threadMap.GetValueOrDefault(invite.MessageThreadId), credentials))
                .ToList(),
            Blocks = blocks
                .Select(block => ToUserBlockRow(block, request.CredentialId, credentials))
                .ToList()
        });
    }

    public async Task<Result<CommunicationsAdminThreadsResponse>> QueryThreadsAsync(
        QueryCommunicationsAdminThreadsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveAdminTenantId(request.Metadata);
        if (!tenantResult.IsSuccess)
        {
            return Failure<CommunicationsAdminThreadsResponse>(tenantResult);
        }

        var tenantId = tenantResult.Data;
        var rows = await BuildThreadRowsAsync(tenantId, ct);
        var page = ApplyGrid(rows, request.Grid);

        return Result<CommunicationsAdminThreadsResponse>.Success(new CommunicationsAdminThreadsResponse
        {
            Summary = new CommunicationsAdminThreadsSummary
            {
                TotalThreads = await CountTenantRowsAsync<MessageThread>(tenantId, ct),
                TotalMessages = await CountTenantRowsAsync<Message>(tenantId, ct),
                TotalMembers = await CountTenantRowsAsync<MessageThreadMember>(tenantId, ct),
                PendingOutboxCount = await dataContext.Query<MessageOutboxEvent>()
                    .IgnoreQueryFilters()
                    .NoCache()
                    .Where(x => x.TenantId == tenantId && !x.IsDeleted)
                    .Where(x => x.ProcessedAt == null && x.DeadLetteredAt == null)
                    .CountAsync(ct)
            },
            Items = page.Items,
            TotalItemCount = page.TotalItemCount
        });
    }

    public async Task<Result<CommunicationsAdminThreadDetailResponse>> GetThreadDetailAsync(
        GetCommunicationsAdminThreadDetailRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveAdminTenantId(request.Metadata);
        if (!tenantResult.IsSuccess)
        {
            return Failure<CommunicationsAdminThreadDetailResponse>(tenantResult);
        }

        var tenantId = tenantResult.Data;
        var thread = await dataContext.Query<MessageThread>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.Type)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Where(x => x.Id == request.ThreadId)
            .FirstOrDefaultAsync(ct);

        if (thread is null)
        {
            return Result<CommunicationsAdminThreadDetailResponse>.NotFound("Communications thread was not found for this tenant.");
        }

        var members = await dataContext.Query<MessageThreadMember>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.Credential)
            .Include(x => x.Credential.IdentityInfo)
            .Include(x => x.Group)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Where(x => x.MessageThreadId == request.ThreadId)
            .OrderBy(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        var messages = await dataContext.Query<Message>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Where(x => x.MessageThreadId == request.ThreadId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
            .ToListAsync(ct);

        var memberMap = members.ToDictionary(x => x.Id);
        var lastMessage = messages.MaxBy(x => x.CreatedAt);

        return Result<CommunicationsAdminThreadDetailResponse>.Success(new CommunicationsAdminThreadDetailResponse
        {
            Thread = new CommunicationsAdminThreadContext
            {
                Id = thread.Id,
                TenantId = thread.TenantId,
                Name = DisplayThreadName(thread),
                Description = Truncate(thread.Description, 160, "No description"),
                TypeName = thread.Type?.Name ?? "N/A",
                IsEnabled = thread.IsEnabled,
                CreatedAt = thread.CreatedAt,
                ModifiedAt = thread.ModifiedAt,
                LastMessageAt = lastMessage?.CreatedAt
            },
            Summary = new CommunicationsAdminThreadDetailSummary
            {
                MemberCount = members.Count,
                MessageCount = messages.Count,
                MutedMemberCount = members.Count(x => x.IsMuted),
                ArchivedMemberCount = members.Count(x => x.IsArchived)
            },
            Members = members.Select(ToThreadMemberRow).ToList(),
            Messages = messages.Select(message => ToThreadMessageRow(message, memberMap.GetValueOrDefault(message.MessageThreadMemberId))).ToList()
        });
    }

    public async Task<Result<CommunicationsAdminOperationsResponse>> GetOperationsAsync(
        GetCommunicationsAdminOperationsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveAdminTenantId(request.Metadata);
        if (!tenantResult.IsSuccess)
        {
            return Failure<CommunicationsAdminOperationsResponse>(tenantResult);
        }

        var tenantId = tenantResult.Data;
        var context = await LoadOperationsContextAsync(tenantId, ct);
        var outbox = await dataContext.Query<MessageOutboxEvent>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.OccurredAt)
            .Take(200)
            .ToListAsync(ct);

        var invites = await dataContext.Query<MessageThreadInvite>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var pins = await dataContext.Query<MessagePin>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var savedMessages = await dataContext.Query<MessageSaved>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        return Result<CommunicationsAdminOperationsResponse>.Success(new CommunicationsAdminOperationsResponse
        {
            PendingOutboxCount = outbox.Count(x => x.ProcessedAt is null && x.DeadLetteredAt is null),
            FailedOutboxCount = outbox.Count(x => x.ProcessedAt is null && x.DeadLetteredAt is not null),
            PendingInviteCount = invites.Count(x => x.Status == MessageThreadInviteStatuses.Pending),
            NotificationsEnabled = await IsNotificationsEnabledAsync(tenantId, ct),
            Outbox = outbox.Select(item => ToOutboxRow(item, context)).ToList(),
            Invites = invites.Select(item => ToOperationInviteRow(item, context)).ToList(),
            Pins = pins.Select(item => ToPinRow(item, context)).ToList(),
            SavedMessages = savedMessages.Select(item => ToSavedRow(item, context)).ToList()
        });
    }

    public async Task<Result<CommunicationsAdminModerationResponse>> GetModerationAsync(
        GetCommunicationsAdminModerationRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveAdminTenantId(request.Metadata);
        if (!tenantResult.IsSuccess)
        {
            return Failure<CommunicationsAdminModerationResponse>(tenantResult);
        }

        var tenantId = tenantResult.Data;
        var policy = await policyService.GetPolicyAsync(tenantId, ct);
        if (!policy.ModerationAdminAuditVisible)
        {
            return Result<CommunicationsAdminModerationResponse>.Success(new CommunicationsAdminModerationResponse
            {
                Policies = CreatePolicyRows()
            });
        }

        var context = await LoadOperationsContextAsync(tenantId, ct);
        var reports = await dataContext.Query<MessageReport>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
            .ToListAsync(ct);

        var blocks = await dataContext.Query<MessageBlock>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
            .ToListAsync(ct);

        var rules = await dataContext.Query<MessageModerationRule>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Take(300)
            .ToListAsync(ct);

        return Result<CommunicationsAdminModerationResponse>.Success(new CommunicationsAdminModerationResponse
        {
            OpenReportCount = reports.Count(x => x.Status == MessageReportStatuses.Open),
            ReviewedReportCount = reports.Count(x =>
                x.Status is MessageReportStatuses.Reviewed or
                    MessageReportStatuses.Resolved or
                    MessageReportStatuses.Escalated),
            DismissedReportCount = reports.Count(x => x.Status == MessageReportStatuses.Dismissed),
            ActiveBlockCount = blocks.Count(x => x.IsEnabled),
            Reports = reports.Select(item => ToReportRow(item, context)).ToList(),
            Blocks = blocks.Select(item => ToModerationBlockRow(item, context)).ToList(),
            Policies = CreatePolicyRows(),
            Rules = rules.Select(ToModerationRuleRow).ToList()
        });
    }

    private async Task<List<CommunicationsAdminUserRow>> BuildUserRowsAsync(
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

        var credentialIds = members.Select(x => x.CredentialId).Distinct().ToHashSet();
        if (credentialIds.Count == 0)
        {
            return [];
        }

        var credentials = await LoadCredentialsAsync(tenantId, credentialIds, ct);
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

        return credentialIds
            .Select(credentialId => CreateUserRow(
                credentialId,
                credentials.GetValueOrDefault(credentialId),
                members.Where(x => x.CredentialId == credentialId).ToList(),
                messages,
                invites,
                blocks))
            .OrderBy(row => row.DisplayName)
            .ToList();
    }

    private async Task<List<CommunicationsAdminThreadRow>> BuildThreadRowsAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var threads = await dataContext.Query<MessageThread>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.Type)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        var loadedMessages = await dataContext.Query<Message>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        var loadedMembers = await dataContext.Query<MessageThreadMember>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        var loadedPins = await dataContext.Query<MessagePin>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1000)
            .ToListAsync(ct);

        var loadedReports = await dataContext.Query<MessageReport>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1000)
            .ToListAsync(ct);

        var loadedInvites = await dataContext.Query<MessageThreadInvite>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1000)
            .ToListAsync(ct);

        return threads
            .Select(thread =>
            {
                var threadMessages = loadedMessages.Where(message => message.MessageThreadId == thread.Id).ToList();
                var threadMembers = loadedMembers.Where(member => member.MessageThreadId == thread.Id).ToList();
                var latestMessage = threadMessages.MaxBy(message => message.CreatedAt);

                return new CommunicationsAdminThreadRow
                {
                    Id = thread.Id,
                    Name = DisplayThreadName(thread),
                    Description = Truncate(thread.Description, 90, "No description"),
                    TypeName = thread.Type?.Name ?? "N/A",
                    MemberCount = threadMembers.Count,
                    MessageCount = threadMessages.Count,
                    PendingInviteCount = loadedInvites.Count(invite =>
                        invite.MessageThreadId == thread.Id &&
                        invite.Status == MessageThreadInviteStatuses.Pending),
                    PinnedCount = loadedPins.Count(pin => pin.MessageThreadId == thread.Id),
                    ReportCount = loadedReports.Count(report =>
                        threadMessages.Any(message => message.Id == report.MessageId)),
                    MutedMemberCount = threadMembers.Count(member => member.IsMuted),
                    ArchivedMemberCount = threadMembers.Count(member => member.IsArchived),
                    LastMessagePreview = latestMessage is null ? "No messages" : Truncate(latestMessage.Text, 90, "No message text"),
                    LastMessageAt = latestMessage?.CreatedAt,
                    IsEnabled = thread.IsEnabled,
                    CreatedAt = thread.CreatedAt
                };
            })
            .OrderByDescending(row => row.LastMessageAt ?? row.CreatedAt)
            .ToList();
    }

    private async Task<CommunicationsAdminOperationContext> LoadOperationsContextAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var threads = await dataContext.Query<MessageThread>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.Type)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(750)
            .ToListAsync(ct);

        var members = await dataContext.Query<MessageThreadMember>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.Credential)
            .Include(x => x.Credential.IdentityInfo)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1500)
            .ToListAsync(ct);

        var messages = await dataContext.Query<Message>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        var credentialIds = members.Select(x => x.CredentialId).Distinct().ToHashSet();
        var credentials = await LoadCredentialsAsync(tenantId, credentialIds, ct);

        return new CommunicationsAdminOperationContext(
            threads.ToDictionary(x => x.Id),
            members.ToDictionary(x => x.Id),
            messages.ToDictionary(x => x.Id),
            credentials);
    }

    private async Task<Dictionary<Guid, IdentityCredential>> LoadCredentialsAsync(
        Guid tenantId,
        IReadOnlySet<Guid> credentialIds,
        CancellationToken ct)
    {
        if (credentialIds.Count == 0)
        {
            return [];
        }

        var credentials = await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.IdentityInfo)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Where(x => credentialIds.Contains(x.Id))
            .OrderBy(x => x.UserName)
            .Take(Math.Max(credentialIds.Count, 1_000))
            .ToListAsync(ct);

        return credentials.ToDictionary(x => x.Id);
    }

    private async Task<int> CountTenantRowsAsync<T>(Guid tenantId, CancellationToken ct)
        where T : class, IHasTenantId, ISoftDeletable =>
        await dataContext.Query<T>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .CountAsync(ct);

    private async Task<bool> IsNotificationsEnabledAsync(Guid tenantId, CancellationToken ct)
    {
        var features = await dataContext.Query<TenantModuleFeature>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Where(x => x.IsEnabled)
            .Take(100)
            .ToListAsync(ct);

        return features.Any(feature =>
            string.Equals(feature.Key, TenantModuleFeatureKeys.Notifications, StringComparison.OrdinalIgnoreCase));
    }

    private static CommunicationsAdminUserRow CreateUserRow(
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

        return new CommunicationsAdminUserRow
        {
            CredentialId = credentialId,
            IdentityInfoId = credential?.IdentityInfoId,
            DisplayName = GetDisplayName(credential),
            UserName = credential?.UserName ?? "No username",
            IdentityLabel = credential?.IdentityInfo?.FullName ?? credential?.IdentityInfo?.IdentityName ?? "No identity record",
            IsOnline = credential?.IsOnline == true,
            LastSeen = NormalizeLastSeen(credential),
            ThreadCount = memberships.Select(x => x.MessageThreadId).Distinct().Count(),
            MessageCount = userMessages.Count,
            MutedThreadCount = memberships.Count(x => x.IsMuted),
            ArchivedThreadCount = memberships.Count(x => x.IsArchived),
            PendingInviteCount = invites.Count(x => x.InvitedCredentialId == credentialId && x.Status == MessageThreadInviteStatuses.Pending),
            BlockRelationshipCount = blocks.Count(x => x.BlockerCredentialId == credentialId || x.BlockedCredentialId == credentialId),
            RoleSummary = roleGroups.Count == 0 ? "Member: 0" : string.Join(", ", roleGroups)
        };
    }

    private static CommunicationsAdminCredentialContext ToCredentialContext(IdentityCredential credential) =>
        new()
        {
            Id = credential.Id,
            IdentityInfoId = credential.IdentityInfoId,
            DisplayName = GetDisplayName(credential),
            UserName = credential.UserName ?? "No username",
            IdentityLabel = credential.IdentityInfo?.FullName ?? credential.IdentityInfo?.IdentityName ?? "No identity record",
            UserAlias = credential.UserAlias,
            IsOnline = credential.IsOnline,
            IsEnabled = credential.IsEnabled,
            LastSeen = NormalizeLastSeen(credential),
            OnlineSince = credential.OnlineSince,
            Device = credential.Device,
            LastActivityType = credential.LastActivityType
        };

    private static CommunicationsAdminUserThreadRow ToUserThreadRow(
        MessageThreadMember member,
        MessageThread? thread) =>
        new()
        {
            ThreadId = member.MessageThreadId,
            ThreadName = thread is null ? member.MessageThreadId.ToString()[..8] : DisplayThreadName(thread),
            ThreadType = thread?.Type?.Name ?? "N/A",
            Role = string.IsNullOrWhiteSpace(member.Role) ? MessageThreadMemberRoles.Member : member.Role,
            IsMuted = member.IsMuted,
            IsArchived = member.IsArchived,
            LastSeenAt = member.LastSeenAt,
            JoinedAt = member.CreatedAt
        };

    private static CommunicationsAdminUserMessageRow ToUserMessageRow(
        Message message,
        MessageThread? thread) =>
        new()
        {
            MessageId = message.Id,
            ThreadId = message.MessageThreadId,
            ThreadName = thread is null ? message.MessageThreadId.ToString()[..8] : DisplayThreadName(thread),
            Preview = Truncate(message.Text, 140, "No message text"),
            HasParent = message.ParentMessageId is not null,
            MentionCount = CountMentions(message.MentionedCredentialIdsJson),
            CreatedAt = message.CreatedAt
        };

    private static CommunicationsAdminUserInviteRow ToUserInviteRow(
        MessageThreadInvite invite,
        MessageThread? thread,
        IReadOnlyDictionary<Guid, IdentityCredential> credentials) =>
        new()
        {
            InviteId = invite.Id,
            ThreadName = thread is null ? invite.MessageThreadId.ToString()[..8] : DisplayThreadName(thread),
            StatusText = InviteStatusText(invite.Status),
            StatusKey = InviteStatusKey(invite.Status),
            InvitedBy = GetCredentialLabel(invite.InvitedByCredentialId, credentials),
            CreatedAt = invite.CreatedAt
        };

    private static CommunicationsAdminUserBlockRow ToUserBlockRow(
        MessageBlock block,
        Guid credentialId,
        IReadOnlyDictionary<Guid, IdentityCredential> credentials)
    {
        var isOutgoing = block.BlockerCredentialId == credentialId;
        var otherCredentialId = isOutgoing ? block.BlockedCredentialId : block.BlockerCredentialId;

        return new CommunicationsAdminUserBlockRow
        {
            BlockId = block.Id,
            Direction = isOutgoing ? "Blocked by user" : "Blocks user",
            OtherCredential = GetCredentialLabel(otherCredentialId, credentials),
            IsEnabled = block.IsEnabled,
            CreatedAt = block.CreatedAt
        };
    }

    private static CommunicationsAdminThreadMemberRow ToThreadMemberRow(MessageThreadMember member) =>
        new()
        {
            MemberId = member.Id,
            CredentialId = member.CredentialId,
            DisplayName = GetMemberLabel(member),
            Alias = string.IsNullOrWhiteSpace(member.Alias) ? "No alias" : member.Alias,
            Group = member.Group?.Alias ?? "Default",
            Role = string.IsNullOrWhiteSpace(member.Role) ? MessageThreadMemberRoles.Member : member.Role,
            IsMuted = member.IsMuted,
            IsArchived = member.IsArchived,
            LastSeenAt = member.LastSeenAt,
            CreatedAt = member.CreatedAt
        };

    private static CommunicationsAdminThreadMessageRow ToThreadMessageRow(
        Message message,
        MessageThreadMember? member) =>
        new()
        {
            MessageId = message.Id,
            MemberId = message.MessageThreadMemberId,
            CredentialId = member?.CredentialId ?? Guid.Empty,
            Author = member is null ? message.MessageThreadMemberId.ToString()[..8] : GetMemberLabel(member),
            Preview = Truncate(message.Text, 160, "No message text"),
            IsReply = message.ParentMessageId is not null,
            MentionCount = CountMentions(message.MentionedCredentialIdsJson),
            CreatedAt = message.CreatedAt
        };

    private static CommunicationsAdminOutboxRow ToOutboxRow(
        MessageOutboxEvent item,
        CommunicationsAdminOperationContext context) =>
        new()
        {
            Id = item.Id,
            EventType = item.EventType,
            AggregateType = item.AggregateType,
            ThreadId = item.ThreadId,
            Thread = item.ThreadId is Guid threadId ? GetThreadLabel(threadId, context.Threads) : "Tenant event",
            Actor = item.ActorCredentialId is Guid actorId ? GetCredentialLabel(actorId, context.Credentials) : "System",
            Status = OutboxStatusText(item),
            StatusKey = OutboxStatusKey(item),
            Attempts = item.Attempts,
            OccurredAt = item.OccurredAt,
            ProcessedDisplay = item.ProcessedAt?.ToString("g") ?? "Not processed",
            LastError = string.IsNullOrWhiteSpace(item.LastError) ? "No error" : Truncate(item.LastError, 120, "No error")
        };

    private static CommunicationsAdminOperationInviteRow ToOperationInviteRow(
        MessageThreadInvite item,
        CommunicationsAdminOperationContext context) =>
        new()
        {
            Id = item.Id,
            ThreadId = item.MessageThreadId,
            Thread = GetThreadLabel(item.MessageThreadId, context.Threads),
            InvitedCredential = GetCredentialLabel(item.InvitedCredentialId, context.Credentials),
            InvitedBy = GetCredentialLabel(item.InvitedByCredentialId, context.Credentials),
            Status = InviteStatusText(item.Status),
            StatusKey = InviteStatusKey(item.Status),
            RespondedDisplay = item.RespondedAt?.ToString("g") ?? "Waiting",
            CreatedAt = item.CreatedAt
        };

    private static string OutboxStatusText(MessageOutboxEvent item)
    {
        if (item.ProcessedAt is not null)
            return "Processed";

        if (item.DeadLetteredAt is not null)
            return "Dead-lettered";

        if (item.LeaseExpiresAt is not null && item.LeaseExpiresAt > DateTime.UtcNow)
            return "Processing";

        if (item.NextAttemptAt is not null && item.NextAttemptAt > DateTime.UtcNow)
            return "Retry scheduled";

        return string.IsNullOrWhiteSpace(item.LastError) ? "Pending" : "Retry pending";
    }

    private static string OutboxStatusKey(MessageOutboxEvent item)
    {
        if (item.ProcessedAt is not null)
            return "active";

        if (item.DeadLetteredAt is not null)
            return "danger";

        if (item.LeaseExpiresAt is not null && item.LeaseExpiresAt > DateTime.UtcNow)
            return "info";

        return string.IsNullOrWhiteSpace(item.LastError) ? "warning" : "danger";
    }

    private static CommunicationsAdminPinRow ToPinRow(
        MessagePin item,
        CommunicationsAdminOperationContext context)
    {
        var message = context.Messages.GetValueOrDefault(item.MessageId);

        return new CommunicationsAdminPinRow
        {
            Id = item.Id,
            ThreadId = item.MessageThreadId,
            Thread = GetThreadLabel(item.MessageThreadId, context.Threads),
            MessagePreview = message is null ? "Message not loaded" : Truncate(message.Text, 120, "No message text"),
            PinnedBy = GetMemberLabel(item.PinnedByMemberId, context.Members),
            CreatedAt = item.CreatedAt
        };
    }

    private static CommunicationsAdminSavedRow ToSavedRow(
        MessageSaved item,
        CommunicationsAdminOperationContext context)
    {
        var message = context.Messages.GetValueOrDefault(item.MessageId);

        return new CommunicationsAdminSavedRow
        {
            Id = item.Id,
            ThreadId = message?.MessageThreadId,
            Thread = message is null ? "Unknown thread" : GetThreadLabel(message.MessageThreadId, context.Threads),
            MessagePreview = message is null ? "Message not loaded" : Truncate(message.Text, 120, "No message text"),
            SavedBy = GetMemberLabel(item.MessageThreadMemberId, context.Members),
            CreatedAt = item.CreatedAt
        };
    }

    private static CommunicationsAdminReportRow ToReportRow(
        MessageReport item,
        CommunicationsAdminOperationContext context)
    {
        var message = context.Messages.GetValueOrDefault(item.MessageId);
        var threadId = message?.MessageThreadId;

        return new CommunicationsAdminReportRow
        {
            Id = item.Id,
            Status = ReportStatusText(item.Status),
            StatusKey = ReportStatusKey(item.Status),
            Reason = string.IsNullOrWhiteSpace(item.Reason) ? "No reason" : item.Reason,
            Details = Truncate(item.Details, 160, "No details"),
            ThreadId = threadId,
            Thread = threadId is Guid id ? GetThreadLabel(id, context.Threads) : "Unknown thread",
            MessagePreview = message is null ? "Message not loaded" : Truncate(message.Text, 140, "No message text"),
            Reporter = GetMemberLabel(item.ReporterMemberId, context.Members),
            CreatedAt = item.CreatedAt
        };
    }

    private static CommunicationsAdminBlockRow ToModerationBlockRow(
        MessageBlock item,
        CommunicationsAdminOperationContext context) =>
        new()
        {
            Id = item.Id,
            Blocker = GetCredentialLabel(item.BlockerCredentialId, context.Credentials),
            Blocked = GetCredentialLabel(item.BlockedCredentialId, context.Credentials),
            Status = item.IsEnabled ? "Active" : "Disabled",
            StatusKey = item.IsEnabled ? "active" : "inactive",
            CreatedAt = item.CreatedAt
        };

    private static CommunicationsModerationRuleResponse ToModerationRuleRow(MessageModerationRule item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        MatchType = item.MatchType,
        Pattern = item.Pattern,
        Action = item.Action,
        Description = item.Description,
        IsEnabled = item.IsEnabled,
        CreatedAt = item.CreatedAt,
        ModifiedAt = item.ModifiedAt
    };

    private static List<CommunicationsAdminPolicyRow> CreatePolicyRows() =>
    [
        new()
        {
            Policy = "Global API rate limit",
            Scope = "Communications API",
            Status = "Active",
            StatusKey = "active",
            Details = "100 requests per minute per IP through XFramework rate limiting."
        },
        new()
        {
            Policy = "General API policy",
            Scope = "Named policy",
            Status = "Active",
            StatusKey = "active",
            Details = "60 requests per minute per IP where the API policy is applied."
        },
        new()
        {
            Policy = "Attachment validation",
            Scope = "Message file links",
            Status = "Active",
            StatusKey = "active",
            Details = "Tenant-owned StorageFile required; deleted files and executable/script extensions blocked."
        },
        new()
        {
            Policy = "DM blocking",
            Scope = "1:1 direct threads",
            Status = "Active",
            StatusKey = "active",
            Details = "Blocked credentials cannot create or continue direct-message threads with the blocker."
        }
    ];

    private static GridPage<T> ApplyGrid<T>(
        IEnumerable<T> source,
        CommunicationsAdminGridRequest request)
    {
        var rows = ApplySearch(source, request.SearchText);
        rows = ApplyFilters(rows, request.Filters);
        rows = ApplySort(rows, request.Sorts);

        var materialized = rows.ToList();
        var total = materialized.Count;
        var count = request.Count <= 0 ? 20 : request.Count;
        var paged = materialized
            .Skip(Math.Max(request.StartIndex, 0))
            .Take(count)
            .ToList();

        return new GridPage<T>(paged, total);
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
        IReadOnlyCollection<CommunicationsAdminFilter>? filters)
    {
        if (filters is null || filters.Count == 0)
        {
            return source;
        }

        foreach (var filter in filters)
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
        IReadOnlyCollection<CommunicationsAdminSort>? sorts)
    {
        var sort = sorts?.FirstOrDefault(x => !string.Equals(x.Direction, "none", StringComparison.OrdinalIgnoreCase));
        if (sort is null)
        {
            return source;
        }

        var property = FindProperty<T>(sort.Field);
        if (property is null)
        {
            return source;
        }

        return string.Equals(sort.Direction, "descending", StringComparison.OrdinalIgnoreCase)
            ? source.OrderByDescending(row => property.GetValue(row))
            : source.OrderBy(row => property.GetValue(row));
    }

    private static bool MatchesFilter(object? value, CommunicationsAdminFilter filter)
    {
        var text = Convert.ToString(value) ?? string.Empty;
        var filterValue = filter.Value ?? string.Empty;

        return filter.Operator switch
        {
            "equals" => string.Equals(text, filterValue, StringComparison.OrdinalIgnoreCase),
            "notEquals" => !string.Equals(text, filterValue, StringComparison.OrdinalIgnoreCase),
            "isEmpty" => string.IsNullOrWhiteSpace(text),
            "isNotEmpty" => !string.IsNullOrWhiteSpace(text),
            "notContains" => !text.Contains(filterValue, StringComparison.OrdinalIgnoreCase),
            "startsWith" => text.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase),
            "endsWith" => text.EndsWith(filterValue, StringComparison.OrdinalIgnoreCase),
            "isTrue" => bool.TryParse(text, out var parsed) && parsed,
            "isFalse" => bool.TryParse(text, out var parsed) && !parsed,
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

    private static Result<T> Failure<T>(Result<Guid> result) =>
        Result<T>.Failure(result.Message ?? "Tenant could not be resolved.", result.StatusCode);

    private Result<Guid> ResolveAdminTenantId(RequestMetadata? metadata)
    {
        var adminContext = requestContextResolver.ResolveAdmin(metadata);
        if (!adminContext.IsSuccess)
            return Result<Guid>.Failure(
                adminContext.Message ?? "Communications administration requires an admin context.",
                adminContext.StatusCode);

        return Result<Guid>.Success(adminContext.Data!.TenantId);
    }

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

    private static DateTime? NormalizeLastSeen(IdentityCredential? credential) =>
        credential is null || credential.LastSeen == default ? null : credential.LastSeen;

    private static string GetCredentialLabel(
        Guid credentialId,
        IReadOnlyDictionary<Guid, IdentityCredential> credentials) =>
        credentials.TryGetValue(credentialId, out var credential)
            ? GetDisplayName(credential)
            : credentialId.ToString()[..8];

    private static string GetMemberLabel(
        Guid memberId,
        IReadOnlyDictionary<Guid, MessageThreadMember> members) =>
        members.TryGetValue(memberId, out var member)
            ? GetMemberLabel(member)
            : memberId.ToString()[..8];

    private static string GetMemberLabel(MessageThreadMember member)
    {
        if (!string.IsNullOrWhiteSpace(member.Credential?.IdentityInfo?.FullName))
        {
            return member.Credential.IdentityInfo.FullName;
        }

        if (!string.IsNullOrWhiteSpace(member.Credential?.UserAlias))
        {
            return member.Credential.UserAlias;
        }

        if (!string.IsNullOrWhiteSpace(member.Credential?.UserName))
        {
            return member.Credential.UserName;
        }

        if (!string.IsNullOrWhiteSpace(member.Alias))
        {
            return member.Alias;
        }

        return member.CredentialId.ToString()[..8];
    }

    private static string GetThreadLabel(
        Guid threadId,
        IReadOnlyDictionary<Guid, MessageThread> threads) =>
        threads.TryGetValue(threadId, out var thread)
            ? DisplayThreadName(thread)
            : threadId.ToString()[..8];

    private static string DisplayThreadName(MessageThread thread) =>
        string.IsNullOrWhiteSpace(thread.Name) ? thread.Id.ToString()[..8] : thread.Name;

    private static string InviteStatusText(short status) => status switch
    {
        MessageThreadInviteStatuses.Accepted => "Accepted",
        MessageThreadInviteStatuses.Declined => "Declined",
        _ => "Pending"
    };

    private static string InviteStatusKey(short status) => status switch
    {
        MessageThreadInviteStatuses.Accepted => "active",
        MessageThreadInviteStatuses.Declined => "inactive",
        _ => "warning"
    };

    private static string ReportStatusText(short status) => status switch
    {
        MessageReportStatuses.Reviewed => "Reviewed",
        MessageReportStatuses.Dismissed => "Dismissed",
        MessageReportStatuses.Resolved => "Resolved",
        MessageReportStatuses.Escalated => "Escalated",
        _ => "Open"
    };

    private static string ReportStatusKey(short status) => status switch
    {
        MessageReportStatuses.Reviewed => "active",
        MessageReportStatuses.Dismissed => "inactive",
        MessageReportStatuses.Resolved => "active",
        MessageReportStatuses.Escalated => "warning",
        _ => "warning"
    };

    private static int CountMentions(string? mentionedCredentialIdsJson)
    {
        if (string.IsNullOrWhiteSpace(mentionedCredentialIdsJson))
        {
            return 0;
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(mentionedCredentialIdsJson)?.Count ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static string Truncate(string? value, int maxLength, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Length <= maxLength ? value : $"{value[..maxLength]}...";
    }

    private sealed record GridPage<T>(List<T> Items, int TotalItemCount);

    private sealed record CommunicationsAdminOperationContext(
        IReadOnlyDictionary<Guid, MessageThread> Threads,
        IReadOnlyDictionary<Guid, MessageThreadMember> Members,
        IReadOnlyDictionary<Guid, Message> Messages,
        IReadOnlyDictionary<Guid, IdentityCredential> Credentials);
}
