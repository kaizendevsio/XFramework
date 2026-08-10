using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Storage.Api.Services;
using Storage.Api.Services.Providers;
using XFramework.Domain.Shared.Contracts;
using XFramework.TestInfrastructure;

namespace StorageMinioProviderContractTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Storage)]
[Category(TestCategories.StorageProvider)]
public sealed class StorageMinioProviderTests
{
    [Test]
    public async Task S3CompatibleProvider_MinIo_PublicPrivateSignedAndReadinessContractsHold()
    {
        var endpoint = Environment.GetEnvironmentVariable("STORAGE_TEST_MINIO_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
            Assert.Ignore("STORAGE_TEST_MINIO_ENDPOINT is required for the MinIO provider contract test.");

        var accessKey = Environment.GetEnvironmentVariable("STORAGE_TEST_MINIO_ACCESS_KEY")
            ?? "storage_test_admin";
        var secretKey = Environment.GetEnvironmentVariable("STORAGE_TEST_MINIO_SECRET_KEY")
            ?? "storage_test_password";
        var suffix = Guid.NewGuid().ToString("N")[..16];
        var readinessBucketName = $"xfw-ready-{suffix}";
        var options = new StorageOptions
        {
            S3 = new S3StorageOptions
            {
                Endpoint = endpoint,
                Region = "us-east-1",
                AccessKeyId = accessKey,
                SecretAccessKey = secretKey,
                UsePathStyle = true,
                PublicBaseUrl = endpoint,
                PublicDeliveryMode = StoragePublicDeliveryMode.ProviderManaged,
                ReadinessBucketName = readinessBucketName
            }
        };
        var provider = new S3CompatibleStorageProvider(
            Options.Create(options),
            new ConfigurationBuilder().Build());
        var profile = new StorageProviderProfile
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Kind = StorageProviderKind.S3Compatible,
            Endpoint = endpoint,
            Region = "us-east-1",
            AccessKeyId = accessKey,
            SecretAccessKey = secretKey,
            UsePathStyle = true,
            AutoCreateBuckets = true,
            PublicBaseUrl = endpoint
        };
        var readinessBucket = CreateBucket(profile, readinessBucketName, StorageBucketPurpose.Private);
        var privateBucket = CreateBucket(profile, $"xfw-private-{suffix}", StorageBucketPurpose.Private);
        var publicBucket = CreateBucket(profile, $"xfw-public-{suffix}", StorageBucketPurpose.Public);

        try
        {
            await provider.EnsureBucketAsync(profile, readinessBucket, CancellationToken.None);
            await provider.EnsureBucketAsync(profile, publicBucket, CancellationToken.None);
            await provider.CheckReadinessAsync(CancellationToken.None);

            privateBucket.Purpose = StorageBucketPurpose.Public;
            await provider.EnsureBucketAsync(profile, privateBucket, CancellationToken.None);
            var legacyPublicFile = await UploadAsync(provider, profile, privateBucket, "legacy-public.bin");
            using var anonymous = new HttpClient();
            using (var legacyPublicResponse = await anonymous.GetAsync(
                       $"{endpoint!.TrimEnd('/')}/{privateBucket.BucketName}/{legacyPublicFile.ObjectKey}"))
            {
                legacyPublicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            privateBucket.Purpose = StorageBucketPurpose.Private;
            await provider.EnsureBucketAsync(profile, privateBucket, CancellationToken.None);
            using (var remediatedPrivateResponse = await anonymous.GetAsync(
                       $"{endpoint.TrimEnd('/')}/{privateBucket.BucketName}/{legacyPublicFile.ObjectKey}"))
            {
                remediatedPrivateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            }

            var orphanFile = new StorageFile
            {
                Id = Guid.NewGuid(),
                TenantId = profile.TenantId,
                ObjectKey = "orphaned-upload.bin",
                ContentType = "application/octet-stream",
                ContentLengthBytes = 4
            };
            var orphanSession = new StorageUploadSession
            {
                Id = Guid.NewGuid(),
                TenantId = profile.TenantId,
                StorageFileId = orphanFile.Id
            };
            _ = await provider.StartUploadAsync(profile, privateBucket, orphanFile, CancellationToken.None);
            orphanSession.ProviderUploadId = null;
            await provider.AbortUploadAsync(
                profile,
                privateBucket,
                orphanFile,
                orphanSession,
                CancellationToken.None);
            await AssertNoMultipartUploadAsync(
                endpoint,
                accessKey,
                secretKey,
                privateBucket.BucketName,
                orphanFile.ObjectKey);

            var privateFile = await UploadAsync(provider, profile, privateBucket, "private.bin");
            var publicFile = await UploadAsync(provider, profile, publicBucket, "public.bin");
            await provider.EnsurePublicAccessAsync(profile, publicBucket, publicFile, CancellationToken.None);

            using var publicResponse = await anonymous.GetAsync(
                $"{endpoint!.TrimEnd('/')}/{publicBucket.BucketName}/{publicFile.ObjectKey}");
            publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            (await publicResponse.Content.ReadAsByteArrayAsync()).Should().Equal(1, 2, 3, 4);

            using var privateResponse = await anonymous.GetAsync(
                $"{endpoint.TrimEnd('/')}/{privateBucket.BucketName}/{privateFile.ObjectKey}");
            privateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var signed = await provider.CreateDownloadUrlAsync(
                profile,
                privateBucket,
                privateFile,
                DateTime.UtcNow.AddMinutes(5),
                CancellationToken.None);
            using var signedResponse = await anonymous.GetAsync(signed.Url);
            signedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await DeleteBucketsAsync(
                endpoint!,
                accessKey,
                secretKey,
                readinessBucketName,
                privateBucket.BucketName,
                publicBucket.BucketName);
        }
    }

    private static StorageTenantBucket CreateBucket(
        StorageProviderProfile profile,
        string name,
        StorageBucketPurpose purpose) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = profile.TenantId,
        ProviderProfileId = profile.Id,
        BucketName = name,
        Purpose = purpose,
        PublicBaseUrl = profile.PublicBaseUrl
    };

