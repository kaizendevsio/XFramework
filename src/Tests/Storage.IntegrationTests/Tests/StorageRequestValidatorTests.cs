using FluentAssertions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Storage.Api.Services;
using Storage.Api.Validation;
using Storage.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Contracts;
using XFramework.TestInfrastructure;

namespace StorageValidationContractTests;

[TestFixture]
[Category(TestCategories.Storage)]
public sealed class StorageRequestValidatorTests
{
    private static readonly StorageOptions Options = new()
    {
        MaxFileSizeBytes = 1024,
        MaxSignedUrlExpirationMinutes = 60
    };

    [Test]
    public async Task CreateSession_RejectsInvalidBoundsEnumsAndHashes()
    {
        var validator = new CreateStorageUploadSessionRequestValidator(Microsoft.Extensions.Options.Options.Create(Options));
        var request = new CreateStorageUploadSessionRequest
        {
            FileName = new string('a', 256),
            ContentType = new string('b', 256),
            TypeId = Guid.Empty,
            StorageFileIdentifierId = Guid.Empty,
            TotalSizeBytes = 1025,
            ChunkSizeBytes = 0,
            Visibility = (StorageFileVisibility)999,
            ProviderProfileName = new string('c', 201),
            ExpectedSha256Hash = "not-a-sha256"
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(
            nameof(CreateStorageUploadSessionRequest.FileName),
            nameof(CreateStorageUploadSessionRequest.ContentType),
            nameof(CreateStorageUploadSessionRequest.TypeId),
            nameof(CreateStorageUploadSessionRequest.StorageFileIdentifierId),
            nameof(CreateStorageUploadSessionRequest.TotalSizeBytes),
            nameof(CreateStorageUploadSessionRequest.ChunkSizeBytes),
            nameof(CreateStorageUploadSessionRequest.Visibility),
            nameof(CreateStorageUploadSessionRequest.ProviderProfileName),
            nameof(CreateStorageUploadSessionRequest.ExpectedSha256Hash));
    }

    [Test]
    public async Task UploadPart_NullPayloadReturnsValidationFailureWithoutThrowing()
    {
        var validator = new UploadStorageFilePartRequestValidator();
        var request = new UploadStorageFilePartRequest
        {
            UploadSessionId = Guid.NewGuid(),
            PartNumber = 1,
            OffsetBytes = 0,
            PartSha256Hash = new string('a', 64),
            ChunkBytes = null!
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UploadStorageFilePartRequest.ChunkBytes));
    }

    [TestCase(0)]
    [TestCase(61)]
    public async Task DownloadUrl_RejectsExpiryOutsideConfiguredBounds(int expirationMinutes)
    {
        var validator = new GetStorageDownloadUrlRequestValidator(Microsoft.Extensions.Options.Options.Create(Options));
        var result = await validator.ValidateAsync(new GetStorageDownloadUrlRequest
        {
            StorageFileId = Guid.NewGuid(),
            ExpirationMinutes = expirationMinutes
        });

        result.IsValid.Should().BeFalse();
    }

    [Test]
    public async Task FileList_RejectsPageSizeAboveServiceLimit()
    {
        var result = await new GetStorageFilesRequestValidator().ValidateAsync(new GetStorageFilesRequest
        {
            Page = 1,
            PageSize = 101
        });

        result.IsValid.Should().BeFalse();
    }
}
