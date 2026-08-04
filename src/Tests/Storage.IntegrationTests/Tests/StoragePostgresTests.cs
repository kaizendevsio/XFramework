using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Storage.Api.Services;
using Storage.IntegrationTests.Infrastructure;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.TestInfrastructure;

namespace Storage.IntegrationTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Storage)]
public sealed class StoragePostgresTests : StorageIntegrationTestBase
{
    [Test]
    [Category(TestCategories.DataContext)]
    public async Task Migration_CreatesStorageTablesAndIndexes()
    {
        await using var db = CreateDbContext();

        var tableCount = await ScalarAsync<long>(
            db,
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'Storage'
              AND table_name IN (
                  'StorageFile',
                  'StorageFileType',
                  'StorageFileIdentifier',
                  'StorageFileIdentifierGroup',
                  'StorageProviderProfile',
                  'StorageTenantBucket',
                  'StorageUploadSession',
                  'StorageUploadPart'
              );
            """);

        var retentionIndexExists = await IndexExistsAsync(db, "ix_storagefile_tenant_retention_objectdeleted");
        var providerDefaultIndexExists = await IndexExistsAsync(db, "ix_storageproviderprofile_tenant_default");
        var bucketIndexExists = await IndexExistsAsync(db, "ix_storagetenantbucket_bucket");
        var unclaimedIndexExists = await IndexExistsAsync(db, "ix_storagefile_global_unclaimed_due");
        var expiredSessionIndexExists = await IndexExistsAsync(db, "ix_storageuploadsession_global_expired_due");

        tableCount.Should().Be(8);
        retentionIndexExists.Should().BeTrue();
        providerDefaultIndexExists.Should().BeTrue();
        bucketIndexExists.Should().BeTrue();
        unclaimedIndexExists.Should().BeTrue();
        expiredSessionIndexExists.Should().BeTrue();
    }

    [Test]
    [Category(TestCategories.Auth)]
    public async Task GetFiles_UnauthenticatedRequest_ReturnsUnauthorizedOrForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/storage/files");
        request.Headers.Add(TestAuthHeaders.Unauthenticated, "true");

        using var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Test]
    [Category(TestCategories.Wrappers)]
    public async Task Wrapper_EnsureStorageUploadMetadata_RepeatedCallsReturnTenantScopedMetadata()
    {
        var metadata = CreateMetadata();
        var suffix = Guid.NewGuid().ToString("N");
        var contentType = $"image/x-integration-{suffix}";
        var groupName = $"Integration Group {suffix}";
        var identifierName = $"Integration Identifier {suffix}";
        var request = new EnsureStorageUploadMetadataRequest
        {
            Metadata = metadata,
            ContentType = contentType,
            IdentifierGroupName = groupName,
            IdentifierName = identifierName,
            IdentifierDescription = "Storage wrapper integration metadata"
        };

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => ServiceWrapper.EnsureStorageUploadMetadata(request)));
        var first = results[0];

        first.IsSuccess.Should().BeTrue(first.Message);
        first.Response.Should().NotBeNull();
        results.Should().OnlyContain(result => result.IsSuccess && result.Response != null);
        results.Select(result => result.Response).Should().OnlyContain(response => response == first.Response);
        var ensuredMetadata = first.Response!;
        var tenantId = metadata.RequestedTenantId!.Value;

        await using var db = CreateDbContext();
        var type = await db.Set<StorageFileType>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == ensuredMetadata.TypeId);
        var identifier = await db.Set<StorageFileIdentifier>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == ensuredMetadata.StorageFileIdentifierId);
        var group = await db.Set<StorageFileIdentifierGroup>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == identifier.GroupId);

        type.TenantId.Should().Be(tenantId);
        type.Name.Should().Be(contentType);
        group.TenantId.Should().Be(tenantId);
        group.Name.Should().Be(groupName);
        identifier.TenantId.Should().Be(tenantId);
        identifier.Name.Should().Be(identifierName);
        identifier.GroupId.Should().Be(group.Id);

        (await db.Set<StorageFileType>()
                .IgnoreQueryFilters()
                .CountAsync(item => item.TenantId == tenantId && item.Name == contentType))
            .Should().Be(1);
        (await db.Set<StorageFileIdentifierGroup>()
                .IgnoreQueryFilters()
                .CountAsync(item => item.TenantId == tenantId && item.Name == groupName))
            .Should().Be(1);
        (await db.Set<StorageFileIdentifier>()
                .IgnoreQueryFilters()
                .CountAsync(item => item.TenantId == tenantId && item.Name == identifierName))
            .Should().Be(1);
    }

    [Test]
    [Category(TestCategories.Wrappers)]
    public async Task Wrapper_CreateUploadSessionUploadPartsAndComplete_PersistsAvailableFile()
    {
        var metadata = CreateMetadata();
        var bytes1 = new byte[] { 1, 2, 3, 4 };
        var bytes2 = new byte[] { 5, 6, 7, 8 };
        var expectedHash = Sha256(bytes1.Concat(bytes2).ToArray());
        var session = await CreateUploadSessionAsync(metadata, totalSizeBytes: 8, chunkSizeBytes: 4, expectedSha256Hash: expectedHash);

        var firstPart = await UploadPartAsync(metadata, session.Id, 1, 0, bytes1);
        var secondPart = await UploadPartAsync(metadata, session.Id, 2, 4, bytes2);
        var parts = await ServiceWrapper.ListStorageUploadParts(new ListStorageUploadPartsRequest
        {
            Metadata = metadata,
            UploadSessionId = session.Id
        });
        var complete = await ServiceWrapper.CompleteStorageUploadSession(new CompleteStorageUploadSessionRequest
        {
            Metadata = metadata,
            UploadSessionId = session.Id
        });

        firstPart.WasAlreadyUploaded.Should().BeFalse();
        secondPart.WasAlreadyUploaded.Should().BeFalse();
        parts.IsSuccess.Should().BeTrue(parts.Message);
        parts.Response!.MissingPartNumbers.Should().BeEmpty();
        complete.IsSuccess.Should().BeTrue(complete.Message);
        complete.Response!.Status.Should().Be(StorageFileStatus.Available);
        complete.Response.ETag.Should().Be("integration-etag");
        complete.Response.Sha256Hash.Should().Be(expectedHash);

        await using var db = CreateDbContext();
        var file = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == session.StorageFileId);
        file.Status.Should().Be(StorageFileStatus.Available);
        file.Sha256Hash.Should().Be(expectedHash);
        file.ProviderProfileId.Should().NotBeNull();
        file.TenantBucketId.Should().NotBeNull();
    }

    [Test]
    [Category(TestCategories.Wrappers)]
    public async Task Wrapper_ClaimStorageFile_IsTenantScopedAndIdempotent()
    {
        var metadata = CreateMetadata();
        var session = await CreateCompletedSessionAsync(
            metadata,
            visibility: StorageFileVisibility.Public,
            requireClaim: true);

        var first = await ServiceWrapper.ClaimStorageFile(new ClaimStorageFileRequest
        {
            Metadata = metadata,
            StorageFileId = session.StorageFileId
        });
        var second = await ServiceWrapper.ClaimStorageFile(new ClaimStorageFileRequest
        {
            Metadata = metadata,
            StorageFileId = session.StorageFileId
        });

        first.IsSuccess.Should().BeTrue(first.Message);
        second.IsSuccess.Should().BeTrue(second.Message);
        first.Response!.UnclaimedUntil.Should().BeNull();
        second.Response!.UnclaimedUntil.Should().BeNull();

        await using var db = CreateDbContext();
        var file = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == session.StorageFileId);
        file.UnclaimedUntil.Should().BeNull();
    }

    [Test]
    [Category(TestCategories.Wrappers)]
    public async Task Wrapper_ReadAbortAndValidationMethods_HaveDirectTransportCoverage()
    {
        var metadata = CreateMetadata();
        var completed = await CreateCompletedSessionAsync(
            metadata,
            visibility: StorageFileVisibility.Public);

        (await ServiceWrapper.GetStorageFile(new GetStorageFileRequest
        {
            Metadata = metadata,
            StorageFileId = completed.StorageFileId
        })).IsSuccess.Should().BeTrue();
        (await ServiceWrapper.GetStorageFiles(new GetStorageFilesRequest
        {
            Metadata = metadata,
            Page = 1,
            PageSize = 10
        })).IsSuccess.Should().BeTrue();
        (await ServiceWrapper.GetStoragePublicUrl(new GetStoragePublicUrlRequest
        {
            Metadata = metadata,
            StorageFileId = completed.StorageFileId
        })).IsSuccess.Should().BeTrue();
        (await ServiceWrapper.GetStorageDownloadUrl(new GetStorageDownloadUrlRequest
        {
            Metadata = metadata,
            StorageFileId = completed.StorageFileId
        })).IsSuccess.Should().BeTrue();
        (await ServiceWrapper.ValidateStorageFileReference(new ValidateStorageFileReferenceRequest
        {
            Metadata = metadata,
            StorageFileId = completed.StorageFileId
        })).IsSuccess.Should().BeTrue();

        var incomplete = await CreateUploadSessionAsync(metadata);
        (await ServiceWrapper.AbortStorageUploadSession(new AbortStorageUploadSessionRequest
        {
            Metadata = metadata,
            UploadSessionId = incomplete.Id
        })).IsSuccess.Should().BeTrue();
    }

    [Test]
    [Category(TestCategories.StorageProvider)]
    public async Task Maintenance_RetriesProviderFailuresThenDeletesUnclaimedFileAndAbortsExpiredSession()
    {
        var metadata = CreateMetadata();
        var completed = await CreateCompletedSessionAsync(metadata, requireClaim: true);
        var incomplete = await CreateUploadSessionAsync(metadata);
        var now = DateTime.UtcNow;

        await using (var setupDb = CreateDbContext())
        {
            await setupDb.Set<StorageFile>()
                .IgnoreQueryFilters()
                .Where(file => file.Id == completed.StorageFileId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(file => file.UnclaimedUntil, now.AddMinutes(-1)));
            await setupDb.Set<StorageUploadSession>()
                .IgnoreQueryFilters()
                .Where(session => session.Id == incomplete.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.ExpiresAt, now.AddMinutes(-1)));
        }

        StorageIntegrationTestFixture.Provider.FailNextDelete = true;
        StorageIntegrationTestFixture.Provider.FailNextAbort = true;
        await RunMaintenanceAsync();

        await using (var failedDb = CreateDbContext())
        {
            var file = await failedDb.Set<StorageFile>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.Id == completed.StorageFileId);
            var session = await failedDb.Set<StorageUploadSession>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.Id == incomplete.Id);
            file.Status.Should().Be(StorageFileStatus.Deleted);
            file.ObjectDeletedAt.Should().BeNull();
            session.Status.Should().Be(StorageUploadSessionStatus.Expired);
            session.AbortedAt.Should().BeNull();
        }

        var result = await RunMaintenanceAsync();

        result.DeletedUnclaimedFiles.Should().Be(1);
        result.ExpiredUploadSessions.Should().Be(1);
        StorageIntegrationTestFixture.Provider.DeleteObjectAttemptCount.Should().Be(2);
        StorageIntegrationTestFixture.Provider.DeleteObjectCount.Should().Be(1);
        StorageIntegrationTestFixture.Provider.AbortUploadCount.Should().Be(2);

        await using var db = CreateDbContext();
        var deletedFile = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == completed.StorageFileId);
        var expiredSession = await db.Set<StorageUploadSession>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == incomplete.Id);
        deletedFile.ObjectDeletedAt.Should().NotBeNull();
        expiredSession.AbortedAt.Should().NotBeNull();
    }

    [Test]
    [Category(TestCategories.Wrappers)]
    public async Task Wrapper_UploadPartWithoutHash_ReturnsBadRequest()
    {
        var metadata = CreateMetadata();
        var session = await CreateUploadSessionAsync(metadata);

        var result = await ServiceWrapper.UploadStorageFilePart(new UploadStorageFilePartRequest
        {
            Metadata = metadata,
            UploadSessionId = session.Id,
            PartNumber = 1,
            OffsetBytes = 0,
            ChunkBytes = [1, 2, 3, 4]
        });

        result.IsSuccess.Should().BeFalse();
        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    [Category(TestCategories.Wrappers)]
    public async Task Wrapper_CompleteUploadWrongExpectedHash_ReturnsConflictAndMarksFileFailed()
    {
        var metadata = CreateMetadata();
        var bytes = new byte[] { 1, 2, 3, 4 };
        var session = await CreateUploadSessionAsync(metadata);
        _ = await UploadPartAsync(metadata, session.Id, 1, 0, bytes);

        var result = await ServiceWrapper.CompleteStorageUploadSession(new CompleteStorageUploadSessionRequest
        {
            Metadata = metadata,
            UploadSessionId = session.Id,
            ExpectedSha256Hash = Sha256([4, 3, 2, 1])
        });

        result.IsSuccess.Should().BeFalse();
        result.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var db = CreateDbContext();
        var file = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == session.StorageFileId);
        file.Status.Should().Be(StorageFileStatus.Failed);
        file.Sha256Hash.Should().Be(Sha256(bytes));
    }

    [Test]
    [Category(TestCategories.StorageProvider)]
    public async Task RestUploadPart_OctetStreamPayload_PersistsPart()
    {
        var metadata = CreateMetadata();
        var session = await CreateUploadSessionAsync(metadata);
        var bytes = new byte[] { 9, 8, 7, 6 };
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await HttpClient.PostAsync(
            $"/api/storage/uploads/sessions/{session.Id}/parts?partNumber=1&offsetBytes=0&partSha256Hash={Sha256(bytes)}",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        await using var db = CreateDbContext();
        var part = await db.Set<StorageUploadPart>()
            .AsNoTracking()
            .SingleAsync(item => item.UploadSessionId == session.Id && item.PartNumber == 1);
        part.SizeBytes.Should().Be(4);
        part.Sha256Hash.Should().Be(Sha256(bytes));
    }

    [Test]
    [Category(TestCategories.Auth)]
    public async Task RestUploadPart_ContentLengthOverLimit_ReturnsPayloadTooLarge()
    {
        using var content = new OversizedRequestContent((100L * 1024 * 1024) + 1);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/storage/uploads/sessions/{Guid.NewGuid()}/parts?partNumber=1&offsetBytes=0")
        {
            Content = content
        };
        request.Headers.ExpectContinue = true;

        using var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    private sealed class OversizedRequestContent(long contentLength) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("The server must reject the oversized Content-Length before reading the body.");

        protected override bool TryComputeLength(out long length)
        {
            length = contentLength;
            return true;
        }
    }

    [Test]
    [Category(TestCategories.Wrappers)]
    public async Task Wrapper_DeleteRestoreAndRetentionCleanup_UsesSoftDeleteThenPhysicalCleanup()
    {
        var metadata = CreateMetadata();
        var session = await CreateCompletedSessionAsync(metadata);

        var delete = await ServiceWrapper.DeleteStorageFile(new DeleteStorageFileRequest
        {
            Metadata = metadata,
            StorageFileId = session.StorageFileId
        });
        delete.IsSuccess.Should().BeTrue(delete.Message);

        var restore = await ServiceWrapper.RestoreStorageFile(new RestoreStorageFileRequest
        {
            Metadata = metadata,
            StorageFileId = session.StorageFileId
        });
        restore.IsSuccess.Should().BeTrue(restore.Message);
        restore.Response!.Status.Should().Be(StorageFileStatus.Available);

        var deleteForCleanup = await ServiceWrapper.DeleteStorageFile(new DeleteStorageFileRequest
        {
            Metadata = metadata,
            StorageFileId = session.StorageFileId,
            RetentionUntil = DateTime.UtcNow.AddMinutes(-1)
        });
        deleteForCleanup.IsSuccess.Should().BeTrue(deleteForCleanup.Message);

        var cleanup = await ServiceWrapper.CleanupStorageRetention(new CleanupStorageRetentionRequest
        {
            Metadata = metadata,
            MaxFiles = 10
        });
        var secondCleanup = await ServiceWrapper.CleanupStorageRetention(new CleanupStorageRetentionRequest
        {
            Metadata = metadata,
            MaxFiles = 10
        });

        cleanup.IsSuccess.Should().BeTrue(cleanup.Message);
        cleanup.Response!.MatchedCount.Should().Be(1);
        cleanup.Response.DeletedObjectCount.Should().Be(1);
        secondCleanup.Response!.MatchedCount.Should().Be(0);
        StorageIntegrationTestFixture.Provider.DeleteObjectCount.Should().Be(1);

        await using var db = CreateDbContext();
        var file = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == session.StorageFileId);
        file.IsDeleted.Should().BeTrue();
        file.ObjectDeletedAt.Should().NotBeNull();
    }

    [Test]
    [Category(TestCategories.DataContext)]
    public async Task RemoteDataContext_QueryStorageFile_ReturnsFileFromStorageService()
    {
        var metadata = CreateMetadata();
        var session = await CreateCompletedSessionAsync(metadata);

        var files = await DataContext.Query<StorageFile>()
            .Where(item => item.Id == session.StorageFileId)
            .ToListAsync();

        files.Should().ContainSingle(item => item.Id == session.StorageFileId);
    }

    private static async Task<StorageUploadSessionResponse> CreateCompletedSessionAsync(
        RequestMetadata metadata,
        StorageFileVisibility visibility = StorageFileVisibility.Private,
        bool requireClaim = false)
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var session = await CreateUploadSessionAsync(
            metadata,
            visibility: visibility,
            expectedSha256Hash: Sha256(bytes),
            requireClaim: requireClaim);
        _ = await UploadPartAsync(metadata, session.Id, 1, 0, bytes);

        var complete = await ServiceWrapper.CompleteStorageUploadSession(new CompleteStorageUploadSessionRequest
        {
            Metadata = metadata,
            UploadSessionId = session.Id
        });
        complete.IsSuccess.Should().BeTrue(complete.Message);

        return session;
    }

    private static async Task<StorageUploadSessionResponse> CreateUploadSessionAsync(
        RequestMetadata metadata,
        long totalSizeBytes = 4,
        int chunkSizeBytes = 4,
        StorageFileVisibility visibility = StorageFileVisibility.Private,
        string? expectedSha256Hash = null,
        bool requireClaim = false)
    {
        var result = await ServiceWrapper.CreateStorageUploadSession(new CreateStorageUploadSessionRequest
        {
            Metadata = metadata,
            FileName = $"storage-{Guid.NewGuid():N}.bin",
            ContentType = "application/octet-stream",
            TypeId = TestConstants.StorageFileTypeId,
            Identifier = Guid.NewGuid(),
            StorageFileIdentifierId = TestConstants.StorageFileIdentifierId,
            TotalSizeBytes = totalSizeBytes,
            ChunkSizeBytes = chunkSizeBytes,
            Visibility = visibility,
            ExpectedSha256Hash = expectedSha256Hash,
            RequireClaim = requireClaim
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        return result.Response!;
    }

    private static async Task<StorageMaintenanceBatchResult> RunMaintenanceAsync()
    {
        await using var db = CreateDbContext();
        var service = new StorageMaintenanceService(
            db,
            new IntegrationStorageProviderFactory(StorageIntegrationTestFixture.Provider),
            Options.Create(new StorageOptions { MaintenanceBatchSize = 20 }),
            TimeProvider.System,
            NullLogger<StorageMaintenanceService>.Instance);
        return await service.RunBatchAsync();
    }

    private static async Task<StorageUploadPartResponse> UploadPartAsync(
        RequestMetadata metadata,
        Guid uploadSessionId,
        int partNumber,
        long offsetBytes,
        byte[] bytes)
    {
        var result = await ServiceWrapper.UploadStorageFilePart(new UploadStorageFilePartRequest
        {
            Metadata = metadata,
            UploadSessionId = uploadSessionId,
            PartNumber = partNumber,
            OffsetBytes = offsetBytes,
            PartSha256Hash = Sha256(bytes),
            ChunkBytes = bytes
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        return result.Response!;
    }

    private static async Task<bool> IndexExistsAsync(DbContext db, string indexName) =>
        await ScalarAsync<bool>(
            db,
            $$"""
            SELECT EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'Storage'
                  AND indexname = '{{indexName}}'
            );
            """);

    private static async Task<T> ScalarAsync<T>(DbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return (T)value!;
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
