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
                .Where(m => !m.IsDeleted)
                .ToListAsync(ct);

            var memberThreadIds = memberships.Select(m => m.MessageThreadId).Distinct().ToList();
            var totalCount = memberThreadIds.Count;

            var threads = await dataContext.Query<MessageThread>()
                .Where(t => memberThreadIds.Contains(t.Id))
                .Where(t => !t.IsDeleted)
                .OrderByDescending(t => t.CreatedAt)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var threadIds = threads.Select(t => t.Id).ToList();

            // Get member counts per thread using GroupByAsync
            var memberGroups = await dataContext.Query<MessageThreadMember>()
                .Where(m => threadIds.Contains(m.MessageThreadId))
                .Where(m => !m.IsDeleted)
                .GroupByAsync(m => m.MessageThreadId, ct);

            var memberCountMap = memberGroups.ToDictionary(g => g.Key, g => g.Items.Count);

            // Get messages for these threads to find last message per thread
            var threadMessages = await dataContext.Query<Message>()
                .Where(m => threadIds.Contains(m.MessageThreadId))
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync(ct);

            var lastMessageMap = threadMessages
                .GroupBy(m => m.MessageThreadId)
                .ToDictionary(g => g.Key, g => g.First());

            var items = threads.Select(t =>
            {
                lastMessageMap.TryGetValue(t.Id, out var lastMsg);
                memberCountMap.TryGetValue(t.Id, out var count);

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
                    LastMessageAt = lastMsg?.CreatedAt
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
                .Where(t => !t.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (thread is null)
            {
                return Result<GetThreadResponse>.NotFound("Thread not found");
            }

            var requesterMember = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == thread.Id)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (requesterMember is null)
                return Result<GetThreadResponse>.Failure("Requester is not a member of this thread", 403);

            var members = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == thread.Id)
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
                .Where(t => !t.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (thread is null)
                return Result<CmdResponse>.NotFound("Thread not found");

            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == caller.CredentialId)
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
                .Where(t => !t.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (thread is null)
            {
                return Result<CmdResponse>.NotFound("Thread not found");
            }

            var actorMember = await GetActiveMemberAsync(request.ThreadId, caller.CredentialId, ct);
            if (actorMember is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            if (!await CanManageThreadAsync(actorMember, ct))
                return Result<CmdResponse>.Forbidden("Only thread admins can add members");

            // Validate credential exists
            var credential = await dataContext.Query<IdentityCredential>()
                .Where(c => c.Id == request.CredentialId)
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
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (existingMember is not null)
            {
                return Result<CmdResponse>.Conflict("Credential is already a member of this thread");
            }

            var group = await dataContext.Query<MessageThreadMemberGroup>()
                .Where(g => g.MessageThreadId == request.ThreadId)
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
            var actorMember = await GetActiveMemberAsync(request.ThreadId, caller.CredentialId, ct);
            if (actorMember is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            // Find the member
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == request.CredentialId)
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
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (senderMember is null)
            {
                return Result<CreateThreadMessageResponse>.Failure("Sender is not a member of this thread", 403);
            }

            var message = new Message
            {
                Id = Guid.NewGuid(),
                TenantId = thread.TenantId,
                MessageThreadId = request.ThreadId,
                MessageThreadMemberId = senderMember.Id,
                Text = request.Text,
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
                    SenderMemberId = senderMember.Id
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
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (requesterMember is null)
            {
                return Result<GetThreadMessagesResponse>.Failure("Requester is not a member of this thread", 403);
            }

            var totalCount = await dataContext.Query<Message>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => !m.IsDeleted)
                .CountAsync(ct);

            var messages = await dataContext.Query<Message>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // Auto-create "Delivered" records for messages this member hasn't seen
            var fetchedMessageIds = messages.Select(m => m.Id).ToList();
            var existingDeliveries = await dataContext.Query<MessageDelivery>()
                .Where(d => d.MessageThreadMemberId == requesterMember.Id)
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
                .ToListAsync(ct);

            var memberMap = members.ToDictionary(m => m.Id);

            var items = messages.Select(m =>
            {
                memberMap.TryGetValue(m.MessageThreadMemberId, out var sender);
                return new ThreadMessageItemResponse
                {
                    Id = m.Id,
                    Text = m.Text,
                    SenderCredentialId = sender?.CredentialId ?? Guid.Empty,
                    SenderAlias = sender?.Alias ?? string.Empty,
                    CreatedAt = m.CreatedAt
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
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            var message = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (message is null)
                return Result<CmdResponse>.NotFound("Message not found");

            if (message.MessageThreadMemberId != member.Id && !await MemberHasAdminRoleAsync(member.Id, ct))
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
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            var message = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (message is null)
                return Result<CmdResponse>.NotFound("Message not found");

            if (message.MessageThreadMemberId != member.Id && !await MemberHasAdminRoleAsync(member.Id, ct))
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
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            var message = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (message is null)
                return Result<CmdResponse>.NotFound("Message not found");

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
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<List<MessageFileResponse>>.Failure("Requester is not a member of this thread", 403);

            var messageExists = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => !m.IsDeleted)
                .AnyAsync(ct);

            if (!messageExists)
                return Result<List<MessageFileResponse>>.NotFound("Message not found");

            var fileEntities = await dataContext.Query<MessageFile>()
                .Where(f => f.MessageId == request.MessageId)
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
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            var message = await dataContext.Query<Message>()
                .Where(m => m.Id == request.MessageId)
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (message is null)
                return Result<CmdResponse>.NotFound("Message not found");

            // Check for duplicate reaction of the same type by this thread member.
            var duplicateExists = await dataContext.Query<MessageReaction>()
                .Where(r => r.MessageId == request.MessageId)
                .Where(r => r.TypeId == request.TypeId)
                .Where(r => r.MessageThreadMemberId == member.Id)
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
                .Where(r => !r.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (reaction is null)
                return Result<CmdResponse>.NotFound("Reaction not found");

            // Verify requester is a member of the thread through the reaction's message
            var message = await dataContext.Query<Message>()
                .Where(m => m.Id == reaction.MessageId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (message is null)
                return Result<CmdResponse>.NotFound("Message not found");

            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == message.MessageThreadId)
                .Where(m => m.CredentialId == caller.CredentialId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            if (reaction.MessageThreadMemberId != member.Id && !await MemberHasAdminRoleAsync(member.Id, ct))
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
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            var requestedMessageIds = request.MessageIds.Distinct().ToList();
            var threadMessages = await dataContext.Query<Message>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => requestedMessageIds.Contains(m.Id))
                .Where(m => !m.IsDeleted)
                .ToListAsync(ct);

            if (threadMessages.Count != requestedMessageIds.Count)
                return Result<CmdResponse>.NotFound("One or more messages were not found in this thread");

            var existingDeliveries = await dataContext.Query<MessageDelivery>()
                .Where(d => d.MessageThreadMemberId == member.Id)
                .Where(d => requestedMessageIds.Contains(d.MessageId))
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
        Guid threadId,
        Guid credentialId,
        CancellationToken ct) =>
        await dataContext.Query<MessageThreadMember>()
            .Where(m => m.MessageThreadId == threadId)
            .Where(m => m.CredentialId == credentialId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .FirstOrDefaultAsync(ct);

    private async Task<bool> ThreadHasExplicitRolesAsync(Guid threadId, CancellationToken ct)
    {
        var members = await dataContext.Query<MessageThreadMember>()
            .Where(m => m.MessageThreadId == threadId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .ToListAsync(ct);

        var memberIds = members.Select(m => m.Id).ToList();
        if (memberIds.Count == 0)
            return false;

        return await dataContext.Query<MessageThreadMemberRole>()
            .Where(r => memberIds.Contains(r.MessageThreadMemberId))
            .Where(r => !r.IsDeleted && r.IsEnabled)
            .AnyAsync(ct);
    }

    private async Task<bool> MemberHasAdminRoleAsync(Guid memberId, CancellationToken ct)
    {
        var memberRoles = await dataContext.Query<MessageThreadMemberRole>()
            .Where(r => r.MessageThreadMemberId == memberId)
            .Where(r => !r.IsDeleted && r.IsEnabled)
            .ToListAsync(ct);

        var roleIds = memberRoles.Select(r => r.RoleId).ToList();
        if (roleIds.Count == 0)
            return false;

        return await dataContext.Query<IdentityRole>()
            .Where(r => roleIds.Contains(r.Id))
            .Where(r => r.TypeId == IdentityConstants.RoleType.Admin)
            .Where(r => !r.IsDeleted && r.IsEnabled)
            .AnyAsync(ct);
    }

    private async Task<bool> CanManageThreadAsync(MessageThreadMember member, CancellationToken ct)
    {
        if (await MemberHasAdminRoleAsync(member.Id, ct))
            return true;

        return !await ThreadHasExplicitRolesAsync(member.MessageThreadId, ct);
    }

    private async Task AddAdminRoleBindingsAsync(
        MessageThreadMember member,
        Guid credentialId,
        CancellationToken ct)
    {
        var adminRoles = await dataContext.Query<IdentityRole>()
            .Where(r => r.CredentialId == credentialId)
            .Where(r => r.TypeId == IdentityConstants.RoleType.Admin)
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
