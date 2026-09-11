using System.Security.Cryptography;
using System.Text;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Requests.ReferenceData;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Communications.Api.Services;

public sealed class ChatReferenceDataService(
    DbContext db,
    ICommunicationsRequestContextResolver contextResolver,
    ILogger<ChatReferenceDataService> logger)
{
    private static readonly (string Name, string Emoji)[] Reactions =
        [("Like", "👍"), ("Love", "❤️"), ("Laugh", "😂"), ("Celebrate", "🎉"), ("Surprised", "😮"), ("Sad", "😢")];

    public async Task<Result<ChatReferenceDataResponse>> EnsureAsync(
        EnsureChatDefaultsRequest request, CancellationToken ct = default)
    {
        var caller = await contextResolver.ResolveAsync(request.Metadata, ct);
        if (!caller.IsSuccess)
            return Result<ChatReferenceDataResponse>.Failure(caller.Message!, caller.StatusCode);
        var tenantId = caller.Data!.TenantId;

        try
        {
            // Serialize first-use provisioning across service instances. The transaction owns only
            // Communications rows; no tenant/Identity schema writes or runtime migrations occur.
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var lockKey = BitConverter.ToInt64(SHA256.HashData(
                    Encoding.UTF8.GetBytes($"communications:chat-defaults:{tenantId:N}")));
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", ct);

                var messageType = await db.Set<MessageType>().AsNoTracking()
                    .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled)
                    .Where(x => x.SystemReferenceId == MessageTypes.Chat || x.Id == MessageTypes.Chat)
                    .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct);
                if (messageType is null)
                {
                    messageType = new MessageType
                    {
                        Id = Guid.NewGuid(), TenantId = tenantId, Name = "Chat",
                        SystemReferenceId = MessageTypes.Chat, IsEnabled = true,
                        CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid()
                    };
                    db.Add(messageType);
                }

                var threadType = await db.Set<MessageThreadType>().AsNoTracking()
                    .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled)
                    .Where(x => x.MessageTypeId == messageType.Id)
                    .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct);
                if (threadType is null)
                    db.Add(new MessageThreadType
                    {
                        Id = Guid.NewGuid(), TenantId = tenantId, Name = "Chat",
                        MessageTypeId = messageType.Id, SystemReferenceId = MessageTypes.Chat,
                        IsEnabled = true, CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid()
                    });

                var reactionTypes = await db.Set<MessageReactionType>().AsNoTracking()
                    .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled)
                    .Take(100).ToListAsync(ct);
                foreach (var (name, emoji) in Reactions)
                    if (!reactionTypes.Any(x => x.Emoji == emoji))
                        db.Add(new MessageReactionType
                        {
                            Id = Guid.NewGuid(), TenantId = tenantId, Name = name, Emoji = emoji,
                            SystemReferenceId = Guid.NewGuid(), IsEnabled = true,
                            CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid()
                        });

                foreach (var (name, referenceId) in new[]
                         { ("Delivered", MessageDeliveryTypes.Delivered), ("Read", MessageDeliveryTypes.Read) })
                    if (!await db.Set<MessageDeliveryType>().AsNoTracking()
                            .AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled &&
                                (x.SystemReferenceId == referenceId || x.Id == referenceId), ct))
                        db.Add(new MessageDeliveryType
                        {
                            Id = Guid.NewGuid(), TenantId = tenantId, Name = name,
                            SystemReferenceId = referenceId, IsEnabled = true,
                            CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid()
                        });

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return await ReadAsync(tenantId, ct);
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not provision chat defaults for tenant {TenantId}", tenantId);
            return Result<ChatReferenceDataResponse>.Failure("Could not initialize chat reference data", 503);
        }
    }

    public async Task<Result<ChatReferenceDataResponse>> GetAsync(
        GetChatReferenceDataRequest request, CancellationToken ct = default)
    {
        var caller = await contextResolver.ResolveAsync(request.Metadata, ct);
        return caller.IsSuccess
            ? await ReadAsync(caller.Data!.TenantId, ct)
            : Result<ChatReferenceDataResponse>.Failure(caller.Message!, caller.StatusCode);
    }

    private async Task<Result<ChatReferenceDataResponse>> ReadAsync(Guid tenantId, CancellationToken ct)
    {
        var threadType = await db.Set<MessageThreadType>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled)
            .Where(x => x.MessageType.TenantId == tenantId && !x.MessageType.IsDeleted && x.MessageType.IsEnabled)
            .Where(x => x.MessageType.SystemReferenceId == MessageTypes.Chat || x.MessageTypeId == MessageTypes.Chat)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { x.Id, x.MessageTypeId })
            .FirstOrDefaultAsync(ct);
        if (threadType is null)
            return Result<ChatReferenceDataResponse>.NotFound("Initialize chat defaults before using chat");

        return Result<ChatReferenceDataResponse>.Success(new ChatReferenceDataResponse
        {
            MessageTypeId = threadType.MessageTypeId,
            ThreadTypeId = threadType.Id,
            ReactionTypes = await db.Set<MessageReactionType>().AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled)
                .OrderBy(x => x.Name).ThenBy(x => x.Id).Take(100)
                .Select(x => new ReactionTypeResponse { Id = x.Id, Name = x.Name, Emoji = x.Emoji })
                .ToListAsync(ct)
        });
    }
}
