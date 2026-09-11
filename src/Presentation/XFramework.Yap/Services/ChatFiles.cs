using Communications.Integration.Clients;
using Communications.Domain.Shared.Contracts.Requests.Attachments;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Forms;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Security;

namespace Yap.Services;

public sealed class ChatFiles(IStorageServiceWrapper storage, ICommunicationsChatClient chat,
    ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, ILogger<ChatFiles> logger)
{
    public const long MaxFileBytes = 20 * 1024 * 1024;

    public async Task<Guid> UploadAsync(IBrowserFile file, Guid threadId, IProgress<int>? progress, CancellationToken ct)
    {
        if (file.Size is <= 0 or > MaxFileBytes) throw new ChatOperationException("Choose a file between 1 byte and 20 MB.");
        var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
        using var token = tokens.Push(actor.AccessToken!);
        var metadata = new RequestMetadata { RequestedTenantId = actor.TenantId, RequestId = Guid.NewGuid(), OperationName = "Yap attachment" };
        var session = await chat.ForCurrentActorAsync(ct: ct);
        var upload = ChatWorkspace.Require(await session.CreateAttachmentUploadAsync(new CreateChatAttachmentUploadRequest
        {
            ThreadId = threadId, FileName = Path.GetFileName(file.Name),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            TotalSizeBytes = file.Size
        }, ct));
        var completedUpload = false;
        try
        {
            await using var stream = file.OpenReadStream(MaxFileBytes, ct);
            // Honor the provider's negotiated part size (S3 may require at least 5 MB).
            if (upload.ChunkSizeBytes <= 0 || upload.ChunkSizeBytes > MaxFileBytes)
                throw new ChatOperationException("The upload service returned an unsupported part size.");
            var buffer = new byte[upload.ChunkSizeBytes];
            long offset = 0;
            var part = 1;
            while (offset < file.Size)
            {
                var length = (int)Math.Min(buffer.Length, file.Size - offset);
                await stream.ReadExactlyAsync(buffer.AsMemory(0, length), ct);
                var partBytes = buffer.AsSpan(0, length).ToArray();
                ChatWorkspace.Require(await storage.UploadChatStorageFilePart(new UploadChatStorageFilePartRequest
                {
                    UploadSessionId = upload.Id, PartNumber = part++, OffsetBytes = offset,
                    ChunkBytes = partBytes, PartSha256Hash = Convert.ToHexString(SHA256.HashData(partBytes)), Metadata = metadata
                }, ct));
                offset += length;
                progress?.Report((int)(offset * 100 / file.Size));
            }
            var completed = ChatWorkspace.Require(await storage.CompleteChatStorageUploadSession(new CompleteChatStorageUploadSessionRequest
            { UploadSessionId = upload.Id, Metadata = metadata }, ct));
            completedUpload = true;
            for (var attempt = 0; attempt < 60 && completed.Status is StorageFileStatus.Verifying or StorageFileStatus.VerificationInProgress; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                completed = ChatWorkspace.Require(await storage.GetStorageFile(new GetStorageFileRequest
                { StorageFileId = completed.Id, Metadata = metadata }, ct));
            }
            if (completed.Status != StorageFileStatus.Available)
                throw new ChatOperationException("The attachment is not ready to share. Try again after the file has been checked.");
            return completed.Id;
        }
        catch
        {
            if (!completedUpload)
            {
                try { ChatWorkspace.Require(await storage.AbortChatStorageUploadSession(new AbortChatStorageUploadSessionRequest { UploadSessionId = upload.Id, Metadata = metadata }, CancellationToken.None)); }
                catch (Exception ex) { logger.LogWarning(ex, "Could not abort incomplete Yap upload {UploadId}.", upload.Id); }
            }
            throw;
        }
    }

    public async Task AttachAsync(Guid threadId, Guid messageId, Guid storageId, CancellationToken ct)
    {
        var session = await chat.ForCurrentActorAsync(ct: ct);
        var result = await session.AttachFileAsync(threadId, messageId, storageId, ct);
        if ((int)result.HttpStatusCode == 409)
        {
            // A prior attempt may have committed before its response was interrupted.
            var linked = ChatWorkspace.Require(await session.GetFilesAsync(threadId, messageId, pageSize: 100, ct: ct));
            if (linked.Items.Any(file => file.StorageFileId == storageId)) return;
        }
        ChatWorkspace.Require(result);
    }

    public async Task<IReadOnlyList<ChatFileLink>> GetLinksAsync(Guid threadId, Guid messageId, CancellationToken ct)
    {
        var session = await chat.ForCurrentActorAsync(ct: ct);
        var files = ChatWorkspace.Require(await session.GetFilesAsync(threadId, messageId, pageSize: 100, ct: ct));
        var links = new List<ChatFileLink>();
        foreach (var file in files.Items)
        {
            var result = ChatWorkspace.Require(await session.GetAttachmentDownloadUrlAsync(threadId, messageId, file.Id, ct));
            if (!Uri.TryCreate(result.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                throw new ChatOperationException("This attachment cannot be opened.");
            links.Add(new ChatFileLink(file.Id, result.Url));
        }
        return links;
    }
}

public sealed record ChatFileLink(Guid Id, string Url);
