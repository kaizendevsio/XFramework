using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Tests.Infrastructure;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test]
    public async Task CallsHistory_IsPagedAndRestrictedToVisibleCallRecordsInActiveMemberships()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var peer = Guid.NewGuid();
        var thread = Thread(Guid.NewGuid(), tenant);
        var member = Member(Guid.NewGuid(), thread.Id, actor, tenant);
        var context = new InMemoryDataContext();
        context.Seed(thread, member, new MessageDirectThread { Id = Guid.NewGuid(), TenantId = tenant,
            MessageThreadId = thread.Id, FirstCredentialId = actor, SecondCredentialId = peer, IsEnabled = true });
        var first = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "Video call"); first.TemplateType = "CallSummary"; first.CreatedAt = DateTime.UtcNow;
        var second = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "Missed voice call"); second.TemplateType = "CallSummary"; second.CreatedAt = first.CreatedAt.AddMinutes(-1);
        var hidden = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "Voice call"); hidden.TemplateType = "CallSummary";
        context.Seed(first, second, hidden, Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "Video call"),
            new MessageHidden { Id = Guid.NewGuid(), TenantId = tenant, MessageThreadMemberId = member.Id, MessageId = hidden.Id, IsEnabled = true });
        var service = CreateService(context);
        var request = new SearchMessagesRequest { CallsOnly = true, PageSize = 1, Metadata = Metadata(actor, tenant) };
        var result = await service.SearchMessagesAsync(request);
        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TotalCount, Is.EqualTo(2));
        Assert.That(result.Data.Items.Single().MessageId, Is.EqualTo(first.Id));
        Assert.That(result.Data.Items.Single().OtherCredentialId, Is.EqualTo(peer));
        request.PageIndex = 1;
        Assert.That((await service.SearchMessagesAsync(request)).Data!.Items.Single().MessageId, Is.EqualTo(second.Id));
        request.Metadata = Metadata(Guid.NewGuid(), tenant);
        Assert.That((await service.SearchMessagesAsync(request)).Data!.Items, Is.Empty);
        request.Metadata = Metadata(actor, Guid.NewGuid());
        Assert.That((await service.SearchMessagesAsync(request)).Data!.Items, Is.Empty);
        request.Metadata = Metadata(actor, tenant); member.IsEnabled = false;
        Assert.That((await service.SearchMessagesAsync(request)).Data!.Items, Is.Empty);
        member.IsEnabled = true; thread.IsDeleted = true;
        Assert.That((await service.SearchMessagesAsync(request)).Data!.Items, Is.Empty);
    }
}
