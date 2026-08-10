using System.Net;
using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Storage.Api.Services;
using Storage.Api.Services.Providers;
using XFramework.Domain.Shared.Contracts;
using XFramework.TestInfrastructure;

namespace StorageAzuriteProviderContractTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Storage)]
[Category(TestCategories.StorageProvider)]
public sealed class StorageAzuriteProviderTests
{
    private const string AccountName = "storageaccount";
    private const string AccountKey = "c3RvcmFnZS10ZXN0LWtleS1zdG9yYWdlLXRlc3Qta2V5LXN0b3JhZ2UtdGVzdC1rZXktMTIzNDU2Nzg5MA==";

    [Test]
    public async Task AzureProvider_Azurite_PublicPrivateSignedAndReadinessContractsHold()
    {
        var endpoint = Environment.GetEnvironmentVariable("STORAGE_TEST_AZURITE_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
            Assert.Ignore("STORAGE_TEST_AZURITE_ENDPOINT is required for the Azurite provider contract test.");

        var connectionString = $"DefaultEndpointsProtocol=http;AccountName={AccountName};AccountKey={AccountKey};BlobEndpoint={endpoint!.TrimEnd('/')}/{AccountName};";
        var suffix = Guid.NewGuid().ToString("N")[..16];
        var readinessBucketName = $"xfw-ready-{suffix}";
        var options = new StorageOptions
        {
            AzureBlob = new AzureBlobStorageOptions
            {
                ConnectionString = connectionString,
                PublicBaseUrl = $"{endpoint.TrimEnd('/')}/{AccountName}",
                PublicDeliveryMode = StoragePublicDeliveryMode.ProviderManaged,
                ReadinessBucketName = readinessBucketName
            }
        };
        var provider = new AzureBlobStorageProvider(
            Options.Create(options),
            new ConfigurationBuilder().Build());
        var profile = new StorageProviderProfile
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Kind = StorageProviderKind.AzureBlob,
            ConnectionString = connectionString,
            AutoCreateBuckets = true,
            PublicBaseUrl = options.AzureBlob.PublicBaseUrl
        };
        var readinessBucket = CreateBucket(profile, readinessBucketName, StorageBucketPurpose.Private);
        var privateBucket = CreateBucket(profile, $"xfw-private-{suffix}", StorageBucketPurpose.Private);
        var publicBucket = CreateBucket(profile, $"xfw-public-{suffix}", StorageBucketPurpose.Public);

        try
        {
            await provider.EnsureBucketAsync(profile, readinessBucket, CancellationToken.None);
            await provider.EnsureBucketAsync(profile, privateBucket, CancellationToken.None);
            await provider.EnsureBucketAsync(profile, publicBucket, CancellationToken.None);
            await provider.CheckReadinessAsync(CancellationToken.None);

            var privateFile = await UploadAsync(provider, profile, privateBucket, "private.bin");
            var publicFile = await UploadAsync(provider, profile, publicBucket, "public.bin");
            await provider.EnsurePublicAccessAsync(profile, publicBucket, publicFile, CancellationToken.None);

            using var anonymous = new HttpClient();
            using var publicResponse = await anonymous.GetAsync(
                $"{options.AzureBlob.PublicBaseUrl}/{publicBucket.BucketName}/{publicFile.ObjectKey}");
            publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            (await publicResponse.Content.ReadAsByteArrayAsync()).Should().Equal(1, 2, 3, 4);

            using var privateResponse = await anonymous.GetAsync(
                $"{options.AzureBlob.PublicBaseUrl}/{privateBucket.BucketName}/{privateFile.ObjectKey}");
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
            var service = new BlobServiceClient(connectionString);
            foreach (var bucket in new[] { readinessBucket, privateBucket, publicBucket })
                await service.GetBlobContainerClient(bucket.BucketName).DeleteIfExistsAsync();
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
            UploadId = Guid.NewGuid().ToString("N"),
            TotalParts = 1,
            TotalSizeBytes = 4,
            ChunkSizeBytes = 4
        };
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
        await provider.CompleteUploadAsync(profile, bucket, file, session, [part], CancellationToken.None);
        return file;
    }
}
