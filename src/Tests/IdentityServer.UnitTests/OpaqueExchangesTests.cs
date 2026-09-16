using IdentityServer.Api.Infrastructure;
using NUnit.Framework;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class OpaqueExchangesTests
{
    [Test]
    public void Exchange_BindsTenantRoleAndExpiration_AndCannotReplay()
    {
        var clock = new Clock(); var exchanges = new OpaqueExchanges(clock);
        var entry = new OpaqueExchanges.Entry(Guid.NewGuid(), Guid.NewGuid(), "name", "scope", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "login", "state", default);
        var valid = exchanges.Add(entry);
        Assert.That(exchanges.Take(valid, entry.TenantId, entry.RoleId), Is.Not.Null);
        Assert.That(exchanges.Take(valid, entry.TenantId, entry.RoleId), Is.Null);
        var tenant = exchanges.Add(entry);
        Assert.That(exchanges.Take(tenant, Guid.NewGuid(), entry.RoleId), Is.Null);
        Assert.That(exchanges.Take(tenant, entry.TenantId, entry.RoleId), Is.Null);
        Assert.That(exchanges.Take(exchanges.Add(entry), entry.TenantId, Guid.NewGuid()), Is.Null);
        var expired = exchanges.Add(entry); clock.Now += TimeSpan.FromMinutes(2);
        Assert.That(exchanges.Take(expired, entry.TenantId, entry.RoleId), Is.Null);
    }
    [Test]
    public void ResetGrant_RequiresSameActorAndReauthenticationKind()
    {
        var exchanges = new OpaqueExchanges(TimeProvider.System);
        var entry = new OpaqueExchanges.Entry(Guid.NewGuid(), Guid.NewGuid(), "name", "scope", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "reauth", "", default);
        Assert.That(exchanges.TakeForActor(exchanges.Add(entry), entry.TenantId, Guid.NewGuid()), Is.Null);
        Assert.That(exchanges.TakeForActor(exchanges.Add(entry with { Kind = "login" }), entry.TenantId, entry.CredentialId), Is.Null);
        var id = exchanges.Add(entry);
        Assert.That(exchanges.TakeForActor(id, entry.TenantId, entry.CredentialId), Is.Not.Null);
        Assert.That(exchanges.TakeForActor(id, entry.TenantId, entry.CredentialId), Is.Null);
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
