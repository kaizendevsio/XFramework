using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ZLogger;

namespace ControlPanel.Server.Services;

public static class ZLoggerSeqSink
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

    private sealed class SeqBatchProcessor : IAsyncLogProcessor, IAsyncDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly LogLevel _minimumLevel;
        private static readonly MediaTypeHeaderValue ClefMediaType = new("application/vnd.serilog.clef");

        public SeqBatchProcessor(HttpClient httpClient, LogLevel minimumLevel)
        {
            _httpClient = httpClient;
            _minimumLevel = minimumLevel;
        }

        public void Post(IZLoggerEntry entry)
        {
            if (entry.LogInfo.LogLevel < _minimumLevel) return;

            var clef = FormatClef(entry);
            _ = SendAsync(clef);
        }

        private async Task SendAsync(string clef)
        {
            try
            {
                var content = new StringContent(clef, Encoding.UTF8);
                content.Headers.ContentType = ClefMediaType;
                await _httpClient.PostAsync("/api/events/raw?clef", content);
            }
            catch
            {
                // Fire and forget
            }
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
            var buffer = new ArrayBufferWriter<byte>();
            var w = new Utf8JsonWriter(buffer);
            w.WriteStartObject();

            w.WriteString("@t", entry.LogInfo.Timestamp.Utc.ToString("O"));
            w.WriteString("@l", entry.LogInfo.LogLevel.ToString());

            var rendered = entry.ToString();
            w.WriteString("@mt", rendered);

            var category = entry.LogInfo.Category.ToString();
            if (!string.IsNullOrEmpty(category))
                w.WriteString("SourceContext", category);

            if (entry.LogInfo.EventId.Id != 0)
            {
                w.WriteNumber("EventId", entry.LogInfo.EventId.Id);
                if (!string.IsNullOrEmpty(entry.LogInfo.EventId.Name))
                    w.WriteString("EventName", entry.LogInfo.EventId.Name);
            }

            // Write structured parameters as individual CLEF properties
            try
            {
                entry.WriteJsonParameterKeyValues(w, ParamJsonOptions);
            }
            catch
            {
                // Some entries may not support parameter extraction
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
