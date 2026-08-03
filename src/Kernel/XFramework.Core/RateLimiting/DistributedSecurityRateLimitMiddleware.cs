using Microsoft.AspNetCore.Http;

namespace XFramework.Core.RateLimiting;

public sealed class DistributedSecurityRateLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IDistributedSecurityRateLimiter rateLimiter)
    {
        if (!StrictSecurityRateLimitPolicyMap.TryResolve(context.Request, out var policy))
        {
            await next(context);
            return;
        }

        DistributedSecurityRateLimitDecision decision;
        try
        {
            decision = await rateLimiter.AcquireAsync(
                policy,
                StrictSecurityRateLimitPolicyMap.CreateClientKey(context),
                context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            decision = DistributedSecurityRateLimitDecision.Rejected(TimeSpan.Zero);
        }

        if (decision.IsAllowed)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";
        if (decision.RetryAfter > TimeSpan.Zero)
        {
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        await context.Response.WriteAsJsonAsync(
            new { message = "Too many requests." },
            context.RequestAborted);
    }
}
