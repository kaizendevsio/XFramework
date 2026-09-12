using Communications.Domain.Shared.Contracts.Requests.Attachments;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Communications.Api.Services;

public sealed partial class ThreadService
{
    public async Task<Result<StorageUploadSessionResponse>> CreateChatAttachmentUploadAsync(
        CreateChatAttachmentUploadRequest request, CancellationToken ct = default)
    {
        var callerResult = await ResolveCallerAsync(request.Metadata, ct);
        if (!callerResult.IsSuccess) return CallerFailure<StorageUploadSessionResponse>(callerResult);
        var caller = callerResult.Data!;
        var member = await dataContext.Query<MessageThreadMember>()
            .Where(x => x.MessageThreadId == request.ThreadId && x.TenantId == caller.TenantId)
            .Where(x => x.CredentialId == caller.CredentialId && !x.IsDeleted && x.IsEnabled)
            .AnyAsync(ct);
        if (!member) return Result<StorageUploadSessionResponse>.Forbidden("Requester is not a member of this thread");
        var activeThread = await dataContext.Query<MessageThread>()
            .Where(x => x.Id == request.ThreadId && x.TenantId == caller.TenantId && !x.IsDeleted && x.IsEnabled)
            .AnyAsync(ct);
        if (!activeThread) return Result<StorageUploadSessionResponse>.NotFound("Thread not found");
        var feature = request.ContentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true
            ? ConversationFeatures.Voice : ConversationFeatures.Attachments;
        if (!await FeatureEnabledAsync(caller.TenantId, request.ThreadId, feature, ct))
            return Result<StorageUploadSessionResponse>.Forbidden("This attachment type is disabled for this conversation");

        var policy = await policyService.GetPolicyAsync(caller.TenantId, ct);
        if (request.TotalSizeBytes <= 0 ||
            policy.AttachmentMaxSizeBytes > 0 && request.TotalSizeBytes > policy.AttachmentMaxSizeBytes)
            return Result<StorageUploadSessionResponse>.Failure("File size exceeds the Communications attachment policy", 400);
        if (!IsAllowedAttachmentFileType(new StorageFileValidationResponse
            { Name = request.FileName, ContentType = request.ContentType }, policy))
            return Result<StorageUploadSessionResponse>.Failure("File type is not allowed for Communications attachments", 400);
        var rate = rateLimiter.Check(caller.TenantId, caller.CredentialId,
            CommunicationsRateLimitActions.AttachmentLink, policy.AttachmentLinkPerMinute);
        if (!rate.IsSuccess) return RateLimitFailure<StorageUploadSessionResponse>(rate);

        var response = await storageServiceWrapper.CreateChatStorageUploadSession(new CreateChatStorageUploadSessionRequest
        {
            Metadata = request.Metadata, ThreadId = request.ThreadId, FileName = request.FileName,
            ContentType = request.ContentType, TotalSizeBytes = request.TotalSizeBytes,
            ChunkSizeBytes = request.ChunkSizeBytes, ExpectedSha256Hash = request.ExpectedSha256Hash
        }, ct);
        return response.IsSuccess && response.Response is not null
            ? Result<StorageUploadSessionResponse>.Success(response.Response, (int)response.HttpStatusCode)
            : Result<StorageUploadSessionResponse>.Failure(response.Message, (int)response.HttpStatusCode);
    }

    public async Task<Result<StorageDownloadUrlResponse>> GetChatAttachmentDownloadUrlAsync(
        GetChatAttachmentDownloadUrlRequest request, CancellationToken ct = default)
    {
        var callerResult = await ResolveCallerAsync(request.Metadata, ct);
        if (!callerResult.IsSuccess) return CallerFailure<StorageDownloadUrlResponse>(callerResult);
        var caller = callerResult.Data!;
        var member = await dataContext.Query<MessageThreadMember>()
            .Where(x => x.MessageThreadId == request.ThreadId && x.TenantId == caller.TenantId)
            .Where(x => x.CredentialId == caller.CredentialId && !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);
        if (member is null) return Result<StorageDownloadUrlResponse>.Forbidden("Requester is not a member of this thread");
        var activeThread = await dataContext.Query<MessageThread>()
            .Where(x => x.Id == request.ThreadId && x.TenantId == caller.TenantId && !x.IsDeleted && x.IsEnabled)
            .AnyAsync(ct);
        if (!activeThread) return Result<StorageDownloadUrlResponse>.NotFound("Thread not found");
        var message = await dataContext.Query<Message>()
            .Where(x => x.Id == request.MessageId && x.MessageThreadId == request.ThreadId)
            .Where(x => x.TenantId == caller.TenantId && !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);
        if (message is null || !await CanAccessMessageAsync(caller.TenantId, member, message, ct))
            return Result<StorageDownloadUrlResponse>.NotFound("Message not found");
        var link = await dataContext.Query<MessageFile>()
            .Where(x => x.Id == request.FileId && x.MessageId == request.MessageId && x.TenantId == caller.TenantId)
            .Where(x => !x.IsDeleted && x.IsEnabled).FirstOrDefaultAsync(ct);
        if (link is null) return Result<StorageDownloadUrlResponse>.NotFound("Attachment not found");

        var response = await storageServiceWrapper.GetChatStorageDownloadUrl(new GetChatStorageDownloadUrlRequest
        {
            Metadata = request.Metadata, ThreadId = request.ThreadId, StorageFileId = link.StorageId,
            ExpirationMinutes = request.ExpirationMinutes
        }, ct);
        return response.IsSuccess && response.Response is not null
            ? Result<StorageDownloadUrlResponse>.Success(response.Response)
            : Result<StorageDownloadUrlResponse>.Failure(response.Message, (int)response.HttpStatusCode);
    }
}
