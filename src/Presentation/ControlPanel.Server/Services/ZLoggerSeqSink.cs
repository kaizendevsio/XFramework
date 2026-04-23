using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using ZLogger;

namespace ControlPanel.Server.Services;

/// <summary>
/// High-performance ZLogger → Seq sink using Channel-based batching.
/// Accumulates CLEF events and flushes in batches (newline-delimited)
/// via a single HTTP POST — same approach as Serilog.Sinks.Seq.
/// </summary>
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
        private readonly Channel<string> _channel;
        private readonly Task _flushTask;
        private readonly CancellationTokenSource _cts = new();

        private const int MaxBatchSize = 100;
        private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(500);
        private static readonly MediaTypeHeaderValue ClefMediaType = new("application/vnd.serilog.clef");

        [GeneratedRegex(@"(?:RequestBody|ResponseBody|Request|Response)=\{.*?\}(?=\s|$)", RegexOptions.Compiled)]
        private static partial Regex BodyJsonPattern();

        public SeqBatchProcessor(HttpClient httpClient, LogLevel minimumLevel)
        {
            _httpClient = httpClient;
            _minimumLevel = minimumLevel;
            _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
            _flushTask = Task.Run(FlushLoopAsync);
        }

        public void Post(IZLoggerEntry entry)
        {
            if (entry.LogInfo.LogLevel < _minimumLevel) return;
            var clef = FormatClef(entry);
            _channel.Writer.TryWrite(clef);
        }

        private async Task FlushLoopAsync()
        {
            var batch = new List<string>(MaxBatchSize);
            var reader = _channel.Reader;

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    // Wait for at least one item
                    if (await reader.WaitToReadAsync(_cts.Token))
                    {
                        batch.Clear();

                        // Drain up to MaxBatchSize
                        while (batch.Count < MaxBatchSize && reader.TryRead(out var item))
                            batch.Add(item);

                        if (batch.Count > 0)
                            await SendBatchAsync(batch);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(1000); }
            }

            // Drain remaining on shutdown
            batch.Clear();
            while (reader.TryRead(out var item))
                batch.Add(item);
            if (batch.Count > 0)
                await SendBatchAsync(batch);
        }

        private async Task SendBatchAsync(List<string> batch)
        {
            try
            {
                var payload = string.Join('\n', batch);
                var content = new StringContent(payload, Encoding.UTF8);
                content.Headers.ContentType = ClefMediaType;
                await _httpClient.PostAsync("/api/events/raw?clef", content);
            }
            catch { /* Seq down — drop batch, loop will retry next batch */ }
        }

        private static readonly JsonSerializerOptions ParamJsonOptions = new()
        {
            WriteIndented = false,
            MaxDepth = 8,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private static string FormatClef(IZLoggerEntry entry)
        {
            // Extract structured params
            var paramBuffer = new ArrayBufferWriter<byte>(256);
            var paramWriter = new Utf8JsonWriter(paramBuffer);
            paramWriter.WriteStartObject();
            try { entry.WriteJsonParameterKeyValues(paramWriter, ParamJsonOptions); } catch { }
            paramWriter.WriteEndObject();
            paramWriter.Flush();

            JsonElement paramProps;
            try { paramProps = JsonDocument.Parse(paramBuffer.WrittenMemory).RootElement; }
            catch { paramProps = default; }

            // Build final CLEF
            var buffer = new ArrayBufferWriter<byte>(512);
            var w = new Utf8JsonWriter(buffer);
            w.WriteStartObject();

            w.WriteString("@t", entry.LogInfo.Timestamp.Utc.ToString("O"));
            w.WriteString("@l", entry.LogInfo.LogLevel.ToString());

            var rendered = entry.ToString();
            var cleanMessage = BodyJsonPattern().Replace(rendered, m =>
                m.Value[..m.Value.IndexOf('=')] + "={…}");
            w.WriteString("@mt", cleanMessage);

            var category = entry.LogInfo.Category.ToString();
            if (!string.IsNullOrEmpty(category))
                w.WriteString("SourceContext", category);

            if (entry.LogInfo.EventId.Id != 0)
                w.WriteNumber("EventId", entry.LogInfo.EventId.Id);

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

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _channel.Writer.Complete();
            await _flushTask;
            _httpClient.Dispose();
            _cts.Dispose();
        }
    }
}
