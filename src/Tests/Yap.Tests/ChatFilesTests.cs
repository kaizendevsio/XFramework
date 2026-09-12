using Communications.Integration.Clients;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Domain.Shared.Contracts.Requests.Attachments;
using System.Net;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Domain.Shared.Contracts.Responses;
using Storage.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Security;
using Yap.Services;

namespace Yap.Tests;

[TestFixture]
public sealed class ChatFilesTests
{
    [Test]
    public void UploadAsync_DisabledFeature_PreservesForbiddenStatus()
    {
        var fixture = new ChatFixture();
        var actors = new Mock<ICommunicationsChatActorProvider>();
        actors.Setup(a => a.GetCurrentActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunicationsChatActor(fixture.Tenant, fixture.Credential, AccessToken: "actor"));
        fixture.Session.Setup(s => s.CreateAttachmentUploadAsync(It.IsAny<CreateChatAttachmentUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<StorageUploadSessionResponse> { HttpStatusCode = HttpStatusCode.Forbidden });
        var storage = new Mock<IStorageServiceWrapper>(MockBehavior.Strict);
        var service = new ChatFiles(storage.Object, fixture.Client.Object, actors.Object,
            Mock.Of<IActorAccessTokenScope>(), NullLogger<ChatFiles>.Instance);
        var error = Assert.ThrowsAsync<YapApiException>(async () => await service.UploadAsync(new BrowserFile([1]), fixture.Thread, null, default));
        Assert.That(error!.Status, Is.EqualTo(403));
        storage.VerifyNoOtherCalls();
    }

    [Test]
    public void UploadAsync_BeyondTheStagingLimit_DirectsLargeFilesToTheResumablePath()
    {
        var fixture = new ChatFixture();
        var storage = new Mock<IStorageServiceWrapper>(MockBehavior.Strict);
        var service = new ChatFiles(storage.Object, fixture.Client.Object, Mock.Of<ICommunicationsChatActorProvider>(),
            Mock.Of<IActorAccessTokenScope>(), NullLogger<ChatFiles>.Instance);

        Assert.ThrowsAsync<ChatOperationException>(async () =>
            await service.UploadAsync(new OversizeFile(ChatFiles.StagedFileBytes + 1), fixture.Thread, null, default));
        storage.VerifyNoOtherCalls();
    }

    [Test]
    public void BeginAsync_BeyondTheAttachmentCeiling_IsRefusedBeforeAnySession()
    {
        var fixture = new ChatFixture();
        var storage = new Mock<IStorageServiceWrapper>(MockBehavior.Strict);
        var service = new ChatFiles(storage.Object, fixture.Client.Object, Mock.Of<ICommunicationsChatActorProvider>(),
            Mock.Of<IActorAccessTokenScope>(), NullLogger<ChatFiles>.Instance);

        Assert.ThrowsAsync<ChatOperationException>(async () =>
            await service.BeginAsync(fixture.Thread, "huge.mkv", "video/x-matroska", ChatFiles.MaxFileBytes + 1, default));
        storage.VerifyNoOtherCalls();
    }

    [Test]
    public async Task BeginAsync_SplitsAFourGigabyteFileIntoWholeParts()
    {
        var fixture = new ChatFixture();
        var actors = new Mock<ICommunicationsChatActorProvider>();
        actors.Setup(a => a.GetCurrentActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunicationsChatActor(fixture.Tenant, fixture.Credential, AccessToken: "actor"));
        fixture.Session.Setup(s => s.CreateAttachmentUploadAsync(It.IsAny<CreateChatAttachmentUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadSessionResponse
            { Id = Guid.NewGuid(), ChunkSizeBytes = ChatFiles.PreferredChunkBytes }));
        var service = new ChatFiles(Mock.Of<IStorageServiceWrapper>(), fixture.Client.Object, actors.Object,
            Mock.Of<IActorAccessTokenScope>(), NullLogger<ChatFiles>.Instance);

        var ticket = await service.BeginAsync(fixture.Thread, "holiday.mp4", "video/mp4", ChatFiles.MaxFileBytes, default);

        Assert.Multiple(() =>
        {
            Assert.That(ticket.ChunkSizeBytes, Is.EqualTo(ChatFiles.PreferredChunkBytes));
            Assert.That(ticket.TotalParts, Is.EqualTo(512));
            // S3 multipart tops out at 10,000 parts; the negotiated size must stay under it.
            Assert.That(ticket.TotalParts, Is.LessThan(10000));
        });
    }

    private sealed class OversizeFile(long size) : IBrowserFile
    {
        public string Name => "oversize.bin";
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => size;
        public string ContentType => "application/octet-stream";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The size gate must reject before any read.");
    }

    [Test]
    public async Task AttachAsync_RetryAfterCommittedLink_VerifiesExistingFile()
    {
        var fixture = new ChatFixture();
        var messageId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        fixture.Session.Setup(s => s.AttachFileAsync(fixture.Thread, messageId, fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.Conflict });
        fixture.Session.Setup(s => s.GetFilesAsync(fixture.Thread, messageId, 0, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new PaginatedResult<MessageFileResponse>
            { Items = [new MessageFileResponse { StorageFileId = fileId }] }));
        var service = new ChatFiles(Mock.Of<IStorageServiceWrapper>(), fixture.Client.Object,
            Mock.Of<ICommunicationsChatActorProvider>(), Mock.Of<IActorAccessTokenScope>(), NullLogger<ChatFiles>.Instance);

        await service.AttachAsync(fixture.Thread, messageId, fileId, default);

        fixture.Session.Verify(s => s.GetFilesAsync(fixture.Thread, messageId, 0, 100, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Session.Verify(s => s.AttachFileAsync(fixture.Thread, messageId, fileId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(StorageFileStatus.Available)]
    [TestCase(StorageFileStatus.Verifying)]
    public async Task UploadAsync_NegotiatedChunkSize_UsesServerPartsAndCompletes(StorageFileStatus completionStatus)
    {
        var storage = new Mock<IStorageServiceWrapper>();
        var fixture = new ChatFixture();
        var actors = new Mock<ICommunicationsChatActorProvider>();
        var scope = new Mock<IActorAccessTokenScope>();
        var tenant = Guid.NewGuid();
        var thread = Guid.NewGuid();
        actors.Setup(a => a.GetCurrentActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunicationsChatActor(tenant, Guid.NewGuid(), AccessToken: "actor"));
        scope.Setup(s => s.Push("actor")).Returns(Mock.Of<IDisposable>());
        var upload = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        CreateChatAttachmentUploadRequest? creation = null;
        fixture.Session.Setup(s => s.CreateAttachmentUploadAsync(It.IsAny<CreateChatAttachmentUploadRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateChatAttachmentUploadRequest, CancellationToken>((request, _) => creation = request)
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadSessionResponse { Id = upload, ChunkSizeBytes = 5 }));
        var parts = new List<UploadChatStorageFilePartRequest>();
        storage.Setup(s => s.UploadChatStorageFilePart(It.IsAny<UploadChatStorageFilePartRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UploadChatStorageFilePartRequest, CancellationToken>((request, _) => parts.Add(request))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadPartResponse()));
        storage.Setup(s => s.CompleteChatStorageUploadSession(It.IsAny<CompleteChatStorageUploadSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageFileResponse { Id = fileId, Status = completionStatus }));
        storage.Setup(s => s.GetStorageFile(It.Is<GetStorageFileRequest>(r => r.StorageFileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageFileResponse { Id = fileId, Status = StorageFileStatus.Available }));
        var service = new ChatFiles(storage.Object, fixture.Client.Object, actors.Object, scope.Object, NullLogger<ChatFiles>.Instance);
        var result = await service.UploadAsync(new BrowserFile([1,2,3,4,5,6,7]), thread, null, default);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(fileId));
            Assert.That(creation!.ThreadId, Is.EqualTo(thread));
            Assert.That(creation.FileName, Is.EqualTo("test.txt"));
            Assert.That(creation.ContentType, Is.EqualTo("text/plain"));
            Assert.That(creation.TotalSizeBytes, Is.EqualTo(7));
            Assert.That(creation.ChunkSizeBytes, Is.Null, "Storage must choose a provider-compatible default part size.");
            Assert.That(parts.Select(p => p.ChunkBytes.Length), Is.EqualTo(new[] { 5, 2 }));
            Assert.That(parts.Select(p => p.OffsetBytes), Is.EqualTo(new long[] { 0, 5 }));
            Assert.That(parts.Select(p => p.PartNumber), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(parts.SelectMany(p => p.ChunkBytes), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5, 6, 7 }));
            Assert.That(parts.Select(p => p.PartSha256Hash), Is.EqualTo(new[]
            {
                "74F81FE167D99B4CB41D6D0CCDA82278CAEE9F3E2F25D5E5A3936FF3DCEC60D0",
                "4E399D0536E9EB556EA05E7C19F52034FC44DC7EEA2F3B5AF2DA5336CA9C9CF1"
            }));
            Assert.That(parts.All(p => p.UploadSessionId == upload), Is.True);
            Assert.That(parts.All(p => p.Metadata!.RequestedTenantId == tenant), Is.True);
        });
        storage.Verify(s => s.GetStorageFile(It.Is<GetStorageFileRequest>(r => r.StorageFileId == fileId), It.IsAny<CancellationToken>()),
            completionStatus == StorageFileStatus.Verifying ? Times.Once() : Times.Never());
    }

    [Test]
    public async Task UploadAsync_PartRejected_AbortsOwnedSessionWithoutCompleting()
    {
        var fixture = new ChatFixture();
        var uploadId = Guid.NewGuid();
        var storage = new Mock<IStorageServiceWrapper>();
        var actors = new Mock<ICommunicationsChatActorProvider>();
        actors.Setup(a => a.GetCurrentActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunicationsChatActor(fixture.Tenant, fixture.Credential, AccessToken: "actor"));
        fixture.Session.Setup(s => s.CreateAttachmentUploadAsync(It.IsAny<CreateChatAttachmentUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadSessionResponse { Id = uploadId, ChunkSizeBytes = 5 }));
        storage.Setup(s => s.UploadChatStorageFilePart(It.IsAny<UploadChatStorageFilePartRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<StorageUploadPartResponse> { HttpStatusCode = HttpStatusCode.Forbidden });
        storage.Setup(s => s.AbortChatStorageUploadSession(It.IsAny<AbortChatStorageUploadSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        var service = new ChatFiles(storage.Object, fixture.Client.Object, actors.Object,
            Mock.Of<IActorAccessTokenScope>(), NullLogger<ChatFiles>.Instance);

        Assert.ThrowsAsync<YapApiException>(async () =>
            await service.UploadAsync(new BrowserFile([1, 2, 3]), fixture.Thread, null, default));

        storage.Verify(s => s.AbortChatStorageUploadSession(
            It.Is<AbortChatStorageUploadSessionRequest>(r => r.UploadSessionId == uploadId && r.Metadata!.RequestedTenantId == fixture.Tenant),
            CancellationToken.None), Times.Once);
        storage.Verify(s => s.CompleteChatStorageUploadSession(It.IsAny<CompleteChatStorageUploadSessionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetLinksAsync_Attachment_UsesMembershipCheckedLinkId()
    {
        var fixture = new ChatFixture();
        var messageId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var storageId = Guid.NewGuid();
        fixture.Session.Setup(s => s.GetFilesAsync(fixture.Thread, messageId, 0, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new PaginatedResult<MessageFileResponse>
            { Items = [new MessageFileResponse { Id = linkId, StorageFileId = storageId }] }));
        fixture.Session.Setup(s => s.GetAttachmentDownloadUrlAsync(fixture.Thread, messageId, linkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageDownloadUrlResponse { StorageFileId = storageId, Url = "https://files.example.test/attachment" }));
        var storage = new Mock<IStorageServiceWrapper>(MockBehavior.Strict);
        var service = new ChatFiles(storage.Object, fixture.Client.Object,
            Mock.Of<ICommunicationsChatActorProvider>(), Mock.Of<IActorAccessTokenScope>(), NullLogger<ChatFiles>.Instance);

        var links = await service.GetLinksAsync(fixture.Thread, messageId, default);

        Assert.That(links, Is.EqualTo(new[] { new ChatFileLink(linkId, "https://files.example.test/attachment") }));
        fixture.Session.Verify(s => s.GetAttachmentDownloadUrlAsync(fixture.Thread, messageId, linkId, It.IsAny<CancellationToken>()), Times.Once);
        storage.VerifyNoOtherCalls();
    }

    [Test]
    public void UploadAsync_OversizedFile_RejectsBeforeContactingStorage()
    {
        var storage = new Mock<IStorageServiceWrapper>(MockBehavior.Strict);
        var file = new Mock<IBrowserFile>();
        file.SetupGet(f => f.Size).Returns(ChatFiles.MaxFileBytes + 1);
        var service = new ChatFiles(storage.Object, Mock.Of<ICommunicationsChatClient>(), Mock.Of<ICommunicationsChatActorProvider>(), Mock.Of<IActorAccessTokenScope>(), NullLogger<ChatFiles>.Instance);
        Assert.ThrowsAsync<ChatOperationException>(async () => await service.UploadAsync(file.Object, Guid.NewGuid(), null, default));
        storage.VerifyNoOtherCalls();
    }

    private sealed class BrowserFile(byte[] bytes) : IBrowserFile
    {
        public string Name => "test.txt";
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => bytes.Length;
        public string ContentType => "text/plain";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream(bytes);
    }
}
