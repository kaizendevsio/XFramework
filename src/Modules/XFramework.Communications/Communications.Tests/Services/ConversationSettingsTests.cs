using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Tests.Infrastructure;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test]
    public async Task Settings_RequireAdmin_AndReturnFeaturesAndNicknames()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var admin = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant); admin.Role = MessageThreadMemberRoles.Admin;
        var member = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var context = new InMemoryDataContext(); context.Seed(thread, admin, member);
        var service = CreateService(context);
        var request = new UpdateThreadRequest { ThreadId = thread.Id, Features = ConversationFeatures.Typing,
            NicknameMemberId = member.Id, Nickname = "  Teammate  ", Metadata = Metadata(member.CredentialId, tenant) };
        Assert.That((await service.UpdateThreadAsync(request)).StatusCode, Is.EqualTo(403));
        Assert.That(thread.Features, Is.EqualTo(ConversationFeatures.All));
        request.Metadata = Metadata(admin.CredentialId, tenant);
        Assert.That((await service.UpdateThreadAsync(request)).IsSuccess, Is.True);
        var result = await service.GetThreadAsync(new() { Id = thread.Id, Metadata = request.Metadata });
        Assert.Multiple(() => {
            Assert.That(result.Data!.Features, Is.EqualTo(ConversationFeatures.Typing));
            Assert.That(result.Data.CanManage, Is.True);
            Assert.That(result.Data.Members.Single(x => x.Id == member.Id).Alias, Is.EqualTo("Teammate"));
            Assert.That(result.Data.Members.Single(x => x.Id == admin.Id).Role, Is.EqualTo(MessageThreadMemberRoles.Admin));
        });
        request.NicknameMemberId = Guid.NewGuid();
        Assert.That((await service.UpdateThreadAsync(request)).StatusCode, Is.EqualTo(404));
        request.NicknameMemberId = member.Id; request.Nickname = new string('x', 81);
        Assert.That((await service.UpdateThreadAsync(request)).StatusCode, Is.EqualTo(400));
        request.Features = (ConversationFeatures)128;
        Assert.That((await service.UpdateThreadAsync(request)).StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task Settings_DisabledFeaturesRejectDirectApiCalls()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant); thread.Features = ConversationFeatures.None;
        var member = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var context = new InMemoryDataContext(); context.Seed(thread, member);
        var service = CreateService(context); var metadata = Metadata(member.CredentialId, tenant);
        Assert.That((await service.PublishTypingAsync(new() { ThreadId = thread.Id, IsTyping = true, Metadata = metadata })).StatusCode, Is.EqualTo(403));
        Assert.That((await service.MarkMessagesReadAsync(new() { ThreadId = thread.Id, Metadata = metadata })).StatusCode, Is.EqualTo(403));
        Assert.That((await service.CreateMessageReactionAsync(new() { ThreadId = thread.Id, Metadata = metadata })).StatusCode, Is.EqualTo(403));
        foreach (var type in new[] { "image/png", "video/mp4", "audio/mp4" })
            Assert.That((await service.CreateChatAttachmentUploadAsync(new() { ThreadId = thread.Id, FileName = "test", ContentType = type, TotalSizeBytes = 10, Metadata = metadata })).StatusCode, Is.EqualTo(403));
    }

    [TestCase(false, ConversationFeatures.Threads)]
    [TestCase(true, ConversationFeatures.Replies)]
    public async Task Settings_ReplyTypesAreIndependentlyEnforced(bool threadReply, ConversationFeatures enabled)
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant); thread.Features = enabled;
        var member = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var parent = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "Parent");
        var context = new InMemoryDataContext(); context.Seed(thread, member, parent);
        var service = CreateService(context);
        var request = new CreateThreadMessageRequest { ThreadId = thread.Id, ParentMessageId = parent.Id,
            Text = "Reply", IsThreadReply = threadReply, ClientMessageId = Guid.NewGuid(), Metadata = Metadata(member.CredentialId, tenant) };
        Assert.That((await service.CreateThreadMessageAsync(request)).StatusCode, Is.EqualTo(403));
        thread.Features = ConversationFeatures.All;
        var sent = await service.CreateThreadMessageAsync(request);
        Assert.That(sent.IsSuccess, Is.True, sent.Message);
        request.IsThreadReply = !threadReply;
        Assert.That((await service.CreateThreadMessageAsync(request)).StatusCode, Is.EqualTo(409));
    }

    [Test]
    public async Task Members_LastAdminCannotBeRemovedOrDemoted_AnotherAdminCanBePromoted()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var admin = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant); admin.Role = MessageThreadMemberRoles.Admin;
        var member = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var context = new InMemoryDataContext(); context.Seed(thread, admin, member);
        var service = CreateService(context); var metadata = Metadata(admin.CredentialId, tenant);
        Assert.That((await service.RemoveThreadMemberAsync(new() { ThreadId = thread.Id, CredentialId = admin.CredentialId, Metadata = metadata })).StatusCode, Is.EqualTo(400));
        Assert.That((await service.LeaveThreadAsync(new() { ThreadId = thread.Id, Metadata = metadata })).StatusCode, Is.EqualTo(400));
        Assert.That((await service.UpdateThreadMemberRoleAsync(new() { ThreadId = thread.Id, MemberId = admin.Id, Role = "Member", Metadata = metadata })).StatusCode, Is.EqualTo(400));
        Assert.That((await service.UpdateThreadMemberRoleAsync(new() { ThreadId = thread.Id, MemberId = member.Id, Role = "Admin", Metadata = Metadata(member.CredentialId, tenant) })).StatusCode, Is.EqualTo(403));
        metadata = Metadata(admin.CredentialId, tenant);
        Assert.That((await service.UpdateThreadMemberRoleAsync(new() { ThreadId = thread.Id, MemberId = member.Id, Role = "Admin", Metadata = metadata })).IsSuccess, Is.True);
        Assert.That((await service.UpdateThreadMemberRoleAsync(new() { ThreadId = thread.Id, MemberId = admin.Id, Role = "Member", Metadata = metadata })).IsSuccess, Is.True);
        Assert.That((await service.UpdateThreadAsync(new() { ThreadId = thread.Id, Features = ConversationFeatures.None, Metadata = metadata })).StatusCode, Is.EqualTo(403));
    }
}
