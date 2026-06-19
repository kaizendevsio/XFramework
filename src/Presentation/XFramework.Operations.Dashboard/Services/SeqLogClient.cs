using System.Globalization;
using System.Text.Json;

namespace XFramework.Operations.Dashboard.Services;

public sealed class SeqLogClient(
    HttpClient httpClient,
    ILogger<SeqLogClient> logger)
{
    public async Task<ExternalDataResult<IReadOnlyList<DashboardLogEvent>>> GetLogsAsync(
        DashboardLogQuery query,
        CancellationToken ct = default)
    {
        if (httpClient.BaseAddress is null)
        {
            return ExternalDataResult<IReadOnlyList<DashboardLogEvent>>.Unavailable(
                [],
                "Seq is not configured.");
        }

        try
        {
            var uri = BuildEventsUri(query);
            using var response = await httpClient.GetAsync(uri, ct);

            if (!response.IsSuccessStatusCode)
            {
                return ExternalDataResult<IReadOnlyList<DashboardLogEvent>>.Unavailable(
                    [],
                    $"Seq returned {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var events = ParseEvents(document.RootElement);

            return ExternalDataResult<IReadOnlyList<DashboardLogEvent>>.Available(events);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read Seq events");
            return ExternalDataResult<IReadOnlyList<DashboardLogEvent>>.Unavailable(
                [],
                "Seq events are unavailable.");
        }
    }

    public static string BuildEventsUri(DashboardLogQuery query)
    {
        var count = Math.Clamp(query.Count, 1, 500);
        var parts = new List<string>
        {
            $"count={count.ToString(CultureInfo.InvariantCulture)}",
            "render=true",
            $"fromDateUtc={Uri.EscapeDataString(query.FromUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}",
            $"toDateUtc={Uri.EscapeDataString(query.ToUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}"
        };

        var filter = BuildFilter(query.Application, query.MachineName);
        if (!string.IsNullOrWhiteSpace(filter))
        {
            parts.Add($"filter={Uri.EscapeDataString(filter)}");
        }

        return "api/events?" + string.Join('&', parts);
    }

    public static string BuildFilter(string? application, string? machineName)
    {
        var filters = new List<string>();

        if (!string.IsNullOrWhiteSpace(application))
        {
            filters.Add($"Application = '{EscapeSeqString(application)}'");
        }

        if (!string.IsNullOrWhiteSpace(machineName))
        {
            filters.Add($"MachineName = '{EscapeSeqString(machineName)}'");
        }

        return string.Join(" and ", filters);
    }

    private static string EscapeSeqString(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    public static IReadOnlyList<DashboardLogEvent> ParseEvents(JsonElement root)
    {
        IEnumerable<JsonElement> source = root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray(),
            JsonValueKind.Object when TryGetProperty(root, "Events", out var upperEvents) && upperEvents.ValueKind == JsonValueKind.Array
                => upperEvents.EnumerateArray(),
            JsonValueKind.Object when TryGetProperty(root, "events", out var lowerEvents) && lowerEvents.ValueKind == JsonValueKind.Array
                => lowerEvents.EnumerateArray(),
            _ => []
        };

        return source.Select(ParseEvent).ToList();
    }

    private static DashboardLogEvent ParseEvent(JsonElement element)
    {
        var properties = ReadProperties(element);

        var timestamp = ReadDateTimeOffset(element, "Timestamp")
            ?? ReadDateTimeOffset(element, "@t")
            ?? DateTimeOffset.MinValue;

        var level = ReadString(element, "Level")
            ?? ReadString(element, "@l")
            ?? "Information";

        var message = ReadString(element, "RenderedMessage")
            ?? ReadString(element, "Message")
            ?? ReadString(element, "@m")
            ?? ReadString(element, "@mt")
            ?? "";

        var sourceContext = ReadString(element, "SourceContext")
            ?? properties.GetValueOrDefault("SourceContext");

        var application = ReadString(element, "Application")
            ?? properties.GetValueOrDefault("Application");

        var machineName = ReadString(element, "MachineName")
            ?? properties.GetValueOrDefault("MachineName");

        return new DashboardLogEvent(
            timestamp,
            level,
            message,
            sourceContext,
            application,
            machineName,
            properties);
    }

    private static Dictionary<string, string> ReadProperties(JsonElement element)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!TryGetProperty(element, "Properties", out var props))
        {
            return properties;
        }

        if (props.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in props.EnumerateObject())
            {
                properties[property.Name] = JsonValueToString(property.Value);
            }
        }
        else if (props.ValueKind == JsonValueKind.Array)
        {
            foreach (var property in props.EnumerateArray())
            {
                var name = ReadString(property, "Name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var value = TryGetProperty(property, "Value", out var valueElement)
                    ? JsonValueToString(valueElement)
                    : "";
                properties[name] = value;
            }
        }

        return properties;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : JsonValueToString(value);
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string propertyName)
    {
        var raw = ReadString(element, propertyName);
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value.ToUniversalTime()
            : null;
    }

    private static string JsonValueToString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "",
            _ => value.GetRawText()
        };
}
