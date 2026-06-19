using System.Globalization;
using System.Text.Json;

namespace XFramework.Operations.Dashboard.Services;

public sealed class JaegerTraceClient(
    HttpClient httpClient,
    ILogger<JaegerTraceClient> logger)
{
    public async Task<ExternalDataResult<IReadOnlyList<DashboardTraceSummary>>> GetTracesAsync(
        DashboardTraceQuery query,
        CancellationToken ct = default)
    {
        if (httpClient.BaseAddress is null)
        {
            return ExternalDataResult<IReadOnlyList<DashboardTraceSummary>>.Unavailable(
                [],
                "Jaeger is not configured.");
        }

        try
        {
            using var response = await httpClient.GetAsync(BuildTracesUri(query), ct);
            if (!response.IsSuccessStatusCode)
            {
                return ExternalDataResult<IReadOnlyList<DashboardTraceSummary>>.Unavailable(
                    [],
                    $"Jaeger returned {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var traces = ParseTraces(document.RootElement);

            return ExternalDataResult<IReadOnlyList<DashboardTraceSummary>>.Available(traces);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read Jaeger traces");
            return ExternalDataResult<IReadOnlyList<DashboardTraceSummary>>.Unavailable(
                [],
                "Jaeger traces are unavailable.");
        }
    }

    public static string BuildTracesUri(DashboardTraceQuery query)
    {
        var service = Uri.EscapeDataString(query.ServiceName);
        var lookback = Math.Max(1, (int)Math.Ceiling(query.Lookback.TotalHours));
        var limit = Math.Clamp(query.Limit, 1, 100);

        return $"api/traces?service={service}&lookback={lookback}h&limit={limit.ToString(CultureInfo.InvariantCulture)}";
    }

    public static IReadOnlyList<DashboardTraceSummary> ParseTraces(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return data.EnumerateArray()
            .Select(ParseTrace)
            .Where(trace => trace is not null)
            .Select(trace => trace!)
            .OrderByDescending(trace => trace.StartedAt)
            .ToList();
    }

    private static DashboardTraceSummary? ParseTrace(JsonElement trace)
    {
        var traceId = ReadString(trace, "traceID") ?? "";
        if (string.IsNullOrWhiteSpace(traceId)
            || !trace.TryGetProperty("spans", out var spansElement)
            || spansElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var processServices = ReadProcessServices(trace);
        var spans = new List<RawJaegerSpan>();

        foreach (var span in spansElement.EnumerateArray())
        {
            var spanId = ReadString(span, "spanID") ?? "";
            var operationName = ReadString(span, "operationName") ?? "unknown";
            var processId = ReadString(span, "processID") ?? "";
            var serviceName = processServices.GetValueOrDefault(processId, "unknown");
            var startMicroseconds = ReadInt64(span, "startTime") ?? 0;
            var durationMicroseconds = ReadInt64(span, "duration") ?? 0;

            spans.Add(new RawJaegerSpan(
                spanId,
                operationName,
                serviceName,
                startMicroseconds,
                durationMicroseconds,
                HasError(span)));
        }

        if (spans.Count == 0)
        {
            return null;
        }

        var traceStart = spans.Min(x => x.StartMicroseconds);
        var traceEnd = spans.Max(x => x.StartMicroseconds + x.DurationMicroseconds);
        var startedAt = FromUnixMicroseconds(traceStart);
        var duration = MicrosecondsToTimeSpan(Math.Max(0, traceEnd - traceStart));

        var mappedSpans = spans
            .OrderBy(x => x.StartMicroseconds)
            .Select(span => new DashboardTraceSpan(
                span.SpanId,
                span.OperationName,
                span.ServiceName,
                FromUnixMicroseconds(span.StartMicroseconds),
                MicrosecondsToTimeSpan(span.DurationMicroseconds),
                MicrosecondsToTimeSpan(Math.Max(0, span.StartMicroseconds - traceStart)),
                span.HasError))
            .ToList();

        var rootSpan = spans.OrderByDescending(x => x.DurationMicroseconds).First();

        return new DashboardTraceSummary(
            traceId,
            rootSpan.OperationName,
            rootSpan.ServiceName,
            startedAt,
            duration,
            mappedSpans.Count,
            mappedSpans.Any(x => x.HasError),
            mappedSpans);
    }

    private static Dictionary<string, string> ReadProcessServices(JsonElement trace)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!trace.TryGetProperty("processes", out var processes) || processes.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var process in processes.EnumerateObject())
        {
            var serviceName = ReadString(process.Value, "serviceName");
            if (!string.IsNullOrWhiteSpace(serviceName))
            {
                result[process.Name] = serviceName;
            }
        }

        return result;
    }

    private static bool HasError(JsonElement span)
    {
        if (!span.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var tag in tags.EnumerateArray())
        {
            var key = ReadString(tag, "key");
            if (!string.Equals(key, "error", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "otel.status_code", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = ReadString(tag, "value");
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    private static long? ReadInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        return long.TryParse(ReadString(element, propertyName), CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset FromUnixMicroseconds(long microseconds) =>
        DateTimeOffset.FromUnixTimeMilliseconds(microseconds / 1000);

    private static TimeSpan MicrosecondsToTimeSpan(long microseconds) =>
        TimeSpan.FromTicks(Math.Max(0, microseconds) * 10);

    private sealed record RawJaegerSpan(
        string SpanId,
        string OperationName,
        string ServiceName,
        long StartMicroseconds,
        long DurationMicroseconds,
        bool HasError);
}
