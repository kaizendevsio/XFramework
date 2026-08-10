using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Api.Services.Providers;

public sealed class S3CompatibleStorageProvider(
    IOptions<StorageOptions> options,
    IConfiguration configuration) : IStorageObjectProvider
{
    private readonly StorageOptions storageOptions = options.Value;

    public StorageProviderKind Kind => StorageProviderKind.S3Compatible;

    public async Task EnsureBucketAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        CancellationToken ct)
    {
        using var client = CreateClient(profile);
        if (profile.AutoCreateBuckets &&
            !await AmazonS3Util.DoesS3BucketExistV2Async(client, bucket.BucketName))
        {
            await client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = bucket.BucketName,
                UseClientRegion = true
            }, ct);
        }

        await EnsurePublicBucketPolicyAsync(client, bucket, ct);
    }

    public async Task<string?> StartUploadAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct)
    {
        using var client = CreateClient(profile);
        var response = await client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName = bucket.BucketName,
            Key = RequireObjectKey(file),
            ContentType = file.ContentType
        }, ct);

        return response.UploadId;
    }

    public async Task<string> UploadPartAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        StorageUploadPart part,
        byte[] bytes,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(session.ProviderUploadId))
            throw new InvalidOperationException("S3-compatible multipart upload ID is missing.");

        using var client = CreateClient(profile);
        await using var stream = new MemoryStream(bytes, writable: false);
        var response = await client.UploadPartAsync(new UploadPartRequest
        {
            BucketName = bucket.BucketName,
            Key = RequireObjectKey(file),
            UploadId = session.ProviderUploadId,
            PartNumber = part.PartNumber,
            PartSize = bytes.LongLength,
            InputStream = stream
        }, ct);

        return response.ETag;
    }

    public async Task<string> ComputeObjectSha256Async(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct)
    {
        using var client = CreateClient(profile);
        using var response = await client.GetObjectAsync(bucket.BucketName, RequireObjectKey(file), ct);
        await using var stream = response.ResponseStream;
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<string?> CompleteUploadAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        IReadOnlyList<StorageUploadPart> parts,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(session.ProviderUploadId))
            throw new InvalidOperationException("S3-compatible multipart upload ID is missing.");

        using var client = CreateClient(profile);
        try
        {
            var response = await client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                BucketName = bucket.BucketName,
                Key = RequireObjectKey(file),
                UploadId = session.ProviderUploadId,
                PartETags = parts
                    .OrderBy(part => part.PartNumber)
                    .Select(part => new PartETag(part.PartNumber, part.ProviderPartId))
                    .ToList()
            }, ct);

            return response.ETag;
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "NoSuchUpload" or "InvalidRequest")
        {
            var metadata = await GetObjectMetadataAsync(profile, bucket, file, ct);
            if (metadata is not null && metadata.ContentLength == file.ContentLengthBytes)
                return metadata.ETag;
            throw;
        }
    }

    public async Task<StorageObjectMetadata?> GetObjectMetadataAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct)
    {
        using var client = CreateClient(profile);
        try
        {
            var response = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = bucket.BucketName,
                Key = RequireObjectKey(file)
            }, ct);
            return new StorageObjectMetadata(response.ContentLength, response.ETag);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public Task EnsurePublicAccessAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct) =>
        bucket.Purpose == StorageBucketPurpose.Public
            ? Task.CompletedTask
            : throw new InvalidOperationException("Public files must use a public-purpose S3 bucket.");

    public async Task CheckReadinessAsync(CancellationToken ct)
    {
        var bucketName = storageOptions.S3.ReadinessBucketName;
        if (string.IsNullOrWhiteSpace(bucketName))
            throw new InvalidOperationException("Storage:S3:ReadinessBucketName is required.");

        using var client = CreateClient(new StorageProviderProfile
        {
            Kind = StorageProviderKind.S3Compatible,
            UsePathStyle = storageOptions.S3.UsePathStyle
        });
        await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucketName,
            MaxKeys = 1
        }, ct);
    }

    public async Task AbortUploadAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        CancellationToken ct)
    {
        using var client = CreateClient(profile);
        var objectKey = RequireObjectKey(file);
        if (!string.IsNullOrWhiteSpace(session.ProviderUploadId))
        {
            await client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            {
                BucketName = bucket.BucketName,
                Key = objectKey,
                UploadId = session.ProviderUploadId
            }, ct);
            return;
        }

        string? keyMarker = null;
        string? uploadIdMarker = null;
        do
        {
            var response = await client.ListMultipartUploadsAsync(new ListMultipartUploadsRequest
            {
                BucketName = bucket.BucketName,
                Prefix = objectKey,
                KeyMarker = keyMarker,
                UploadIdMarker = uploadIdMarker
            }, ct);
            foreach (var upload in response.MultipartUploads.Where(upload => upload.Key == objectKey))
            {
                await client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
                {
                    BucketName = bucket.BucketName,
                    Key = objectKey,
                    UploadId = upload.UploadId
                }, ct);
            }

            keyMarker = response.IsTruncated == true ? response.NextKeyMarker : null;
            uploadIdMarker = response.IsTruncated == true ? response.NextUploadIdMarker : null;
        } while (keyMarker is not null || uploadIdMarker is not null);
    }

    public async Task DeleteObjectAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct)
    {
        using var client = CreateClient(profile);
        await client.DeleteObjectAsync(bucket.BucketName, RequireObjectKey(file), ct);
    }

    public Task<StorageDownloadUrlResponse> CreateDownloadUrlAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        DateTime expiresAt,
        CancellationToken ct)
    {
        using var client = CreateClient(profile);
        var url = client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucket.BucketName,
            Key = RequireObjectKey(file),
            Expires = expiresAt,
            Verb = HttpVerb.GET,
            Protocol = (profile.Endpoint ?? storageOptions.S3.Endpoint)?.StartsWith(
                "http://",
                StringComparison.OrdinalIgnoreCase) == true
                ? Protocol.HTTP
                : Protocol.HTTPS
        });

        return Task.FromResult(new StorageDownloadUrlResponse
        {
            StorageFileId = file.Id,
            Url = url,
            ExpiresAt = expiresAt,
            IsPublic = false
        });
    }

    private AmazonS3Client CreateClient(StorageProviderProfile profile)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = profile.UsePathStyle
        };

        var endpoint = profile.Endpoint ?? storageOptions.S3.Endpoint;
        var region = profile.Region ?? storageOptions.S3.Region;

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            config.ServiceURL = endpoint;
            config.UseHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }
        else if (!string.IsNullOrWhiteSpace(region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.USEast1;
        }

        var accessKeyId = ResolveSecret(profile.AccessKeyId, profile.AccessKeyIdSecretName)
            ?? ResolveSecret(storageOptions.S3.AccessKeyId, storageOptions.S3.AccessKeyIdSecretName);
        var secretAccessKey = ResolveSecret(profile.SecretAccessKey, profile.SecretAccessKeySecretName)
            ?? ResolveSecret(storageOptions.S3.SecretAccessKey, storageOptions.S3.SecretAccessKeySecretName);

        if (!string.IsNullOrWhiteSpace(accessKeyId) && !string.IsNullOrWhiteSpace(secretAccessKey))
            return new AmazonS3Client(accessKeyId, secretAccessKey, config);

        return new AmazonS3Client(config);
    }

    private string? ResolveSecret(string? value, string? secretName)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        if (string.IsNullOrWhiteSpace(secretName))
            return null;

        return configuration[secretName] ?? Environment.GetEnvironmentVariable(secretName);
    }

    private static string RequireObjectKey(StorageFile file) =>
        string.IsNullOrWhiteSpace(file.ObjectKey)
            ? throw new InvalidOperationException("Storage file object key is missing.")
            : file.ObjectKey;

    private async Task EnsurePublicBucketPolicyAsync(
        IAmazonS3 client,
        StorageTenantBucket bucket,
        CancellationToken ct)
    {
        if (bucket.Purpose != StorageBucketPurpose.Public ||
            storageOptions.S3.PublicDeliveryMode != StoragePublicDeliveryMode.ProviderManaged)
        {
            await client.DeleteBucketPolicyAsync(new DeleteBucketPolicyRequest
            {
                BucketName = bucket.BucketName
            }, ct);
            return;
        }

        var policy = JsonSerializer.Serialize(new
        {
            Version = "2012-10-17",
            Statement = new[]
            {
                new
                {
                    Sid = "XFrameworkStoragePublicRead",
                    Effect = "Allow",
                    Principal = new { AWS = "*" },
                    Action = "s3:GetObject",
                    Resource = $"arn:aws:s3:::{bucket.BucketName}/*"
                }
            }
        });
        await client.PutBucketPolicyAsync(new PutBucketPolicyRequest
        {
            BucketName = bucket.BucketName,
            Policy = policy
        }, ct);
    }
}
