using System.Net;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Storage.Api.Services.Providers;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;

namespace Storage.Api.Services;

public sealed partial class StorageService(
    AppDbContext db,
    IStorageProviderFactory providerFactory,
    IOptions<StorageOptions> options,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ILogger<StorageService> logger)
{
    private readonly StorageOptions storageOptions = options.Value;
    private const int MaxChunkSizeBytes = 100 * 1024 * 1024;
    private const int S3MinimumNonFinalPartSizeBytes = 5 * 1024 * 1024;
    private const int S3MaximumPartCount = 10_000;
    private const int AzureMaximumBlockCount = 50_000;

    public async Task<Result<StorageUploadSessionResponse>> CreateUploadSessionAsync(
        CreateStorageUploadSessionRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveTenantId(request.Metadata);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageUploadSessionResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        if (request.TotalSizeBytes <= 0)
            return Result<StorageUploadSessionResponse>.Failure("Total file size must be greater than zero", 400);

        if (string.IsNullOrWhiteSpace(request.FileName))
            return Result<StorageUploadSessionResponse>.Failure("File name is required", 400);

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
        var totalParts = checked((int)Math.Ceiling(request.TotalSizeBytes / (double)chunkSize));
        var now = DateTime.UtcNow;
        var profile = await ResolveProviderProfileAsync(tenantId, request.ProviderProfileName, ct);
        var bucket = await ResolveTenantBucketAsync(tenantId, profile, ct);
        var provider = providerFactory.Resolve(profile.Kind);
        var providerLimitResult = ValidateProviderLimits(profile.Kind, request.TotalSizeBytes, chunkSize, totalParts);
        if (!providerLimitResult.IsSuccess)
            return Result<StorageUploadSessionResponse>.Failure(providerLimitResult.Message ?? "Invalid upload provider limits", providerLimitResult.StatusCode);

        var storageFileId = Guid.NewGuid();
        var safeName = NormalizeFileName(request.FileName);
        var objectKey = BuildObjectKey(tenantId, storageFileId, safeName);
        var publicUrl = request.Visibility == StorageFileVisibility.Public
            ? BuildPublicUrl(profile, bucket, objectKey, preferCdn: false)
            : null;
        var cdnUrl = request.Visibility == StorageFileVisibility.Public
            ? BuildPublicUrl(profile, bucket, objectKey, preferCdn: true)
            : null;
        publicUrl = string.IsNullOrWhiteSpace(publicUrl) ? null : publicUrl;
        cdnUrl = string.IsNullOrWhiteSpace(cdnUrl) ? null : cdnUrl;

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
            PublicUrl = publicUrl,
            CdnBaseUrl = cdnUrl,
            UploadStartedAt = now,
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

        try
        {
            await provider.EnsureBucketAsync(profile, bucket, ct);
            session.ProviderUploadId = await provider.StartUploadAsync(profile, bucket, file, ct);

            db.Set<StorageFile>().Add(file);
            db.Set<StorageUploadSession>().Add(session);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create storage upload session for tenant {TenantId}", tenantId);
            await TryAbortProviderUploadAsync(provider, profile, bucket, file, session, ct);
            return Result<StorageUploadSessionResponse>.Failure("Failed to create storage upload session", 500);
        }

        logger.LogInformation(
            "Created storage upload session {UploadSessionId} for file {StorageFileId} in tenant {TenantId}",
            session.Id,
            file.Id,
            tenantId);

        return Result<StorageUploadSessionResponse>.Success(
            ToSessionResponse(session, file, uploadedParts: 0),
            201,
            "Upload session created");
    }

    public async Task<Result<StorageUploadPartResponse>> UploadPartAsync(
        UploadStorageFilePartRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveTenantId(request.Metadata);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageUploadPartResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        if (request.ChunkBytes is null || request.ChunkBytes.Length == 0)
            return Result<StorageUploadPartResponse>.Failure("Chunk payload is required", 400);

        var session = await db.Set<StorageUploadSession>()
            .Include(upload => upload.StorageFile)
            .AsTracking()
            .FirstOrDefaultAsync(
                upload => upload.Id == request.UploadSessionId && upload.TenantId == tenantId,
                ct);

        if (session is null)
            return Result<StorageUploadPartResponse>.NotFound("Upload session not found");

        if (session.Status is StorageUploadSessionStatus.Completed or StorageUploadSessionStatus.Aborted)
            return Result<StorageUploadPartResponse>.Conflict("Upload session is no longer writable");

        if (session.ExpiresAt <= DateTime.UtcNow)
        {
            session.Status = StorageUploadSessionStatus.Expired;
            await db.SaveChangesAsync(ct);
            return Result<StorageUploadPartResponse>.Failure("Upload session has expired", 410);
        }

        var validationResult = ValidatePartShape(session, request);
        if (!validationResult.IsSuccess)
            return Result<StorageUploadPartResponse>.Failure(validationResult.Message ?? "Invalid upload part", validationResult.StatusCode);

        var computedHash = ComputeSha256(request.ChunkBytes);
        var requestedHash = NormalizeHash(request.PartSha256Hash);
        if (string.IsNullOrWhiteSpace(requestedHash))
            return Result<StorageUploadPartResponse>.Failure("Part SHA-256 hash is required", 400);

        if (!string.Equals(requestedHash, computedHash, StringComparison.OrdinalIgnoreCase))
            return Result<StorageUploadPartResponse>.Failure("Part hash does not match payload", 400);

        var existingPart = await db.Set<StorageUploadPart>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                part => part.UploadSessionId == session.Id && part.PartNumber == request.PartNumber,
                ct);

        if (existingPart is not null)
        {
            if (existingPart.OffsetBytes == request.OffsetBytes &&
                existingPart.SizeBytes == request.ChunkBytes.Length &&
                string.Equals(existingPart.Sha256Hash, computedHash, StringComparison.OrdinalIgnoreCase))
            {
                return Result<StorageUploadPartResponse>.Success(
                    ToPartResponse(existingPart, wasAlreadyUploaded: true),
                    "Upload part already exists");
            }

            return Result<StorageUploadPartResponse>.Conflict("Upload part retry conflicts with the existing part metadata");
        }

        var profile = await GetProviderProfileAsync(session.StorageFile.ProviderProfileId, tenantId, ct);
        var bucket = await GetTenantBucketAsync(session.StorageFile.TenantBucketId, tenantId, ct);
        if (profile is null || bucket is null)
            return Result<StorageUploadPartResponse>.Failure("Storage provider metadata is missing", 500);

        var now = DateTime.UtcNow;
        var part = new StorageUploadPart
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UploadSessionId = session.Id,
            PartNumber = request.PartNumber,
            OffsetBytes = request.OffsetBytes,
            SizeBytes = request.ChunkBytes.Length,
            Sha256Hash = computedHash,
            UploadedAt = now,
            CreatedAt = now,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };

        var provider = providerFactory.Resolve(profile.Kind);
        part.ProviderPartId = await provider.UploadPartAsync(profile, bucket, session.StorageFile, session, part, request.ChunkBytes, ct);

        session.Status = StorageUploadSessionStatus.Uploading;
        session.ModifiedAt = now;
        session.StorageFile.Status = StorageFileStatus.Uploading;
        session.StorageFile.ModifiedAt = now;
        db.Set<StorageUploadPart>().Add(part);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(part).State = EntityState.Detached;
            var concurrentPart = await db.Set<StorageUploadPart>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.UploadSessionId == session.Id && item.PartNumber == request.PartNumber,
                    ct);

            if (concurrentPart is not null &&
                concurrentPart.OffsetBytes == request.OffsetBytes &&
                concurrentPart.SizeBytes == request.ChunkBytes.Length &&
                string.Equals(concurrentPart.Sha256Hash, computedHash, StringComparison.OrdinalIgnoreCase))
            {
                return Result<StorageUploadPartResponse>.Success(
                    ToPartResponse(concurrentPart, wasAlreadyUploaded: true),
                    "Upload part already exists");
            }

            return Result<StorageUploadPartResponse>.Conflict("Upload part retry conflicts with the existing part metadata");
        }

        return Result<StorageUploadPartResponse>.Success(ToPartResponse(part, wasAlreadyUploaded: false), 201, "Upload part stored");
    }

    public async Task<Result<StorageUploadPartListResponse>> ListPartsAsync(
        ListStorageUploadPartsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveTenantId(request.Metadata);
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

        var uploadedPartNumbers = parts.Select(part => part.PartNumber).ToHashSet();
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
        var tenantResult = ResolveTenantId(request.Metadata);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageFileResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var session = await db.Set<StorageUploadSession>()
            .Include(upload => upload.StorageFile)
            .Include(upload => upload.Parts)
            .AsTracking()
            .FirstOrDefaultAsync(
                upload => upload.Id == request.UploadSessionId && upload.TenantId == tenantId,
                ct);

        if (session is null)
            return Result<StorageFileResponse>.NotFound("Upload session not found");

        if (session.Status == StorageUploadSessionStatus.Completed)
            return Result<StorageFileResponse>.Success(ToFileResponse(session.StorageFile), "Upload session already completed");

        if (session.Status == StorageUploadSessionStatus.Aborted)
            return Result<StorageFileResponse>.Conflict("Upload session was aborted");

        var requestedHash = NormalizeHash(request.ExpectedSha256Hash);
        if (!string.IsNullOrWhiteSpace(requestedHash) &&
            !string.IsNullOrWhiteSpace(session.ExpectedSha256Hash) &&
            !string.Equals(requestedHash, session.ExpectedSha256Hash, StringComparison.OrdinalIgnoreCase))
        {
            return Result<StorageFileResponse>.Conflict("Completion hash does not match the upload session hash");
        }

        var parts = session.Parts
            .OrderBy(part => part.PartNumber)
            .ToList();
        if (parts.Count != session.TotalParts)
            return Result<StorageFileResponse>.Failure("Upload session is missing one or more parts", 400);

        var expectedPartNumbers = Enumerable.Range(1, session.TotalParts).ToArray();
        if (!parts.Select(part => part.PartNumber).SequenceEqual(expectedPartNumbers))
            return Result<StorageFileResponse>.Failure("Upload session has non-contiguous parts", 400);

        var uploadedSize = parts.Sum(part => (long)part.SizeBytes);
        if (uploadedSize != session.TotalSizeBytes)
            return Result<StorageFileResponse>.Failure("Uploaded part sizes do not match the declared file size", 400);

        var profile = await GetProviderProfileAsync(session.StorageFile.ProviderProfileId, tenantId, ct);
        var bucket = await GetTenantBucketAsync(session.StorageFile.TenantBucketId, tenantId, ct);
        if (profile is null || bucket is null)
            return Result<StorageFileResponse>.Failure("Storage provider metadata is missing", 500);

        var provider = providerFactory.Resolve(profile.Kind);
        string actualSha256Hash;
        try
        {
            session.StorageFile.ETag = await provider.CompleteUploadAsync(profile, bucket, session.StorageFile, session, parts, ct);
            actualSha256Hash = await provider.ComputeObjectSha256Async(profile, bucket, session.StorageFile, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to complete storage upload session {UploadSessionId}", session.Id);
            session.Status = StorageUploadSessionStatus.Failed;
            session.StorageFile.Status = StorageFileStatus.Failed;
            session.ModifiedAt = DateTime.UtcNow;
            session.StorageFile.ModifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Result<StorageFileResponse>.Failure("Storage provider failed to complete upload", 500);
        }

        var expectedHash = requestedHash ?? session.ExpectedSha256Hash;
        if (!string.IsNullOrWhiteSpace(expectedHash) &&
            !string.Equals(expectedHash, actualSha256Hash, StringComparison.OrdinalIgnoreCase))
        {
            session.Status = StorageUploadSessionStatus.Failed;
            session.StorageFile.Status = StorageFileStatus.Failed;
            session.StorageFile.Sha256Hash = actualSha256Hash;
            session.StorageFile.Hash = actualSha256Hash;
            session.ModifiedAt = DateTime.UtcNow;
            session.StorageFile.ModifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Result<StorageFileResponse>.Conflict("Completed object hash does not match the expected SHA-256 hash");
        }

        var now = DateTime.UtcNow;
        session.Status = StorageUploadSessionStatus.Completed;
        session.CompletedAt = now;
        session.ModifiedAt = now;
        session.StorageFile.Status = StorageFileStatus.Available;
        session.StorageFile.UploadedAt = now;
        session.StorageFile.CompletedAt = now;
        session.StorageFile.ModifiedAt = now;
        session.StorageFile.Sha256Hash = actualSha256Hash;
        session.StorageFile.Hash = session.StorageFile.Sha256Hash;

        await db.SaveChangesAsync(ct);

        return Result<StorageFileResponse>.Success(ToFileResponse(session.StorageFile), "Upload completed");
    }

    public async Task<Result> AbortUploadAsync(
        AbortStorageUploadSessionRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveTenantId(request.Metadata);
        if (!tenantResult.IsSuccess)
            return TenantFailure(tenantResult);
        var tenantId = tenantResult.Data;

        var session = await db.Set<StorageUploadSession>()
            .Include(upload => upload.StorageFile)
            .AsTracking()
            .FirstOrDefaultAsync(
                upload => upload.Id == request.UploadSessionId && upload.TenantId == tenantId,
                ct);

        if (session is null)
            return Result.NotFound("Upload session not found");

        if (session.Status == StorageUploadSessionStatus.Completed)
            return Result.Conflict("Completed upload sessions cannot be aborted");

        var profile = await GetProviderProfileAsync(session.StorageFile.ProviderProfileId, tenantId, ct);
        var bucket = await GetTenantBucketAsync(session.StorageFile.TenantBucketId, tenantId, ct);
        if (profile is not null && bucket is not null)
        {
            var provider = providerFactory.Resolve(profile.Kind);
            await provider.AbortUploadAsync(profile, bucket, session.StorageFile, session, ct);
        }

        var now = DateTime.UtcNow;
        session.Status = StorageUploadSessionStatus.Aborted;
        session.AbortedAt = now;
        session.ModifiedAt = now;
        session.StorageFile.Status = StorageFileStatus.Failed;
        session.StorageFile.ModifiedAt = now;
        await db.SaveChangesAsync(ct);

        return Result.Success("Upload session aborted");
    }

    public async Task<Result<StorageFileResponse>> GetFileAsync(
        GetStorageFileRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveTenantId(request.Metadata);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageFileResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var file = await db.Set<StorageFile>()
            .AsNoTracking()
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
        var tenantResult = ResolveTenantId(request.Metadata);
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
        var tenantResult = ResolveTenantId(request.Metadata);
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

        var expiresAt = DateTime.UtcNow.AddMinutes(
            Math.Max(1, request.ExpirationMinutes ?? storageOptions.SignedUrlExpirationMinutes));
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
        var tenantResult = ResolveTenantId(request.Metadata);
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
        var tenantResult = ResolveTenantId(request.Metadata);
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
        var tenantResult = ResolveTenantId(request.Metadata);
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
        await db.SaveChangesAsync(ct);

        return Result<StorageFileResponse>.Success(ToFileResponse(file), "Storage file restored");
    }

    public async Task<Result<StorageRetentionCleanupResponse>> CleanupRetentionAsync(
        CleanupStorageRetentionRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveTenantId(request.Metadata);
        if (!tenantResult.IsSuccess)
            return TenantFailure<StorageRetentionCleanupResponse>(tenantResult);
        var tenantId = tenantResult.Data;

        var now = DateTime.UtcNow;
        var maxFiles = request.MaxFiles <= 0 ? 100 : Math.Min(request.MaxFiles, 1000);
        var files = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(file => file.TenantId == tenantId)
            .Where(file => file.Status == StorageFileStatus.Deleted || file.IsDeleted)
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
            .Where(profile => profile.TenantId == tenantId)
            .ToDictionaryAsync(profile => profile.Id, ct);
        var buckets = await db.Set<StorageTenantBucket>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(bucket => bucket.TenantId == tenantId)
            .ToDictionaryAsync(bucket => bucket.Id, ct);

        foreach (var file in files)
        {
            if (file.ProviderProfileId is null ||
                file.TenantBucketId is null ||
                !profiles.TryGetValue(file.ProviderProfileId.Value, out var profile) ||
                !buckets.TryGetValue(file.TenantBucketId.Value, out var bucket))
            {
                file.ObjectDeletedAt = now;
                file.ModifiedAt = now;
                continue;
            }

            try
            {
                var provider = providerFactory.Resolve(profile.Kind);
                await provider.DeleteObjectAsync(profile, bucket, file, ct);
                file.ObjectDeletedAt = now;
                file.ModifiedAt = now;
                response.DeletedObjectCount++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to physically delete storage file {StorageFileId}", file.Id);
            }
        }

        await db.SaveChangesAsync(ct);
        return Result<StorageRetentionCleanupResponse>.Success(response);
    }

    public async Task<Result<StorageFileValidationResponse>> ValidateFileReferenceAsync(
        ValidateStorageFileReferenceRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = ResolveTenantId(request.Metadata);
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

        var profile = await GetProviderProfileAsync(file.ProviderProfileId, tenantId, ct);
        var bucket = await GetTenantBucketAsync(file.TenantBucketId, tenantId, ct);
        var objectKey = file.ObjectKey ?? file.ContentPath;

        return new StoragePublicUrlResponse
        {
            StorageFileId = file.Id,
            IsPublic = true,
            PublicUrl = !string.IsNullOrWhiteSpace(file.PublicUrl)
                ? file.PublicUrl
                : profile is not null && bucket is not null && !string.IsNullOrWhiteSpace(objectKey)
                ? BuildPublicUrl(profile, bucket, objectKey, preferCdn: false)
                : null,
            CdnUrl = !string.IsNullOrWhiteSpace(file.CdnBaseUrl)
                ? file.CdnBaseUrl
                : profile is not null && bucket is not null && !string.IsNullOrWhiteSpace(objectKey)
                ? BuildPublicUrl(profile, bucket, objectKey, preferCdn: true)
                : null
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
        CancellationToken ct)
    {
        var bucket = await db.Set<StorageTenantBucket>()
            .AsTracking()
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.ProviderProfileId == profile.Id && !item.IsDeleted,
                ct);

        if (bucket is not null)
            return bucket;

        bucket = new StorageTenantBucket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProviderProfileId = profile.Id,
            BucketName = BuildTenantBucketName(profile.BucketPrefix, tenantId),
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

    private Result<Guid> ResolveTenantId(RequestMetadata? metadata)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var isSignedInternalRequest = httpContext is null && IsTrustedServerMetadata(metadata);
        var trustedTenantId = TryGetClaimGuid(httpContext?.User, "tenant_id", "tenantId", "TenantId", "tenant", "tid")
            ?? TryGetItemGuid(httpContext, "TenantId")
            ?? (isSignedInternalRequest ? metadata?.TenantId : null);

        if (trustedTenantId is null && httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            trustedTenantId = Guid.TryParse(configuration["Tenant:DefaultId"], out var defaultTenantId)
                ? defaultTenantId
                : null;
        }

        if (trustedTenantId is null || trustedTenantId.Value == Guid.Empty)
            return Result<Guid>.Failure("Tenant context is required", 400);

        if (metadata?.TenantId is { } metadataTenantId &&
            metadataTenantId != Guid.Empty &&
            metadataTenantId != trustedTenantId.Value)
        {
            return Result<Guid>.Forbidden("Request tenant does not match trusted tenant context");
        }

        return Result<Guid>.Success(trustedTenantId.Value);
    }

    private bool IsTrustedServerMetadata(RequestMetadata? metadata)
    {
        var secret = configuration["Storage:TrustedMetadata:SharedSecret"]
            ?? configuration["BoltConfiguration:Signature"];
        var maxAgeMinutes = configuration.GetValue("Storage:TrustedMetadata:MaxAgeMinutes", 10);
        var maxAge = TimeSpan.FromMinutes(Math.Clamp(maxAgeMinutes, 1, 60));
        return RequestMetadataTrust.IsValid(metadata, secret, maxAge);
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
    {
        var normalizedPrefix = BucketNameSanitizer().Replace(bucketPrefix.ToLowerInvariant(), "-").Trim('-');
        if (string.IsNullOrWhiteSpace(normalizedPrefix))
            normalizedPrefix = "xframework";

        var name = $"{normalizedPrefix}-{tenantId:N}".ToLowerInvariant();
        if (name.Length > 63)
        {
            var tenantSuffix = tenantId.ToString("N");
            var maxPrefixLength = 63 - tenantSuffix.Length - 1;
            normalizedPrefix = normalizedPrefix.Length <= maxPrefixLength
                ? normalizedPrefix
                : normalizedPrefix[..maxPrefixLength].Trim('-');
            name = $"{normalizedPrefix}-{tenantSuffix}".Trim('-');
        }

        return name;
    }

    private static string BuildObjectKey(Guid tenantId, Guid fileId, string fileName) =>
        $"{tenantId:N}/{fileId:N}/{fileName}";

    private static string BuildPublicUrl(
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
        Status = file.Status,
        Visibility = file.Visibility,
        ProviderProfileName = file.ProviderProfileName,
        BucketName = file.BucketName,
        ObjectKey = file.ObjectKey,
        ContentLengthBytes = file.ContentLengthBytes,
        Sha256Hash = file.Sha256Hash,
        ETag = file.ETag,
        PublicUrl = file.PublicUrl,
        CdnBaseUrl = file.CdnBaseUrl,
        UploadStartedAt = file.UploadStartedAt,
        CompletedAt = file.CompletedAt,
        RetentionUntil = file.RetentionUntil,
        ObjectDeletedAt = file.ObjectDeletedAt,
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
