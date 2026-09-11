using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Storage.Api.Services;
using Storage.Api.Services.Providers;
using XFramework.Domain.Shared.Contracts;

namespace StorageMinioProviderContractTests;

[TestFixture]
public sealed class StorageDownloadEndpointTests
{
    [TestCase("http://minio:9000", "https://files.example.test", "files.example.test", "https")]
    [TestCase("http://minio:9000", null, "minio", "http")]
    [TestCase("https://another-store.example.test", "https://files.example.test", "another-store.example.test", "https")]
    public async Task PrivateUrl_IsSignedForConfiguredAliasWithoutChangingOtherProviders(
        string providerEndpoint, string? downloadEndpoint, string expectedHost, string expectedScheme)
    {
        var options = new StorageOptions
        {
            S3 = new() { Endpoint = "http://minio:9000", DownloadEndpoint = downloadEndpoint,
                AccessKeyId = "test-access-key", SecretAccessKey = "test-secret-key", Region = "us-east-1" }
        };
        var provider = new S3CompatibleStorageProvider(Options.Create(options), new ConfigurationBuilder().Build());
        var profile = new StorageProviderProfile { Endpoint = providerEndpoint, Region = "us-east-1", UsePathStyle = true };
        var result = await provider.CreateDownloadUrlAsync(profile,
            new StorageTenantBucket { BucketName = "private-test" },
            new StorageFile { Id = Guid.NewGuid(), ObjectKey = "attachment.txt" },
            DateTime.UtcNow.AddMinutes(5), CancellationToken.None);
        var uri = new Uri(result.Url);
        uri.Host.Should().Be(expectedHost);
        uri.Scheme.Should().Be(expectedScheme);
        uri.AbsolutePath.Should().Be("/private-test/attachment.txt");
        uri.Query.Should().Contain("X-Amz-Signature=");
        result.IsPublic.Should().BeFalse();
        profile.Endpoint.Should().Be(providerEndpoint);
    }
}
