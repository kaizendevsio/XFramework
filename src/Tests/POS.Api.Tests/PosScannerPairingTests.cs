using POS.Api.Services;
using XFramework.TestInfrastructure;

namespace POS.Api.Tests;

[TestFixture]
[Category(TestCategories.POS)]
public sealed class PosScannerPairingTests
{
    private ManualClock clock = null!;
    private PosScannerPairingStore store = null!;
    private ScannerActor desktop = null!;
    private ScannerActor phone = null!;

    [SetUp]
    public void Setup()
    {
        clock = new();
        store = new(clock);
        desktop = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        phone = desktop with { SessionId = Guid.NewGuid() };
    }

    [Test]
    public void Claim_AnotherActorTenantOrSharedDesktopLogin_IsDeniedWithoutConsumingChallenge()
    {
        var pairing = Create();
        store.Claim(phone with { TenantId = Guid.NewGuid() }, pairing.Challenge).StatusCode.Should().Be(403);
        store.Claim(phone with { CredentialId = Guid.NewGuid() }, pairing.Challenge).StatusCode.Should().Be(403);
        store.Claim(desktop, pairing.Challenge).StatusCode.Should().Be(403);
        store.Claim(phone, pairing.Challenge).IsSuccess.Should().BeTrue();
        store.Claim(phone, pairing.Challenge).StatusCode.Should().Be(403, "a QR is consumed once");
    }

