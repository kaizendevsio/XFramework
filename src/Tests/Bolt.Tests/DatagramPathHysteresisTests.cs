using Bolt.Protocol.Transport;
using NUnit.Framework;

namespace Bolt.Tests;

public sealed class DatagramPathHysteresisTests
{
    [Test]
    public void APathThatFlapsEverySecond_IsLeftAtOnce_ReturnsOnlyAfterTheHoldAndSustainedHealth_ThenIsGivenUp()
    {
        var path = new DatagramPathHysteresis();
        Assert.That(path.Observe(true, 0), Is.True, "the first open needs no proof");
        // Production pattern (12:47-12:49 UTC): bad for about a second every few seconds.
        for (var t = 1_000L; t <= 300_000; t += 500)
            path.Observe(t / 500 % 8 != 0, t);
        Assert.Multiple(() =>
        {
            Assert.That(path.GivenUp, Is.True, "a path that keeps flapping is given up for the call");
            Assert.That(path.Usable, Is.False);
            Assert.That(path.Switches, Is.LessThanOrEqualTo(2 * path.Options.MaxFlaps), "switches are bounded, not one per blip");
        });
    }

    [Test]
    public void AfterABlip_MediaReturnsOnlyAfterTheHold_AndFiveSecondsHealthy()
    {
        var path = new DatagramPathHysteresis();
        path.Observe(true, 0);
        Assert.That(path.Observe(false, 1_000), Is.True);
        Assert.That(path.Usable, Is.False);
        for (var t = 1_500L; t < 11_000; t += 500) Assert.That(path.Observe(true, t), Is.False, $"held at {t} ms");
        // The hold ended at 11 s; healthy since 1.5 s, so it returns now.
        Assert.That(path.Observe(true, 11_000), Is.True);
        // A second blip doubles the hold and needs healthy time again.
        Assert.That(path.Observe(false, 12_000), Is.True);
        Assert.That(path.HoldUntil, Is.EqualTo(32_000));
        path.Observe(true, 30_000);
        Assert.That(path.Observe(true, 32_000), Is.False, "the hold is over but it has only been healthy for 2 s");
        Assert.That(path.Observe(true, 35_000), Is.True);
    }

    [Test]
    public void ANetworkChange_StartsOver()
    {
        var path = new DatagramPathHysteresis(new DatagramHysteresisOptions { MaxFlaps = 1 });
        path.Observe(true, 0);
        path.Observe(false, 1);
        Assert.That(path.GivenUp, Is.True);
        path.Reset();
        Assert.That(path.Observe(true, 2), Is.True);
    }
}
