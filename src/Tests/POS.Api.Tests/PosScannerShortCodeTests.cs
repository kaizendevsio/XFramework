using POS.Api.Features.Scanner.Claim;
using POS.Api.Services;
using POS.Domain.Shared.Contracts.Requests;
using XFramework.TestInfrastructure;

namespace POS.Api.Tests;

[TestFixture]
[Category(TestCategories.POS)]
[Category("Kind:Unit")]
[Category("Area:Scanner")]
public sealed class PosScannerShortCodeTests
{
    private Clock clock = null!;
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
    public void Create_LeadingZeroAndCollision_ProducesUniqueSixDigitsAndIndependentQr()
    {
        var codes = new Queue<string>(["000007", "000007", "000008"]);
        store = new(clock, () => codes.Dequeue());
        var first = Create();
        var second = Create();
        first.PairingCode.Should().Be("000007");
        second.PairingCode.Should().Be("000008");
        first.Challenge.Should().HaveLength(64).And.NotBe(second.Challenge);
        first.PairingCode.Should().NotBe(first.Challenge);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Claim_EitherCode_ConsumesBothAndBindsTheIndependentPhone(bool qr)
    {
        var pairing = Create();
        var claim = store.Claim(phone, qr ? pairing.Challenge : pairing.PairingCode);
        claim.IsSuccess.Should().BeTrue();
        claim.Data!.PairingId.Should().Be(pairing.PairingId);
        store.Claim(phone, pairing.PairingCode).StatusCode.Should().Be(403);
        store.Claim(phone, pairing.Challenge).StatusCode.Should().Be(403);
        store.Status(phone with { SessionId = Guid.NewGuid() }, pairing.PairingId, claim.Data.PhoneKey).StatusCode.Should().Be(403);
    }

    [Test]
    public void Claim_WrongTenantActorOrDesktopSession_DoesNotConsumeShortCode()
    {
        var pairing = Create();
        store.Claim(phone with { TenantId = Guid.NewGuid() }, pairing.PairingCode).StatusCode.Should().Be(403);
        store.Claim(phone with { CredentialId = Guid.NewGuid() }, pairing.PairingCode).StatusCode.Should().Be(403);
        store.Claim(desktop, pairing.PairingCode).StatusCode.Should().Be(403);
        store.Claim(phone, pairing.PairingCode).IsSuccess.Should().BeTrue();
    }

    [Test]
    public void Claim_ExpiredOrRevokedShortCode_IsDenied()
    {
        var revoked = Create();
        store.Revoke(desktop, revoked.PairingId, revoked.DesktopKey).IsSuccess.Should().BeTrue();
        store.Claim(phone, revoked.PairingCode).StatusCode.Should().Be(403);
        var expired = Create();
        for (var i = 0; i < 8; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(15));
            store.Poll(desktop, expired.PairingId, expired.DesktopKey, 0);
        }
        store.Claim(phone, expired.PairingCode).StatusCode.Should().Be(403);
    }

    [Test]
    public void Create_RetiredCodeCollision_FailsBoundedlyInsteadOfReusingCode()
    {
        var calls = 0;
        store = new(clock, () => { calls++; return "000007"; });
        var first = Create();
        store.Revoke(desktop, first.PairingId, first.DesktopKey);
        store.Create(desktop, Guid.NewGuid(), "Replacement").StatusCode.Should().Be(429);
        calls.Should().Be(33);
        store.Claim(phone, first.PairingCode).StatusCode.Should().Be(403);
        clock.Advance(PosScannerPairingStore.PairingTtl);
        Create().PairingCode.Should().Be("000007", "retired reservations have a bounded lifetime");
    }

    [Test]
    public void Claim_ActorBudgetSurvivesSessionTenantAndPairingRotation_WhileQrRemainsUsable()
    {
        var pairing = Create();
        for (var i = 0; i < PosScannerPairingStore.ShortClaimLimit; i++)
            store.Claim(phone with { SessionId = Guid.NewGuid(), TenantId = Guid.NewGuid() }, pairing.PairingCode).StatusCode.Should().Be(403);
        store.Revoke(desktop, pairing.PairingId, pairing.DesktopKey);
        var replacement = Create();
        store.Claim(phone, replacement.PairingCode).StatusCode.Should().Be(429);
        store.Claim(phone, replacement.Challenge).IsSuccess.Should().BeTrue();
        clock.Advance(PosScannerPairingStore.ChallengeTtl);
        store.Claim(phone, Create().PairingCode).IsSuccess.Should().BeTrue();
    }

    [Test]
    public void Claim_GlobalBudgetBoundsDistributedActors()
    {
        for (var i = 0; i < PosScannerPairingStore.GlobalShortClaimLimit; i++)
            store.Claim(phone with { CredentialId = Guid.NewGuid() }, "000007").StatusCode.Should().Be(403);
        store.Claim(phone, Create().PairingCode).StatusCode.Should().Be(429);
    }

    [Test]
    public async Task Claim_ConcurrentShortCode_ClaimsOnceAndSharesAttemptBudget()
    {
        var pairing = Create();
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
            store.Claim(phone with { SessionId = Guid.NewGuid() }, pairing.PairingCode))));
        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Count(r => r.StatusCode == 429).Should().Be(15);
    }

    [TestCase("000007", true)]
    [TestCase("12345", false)]
    [TestCase("1234567", false)]
    [TestCase("12345A", false)]
    [TestCase(" 12345", false)]
    [TestCase("\u0661\u0662\u0663\u0664\u0665\u0666", false)]
    public void Validator_ManualCode_RequiresExactlySixAsciiDigits(string code, bool valid) =>
        new ClaimPosScannerPairingValidator().Validate(new ClaimPosScannerPairingRequest { PairingCode = code }).IsValid.Should().Be(valid);

    [Test]
    public void Validator_QrOrManual_RequiresExactlyOneValidCredential()
    {
        var validator = new ClaimPosScannerPairingValidator();
        validator.Validate(new ClaimPosScannerPairingRequest()).IsValid.Should().BeFalse();
        validator.Validate(new ClaimPosScannerPairingRequest { Challenge = new string('A', 64) }).IsValid.Should().BeTrue();
        validator.Validate(new ClaimPosScannerPairingRequest { Challenge = "000007" }).IsValid.Should().BeFalse();
        validator.Validate(new ClaimPosScannerPairingRequest { PairingCode = "000007", Challenge = new string('A', 64) }).IsValid.Should().BeFalse();
        validator.Validate(new ClaimPosScannerPairingRequest { Challenge = new string('G', 64) }).IsValid.Should().BeFalse();
    }

    private POS.Domain.Shared.Contracts.Responses.PosScannerPairingResponse Create() =>
        store.Create(desktop, Guid.NewGuid(), "Register").Data!;

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan value) => now += value;
    }
}
