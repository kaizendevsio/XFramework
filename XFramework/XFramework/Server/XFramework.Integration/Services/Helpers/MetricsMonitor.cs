using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace XFramework.Integration.Services.Helpers;


public class MetricsMonitor(ILogger<MetricsMonitor> logger)
{
    public TimingInfo Start(string? message = null, [CallerMemberName] string? callerName = null)
    {
        logger.LogInformation("[{Caller}] {Message}", callerName, message);
        return new TimingInfo(logger, callerName, message);
    }

    public class TimingInfo : IDisposable
    {
        private readonly ILogger<MetricsMonitor> _logger;
        private readonly string? _callerName;
        private readonly string _message;
        private readonly Stopwatch _stopwatch;
        private bool _isDisposed = false;

        internal TimingInfo(ILogger<MetricsMonitor> logger, string? callerName, string message)
        {
            _logger = logger;
            _callerName = callerName;
            _stopwatch = Stopwatch.StartNew();
            _message = message;
        }

        public void Dispose()
        {
            Completed(_message);
        }

        public void Completed(string? message = null)
        {
            LogElapsedTime("completed", string.IsNullOrEmpty(message) ? _message : message);
        }
        
        public void Cancelled(string message)
        {
            LogElapsedTime("cancelled", message);
        }

        public void Failed(string message)
        {
            LogElapsedTime("failed", message);
        }

        private void LogElapsedTime(string status, string message)
        {
            if (_isDisposed) return;
            
            _stopwatch.Stop();
            _logger.LogInformation("[{Caller}] {Message} {Status} in {ElapsedTimeTotalMilliseconds}ms", _callerName, message, status, _stopwatch.Elapsed.TotalMilliseconds);
            _isDisposed = true;
        }
    }
}