using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Requests.Edit;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Tests.Infrastructure;
using NUnit.Framework;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Responses;
using System.Text.Json;
using Communications.Domain.Shared.Contracts.Requests.Attachments;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    private static string Envelope(char value) => "-----BEGIN PGP MESSAGE-----\n" + new string(value, 100) + "\n-----END PGP MESSAGE-----";

    [Test]
    public async Task EncryptedMessage_PersistsOpaqueContent_RetriesIdentically_RejectsDowngrade()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var member = Member(Guid.NewGuid(), Guid.NewGuid(), actor, tenant);
        var context = new InMemoryDataContext();
        context.Seed(Thread(member.MessageThreadId, tenant), member);
        var device = Guid.NewGuid();
        context.Seed(new IdentityServer.Domain.Shared.Contracts.EncryptionAccount { TenantId = tenant, CredentialId = actor,
            DirectoryRevision = 1, DevicesJson = System.Text.Json.JsonSerializer.Serialize(new[] { new IdentityServer.Domain.Shared.Contracts.Responses.EncryptionDevice { DeviceId = device } }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) });
        var service = CreateService(context);
        var request = new CreateThreadMessageRequest { ThreadId = member.MessageThreadId, ClientMessageId = Guid.NewGuid(),
            Text = EncryptedMessages.Preview, EncryptedEnvelope = Envelope('a'), RecipientCredentialIds = [actor],
            EncryptionSenderDeviceId = device, SenderDirectoryRevision = 1, RecipientDirectoryRevisions = new() { [actor] = 1 }, Metadata = Metadata(actor, tenant) };
        Assert.That((await service.CreateThreadMessageAsync(request)).IsSuccess, Is.True);
        Assert.That((await service.CreateThreadMessageAsync(request)).IsSuccess, Is.True);
        Assert.That(context.Set<Message>(), Has.Count.EqualTo(1));
        Assert.That(context.Set<Message>().Single().Text, Is.EqualTo(EncryptedMessages.Preview));
        Assert.That(context.Set<Message>().Single().EncryptedEnvelope, Is.EqualTo(request.EncryptedEnvelope));
        Assert.That(context.Set<MessageThread>().Single().EncryptionRequired, Is.True);
        request.EncryptedEnvelope = Envelope('b');
        Assert.That((await service.CreateThreadMessageAsync(request)).StatusCode, Is.EqualTo(409));
        request.ClientMessageId = Guid.NewGuid(); request.EncryptedEnvelope = null; request.Text = "plaintext";
        request.EncryptionSenderDeviceId = null; request.SenderDirectoryRevision = null;
        Assert.That((await service.CreateThreadMessageAsync(request)).StatusCode, Is.EqualTo(409));
        Assert.That((await service.EditThreadMessageAsync(new EditThreadMessageRequest { ThreadId = member.MessageThreadId,
            MessageId = context.Set<Message>().Single().Id, Text = "plaintext", Metadata = Metadata(actor, tenant) })).StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task EncryptedMessage_RejectsMissingRecipients_AndPlaintextSideChannels()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var other = Guid.NewGuid();
        var member = Member(Guid.NewGuid(), Guid.NewGuid(), actor, tenant);
        var context = new InMemoryDataContext();
        context.Seed(Thread(member.MessageThreadId, tenant), member, Member(Guid.NewGuid(), member.MessageThreadId, other, tenant));
        var service = CreateService(context);
        var request = new CreateThreadMessageRequest { ThreadId = member.MessageThreadId, ClientMessageId = Guid.NewGuid(),
            Text = EncryptedMessages.Preview, EncryptedEnvelope = Envelope('a'), RecipientCredentialIds = [actor], Metadata = Metadata(actor, tenant) };
        Assert.That((await service.CreateThreadMessageAsync(request)).StatusCode, Is.EqualTo(412));
        request.RecipientCredentialIds.Add(other); request.Text = "secret";
        Assert.That((await service.CreateThreadMessageAsync(request)).StatusCode, Is.EqualTo(400));
        request.Text = EncryptedMessages.Preview; request.MentionedCredentialIds = [other];
        Assert.That((await service.CreateThreadMessageAsync(request)).StatusCode, Is.EqualTo(400));
        request.MentionedCredentialIds.Clear(); request.EncryptedEnvelope = new string('a', EncryptedMessages.MaxEnvelopeLength + 1);
        Assert.That((await service.CreateThreadMessageAsync(request)).StatusCode, Is.EqualTo(400));
        Assert.That(context.Set<Message>(), Is.Empty);
    }

    [Test]
    public async Task EncryptedMessage_RevokedSenderCannotCreateNewMessage_ButAcceptedRetrySurvivesRevocation()
    {
        var (context, request, sender, _) = EncryptionScenario();
        var service = CreateService(context);
        request.Metadata = Metadata(sender.CredentialId, sender.TenantId);
        Assert.That((await service.CreateThreadMessageAsync(request)).IsSuccess, Is.True);
        sender.DirectoryRevision = 2;
        sender.DevicesJson = Devices(request.EncryptionSenderDeviceId!.Value, "revoked");
        Assert.That((await service.CreateThreadMessageAsync(request)).IsSuccess, Is.True,
            "An already accepted immutable ID does not create or re-encrypt anything after revocation.");
        request.ClientMessageId = Guid.NewGuid();
        Assert.That((await service.CreateThreadMessageAsync(request)).StatusCode, Is.EqualTo(412));
        request.SenderDirectoryRevision = 2;
        request.RecipientDirectoryRevisions[sender.CredentialId] = 2;
        Assert.That((await service.CreateThreadMessageAsync(request)).StatusCode, Is.EqualTo(412),
            "Knowing the latest directory revision cannot restore a revoked signing device.");
        Assert.That(context.Set<Message>(), Has.Count.EqualTo(1));
        Assert.That(context.Set<Message>().Single().AcceptedSenderDirectoryRevision, Is.EqualTo(1));
    }

    [Test]
    public async Task EncryptedMessage_ChangedOtherRecipientDirectoryRequiresFreshSnapshot()
    {
        var (context, request, sender, recipient) = EncryptionScenario();
        recipient.DirectoryRevision = 2;
        var service = CreateService(context);
        request.Metadata = Metadata(sender.CredentialId, sender.TenantId);
        Assert.That((await service.CreateThreadMessageAsync(request)).StatusCode, Is.EqualTo(412));
        Assert.That(context.Set<Message>(), Is.Empty);
        request.RecipientDirectoryRevisions[recipient.CredentialId] = 2;
        Assert.That((await service.CreateThreadMessageAsync(request)).IsSuccess, Is.True);
    }

    [Test]
    public async Task EncryptedEdit_StampsCurrentSenderDeviceAndRevision_RejectsOtherAdmin()
    {
        var (context, request, sender, recipient) = EncryptionScenario();
        var service = CreateService(context);
        request.Metadata = Metadata(sender.CredentialId, sender.TenantId);
        Assert.That((await service.CreateThreadMessageAsync(request)).IsSuccess, Is.True);
        var replacementDevice = Guid.NewGuid();
        sender.DirectoryRevision = 2;
        sender.DevicesJson = Devices(replacementDevice);
        var edit = new EditThreadMessageRequest { ThreadId = request.ThreadId, MessageId = request.ClientMessageId!.Value,
            Text = EncryptedMessages.Preview, EncryptedEnvelope = Envelope('b'), EncryptionSenderDeviceId = replacementDevice,
            SenderDirectoryRevision = 2, RecipientDirectoryRevisions = new() { [sender.CredentialId] = 2, [recipient.CredentialId] = 1 },
            Metadata = Metadata(sender.CredentialId, sender.TenantId) };
        Assert.That((await service.EditThreadMessageAsync(edit)).IsSuccess, Is.True);
        var saved = context.Set<Message>().Single();
        Assert.That(saved.EncryptedEnvelope, Is.EqualTo(edit.EncryptedEnvelope));
        Assert.That(saved.EncryptionSenderDeviceId, Is.EqualTo(replacementDevice));
        Assert.That(saved.AcceptedSenderDirectoryRevision, Is.EqualTo(2));
        edit.Metadata = Metadata(recipient.CredentialId, sender.TenantId);
        edit.EncryptedEnvelope = Envelope('c');
        Assert.That((await service.EditThreadMessageAsync(edit)).StatusCode, Is.EqualTo(403), "Conversation admin is not the cryptographic sender.");
        Assert.That(saved.EncryptedEnvelope, Is.EqualTo(Envelope('b')));
        Assert.That(saved.EncryptionSenderDeviceId, Is.EqualTo(replacementDevice));
    }

    private static (InMemoryDataContext Context, CreateThreadMessageRequest Request, EncryptionAccount Sender, EncryptionAccount Recipient) EncryptionScenario()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var peer = Guid.NewGuid(); var thread = Guid.NewGuid(); var device = Guid.NewGuid();
        var context = new InMemoryDataContext();
        var senderMember = Member(Guid.NewGuid(), thread, actor, tenant); senderMember.Role = MessageThreadMemberRoles.Admin;
        var peerMember = Member(Guid.NewGuid(), thread, peer, tenant); peerMember.Role = MessageThreadMemberRoles.Admin;
        var sender = new EncryptionAccount { TenantId = tenant, CredentialId = actor, DirectoryRevision = 1, DevicesJson = Devices(device) };
        var recipient = new EncryptionAccount { TenantId = tenant, CredentialId = peer, DirectoryRevision = 1, DevicesJson = Devices(Guid.NewGuid()) };
        context.Seed(Thread(thread, tenant), senderMember, peerMember, sender, recipient);
        return (context, new CreateThreadMessageRequest { ThreadId = thread, ClientMessageId = Guid.NewGuid(),
            Text = EncryptedMessages.Preview, EncryptedEnvelope = Envelope('a'), RecipientCredentialIds = [actor, peer],
            EncryptionSenderDeviceId = device, SenderDirectoryRevision = 1, RecipientDirectoryRevisions = new() { [actor] = 1, [peer] = 1 },
            Metadata = Metadata(actor, tenant) }, sender, recipient);
    }

    private static string Devices(Guid device, string? revocation = null) => JsonSerializer.Serialize(
        new[] { new EncryptionDevice { DeviceId = device, Revocation = revocation } }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [TestCase("voice.pgp", ConversationFeatures.Voice, 201)]
    [TestCase("voice.pgp", ConversationFeatures.Attachments, 403)]
    [TestCase("attachment.pgp", ConversationFeatures.Voice, 403)]
    [TestCase("attachment.pgp", ConversationFeatures.Attachments, 201)]
    public async Task EncryptedUpload_VoiceMarkerPreservesIndependentConversationFeature(string name, ConversationFeatures enabled, int status)
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var thread = Thread(Guid.NewGuid(), tenant); thread.Features = enabled;
        var context = new InMemoryDataContext(); context.Seed(thread, Member(Guid.NewGuid(), thread.Id, actor, tenant));
        var storage = new TestStorageServiceWrapper(); var service = CreateService(context, storage: storage);
        var result = await service.CreateChatAttachmentUploadAsync(new CreateChatAttachmentUploadRequest
        { ThreadId = thread.Id, FileName = name, ContentType = "application/octet-stream", TotalSizeBytes = 1024,
            Metadata = Metadata(actor, tenant) });
        Assert.That(result.StatusCode, Is.EqualTo(status), result.Message);
        Assert.That(storage.ChatUploadCalls, Is.EqualTo(status == 201 ? 1 : 0));
        if (status == 201) Assert.That(storage.LastChatUpload!.FileName, Is.EqualTo(name));
    }
}
