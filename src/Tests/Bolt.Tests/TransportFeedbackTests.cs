using Bolt.Media.Congestion;
using Bolt.Protocol;
using Bolt.Server;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// Per-message delivery feedback on the uplink of a datagram path: the wire format, the relay's recorder, the sender's
/// delay-gradient, loss and throughput estimate, and how the rate controller reads it without counting the uplink twice.
/// </summary>
public sealed class TransportFeedbackTests
{
    [Test]
    public void TheReport_RoundTripsArrivalsAndLosses_InAboutABytePerMessage()
    {
        // 250 µs resolution; a 1 s gap and an out-of-order arrival take the escape form.
        long[] arrivals = [1_000_000, 1_000_250, -1, 1_020_000, 2_020_000, 2_019_500, -1, -1, 2_030_000];
        var report = TransportFeedbackCodec.Write(65_530, arrivals);
        var decoded = new List<long>();
        Assert.That(TransportFeedbackCodec.TryRead(report, out var first, decoded), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo((ushort)65_530));
            Assert.That(decoded, Is.EqualTo(arrivals));
            Assert.That(report.Length, Is.LessThanOrEqualTo(TransportFeedbackCodec.HeaderSize + 2 + 6 + 2 * 3));
        });

        var steady = Enumerable.Range(0, 100).Select(i => 5_000_000L + i * 10_000).ToArray();
        Assert.That(TransportFeedbackCodec.Write(1, steady).Length, Is.EqualTo(TransportFeedbackCodec.HeaderSize + 13 + 100),
            "100 messages 10 ms apart: a bitmap bit and one byte each");
        Assert.That(TransportFeedbackCodec.TryRead(report[..^1], out _, decoded), Is.False, "a truncated report is refused");
    }

    [Test]
    public void TheStamp_WrapsAnyMessage_AndCannotBeNested()
    {
        var inner = new byte[] { (byte)FrameType.MediaFrame, 1, 2, 3 };
        var stamped = new byte[TransportSequenceCodec.HeaderSize + inner.Length];
        TransportSequenceCodec.Write(stamped, 513, inner);
        Assert.That(TransportSequenceCodec.TryRead(stamped, out var sequence, out var message), Is.True);
        Assert.That((sequence, message.ToArray()), Is.EqualTo(((ushort)513, inner)));
        var twice = new byte[TransportSequenceCodec.HeaderSize + stamped.Length];
        TransportSequenceCodec.Write(twice, 514, stamped);
        Assert.That(TransportSequenceCodec.TryRead(twice, out _, out _), Is.False);
    }

    [Test]
    public void TheRelaysRecorder_ReportsEveryNumberOnce_AndMissingOnesAsLost()
    {
        var recorder = new TransportFeedbackRecorder();
        Assert.That(recorder.TryBuild(), Is.Null, "nothing arrived, nothing to say");
        recorder.Record(65_534, 100);
        recorder.Record(0, 300);      // across the wrap
        recorder.Record(65_535, 200); // out of order, but before the report
        recorder.Record(2, 500);      // 1 lost
        var decoded = new List<long>();
        Assert.That(TransportFeedbackCodec.TryRead(recorder.TryBuild()!, out var first, decoded), Is.True);
        Assert.That(first, Is.EqualTo((ushort)65_534));
        Assert.That(decoded, Is.EqualTo(new long[] { 0, 0, 250, -1, 500 }), "250 µs resolution; 65535 arrived out of order");

        recorder.Record(1, 900);      // too late: already reported lost
        Assert.That(recorder.TryBuild(), Is.Null);
        recorder.Record(3, 1_000);
        Assert.That(TransportFeedbackCodec.TryRead(recorder.TryBuild()!, out first, decoded), Is.True);
        Assert.That((first, decoded.Count), Is.EqualTo(((ushort)3, 1)));
    }

    /// <summary>Send <paramref name="count"/> messages of <paramref name="bytes"/> every <paramref name="spacingUs"/>, through a link of <paramref name="capacityKbps"/>.</summary>
    private static TransportSignal Run(TransportFeedbackEstimator estimator, int count, int bytes, long spacingUs, int capacityKbps,
        double loss = 0, int baseDelayMs = 40, ushort start = 0)
    {
        var random = new Random(7);
        long linkFreeAt = 0, sent = 1_000_000;
        var arrivals = new List<long>();
        ushort first = start;
        var now = 0L;
        for (var i = 0; i < count; i++)
        {
            var sequence = unchecked((ushort)(start + i));
            estimator.OnSent(sequence, bytes, sent);
            var serialization = bytes * 8_000L / capacityKbps; // µs
            linkFreeAt = Math.Max(linkFreeAt, sent) + serialization;
            arrivals.Add(random.NextDouble() < loss ? -1 : linkFreeAt + baseDelayMs * 1000 + 3_000_000);
            sent += spacingUs;
            if (arrivals.Count == 10)
            {
                now = sent / 1000;
                estimator.OnFeedback(first, arrivals, now);
                first = unchecked((ushort)(sequence + 1));
                arrivals.Clear();
            }
        }
        return estimator.Signal!.Value;
    }

    [Test]
    public void AnUplinkWithRoom_ShowsNoQueue_NoLoss_AndWhatWasSent()
    {
        var signal = Run(new TransportFeedbackEstimator(), count: 500, bytes: 1000, spacingUs: 10_000, capacityKbps: 2_000);
        Assert.Multiple(() =>
        {
            Assert.That(signal.QueueDelayMs, Is.LessThan(10));
            Assert.That(signal.TrendMsPerSecond, Is.InRange(-20, 20));
            Assert.That(signal.LossFraction, Is.Zero);
            Assert.That(signal.DeliveredKbps, Is.InRange(760, 840), "800 kbps offered, 800 delivered");
        });
    }

    [Test]
    public void AnOverloadedUplink_ShowsAGrowingQueue_AndDeliversItsCapacity()
    {
        // 800 kbps offered into 500 kbps.
        var signal = Run(new TransportFeedbackEstimator(), count: 300, bytes: 1000, spacingUs: 10_000, capacityKbps: 500);
        Assert.Multiple(() =>
        {
            Assert.That(signal.QueueDelayMs, Is.GreaterThan(500));
            Assert.That(signal.TrendMsPerSecond, Is.GreaterThan(300), "the queue grows 0.6 s per second");
            Assert.That(signal.DeliveredKbps, Is.InRange(470, 530));
        });
    }

    [Test]
    public void LossOnTheUplink_IsMeasured()
    {
        var signal = Run(new TransportFeedbackEstimator(), count: 500, bytes: 1000, spacingUs: 10_000, capacityKbps: 2_000, loss: 0.2);
        Assert.That(signal.LossFraction, Is.InRange(0.12, 0.28));
    }

    private static SendPathSample Sample(long now, RelaySignal? relay = null, TransportSignal? transport = null, int sent = 600) =>
        new(now, sent, 60, 0, 0, false, relay, null, transport);

    [Test]
    public void WithTransportFeedback_TheRelaysUplinkDelay_IsNotCountedAgain()
    {
        // The relay's per-picture uplink delay says 300 ms; per-message feedback on the same leg says it is empty.
        var withFeedback = new SendRateController(600);
        var withoutFeedback = new SendRateController(600);
        for (var now = 0L; now <= 6_000; now += 250)
        {
            var relay = new RelaySignal(now, 0, 300, 0, false);
            withFeedback.Update(Sample(now, relay, new TransportSignal(now, 5, 0, 0, 600)));
            withoutFeedback.Update(Sample(now, relay));
        }
        Assert.Multiple(() =>
        {
            Assert.That(withFeedback.EstimateKbps, Is.GreaterThan(600), "the uplink is measured empty: the path is calm and probed");
            Assert.That(withoutFeedback.EstimateKbps, Is.LessThan(600), "without it the coarse uplink delay is all there is");
        });
    }

    [Test]
    public void AQueueOnTheUplink_CutsTheEstimateToWhatTheUplinkDelivered()
    {
        var controller = new SendRateController(1_200);
        for (var now = 0L; now <= 3_000; now += 250)
            controller.Update(Sample(now, transport: new TransportSignal(now, 200 + (int)now / 5, 400, 0, 500), sent: 1_200));
        Assert.That(controller.EstimateKbps, Is.InRange(250, 500));
    }

    [TestCase(0.01, true)]
    [TestCase(0.05, false)]
    [TestCase(0.15, false)]
    public void UplinkLoss_IsCongestionOnlyWhenHeavy(double loss, bool grows)
    {
        var controller = new SendRateController(600);
        for (var now = 0L; now <= 8_000; now += 250)
            controller.Update(Sample(now, transport: new TransportSignal(now, 0, 0, loss, 600)));
        if (grows) Assert.That(controller.EstimateKbps, Is.GreaterThan(600), "random loss a mobile link always has");
        else if (loss < 0.1) Assert.That(controller.EstimateKbps, Is.EqualTo(600), "some loss: hold, do not probe");
        else Assert.That(controller.EstimateKbps, Is.LessThan(600), "heavy loss: back off");
    }
}
