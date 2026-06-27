using System.Collections.Concurrent;
using XFramework.Core.Patterns;

namespace Communications.Api.Services;

public static class CommunicationsRateLimitActions
{
    public const string MessageCreate = "message-create";
    public const string ReactionCreate = "reaction-create";
    public const string InviteCreate = "invite-create";
    public const string AttachmentLink = "attachment-link";
    public const string ReportCreate = "report-create";
    public const string DirectExternalTransport = "direct-external-transport";
}

public interface ICommunicationsActionRateLimiter
{
    Result Check(
        Guid tenantId,
        Guid credentialId,
        string action,
        int permitLimitPerMinute);
}

public sealed class CommunicationsActionRateLimiter : ICommunicationsActionRateLimiter
{
    private sealed class WindowCounter
    {
        public DateTime WindowStartedAtUtc { get; set; }
        public int Count { get; set; }
    }

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, WindowCounter> counters = new(StringComparer.Ordinal);

    public Result Check(
        Guid tenantId,
        Guid credentialId,
        string action,
        int permitLimitPerMinute)
    {
        if (permitLimitPerMinute <= 0)
            return Result.Success();

        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(action))
            return Result.Failure("Rate-limit context is incomplete", 400);

        var now = DateTime.UtcNow;
        var actorId = credentialId == Guid.Empty ? tenantId : credentialId;
        var key = $"{tenantId:N}:{actorId:N}:{action}";
        var counter = counters.GetOrAdd(key, _ => new WindowCounter
        {
            WindowStartedAtUtc = now,
            Count = 0
        });

        lock (counter)
        {
            if (now - counter.WindowStartedAtUtc >= Window)
            {
                counter.WindowStartedAtUtc = now;
                counter.Count = 0;
            }

            if (counter.Count >= permitLimitPerMinute)
            {
                var retryAfterSeconds = Math.Max(
                    1,
                    (int)Math.Ceiling((counter.WindowStartedAtUtc.Add(Window) - now).TotalSeconds));

                return Result.Failure(
                    $"Communications rate limit exceeded for {action}. Retry after {retryAfterSeconds} seconds.",
                    429);
            }

            counter.Count++;
        }

        TrimExpiredCounters(now);
        return Result.Success();
    }

    private void TrimExpiredCounters(DateTime now)
    {
        if (counters.Count < 10_000)
            return;

        foreach (var (key, counter) in counters)
        {
            if (now - counter.WindowStartedAtUtc > Window.Add(Window))
                counters.TryRemove(key, out _);
        }
    }
}
