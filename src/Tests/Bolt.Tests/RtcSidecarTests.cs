using System.Diagnostics;
using Bolt.Protocol.Transport;
using Bolt.Rtc;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// The real bolt-rtc sidecar (Pion) driven through <see cref="RtcSidecar"/>: two peers negotiate over its local
/// socket and carry messages on the unordered, unretransmitted channel. Peers use loopback candidates only, so
/// nothing listens on the host's network. Needs the sidecar binary (BOLT_RTC_SIDECAR) or a Go toolchain to
/// build it; without either the tests are skipped (CI has Go).
/// </summary>
[CancelAfter(60_000)]
[NonParallelizable]
public sealed class RtcSidecarTests
{
    private static readonly Lazy<string?> Binary = new(LocateOrBuild);

    internal static string RequireBinary()
    {
        if (Binary.Value is { } path) return path;
        Assert.Ignore("The bolt-rtc sidecar is not available (set BOLT_RTC_SIDECAR or install Go).");
        return "";
    }

    private static string? LocateOrBuild()
    {
        if (Environment.GetEnvironmentVariable("BOLT_RTC_SIDECAR") is { Length: > 0 } configured && File.Exists(configured))
            return configured;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XFramework.slnx")))
            directory = directory.Parent;
        if (directory is null) return null;
        var source = Path.Combine(directory.FullName, "src", "Libraries", "Bolt", "Bolt.Rtc", "sidecar");
        var output = Path.Combine(Path.GetTempPath(), "bolt-rtc-test-" + Environment.ProcessId, OperatingSystem.IsWindows() ? "bolt-rtc.exe" : "bolt-rtc");
        try
        {
            var build = Process.Start(new ProcessStartInfo("go", ["build", "-o", output, "."])
            { WorkingDirectory = source, RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false });
            if (build is null) return null;
            build.WaitForExit(300_000);
            return build.ExitCode == 0 && File.Exists(output) ? output : null;
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    private static RtcPeerOptions Loopback(bool relayOnly = false) => new([], relayOnly, RtcDefaults.MaxMessageBytes, 128 * 1024, AllowLoopback: true);

    /// <summary>Offer, answer and trickle between two peers until both channels are open.</summary>
    internal static async Task ConnectAsync(IRtcPeer offerer, IRtcPeer answerer)
    {
        var aOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        offerer.StateChanged += state => { if (state == RtcChannelState.Open) aOpen.TrySetResult(); };
        answerer.StateChanged += state => { if (state == RtcChannelState.Open) bOpen.TrySetResult(); };
        offerer.LocalCandidate += candidate => _ = answerer.AddCandidateAsync(candidate, CancellationToken.None);
        answerer.LocalCandidate += candidate => _ = offerer.AddCandidateAsync(candidate, CancellationToken.None);
        var offer = await offerer.CreateOfferAsync(false, CancellationToken.None);
        var answer = await answerer.AnswerAsync(offer, CancellationToken.None);
        await offerer.SetAnswerAsync(answer, CancellationToken.None);
        await Task.WhenAll(aOpen.Task, bOpen.Task).WaitAsync(TimeSpan.FromSeconds(20));
    }

    [Test]
    public async Task TwoPeers_OpenTheMediaChannel_AndCarryWholeMessagesBothWays()
    {
        await using var sidecar = new RtcSidecar(new RtcSidecarOptions { ExecutablePath = RequireBinary() }, NullLogger<RtcSidecar>.Instance);
        await using var offerer = await sidecar.CreateAsync(RtcPeerRole.Offer, Loopback(), CancellationToken.None);
        await using var answerer = await sidecar.CreateAsync(RtcPeerRole.Answer, Loopback(), CancellationToken.None);
        var received = new System.Collections.Concurrent.ConcurrentQueue<byte[]>();
        answerer.Message += data => received.Enqueue(data.ToArray());
        var back = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        offerer.Message += data => back.TrySetResult(data.ToArray());
        await ConnectAsync(offerer, answerer);

        var big = Enumerable.Range(0, RtcDefaults.MaxMessageBytes).Select(i => (byte)i).ToArray();
        for (var i = 0; i < 20; i++) Assert.That(offerer.TrySend(big), Is.True);
        Assert.That(answerer.TrySend([0x21, 7]), Is.True);
        Assert.That(offerer.TrySend(new byte[RtcDefaults.MaxMessageBytes + 1]), Is.False, "a message that would be split is refused");

        Assert.That(await back.Task.WaitAsync(TimeSpan.FromSeconds(10)), Is.EqualTo(new byte[] { 0x21, 7 }));
        await WaitFor(() => received.Count == 20);
        Assert.That(received.All(x => x.AsSpan().SequenceEqual(big)), Is.True, "every message arrives whole");
        await WaitFor(() => answerer.Path is not null && answerer.CongestionWindow > 0);
        Assert.Multiple(() =>
        {
            Assert.That(answerer.Path!.Local, Is.EqualTo("host"));
            Assert.That(answerer.Path.LocalProtocol, Is.EqualTo("udp"));
            Assert.That(answerer.CongestionWindow, Is.GreaterThanOrEqualTo(128 * 1024), "the congestion-window floor holds");
        });
    }

    [Test]
    public async Task ClosingOnePeer_ClosesTheOtherChannel()
    {
        await using var sidecar = new RtcSidecar(new RtcSidecarOptions { ExecutablePath = RequireBinary() }, NullLogger<RtcSidecar>.Instance);
        var offerer = await sidecar.CreateAsync(RtcPeerRole.Offer, Loopback(), CancellationToken.None);
        await using var answerer = await sidecar.CreateAsync(RtcPeerRole.Answer, Loopback(), CancellationToken.None);
        await ConnectAsync(offerer, answerer);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        answerer.StateChanged += state => { if (state is RtcChannelState.Closed or RtcChannelState.Failed) closed.TrySetResult(); };
        await offerer.DisposeAsync();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.That(answerer.TrySend([0x21]), Is.False);
    }

    [Test]
    public async Task ADeadSidecar_IsRestarted_AndItsPeersReportClosed()
    {
        await using var sidecar = new RtcSidecar(new RtcSidecarOptions { ExecutablePath = RequireBinary(), RestartBackoff = TimeSpan.Zero },
            NullLogger<RtcSidecar>.Instance);
        await using var first = await sidecar.CreateAsync(RtcPeerRole.Offer, Loopback(), CancellationToken.None);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        first.StateChanged += state => { if (state is RtcChannelState.Failed or RtcChannelState.Closed) lost.TrySetResult(); };
        Process.GetProcessById(sidecar.ProcessId!.Value).Kill();
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using var again = await sidecar.CreateAsync(RtcPeerRole.Offer, Loopback(), CancellationToken.None);
        Assert.That(await again.CreateOfferAsync(false, CancellationToken.None), Does.Contain("m=application"));
    }

    [Test]
    public async Task ARelayOnlyPeerWithoutTurn_NeverOpens_AndOffersNoHostAddress()
    {
        await using var sidecar = new RtcSidecar(new RtcSidecarOptions { ExecutablePath = RequireBinary() }, NullLogger<RtcSidecar>.Instance);
        await using var offerer = await sidecar.CreateAsync(RtcPeerRole.Offer, Loopback(relayOnly: true), CancellationToken.None);
        var candidates = new System.Collections.Concurrent.ConcurrentQueue<RtcCandidate>();
        offerer.LocalCandidate += candidates.Enqueue;
        var offer = await offerer.CreateOfferAsync(false, CancellationToken.None);
        await WaitFor(() => candidates.Any(x => x.Candidate == ""));
        Assert.Multiple(() =>
        {
            Assert.That(offer, Does.Not.Contain("typ host"));
            Assert.That(candidates.Where(x => x.Candidate != ""), Is.Empty);
            Assert.That(offerer.State, Is.EqualTo(RtcChannelState.Connecting));
        });
    }

    [Test]
    public void AMissingExecutable_IsReportedAsUnavailable()
    {
        var sidecar = new RtcSidecar(new RtcSidecarOptions { ExecutablePath = Path.Combine(Path.GetTempPath(), "no-such-bolt-rtc") });
        Assert.That(sidecar.IsAvailable, Is.False);
        Assert.ThrowsAsync<FileNotFoundException>(async () => await sidecar.CreateAsync(RtcPeerRole.Offer, Loopback(), CancellationToken.None));
    }

    private static async Task WaitFor(Func<bool> condition, int timeoutMs = 10_000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline) Assert.Fail("Timed out.");
            await Task.Delay(20);
        }
    }
}
