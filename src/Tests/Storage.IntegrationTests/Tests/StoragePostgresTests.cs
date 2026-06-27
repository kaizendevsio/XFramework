using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
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

        tableCount.Should().Be(8);
        retentionIndexExists.Should().BeTrue();
        providerDefaultIndexExists.Should().BeTrue();
        bucketIndexExists.Should().BeTrue();
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

    private static async Task<StorageUploadSessionResponse> CreateCompletedSessionAsync(RequestMetadata metadata)
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var session = await CreateUploadSessionAsync(metadata, expectedSha256Hash: Sha256(bytes));
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
        string? expectedSha256Hash = null)
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
            ExpectedSha256Hash = expectedSha256Hash
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        return result.Response!;
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
