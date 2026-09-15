using Communications.Api.Services;
using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [TestCase(null, 180)]
    [TestCase("60", 60)]
    [TestCase("12", 12)]
    [TestCase("0", 0)]
    public async Task MessageRatePolicy_DefaultSupportsTwoPerSecond_StoredTenantLimitsRemainAuthoritative(string? stored, int expected)
    {
        var tenant = Guid.NewGuid(); var context = new InMemoryDataContext();
        // Another tenant's configuration must not change this tenant's default.
        context.Seed(PolicySetting(Guid.NewGuid(), "RateLimits.MessageCreatePerMinute", "1"));
        if (stored is not null) context.Seed(PolicySetting(tenant, "RateLimits.MessageCreatePerMinute", stored));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var policy = await new CommunicationsPolicyService(context, cache).GetPolicyAsync(tenant);
        Assert.That(policy.MessageCreatePerMinute, Is.EqualTo(expected));
        if (stored is null)
        {
            var limiter = new CommunicationsActionRateLimiter(); var actor = Guid.NewGuid();
            for (var i = 0; i < 180; i++)
                Assert.That(limiter.Check(tenant, actor, CommunicationsRateLimitActions.MessageCreate, policy.MessageCreatePerMinute).IsSuccess, Is.True);
            Assert.That(limiter.Check(tenant, actor, CommunicationsRateLimitActions.MessageCreate, policy.MessageCreatePerMinute).StatusCode, Is.EqualTo(429));
        }
    }

    [Test]
    public async Task CreateMessage_AcceptedRetriesDoNotConsumePermits_ButNewMessagesRemainLimitedAndRetriesAuthorized()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var member = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var context = new InMemoryDataContext(); context.Seed(thread, member,
            PolicySetting(tenant, "RateLimits.MessageCreatePerMinute", "2"));
        var service = CreateService(context);
        var request = new CreateThreadMessageRequest { ThreadId = thread.Id, ClientMessageId = Guid.NewGuid(),
            Text = "identical retry", Metadata = Metadata(member.CredentialId, tenant) };
        Assert.That((await service.CreateThreadMessageAsync(request)).IsSuccess, Is.True);
        for (var i = 0; i < 5; i++)
            Assert.That((await service.CreateThreadMessageAsync(request)).IsSuccess, Is.True);
        var second = request with { ClientMessageId = Guid.NewGuid(), Text = "second" };
        Assert.That((await service.CreateThreadMessageAsync(second)).IsSuccess, Is.True, "Retries must leave the second new-message permit available.");
        Assert.That((await service.CreateThreadMessageAsync(request with { ClientMessageId = Guid.NewGuid() })).StatusCode, Is.EqualTo(429));
        Assert.That((await service.CreateThreadMessageAsync(request)).IsSuccess, Is.True, "A lost receipt can be retried even when the new-message window is full.");
        Assert.That((await service.CreateThreadMessageAsync(request with { Text = "changed" })).StatusCode, Is.EqualTo(409));
        var otherMember = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant); context.Seed(otherMember);
        Assert.That((await service.CreateThreadMessageAsync(request with { Metadata = Metadata(otherMember.CredentialId, tenant) })).StatusCode, Is.EqualTo(409));
        member.IsEnabled = false;
        request.Metadata = Metadata(member.CredentialId, tenant);
        Assert.That((await service.CreateThreadMessageAsync(request)).StatusCode, Is.EqualTo(403));
        Assert.That(context.Set<Message>(), Has.Count.EqualTo(2));
        Assert.That(context.Set<MessageOutboxEvent>(), Has.Count.EqualTo(2));
    }
}
