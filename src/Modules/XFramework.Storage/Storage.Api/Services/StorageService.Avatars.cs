using System.Security.Cryptography;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;

namespace Storage.Api.Services;

public sealed partial class StorageService
{
    public async Task<Result<StorageFileResponse>> UploadOwnAvatarFileAsync(
        UploadOwnAvatarFileRequest request, CancellationToken ct = default)
    {
        var context = trustedInvocationContextAccessor.Current;
        if (context?.Service?.ClientId != XFrameworkServiceNames.IdentityServer ||
            context.Actor is not { CredentialId: var owner } || owner == Guid.Empty)
            return Result<StorageFileResponse>.Forbidden("Avatar uploads require an Identity actor invocation");

        var bytes = request.Bytes;
        if (bytes is not { Length: >= 12 and <= 5 * 1024 * 1024 })
            return Result<StorageFileResponse>.Failure("Choose an image up to 5 MB", 400);
        var (type, extension) = bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xd8, 0xff }) ? ("image/jpeg", "jpg")
            : bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? ("image/png", "png")
            : bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) ? ("image/webp", "webp")
            : (null, null);
        if (type is null) return Result<StorageFileResponse>.Failure("Choose a JPEG, PNG or WebP image", 400);

        var metadata = await EnsureUploadMetadataAsync(new()
        {
            Metadata = request.Metadata, ContentType = type,
            IdentifierGroupName = "IdentityServer", IdentifierName = "IdentityCredentialAvatar"
        }, ct);
        if (!metadata.IsSuccess) return Result<StorageFileResponse>.Failure(metadata.Message, metadata.StatusCode);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var session = await CreateUploadSessionAsync(new()
        {
            Metadata = request.Metadata, FileName = $"profile.{extension}", ContentType = type,
            TypeId = metadata.Data!.TypeId, StorageFileIdentifierId = metadata.Data.StorageFileIdentifierId,
            Identifier = owner, TotalSizeBytes = bytes.Length, ChunkSizeBytes = bytes.Length,
            ExpectedSha256Hash = hash, Visibility = StorageFileVisibility.Public, RequireClaim = true
        }, ct);
        if (!session.IsSuccess) return Result<StorageFileResponse>.Failure(session.Message, session.StatusCode);
        var completed = false;
        try
        {
            var part = await UploadPartAsync(new()
            {
                Metadata = request.Metadata, UploadSessionId = session.Data!.Id,
                PartNumber = 1, OffsetBytes = 0, ChunkBytes = bytes, PartSha256Hash = hash
            }, ct);
            if (!part.IsSuccess) return Result<StorageFileResponse>.Failure(part.Message, part.StatusCode);
            var result = await CompleteUploadAsync(new()
            {
                Metadata = request.Metadata, UploadSessionId = session.Data!.Id, ExpectedSha256Hash = hash
            }, ct);
            completed = result.IsSuccess;
            return result;
        }
        finally
        {
            if (!completed)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await AbortUploadAsync(new() { Metadata = request.Metadata, UploadSessionId = session.Data!.Id }, cleanup.Token); }
                catch (Exception ex) { logger.LogWarning(ex, "Could not abort failed avatar upload {SessionId}", session.Data!.Id); }
            }
        }
    }
}
