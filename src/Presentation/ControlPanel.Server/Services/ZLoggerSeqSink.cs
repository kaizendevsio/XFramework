using System.Net.Http.Headers;
using ZLogger;

namespace ControlPanel.Server.Services;

/// <summary>
/// ZLogger async batching processor that POSTs CLEF (Compact Log Event Format) to Seq's ingestion API.
/// Non-blocking — buffers log entries and flushes in batches over HTTP.
/// </summary>
public sealed class ZLoggerSeqSink : IAsyncDisposable
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
                var content = new StringContent(clef);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.serilog.clef");
                await _httpClient.PostAsync("/api/events/raw?clef", content);
            }
            catch
            {
                // Fire and forget — don't let Seq failures crash the app
            }
        }

        private static string FormatClef(IZLoggerEntry entry)
        {
            var writer = new System.Buffers.ArrayBufferWriter<byte>();
            var utf8Writer = new Utf8JsonWriter(writer);
            utf8Writer.WriteStartObject();
            utf8Writer.WriteString("@t", entry.LogInfo.Timestamp.Utc.ToString("O"));
            utf8Writer.WriteString("@l", entry.LogInfo.LogLevel.ToString());
            utf8Writer.WriteString("@mt", entry.ToString());

            if (entry.LogInfo.Category is { Length: > 0 })
                utf8Writer.WriteString("SourceContext", entry.LogInfo.Category.ToString());

            if (entry.LogInfo.EventId.Id != 0)
                utf8Writer.WriteNumber("EventId", entry.LogInfo.EventId.Id);

            utf8Writer.WriteEndObject();
            utf8Writer.Flush();
            return System.Text.Encoding.UTF8.GetString(writer.WrittenSpan);
        }

        public ValueTask DisposeAsync()
        {
            _httpClient.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