    private static async Task<StorageFile> UploadAsync(
        IStorageObjectProvider provider,
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        string objectKey)
    {
        var file = new StorageFile
        {
            Id = Guid.NewGuid(),
            TenantId = profile.TenantId,
            ObjectKey = objectKey,
            ContentType = "application/octet-stream",
            ContentLengthBytes = 4,
            Visibility = bucket.Purpose == StorageBucketPurpose.Public
                ? StorageFileVisibility.Public
                : StorageFileVisibility.Private
        };
        var session = new StorageUploadSession
        {
            Id = Guid.NewGuid(),
            TenantId = profile.TenantId,
            StorageFileId = file.Id,
            TotalParts = 1,
            TotalSizeBytes = 4,
            ChunkSizeBytes = 4
        };
        session.ProviderUploadId = await provider.StartUploadAsync(profile, bucket, file, CancellationToken.None);
        var part = new StorageUploadPart
        {
            Id = Guid.NewGuid(),
            TenantId = profile.TenantId,
            UploadSessionId = session.Id,
            PartNumber = 1,
            OffsetBytes = 0,
            SizeBytes = 4,
            Sha256Hash = "9f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a"
        };
        part.ProviderPartId = await provider.UploadPartAsync(
            profile, bucket, file, session, part, [1, 2, 3, 4], CancellationToken.None);
        var etag = await provider.CompleteUploadAsync(profile, bucket, file, session, [part], CancellationToken.None);
        var recoveredEtag = await provider.CompleteUploadAsync(profile, bucket, file, session, [part], CancellationToken.None);
        recoveredEtag.Should().Be(etag);
        return file;
    }

    private static async Task DeleteBucketsAsync(
        string endpoint,
        string accessKey,
        string secretKey,
        params string[] bucketNames)
    {
        using var client = new AmazonS3Client(
            accessKey,
            secretKey,
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                UseHttp = Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp
            });

        foreach (var bucketName in bucketNames)
        {
            try
            {
                string? continuationToken = null;
                do
                {
                    var listed = await client.ListObjectsV2Async(new ListObjectsV2Request
                    {
                        BucketName = bucketName,
                        ContinuationToken = continuationToken
                    });
                    foreach (var item in listed.S3Objects)
                        await client.DeleteObjectAsync(bucketName, item.Key);
                    continuationToken = listed.IsTruncated == true ? listed.NextContinuationToken : null;
                } while (continuationToken is not null);

                await client.DeleteBucketAsync(bucketName);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Bucket creation may have failed before cleanup.
            }
        }
    }

    private static async Task AssertNoMultipartUploadAsync(
        string endpoint,
        string accessKey,
        string secretKey,
        string bucketName,
        string objectKey)
    {
        using var client = new AmazonS3Client(
            accessKey,
            secretKey,
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                UseHttp = Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp
            });
        var uploads = await client.ListMultipartUploadsAsync(new ListMultipartUploadsRequest
        {
            BucketName = bucketName,
            Prefix = objectKey
        });

        uploads.MultipartUploads.Should().NotContain(upload => upload.Key == objectKey);
    }
}
