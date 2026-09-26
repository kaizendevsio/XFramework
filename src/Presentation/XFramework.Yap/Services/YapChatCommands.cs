using Communications.Domain.Shared.Contracts.Realtime;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Integration.Clients;
using Yap.Contracts;

namespace Yap.Services;

// Both browser transports use the same validation and module-owned business operations.
internal static class YapChatCommands
{
    internal static async Task<MessageReceipt> SendAsync(SendMessage request, ICommunicationsChatSession session,
        bool encryptionRequired, CancellationToken ct)
    {
        if (encryptionRequired && request.EncryptedEnvelope is null)
            throw new YapApiException(409, "Update Yap and unlock encryption before sending.");
        if (request.Id == Guid.Empty || request.ThreadId == Guid.Empty || string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 4000)
            throw new YapApiException(400, "Write a message up to 4,000 characters.");
        var data = YapApi.Require(await session.SendMessageAsync(new CreateThreadMessageRequest
        {
            ThreadId = request.ThreadId, Text = request.Text, ParentMessageId = request.ParentId, ClientMessageId = request.Id,
            IsThreadReply = request.IsThreadReply, EncryptedEnvelope = request.EncryptedEnvelope,
            RecipientCredentialIds = request.RecipientCredentialIds ?? [], EncryptionSenderDeviceId = request.EncryptionSenderDeviceId,
            SenderDirectoryRevision = request.SenderDirectoryRevision, RecipientDirectoryRevisions = request.RecipientDirectoryRevisions ?? []
        }, ct));
        return new(data.MessageId);
    }

    internal static async Task ReadAsync(ReadMessages request, ICommunicationsChatSession session, CancellationToken ct)
    {
        if (request.ThreadId == Guid.Empty || request.MessageIds is not { Count: > 0 and <= 100 } || request.MessageIds.Contains(Guid.Empty))
            throw new YapApiException(400, "Choose up to 100 messages.");
        YapApi.Require(await session.MarkReadAsync(request.ThreadId, request.MessageIds, ct));
    }

    internal static async Task TypingAsync(ThreadAction request, ICommunicationsChatSession session, CancellationToken ct)
    {
        if (request.ThreadId == Guid.Empty || request.Action != "typing") throw new YapApiException(400, "Choose a conversation.");
        if (!Enum.IsDefined(request.Activity)) throw new YapApiException(400, "Choose a supported activity.");
        if (request.Activity == ChatActivity.Typing) await session.PublishTypingAsync(request.ThreadId, request.Value, ct);
        else await session.PublishTypingAsync(request.ThreadId, request.Value, (CommunicationsTypingActivity)request.Activity, request.Count, ct);
    }

    /// <summary>Projects a typing-channel event for the browser: only the kind and count of an
    /// attachment in progress, and an unknown kind from a newer module reads as plain typing.</summary>
    internal static TypingUpdate Typing(CommunicationsTypingState state)
    {
        var activity = Enum.IsDefined((ChatActivity)state.Activity) ? (ChatActivity)state.Activity : ChatActivity.Typing;
        var count = activity is ChatActivity.Typing or ChatActivity.Recording ? 0 : Math.Clamp(state.Count, 1, 99);
        return new TypingUpdate(state.ThreadId, state.CredentialId, state.IsTyping, activity, count);
    }
}
