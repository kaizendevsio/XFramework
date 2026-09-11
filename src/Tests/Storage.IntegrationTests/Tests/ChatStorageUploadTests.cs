using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Storage.Api.Services;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Domain.Shared.Contracts.Responses;
using Storage.IntegrationTests.Infrastructure;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;
using XFramework.Core.Services.FeatureGates;

namespace Storage.IntegrationTests;

[TestFixture]
public sealed class ChatStorageUploadTests : StorageIntegrationTestBase
{
    [TestCase(1)]
    [TestCase(2)]
    public async Task ChatUpload_RegularActorOwnsSingleAndMultipartUploads(int parts)
    {
        var owner = Guid.NewGuid();
        var thread = Guid.NewGuid();
        var bytes = Enumerable.Range(0, 50).Select(i => (byte)i).ToArray();
        var session = await CreateAsync(owner, thread, bytes, parts);
        using var actor = ActorScope(owner);
        for (var part = 0; part < parts; part++)
        {
            var chunk = bytes.Skip(part * (50 / parts)).Take(50 / parts).ToArray();
            var result = await ServiceWrapper.UploadChatStorageFilePart(new UploadChatStorageFilePartRequest
            {
                Metadata = CreateMetadata(), UploadSessionId = session.Id, PartNumber = part + 1,
                OffsetBytes = part * chunk.Length, ChunkBytes = chunk, PartSha256Hash = Sha(chunk)
            });
            result.IsSuccess.Should().BeTrue(result.Message);
        }
        var complete = await ServiceWrapper.CompleteChatStorageUploadSession(new CompleteChatStorageUploadSessionRequest
        {
            Metadata = CreateMetadata(), UploadSessionId = session.Id, ExpectedSha256Hash = Sha(bytes)
        });
        complete.IsSuccess.Should().BeTrue(complete.Message);
        if (parts > 1)
        {
            complete.Response!.Status.Should().Be(StorageFileStatus.Verifying);
            await using var maintenanceDb = CreateDbContext();
            var maintenance = new StorageMaintenanceService(maintenanceDb,
                new IntegrationStorageProviderFactory(StorageIntegrationTestFixture.Provider),
                Options.Create(new StorageOptions { MaintenanceBatchSize = 20 }), TimeProvider.System,
                NullLogger<StorageMaintenanceService>.Instance);
            (await maintenance.RunBatchAsync()).VerifiedFiles.Should().BeGreaterThanOrEqualTo(1);
        }

        await using var db = CreateDbContext();
        var file = await db.Set<StorageFile>().SingleAsync(x => x.Id == session.StorageFileId);
        file.UploadedByCredentialId.Should().Be(owner);
        file.UploadPurpose.Should().Be(StorageUploadPurposes.ChatAttachment);
        file.Identifier.Should().Be(thread);
        file.Name.Should().Be("live-chat-attachment.txt");
        file.ContentType.Should().Be("text/plain");
        file.Status.Should().Be(StorageFileStatus.Available);
        file.Visibility.Should().Be(StorageFileVisibility.Private);

        // Even the uploader cannot bypass Communications link/membership checks for downloads.
        var genericDownload = await ServiceWrapper.GetStorageDownloadUrl(new GetStorageDownloadUrlRequest
        {
            Metadata = CreateMetadata(), StorageFileId = file.Id
        });
        genericDownload.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        var portalDownload = await ServiceWrapper.GetChatStorageDownloadUrl(new GetChatStorageDownloadUrlRequest
        {
            Metadata = CreateMetadata(), StorageFileId = file.Id, ThreadId = thread
        });
        portalDownload.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        var portalValidation = await ServiceWrapper.ValidateChatStorageFileReference(new ValidateChatStorageFileReferenceRequest
        {
            Metadata = CreateMetadata(), StorageFileId = file.Id, ThreadId = thread
        });
        portalValidation.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);

