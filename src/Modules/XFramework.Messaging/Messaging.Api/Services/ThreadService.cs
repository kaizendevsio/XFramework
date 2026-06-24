using System.Net;
using System.Text.Json;
using IdentityServer.Domain.Shared;
using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts.Requests.Attachments;
using Messaging.Domain.Shared.Contracts.Requests.Delete;
using Messaging.Domain.Shared.Contracts.Requests.Edit;
using Messaging.Domain.Shared.Contracts.Requests.Reactions;
using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.DataContext;

namespace Messaging.Api.Services;
public sealed class ThreadService(
    IDataContext dataContext,
    IMessagingRequestContextResolver requestContextResolver,
    ILogger<ThreadService> logger
) : IThreadService
{
    private const decimal MaxAttachmentSizeBytes = 25 * 1024 * 1024;
    private static readonly JsonSerializerOptions OutboxJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<CreateThreadResponse>> CreateThreadAsync(CreateThreadRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CreateThreadResponse>(callerResult);

            var caller = callerResult.Data!;
            var allMemberIds = request.InitialMemberCredentialIds
                .Append(caller.CredentialId)
                .Distinct()
                .ToList();

            var threadTypeExists = await dataContext.Query<MessageThreadType>()
                .Where(t => t.Id == request.TypeId)
                .Where(t => !t.IsDeleted && t.IsEnabled)
                .AnyAsync(ct);

            if (!threadTypeExists)
                return Result<CreateThreadResponse>.NotFound("Thread type not found");

            var existingMembers = await dataContext.Query<IdentityCredential>()
                .Where(c => allMemberIds.Contains(c.Id))
                .Where(c => c.TenantId == caller.TenantId)
                .Where(c => !c.IsDeleted && c.IsEnabled)
                .ToListAsync(ct);

            if (existingMembers.Count != allMemberIds.Count)
                return Result<CreateThreadResponse>.NotFound("One or more initial member credentials were not found");

            var thread = new MessageThread
            {
                Id = Guid.NewGuid(),
                TenantId = caller.TenantId,
                Name = request.Name,
                Description = request.Description ?? string.Empty,
                TypeId = request.TypeId,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };

            dataContext.Add(thread);

            var defaultGroup = CreateDefaultThreadMemberGroup(thread.Id, caller.TenantId);
            dataContext.Add(defaultGroup);

            foreach (var credentialId in allMemberIds)
            {
                var member = new MessageThreadMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = caller.TenantId,
                    MessageThreadId = thread.Id,
                    CredentialId = credentialId,
                    GroupId = defaultGroup.Id,
                    Alias = string.Empty,
                    Emoji = string.Empty,
                    Description = string.Empty,
                    Status = 1, // Active
                    Role = credentialId == caller.CredentialId
                        ? MessageThreadMemberRoles.Owner
                        : MessageThreadMemberRoles.Member,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow,
                    ConcurrencyStamp = Guid.NewGuid()
                };

                dataContext.Add(member);

                if (credentialId == caller.CredentialId)
                    await AddAdminRoleBindingsAsync(member, caller.CredentialId, ct);
            }

            AddOutboxEvent(
                MessageRealtimeEvents.ThreadCreated,
                caller.TenantId,
                thread.Id,
                thread.Id,
                nameof(MessageThread),
                caller.CredentialId,
                new
                {
                    thread.Id,
                    thread.Name,
                    MemberCredentialIds = allMemberIds
                });

            await dataContext.SaveChangesAsync(ct);

            return Result<CreateThreadResponse>.Success(new CreateThreadResponse
            {
                ThreadId = thread.Id
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating thread: {Name}", request.Name);
            return Result<CreateThreadResponse>.Failure($"Error creating thread: {ex.Message}");
        }
    }

    public async Task<Result<CreateThreadResponse>> CreateDirectThreadAsync(CreateDirectThreadRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CreateThreadResponse>(callerResult);

            var caller = callerResult.Data!;
            if (request.OtherCredentialId == caller.CredentialId)
                return Result<CreateThreadResponse>.Failure("Direct thread requires another credential", 400);

            var isBlocked = await IsBlockedAsync(caller.TenantId, caller.CredentialId, request.OtherCredentialId, ct);
            if (isBlocked)
                return Result<CreateThreadResponse>.Forbidden("Direct messaging is blocked between these credentials");

            var otherCredentialExists = await dataContext.Query<IdentityCredential>()
                .Where(c => c.Id == request.OtherCredentialId)
                .Where(c => c.TenantId == caller.TenantId)
                .Where(c => !c.IsDeleted && c.IsEnabled)
                .AnyAsync(ct);
            if (!otherCredentialExists)
                return Result<CreateThreadResponse>.NotFound("Credential not found");

            var existingThreadId = await FindDirectThreadAsync(caller.TenantId, caller.CredentialId, request.OtherCredentialId, ct);
            if (existingThreadId is Guid existing)
                return Result<CreateThreadResponse>.Success(new CreateThreadResponse { ThreadId = existing });

            var typeId = request.TypeId ?? await ResolveChatThreadTypeIdAsync(caller.TenantId, ct);
            if (typeId is null)
                return Result<CreateThreadResponse>.NotFound("Chat thread type not found");

            var thread = new MessageThread
            {
                Id = Guid.NewGuid(),
                TenantId = caller.TenantId,
                Name = request.Name ?? "Direct message",
                Description = string.Empty,
                TypeId = typeId.Value,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };
            dataContext.Add(thread);

            var group = CreateDefaultThreadMemberGroup(thread.Id, caller.TenantId);
            dataContext.Add(group);

            foreach (var (credentialId, role) in new[]
                     {
                         (caller.CredentialId, MessageThreadMemberRoles.Owner),
                         (request.OtherCredentialId, MessageThreadMemberRoles.Member)
                     })
            {
                dataContext.Add(new MessageThreadMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = caller.TenantId,
                    MessageThreadId = thread.Id,
                    CredentialId = credentialId,
                    GroupId = group.Id,
                    Alias = string.Empty,
                    Emoji = string.Empty,
                    Description = string.Empty,
                    Status = 1,
                    Role = role,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow,
                    ConcurrencyStamp = Guid.NewGuid()
                });
            }

            AddOutboxEvent(
                MessageRealtimeEvents.ThreadCreated,
                caller.TenantId,
                thread.Id,
                thread.Id,
                nameof(MessageThread),
                caller.CredentialId,
                new
                {
                    thread.Id,
                    thread.Name,
                    IsDirect = true,
                    MemberCredentialIds = new[] { caller.CredentialId, request.OtherCredentialId }
                });

            await dataContext.SaveChangesAsync(ct);
            return Result<CreateThreadResponse>.Success(new CreateThreadResponse { ThreadId = thread.Id }, 201);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating direct thread for credential {CredentialId}", request.OtherCredentialId);
            return Result<CreateThreadResponse>.Failure($"Error creating direct thread: {ex.Message}");
        }
    }

    public async Task<Result<GetThreadListResponse>> GetThreadListAsync(GetThreadListRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<GetThreadListResponse>(callerResult);

            var caller = callerResult.Data!;
            var pageIndex = request.PageIndex < 0 ? 0 : request.PageIndex;
            var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);

            // Get memberships for this credential
            var memberships = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .ToListAsync(ct);

            var memberThreadIds = memberships.Select(m => m.MessageThreadId).Distinct().ToList();
            var totalCount = memberThreadIds.Count;

            var threads = await dataContext.Query<MessageThread>()
                .Where(t => memberThreadIds.Contains(t.Id))
                .Where(t => t.TenantId == caller.TenantId)
                .Where(t => !t.IsDeleted)
                .OrderByDescending(t => t.CreatedAt)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var threadIds = threads.Select(t => t.Id).ToList();

            // Get member counts per thread using GroupByAsync
            var memberGroups = await dataContext.Query<MessageThreadMember>()
                .Where(m => threadIds.Contains(m.MessageThreadId))
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .GroupByAsync(m => m.MessageThreadId, ct);

            var memberCountMap = memberGroups.ToDictionary(g => g.Key, g => g.Items.Count);

            // Get messages for these threads to find last message per thread
            var threadMessages = await dataContext.Query<Message>()
                .Where(m => threadIds.Contains(m.MessageThreadId))
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync(ct);

            var lastMessageMap = threadMessages
                .GroupBy(m => m.MessageThreadId)
                .ToDictionary(g => g.Key, g => g.First());

            var unreadMap = await GetUnreadCountMapAsync(caller.TenantId, caller.CredentialId, threadIds, ct);
            var membershipMap = memberships
                .GroupBy(m => m.MessageThreadId)
                .ToDictionary(g => g.Key, g => g.First());

            var items = threads.Select(t =>
            {
                lastMessageMap.TryGetValue(t.Id, out var lastMsg);
                memberCountMap.TryGetValue(t.Id, out var count);
                unreadMap.TryGetValue(t.Id, out var unreadCount);
                membershipMap.TryGetValue(t.Id, out var membership);

                return new ThreadListItemResponse
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    TypeId = t.TypeId,
                    CreatedAt = t.CreatedAt,
                    MemberCount = count,
                    LastMessagePreview = lastMsg?.Text?.Length > 100
                        ? lastMsg.Text[..100] + "..."
                        : lastMsg?.Text,
                    LastMessageAt = lastMsg?.CreatedAt,
                    UnreadCount = unreadCount,
                    IsMuted = membership?.IsMuted == true,
                    IsArchived = membership?.IsArchived == true
                };
            }).ToList();

            return Result<GetThreadListResponse>.Success(new GetThreadListResponse
            {
                Items = items,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting thread list");
            return Result<GetThreadListResponse>.Failure($"Error getting thread list: {ex.Message}");
        }
    }

    public async Task<Result<GetThreadResponse>> GetThreadAsync(GetThreadRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<GetThreadResponse>(callerResult);

            var caller = callerResult.Data!;

            var thread = await dataContext.Query<MessageThread>()
                .Where(t => t.Id == request.Id)
                .Where(t => t.TenantId == caller.TenantId)
                .Where(t => !t.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (thread is null)
            {
                return Result<GetThreadResponse>.NotFound("Thread not found");
            }

            var requesterMember = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == thread.Id)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (requesterMember is null)
                return Result<GetThreadResponse>.Failure("Requester is not a member of this thread", 403);

            var members = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == thread.Id)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .ToListAsync(ct);

            return Result<GetThreadResponse>.Success(new GetThreadResponse
            {
                Id = thread.Id,
                Name = thread.Name,
                Description = thread.Description,
                TypeId = thread.TypeId,
                CreatedAt = thread.CreatedAt,
                Members = members.Select(m => new ThreadMemberResponse
                {
                    Id = m.Id,
                    CredentialId = m.CredentialId,
                    Alias = m.Alias,
                    Status = m.Status,
                    JoinedAt = m.CreatedAt
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting thread: {ThreadId}", request.Id);
            return Result<GetThreadResponse>.Failure($"Error getting thread: {ex.Message}");
        }
    }

    public async Task<Result<GetUnreadCountsResponse>> GetUnreadCountsAsync(GetUnreadCountsRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<GetUnreadCountsResponse>(callerResult);

            var caller = callerResult.Data!;
            var memberships = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .ToListAsync(ct);

            var threadIds = memberships
                .Select(m => m.MessageThreadId)
                .Distinct()
                .ToList();

            var unreadMap = await GetUnreadCountMapAsync(caller.TenantId, caller.CredentialId, threadIds, ct);
            var items = unreadMap
                .Select(pair => new UnreadThreadCountResponse
                {
                    ThreadId = pair.Key,
                    UnreadCount = pair.Value
                })
                .ToList();

            return Result<GetUnreadCountsResponse>.Success(new GetUnreadCountsResponse
            {
                Threads = items,
                TotalUnreadCount = items.Sum(item => item.UnreadCount)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting unread counts");
            return Result<GetUnreadCountsResponse>.Failure($"Error getting unread counts: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> UpdateThreadAsync(UpdateThreadRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var thread = await dataContext.Query<MessageThread>()
                .Where(t => t.Id == request.ThreadId)
                .Where(t => t.TenantId == caller.TenantId)
                .Where(t => !t.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (thread is null)
                return Result<CmdResponse>.NotFound("Thread not found");

            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            if (!await CanManageThreadAsync(member, ct))
                return Result<CmdResponse>.Forbidden("Only thread admins can update this thread");

            if (request.Name is not null)
                thread.Name = request.Name;

            if (request.Description is not null)
                thread.Description = request.Description;

            thread.ModifiedAt = DateTime.UtcNow;

            AddOutboxEvent(
                MessageRealtimeEvents.ThreadUpdated,
                thread.TenantId,
                thread.Id,
                thread.Id,
                nameof(MessageThread),
                caller.CredentialId,
                new
                {
                    thread.Id,
                    thread.Name,
                    thread.Description
                });

            dataContext.Update(thread);
            await dataContext.SaveChangesAsync(ct);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Thread updated successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating thread {ThreadId}", request.ThreadId);
            return Result<CmdResponse>.Failure($"Error updating thread: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> LeaveThreadAsync(LeaveThreadRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var member = await GetActiveMemberAsync(caller.TenantId, request.ThreadId, caller.CredentialId, ct);
            if (member is null)
                return Result<CmdResponse>.Forbidden("Requester is not a member of this thread");

            var activeMembers = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(ct);

            if (activeMembers.Count <= 1)
                return Result<CmdResponse>.Failure("Cannot leave as the last member of a thread", 400);

            member.IsDeleted = true;
            member.IsEnabled = false;
            member.DeletedAt = DateTime.UtcNow;
            member.ModifiedAt = DateTime.UtcNow;
            dataContext.Update(member);

            if (member.Role == MessageThreadMemberRoles.Owner &&
                activeMembers.Where(m => m.Id != member.Id).All(m => m.Role != MessageThreadMemberRoles.Owner))
            {
                var replacementOwner = activeMembers.First(m => m.Id != member.Id);
                replacementOwner.Role = MessageThreadMemberRoles.Owner;
                replacementOwner.ModifiedAt = DateTime.UtcNow;
                dataContext.Update(replacementOwner);
            }

            AddOutboxEvent(
                MessageRealtimeEvents.ThreadLeft,
                member.TenantId,
                request.ThreadId,
                member.Id,
                nameof(MessageThreadMember),
                caller.CredentialId,
                new
                {
                    request.ThreadId,
                    MemberId = member.Id,
                    member.CredentialId
                });

            await dataContext.SaveChangesAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Thread left successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error leaving thread {ThreadId}", request.ThreadId);
            return Result<CmdResponse>.Failure($"Error leaving thread: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> MuteThreadAsync(MuteThreadRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var member = await GetActiveMemberAsync(caller.TenantId, request.ThreadId, caller.CredentialId, ct);
            if (member is null)
                return Result<CmdResponse>.Forbidden("Requester is not a member of this thread");

            member.IsMuted = request.IsMuted;
            member.MutedAt = request.IsMuted ? DateTime.UtcNow : null;
            member.ModifiedAt = DateTime.UtcNow;
            dataContext.Update(member);

            AddOutboxEvent(
                MessageRealtimeEvents.ThreadMuted,
                member.TenantId,
                request.ThreadId,
                member.Id,
                nameof(MessageThreadMember),
                caller.CredentialId,
                new
                {
                    request.ThreadId,
                    MemberId = member.Id,
                    request.IsMuted
                });

            await dataContext.SaveChangesAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = request.IsMuted ? "Thread muted successfully" : "Thread unmuted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating mute state for thread {ThreadId}", request.ThreadId);
            return Result<CmdResponse>.Failure($"Error updating mute state: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> ArchiveThreadAsync(ArchiveThreadRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var member = await GetActiveMemberAsync(caller.TenantId, request.ThreadId, caller.CredentialId, ct);
            if (member is null)
                return Result<CmdResponse>.Forbidden("Requester is not a member of this thread");

            member.IsArchived = request.IsArchived;
            member.ArchivedAt = request.IsArchived ? DateTime.UtcNow : null;
            member.ModifiedAt = DateTime.UtcNow;
            dataContext.Update(member);

            AddOutboxEvent(
                MessageRealtimeEvents.ThreadArchived,
                member.TenantId,
                request.ThreadId,
                member.Id,
                nameof(MessageThreadMember),
                caller.CredentialId,
                new
                {
                    request.ThreadId,
                    MemberId = member.Id,
                    request.IsArchived
                });

            await dataContext.SaveChangesAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = request.IsArchived ? "Thread archived successfully" : "Thread unarchived successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating archive state for thread {ThreadId}", request.ThreadId);
            return Result<CmdResponse>.Failure($"Error updating archive state: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> AddThreadMemberAsync(AddThreadMemberRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;

            // Validate thread exists
            var thread = await dataContext.Query<MessageThread>()
                .Where(t => t.Id == request.ThreadId)
                .Where(t => t.TenantId == caller.TenantId)
                .Where(t => !t.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (thread is null)
            {
                return Result<CmdResponse>.NotFound("Thread not found");
            }

            var actorMember = await GetActiveMemberAsync(caller.TenantId, request.ThreadId, caller.CredentialId, ct);
            if (actorMember is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            if (!await CanManageThreadAsync(actorMember, ct))
                return Result<CmdResponse>.Forbidden("Only thread admins can add members");

            // Validate credential exists
            var credential = await dataContext.Query<IdentityCredential>()
                .Where(c => c.Id == request.CredentialId)
                .Where(c => c.TenantId == caller.TenantId)
                .Where(c => !c.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (credential is null)
            {
                return Result<CmdResponse>.NotFound("Credential not found");
            }

            // Check not already a member
            var existingMember = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == request.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (existingMember is not null)
            {
                return Result<CmdResponse>.Conflict("Credential is already a member of this thread");
            }

            var group = await dataContext.Query<MessageThreadMemberGroup>()
                .Where(g => g.MessageThreadId == request.ThreadId)
                .Where(g => g.TenantId == caller.TenantId)
                .Where(g => !g.IsDeleted && g.IsEnabled)
                .OrderBy(g => g.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (group is null)
            {
                group = CreateDefaultThreadMemberGroup(request.ThreadId, thread.TenantId);
                dataContext.Add(group);
            }

            var member = new MessageThreadMember
            {
                Id = Guid.NewGuid(),
                TenantId = thread.TenantId,
                MessageThreadId = request.ThreadId,
                CredentialId = request.CredentialId,
                GroupId = group.Id,
                Alias = string.Empty,
                Emoji = string.Empty,
                Description = string.Empty,
                Status = 1, // Active
                Role = MessageThreadMemberRoles.Member,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };

            dataContext.Add(member);
            AddOutboxEvent(
                MessageRealtimeEvents.ThreadMemberAdded,
                thread.TenantId,
                thread.Id,
                member.Id,
                nameof(MessageThreadMember),
                caller.CredentialId,
                new
                {
                    ThreadId = thread.Id,
                    MemberId = member.Id,
                    member.CredentialId
                });

            await dataContext.SaveChangesAsync(ct);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Member added successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding member {CredentialId} to thread {ThreadId}", request.CredentialId, request.ThreadId);
            return Result<CmdResponse>.Failure($"Error adding member: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> RemoveThreadMemberAsync(RemoveThreadMemberRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var actorMember = await GetActiveMemberAsync(caller.TenantId, request.ThreadId, caller.CredentialId, ct);
            if (actorMember is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            // Find the member
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == request.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (member is null)
            {
                return Result<CmdResponse>.NotFound("Member not found in this thread");
            }

            if (member.Id != actorMember.Id && !await CanManageThreadAsync(actorMember, ct))
                return Result<CmdResponse>.Forbidden("Only thread admins can remove other members");

            // Validate can't remove if they are the last member
            var memberCount = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .CountAsync(ct);

            if (memberCount <= 1)
            {
                return Result<CmdResponse>.Failure("Cannot remove the last member from a thread");
            }

            // Soft delete the member
            member.IsDeleted = true;
            member.DeletedAt = DateTime.UtcNow;
            dataContext.Update(member);

            AddOutboxEvent(
                MessageRealtimeEvents.ThreadMemberRemoved,
                member.TenantId,
                request.ThreadId,
                member.Id,
                nameof(MessageThreadMember),
                caller.CredentialId,
                new
                {
                    request.ThreadId,
                    MemberId = member.Id,
                    member.CredentialId
                });

            await dataContext.SaveChangesAsync(ct);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Member removed successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing member {CredentialId} from thread {ThreadId}", request.CredentialId, request.ThreadId);
            return Result<CmdResponse>.Failure($"Error removing member: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> CreateThreadInviteAsync(CreateThreadInviteRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var actorMember = await GetActiveMemberAsync(caller.TenantId, request.ThreadId, caller.CredentialId, ct);
            if (actorMember is null)
                return Result<CmdResponse>.Forbidden("Requester is not a member of this thread");

            if (!await CanManageThreadAsync(actorMember, ct))
                return Result<CmdResponse>.Forbidden("Only thread admins can invite members");

            var credentialExists = await dataContext.Query<IdentityCredential>()
                .Where(c => c.Id == request.CredentialId)
                .Where(c => c.TenantId == caller.TenantId)
                .Where(c => !c.IsDeleted && c.IsEnabled)
                .AnyAsync(ct);
            if (!credentialExists)
                return Result<CmdResponse>.NotFound("Credential not found");

            var alreadyMember = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == request.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .AnyAsync(ct);
            if (alreadyMember)
                return Result<CmdResponse>.Conflict("Credential is already a member of this thread");

            var pendingInvite = await dataContext.Query<MessageThreadInvite>()
                .Where(i => i.MessageThreadId == request.ThreadId)
                .Where(i => i.InvitedCredentialId == request.CredentialId)
                .Where(i => i.TenantId == caller.TenantId)
                .Where(i => i.Status == MessageThreadInviteStatuses.Pending)
                .Where(i => !i.IsDeleted && i.IsEnabled)
                .FirstOrDefaultAsync(ct);
            if (pendingInvite is not null)
                return Result<CmdResponse>.Conflict("A pending invite already exists for this credential");

            var invite = new MessageThreadInvite
            {
                Id = Guid.NewGuid(),
                TenantId = caller.TenantId,
                MessageThreadId = request.ThreadId,
                InvitedCredentialId = request.CredentialId,
                InvitedByCredentialId = caller.CredentialId,
                Status = MessageThreadInviteStatuses.Pending,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };

            dataContext.Add(invite);
            AddOutboxEvent(
                MessageRealtimeEvents.ThreadInviteCreated,
                caller.TenantId,
                request.ThreadId,
                invite.Id,
                nameof(MessageThreadInvite),
                caller.CredentialId,
                new
                {
                    request.ThreadId,
                    InviteId = invite.Id,
                    invite.InvitedCredentialId,
                    invite.InvitedByCredentialId
                });

            await dataContext.SaveChangesAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Created,
                Message = "Thread invite created successfully"
            }, 201);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating invite for thread {ThreadId}", request.ThreadId);
            return Result<CmdResponse>.Failure($"Error creating thread invite: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> RespondThreadInviteAsync(RespondThreadInviteRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var invite = await dataContext.Query<MessageThreadInvite>()
                .Where(i => i.Id == request.InviteId)
                .Where(i => i.MessageThreadId == request.ThreadId)
                .Where(i => i.InvitedCredentialId == caller.CredentialId)
                .Where(i => i.TenantId == caller.TenantId)
                .Where(i => !i.IsDeleted && i.IsEnabled)
                .FirstOrDefaultAsync(ct);
            if (invite is null)
                return Result<CmdResponse>.NotFound("Thread invite not found");

            if (invite.Status != MessageThreadInviteStatuses.Pending)
                return Result<CmdResponse>.Conflict("Thread invite has already been resolved");

            invite.Status = request.Accept
                ? MessageThreadInviteStatuses.Accepted
                : MessageThreadInviteStatuses.Declined;
            invite.RespondedAt = DateTime.UtcNow;
            invite.ModifiedAt = DateTime.UtcNow;
            dataContext.Update(invite);

            MessageThreadMember? member = null;
            if (request.Accept)
            {
                member = await GetActiveMemberAsync(caller.TenantId, request.ThreadId, caller.CredentialId, ct);
                if (member is null)
                {
                    var group = await dataContext.Query<MessageThreadMemberGroup>()
                        .Where(g => g.MessageThreadId == request.ThreadId)
                        .Where(g => g.TenantId == caller.TenantId)
                        .Where(g => !g.IsDeleted && g.IsEnabled)
                        .OrderBy(g => g.CreatedAt)
                        .FirstOrDefaultAsync(ct);

                    if (group is null)
                    {
                        group = CreateDefaultThreadMemberGroup(request.ThreadId, caller.TenantId);
                        dataContext.Add(group);
                    }

                    member = new MessageThreadMember
                    {
                        Id = Guid.NewGuid(),
                        TenantId = caller.TenantId,
                        MessageThreadId = request.ThreadId,
                        CredentialId = caller.CredentialId,
                        GroupId = group.Id,
                        Alias = string.Empty,
                        Emoji = string.Empty,
                        Description = string.Empty,
                        Status = 1,
                        Role = MessageThreadMemberRoles.Member,
                        IsEnabled = true,
                        CreatedAt = DateTime.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                    };
                    dataContext.Add(member);
                }
            }

            AddOutboxEvent(
                request.Accept ? MessageRealtimeEvents.ThreadInviteAccepted : MessageRealtimeEvents.ThreadInviteDeclined,
                caller.TenantId,
                request.ThreadId,
                invite.Id,
                nameof(MessageThreadInvite),
                caller.CredentialId,
                new
                {
                    request.ThreadId,
                    InviteId = invite.Id,
                    invite.InvitedCredentialId,
                    MemberId = member?.Id
                });

            await dataContext.SaveChangesAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = request.Accept ? "Thread invite accepted successfully" : "Thread invite declined successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error responding to invite {InviteId} for thread {ThreadId}", request.InviteId, request.ThreadId);
            return Result<CmdResponse>.Failure($"Error responding to thread invite: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> UpdateThreadMemberRoleAsync(UpdateThreadMemberRoleRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var actorMember = await GetActiveMemberAsync(caller.TenantId, request.ThreadId, caller.CredentialId, ct);
            if (actorMember is null)
                return Result<CmdResponse>.Forbidden("Requester is not a member of this thread");

            var targetMember = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.Id == request.MemberId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);
            if (targetMember is null)
                return Result<CmdResponse>.NotFound("Thread member not found");

            var normalizedRole = NormalizeRole(request.Role);
            if (normalizedRole == MessageThreadMemberRoles.Owner && actorMember.Role != MessageThreadMemberRoles.Owner)
                return Result<CmdResponse>.Forbidden("Only a thread owner can transfer ownership");

            if (normalizedRole != MessageThreadMemberRoles.Owner && !await CanManageThreadAsync(actorMember, ct))
                return Result<CmdResponse>.Forbidden("Only thread admins can update member roles");

            var owners = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => m.Role == MessageThreadMemberRoles.Owner)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .ToListAsync(ct);

            if (targetMember.Role == MessageThreadMemberRoles.Owner &&
                normalizedRole != MessageThreadMemberRoles.Owner &&
                owners.All(owner => owner.Id == targetMember.Id))
                return Result<CmdResponse>.Failure("Cannot remove the last owner from a thread", 400);

            if (normalizedRole == MessageThreadMemberRoles.Owner)
            {
                foreach (var owner in owners.Where(owner => owner.Id != targetMember.Id))
                {
                    owner.Role = MessageThreadMemberRoles.Admin;
                    owner.ModifiedAt = DateTime.UtcNow;
                    dataContext.Update(owner);
                }
            }

            targetMember.Role = normalizedRole;
            targetMember.ModifiedAt = DateTime.UtcNow;
            dataContext.Update(targetMember);

            AddOutboxEvent(
                MessageRealtimeEvents.ThreadMemberRoleChanged,
                targetMember.TenantId,
                request.ThreadId,
                targetMember.Id,
                nameof(MessageThreadMember),
                caller.CredentialId,
                new
                {
                    request.ThreadId,
                    MemberId = targetMember.Id,
                    targetMember.CredentialId,
                    Role = normalizedRole
                });

            await dataContext.SaveChangesAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Thread member role updated successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating member {MemberId} role in thread {ThreadId}", request.MemberId, request.ThreadId);
            return Result<CmdResponse>.Failure($"Error updating thread member role: {ex.Message}");
        }
    }

    public async Task<Result<CreateThreadMessageResponse>> CreateThreadMessageAsync(CreateThreadMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CreateThreadMessageResponse>(callerResult);

            var caller = callerResult.Data!;

            // Validate thread exists
            var thread = await dataContext.Query<MessageThread>()
                .Where(t => t.Id == request.ThreadId)
                .Where(t => t.TenantId == caller.TenantId)
                .Where(t => !t.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (thread is null)
            {
                return Result<CreateThreadMessageResponse>.NotFound("Thread not found");
            }

            // Validate sender is a member of the thread
            var senderMember = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (senderMember is null)
            {
                return Result<CreateThreadMessageResponse>.Failure("Sender is not a member of this thread", 403);
            }

            var activeThreadMembers = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .ToListAsync(ct);

            if (activeThreadMembers.Count == 2)
            {
                var otherCredentialId = activeThreadMembers
                    .First(m => m.CredentialId != caller.CredentialId)
                    .CredentialId;

                if (await IsBlockedAsync(caller.TenantId, caller.CredentialId, otherCredentialId, ct))
                    return Result<CreateThreadMessageResponse>.Forbidden("Direct messaging is blocked between these credentials");
            }

            if (request.ParentMessageId is Guid parentMessageId)
            {
                var parentExists = await dataContext.Query<Message>()
                    .Where(m => m.Id == parentMessageId)
                    .Where(m => m.MessageThreadId == request.ThreadId)
                    .Where(m => m.TenantId == caller.TenantId)
                    .Where(m => !m.IsDeleted)
                    .AnyAsync(ct);
                if (!parentExists)
                    return Result<CreateThreadMessageResponse>.NotFound("Parent message not found in this thread");
            }

            var mentionedCredentialIds = request.MentionedCredentialIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
            if (mentionedCredentialIds.Count > 0)
            {
                var activeCredentialIds = activeThreadMembers.Select(m => m.CredentialId).ToHashSet();
                if (mentionedCredentialIds.Any(id => !activeCredentialIds.Contains(id)))
                    return Result<CreateThreadMessageResponse>.Failure("Mentioned credentials must be active members of this thread", 400);
            }

            var message = new Message
            {
                Id = Guid.NewGuid(),
                TenantId = thread.TenantId,
                MessageThreadId = request.ThreadId,
                MessageThreadMemberId = senderMember.Id,
                Text = request.Text,
                ParentMessageId = request.ParentMessageId,
                MentionedCredentialIdsJson = JsonSerializer.Serialize(mentionedCredentialIds, OutboxJsonOptions),
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };

            dataContext.Add(message);
            AddOutboxEvent(
                MessageRealtimeEvents.MessageCreated,
                thread.TenantId,
                thread.Id,
                message.Id,
                nameof(Message),
                caller.CredentialId,
                new
                {
                    ThreadId = thread.Id,
                    MessageId = message.Id,
                    SenderMemberId = senderMember.Id,
                    message.ParentMessageId,
                    MentionedCredentialIds = mentionedCredentialIds
                });

            await dataContext.SaveChangesAsync(ct);

            return Result<CreateThreadMessageResponse>.Success(new CreateThreadMessageResponse
            {
                MessageId = message.Id
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating message in thread {ThreadId}", request.ThreadId);
            return Result<CreateThreadMessageResponse>.Failure($"Error creating message: {ex.Message}");
        }
    }

    public async Task<Result<GetThreadMessagesResponse>> GetThreadMessagesAsync(GetThreadMessagesRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<GetThreadMessagesResponse>(callerResult);

            var caller = callerResult.Data!;
            var pageIndex = request.PageIndex < 0 ? 0 : request.PageIndex;
            var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);

            // Validate requester is a member
            var requesterMember = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (requesterMember is null)
            {
                return Result<GetThreadMessagesResponse>.Failure("Requester is not a member of this thread", 403);
            }

            var totalCount = await dataContext.Query<Message>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .CountAsync(ct);

            var messages = await dataContext.Query<Message>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // Auto-create "Delivered" records for messages this member hasn't seen
            var fetchedMessageIds = messages.Select(m => m.Id).ToList();
            var existingDeliveries = await dataContext.Query<MessageDelivery>()
                .Where(d => d.MessageThreadMemberId == requesterMember.Id)
                .Where(d => d.TenantId == caller.TenantId)
                .Where(d => fetchedMessageIds.Contains(d.MessageId))
                .Where(d => !d.IsDeleted)
                .ToListAsync(ct);
            var existingDeliveryMessageIds = existingDeliveries.Select(d => d.MessageId).ToList();

            var undeliveredIds = fetchedMessageIds.Except(existingDeliveryMessageIds).ToList();
            if (undeliveredIds.Count > 0)
            {
                foreach (var msgId in undeliveredIds)
                {
                    dataContext.Add(new MessageDelivery
                    {
                        Id = Guid.NewGuid(),
                        TenantId = requesterMember.TenantId,
                        MessageThreadMemberId = requesterMember.Id,
                        MessageId = msgId,
                        TypeId = MessageDeliveryTypes.Delivered,
                        IsEnabled = true,
                        CreatedAt = DateTime.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                    });
                }
                await dataContext.SaveChangesAsync(ct);
            }

            // Get the member info for senders
            var memberIds = messages.Select(m => m.MessageThreadMemberId).Distinct().ToList();
            var members = await dataContext.Query<MessageThreadMember>()
                .Where(m => memberIds.Contains(m.Id))
                .Where(m => m.TenantId == caller.TenantId)
                .ToListAsync(ct);

            var memberMap = members.ToDictionary(m => m.Id);
            var messageIds = messages.Select(m => m.Id).ToList();
            var pins = await dataContext.Query<MessagePin>()
                .Where(p => messageIds.Contains(p.MessageId))
                .Where(p => p.MessageThreadId == request.ThreadId)
                .Where(p => p.TenantId == caller.TenantId)
                .Where(p => !p.IsDeleted && p.IsEnabled)
                .ToListAsync(ct);

            var saved = await dataContext.Query<MessageSaved>()
                .Where(s => messageIds.Contains(s.MessageId))
                .Where(s => s.MessageThreadMemberId == requesterMember.Id)
                .Where(s => s.TenantId == caller.TenantId)
                .Where(s => !s.IsDeleted && s.IsEnabled)
                .ToListAsync(ct);

            var pinnedSet = pins.Select(p => p.MessageId).ToHashSet();
            var savedSet = saved.Select(s => s.MessageId).ToHashSet();

            var items = messages.Select(m =>
            {
                memberMap.TryGetValue(m.MessageThreadMemberId, out var sender);
                return new ThreadMessageItemResponse
                {
                    Id = m.Id,
                    Text = m.Text,
                    SenderCredentialId = sender?.CredentialId ?? Guid.Empty,
                    SenderAlias = sender?.Alias ?? string.Empty,
                    CreatedAt = m.CreatedAt,
                    ParentMessageId = m.ParentMessageId,
                    MentionedCredentialIds = DeserializeMentionedCredentialIds(m.MentionedCredentialIdsJson),
                    IsPinned = pinnedSet.Contains(m.Id),
                    IsSaved = savedSet.Contains(m.Id)
                };
            }).ToList();

            return Result<GetThreadMessagesResponse>.Success(new GetThreadMessagesResponse
            {
                Items = items,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting messages for thread {ThreadId}", request.ThreadId);
            return Result<GetThreadMessagesResponse>.Failure($"Error getting messages: {ex.Message}");
        }
    }

    public async Task<Result<SearchMessagesResponse>> SearchMessagesAsync(SearchMessagesRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<SearchMessagesResponse>(callerResult);

            var caller = callerResult.Data!;
            var queryText = request.Query.Trim();
            if (queryText.Length == 0)
                return Result<SearchMessagesResponse>.Failure("Search query is required", 400);

            var memberships = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .ToListAsync(ct);

            var allowedThreadIds = memberships.Select(m => m.MessageThreadId).Distinct().ToList();
            if (request.ThreadId is Guid threadId)
            {
                if (!allowedThreadIds.Contains(threadId))
                    return Result<SearchMessagesResponse>.Forbidden("Requester is not a member of this thread");

                allowedThreadIds = [threadId];
            }

            var pageIndex = request.PageIndex < 0 ? 0 : request.PageIndex;
            var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);
            var normalizedQuery = queryText.ToLowerInvariant();

            var baseQuery = dataContext.Query<Message>()
                .Where(m => allowedThreadIds.Contains(m.MessageThreadId))
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .Where(m => m.Text.ToLower().Contains(normalizedQuery));

            var totalCount = await baseQuery.CountAsync(ct);
            var messages = await baseQuery
                .OrderByDescending(m => m.CreatedAt)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var memberIds = messages.Select(m => m.MessageThreadMemberId).Distinct().ToList();
            var members = await dataContext.Query<MessageThreadMember>()
                .Where(m => memberIds.Contains(m.Id))
                .Where(m => m.TenantId == caller.TenantId)
                .ToListAsync(ct);

            var memberMap = members.ToDictionary(m => m.Id);

            return Result<SearchMessagesResponse>.Success(new SearchMessagesResponse
            {
                Items = messages.Select(message =>
                {
                    memberMap.TryGetValue(message.MessageThreadMemberId, out var sender);
                    return new SearchMessageItemResponse
                    {
                        ThreadId = message.MessageThreadId,
                        MessageId = message.Id,
                        SenderCredentialId = sender?.CredentialId ?? Guid.Empty,
                        Text = message.Text,
                        CreatedAt = message.CreatedAt
                    };
                }).ToList(),
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching messages");
            return Result<SearchMessagesResponse>.Failure($"Error searching messages: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> DeleteThreadMessageAsync(DeleteThreadMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            var message = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (message is null)
                return Result<CmdResponse>.NotFound("Message not found");

            if (message.MessageThreadMemberId != member.Id && !await MemberHasAdminRoleAsync(member.TenantId, member.Id, ct))
                return Result<CmdResponse>.Failure("You can only delete your own messages", 403);

            message.IsDeleted = true;
            message.IsEnabled = false;
            message.DeletedAt = DateTime.UtcNow;
            message.ModifiedAt = DateTime.UtcNow;

            dataContext.Update(message);
            AddOutboxEvent(
                MessageRealtimeEvents.MessageDeleted,
                message.TenantId,
                message.MessageThreadId,
                message.Id,
                nameof(Message),
                caller.CredentialId,
                new
                {
                    request.ThreadId,
                    request.MessageId
                });

            await dataContext.SaveChangesAsync(ct);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Message deleted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting message {MessageId} in thread {ThreadId}", request.MessageId, request.ThreadId);
            return Result<CmdResponse>.Failure($"Error deleting message: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> EditThreadMessageAsync(EditThreadMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            var message = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (message is null)
                return Result<CmdResponse>.NotFound("Message not found");

            if (message.MessageThreadMemberId != member.Id && !await MemberHasAdminRoleAsync(member.TenantId, member.Id, ct))
                return Result<CmdResponse>.Failure("You can only edit your own messages", 403);

            message.Text = request.Text;
            message.ModifiedAt = DateTime.UtcNow;

            dataContext.Update(message);
            AddOutboxEvent(
                MessageRealtimeEvents.MessageEdited,
                message.TenantId,
                message.MessageThreadId,
                message.Id,
                nameof(Message),
                caller.CredentialId,
                new
                {
                    request.ThreadId,
                    request.MessageId
                });

            await dataContext.SaveChangesAsync(ct);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Message updated successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error editing message {MessageId} in thread {ThreadId}", request.MessageId, request.ThreadId);
            return Result<CmdResponse>.Failure($"Error editing message: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> PinMessageAsync(PinMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var member = await GetActiveMemberAsync(caller.TenantId, request.ThreadId, caller.CredentialId, ct);
            if (member is null)
                return Result<CmdResponse>.Forbidden("Requester is not a member of this thread");

            if (!await CanManageThreadAsync(member, ct))
                return Result<CmdResponse>.Forbidden("Only thread admins can pin messages");

            var messageExists = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .AnyAsync(ct);
            if (!messageExists)
                return Result<CmdResponse>.NotFound("Message not found");

            var existingPin = await dataContext.Query<MessagePin>()
                .Where(p => p.MessageThreadId == request.ThreadId)
                .Where(p => p.MessageId == request.MessageId)
                .Where(p => p.TenantId == caller.TenantId)
                .Where(p => !p.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (request.IsPinned)
            {
                if (existingPin is null)
                {
                    existingPin = new MessagePin
                    {
                        Id = Guid.NewGuid(),
                        TenantId = caller.TenantId,
                        MessageThreadId = request.ThreadId,
                        MessageId = request.MessageId,
                        PinnedByMemberId = member.Id,
                        IsEnabled = true,
                        CreatedAt = DateTime.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                    };
                    dataContext.Add(existingPin);
                }
                else
                {
                    existingPin.IsEnabled = true;
                    existingPin.ModifiedAt = DateTime.UtcNow;
                    dataContext.Update(existingPin);
                }
            }
            else if (existingPin is not null)
            {
                existingPin.IsDeleted = true;
                existingPin.IsEnabled = false;
                existingPin.DeletedAt = DateTime.UtcNow;
                existingPin.ModifiedAt = DateTime.UtcNow;
                dataContext.Update(existingPin);
            }

            AddOutboxEvent(
                request.IsPinned ? MessageRealtimeEvents.MessagePinned : MessageRealtimeEvents.MessageUnpinned,
                caller.TenantId,
                request.ThreadId,
                request.MessageId,
                nameof(MessagePin),
                caller.CredentialId,
                new
                {
                    request.ThreadId,
                    request.MessageId,
                    MemberId = member.Id
                });

            await dataContext.SaveChangesAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = request.IsPinned ? "Message pinned successfully" : "Message unpinned successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating pin for message {MessageId}", request.MessageId);
            return Result<CmdResponse>.Failure($"Error updating message pin: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> SaveMessageAsync(SaveMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var member = await GetActiveMemberAsync(caller.TenantId, request.ThreadId, caller.CredentialId, ct);
            if (member is null)
                return Result<CmdResponse>.Forbidden("Requester is not a member of this thread");

            var messageExists = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .AnyAsync(ct);
            if (!messageExists)
                return Result<CmdResponse>.NotFound("Message not found");

            var existingSaved = await dataContext.Query<MessageSaved>()
                .Where(s => s.MessageId == request.MessageId)
                .Where(s => s.MessageThreadMemberId == member.Id)
                .Where(s => s.TenantId == caller.TenantId)
                .Where(s => !s.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (request.IsSaved)
            {
                if (existingSaved is null)
                {
                    existingSaved = new MessageSaved
                    {
                        Id = Guid.NewGuid(),
                        TenantId = caller.TenantId,
                        MessageId = request.MessageId,
                        MessageThreadMemberId = member.Id,
                        IsEnabled = true,
                        CreatedAt = DateTime.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                    };
                    dataContext.Add(existingSaved);
                }
                else
                {
                    existingSaved.IsEnabled = true;
                    existingSaved.ModifiedAt = DateTime.UtcNow;
                    dataContext.Update(existingSaved);
                }
            }
            else if (existingSaved is not null)
            {
                existingSaved.IsDeleted = true;
                existingSaved.IsEnabled = false;
                existingSaved.DeletedAt = DateTime.UtcNow;
                existingSaved.ModifiedAt = DateTime.UtcNow;
                dataContext.Update(existingSaved);
            }

            AddOutboxEvent(
                request.IsSaved ? MessageRealtimeEvents.MessageSaved : MessageRealtimeEvents.MessageUnsaved,
                caller.TenantId,
                request.ThreadId,
                request.MessageId,
                nameof(MessageSaved),
                caller.CredentialId,
                new
                {
                    request.ThreadId,
                    request.MessageId,
                    MemberId = member.Id
                });

            await dataContext.SaveChangesAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = request.IsSaved ? "Message saved successfully" : "Message unsaved successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating saved state for message {MessageId}", request.MessageId);
            return Result<CmdResponse>.Failure($"Error updating saved message: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> ReportMessageAsync(ReportMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var member = await GetActiveMemberAsync(caller.TenantId, request.ThreadId, caller.CredentialId, ct);
            if (member is null)
                return Result<CmdResponse>.Forbidden("Requester is not a member of this thread");

            var messageExists = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .AnyAsync(ct);
            if (!messageExists)
                return Result<CmdResponse>.NotFound("Message not found");

            var report = new MessageReport
            {
                Id = Guid.NewGuid(),
                TenantId = caller.TenantId,
                MessageId = request.MessageId,
                ReporterMemberId = member.Id,
                Reason = request.Reason.Trim(),
                Details = request.Details,
                Status = MessageReportStatuses.Open,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };

            dataContext.Add(report);
            AddOutboxEvent(
                MessageRealtimeEvents.MessageReported,
                caller.TenantId,
                request.ThreadId,
                report.Id,
                nameof(MessageReport),
                caller.CredentialId,
                new
                {
                    request.ThreadId,
                    request.MessageId,
                    ReportId = report.Id,
                    MemberId = member.Id,
                    report.Reason
                });

            await dataContext.SaveChangesAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Created,
                Message = "Message reported successfully"
            }, 201);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reporting message {MessageId}", request.MessageId);
            return Result<CmdResponse>.Failure($"Error reporting message: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> BlockCredentialAsync(BlockCredentialRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            if (request.CredentialId == caller.CredentialId)
                return Result<CmdResponse>.Failure("Cannot block your own credential", 400);

            var credentialExists = await dataContext.Query<IdentityCredential>()
                .Where(c => c.Id == request.CredentialId)
                .Where(c => c.TenantId == caller.TenantId)
                .Where(c => !c.IsDeleted && c.IsEnabled)
                .AnyAsync(ct);
            if (!credentialExists)
                return Result<CmdResponse>.NotFound("Credential not found");

            var existingBlock = await dataContext.Query<MessageBlock>()
                .Where(b => b.BlockerCredentialId == caller.CredentialId)
                .Where(b => b.BlockedCredentialId == request.CredentialId)
                .Where(b => b.TenantId == caller.TenantId)
                .Where(b => !b.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (existingBlock is null)
            {
                existingBlock = new MessageBlock
                {
                    Id = Guid.NewGuid(),
                    TenantId = caller.TenantId,
                    BlockerCredentialId = caller.CredentialId,
                    BlockedCredentialId = request.CredentialId,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow,
                    ConcurrencyStamp = Guid.NewGuid()
                };
                dataContext.Add(existingBlock);
            }
            else
            {
                existingBlock.IsEnabled = true;
                existingBlock.ModifiedAt = DateTime.UtcNow;
                dataContext.Update(existingBlock);
            }

            AddOutboxEvent(
                MessageRealtimeEvents.CredentialBlocked,
                caller.TenantId,
                null,
                existingBlock.Id,
                nameof(MessageBlock),
                caller.CredentialId,
                new
                {
                    BlockId = existingBlock.Id,
                    existingBlock.BlockerCredentialId,
                    existingBlock.BlockedCredentialId
                });

            await dataContext.SaveChangesAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Created,
                Message = "Credential blocked successfully"
            }, 201);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error blocking credential {CredentialId}", request.CredentialId);
            return Result<CmdResponse>.Failure($"Error blocking credential: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> DeleteCredentialBlockAsync(DeleteCredentialBlockRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var block = await dataContext.Query<MessageBlock>()
                .Where(b => b.BlockerCredentialId == caller.CredentialId)
                .Where(b => b.BlockedCredentialId == request.CredentialId)
                .Where(b => b.TenantId == caller.TenantId)
                .Where(b => !b.IsDeleted && b.IsEnabled)
                .FirstOrDefaultAsync(ct);
            if (block is null)
                return Result<CmdResponse>.NotFound("Credential block not found");

            block.IsDeleted = true;
            block.IsEnabled = false;
            block.DeletedAt = DateTime.UtcNow;
            block.ModifiedAt = DateTime.UtcNow;
            dataContext.Update(block);

            AddOutboxEvent(
                MessageRealtimeEvents.CredentialUnblocked,
                caller.TenantId,
                null,
                block.Id,
                nameof(MessageBlock),
                caller.CredentialId,
                new
                {
                    BlockId = block.Id,
                    block.BlockerCredentialId,
                    block.BlockedCredentialId
                });

            await dataContext.SaveChangesAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Credential block removed successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing credential block for {CredentialId}", request.CredentialId);
            return Result<CmdResponse>.Failure($"Error removing credential block: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> CreateMessageFileAsync(CreateMessageFileRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            var message = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (message is null)
                return Result<CmdResponse>.NotFound("Message not found");

            var storageFile = await dataContext.Query<StorageFile>()
                .Where(f => f.Id == request.StorageFileId)
                .Where(f => f.TenantId == caller.TenantId)
                .Where(f => !f.IsDeleted && f.IsEnabled)
                .FirstOrDefaultAsync(ct);
            if (storageFile is null)
                return Result<CmdResponse>.NotFound("Storage file not found");

            if (storageFile.FileSize is decimal fileSize && fileSize > MaxAttachmentSizeBytes)
                return Result<CmdResponse>.Failure("Storage file exceeds the Messaging attachment size policy", 400);

            if (!IsAllowedAttachmentFileType(storageFile))
                return Result<CmdResponse>.Failure("Storage file type is not allowed for Messaging attachments", 400);

            var file = new MessageFile
            {
                Id = Guid.NewGuid(),
                TenantId = message.TenantId,
                MessageId = request.MessageId,
                StorageId = request.StorageFileId,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };

            dataContext.Add(file);
            await dataContext.SaveChangesAsync(ct);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Created,
                Message = "File attachment created successfully"
            }, 201);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating file attachment for message {MessageId}", request.MessageId);
            return Result<CmdResponse>.Failure($"Error creating file attachment: {ex.Message}");
        }
    }

    public async Task<Result<List<MessageFileResponse>>> GetMessageFilesAsync(GetMessageFilesRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<List<MessageFileResponse>>(callerResult);

            var caller = callerResult.Data!;
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<List<MessageFileResponse>>.Failure("Requester is not a member of this thread", 403);

            var messageExists = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .AnyAsync(ct);

            if (!messageExists)
                return Result<List<MessageFileResponse>>.NotFound("Message not found");

            var fileEntities = await dataContext.Query<MessageFile>()
                .Where(f => f.MessageId == request.MessageId)
                .Where(f => f.TenantId == caller.TenantId)
                .Where(f => !f.IsDeleted && f.IsEnabled)
                .ToListAsync(ct);

            var files = fileEntities.Select(f => new MessageFileResponse
            {
                Id = f.Id,
                MessageId = f.MessageId,
                StorageFileId = f.StorageId,
                CreatedAt = f.CreatedAt
            }).ToList();

            return Result<List<MessageFileResponse>>.Success(files);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving files for message {MessageId}", request.MessageId);
            return Result<List<MessageFileResponse>>.Failure($"Error retrieving message files: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> CreateMessageReactionAsync(CreateMessageReactionRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            var message = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (message is null)
                return Result<CmdResponse>.NotFound("Message not found");

            // Check for duplicate reaction of the same type by this thread member.
            var duplicateExists = await dataContext.Query<MessageReaction>()
                .Where(r => r.MessageId == request.MessageId)
                .Where(r => r.TypeId == request.TypeId)
                .Where(r => r.MessageThreadMemberId == member.Id)
                .Where(r => r.TenantId == caller.TenantId)
                .Where(r => !r.IsDeleted && r.IsEnabled)
                .AnyAsync(ct);

            if (duplicateExists)
                return Result<CmdResponse>.Conflict("A reaction of this type already exists on this message");

            var reaction = new MessageReaction
            {
                Id = Guid.NewGuid(),
                TenantId = message.TenantId,
                MessageId = request.MessageId,
                TypeId = request.TypeId,
                MessageThreadMemberId = member.Id,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };

            dataContext.Add(reaction);
            AddOutboxEvent(
                MessageRealtimeEvents.ReactionCreated,
                message.TenantId,
                message.MessageThreadId,
                reaction.Id,
                nameof(MessageReaction),
                caller.CredentialId,
                new
                {
                    ReactionId = reaction.Id,
                    request.MessageId,
                    request.TypeId,
                    MemberId = member.Id
                });

            await dataContext.SaveChangesAsync(ct);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Created,
                Message = "Reaction created successfully"
            }, 201);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating reaction on message {MessageId}", request.MessageId);
            return Result<CmdResponse>.Failure($"Error creating reaction: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> DeleteMessageReactionAsync(DeleteMessageReactionRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var reaction = await dataContext.Query<MessageReaction>()
                .Where(r => r.Id == request.ReactionId)
                .Where(r => r.TenantId == caller.TenantId)
                .Where(r => !r.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (reaction is null)
                return Result<CmdResponse>.NotFound("Reaction not found");

            // Verify requester is a member of the thread through the reaction's message
            var message = await dataContext.Query<Message>()
                .Where(m => m.Id == reaction.MessageId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (message is null)
                return Result<CmdResponse>.NotFound("Message not found");

            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == message.MessageThreadId)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            if (reaction.MessageThreadMemberId != member.Id && !await MemberHasAdminRoleAsync(member.TenantId, member.Id, ct))
                return Result<CmdResponse>.Forbidden("You can only delete your own reactions");

            reaction.IsDeleted = true;
            reaction.IsEnabled = false;
            reaction.DeletedAt = DateTime.UtcNow;
            reaction.ModifiedAt = DateTime.UtcNow;

            dataContext.Update(reaction);
            AddOutboxEvent(
                MessageRealtimeEvents.ReactionDeleted,
                reaction.TenantId,
                message.MessageThreadId,
                reaction.Id,
                nameof(MessageReaction),
                caller.CredentialId,
                new
                {
                    ReactionId = reaction.Id,
                    reaction.MessageId,
                    reaction.TypeId
                });

            await dataContext.SaveChangesAsync(ct);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Reaction deleted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting reaction {ReactionId}", request.ReactionId);
            return Result<CmdResponse>.Failure($"Error deleting reaction: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> MarkMessagesReadAsync(MarkMessagesReadRequest request, CancellationToken ct = default)
    {
        try
        {
            var callerResult = ResolveCaller(request.Metadata);
            if (!callerResult.IsSuccess)
                return CallerFailure<CmdResponse>(callerResult);

            var caller = callerResult.Data!;
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            var requestedMessageIds = request.MessageIds.Distinct().ToList();
            var threadMessages = await dataContext.Query<Message>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => requestedMessageIds.Contains(m.Id))
                .Where(m => m.TenantId == caller.TenantId)
                .Where(m => !m.IsDeleted)
                .ToListAsync(ct);

            if (threadMessages.Count != requestedMessageIds.Count)
                return Result<CmdResponse>.NotFound("One or more messages were not found in this thread");

            var existingDeliveries = await dataContext.Query<MessageDelivery>()
                .Where(d => d.MessageThreadMemberId == member.Id)
                .Where(d => requestedMessageIds.Contains(d.MessageId))
                .Where(d => d.TenantId == caller.TenantId)
                .Where(d => !d.IsDeleted)
                .ToListAsync(ct);

            var existingByMessage = existingDeliveries
                .GroupBy(d => d.MessageId)
                .ToDictionary(g => g.Key, g => g.First());
            var markedCount = 0;

            foreach (var messageId in requestedMessageIds)
            {
                if (existingByMessage.TryGetValue(messageId, out var delivery))
                {
                    if (delivery.TypeId == MessageDeliveryTypes.Read)
                        continue;

                    delivery.TypeId = MessageDeliveryTypes.Read;
                    delivery.ModifiedAt = DateTime.UtcNow;
                    dataContext.Update(delivery);
                    markedCount++;
                }
                else
                {
                    dataContext.Add(new MessageDelivery
                    {
                        Id = Guid.NewGuid(),
                        TenantId = member.TenantId,
                        MessageThreadMemberId = member.Id,
                        MessageId = messageId,
                        TypeId = MessageDeliveryTypes.Read,
                        IsEnabled = true,
                        CreatedAt = DateTime.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                    });
                    markedCount++;
                }
            }

            member.LastSeenAt = DateTime.UtcNow;
            member.ModifiedAt = DateTime.UtcNow;
            dataContext.Update(member);

            if (markedCount > 0)
            {
                AddOutboxEvent(
                    MessageRealtimeEvents.MessagesRead,
                    member.TenantId,
                    request.ThreadId,
                    member.Id,
                    nameof(MessageDelivery),
                    caller.CredentialId,
                    new
                    {
                        request.ThreadId,
                        MemberId = member.Id,
                        MessageIds = requestedMessageIds,
                        Count = markedCount
                    });
            }

            await dataContext.SaveChangesAsync(ct);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = $"{markedCount} message(s) marked as read"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error marking messages as read in thread {ThreadId}", request.ThreadId);
            return Result<CmdResponse>.Failure($"Error marking messages as read: {ex.Message}");
        }
    }

    private Result<MessagingRequestContext> ResolveCaller(RequestMetadata? metadata) =>
        requestContextResolver.Resolve(metadata);

    private static Result<T> CallerFailure<T>(Result<MessagingRequestContext> caller) =>
        caller.StatusCode switch
        {
            401 => Result<T>.Unauthorized(caller.Message),
            403 => Result<T>.Forbidden(caller.Message),
            _ => Result<T>.Failure(caller.Message ?? "Caller context could not be resolved", caller.StatusCode)
        };

    private async Task<MessageThreadMember?> GetActiveMemberAsync(
        Guid tenantId,
        Guid threadId,
        Guid credentialId,
        CancellationToken ct) =>
        await dataContext.Query<MessageThreadMember>()
            .Where(m => m.MessageThreadId == threadId)
            .Where(m => m.CredentialId == credentialId)
            .Where(m => m.TenantId == tenantId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .FirstOrDefaultAsync(ct);

    private async Task<bool> ThreadHasExplicitRolesAsync(Guid tenantId, Guid threadId, CancellationToken ct)
    {
        var members = await dataContext.Query<MessageThreadMember>()
            .Where(m => m.MessageThreadId == threadId)
            .Where(m => m.TenantId == tenantId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .ToListAsync(ct);

        var memberIds = members.Select(m => m.Id).ToList();
        if (memberIds.Count == 0)
            return false;

        return await dataContext.Query<MessageThreadMemberRole>()
            .Where(r => memberIds.Contains(r.MessageThreadMemberId))
            .Where(r => r.TenantId == tenantId)
            .Where(r => !r.IsDeleted && r.IsEnabled)
            .AnyAsync(ct);
    }

    private async Task<bool> MemberHasAdminRoleAsync(Guid tenantId, Guid memberId, CancellationToken ct)
    {
        var memberRoles = await dataContext.Query<MessageThreadMemberRole>()
            .Where(r => r.MessageThreadMemberId == memberId)
            .Where(r => r.TenantId == tenantId)
            .Where(r => !r.IsDeleted && r.IsEnabled)
            .ToListAsync(ct);

        var roleIds = memberRoles.Select(r => r.RoleId).ToList();
        if (roleIds.Count == 0)
            return false;

        return await dataContext.Query<IdentityRole>()
            .Where(r => roleIds.Contains(r.Id))
            .Where(r => r.TypeId == IdentityConstants.RoleType.Admin)
            .Where(r => r.TenantId == tenantId)
            .Where(r => !r.IsDeleted && r.IsEnabled)
            .AnyAsync(ct);
    }

    private async Task<bool> CanManageThreadAsync(MessageThreadMember member, CancellationToken ct)
    {
        if (member.Role is MessageThreadMemberRoles.Owner or MessageThreadMemberRoles.Admin)
            return true;

        if (await MemberHasAdminRoleAsync(member.TenantId, member.Id, ct))
            return true;

        return !await ThreadHasExplicitRolesAsync(member.TenantId, member.MessageThreadId, ct);
    }

    private async Task<Guid?> ResolveChatThreadTypeIdAsync(Guid tenantId, CancellationToken ct)
    {
        var type = await dataContext.Query<MessageThreadType>()
            .Where(t => t.TenantId == tenantId)
            .Where(t => t.MessageTypeId == MessageTypes.Chat)
            .Where(t => !t.IsDeleted && t.IsEnabled)
            .FirstOrDefaultAsync(ct);

        return type?.Id;
    }

    private async Task<Guid?> FindDirectThreadAsync(
        Guid tenantId,
        Guid credentialId,
        Guid otherCredentialId,
        CancellationToken ct)
    {
        var participantIds = new[] { credentialId, otherCredentialId };
        var participantMemberships = await dataContext.Query<MessageThreadMember>()
            .Where(m => participantIds.Contains(m.CredentialId))
            .Where(m => m.TenantId == tenantId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .ToListAsync(ct);

        var candidateThreadIds = participantMemberships
            .GroupBy(m => m.MessageThreadId)
            .Where(g => g.Select(m => m.CredentialId).Distinct().Count() == 2)
            .Select(g => g.Key)
            .ToList();
        if (candidateThreadIds.Count == 0)
            return null;

        var allCandidateMembers = await dataContext.Query<MessageThreadMember>()
            .Where(m => candidateThreadIds.Contains(m.MessageThreadId))
            .Where(m => m.TenantId == tenantId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .ToListAsync(ct);

        return allCandidateMembers
            .GroupBy(m => m.MessageThreadId)
            .FirstOrDefault(g =>
                g.Count() == 2 &&
                g.Select(m => m.CredentialId).OrderBy(id => id)
                    .SequenceEqual(participantIds.OrderBy(id => id)))
            ?.Key;
    }

    private async Task<Dictionary<Guid, int>> GetUnreadCountMapAsync(
        Guid tenantId,
        Guid credentialId,
        List<Guid> threadIds,
        CancellationToken ct)
    {
        if (threadIds.Count == 0)
            return [];

        var memberships = await dataContext.Query<MessageThreadMember>()
            .Where(m => threadIds.Contains(m.MessageThreadId))
            .Where(m => m.CredentialId == credentialId)
            .Where(m => m.TenantId == tenantId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .ToListAsync(ct);

        var memberByThread = memberships.ToDictionary(m => m.MessageThreadId);
        var memberIds = memberships.Select(m => m.Id).ToList();

        var messages = await dataContext.Query<Message>()
            .Where(m => threadIds.Contains(m.MessageThreadId))
            .Where(m => m.TenantId == tenantId)
            .Where(m => !m.IsDeleted)
            .ToListAsync(ct);

        var readDeliveries = await dataContext.Query<MessageDelivery>()
            .Where(d => memberIds.Contains(d.MessageThreadMemberId))
            .Where(d => d.TenantId == tenantId)
            .Where(d => d.TypeId == MessageDeliveryTypes.Read)
            .Where(d => !d.IsDeleted)
            .ToListAsync(ct);

        var readIdsByMember = readDeliveries
            .GroupBy(d => d.MessageThreadMemberId)
            .ToDictionary(g => g.Key, g => g.Select(d => d.MessageId).ToHashSet());

        return threadIds.ToDictionary(
            threadId => threadId,
            threadId =>
            {
                if (!memberByThread.TryGetValue(threadId, out var member))
                    return 0;

                readIdsByMember.TryGetValue(member.Id, out var readIds);
                readIds ??= [];

                return messages.Count(m =>
                    m.MessageThreadId == threadId &&
                    m.MessageThreadMemberId != member.Id &&
                    !readIds.Contains(m.Id));
            });
    }

    private async Task<bool> IsBlockedAsync(Guid tenantId, Guid firstCredentialId, Guid secondCredentialId, CancellationToken ct) =>
        await dataContext.Query<MessageBlock>()
            .Where(b => b.TenantId == tenantId)
            .Where(b => !b.IsDeleted && b.IsEnabled)
            .Where(b =>
                (b.BlockerCredentialId == firstCredentialId && b.BlockedCredentialId == secondCredentialId) ||
                (b.BlockerCredentialId == secondCredentialId && b.BlockedCredentialId == firstCredentialId))
            .AnyAsync(ct);

    private static string NormalizeRole(string role) =>
        role.Trim() switch
        {
            nameof(MessageThreadMemberRoles.Owner) => MessageThreadMemberRoles.Owner,
            nameof(MessageThreadMemberRoles.Admin) => MessageThreadMemberRoles.Admin,
            _ => MessageThreadMemberRoles.Member
        };

    private static List<Guid> DeserializeMentionedCredentialIds(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, OutboxJsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static bool IsAllowedAttachmentFileType(StorageFile file)
    {
        var blockedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bat",
            ".cmd",
            ".com",
            ".dll",
            ".exe",
            ".js",
            ".msi",
            ".ps1",
            ".scr",
            ".sh",
            ".vbs"
        };

        var extension = Path.GetExtension(file.Name ?? file.ContentPath);
        if (!string.IsNullOrWhiteSpace(extension) && blockedExtensions.Contains(extension))
            return false;

        if (string.IsNullOrWhiteSpace(file.ContentType))
            return true;

        return file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
               file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
               file.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
               file.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
               file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
               file.ContentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
               file.ContentType.Equals("application/zip", StringComparison.OrdinalIgnoreCase) ||
               file.ContentType.StartsWith("application/vnd.", StringComparison.OrdinalIgnoreCase);
    }

    private async Task AddAdminRoleBindingsAsync(
        MessageThreadMember member,
        Guid credentialId,
        CancellationToken ct)
    {
        var adminRoles = await dataContext.Query<IdentityRole>()
            .Where(r => r.CredentialId == credentialId)
            .Where(r => r.TypeId == IdentityConstants.RoleType.Admin)
            .Where(r => r.TenantId == member.TenantId)
            .Where(r => !r.IsDeleted && r.IsEnabled)
            .ToListAsync(ct);

        foreach (var role in adminRoles)
        {
            dataContext.Add(new MessageThreadMemberRole
            {
                Id = Guid.NewGuid(),
                TenantId = member.TenantId,
                MessageThreadMemberId = member.Id,
                RoleId = role.Id,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }
    }

    private void AddOutboxEvent(
        string eventType,
        Guid tenantId,
        Guid? threadId,
        Guid aggregateId,
        string aggregateType,
        Guid actorCredentialId,
        object payload)
    {
        var now = DateTime.UtcNow;
        dataContext.Add(new MessageOutboxEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = eventType,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            ThreadId = threadId,
            ActorCredentialId = actorCredentialId,
            PayloadJson = JsonSerializer.Serialize(payload, OutboxJsonOptions),
            OccurredAt = now,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
    }

    private static MessageThreadMemberGroup CreateDefaultThreadMemberGroup(Guid threadId, Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        MessageThreadId = threadId,
        Alias = "Default",
        Emoji = string.Empty,
        Description = string.Empty,
        Status = 1,
        SystemReferenceId = Guid.Empty,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };
}
