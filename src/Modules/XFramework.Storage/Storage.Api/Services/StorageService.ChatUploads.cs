using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;

namespace Storage.Api.Services;

public sealed partial class StorageService
{
    public async Task<Result<StorageUploadSessionResponse>> CreateChatUploadSessionAsync(
        CreateChatStorageUploadSessionRequest request, CancellationToken ct = default)
    {
        var actor = trustedInvocationContextAccessor.Current?.Actor;
        if (!IsCommunicationsCaller() || actor is null)
            return Result<StorageUploadSessionResponse>.Forbidden("Chat uploads require a Communications actor invocation");
        if (request.ThreadId == Guid.Empty)
            return Result<StorageUploadSessionResponse>.Failure("Thread ID is required", 400);

        // Only fixed owner-defined metadata can be created by the ordinary upload path.
        var metadata = await EnsureUploadMetadataAsync(new EnsureStorageUploadMetadataRequest
        {
            Metadata = request.Metadata,
            ContentType = "application/octet-stream",
            IdentifierGroupName = "Communications",
            IdentifierName = "Chat attachments"
        }, ct);
        if (!metadata.IsSuccess)
            return Result<StorageUploadSessionResponse>.Failure(metadata.Message, metadata.StatusCode);

        return await CreateUploadSessionCoreAsync(new CreateStorageUploadSessionRequest
        {
            Metadata = request.Metadata,
            FileName = request.FileName,
            ContentType = request.ContentType,
            TotalSizeBytes = request.TotalSizeBytes,
            ChunkSizeBytes = request.ChunkSizeBytes,
            ExpectedSha256Hash = request.ExpectedSha256Hash,
            TypeId = metadata.Data!.TypeId,
            StorageFileIdentifierId = metadata.Data.StorageFileIdentifierId,
            Identifier = request.ThreadId,
            Visibility = StorageFileVisibility.Private
        }, actor.CredentialId, StorageUploadPurposes.ChatAttachment, ct);
    }

    public Task<Result<StorageUploadPartResponse>> UploadChatPartAsync(
        UploadChatStorageFilePartRequest request, CancellationToken ct = default) =>
        UploadPartCoreAsync(new UploadStorageFilePartRequest
        {
            Metadata = request.Metadata, UploadSessionId = request.UploadSessionId,
            PartNumber = request.PartNumber, OffsetBytes = request.OffsetBytes,
            PartSha256Hash = request.PartSha256Hash, ChunkBytes = request.ChunkBytes
        }, true, ct);

    public Task<Result<StorageFileResponse>> CompleteChatUploadAsync(
        CompleteChatStorageUploadSessionRequest request, CancellationToken ct = default) =>
        CompleteUploadCoreAsync(new CompleteStorageUploadSessionRequest
        {
            Metadata = request.Metadata, UploadSessionId = request.UploadSessionId,
            ExpectedSha256Hash = request.ExpectedSha256Hash
        }, true, ct);

    public Task<Result> AbortChatUploadAsync(
        AbortChatStorageUploadSessionRequest request, CancellationToken ct = default) =>
        AbortUploadCoreAsync(new AbortStorageUploadSessionRequest
        {
            Metadata = request.Metadata, UploadSessionId = request.UploadSessionId
        }, true, ct);

    public async Task<Result<StorageFileValidationResponse>> ValidateChatFileReferenceAsync(
        ValidateChatStorageFileReferenceRequest request, CancellationToken ct = default)
    {
        var file = await FindChatFileAsync(request.StorageFileId, request.ThreadId, request.Metadata, ct);
        if (file is null || !CanMutateUpload(file, true))
            return Result<StorageFileValidationResponse>.Forbidden("Chat attachment is not owned by the current actor in this thread");
        return await ValidateFileReferenceAsync(new ValidateStorageFileReferenceRequest
        {
            Metadata = request.Metadata, StorageFileId = request.StorageFileId, RequireAvailable = true
        }, ct);
    }

    public async Task<Result<StorageDownloadUrlResponse>> GetChatDownloadUrlAsync(
        GetChatStorageDownloadUrlRequest request, CancellationToken ct = default)
    {
        var file = await FindChatFileAsync(request.StorageFileId, request.ThreadId, request.Metadata, ct);
        if (file is null)
            return Result<StorageDownloadUrlResponse>.NotFound("Chat attachment not found");
        // Communications has already checked the active member, visible message, and link.
        // Storage never reads or writes Communications tables.
        return await GetDownloadUrlCoreAsync(new GetStorageDownloadUrlRequest
        {
            Metadata = request.Metadata, StorageFileId = request.StorageFileId,
            ExpirationMinutes = request.ExpirationMinutes
        }, true, ct);
    }

    private async Task<StorageFile?> FindChatFileAsync(
        Guid storageFileId, Guid threadId, RequestMetadata metadata, CancellationToken ct)
    {
        if (!IsCommunicationsCaller() || trustedInvocationContextAccessor.Current?.Actor is null || threadId == Guid.Empty)
            return null;
        var tenant = await ResolveTenantIdAsync(metadata, ct);
        if (!tenant.IsSuccess) return null;
        return await db.Set<StorageFile>().AsNoTracking().FirstOrDefaultAsync(file =>
            file.Id == storageFileId && file.TenantId == tenant.Data && file.Identifier == threadId &&
            file.UploadPurpose == StorageUploadPurposes.ChatAttachment && !file.IsDeleted && file.IsEnabled, ct);
    }

    private bool IsCommunicationsCaller() => string.Equals(
        trustedInvocationContextAccessor.Current?.Service?.ClientId,
        XFrameworkServiceNames.Communications, StringComparison.Ordinal);

    private bool CanMutateUpload(StorageFile file, bool chatOnly) =>
        file.UploadPurpose == StorageUploadPurposes.ChatAttachment
            ? trustedInvocationContextAccessor.Current?.Actor?.CredentialId is { } actor &&
              file.UploadedByCredentialId == actor
            : !chatOnly;
}
