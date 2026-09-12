using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace Yap.Client.Services;

// Record only level, event ID and exception type; framework log messages can
// contain user input, URLs or request data and must not enter a shared report.
public sealed class DiagnosticsLoggerProvider(IJSRuntime js) : ILoggerProvider
{
    private int writing;
    public ILogger CreateLogger(string categoryName) => new DiagnosticLogger(this);
    public void Dispose() { }
    private async Task WriteAsync(LogLevel level, EventId id, Exception? error)
    {
        if (Interlocked.Exchange(ref writing, 1) != 0) return;
        try { await js.InvokeVoidAsync("yap.diagnostics.record", "dotnet.log", new { phase = level.ToString(), eventId = id.Id, type = error?.GetType().FullName }); }
        catch { /* Logging must not create another failure. */ }
        finally { Interlocked.Exchange(ref writing, 0); }
    }
    private sealed class DiagnosticLogger(DiagnosticsLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning && level != LogLevel.None;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (IsEnabled(level)) _ = provider.WriteAsync(level, id, exception); }
    }
}
