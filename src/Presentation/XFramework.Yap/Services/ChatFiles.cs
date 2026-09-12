using Communications.Integration.Clients;
using Communications.Domain.Shared.Contracts.Requests.Attachments;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Forms;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Security;
using Yap.Contracts;

namespace Yap.Services;

public sealed class ChatFiles(IStorageServiceWrapper storage, ICommunicationsChatClient chat,
    ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, ILogger<ChatFiles> logger)
{
    public const long MaxFileBytes = ChatLimits.MaxFileBytes;
    public const long StagedFileBytes = ChatLimits.StagedFileBytes;
    public const int PreferredChunkBytes = ChatLimits.PreferredChunkBytes;
    public const long PartRequestBytes = ChatLimits.PartRequestBytes;

    public async Task<ChatUploadTicket> BeginAsync(Guid threadId, string fileName, string contentType,
        long totalBytes, CancellationToken ct)
    {
        if (totalBytes is <= 0 or > MaxFileBytes)
            throw new ChatOperationException($"Choose a file between 1 byte and {MaxFileBytes / (1024 * 1024 * 1024)} GB.");
        var upload = await CreateSessionAsync(threadId, fileName, contentType, totalBytes, ct);
        var chunk = upload.ChunkSizeBytes;
        if (chunk <= 0) throw new ChatOperationException("The upload service returned an unsupported part size.");
        return new ChatUploadTicket(upload.Id, chunk, (int)((totalBytes + chunk - 1) / chunk));
    }

    /// <summary>
    /// Hashing happens here rather than in the browser: storage must verify bytes it
    /// actually received, and a client-supplied digest would prove nothing.
    /// </summary>
    public async Task UploadPartAsync(Guid uploadId, int partNumber, long offset, byte[] bytes, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        using var token = tokens.Push(actor.AccessToken!);
        YapApi.Require(await storage.UploadChatStorageFilePart(new UploadChatStorageFilePartRequest
        {
            UploadSessionId = uploadId, PartNumber = partNumber, OffsetBytes = offset,
            ChunkBytes = bytes, PartSha256Hash = Convert.ToHexString(SHA256.HashData(bytes)), Metadata = Metadata(actor)
        }, ct));
    }

    public async Task<Guid> CompleteAsync(Guid uploadId, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        using var token = tokens.Push(actor.AccessToken!);
        return await FinishAsync(uploadId, Metadata(actor), ct);
    }

    public async Task AbortAsync(Guid uploadId, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        using var token = tokens.Push(actor.AccessToken!);
        YapApi.Require(await storage.AbortChatStorageUploadSession(new AbortChatStorageUploadSessionRequest
        { UploadSessionId = uploadId, Metadata = Metadata(actor) }, ct));
    }

    /// <summary>
    /// Single-request path for attachments small enough to be staged on the device.
    /// Larger files use <see cref="BeginAsync"/> so a dropped connection does not restart the transfer.
    /// </summary>
    public async Task<Guid> UploadAsync(IBrowserFile file, Guid threadId, IProgress<int>? progress, CancellationToken ct)
    {
        if (file.Size is <= 0 or > StagedFileBytes)
            throw new ChatOperationException($"Choose a file between 1 byte and {StagedFileBytes / (1024 * 1024)} MB.");
        var actor = await ActorAsync(ct);
        using var token = tokens.Push(actor.AccessToken!);
        var metadata = Metadata(actor);
        var upload = await CreateSessionAsync(threadId, Path.GetFileName(file.Name),
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType, file.Size, ct);
        var completedUpload = false;
        try
        {
            await using var stream = file.OpenReadStream(StagedFileBytes, ct);
            // Honor the provider's negotiated part size (S3 may require at least 5 MB).
            if (upload.ChunkSizeBytes <= 0 || upload.ChunkSizeBytes > StagedFileBytes)
                throw new ChatOperationException("The upload service returned an unsupported part size.");
            var buffer = new byte[upload.ChunkSizeBytes];
            long offset = 0;
            var part = 1;
            while (offset < file.Size)
            {
                var length = (int)Math.Min(buffer.Length, file.Size - offset);
                await stream.ReadExactlyAsync(buffer.AsMemory(0, length), ct);
                var partBytes = buffer.AsSpan(0, length).ToArray();
                YapApi.Require(await storage.UploadChatStorageFilePart(new UploadChatStorageFilePartRequest
                {
                    UploadSessionId = upload.Id, PartNumber = part++, OffsetBytes = offset,
                    ChunkBytes = partBytes, PartSha256Hash = Convert.ToHexString(SHA256.HashData(partBytes)), Metadata = metadata
                }, ct));
                offset += length;
                progress?.Report((int)(offset * 100 / file.Size));
            }
            var id = await FinishAsync(upload.Id, metadata, ct);
            completedUpload = true;
            return id;
        }
        catch
        {
            if (!completedUpload) await SafeAbortAsync(upload.Id, metadata);
            throw;
        }
    }

    private async Task<Storage.Domain.Shared.Contracts.Responses.StorageUploadSessionResponse> CreateSessionAsync(
        Guid threadId, string fileName, string contentType, long totalBytes, CancellationToken ct)
    {
        var session = await chat.ForCurrentActorAsync(ct: ct);
        return YapApi.Require(await session.CreateAttachmentUploadAsync(new CreateChatAttachmentUploadRequest
        {
            ThreadId = threadId, FileName = Path.GetFileName(fileName),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            TotalSizeBytes = totalBytes,
            // Fewer, larger parts keep a multi-gigabyte transfer inside the provider's part ceiling.
            ChunkSizeBytes = totalBytes > StagedFileBytes ? PreferredChunkBytes : null
        }, ct));
    }

    private async Task<Guid> FinishAsync(Guid uploadId, RequestMetadata metadata, CancellationToken ct)
    {
        var completed = YapApi.Require(await storage.CompleteChatStorageUploadSession(new CompleteChatStorageUploadSessionRequest
        { UploadSessionId = uploadId, Metadata = metadata }, ct));
        for (var attempt = 0; attempt < 60 && completed.Status is StorageFileStatus.Verifying or StorageFileStatus.VerificationInProgress; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            completed = YapApi.Require(await storage.GetStorageFile(new GetStorageFileRequest
            { StorageFileId = completed.Id, Metadata = metadata }, ct));
        }
        if (completed.Status != StorageFileStatus.Available)
            throw new ChatOperationException("The attachment is not ready to share. Try again after the file has been checked.");
        return completed.Id;
    }

    private async Task SafeAbortAsync(Guid uploadId, RequestMetadata metadata)
    {
        try { YapApi.Require(await storage.AbortChatStorageUploadSession(new AbortChatStorageUploadSessionRequest { UploadSessionId = uploadId, Metadata = metadata }, CancellationToken.None)); }
        catch (Exception ex) { logger.LogWarning(ex, "Could not abort incomplete Yap upload {UploadId}.", uploadId); }
    }

    /// <summary>
    /// Resolves the actor only. The access token rides on an AsyncLocal, so the caller
    /// must push it in its own body: a push inside an async helper is discarded on return.
    /// </summary>
    private async Task<CommunicationsChatActor> ActorAsync(CancellationToken ct) =>
        await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();

    private static RequestMetadata Metadata(CommunicationsChatActor actor) => new()
    { RequestedTenantId = actor.TenantId, RequestId = Guid.NewGuid(), OperationName = "Yap attachment" };

    public async Task AttachAsync(Guid threadId, Guid messageId, Guid storageId, CancellationToken ct)
    {
        var session = await chat.ForCurrentActorAsync(ct: ct);
        var result = await session.AttachFileAsync(threadId, messageId, storageId, ct);
        if ((int)result.HttpStatusCode == 409)
        {
            // A prior attempt may have committed before its response was interrupted.
            var linked = YapApi.Require(await session.GetFilesAsync(threadId, messageId, pageSize: 100, ct: ct));
            if (linked.Items.Any(file => file.StorageFileId == storageId)) return;
        }
        YapApi.Require(result);
    }

    public async Task<IReadOnlyList<ChatFileLink>> GetLinksAsync(Guid threadId, Guid messageId, CancellationToken ct)
    {
        var session = await chat.ForCurrentActorAsync(ct: ct);
        var files = YapApi.Require(await session.GetFilesAsync(threadId, messageId, pageSize: 100, ct: ct));
        var links = new List<ChatFileLink>();
        foreach (var file in files.Items)
        {
            var result = YapApi.Require(await session.GetAttachmentDownloadUrlAsync(threadId, messageId, file.Id, ct));
            if (!Uri.TryCreate(result.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                throw new ChatOperationException("This attachment cannot be opened.");
            links.Add(new ChatFileLink(file.Id, result.Url));
        }
        return links;
    }

    public async Task<List<Yap.Contracts.ChatAttachment>> GetDetailsAsync(Guid threadId, Guid messageId, CancellationToken ct)
    {
        var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
        using var token = tokens.Push(actor.AccessToken!);
        var session = await chat.ForCurrentActorAsync(ct: ct);
        var files = YapApi.Require(await session.GetFilesAsync(threadId, messageId, pageSize: 100, ct: ct));
        var details = new List<Yap.Contracts.ChatAttachment>();
        foreach (var file in files.Items)
        {
            var data = YapApi.Require(await storage.GetStorageFile(new GetStorageFileRequest
            {
                StorageFileId = file.StorageFileId,
                Metadata = new RequestMetadata { RequestedTenantId = actor.TenantId, RequestId = Guid.NewGuid(), OperationName = "Yap attachment details" }
            }, ct));
            details.Add(new(file.Id, data.Name, data.ContentType ?? "application/octet-stream", data.ContentLengthBytes ?? 0));
        }
        return details;
    }
}

public sealed record ChatFileLink(Guid Id, string Url);
public sealed record ChatUploadTicket(Guid UploadId, int ChunkSizeBytes, int TotalParts);
