using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using System.Security.Cryptography;
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
        if (!profile.AutoCreateBuckets)
            return;

        using var client = CreateClient(profile);
        if (await AmazonS3Util.DoesS3BucketExistV2Async(client, bucket.BucketName))
            return;

        await client.PutBucketAsync(new PutBucketRequest
        {
            BucketName = bucket.BucketName,
            UseClientRegion = true
        }, ct);
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

    public async Task AbortUploadAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(session.ProviderUploadId))
            return;

        using var client = CreateClient(profile);
        await client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
        {
            BucketName = bucket.BucketName,
            Key = RequireObjectKey(file),
            UploadId = session.ProviderUploadId
        }, ct);
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
            Verb = HttpVerb.GET
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
}
