using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace XFramework.Core.Middlewares;

public sealed class BoltAccessTokenRedactionMiddleware(RequestDelegate next)
{
    private const string AccessTokenParameter = "access_token";
    private static readonly PathString BoltWebSocketPath = new("/bolt/ws");
    private static readonly object AccessTokenItemKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(BoltWebSocketPath)
            || !context.Request.Query.TryGetValue(AccessTokenParameter, out var accessToken))
        {
            await next(context);
            return;
        }

        context.Items[AccessTokenItemKey] = accessToken.ToString();

        var sanitizedQueryString = QueryString.Create(
            context.Request.Query.Where(parameter =>
                !string.Equals(parameter.Key, AccessTokenParameter, StringComparison.OrdinalIgnoreCase)));

        context.Request.QueryString = sanitizedQueryString;
        SanitizeRawTarget(context, sanitizedQueryString);
        SanitizeCurrentActivity(context);

        try
        {
            await next(context);
        }
        finally
        {
            context.Items.Remove(AccessTokenItemKey);
        }
    }

    public static string? GetAccessToken(HttpContext context)
    {
        if (!context.Items.TryGetValue(AccessTokenItemKey, out var value))
        {
            return null;
        }

        return value as string;
    }

    private static void SanitizeRawTarget(HttpContext context, QueryString sanitizedQueryString)
    {
        var requestFeature = context.Features.Get<IHttpRequestFeature>();
        if (requestFeature?.RawTarget is not { } rawTarget)
        {
            return;
        }

        var queryStart = rawTarget.IndexOf('?', StringComparison.Ordinal);
        if (queryStart >= 0)
        {
            requestFeature.RawTarget = rawTarget[..queryStart] + sanitizedQueryString.Value;
        }
    }

    private static void SanitizeCurrentActivity(HttpContext context)
    {
        if (Activity.Current is not { } activity)
        {
            return;
        }

        var request = context.Request;
        var query = request.QueryString.HasValue
            ? request.QueryString.Value![1..]
            : string.Empty;
        var target = context.Features.Get<IHttpRequestFeature>()?.RawTarget
            ?? request.PathBase + request.Path + request.QueryString;
        var url = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}{request.QueryString}";

        activity.SetTag("url.query", query);
        ReplaceTagIfPresent(activity, "url.full", url);
        ReplaceTagIfPresent(activity, "http.target", target);
        ReplaceTagIfPresent(activity, "http.url", url);
    }

    private static void ReplaceTagIfPresent(Activity activity, string name, string value)
    {
        if (activity.TagObjects.Any(tag => string.Equals(tag.Key, name, StringComparison.Ordinal)))
        {
            activity.SetTag(name, value);
        }
    }
}
