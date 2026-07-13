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
}
