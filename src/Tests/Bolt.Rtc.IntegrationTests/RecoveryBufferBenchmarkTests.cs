using System.Diagnostics;
using System.Text.Json;
using Bolt.Media.Browser;
using NUnit.Framework;

namespace Bolt.Rtc.IntegrationTests;

/// <summary>Prevent the benchmark's in-page recovery model from silently lagging the shipped implementation.</summary>
public sealed class RecoveryBufferBenchmarkTests
{
    [TestCase(true, 14)]
    [TestCase(true, 200)]
    [TestCase(true, 1000)]
    [TestCase(false, 200)]
    public async Task BenchmarkRecovery_DeterministicLossTrace_MatchesProductionDecisions(bool recover, int rtt)
    {
        var ops = new List<object>();
        uint sequence = 1000;
        var random = new Random(581);
        var delayed = new List<(uint Sequence, byte[] Fragment, long At)>();
        long at = 0;
        // First delta, fragmented keyframes that take longer than the old window, temporal losses, tail loss,
        // replay, sender restart jumps, and enough partial pictures to exercise the bound.
        for (uint frame = 1; frame <= 110; frame++)
        {
            if (frame == 70) sequence += 600;
            var data = new byte[(frame % 23 == 0 ? 200 : 3) * 300 - 100];
            random.NextBytes(data);
            var key = frame % 23 == 0 || frame == 2;
            var layer = (int)(frame % 4 == 0 ? 0 : frame % 4 == 2 ? 1 : 2);
            var parts = VideoFrameFragments.Split(data, frame, frame * 33_000, key, layer, payload: 300,
                orientation: frame % 9 == 0 ? 3 : 0);
            foreach (var part in parts)
            {
                var seq = sequence++;
                at += 2;
                if (random.Next(9) == 0) delayed.Add((seq, part, at + 175));
                else if (random.Next(12) != 0) ops.Add(new { type = "push", sequence = seq, fragment = part, at });
                if (seq % 5 == 0) ops.Add(new { type = "poll", at });
                foreach (var late in delayed.Where(x => x.At <= at).ToArray())
                {
                    ops.Add(new { type = "push", sequence = late.Sequence, fragment = late.Fragment, at });
                    delayed.Remove(late);
                }
            }
            if (frame % 11 == 0) ops.Add(new { type = "decline", sequences = new[] { sequence - 2, sequence - 1 } });
            at += 31;
            ops.Add(new { type = "poll", at });
        }
        for (var time = at; time <= at + 6000; time += 10) ops.Add(new { type = "poll", at = time });
        await CompareAsync(recover, rtt, ops);
    }

    [Test]
    public async Task BenchmarkRecovery_PartialPictureBound_MatchesProductionEviction()
    {
        var ops = new List<object>();
        for (uint frame = 1; frame <= 70; frame++)
        {
            var parts = VideoFrameFragments.Split(new byte[600], frame, frame * 1000, false, payload: 300);
            ops.Add(new { type = "push", sequence = 1000 + frame * 2, fragment = parts[0], at = (long)frame });
        }
        ops.Add(new { type = "push", sequence = 1200u, fragment = VideoFrameFragments.Split(new byte[300], 71, 71000, true)[0], at = 71L });
        ops.Add(new { type = "poll", at = 2000L });
        await CompareAsync(true, 1000, ops);
    }

    [Test]
    public async Task BenchmarkRecovery_SlowlyArrivingKeyframe_DoesNotNackItsUnsentTail()
    {
        var ops = new List<object>();
        var parts = VideoFrameFragments.Split(new byte[200 * 300 - 100], 1, 0, true, payload: 300);
        for (var i = 0; i < parts.Count; i++)
        {
            ops.Add(new { type = "push", sequence = (uint)(1000 + i), fragment = parts[i], at = i * 2L });
            ops.Add(new { type = "poll", at = i * 2L });
        }
        await CompareAsync(true, 14, ops);
    }

    private static async Task CompareAsync(bool recover, int rtt, List<object> ops)
    {
        var buffer = new VideoRecoveryBuffer();
        buffer.Configure(recover, rtt);
        var expected = new List<object>();
        foreach (var operation in ops)
        {
            var op = JsonSerializer.SerializeToElement(operation);
            var ready = new List<VideoFramePayload>();
            var nacks = new List<uint>();
            switch (op.GetProperty("type").GetString())
            {
                case "push":
                    buffer.Push(op.GetProperty("sequence").GetUInt32(), op.GetProperty("fragment").GetBytesFromBase64(), op.GetProperty("at").GetInt64(), ready);
                    break;
                case "poll": buffer.Poll(op.GetProperty("at").GetInt64(), ready, nacks); break;
                case "decline": buffer.Decline(op.GetProperty("sequences").EnumerateArray().Select(x => x.GetUInt32()).ToArray(), ready); break;
            }
            expected.Add(new
            {
                ready = ready.Select(p => new { data = p.Data, timestamp = p.TimestampMicroseconds, keyframe = p.IsKeyframe,
                    discontinuity = p.Discontinuity, frameId = p.FrameId, layer = p.Layer, orientation = p.Orientation }),
                nacks = nacks.Order(),
                stats = new { nacked = buffer.Nacked, recovered = buffer.Recovered, abandoned = buffer.Abandoned,
                    declined = buffer.Declined, skipped = buffer.Skipped, incomplete = buffer.Incomplete },
                window = buffer.RecoveryMs,
            });
        }
        using var node = Process.Start(new ProcessStartInfo("node", [Path.Combine(AppContext.BaseDirectory, "bench", "recovery-parity.mjs")])
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true,
        })!;
        var output = node.StandardOutput.ReadToEndAsync();
        var errors = node.StandardError.ReadToEndAsync();
        await node.StandardInput.WriteAsync(JsonSerializer.Serialize(new { recover, rtt, ops, keyframeSupersedes = RecoveryBenchmarkPolicy.KeyframeSupersedes }));
        node.StandardInput.Close();
        await node.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.That(node.ExitCode, Is.Zero, await errors);
        var actual = JsonSerializer.Deserialize<JsonElement[]>(await output)!;
        Assert.That(actual, Has.Length.EqualTo(expected.Count));
        for (var i = 0; i < actual.Length; i++)
            Assert.That(JsonElement.DeepEquals(actual[i], JsonSerializer.SerializeToElement(expected[i])), Is.True,
                $"recovery decision {i}, recover={recover}, RTT={rtt}: actual={actual[i]}, expected={JsonSerializer.Serialize(expected[i])}");
    }
}

// Baseline snapshots use the same measurement harness and their own production recovery behavior.
internal static class RecoveryBenchmarkPolicy
{
    public static bool KeyframeSupersedes
    {
        get
        {
            var buffer = new VideoRecoveryBuffer();
            buffer.Configure(true, 200);
            var ready = new List<VideoFramePayload>();
            buffer.Push(1000, VideoFrameFragments.Split(new byte[300], 1, 0, true)[0], 0, ready);
            buffer.Push(1001, VideoFrameFragments.Split(new byte[600], 2, 1000, false, payload: 300)[0], 1, ready);
            ready.Clear();
            buffer.Push(1003, VideoFrameFragments.Split(new byte[300], 3, 2000, true)[0], 2, ready);
            return ready.Any(p => p.FrameId == 3);
        }
    }
}
