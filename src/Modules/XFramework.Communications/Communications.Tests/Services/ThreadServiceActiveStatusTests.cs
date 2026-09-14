using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Tests.Infrastructure;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test]
    public async Task ActiveStatusPreference_OnlyUpdatesCallersMembership_AndSurvivesReadback()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var thread = Guid.NewGuid();
        var own = Member(Guid.NewGuid(), thread, actor, tenant);
        var other = Member(Guid.NewGuid(), thread, Guid.NewGuid(), tenant);
        var context = new InMemoryDataContext(); context.Seed(Thread(thread, tenant), own, other);
        var service = CreateService(context);
        var request = new SetThreadActiveStatusRequest { ThreadId = thread, ShareActiveStatus = false, Metadata = Metadata(actor, tenant) };
        Assert.That((await service.SetThreadActiveStatusAsync(request)).IsSuccess, Is.True);
        Assert.That(own.HideActiveStatus, Is.True);
        Assert.That(other.HideActiveStatus, Is.False);
        var readback = await service.GetThreadAsync(new GetThreadRequest { Id = thread, Metadata = Metadata(actor, tenant) });
        Assert.That(readback.Data!.Members.Single(m => m.CredentialId == actor).HideActiveStatus, Is.True);
        request.ShareActiveStatus = true;
        Assert.That((await service.SetThreadActiveStatusAsync(request)).IsSuccess, Is.True);
        Assert.That(own.HideActiveStatus, Is.False);
    }

    [Test]
    public async Task ActiveStatusPreference_RejectsRemovedMemberAndOtherTenant()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var thread = Guid.NewGuid();
        var own = Member(Guid.NewGuid(), thread, actor, tenant);
        var context = new InMemoryDataContext(); context.Seed(Thread(thread, tenant), own);
        var service = CreateService(context);
        Assert.That((await service.SetThreadActiveStatusAsync(new() { ThreadId = thread, Metadata = Metadata(actor, Guid.NewGuid()) })).StatusCode, Is.EqualTo(403));
        own.IsEnabled = false;
        Assert.That((await service.SetThreadActiveStatusAsync(new() { ThreadId = thread, Metadata = Metadata(actor, tenant) })).StatusCode, Is.EqualTo(403));
        Assert.That(own.HideActiveStatus, Is.False);
    }
}
