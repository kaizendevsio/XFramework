using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;

namespace Communications.Api.Services;

public sealed partial class ThreadService
{
    internal const string CallSummaryType = "CallSummary";

    public async Task<Result> RecordCallAsync(RecordCallRequest request, CancellationToken ct = default)
    {
        var actor = await requestContextResolver.ResolveTrustedInternalAsync(request.Metadata, [XFrameworkServiceNames.Yap], ct);
        if (!actor.IsSuccess) return Result.Failure(actor.Message ?? "Trusted call gateway required", actor.StatusCode);
        if (request.CallId == Guid.Empty || request.ThreadId == Guid.Empty || request.CallerId == Guid.Empty ||
            request.EndedAt == default || request.EndedAt > DateTimeOffset.UtcNow.AddMinutes(1) ||
            request.ConnectedAt is { } start && (start > request.EndedAt || request.EndedAt - start > TimeSpan.FromHours(2)))
            return Result.Failure("Invalid call outcome", 400);

        var tenant = actor.Data!.TenantId;
        await using var mutation = await ConversationMutationLock.AcquireAsync(db, tenant, request.ThreadId, ct);
        var existing = await dataContext.Query<Message>().Where(m => m.Id == request.CallId && m.TenantId == tenant).FirstOrDefaultAsync(ct);
        if (existing is not null)
            return existing.TenantId == tenant && existing.MessageThreadId == request.ThreadId && existing.TemplateType == CallSummaryType
                ? Result.Success() : Result.Failure("Call identity already in use", 409);

        var member = await dataContext.Query<MessageThreadMember>()
            .Where(m => m.TenantId == tenant && m.MessageThreadId == request.ThreadId && m.CredentialId == request.CallerId).FirstOrDefaultAsync(ct);
        if (member is null || !await dataContext.Query<MessageThread>().AnyAsync(t => t.TenantId == tenant && t.Id == request.ThreadId && !t.IsDeleted, ct))
            return Result.Failure("Conversation not found", 404);

        var duration = request.ConnectedAt is { } connected ? (int)(request.EndedAt - connected).TotalSeconds : 0;
        var kind = request.Video ? "video" : "voice";
        var text = request.ConnectedAt is null ? $"Missed {kind} call"
            : $"{(request.Video ? "Video" : "Voice")} call · {duration / 60}:{duration % 60:00}";
        dataContext.Add(new Message
        {
            Id = request.CallId, TenantId = tenant, MessageThreadId = request.ThreadId,
            MessageThreadMemberId = member.Id, Text = text, TemplateType = CallSummaryType,
            TemplateVariablesJson = System.Text.Json.JsonSerializer.Serialize(new { request.ConnectedAt, request.EndedAt, request.Video }),
            CreatedAt = request.EndedAt.UtcDateTime, IsEnabled = true, ConcurrencyStamp = Guid.NewGuid()
        });
        AddOutboxEvent(MessageRealtimeEvents.MessageCreated, tenant, request.ThreadId, request.CallId,
            nameof(Message), request.CallerId, new { ThreadId = request.ThreadId, MessageId = request.CallId, SenderMemberId = member.Id });
        await SaveAndSignalAsync(ct);
        return Result.Success();
    }
}
