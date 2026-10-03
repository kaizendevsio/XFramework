using Bolt.Protocol.Transport;
using NUnit.Framework;

namespace Bolt.Tests;

public sealed class DatagramPathHysteresisTests
{
    [Test]
    public void APathThatBlipsEveryFewSeconds_IsLeftOnce_AndNotUsedAgainWhileItKeepsBlipping()
    {
        var path = new DatagramPathHysteresis();
        Assert.That(path.Observe(true, 0), Is.True, "the first open needs no proof");
        // Production pattern (12:47-12:49 UTC): bad for about a second every few seconds.
        for (var t = 1_000L; t <= 300_000; t += 500)
            path.Observe(t / 500 % 8 != 0, t);
        Assert.Multiple(() =>
        {
            Assert.That(path.Usable, Is.False, "never 5 s healthy in a row: media stays on the WebSocket");
            Assert.That(path.Switches, Is.EqualTo(2), "onto the path once and off it once, not once per blip");
        });
    }

    [Test]
    public void APathThatComesBackAndFailsAgain_IsGivenUpAfterThreeFlaps()
    {
        var path = new DatagramPathHysteresis();
        path.Observe(true, 0);
        // Bad for a second every 80 s: long enough to come back each time, then it fails again.
        for (var t = 500L; t <= 400_000; t += 500)
            path.Observe(t % 80_000 != 0, t);
        Assert.Multiple(() =>
        {
            Assert.That(path.GivenUp, Is.True);
            Assert.That(path.Usable, Is.False);
            Assert.That(path.Switches, Is.EqualTo(2 * path.Options.MaxFlaps), "opened, then left three times and back twice");
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
        Assert.That(path.Observe(true, 2), Is.True, "after a network change a healthy path is used at once");
    }
}
