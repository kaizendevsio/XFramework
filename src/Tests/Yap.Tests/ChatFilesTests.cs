using Communications.Integration.Clients;
using Communications.Domain.Shared.Contracts.Responses;
using System.Net;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Domain.Shared.Contracts.Responses;
using Storage.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Security;
using Yap.Services;

namespace Yap.Tests;

[TestFixture]
public sealed class ChatFilesTests
{
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

    [Test]
    public async Task UploadAsync_NegotiatedChunkSize_UsesServerPartsAndCompletes()
    {
        var storage = new Mock<IStorageServiceWrapper>();
        var actors = new Mock<ICommunicationsChatActorProvider>();
        var scope = new Mock<IActorAccessTokenScope>();
        var tenant = Guid.NewGuid();
        var thread = Guid.NewGuid();
        actors.Setup(a => a.GetCurrentActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunicationsChatActor(tenant, Guid.NewGuid(), AccessToken: "actor"));
        scope.Setup(s => s.Push("actor")).Returns(Mock.Of<IDisposable>());
        storage.Setup(s => s.EnsureStorageUploadMetadata(It.IsAny<EnsureStorageUploadMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadMetadataResponse { TypeId = Guid.NewGuid(), StorageFileIdentifierId = Guid.NewGuid() }));
        var upload = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        storage.Setup(s => s.CreateStorageUploadSession(It.IsAny<CreateStorageUploadSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadSessionResponse { Id = upload, ChunkSizeBytes = 5 }));
        var parts = new List<UploadStorageFilePartRequest>();
        storage.Setup(s => s.UploadStorageFilePart(It.IsAny<UploadStorageFilePartRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UploadStorageFilePartRequest, CancellationToken>((request, _) => parts.Add(request))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadPartResponse()));
        storage.Setup(s => s.CompleteStorageUploadSession(It.IsAny<CompleteStorageUploadSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageFileResponse { Id = fileId }));
        var service = new ChatFiles(storage.Object, Mock.Of<ICommunicationsChatClient>(), actors.Object, scope.Object, NullLogger<ChatFiles>.Instance);
        var result = await service.UploadAsync(new BrowserFile([1,2,3,4,5,6,7]), thread, null, default);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(fileId));
            Assert.That(parts.Select(p => p.ChunkBytes.Length), Is.EqualTo(new[] { 5, 2 }));
            Assert.That(parts.Select(p => p.OffsetBytes), Is.EqualTo(new long[] { 0, 5 }));
            Assert.That(parts.All(p => p.Metadata!.RequestedTenantId == tenant), Is.True);
        });
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