    [Test]
    public void Send_WrongActorTenantSessionOrHandle_CannotQueue()
    {
        var pairing = Create();
        var claimed = store.Claim(phone, pairing.Challenge).Data!;
        foreach (var actor in new[] { desktop, phone with { TenantId = Guid.NewGuid() },
                     phone with { CredentialId = Guid.NewGuid() }, phone with { SessionId = Guid.NewGuid() } })
            store.Send(actor, pairing.PairingId, claimed.PhoneKey, 1, "012345678905").StatusCode.Should().Be(403);
        store.Send(phone, pairing.PairingId, pairing.DesktopKey, 1, "012345678905").StatusCode.Should().Be(403);
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0).Data!.Codes.Should().BeEmpty();
    }

    [Test]
    public void Poll_AnotherDesktopTabCannotConsumeCodes()
    {
        var first = Create();
        var second = Create();
        var claimed = store.Claim(phone, first.Challenge).Data!;
        store.Send(phone, first.PairingId, claimed.PhoneKey, 1, "SKU-1").IsSuccess.Should().BeTrue();
        store.Poll(desktop, first.PairingId, second.DesktopKey, 0).StatusCode.Should().Be(403);
        store.Poll(desktop with { SessionId = Guid.NewGuid() }, first.PairingId, first.DesktopKey, 0).StatusCode.Should().Be(403);
        store.Poll(desktop, second.PairingId, second.DesktopKey, 0).Data!.Codes.Should().BeEmpty();
        store.Poll(desktop, first.PairingId, first.DesktopKey, 0).Data!.Codes.Should().ContainSingle();
    }

    [Test]
    public void Send_ReplaysAreIdempotent_DistinctPhysicalScansOfSameItemBothCount()
    {
        var pairing = Create();
        var claimed = store.Claim(phone, pairing.Challenge).Data!;
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 1, "SKU-1").Data!.Duplicate.Should().BeFalse();
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 1, "SKU-1").Data!.Duplicate.Should().BeTrue();
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 1, "SKU-2").StatusCode.Should().Be(409);
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 3, "SKU-2").StatusCode.Should().Be(409);
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 2, "SKU-1").Data!.Duplicate.Should().BeFalse();
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 3, "SKU-1").Data!.Duplicate.Should().BeFalse();
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 1, "SKU-1").StatusCode.Should().Be(409);
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0).Data!.Codes.Select(c => c.Sequence).Should().Equal(1, 2, 3);
    }

    [Test]
    public void Poll_RequiresAcknowledgementAndRetainsUndeliveredScans()
    {
        var pairing = Create();
        var claimed = store.Claim(phone, pairing.Challenge).Data!;
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 1, "SKU-1");
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 1).StatusCode.Should().Be(409);
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0).Data!.Codes.Should().ContainSingle();
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0).Data!.Codes.Should().ContainSingle();
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 1).Data!.Codes.Should().BeEmpty();
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0).StatusCode.Should().Be(409);
    }

    [Test]
    public void Send_FullQueueAppliesBackpressureWithoutConsumingSequence()
    {
        var pairing = Create();
        var claimed = store.Claim(phone, pairing.Challenge).Data!;
        for (var i = 1; i <= PosScannerPairingStore.QueueLimit; i++)
            store.Send(phone, pairing.PairingId, claimed.PhoneKey, i, $"SKU-{i}").IsSuccess.Should().BeTrue();
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 33, "SKU-33").StatusCode.Should().Be(429);
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0).Data!.Codes.Should().HaveCount(32);
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 32).IsSuccess.Should().BeTrue();
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 33, "SKU-33").IsSuccess.Should().BeTrue();
    }

    [Test]
    public void Revoke_OnlyOwningDesktopCanRevoke_ThenAllPhoneOperationsFail()
    {
        var pairing = Create();
        var claimed = store.Claim(phone, pairing.Challenge).Data!;
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 1, "SKU-1");
        store.Revoke(phone, pairing.PairingId, pairing.DesktopKey).StatusCode.Should().Be(403);
        store.Revoke(desktop, pairing.PairingId, claimed.PhoneKey).StatusCode.Should().Be(403);
        store.Revoke(desktop, pairing.PairingId, pairing.DesktopKey).IsSuccess.Should().BeTrue();
        store.Revoke(desktop, pairing.PairingId, pairing.DesktopKey).IsSuccess.Should().BeTrue();
        store.Status(phone, pairing.PairingId, claimed.PhoneKey).StatusCode.Should().Be(403);
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 2, "SKU-2").StatusCode.Should().Be(403);
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0).StatusCode.Should().Be(403);
    }

    [Test]
    public void Claim_ChallengeExpiryIsEnforcedEvenWithDesktopHeartbeat()
    {
        var pairing = Create();
        for (var i = 0; i < 8; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(15));
            store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0);
        }
        store.Claim(phone, pairing.Challenge).StatusCode.Should().Be(403);
    }

    [Test]
    public void Send_DesktopLeaseExpiresAndCannotBeResurrected()
    {
        var pairing = Create();
        var claimed = store.Claim(phone, pairing.Challenge).Data!;
        clock.Advance(PosScannerPairingStore.DesktopLease);
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 1, "SKU-1").StatusCode.Should().Be(403);
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0).StatusCode.Should().Be(403);
    }

    [Test]
    public void Status_HardExpiryIsNotExtendedByHeartbeats()
    {
        var pairing = Create();
        var claimed = store.Claim(phone, pairing.Challenge).Data!;
        for (var i = 0; i < 120; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(15));
            store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0);
        }
        store.Status(phone, pairing.PairingId, claimed.PhoneKey).StatusCode.Should().Be(403);
    }

    [Test]
    public void Create_ActorQuotaIsBoundedAndExpiredSlotsAreReclaimed()
    {
        for (var i = 0; i < 8; i++) Create();
        store.Create(desktop, Guid.NewGuid(), "Register").StatusCode.Should().Be(429);
        clock.Advance(PosScannerPairingStore.DesktopLease);
        Create().Should().NotBeNull();
    }

    [TestCase("")]
    [TestCase("  ")]
    [TestCase("SKU\n1")]
    public void Send_InvalidCodeDoesNotConsumeSequence(string code)
    {
        var pairing = Create();
        var claimed = store.Claim(phone, pairing.Challenge).Data!;
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 1, code).StatusCode.Should().Be(400);
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 1, new string('X', 257)).StatusCode.Should().Be(400);
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 1, "SKU-1").IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task Send_ConcurrentSameSequence_IsQueuedExactlyOnce()
    {
        var pairing = Create();
        var claimed = store.Claim(phone, pairing.Challenge).Data!;
        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Task.Run(() =>
            store.Send(phone, pairing.PairingId, claimed.PhoneKey, 1, "SKU-1"))));
        results.Should().OnlyContain(result => result.IsSuccess);
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0).Data!.Codes.Should().ContainSingle();
    }

    private POS.Domain.Shared.Contracts.Responses.PosScannerPairingResponse Create() =>
        store.Create(desktop, Guid.NewGuid(), "Register").Data!;

    [Test]
    public void Poll_PaymentPaused_DoesNotDeliverOrAcknowledge_AndBlocksNewSends()
    {
        var pairing = Create();
        var claimed = store.Claim(phone, pairing.Challenge).Data!;
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 1, "SKU-1");
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0).Data!.Codes.Should().ContainSingle();
        var paused = store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 1, pauseDelivery: true);
        paused.Data!.Codes.Should().BeEmpty();
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 2, "SKU-2").StatusCode.Should().Be(423);
        clock.Advance(TimeSpan.FromSeconds(20));
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 1, pauseDelivery: true).IsSuccess.Should().BeTrue();
        clock.Advance(TimeSpan.FromSeconds(20));
        store.Poll(desktop, pairing.PairingId, pairing.DesktopKey, 0).Data!.Codes.Should().ContainSingle();
        store.Send(phone, pairing.PairingId, claimed.PhoneKey, 2, "SKU-2").IsSuccess.Should().BeTrue();
    }
    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan value) => _now += value;
    }
}
