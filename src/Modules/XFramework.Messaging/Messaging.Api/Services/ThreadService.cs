using System.Net;
using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts;
using Messaging.Domain.Shared.Contracts.Requests.Attachments;
using Messaging.Domain.Shared.Contracts.Requests.Delete;
using Messaging.Domain.Shared.Contracts.Requests.Edit;
using Messaging.Domain.Shared.Contracts.Requests.Reactions;
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

    public async Task<Result<CmdResponse>> UpdateThreadAsync(UpdateThreadRequest request, CancellationToken ct = default)
    {
        try
        {
            var thread = await dataContext.Query<MessageThread>()
                .Where(t => t.Id == request.ThreadId)
                .Where(t => !t.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (thread is null)
                return Result<CmdResponse>.NotFound("Thread not found");

            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == request.RequesterCredentialId)
                .Where(m => !m.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            if (request.Name is not null)
                thread.Name = request.Name;

            if (request.Description is not null)
                thread.Description = request.Description;

            thread.ModifiedAt = DateTime.UtcNow;

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
                        MessageThreadMemberId = requesterMember.Id,
                        MessageId = msgId,
                        TypeId = MessageDeliveryTypes.Delivered,
                        IsEnabled = true
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
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == request.RequesterCredentialId)
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

            if (message.MessageThreadMemberId != member.Id)
                return Result<CmdResponse>.Failure("You can only delete your own messages", 403);

            message.IsDeleted = true;
            message.IsEnabled = false;
            message.DeletedAt = DateTime.UtcNow;
            message.ModifiedAt = DateTime.UtcNow;

            dataContext.Update(message);
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
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == request.RequesterCredentialId)
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

            if (message.MessageThreadMemberId != member.Id)
                return Result<CmdResponse>.Failure("You can only edit your own messages", 403);

            message.Text = request.Text;
            message.ModifiedAt = DateTime.UtcNow;

            dataContext.Update(message);
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
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == request.RequesterCredentialId)
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
                MessageId = request.MessageId,
                StorageId = request.StorageFileId,
                IsEnabled = true
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
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == request.RequesterCredentialId)
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
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == request.RequesterCredentialId)
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

            // Check for duplicate reaction of the same type by this member's credential (via TenantId matching)
            var duplicateExists = await dataContext.Query<MessageReaction>()
                .Where(r => r.MessageId == request.MessageId)
                .Where(r => r.TypeId == request.TypeId)
                .Where(r => !r.IsDeleted && r.IsEnabled)
                .AnyAsync(ct);

            if (duplicateExists)
                return Result<CmdResponse>.Conflict("A reaction of this type already exists on this message");

            var reaction = new MessageReaction
            {
                MessageId = request.MessageId,
                TypeId = request.TypeId,
                IsEnabled = true
            };

            dataContext.Add(reaction);
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
                .Where(m => m.CredentialId == request.RequesterCredentialId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            reaction.IsDeleted = true;
            reaction.IsEnabled = false;
            reaction.DeletedAt = DateTime.UtcNow;
            reaction.ModifiedAt = DateTime.UtcNow;

            dataContext.Update(reaction);
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
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.MessageThreadId == request.ThreadId)
                .Where(m => m.CredentialId == request.RequesterCredentialId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (member is null)
                return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

            var existingDeliveries = await dataContext.Query<MessageDelivery>()
                .Where(d => d.MessageThreadMemberId == member.Id)
                .Where(d => request.MessageIds.Contains(d.MessageId))
                .Where(d => !d.IsDeleted)
                .ToListAsync(ct);

            var existingByMessage = existingDeliveries.ToDictionary(d => d.MessageId);
            var markedCount = 0;

            foreach (var messageId in request.MessageIds)
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
                        MessageThreadMemberId = member.Id,
                        MessageId = messageId,
                        TypeId = MessageDeliveryTypes.Read,
                        IsEnabled = true
                    });
                    markedCount++;
                }
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
}