        var service = Service(db, owner);
        var valid = await service.ValidateChatFileReferenceAsync(new ValidateChatStorageFileReferenceRequest
        {
            Metadata = CreateMetadata(), StorageFileId = file.Id, ThreadId = thread
        });
        valid.IsSuccess.Should().BeTrue(valid.Message);
        valid.Data!.IsValid.Should().BeTrue();
        // Communications may request a URL for another authorized group member.
        var download = await Service(db, Guid.NewGuid()).GetChatDownloadUrlAsync(new GetChatStorageDownloadUrlRequest
        {
            Metadata = CreateMetadata(), StorageFileId = file.Id, ThreadId = thread
        });
        download.IsSuccess.Should().BeTrue(download.Message);
        download.Data!.IsPublic.Should().BeFalse();
        download.Data.Url.Should().NotBeNullOrEmpty();
    }

    [TestCase("otheractor")]
    [TestCase("wrongtenant")]
    [TestCase("missingcapability")]
    public async Task ChatUpload_NonOwnerOrMissingPermission_CannotMutateSession(string scenario)
    {
        var owner = Guid.NewGuid();
        var bytes = new byte[] { 1, 2, 3, 4 };
        var session = await CreateAsync(owner, Guid.NewGuid(), bytes, 1);
        var caller = scenario == "otheractor" ? Guid.NewGuid() : owner;
        if (caller != owner) await SeedRegularActorAsync(caller);
        using var actor = ActorScope(caller,
            scenario == "wrongtenant" ? Guid.NewGuid() : null, scenario != "missingcapability");
        var uploaded = await ServiceWrapper.UploadChatStorageFilePart(new UploadChatStorageFilePartRequest
        {
            Metadata = CreateMetadata(), UploadSessionId = session.Id, PartNumber = 1,
            OffsetBytes = 0, ChunkBytes = bytes, PartSha256Hash = Sha(bytes)
        });
        var completed = await ServiceWrapper.CompleteChatStorageUploadSession(new CompleteChatStorageUploadSessionRequest
        {
            Metadata = CreateMetadata(), UploadSessionId = session.Id
        });
        var aborted = await ServiceWrapper.AbortChatStorageUploadSession(new AbortChatStorageUploadSessionRequest
        {
            Metadata = CreateMetadata(), UploadSessionId = session.Id
        });
        uploaded.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        completed.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        aborted.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        StorageIntegrationTestFixture.Provider.UploadPartCount.Should().Be(0);
        StorageIntegrationTestFixture.Provider.CompleteUploadCount.Should().Be(0);
        StorageIntegrationTestFixture.Provider.AbortUploadCount.Should().Be(0);

        using var ownerScope = ActorScope(owner);
        (await ServiceWrapper.AbortChatStorageUploadSession(new AbortChatStorageUploadSessionRequest
            { Metadata = CreateMetadata(), UploadSessionId = session.Id })).IsSuccess.Should().BeTrue();
    }

    [TestCase("otheractor")]
    [TestCase("wrongthread")]
    [TestCase("wrongtenant")]
    [TestCase("wrongpurpose")]
    public async Task ChatReference_RequiresTrustedUploaderTenantThreadAndPurpose(string scenario)
    {
        var owner = Guid.NewGuid();
        var thread = Guid.NewGuid();
        var session = await CreateAsync(owner, thread, [1, 2, 3, 4], 1);
        await using var db = CreateDbContext();
        if (scenario == "wrongpurpose")
        {
            var file = await db.Set<StorageFile>().SingleAsync(x => x.Id == session.StorageFileId);
            file.UploadPurpose = null;
            await db.SaveChangesAsync();
        }
        var result = await Service(db, scenario == "otheractor" ? Guid.NewGuid() : owner,
            scenario == "wrongtenant" ? Guid.NewGuid() : null).ValidateChatFileReferenceAsync(new ValidateChatStorageFileReferenceRequest
        {
            Metadata = CreateMetadata(), StorageFileId = session.StorageFileId,
            ThreadId = scenario == "wrongthread" ? Guid.NewGuid() : thread
        });
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ChatUpload_DisabledStorageFeature_DeniesOwnerMutation()
    {
        var owner = Guid.NewGuid();
        var session = await CreateAsync(owner, Guid.NewGuid(), [1, 2, 3, 4], 1);
        using var actor = ActorScope(owner);
        await using var db = CreateDbContext();
        var feature = await db.Set<TenantModuleFeature>().SingleAsync(x =>
            x.TenantId == StorageIntegrationTestFixture.TestTenantId &&
            x.ModuleKey == TenantModuleFeatureKeys.Storage && x.SubFeatureKey == string.Empty);
        try
        {
            feature.IsEnabled = false;
            await db.SaveChangesAsync();
            StorageIntegrationTestFixture.InvalidateStorageFeature(StorageIntegrationTestFixture.TestTenantId);
            var result = await ServiceWrapper.AbortChatStorageUploadSession(new AbortChatStorageUploadSessionRequest
                { Metadata = CreateMetadata(), UploadSessionId = session.Id });
            result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
            StorageIntegrationTestFixture.Provider.AbortUploadCount.Should().Be(0);
        }
        finally
        {
            feature.IsEnabled = true;
            await db.SaveChangesAsync();
            StorageIntegrationTestFixture.InvalidateStorageFeature(StorageIntegrationTestFixture.TestTenantId);
        }
    }

    [Test]
    public async Task ChatUpload_DirectClientCannotProvisionAndGenericMetadataStaysAdminOnly()
    {
        var caller = Guid.NewGuid();
        await SeedRegularActorAsync(caller);
        using var actor = ActorScope(caller);
        var direct = await ServiceWrapper.CreateChatStorageUploadSession(new CreateChatStorageUploadSessionRequest
        {
            Metadata = CreateMetadata(), ThreadId = Guid.NewGuid(), FileName = "test.txt",
            ContentType = "text/plain", TotalSizeBytes = 4
        });
        direct.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        var metadata = await ServiceWrapper.EnsureStorageUploadMetadata(new EnsureStorageUploadMetadataRequest
        {
            Metadata = CreateMetadata(), ContentType = "text/plain", IdentifierName = "arbitrary",
            IdentifierGroupName = "arbitrary"
        });
        metadata.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<StorageUploadSessionResponse> CreateAsync(Guid owner, Guid thread, byte[] bytes, int parts)
    {
        await SeedRegularActorAsync(owner);
        await using var db = CreateDbContext();
        var result = await Service(db, owner).CreateChatUploadSessionAsync(new CreateChatStorageUploadSessionRequest
        {
            Metadata = CreateMetadata(), ThreadId = thread, FileName = "live-chat-attachment.txt",
            ContentType = "text/plain", TotalSizeBytes = bytes.Length,
            ChunkSizeBytes = bytes.Length / parts, ExpectedSha256Hash = Sha(bytes)
        });
        result.IsSuccess.Should().BeTrue(result.Message);
        return result.Data!;
    }

    private static async Task SeedRegularActorAsync(Guid credentialId)
    {
        await using var db = CreateDbContext();
        var tenant = StorageIntegrationTestFixture.TestTenantId;
        var identityId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var roleTypeId = Guid.NewGuid();
        db.AddRange(
            new IdentityInformation { Id = identityId, TenantId = tenant, IdentityName = "Chat member", IsEnabled = true },
            new IdentityCredential { Id = credentialId, TenantId = tenant, IdentityInfoId = identityId,
                UserName = $"chat-{credentialId:N}", IsEnabled = true },
            new IdentityRoleTypeGroup { Id = groupId, TenantId = tenant, Name = $"Chat {groupId:N}",
                Description = "Ordinary chat test users", IsEnabled = true },
            new IdentityRoleType { Id = roleTypeId, TenantId = tenant, GroupId = groupId,
                Name = $"Member {roleTypeId:N}", IsEnabled = true },
            new IdentityRole { Id = Guid.NewGuid(), TenantId = tenant, CredentialId = credentialId,
                TypeId = roleTypeId, RoleExpiration = DateTime.UtcNow.AddDays(1), IsEnabled = true });
        foreach (var capability in new[] { IdentityAuthorizationConstants.View, IdentityAuthorizationConstants.Create })
            db.Add(new IdentityRoleTypeFeaturePermission
            {
                Id = Guid.NewGuid(), TenantId = tenant, RoleTypeId = roleTypeId,
                ModuleKey = TenantModuleFeatureKeys.Storage, SubFeatureKey = string.Empty,
                CapabilityKey = capability, Effect = RoleCapabilityPermissionEffect.Allow, IsEnabled = true
            });
        await db.SaveChangesAsync();
        var permissions = new TenantCredentialCapabilityService(db, NullLogger<TenantCredentialCapabilityService>.Instance);
        (await permissions.IsAllowedAsync(tenant, credentialId, TenantModuleFeatureKeys.Storage, null,
            IdentityAuthorizationConstants.Create)).Data.Should().BeTrue();
        (await permissions.IsAllowedAsync(tenant, credentialId, TenantModuleFeatureKeys.Storage, null,
            IdentityAuthorizationConstants.Manage)).Data.Should().BeFalse();
    }

    private static StorageService Service(XFramework.Domain.Contexts.AppDbContext db, Guid owner, Guid? tenant = null) =>
        new(db, new IntegrationStorageProviderFactory(StorageIntegrationTestFixture.Provider),
            Options.Create(new StorageOptions
            {
                EnforceProviderLimits = false, ProviderProfileName = "integration", BucketPrefix = "xframework-test",
                S3 = new() { Endpoint = "http://storage-provider.integration", Region = "us-east-1",
                    PublicBaseUrl = "https://public.storage.integration" }
            }),
            new ContextAccessor(new TrustedInvocationContext(
                new TrustedActorIdentity(owner, Guid.NewGuid(), tenant ?? StorageIntegrationTestFixture.TestTenantId,
                    Guid.NewGuid(), new HashSet<string>(), new HashSet<string> { StorageAuthorizationCapabilities.Create, StorageAuthorizationCapabilities.View },
                    "test", DateTimeOffset.UtcNow.AddMinutes(5)),
                new TrustedServiceIdentity(XFrameworkServiceNames.Communications, XFrameworkServiceNames.Storage,
                    new HashSet<string> { XFrameworkServiceScopes.StorageRead, XFrameworkServiceScopes.StorageWrite }, "test"),
                tenant ?? StorageIntegrationTestFixture.TestTenantId, null, Guid.NewGuid())),
            NullLogger<StorageService>.Instance);

    private static IDisposable ActorScope(Guid owner, Guid? tenant = null, bool canCreate = true) =>
        TestInvocationActorTokenScope.Push(TestInvocationIdentityExtensions.CreateTestActorToken(
            tenant ?? StorageIntegrationTestFixture.TestTenantId, owner, Guid.NewGuid(), Guid.NewGuid(), [],
            canCreate ? [StorageAuthorizationCapabilities.Create, StorageAuthorizationCapabilities.View] : [StorageAuthorizationCapabilities.View]));

    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private sealed class ContextAccessor(TrustedInvocationContext context) : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext? Current => context;
    }
}
