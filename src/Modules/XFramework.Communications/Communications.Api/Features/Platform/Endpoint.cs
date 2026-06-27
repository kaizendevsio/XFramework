using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Platform;

public static class CreateDirectThreadEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/threads/direct", Tags = ["Threads"],
        Summary = "Get or create a direct message thread",
        Description = "Creates a 1:1 direct message thread idempotently for the authenticated requester and another credential.")]
    public static Task<Result<CreateThreadResponse>> Handle(
        CreateDirectThreadRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.CreateDirectThreadAsync(request, ct);
}

public static class GetUnreadCountsEndpoint
{
    [BoltHandler]
    [MapGet("/api/communications/threads/unread-counts", Tags = ["Threads"],
        Summary = "Get unread message counts",
        Description = "Returns unread message counts per thread and the total unread count for the requester.")]
    public static Task<Result<GetUnreadCountsResponse>> Handle(
        GetUnreadCountsRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.GetUnreadCountsAsync(request, ct);
}

public static class LeaveThreadEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/threads/{threadId:guid}/leave", Tags = ["Threads"],
        Summary = "Leave a thread",
        Description = "Removes the requester from a thread while preserving at least one member.")]
    public static Task<Result<CmdResponse>> Handle(
        LeaveThreadRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.LeaveThreadAsync(request, ct);
}

public static class MuteThreadEndpoint
{
    [BoltHandler]
    [MapPatch("/api/communications/threads/{threadId:guid}/mute", Tags = ["Threads"],
        Summary = "Mute or unmute a thread",
        Description = "Updates the requester's per-thread mute state.")]
    public static Task<Result<CmdResponse>> Handle(
        MuteThreadRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.MuteThreadAsync(request, ct);
}

public static class ArchiveThreadEndpoint
{
    [BoltHandler]
    [MapPatch("/api/communications/threads/{threadId:guid}/archive", Tags = ["Threads"],
        Summary = "Archive or unarchive a thread",
        Description = "Updates the requester's per-thread archive state.")]
    public static Task<Result<CmdResponse>> Handle(
        ArchiveThreadRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.ArchiveThreadAsync(request, ct);
}

public static class CreateThreadInviteEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/threads/{threadId:guid}/invites", Tags = ["Thread Invites"],
        Summary = "Invite a credential to a thread",
        Description = "Creates a pending invitation for a credential to join a thread.")]
    public static Task<Result<CmdResponse>> Handle(
        CreateThreadInviteRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.CreateThreadInviteAsync(request, ct);
}

public static class AcceptThreadInviteEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/threads/{threadId:guid}/invites/{inviteId:guid}/accept", Tags = ["Thread Invites"],
        Summary = "Accept a thread invite",
        Description = "Accepts a pending thread invitation for the requester.")]
    public static Task<Result<CmdResponse>> Handle(
        RespondThreadInviteRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        request.Accept = true;
        return threadService.RespondThreadInviteAsync(request, ct);
    }
}

public static class DeclineThreadInviteEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/threads/{threadId:guid}/invites/{inviteId:guid}/decline", Tags = ["Thread Invites"],
        Summary = "Decline a thread invite",
        Description = "Declines a pending thread invitation for the requester.")]
    public static Task<Result<CmdResponse>> Handle(
        RespondThreadInviteRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        request.Accept = false;
        return threadService.RespondThreadInviteAsync(request, ct);
    }
}

public static class UpdateThreadMemberRoleEndpoint
{
    [BoltHandler]
    [MapPatch("/api/communications/threads/{threadId:guid}/members/{memberId:guid}/role", Tags = ["Thread Members"],
        Summary = "Update a thread member role",
        Description = "Updates a thread member role to Owner, Admin, or Member.")]
    public static Task<Result<CmdResponse>> Handle(
        UpdateThreadMemberRoleRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.UpdateThreadMemberRoleAsync(request, ct);
}

public static class PinMessageEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/threads/{threadId:guid}/messages/{messageId:guid}/pin", Tags = ["Messages"],
        Summary = "Pin a message",
        Description = "Pins a message in a thread.")]
    public static Task<Result<CmdResponse>> Handle(
        PinMessageRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.PinMessageAsync(request, ct);
}

public static class UnpinMessageEndpoint
{
    [BoltHandler]
    [MapDelete("/api/communications/threads/{threadId:guid}/messages/{messageId:guid}/pin", Tags = ["Messages"],
        Summary = "Unpin a message",
        Description = "Removes a pinned message from a thread.")]
    public static Task<Result<CmdResponse>> Handle(
        UnpinMessageRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.PinMessageAsync(new PinMessageRequest
        {
            Metadata = request.Metadata,
            ThreadId = request.ThreadId,
            MessageId = request.MessageId,
            IsPinned = false
        }, ct);
}

public static class SaveMessageEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/threads/{threadId:guid}/messages/{messageId:guid}/save", Tags = ["Messages"],
        Summary = "Save a message",
        Description = "Saves a message for the requester.")]
    public static Task<Result<CmdResponse>> Handle(
        SaveMessageRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.SaveMessageAsync(request, ct);
}

public static class UnsaveMessageEndpoint
{
    [BoltHandler]
    [MapDelete("/api/communications/threads/{threadId:guid}/messages/{messageId:guid}/save", Tags = ["Messages"],
        Summary = "Unsave a message",
        Description = "Removes a saved message for the requester.")]
    public static Task<Result<CmdResponse>> Handle(
        UnsaveMessageRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.SaveMessageAsync(new SaveMessageRequest
        {
            Metadata = request.Metadata,
            ThreadId = request.ThreadId,
            MessageId = request.MessageId,
            IsSaved = false
        }, ct);
}

public static class SearchMessagesEndpoint
{
    [BoltHandler]
    [MapGet("/api/communications/messages/search", Tags = ["Messages"],
        Summary = "Search messages",
        Description = "Searches messages scoped to the requester's tenant and thread memberships.")]
    public static Task<Result<SearchMessagesResponse>> Handle(
        SearchMessagesRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.SearchMessagesAsync(request, ct);
}

public static class ReportMessageEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/threads/{threadId:guid}/messages/{messageId:guid}/report", Tags = ["Moderation"],
        Summary = "Report a message",
        Description = "Creates a moderation report for a message.")]
    public static Task<Result<CmdResponse>> Handle(
        ReportMessageRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.ReportMessageAsync(request, ct);
}

public static class BlockCredentialEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/blocks", Tags = ["Moderation"],
        Summary = "Block a credential",
        Description = "Blocks another credential from 1:1 direct communications with the requester.")]
    public static Task<Result<CmdResponse>> Handle(
        BlockCredentialRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.BlockCredentialAsync(request, ct);
}

public static class DeleteCredentialBlockEndpoint
{
    [BoltHandler]
    [MapDelete("/api/communications/blocks/{credentialId:guid}", Tags = ["Moderation"],
        Summary = "Remove a credential block",
        Description = "Removes a 1:1 direct communications block created by the requester.")]
    public static Task<Result<CmdResponse>> Handle(
        DeleteCredentialBlockRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.DeleteCredentialBlockAsync(request, ct);
}
