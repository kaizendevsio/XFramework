using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ZLogger;

namespace ControlPanel.Server.Services;

public static partial class ZLoggerSeqSink
{
    public static void Register(ILoggingBuilder logging, string seqUrl, string? apiKey = null, LogLevel minimumLevel = LogLevel.Debug)
    {
        logging.AddZLoggerLogProcessor(options =>
        {
            var httpClient = new HttpClient { BaseAddress = new Uri(seqUrl.TrimEnd('/')) };
            if (!string.IsNullOrEmpty(apiKey))
                httpClient.DefaultRequestHeaders.Add("X-Seq-ApiKey", apiKey);

            return new SeqBatchProcessor(httpClient, minimumLevel);
        });
    }

    private sealed partial class SeqBatchProcessor : IAsyncLogProcessor, IAsyncDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly LogLevel _minimumLevel;
        private static readonly MediaTypeHeaderValue ClefMediaType = new("application/vnd.serilog.clef");

        // Strips inline JSON blobs from rendered message for cleaner @mt
        // Matches: RequestBody={...} or ResponseBody={...} (including nested braces)
        [GeneratedRegex(@"(?:RequestBody|ResponseBody)=\{.*?\}(?=\s|$)", RegexOptions.Compiled)]
        private static partial Regex BodyJsonPattern();

        public SeqBatchProcessor(HttpClient httpClient, LogLevel minimumLevel)
        {
            _httpClient = httpClient;
            _minimumLevel = minimumLevel;
        }

        public void Post(IZLoggerEntry entry)
        {
            if (entry.LogInfo.LogLevel < _minimumLevel) return;
            _ = SendAsync(FormatClef(entry));
        }

        private async Task SendAsync(string clef)
        {
            try
            {
                var content = new StringContent(clef, Encoding.UTF8);
                content.Headers.ContentType = ClefMediaType;
                await _httpClient.PostAsync("/api/events/raw?clef", content);
            }
            catch { /* Fire and forget */ }
        }

        private static readonly JsonSerializerOptions ParamJsonOptions = new()
        {
            WriteIndented = false,
            MaxDepth = 4,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private static string FormatClef(IZLoggerEntry entry)
        {
            // Step 1: Write structured params to a temp buffer so we can inspect them
            var paramBuffer = new ArrayBufferWriter<byte>();
            var paramWriter = new Utf8JsonWriter(paramBuffer);
            paramWriter.WriteStartObject();
            try { entry.WriteJsonParameterKeyValues(paramWriter, ParamJsonOptions); } catch { }
            paramWriter.WriteEndObject();
            paramWriter.Flush();

            // Parse params to rewrite into the final CLEF event
            JsonElement paramProps;
            try { paramProps = JsonDocument.Parse(paramBuffer.WrittenMemory).RootElement; }
            catch { paramProps = default; }

            // Step 2: Build the final CLEF event
            var buffer = new ArrayBufferWriter<byte>();
            var w = new Utf8JsonWriter(buffer);
            w.WriteStartObject();

            w.WriteString("@t", entry.LogInfo.Timestamp.Utc.ToString("O"));
            w.WriteString("@l", entry.LogInfo.LogLevel.ToString());

            // Clean @mt: strip inline JSON blobs (RequestBody/ResponseBody values)
            // so Seq shows a clean message. The full data lives in structured properties.
            var rendered = entry.ToString();
            var cleanMessage = BodyJsonPattern().Replace(rendered, m =>
                m.Value[..m.Value.IndexOf('=')] + "={...}");
            w.WriteString("@mt", cleanMessage);

            var category = entry.LogInfo.Category.ToString();
            if (!string.IsNullOrEmpty(category))
                w.WriteString("SourceContext", category);

            if (entry.LogInfo.EventId.Id != 0)
            {
                w.WriteNumber("EventId", entry.LogInfo.EventId.Id);
                if (!string.IsNullOrEmpty(entry.LogInfo.EventId.Name))
                    w.WriteString("EventName", entry.LogInfo.EventId.Name);
            }

            // Write each structured parameter as a top-level CLEF property
            if (paramProps.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in paramProps.EnumerateObject())
                {
                    w.WritePropertyName(prop.Name);
                    prop.Value.WriteTo(w);
                }
            }

            w.WriteEndObject();
            w.Flush();
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        public ValueTask DisposeAsync()
        {
            _httpClient.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
