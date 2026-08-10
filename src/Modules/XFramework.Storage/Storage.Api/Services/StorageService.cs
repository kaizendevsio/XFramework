using System.Net;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Storage.Api.Services.Providers;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Storage.Api.Services;

public sealed partial class StorageService(
    AppDbContext db,
    IStorageProviderFactory providerFactory,
    IOptions<StorageOptions> options,
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor,
    ILogger<StorageService> logger)
{
    private readonly StorageOptions storageOptions = options.Value;
    private const int MaxChunkSizeBytes = 100 * 1024 * 1024;
    private const int S3MinimumNonFinalPartSizeBytes = 5 * 1024 * 1024;
    private const int S3MaximumPartCount = 10_000;
    private const int AzureMaximumBlockCount = 50_000;
    private const int MaxFileNameLength = 255;
    private const int MaxContentTypeLength = 255;
    private const int MaxProviderProfileNameLength = 200;

    public async Task<Result<StorageUploadMetadataResponse>> EnsureUploadMetadataAsync(
        EnsureStorageUploadMetadataRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ContentType) || request.ContentType.Length > MaxContentTypeLength ||
            string.IsNullOrWhiteSpace(request.IdentifierGroupName) || request.IdentifierGroupName.Length > 200 ||
            string.IsNullOrWhiteSpace(request.IdentifierName) || request.IdentifierName.Length > 200 ||
            request.IdentifierDescription?.Length > 500)
        {
            return Result<StorageUploadMetadataResponse>.Failure("Storage upload metadata is invalid", 400);
        }

        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageUploadMetadataResponse>(tenantResult);

        var tenantId = tenantResult.Data;
        var contentType = request.ContentType.Trim().ToLowerInvariant();
        var groupName = request.IdentifierGroupName.Trim();
        var identifierName = request.IdentifierName.Trim();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var metadataLockKey = $"{tenantId:D}:{contentType}:{groupName}:{identifierName}";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({metadataLockKey}, 0))",
            ct);

        var type = await db.Set<StorageFileType>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == contentType && !x.IsDeleted, ct);
        if (type is null)
        {
            type = new StorageFileType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = contentType,
                SystemReferenceId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true
            };
            db.Add(type);
        }

        var group = await db.Set<StorageFileIdentifierGroup>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == groupName && !x.IsDeleted, ct);
        if (group is null)
        {
            group = new StorageFileIdentifierGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = groupName,
                SystemReferenceId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true
            };
            db.Add(group);
        }

        var identifier = await db.Set<StorageFileIdentifier>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == identifierName && !x.IsDeleted, ct);
        if (identifier is null)
        {
            identifier = new StorageFileIdentifier
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = identifierName,
                Description = request.IdentifierDescription?.Trim(),
                GroupId = group.Id,
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true
            };
            db.Add(identifier);
        }
        else if (identifier.GroupId != group.Id)
        {
            return Result<StorageUploadMetadataResponse>.Failure(
                "Storage identifier is already assigned to another group", 409);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<StorageUploadMetadataResponse>.Success(new StorageUploadMetadataResponse
        {
            TypeId = type.Id,
            StorageFileIdentifierId = identifier.Id
        });
    }

    public async Task<Result<StorageUploadSessionResponse>> CreateUploadSessionAsync(
        CreateStorageUploadSessionRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageUploadSessionResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        if (request.TotalSizeBytes <= 0)
            return Result<StorageUploadSessionResponse>.Failure("Total file size must be greater than zero", 400);

        if (request.TotalSizeBytes > storageOptions.MaxFileSizeBytes)
            return Result<StorageUploadSessionResponse>.Failure("Total file size exceeds the configured maximum", 400);

        if (string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Length > MaxFileNameLength)
            return Result<StorageUploadSessionResponse>.Failure("File name is required", 400);

        if (request.ContentType?.Length > MaxContentTypeLength)
            return Result<StorageUploadSessionResponse>.Failure("Content type exceeds the configured maximum length", 400);

        if (request.ProviderProfileName?.Length > MaxProviderProfileNameLength)
            return Result<StorageUploadSessionResponse>.Failure("Provider profile name exceeds the configured maximum length", 400);

        if (!Enum.IsDefined(request.Visibility))
            return Result<StorageUploadSessionResponse>.Failure("Storage file visibility is invalid", 400);

        if (!IsValidOptionalSha256(request.ExpectedSha256Hash))
            return Result<StorageUploadSessionResponse>.Failure("Expected SHA-256 hash must contain 64 hexadecimal characters", 400);

        if (request.TypeId == Guid.Empty)
            return Result<StorageUploadSessionResponse>.Failure("Storage file type is required", 400);

        if (request.StorageFileIdentifierId == Guid.Empty)
            return Result<StorageUploadSessionResponse>.Failure("Storage file identifier is required", 400);

        var typeExists = await db.Set<StorageFileType>()
            .AsNoTracking()
            .AnyAsync(type => type.Id == request.TypeId && type.TenantId == tenantId && !type.IsDeleted && type.IsEnabled, ct);
        if (!typeExists)
            return Result<StorageUploadSessionResponse>.NotFound("Storage file type not found");

        var identifierExists = await db.Set<StorageFileIdentifier>()
            .AsNoTracking()
            .AnyAsync(identifier => identifier.Id == request.StorageFileIdentifierId && identifier.TenantId == tenantId && !identifier.IsDeleted && identifier.IsEnabled, ct);
        if (!identifierExists)
            return Result<StorageUploadSessionResponse>.NotFound("Storage file identifier not found");

        var chunkSize = NormalizeChunkSize(request.ChunkSizeBytes);
        var totalPartsLong = request.TotalSizeBytes / chunkSize + (request.TotalSizeBytes % chunkSize == 0 ? 0 : 1);
        if (totalPartsLong > int.MaxValue)
            return Result<StorageUploadSessionResponse>.Failure("Upload session contains too many parts", 400);
        var totalParts = (int)totalPartsLong;
        var now = DateTime.UtcNow;
        var profile = await ResolveProviderProfileAsync(tenantId, request.ProviderProfileName, ct);
        var publicDeliveryResult = ValidatePublicDelivery(profile, request.Visibility);
        if (!publicDeliveryResult.IsSuccess)
            return Result<StorageUploadSessionResponse>.Failure(publicDeliveryResult.Message ?? "Public delivery is unavailable", publicDeliveryResult.StatusCode);

        var bucket = await ResolveTenantBucketAsync(tenantId, profile, request.Visibility, ct);
        var provider = providerFactory.Resolve(profile.Kind);
        var providerLimitResult = ValidateProviderLimits(profile.Kind, request.TotalSizeBytes, chunkSize, totalParts);
        if (!providerLimitResult.IsSuccess)
            return Result<StorageUploadSessionResponse>.Failure(providerLimitResult.Message ?? "Invalid upload provider limits", providerLimitResult.StatusCode);

        var storageFileId = Guid.NewGuid();
        var safeName = NormalizeFileName(request.FileName);
        var objectKey = BuildObjectKey(tenantId, storageFileId, safeName);
        var file = new StorageFile
        {
            Id = storageFileId,
            TenantId = tenantId,
            Name = safeName,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType.Trim(),
            TypeId = request.TypeId,
            Identifier = request.Identifier == Guid.Empty ? storageFileId : request.Identifier,
            StorageFileIdentifierId = request.StorageFileIdentifierId,
            FileSize = request.TotalSizeBytes,
            ContentLengthBytes = request.TotalSizeBytes,
            Hash = NormalizeHash(request.ExpectedSha256Hash),
            Sha256Hash = NormalizeHash(request.ExpectedSha256Hash),
            ContentPath = objectKey,
            BlobContainer = bucket.BucketName,
            ProviderProfileId = profile.Id,
            TenantBucketId = bucket.Id,
            ProviderProfileName = profile.Name,
            BucketName = bucket.BucketName,
            ObjectKey = objectKey,
            Status = StorageFileStatus.Pending,
            Visibility = request.Visibility,
            PublicUrl = null,
            CdnBaseUrl = null,
            UploadStartedAt = now,
            UnclaimedUntil = request.RequireClaim ? now : null,
            CreatedAt = now,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };

        var session = new StorageUploadSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StorageFileId = storageFileId,
            UploadId = Guid.NewGuid().ToString("N"),
            Status = StorageUploadSessionStatus.Created,
            ChunkSizeBytes = chunkSize,
            TotalSizeBytes = request.TotalSizeBytes,
            TotalParts = totalParts,
            ExpectedSha256Hash = NormalizeHash(request.ExpectedSha256Hash),
            ExpiresAt = now.AddMinutes(Math.Max(1, storageOptions.SessionTtlMinutes)),
            CreatedAt = now,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid(),
            StorageFile = file
        };

        db.Set<StorageFile>().Add(file);
        db.Set<StorageUploadSession>().Add(session);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to persist storage upload session metadata");
            return Result<StorageUploadSessionResponse>.Failure("Failed to create storage upload session", 500);
        }

        try
        {
            await provider.EnsureBucketAsync(profile, bucket, ct);
            session.ProviderUploadId = await provider.StartUploadAsync(profile, bucket, file, ct);
            session.ModifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to create storage upload session");
            await TryAbortProviderUploadAsync(provider, profile, bucket, file, session, ct);
            await TryMarkUploadSessionFailedAsync(session, ct);
            return Result<StorageUploadSessionResponse>.Failure("Failed to create storage upload session", 500);
        }

        logger.LogInformation(
            "Created storage upload session {UploadSessionId} for file {StorageFileId}",
            session.Id,
            file.Id);

        return Result<StorageUploadSessionResponse>.Success(
            ToSessionResponse(session, file, uploadedParts: 0),
            201,
            "Upload session created");
    }

    public async Task<Result<StorageUploadPartResponse>> UploadPartAsync(
        UploadStorageFilePartRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageUploadPartResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        if (request.ChunkBytes is null || request.ChunkBytes.Length == 0)
            return Result<StorageUploadPartResponse>.Failure("Chunk payload is required", 400);

        var computedHash = ComputeSha256(request.ChunkBytes);
        var requestedHash = NormalizeHash(request.PartSha256Hash);
        if (string.IsNullOrWhiteSpace(requestedHash))
            return Result<StorageUploadPartResponse>.Failure("Part SHA-256 hash is required", 400);

        if (!IsValidSha256(requestedHash))
            return Result<StorageUploadPartResponse>.Failure("Part SHA-256 hash must contain 64 hexadecimal characters", 400);

        if (!string.Equals(requestedHash, computedHash, StringComparison.OrdinalIgnoreCase))
            return Result<StorageUploadPartResponse>.Failure("Part hash does not match payload", 400);

        var now = DateTime.UtcNow;
        var leaseCutoff = now.AddSeconds(-Math.Max(30, storageOptions.MaintenanceLeaseSeconds));
        var leaseToken = Guid.NewGuid();
        StorageUploadPart part;
        StorageUploadSession session;
        StorageProviderProfile profile;
        StorageTenantBucket bucket;

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var lockKey = $"storage-session:{tenantId:N}:{request.UploadSessionId:N}";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
                ct);

            session = await db.Set<StorageUploadSession>()
                .Include(upload => upload.StorageFile)
                .AsTracking()
                .FirstOrDefaultAsync(
                    upload => upload.Id == request.UploadSessionId && upload.TenantId == tenantId,
                    ct) ?? null!;

            if (session is null)
                return Result<StorageUploadPartResponse>.NotFound("Upload session not found");

            if (session.Status is StorageUploadSessionStatus.Completed or StorageUploadSessionStatus.Aborted or
                StorageUploadSessionStatus.Completing or StorageUploadSessionStatus.Aborting)
            {
                return Result<StorageUploadPartResponse>.Conflict("Upload session is no longer writable");
            }

            if (session.ExpiresAt <= now)
            {
                session.Status = StorageUploadSessionStatus.Expired;
                session.ModifiedAt = now;
                session.ConcurrencyStamp = Guid.NewGuid();
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Result<StorageUploadPartResponse>.Failure("Upload session has expired", 410);
            }

            var validationResult = ValidatePartShape(session, request);
            if (!validationResult.IsSuccess)
                return Result<StorageUploadPartResponse>.Failure(validationResult.Message ?? "Invalid upload part", validationResult.StatusCode);

            part = await db.Set<StorageUploadPart>()
                .AsTracking()
                .FirstOrDefaultAsync(
                    item => item.UploadSessionId == session.Id && item.PartNumber == request.PartNumber,
                    ct) ?? null!;

            if (part is not null)
            {
                if (part.OffsetBytes != request.OffsetBytes ||
                    part.SizeBytes != request.ChunkBytes.Length ||
                    !string.Equals(part.Sha256Hash, computedHash, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<StorageUploadPartResponse>.Conflict("Upload part retry conflicts with the existing part metadata");
                }

                if (part.Status == StorageUploadPartStatus.Uploaded)
                {
                    return Result<StorageUploadPartResponse>.Success(
                        ToPartResponse(part, wasAlreadyUploaded: true),
                        "Upload part already exists");
                }

                if (part.Status == StorageUploadPartStatus.Uploading && part.ModifiedAt > leaseCutoff)
                    return Result<StorageUploadPartResponse>.Conflict("Upload part is already being processed");

                part.Status = StorageUploadPartStatus.Uploading;
                part.ProviderPartId = null;
                part.ModifiedAt = now;
                part.ConcurrencyStamp = leaseToken;
            }
            else
            {
                part = new StorageUploadPart
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UploadSessionId = session.Id,
                    PartNumber = request.PartNumber,
                    OffsetBytes = request.OffsetBytes,
                    SizeBytes = request.ChunkBytes.Length,
                    Sha256Hash = computedHash,
                    Status = StorageUploadPartStatus.Uploading,
                    UploadedAt = now,
                    CreatedAt = now,
                    ModifiedAt = now,
                    IsEnabled = true,
                    ConcurrencyStamp = leaseToken
                };
                db.Set<StorageUploadPart>().Add(part);
            }

            profile = await GetProviderProfileAsync(session.StorageFile.ProviderProfileId, tenantId, ct) ?? null!;
            bucket = await GetTenantBucketAsync(session.StorageFile.TenantBucketId, tenantId, ct) ?? null!;
            if (profile is null || bucket is null)
                return Result<StorageUploadPartResponse>.Failure("Storage provider metadata is missing", 500);

            session.Status = StorageUploadSessionStatus.Uploading;
            session.ModifiedAt = now;
            session.ConcurrencyStamp = Guid.NewGuid();
            session.StorageFile.Status = StorageFileStatus.Uploading;
            session.StorageFile.ModifiedAt = now;
            session.StorageFile.ConcurrencyStamp = Guid.NewGuid();
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result<StorageUploadPartResponse>.Conflict("Upload session state changed while reserving the part");
            }
            await transaction.CommitAsync(ct);
        }

        string providerPartId;
        try
        {
            providerPartId = await providerFactory.Resolve(profile.Kind)
                .UploadPartAsync(profile, bucket, session.StorageFile, session, part, request.ChunkBytes, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await db.Set<StorageUploadPart>()
                .Where(item => item.Id == part.Id && item.ConcurrencyStamp == leaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, StorageUploadPartStatus.Failed)
                    .SetProperty(item => item.ModifiedAt, DateTime.UtcNow)
                    .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
            logger.LogError(ex, "Failed to upload storage part {PartNumber} for session {UploadSessionId}", request.PartNumber, session.Id);
            return Result<StorageUploadPartResponse>.Failure("Storage provider failed to upload part", 500);
        }

        var uploadedAt = DateTime.UtcNow;
        var finalized = await db.Set<StorageUploadPart>()
            .Where(item => item.Id == part.Id &&
                           item.Status == StorageUploadPartStatus.Uploading &&
                           item.ConcurrencyStamp == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ProviderPartId, providerPartId)
                .SetProperty(item => item.Status, StorageUploadPartStatus.Uploaded)
                .SetProperty(item => item.UploadedAt, uploadedAt)
                .SetProperty(item => item.ModifiedAt, uploadedAt)
                .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
        if (finalized == 0)
            return Result<StorageUploadPartResponse>.Conflict("Upload part ownership changed before completion");

        part.ProviderPartId = providerPartId;
        part.Status = StorageUploadPartStatus.Uploaded;
        part.UploadedAt = uploadedAt;
        return Result<StorageUploadPartResponse>.Success(ToPartResponse(part, wasAlreadyUploaded: false), 201, "Upload part stored");
    }

    public async Task<Result<StorageUploadPartListResponse>> ListPartsAsync(
        ListStorageUploadPartsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageUploadPartListResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var session = await db.Set<StorageUploadSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                upload => upload.Id == request.UploadSessionId && upload.TenantId == tenantId,
                ct);

        if (session is null)
            return Result<StorageUploadPartListResponse>.NotFound("Upload session not found");

        var parts = await db.Set<StorageUploadPart>()
            .AsNoTracking()
            .Where(part => part.UploadSessionId == session.Id)
            .OrderBy(part => part.PartNumber)
            .ToListAsync(ct);

        var uploadedPartNumbers = parts
            .Where(part => part.Status == StorageUploadPartStatus.Uploaded)
            .Select(part => part.PartNumber)
            .ToHashSet();
        var missingParts = Enumerable.Range(1, session.TotalParts)
            .Where(partNumber => !uploadedPartNumbers.Contains(partNumber))
            .ToList();

        return Result<StorageUploadPartListResponse>.Success(new StorageUploadPartListResponse
        {
            UploadSessionId = session.Id,
            TotalParts = session.TotalParts,
            Parts = parts.Select(part => ToPartResponse(part, wasAlreadyUploaded: true)).ToList(),
            MissingPartNumbers = missingParts
        });
    }

    public async Task<Result<StorageFileResponse>> CompleteUploadAsync(
        CompleteStorageUploadSessionRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageFileResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var requestedHash = NormalizeHash(request.ExpectedSha256Hash);
        if (!IsValidOptionalSha256(request.ExpectedSha256Hash))
            return Result<StorageFileResponse>.Failure("Expected SHA-256 hash must contain 64 hexadecimal characters", 400);
        var now = DateTime.UtcNow;
        var leaseCutoff = now.AddSeconds(-Math.Max(30, storageOptions.MaintenanceLeaseSeconds));
        var leaseToken = Guid.NewGuid();
        StorageUploadSession session;
        List<StorageUploadPart> parts;
        StorageProviderProfile profile;
        StorageTenantBucket bucket;

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var lockKey = $"storage-session:{tenantId:N}:{request.UploadSessionId:N}";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
                ct);

            session = await db.Set<StorageUploadSession>()
                .Include(upload => upload.StorageFile)
                .Include(upload => upload.Parts)
                .AsTracking()
                .FirstOrDefaultAsync(
                    upload => upload.Id == request.UploadSessionId && upload.TenantId == tenantId,
                    ct) ?? null!;

            if (session is null)
                return Result<StorageFileResponse>.NotFound("Upload session not found");

            if (session.Status == StorageUploadSessionStatus.Completed)
                return Result<StorageFileResponse>.Success(ToFileResponse(session.StorageFile), "Upload session already completed");

            if (session.Status is StorageUploadSessionStatus.Aborted or StorageUploadSessionStatus.Aborting)
                return Result<StorageFileResponse>.Conflict("Upload session was aborted or is being aborted");

            if (session.Status == StorageUploadSessionStatus.Completing && session.ModifiedAt > leaseCutoff)
                return Result<StorageFileResponse>.Conflict("Upload session completion is already in progress");

            if (!string.IsNullOrWhiteSpace(requestedHash) &&
                !string.IsNullOrWhiteSpace(session.ExpectedSha256Hash) &&
                !string.Equals(requestedHash, session.ExpectedSha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                return Result<StorageFileResponse>.Conflict("Completion hash does not match the upload session hash");
            }

            parts = session.Parts
                .Where(part => part.Status == StorageUploadPartStatus.Uploaded)
                .OrderBy(part => part.PartNumber)
                .ToList();
            if (parts.Count != session.TotalParts)
                return Result<StorageFileResponse>.Failure("Upload session is missing one or more completed parts", 400);

            var expectedPartNumbers = Enumerable.Range(1, session.TotalParts).ToArray();
            if (!parts.Select(part => part.PartNumber).SequenceEqual(expectedPartNumbers))
                return Result<StorageFileResponse>.Failure("Upload session has non-contiguous parts", 400);

            var uploadedSize = parts.Sum(part => (long)part.SizeBytes);
            if (uploadedSize != session.TotalSizeBytes)
                return Result<StorageFileResponse>.Failure("Uploaded part sizes do not match the declared file size", 400);

            profile = await GetProviderProfileAsync(session.StorageFile.ProviderProfileId, tenantId, ct) ?? null!;
            bucket = await GetTenantBucketAsync(session.StorageFile.TenantBucketId, tenantId, ct) ?? null!;
            if (profile is null || bucket is null)
                return Result<StorageFileResponse>.Failure("Storage provider metadata is missing", 500);

            session.Status = StorageUploadSessionStatus.Completing;
            session.ModifiedAt = now;
            session.ConcurrencyStamp = leaseToken;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result<StorageFileResponse>.Conflict("Upload session state changed before completion could be claimed");
            }
            await transaction.CommitAsync(ct);
        }

        var provider = providerFactory.Resolve(profile.Kind);
        string? etag;
        try
        {
            etag = await provider.CompleteUploadAsync(profile, bucket, session.StorageFile, session, parts, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to complete storage upload session {UploadSessionId}", session.Id);
            var failedAt = DateTime.UtcNow;
            await db.Set<StorageUploadSession>()
                .Where(item => item.Id == session.Id && item.ConcurrencyStamp == leaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, StorageUploadSessionStatus.Failed)
                    .SetProperty(item => item.ModifiedAt, failedAt)
                    .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
            await db.Set<StorageFile>()
                .Where(item => item.Id == session.StorageFileId && item.Status != StorageFileStatus.Available)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, StorageFileStatus.Failed)
                    .SetProperty(item => item.ModifiedAt, failedAt)
                    .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
            return Result<StorageFileResponse>.Failure("Storage provider failed to complete upload", 500);
        }

        now = DateTime.UtcNow;
        var expectedHash = requestedHash ?? session.ExpectedSha256Hash;
        var singlePartHash = parts.Count == 1 ? parts[0].Sha256Hash : null;
        var singlePartHashMismatch = singlePartHash is not null &&
                                     !string.IsNullOrWhiteSpace(expectedHash) &&
                                     !string.Equals(expectedHash, singlePartHash, StringComparison.OrdinalIgnoreCase);
        if (singlePartHash is not null && !singlePartHashMismatch && session.StorageFile.Visibility == StorageFileVisibility.Public)
            await provider.EnsurePublicAccessAsync(profile, bucket, session.StorageFile, ct);

        session.Status = StorageUploadSessionStatus.Completed;
        session.CompletedAt = now;
        session.ModifiedAt = now;
        session.ConcurrencyStamp = Guid.NewGuid();
        session.StorageFile.Status = singlePartHashMismatch
            ? StorageFileStatus.Failed
            : singlePartHash is null
                ? StorageFileStatus.Verifying
                : StorageFileStatus.Available;
        session.StorageFile.UploadedAt = now;
        session.StorageFile.CompletedAt = singlePartHash is null || singlePartHashMismatch ? null : now;
        session.StorageFile.ModifiedAt = now;
        session.StorageFile.ETag = etag;
        if (singlePartHash is not null)
        {
            session.StorageFile.Sha256Hash = singlePartHash;
            session.StorageFile.Hash = singlePartHash;
        }
        if (session.StorageFile.Status == StorageFileStatus.Available)
        {
            SetPublicUrls(session.StorageFile, profile, bucket);
            if (session.StorageFile.UnclaimedUntil is not null)
            {
                session.StorageFile.UnclaimedUntil = now.AddMinutes(
                    Math.Max(1, storageOptions.UnclaimedFileTtlMinutes));
            }
        }
        session.StorageFile.ConcurrencyStamp = Guid.NewGuid();

        await db.SaveChangesAsync(ct);

        if (singlePartHashMismatch)
            return Result<StorageFileResponse>.Conflict("Completed object hash does not match the expected SHA-256 hash");

        return Result<StorageFileResponse>.Success(
            ToFileResponse(session.StorageFile),
            singlePartHash is null ? 202 : 200,
            singlePartHash is null ? "Upload completed and queued for verification" : "Upload completed");
    }

    public async Task<Result> AbortUploadAsync(
        AbortStorageUploadSessionRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure(tenantResult);
        var tenantId = tenantResult.Data;

        var now = DateTime.UtcNow;
        var leaseCutoff = now.AddSeconds(-Math.Max(30, storageOptions.MaintenanceLeaseSeconds));
        var leaseToken = Guid.NewGuid();
        StorageUploadSession session;
        StorageProviderProfile profile;
        StorageTenantBucket bucket;

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var lockKey = $"storage-session:{tenantId:N}:{request.UploadSessionId:N}";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
                ct);
            session = await db.Set<StorageUploadSession>()
                .Include(upload => upload.StorageFile)
                .Include(upload => upload.Parts)
                .AsTracking()
                .FirstOrDefaultAsync(
                    upload => upload.Id == request.UploadSessionId && upload.TenantId == tenantId,
                    ct) ?? null!;

            if (session is null)
                return Result.NotFound("Upload session not found");
            if (session.Status == StorageUploadSessionStatus.Completed)
                return Result.Conflict("Completed upload sessions cannot be aborted");
            if (session.Status == StorageUploadSessionStatus.Aborted)
                return Result.Success("Upload session already aborted");
            if (session.Status == StorageUploadSessionStatus.Completing)
                return Result.Conflict("Upload session completion is in progress");
            if (session.Status == StorageUploadSessionStatus.Aborting && session.ModifiedAt > leaseCutoff)
                return Result.Conflict("Upload session abort is already in progress");
            if (session.Parts.Any(part =>
                    part.Status == StorageUploadPartStatus.Uploading &&
                    part.ModifiedAt > leaseCutoff))
            {
                return Result.Conflict("Upload session has a part upload in progress");
            }

            profile = await GetProviderProfileAsync(session.StorageFile.ProviderProfileId, tenantId, ct) ?? null!;
            bucket = await GetTenantBucketAsync(session.StorageFile.TenantBucketId, tenantId, ct) ?? null!;
            if (profile is null || bucket is null)
                return Result.Failure("Storage provider metadata is missing", 500);

            session.Status = StorageUploadSessionStatus.Aborting;
            session.ModifiedAt = now;
            session.ConcurrencyStamp = leaseToken;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Conflict("Upload session state changed before abort could be claimed");
            }
            await transaction.CommitAsync(ct);
        }

        try
        {
            await providerFactory.Resolve(profile.Kind)
                .AbortUploadAsync(profile, bucket, session.StorageFile, session, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to abort provider upload for storage session {UploadSessionId}", session.Id);
            await db.Set<StorageUploadSession>()
                .Where(item => item.Id == session.Id && item.ConcurrencyStamp == leaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, StorageUploadSessionStatus.Failed)
                    .SetProperty(item => item.ModifiedAt, DateTime.UtcNow)
                    .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
            return Result.Failure("Storage provider failed to abort upload", 500);
        }

        now = DateTime.UtcNow;
        await db.Set<StorageUploadSession>()
            .Where(item => item.Id == session.Id && item.ConcurrencyStamp == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, StorageUploadSessionStatus.Aborted)
                .SetProperty(item => item.AbortedAt, now)
                .SetProperty(item => item.ModifiedAt, now)
                .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
        await db.Set<StorageFile>()
            .Where(item => item.Id == session.StorageFileId && item.Status != StorageFileStatus.Available)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, StorageFileStatus.Failed)
                .SetProperty(item => item.ModifiedAt, now)
                .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);

        return Result.Success("Upload session aborted");
    }

    public async Task<Result<StorageFileResponse>> ClaimFileAsync(
        ClaimStorageFileRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageFileResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var file = await db.Set<StorageFile>()
            .AsTracking()
            .FirstOrDefaultAsync(
                item => item.Id == request.StorageFileId && item.TenantId == tenantId,
                ct);
        if (file is null)
            return Result<StorageFileResponse>.NotFound("Storage file not found");

        if (file.Status != StorageFileStatus.Available || file.ObjectDeletedAt is not null)
            return Result<StorageFileResponse>.Conflict("Storage file is not available to claim");

        if (file.UnclaimedUntil is { } unclaimedUntil && unclaimedUntil <= DateTime.UtcNow)
            return Result<StorageFileResponse>.Conflict("Storage file claim period has expired");

        if (file.UnclaimedUntil is not null)
        {
            file.UnclaimedUntil = null;
            file.ModifiedAt = DateTime.UtcNow;
            file.ConcurrencyStamp = Guid.NewGuid();
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result<StorageFileResponse>.Conflict("Storage file claim conflicted with maintenance");
            }
        }

        return Result<StorageFileResponse>.Success(ToFileResponse(file), "Storage file claimed");
    }

    public async Task<Result<StorageFileResponse>> GetFileAsync(
        GetStorageFileRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageFileResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var file = await db.Set<StorageFile>()
            .AsNoTracking()
            .Include(storageFile => storageFile.StorageFileIdentifier)
            .ThenInclude(identifier => identifier!.Group)
            .FirstOrDefaultAsync(
                storageFile => storageFile.Id == request.StorageFileId && storageFile.TenantId == tenantId,
                ct);

        return file is null
            ? Result<StorageFileResponse>.NotFound("Storage file not found")
            : Result<StorageFileResponse>.Success(ToFileResponse(file));
    }

    public async Task<Result<StorageFileListResponse>> GetFilesAsync(
        GetStorageFilesRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageFileListResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);
        var query = db.Set<StorageFile>()
            .AsNoTracking()
            .Where(file => file.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(file =>
                (file.Name != null && file.Name.ToLower().Contains(searchTerm)) ||
                (file.ContentType != null && file.ContentType.ToLower().Contains(searchTerm)));
        }

        if (request.TypeId.HasValue)
            query = query.Where(file => file.TypeId == request.TypeId.Value);

        if (request.Identifier.HasValue)
            query = query.Where(file => file.Identifier == request.Identifier.Value);

        if (request.Status.HasValue)
            query = query.Where(file => file.Status == request.Status.Value);

        if (request.Visibility.HasValue)
            query = query.Where(file => file.Visibility == request.Visibility.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(file => file.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(file => ToFileResponse(file))
            .ToListAsync(ct);

        return Result<StorageFileListResponse>.Success(new StorageFileListResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    public async Task<Result<StorageDownloadUrlResponse>> GetDownloadUrlAsync(
        GetStorageDownloadUrlRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageDownloadUrlResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var file = await db.Set<StorageFile>()
            .AsTracking()
            .FirstOrDefaultAsync(
                storageFile => storageFile.Id == request.StorageFileId && storageFile.TenantId == tenantId,
                ct);

        if (file is null)
            return Result<StorageDownloadUrlResponse>.NotFound("Storage file not found");

        var availability = ValidateAvailableFile(file);
        if (!availability.IsSuccess)
            return Result<StorageDownloadUrlResponse>.Failure(availability.Message ?? "Storage file is not available", availability.StatusCode);

        if (file.Visibility == StorageFileVisibility.Public)
        {
            var publicUrl = await GetPublicUrlInternalAsync(file, tenantId, ct);
            var url = string.IsNullOrWhiteSpace(publicUrl.CdnUrl) ? publicUrl.PublicUrl : publicUrl.CdnUrl;
            if (!string.IsNullOrWhiteSpace(url))
            {
                return Result<StorageDownloadUrlResponse>.Success(new StorageDownloadUrlResponse
                {
                    StorageFileId = file.Id,
                    Url = url,
                    ExpiresAt = DateTime.MaxValue,
                    IsPublic = true
                });
            }
        }

        var profile = await GetProviderProfileAsync(file.ProviderProfileId, tenantId, ct);
        var bucket = await GetTenantBucketAsync(file.TenantBucketId, tenantId, ct);
        if (profile is null || bucket is null)
            return Result<StorageDownloadUrlResponse>.Failure("Storage provider metadata is missing", 500);

        var expirationMinutes = request.ExpirationMinutes ?? storageOptions.SignedUrlExpirationMinutes;
        var maxExpirationMinutes = Math.Max(1, storageOptions.MaxSignedUrlExpirationMinutes);
        if (expirationMinutes < 1 || expirationMinutes > maxExpirationMinutes)
        {
            return Result<StorageDownloadUrlResponse>.Failure(
                $"Signed URL expiration must be between 1 and {maxExpirationMinutes} minutes", 400);
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);
        var provider = providerFactory.Resolve(profile.Kind);
        StorageDownloadUrlResponse response;
        try
        {
            response = await provider.CreateDownloadUrlAsync(profile, bucket, file, expiresAt, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create signed download URL for storage file {StorageFileId}", file.Id);
            return Result<StorageDownloadUrlResponse>.Failure("Storage provider cannot create a signed download URL", 500);
        }

        file.DownloadUrlExpiresAt = expiresAt;
        file.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result<StorageDownloadUrlResponse>.Success(response);
    }

    public async Task<Result<StoragePublicUrlResponse>> GetPublicUrlAsync(
        GetStoragePublicUrlRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StoragePublicUrlResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var file = await db.Set<StorageFile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                storageFile => storageFile.Id == request.StorageFileId && storageFile.TenantId == tenantId,
                ct);

        if (file is null)
            return Result<StoragePublicUrlResponse>.NotFound("Storage file not found");

        var availability = ValidateAvailableFile(file);
        if (!availability.IsSuccess)
            return Result<StoragePublicUrlResponse>.Failure(availability.Message ?? "Storage file is not available", availability.StatusCode);

        var response = await GetPublicUrlInternalAsync(file, tenantId, ct);
        if (response.IsPublic &&
            string.IsNullOrWhiteSpace(response.PublicUrl) &&
            string.IsNullOrWhiteSpace(response.CdnUrl))
        {
            return Result<StoragePublicUrlResponse>.Failure("Public storage base URL is not configured for this provider profile", 409);
        }

        return Result<StoragePublicUrlResponse>.Success(response);
    }

    public async Task<Result> DeleteFileAsync(
        DeleteStorageFileRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure(tenantResult);
        var tenantId = tenantResult.Data;

        var file = await db.Set<StorageFile>()
            .AsTracking()
            .FirstOrDefaultAsync(
                storageFile => storageFile.Id == request.StorageFileId && storageFile.TenantId == tenantId,
                ct);

        if (file is null)
            return Result.NotFound("Storage file not found");

        var now = DateTime.UtcNow;
        file.Status = StorageFileStatus.Deleted;
        file.RetentionUntil = request.RetentionUntil ?? now.AddDays(Math.Max(1, storageOptions.RetentionDays));
        file.ModifiedAt = now;
        db.Set<StorageFile>().Remove(file);
        await db.SaveChangesAsync(ct);

        return Result.Success("Storage file marked for retention cleanup");
    }

    public async Task<Result<StorageFileResponse>> RestoreFileAsync(
        RestoreStorageFileRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageFileResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var file = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(
                storageFile => storageFile.Id == request.StorageFileId && storageFile.TenantId == tenantId,
                ct);

        if (file is null)
            return Result<StorageFileResponse>.NotFound("Storage file not found");

        if (file.Status == StorageFileStatus.Deleting)
            return Result<StorageFileResponse>.Conflict("Storage file deletion is already in progress");

        if (!file.IsDeleted && file.Status != StorageFileStatus.Deleted)
            return Result<StorageFileResponse>.Success(ToFileResponse(file), "Storage file is already active");

        if (file.ObjectDeletedAt.HasValue)
            return Result<StorageFileResponse>.Conflict("Storage file object was already physically deleted and cannot be restored");

        var now = DateTime.UtcNow;
        file.IsDeleted = false;
        file.DeletedAt = null;
        file.RetentionUntil = null;
        file.Status = file.CompletedAt.HasValue ? StorageFileStatus.Available : StorageFileStatus.Pending;
        file.ModifiedAt = now;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<StorageFileResponse>.Conflict("Storage file state changed before it could be restored");
        }

        return Result<StorageFileResponse>.Success(ToFileResponse(file), "Storage file restored");
    }

    public async Task<Result<StorageRetentionCleanupResponse>> CleanupRetentionAsync(
        CleanupStorageRetentionRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageRetentionCleanupResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var now = DateTime.UtcNow;
        var leaseCutoff = now.AddSeconds(-Math.Max(30, storageOptions.MaintenanceLeaseSeconds));
        var maxFiles = request.MaxFiles <= 0 ? 100 : Math.Min(request.MaxFiles, 1000);
        var files = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(file => file.TenantId == tenantId)
            .Where(file => file.Status == StorageFileStatus.Deleted ||
                           file.Status == StorageFileStatus.Deleting &&
                           (file.ModifiedAt == null || file.ModifiedAt <= leaseCutoff) ||
                           file.IsDeleted && file.Status != StorageFileStatus.Deleting)
            .Where(file => file.RetentionUntil != null && file.RetentionUntil <= now)
            .Where(file => file.ObjectDeletedAt == null)
            .OrderBy(file => file.RetentionUntil)
            .Take(maxFiles)
            .ToListAsync(ct);

        var response = new StorageRetentionCleanupResponse
        {
            MatchedCount = files.Count,
            DryRun = request.DryRun,
            StorageFileIds = files.Select(file => file.Id).ToList()
        };

        if (request.DryRun || files.Count == 0)
            return Result<StorageRetentionCleanupResponse>.Success(response);

        var profiles = await db.Set<StorageProviderProfile>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.IsEnabled && !profile.IsDeleted)
            .ToDictionaryAsync(profile => profile.Id, ct);
        var buckets = await db.Set<StorageTenantBucket>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(bucket => bucket.TenantId == tenantId && bucket.IsEnabled && !bucket.IsDeleted)
            .ToDictionaryAsync(bucket => bucket.Id, ct);

        foreach (var file in files)
        {
            var leaseToken = Guid.NewGuid();
            var claimed = await db.Set<StorageFile>()
                .IgnoreQueryFilters()
                .Where(item => item.Id == file.Id &&
                               item.TenantId == tenantId &&
                               item.ObjectDeletedAt == null &&
                               item.RetentionUntil != null &&
                               item.RetentionUntil <= now)
                .Where(item => item.Status == StorageFileStatus.Deleted ||
                               item.Status == StorageFileStatus.Deleting &&
                               (item.ModifiedAt == null || item.ModifiedAt <= leaseCutoff) ||
                               item.IsDeleted && item.Status != StorageFileStatus.Deleting)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, StorageFileStatus.Deleting)
                    .SetProperty(item => item.ModifiedAt, now)
                    .SetProperty(item => item.ConcurrencyStamp, leaseToken), ct);
            if (claimed == 0)
                continue;

            if (file.ProviderProfileId is null ||
                file.TenantBucketId is null ||
                !profiles.TryGetValue(file.ProviderProfileId.Value, out var profile) ||
                !buckets.TryGetValue(file.TenantBucketId.Value, out var bucket))
            {
                logger.LogWarning("Storage file {StorageFileId} is missing provider metadata and remains retryable.", file.Id);
                await ReleaseDeletionLeaseAsync(file.Id, leaseToken, now, ct);
                continue;
            }

            try
            {
                var provider = providerFactory.Resolve(profile.Kind);
                await provider.DeleteObjectAsync(profile, bucket, file, ct);
                var deleted = await db.Set<StorageFile>()
                    .IgnoreQueryFilters()
                    .Where(item => item.Id == file.Id &&
                                   item.Status == StorageFileStatus.Deleting &&
                                   item.ConcurrencyStamp == leaseToken)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, StorageFileStatus.Deleted)
                        .SetProperty(item => item.ObjectDeletedAt, now)
                        .SetProperty(item => item.ModifiedAt, now)
                        .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
                response.DeletedObjectCount += deleted;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to physically delete storage file {StorageFileId}", file.Id);
                await ReleaseDeletionLeaseAsync(file.Id, leaseToken, now, ct);
            }
        }

        return Result<StorageRetentionCleanupResponse>.Success(response);
    }

    private Task ReleaseDeletionLeaseAsync(Guid fileId, Guid leaseToken, DateTime now, CancellationToken ct) =>
        db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .Where(item => item.Id == fileId && item.ConcurrencyStamp == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, StorageFileStatus.Deleted)
                .SetProperty(item => item.ModifiedAt, now)
                .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);

    public async Task<Result<StorageFileValidationResponse>> ValidateFileReferenceAsync(
        ValidateStorageFileReferenceRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantIdAsync(request.Metadata, ct);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageFileValidationResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var query = db.Set<StorageFile>().AsNoTracking();
        if (request.AllowDeleted)
            query = query.IgnoreQueryFilters();

        var file = await query
            .FirstOrDefaultAsync(
                storageFile => storageFile.Id == request.StorageFileId && storageFile.TenantId == tenantId,
                ct);

        if (file is null)
            return Result<StorageFileValidationResponse>.NotFound("Storage file not found");

        var isDeleted = file.IsDeleted || file.Status == StorageFileStatus.Deleted || file.ObjectDeletedAt.HasValue;
        var isAvailable = file.Status == StorageFileStatus.Available;
        var isValid = (!isDeleted || request.AllowDeleted) && (!request.RequireAvailable || isAvailable);

        return Result<StorageFileValidationResponse>.Success(new StorageFileValidationResponse
        {
            StorageFileId = file.Id,
            TenantId = file.TenantId,
            IsValid = isValid,
            Status = file.Status,
            Visibility = file.Visibility,
            TypeId = file.TypeId,
            Name = file.Name,
            ContentType = file.ContentType,
            ContentLengthBytes = file.ContentLengthBytes ?? (file.FileSize.HasValue ? (long)file.FileSize.Value : null),
            Sha256Hash = file.Sha256Hash,
            Message = isValid ? null : "Storage file is not available for reference"
        });
    }

    private async Task<StoragePublicUrlResponse> GetPublicUrlInternalAsync(
        StorageFile file,
        Guid tenantId,
        CancellationToken ct)
    {
        if (file.Visibility != StorageFileVisibility.Public)
        {
            return new StoragePublicUrlResponse
            {
                StorageFileId = file.Id,
                IsPublic = false
            };
        }

        return new StoragePublicUrlResponse
        {
            StorageFileId = file.Id,
            IsPublic = true,
            PublicUrl = file.PublicUrl,
            CdnUrl = file.CdnBaseUrl
        };
    }

    private async Task<StorageProviderProfile> ResolveProviderProfileAsync(
        Guid tenantId,
        string? requestedProfileName,
        CancellationToken ct)
    {
        var profileName = string.IsNullOrWhiteSpace(requestedProfileName)
            ? storageOptions.ProviderProfileName
            : requestedProfileName.Trim();

        var profile = await db.Set<StorageProviderProfile>()
            .AsTracking()
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.Name == profileName && !item.IsDeleted,
                ct);

        if (profile is not null)
            return profile;

        var kind = storageOptions.ResolveDefaultProviderKind();
        profile = new StorageProviderProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = profileName,
            Kind = kind,
            BucketPrefix = storageOptions.BucketPrefix,
            AutoCreateBuckets = storageOptions.AutoCreateBuckets,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };

        if (kind == StorageProviderKind.AzureBlob)
        {
            profile.ConnectionStringSecretName = storageOptions.AzureBlob.ConnectionStringSecretName;
            profile.PublicBaseUrl = storageOptions.AzureBlob.PublicBaseUrl;
            profile.CdnBaseUrl = storageOptions.AzureBlob.CdnBaseUrl;
        }
        else
        {
            profile.Endpoint = storageOptions.S3.Endpoint;
            profile.Region = storageOptions.S3.Region;
            profile.AccessKeyIdSecretName = storageOptions.S3.AccessKeyIdSecretName;
            profile.SecretAccessKeySecretName = storageOptions.S3.SecretAccessKeySecretName;
            profile.UsePathStyle = storageOptions.S3.UsePathStyle;
            profile.PublicBaseUrl = storageOptions.S3.PublicBaseUrl;
            profile.CdnBaseUrl = storageOptions.S3.CdnBaseUrl;
        }

        db.Set<StorageProviderProfile>().Add(profile);
        return profile;
    }

    private async Task<StorageTenantBucket> ResolveTenantBucketAsync(
        Guid tenantId,
        StorageProviderProfile profile,
        StorageFileVisibility visibility,
        CancellationToken ct)
    {
        var purpose = visibility == StorageFileVisibility.Public
            ? StorageBucketPurpose.Public
            : StorageBucketPurpose.Private;
        var bucket = await db.Set<StorageTenantBucket>()
            .AsTracking()
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.ProviderProfileId == profile.Id && item.Purpose == purpose && !item.IsDeleted,
                ct);

        if (bucket is not null)
            return bucket;

        bucket = new StorageTenantBucket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProviderProfileId = profile.Id,
            BucketName = BuildTenantBucketName(profile.BucketPrefix, tenantId, purpose),
            Purpose = purpose,
            PublicBaseUrl = profile.PublicBaseUrl,
            CdnBaseUrl = profile.CdnBaseUrl,
            CreatedAt = DateTime.UtcNow,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid(),
            ProviderProfile = profile
        };

        db.Set<StorageTenantBucket>().Add(bucket);
        return bucket;
    }

    private async Task<StorageProviderProfile?> GetProviderProfileAsync(Guid? providerProfileId, Guid tenantId, CancellationToken ct) =>
        providerProfileId is null
            ? null
            : await db.Set<StorageProviderProfile>()
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.Id == providerProfileId && profile.TenantId == tenantId, ct);

    private async Task<StorageTenantBucket?> GetTenantBucketAsync(Guid? tenantBucketId, Guid tenantId, CancellationToken ct) =>
        tenantBucketId is null
            ? null
            : await db.Set<StorageTenantBucket>()
                .AsNoTracking()
                .FirstOrDefaultAsync(bucket => bucket.Id == tenantBucketId && bucket.TenantId == tenantId, ct);

    private Task<Result<Guid>> ResolveTenantIdAsync(
        RequestMetadata? metadata,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var trustedTenantId = trustedInvocationContextAccessor.Current?.EffectiveTenantId;

        if (trustedTenantId is null || trustedTenantId.Value == Guid.Empty)
            return Task.FromResult(Result<Guid>.Failure("Tenant context is required", 400));

        if (metadata?.RequestedTenantId is { } metadataTenantId &&
            metadataTenantId != Guid.Empty &&
            metadataTenantId != trustedTenantId.Value)
        {
            return Task.FromResult(Result<Guid>.Forbidden("Request tenant does not match trusted tenant context"));
        }

        return Task.FromResult(Result<Guid>.Success(trustedTenantId.Value));
    }

    private static Result<T> TenantFailure<T>(Result<Guid> tenantResult) =>
        tenantResult.StatusCode switch
        {
            403 => Result<T>.Forbidden(tenantResult.Message),
            401 => Result<T>.Unauthorized(tenantResult.Message),
            _ => Result<T>.Failure(tenantResult.Message ?? "Tenant context is required", tenantResult.StatusCode)
        };

    private static Result TenantFailure(Result<Guid> tenantResult) =>
        tenantResult.StatusCode switch
        {
            403 => Result.Forbidden(tenantResult.Message),
            401 => Result.Unauthorized(tenantResult.Message),
            _ => Result.Failure(tenantResult.Message ?? "Tenant context is required", tenantResult.StatusCode)
        };

    private static Guid? TryGetClaimGuid(ClaimsPrincipal? user, params string[] claimTypes)
    {
        if (user is null)
            return null;

        foreach (var claimType in claimTypes)
        {
            var value = user.Claims.FirstOrDefault(claim =>
                string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))?.Value;
            if (Guid.TryParse(value, out var id))
                return id;
        }

        return null;
    }

    private static Guid? TryGetItemGuid(HttpContext? context, string key)
    {
        if (context?.Items.TryGetValue(key, out var value) != true)
            return null;

        return value switch
        {
            Guid id => id,
            string text when Guid.TryParse(text, out var id) => id,
            _ => null
        };
    }

    private Result ValidateProviderLimits(
        StorageProviderKind providerKind,
        long totalSizeBytes,
        int chunkSizeBytes,
        int totalParts)
    {
        if (!storageOptions.EnforceProviderLimits)
            return Result.Success();

        if (totalParts <= 0)
            return Result.Failure("Upload session must contain at least one part", 400);

        if (chunkSizeBytes > MaxChunkSizeBytes)
            return Result.Failure("Upload part size exceeds the configured maximum", 400);

        return providerKind switch
        {
            StorageProviderKind.S3Compatible when totalParts > S3MaximumPartCount =>
                Result.Failure("S3-compatible uploads cannot exceed 10,000 parts", 400),
            StorageProviderKind.S3Compatible when totalParts > 1 && chunkSizeBytes < S3MinimumNonFinalPartSizeBytes =>
                Result.Failure("S3-compatible multipart uploads require non-final parts to be at least 5 MiB", 400),
            StorageProviderKind.AzureBlob when totalParts > AzureMaximumBlockCount =>
                Result.Failure("Azure Blob block uploads cannot exceed 50,000 blocks", 400),
            _ => Result.Success()
        };
    }

    private async Task TryAbortProviderUploadAsync(
        IStorageObjectProvider provider,
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        CancellationToken ct)
    {
        try
        {
            await provider.AbortUploadAsync(profile, bucket, file, session, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to abort provider upload for storage session {UploadSessionId}", session.Id);
        }
    }

    private async Task TryMarkUploadSessionFailedAsync(StorageUploadSession session, CancellationToken ct)
    {
        try
        {
            var failedAt = DateTime.UtcNow;
            await db.Set<StorageUploadSession>()
                .Where(item => item.Id == session.Id && item.Status != StorageUploadSessionStatus.Completed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, StorageUploadSessionStatus.Failed)
                    .SetProperty(item => item.ExpiresAt, failedAt)
                    .SetProperty(item => item.ModifiedAt, failedAt)
                    .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
            await db.Set<StorageFile>()
                .Where(item => item.Id == session.StorageFileId && item.Status != StorageFileStatus.Available)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, StorageFileStatus.Failed)
                    .SetProperty(item => item.ModifiedAt, failedAt)
                    .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to mark storage upload session {UploadSessionId} for retryable cleanup", session.Id);
        }
    }

    private Result ValidatePartShape(StorageUploadSession session, UploadStorageFilePartRequest request)
    {
        if (request.PartNumber < 1 || request.PartNumber > session.TotalParts)
            return Result.Failure("Part number is outside the upload session range", 400);

        if (request.ChunkBytes.Length > MaxChunkSizeBytes)
            return Result.Failure("Part payload size exceeds the configured maximum", 400);

        var expectedOffset = (long)(request.PartNumber - 1) * session.ChunkSizeBytes;
        if (request.OffsetBytes != expectedOffset)
            return Result.Failure("Part offset does not match the upload session chunk layout", 400);

        var expectedSize = (int)Math.Min(session.ChunkSizeBytes, session.TotalSizeBytes - expectedOffset);
        if (request.ChunkBytes.Length != expectedSize)
            return Result.Failure("Part payload size does not match the expected chunk size", 400);

        return Result.Success();
    }

    private static Result ValidateAvailableFile(StorageFile file)
    {
        if (file.IsDeleted || file.Status == StorageFileStatus.Deleted || file.ObjectDeletedAt.HasValue)
            return Result.NotFound("Storage file not found");

        if (file.Status != StorageFileStatus.Available)
            return Result.Failure("Storage file is not available", 409);

        return Result.Success();
    }

    private int NormalizeChunkSize(int? requestedChunkSize)
    {
        var chunkSize = requestedChunkSize.GetValueOrDefault(storageOptions.DefaultChunkSizeBytes);
        if (chunkSize <= 0)
            chunkSize = storageOptions.DefaultChunkSizeBytes;

        return Math.Clamp(chunkSize, 1, 100 * 1024 * 1024);
    }

    public static string BuildTenantBucketName(string bucketPrefix, Guid tenantId)
        => BuildTenantBucketName(bucketPrefix, tenantId, StorageBucketPurpose.Private);

    public static string BuildTenantBucketName(
        string bucketPrefix,
        Guid tenantId,
        StorageBucketPurpose purpose)
    {
        var normalizedPrefix = BucketNameSanitizer().Replace(bucketPrefix.ToLowerInvariant(), "-").Trim('-');
        if (string.IsNullOrWhiteSpace(normalizedPrefix))
            normalizedPrefix = "xframework";

        var purposeSuffix = purpose == StorageBucketPurpose.Public ? "-public" : string.Empty;
        var name = $"{normalizedPrefix}-{tenantId:N}{purposeSuffix}".ToLowerInvariant();
        if (name.Length > 63)
        {
            var tenantSuffix = $"{tenantId:N}{purposeSuffix}";
            var maxPrefixLength = 63 - tenantSuffix.Length - 1;
            normalizedPrefix = normalizedPrefix.Length <= maxPrefixLength
                ? normalizedPrefix
                : normalizedPrefix[..maxPrefixLength].Trim('-');
            name = $"{normalizedPrefix}-{tenantSuffix}".Trim('-');
        }

        return name;
    }

    private Result ValidatePublicDelivery(
        StorageProviderProfile profile,
        StorageFileVisibility visibility)
    {
        if (visibility != StorageFileVisibility.Public)
            return Result.Success();

        var mode = profile.Kind == StorageProviderKind.AzureBlob
            ? storageOptions.AzureBlob.PublicDeliveryMode
            : storageOptions.S3.PublicDeliveryMode;
        if (mode == StoragePublicDeliveryMode.Disabled)
            return Result.Failure("Public delivery is disabled for the selected storage provider", 409);

        if (mode == StoragePublicDeliveryMode.PrivateOriginCdn && string.IsNullOrWhiteSpace(profile.CdnBaseUrl))
            return Result.Failure("Public CDN delivery requires a configured CDN base URL", 409);

        if (mode == StoragePublicDeliveryMode.ProviderManaged && string.IsNullOrWhiteSpace(profile.PublicBaseUrl))
            return Result.Failure("Provider-managed public delivery requires a configured public base URL", 409);

        return Result.Success();
    }

    private static string BuildObjectKey(Guid tenantId, Guid fileId, string fileName) =>
        $"{tenantId:N}/{fileId:N}/{fileName}";

    internal static string BuildPublicUrl(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        string objectKey,
        bool preferCdn)
    {
        var baseUrl = preferCdn
            ? bucket.CdnBaseUrl ?? profile.CdnBaseUrl
            : bucket.PublicBaseUrl ?? profile.PublicBaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
            return string.Empty;

        return $"{baseUrl.TrimEnd('/')}/{bucket.BucketName}/{EscapeObjectKey(objectKey)}";
    }

    private void SetPublicUrls(
        StorageFile file,
        StorageProviderProfile profile,
        StorageTenantBucket bucket)
    {
        if (file.Visibility != StorageFileVisibility.Public || string.IsNullOrWhiteSpace(file.ObjectKey))
            return;

        var publicUrl = BuildPublicUrl(profile, bucket, file.ObjectKey, preferCdn: false);
        var cdnUrl = BuildPublicUrl(profile, bucket, file.ObjectKey, preferCdn: true);
        var deliveryMode = profile.Kind == StorageProviderKind.AzureBlob
            ? storageOptions.AzureBlob.PublicDeliveryMode
            : storageOptions.S3.PublicDeliveryMode;
        file.PublicUrl = deliveryMode == StoragePublicDeliveryMode.ProviderManaged &&
                         !string.IsNullOrWhiteSpace(publicUrl)
            ? publicUrl
            : null;
        file.CdnBaseUrl = string.IsNullOrWhiteSpace(cdnUrl) ? null : cdnUrl;
    }

    private static string EscapeObjectKey(string objectKey) =>
        string.Join("/", objectKey.Split('/').Select(WebUtility.UrlEncode));

    private static string NormalizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        name = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(name) ? "file" : name;
    }

    private static string? NormalizeHash(string? hash) =>
        string.IsNullOrWhiteSpace(hash)
            ? null
            : hash.Trim().ToLowerInvariant();

    private static bool IsValidOptionalSha256(string? hash) =>
        string.IsNullOrWhiteSpace(hash) || IsValidSha256(hash.Trim());

    private static bool IsValidSha256(string hash) =>
        hash.Length == 64 && hash.All(Uri.IsHexDigit);

    private static string ComputeSha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static StorageFileResponse ToFileResponse(StorageFile file) => new()
    {
        Id = file.Id,
        TenantId = file.TenantId,
        Name = file.Name ?? string.Empty,
        ContentType = file.ContentType,
        TypeId = file.TypeId,
        Identifier = file.Identifier,
        StorageFileIdentifierId = file.StorageFileIdentifierId,
        StorageFileIdentifierName = file.StorageFileIdentifier?.Name,
        StorageFileIdentifierGroupName = file.StorageFileIdentifier?.Group?.Name,
        Status = file.Status,
        Visibility = file.Visibility,
        ProviderProfileName = file.ProviderProfileName,
        BucketName = file.BucketName,
        ObjectKey = file.ObjectKey,
        BlobContainer = file.BlobContainer,
        ContentLengthBytes = file.ContentLengthBytes,
        Sha256Hash = file.Sha256Hash,
        ETag = file.ETag,
        PublicUrl = file.PublicUrl,
        CdnBaseUrl = file.CdnBaseUrl,
        UploadStartedAt = file.UploadStartedAt,
        CompletedAt = file.CompletedAt,
        RetentionUntil = file.RetentionUntil,
        ObjectDeletedAt = file.ObjectDeletedAt,
        UnclaimedUntil = file.UnclaimedUntil,
        CreatedAt = file.CreatedAt
    };

    private static StorageUploadSessionResponse ToSessionResponse(
        StorageUploadSession session,
        StorageFile file,
        int uploadedParts) => new()
    {
        Id = session.Id,
        TenantId = session.TenantId,
        StorageFileId = session.StorageFileId,
        UploadId = session.UploadId,
        Status = session.Status,
        ChunkSizeBytes = session.ChunkSizeBytes,
        TotalSizeBytes = session.TotalSizeBytes,
        TotalParts = session.TotalParts,
        UploadedParts = uploadedParts,
        ExpectedSha256Hash = session.ExpectedSha256Hash,
        ExpiresAt = session.ExpiresAt,
        File = ToFileResponse(file)
    };

    private static StorageUploadPartResponse ToPartResponse(
        StorageUploadPart part,
        bool wasAlreadyUploaded) => new()
    {
        Id = part.Id,
        UploadSessionId = part.UploadSessionId,
        PartNumber = part.PartNumber,
        OffsetBytes = part.OffsetBytes,
        SizeBytes = part.SizeBytes,
        Sha256Hash = part.Sha256Hash,
        ProviderPartId = part.ProviderPartId,
        WasAlreadyUploaded = wasAlreadyUploaded,
        UploadedAt = part.UploadedAt
    };

    [GeneratedRegex("[^a-z0-9-]+", RegexOptions.Compiled)]
    private static partial Regex BucketNameSanitizer();
}
