using System.Text.Json;

namespace XFramework.Bolt.Phase0Synthetics;

public static class SyntheticReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(SyntheticReport report)
    {
        SyntheticReportValidator.Validate(report);
        return JsonSerializer.Serialize(report, SerializerOptions);
    }

    public static async Task WriteAsync(TextWriter writer, SyntheticReport report) =>
        await writer.WriteLineAsync(Serialize(report));
}
