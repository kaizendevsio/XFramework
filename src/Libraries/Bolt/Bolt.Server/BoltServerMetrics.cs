using System.Diagnostics;
using System.Diagnostics.Metrics;
using Bolt.Protocol;

namespace Bolt.Server;

internal static class BoltServerMetrics
{
    public const string MeterName = "Bolt.Server";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RegistrationRejections =
        Meter.CreateCounter<long>("bolt.server.registration.rejections");
    private static readonly Counter<long> OversizedFrameRejections =
        Meter.CreateCounter<long>("bolt.server.frame.oversized_rejections");
    private static readonly Counter<long> QuotaRejections =
        Meter.CreateCounter<long>("bolt.server.quota.rejections");
    private static readonly Counter<long> PlaintextRejections =
        Meter.CreateCounter<long>("bolt.server.transport.plaintext_rejections");
    private static readonly Counter<long> DisabledMediaRejections =
        Meter.CreateCounter<long>("bolt.server.media.disabled_rejections");
    private static readonly Counter<long> RouteMisses =
        Meter.CreateCounter<long>("bolt.server.route.misses");
    private static readonly Counter<long> TransportSendFailures =
        Meter.CreateCounter<long>("bolt.server.transport.send_failures");
    private static readonly Counter<long> RequestCancellations =
        Meter.CreateCounter<long>("bolt.server.rpc.cancellations");
    private static readonly Counter<long> RequestRateRejections =
        Meter.CreateCounter<long>("bolt.server.rate_limit.request_rejections");
    private static readonly Counter<long> ByteRateRejections =
        Meter.CreateCounter<long>("bolt.server.rate_limit.byte_rejections");
    private static readonly Counter<long> PushRateRejections =
        Meter.CreateCounter<long>("bolt.server.rate_limit.push_rejections");
    private static readonly Histogram<double> RpcDuration =
        Meter.CreateHistogram<double>("bolt.server.rpc.duration", "ms");
    private static readonly Histogram<long> ReplayDeferredBytes =
        Meter.CreateHistogram<long>("bolt.server.replay.deferred_bytes", "By");

    public static void RecordRegistrationRejection(string reason) =>
        RegistrationRejections.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public static void RecordOversizedFrameRejection(string stage) =>
        OversizedFrameRejections.Add(1, new KeyValuePair<string, object?>("stage", stage));

    public static void RecordQuotaRejection(string resource) =>
        QuotaRejections.Add(1, new KeyValuePair<string, object?>("resource", resource));

    public static void RecordPlaintextRejection() => PlaintextRejections.Add(1);

    public static void RecordDisabledMediaRejection(FrameType frameType) =>
        DisabledMediaRejections.Add(
            1,
            new KeyValuePair<string, object?>("frame_type", frameType.ToString()));

    public static void RecordRouteMiss(string frameType) =>
        RouteMisses.Add(1, new KeyValuePair<string, object?>("frame_type", frameType));

    public static void RecordTransportSendFailure(string reason) =>
        TransportSendFailures.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public static void RecordRequestCancellation() => RequestCancellations.Add(1);

    public static void RecordRateLimitRejection(string frameCategory, string reason, bool isPush)
    {
        var tags = new TagList
        {
            { "frame_category", frameCategory },
            { "reason", reason }
        };

        if (reason == "request_rate")
        {
            RequestRateRejections.Add(1, tags);
        }
        else
        {
            ByteRateRejections.Add(1, tags);
        }

        if (isPush)
        {
            PushRateRejections.Add(1, tags);
        }
    }

    public static void RecordRpcDuration(long elapsedMilliseconds, string outcome) =>
        RpcDuration.Record(elapsedMilliseconds, new KeyValuePair<string, object?>("outcome", outcome));

    public static void RecordReplayDeferredBytes(long bytes) => ReplayDeferredBytes.Record(bytes);
}

internal readonly record struct BoltRateLimitRejectionTotals(
    long RequestRate,
    long ByteRate,
    long PushRate);
