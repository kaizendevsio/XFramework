using System.Net;
using Messaging.Domain.Shared.Contracts;
using Messaging.Domain.Shared.Contracts.Requests.Attachments;
using Messaging.Domain.Shared.Contracts.Requests.Delete;
using Messaging.Domain.Shared.Contracts.Requests.Edit;
using Messaging.Domain.Shared.Contracts.Requests.Reactions;
using Messaging.Domain.Shared.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;

namespace Messaging.Api.Services;

public sealed class ThreadService(
    IDataContext dataContext,
    ILogger<ThreadService> logger
) : IThreadService
{
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
}
