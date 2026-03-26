using System.Net;
using Messaging.Domain.Shared.Contracts;
using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Domain.Shared.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;

namespace Messaging.Api.Services;

public sealed class ThreadService(
    IDataContext dataContext,
    ITenantResolver tenantService,
    ILogger<ThreadService> logger
) : IThreadService
{
    public async Task<Result<CreateThreadResponse>> CreateThreadAsync(CreateThreadRequest request, CancellationToken ct = default)
    {
        try
        {
            var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

            var thread = new MessageThread
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = request.Name,
                Description = request.Description ?? string.Empty,
                TypeId = request.TypeId,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };

            dataContext.Add(thread);

            // Add the creator (first credential in the list) as a member
            var allMemberIds = request.InitialMemberCredentialIds.Distinct().ToList();

            foreach (var credentialId in allMemberIds)
            {
                var member = new MessageThreadMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    MessageThreadId = thread.Id,
                    CredentialId = credentialId,
                    Alias = string.Empty,
                    Emoji = string.Empty,
                    Description = string.Empty,
                    Status = 1, // Active
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow,
                    ConcurrencyStamp = Guid.NewGuid()
                };

                dataContext.Add(member);
            }

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
            var pageIndex = request.PageIndex < 0 ? 0 : request.PageIndex;
            var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);

            // Get memberships for this credential
            var memberships = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.CredentialId == request.CredentialId)
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
            logger.LogError(ex, "Error getting thread list for credential: {CredentialId}", request.CredentialId);
            return Result<GetThreadListResponse>.Failure($"Error getting thread list: {ex.Message}");
        }
    }

    public async Task<Result<GetThreadResponse>> GetThreadAsync(GetThreadRequest request, CancellationToken ct = default)
    {
        try
        {
            var thread = await dataContext.Query<MessageThread>()
                .Where(t => t.Id == request.Id)
                .Where(t => !t.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (thread is null)
            {
                return Result<GetThreadResponse>.NotFound("Thread not found");
            }

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

    public async Task<Result<CmdResponse>> AddThreadMemberAsync(AddThreadMemberRequest request, CancellationToken ct = default)
    {
        try
        {
            var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

            // Validate thread exists
            var thread = await dataContext.Query<MessageThread>()
                .Where(t => t.Id == request.ThreadId)
                .Where(t => !t.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (thread is null)
            {
                return Result<CmdResponse>.NotFound("Thread not found");
            }

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

            var member = new MessageThreadMember
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                MessageThreadId = request.ThreadId,
                CredentialId = request.CredentialId,
                Alias = string.Empty,
                Emoji = string.Empty,
                Description = string.Empty,
                Status = 1, // Active
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };

            dataContext.Add(member);
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
            var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

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
                .Where(m => m.CredentialId == request.SenderCredentialId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (senderMember is null)
            {
                return Result<CreateThreadMessageResponse>.Failure("Sender is not a member of this thread", 403);
            }

            var message = new Message
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                MessageThreadId = request.ThreadId,
                MessageThreadMemberId = senderMember.Id,
                Text = request.Text,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };

            dataContext.Add(message);
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
            var pageIndex = request.PageIndex < 0 ? 0 : request.PageIndex;
            var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);

            // Validate requester is a member
            var requesterMember = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == request.RequesterCredentialId)
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
}
